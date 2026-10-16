using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Email;

namespace YemeniBreeze.Tests;

/// <summary>Events with RequiresApproval: signups wait as Pending, and only the admin's decision sends email.</summary>
public class RegistrationApprovalTests(EmailApiFactory factory) : IClassFixture<EmailApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private EmailDispatcher Dispatcher => factory.Services.GetRequiredService<EmailDispatcher>();

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<Event> CreateEventAsync(int capacity = 10, bool requiresApproval = true) => WithDbAsync(async db =>
    {
        var ev = new Event
        {
            Slug = $"approval-{Guid.NewGuid():N}", TitleEn = "Approval Test", Location = "Amsterdam",
            Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), StartTime = "18:00", EndTime = "21:00",
            Capacity = capacity, Status = EventStatus.Published, IsRegistrationOpen = true,
            RequiresApproval = requiresApproval
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    });

    private async Task<(int Id, string Status)> RegisterAsync(string slug, string email, int guests = 1)
    {
        var response = await _client.PostAsJsonAsync($"/api/events/{slug}/register", new
        {
            FullName = "Approval Person", Email = email, GuestsCount = guests, Language = "en",
            Phone = "+3160000000", Country = "Yemen", Reason = "Testing",
            GuestNames = guests > 1 ? "A Friend" : null
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (result.GetProperty("id").GetInt32(), result.GetProperty("status").GetString()!);
    }

    private Task<HttpResponseMessage> SetStatusAsync(int eventId, int id, string status) =>
        _client.PostAsJsonAsync($"/api/admin/events/{eventId}/registrations/{id}/status", new { Status = status });

    private async Task DrainAsync()
    {
        for (var i = 0; i < 20 && await Dispatcher.ProcessDueAsync(DateTime.UtcNow.AddSeconds(1)) > 0; i++) { }
    }

    private Task<List<OutgoingEmail>> EmailsForAsync(string email) => WithDbAsync(db =>
        db.OutgoingEmails.Where(o => o.Registration!.Email == email).OrderBy(o => o.Id).ToListAsync());

    private async Task AuthenticateAsync()
    {
        if (_client.DefaultRequestHeaders.Authorization is not null) return;
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@yemenbreeze.nl", password = "ChangeMe!2026" });
        var login = await response.Content.ReadFromJsonAsync<JsonElement>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());
    }

    [Fact]
    public async Task Signup_waits_as_pending_and_sends_no_email()
    {
        var ev = await CreateEventAsync();

        var (_, status) = await RegisterAsync(ev.Slug, "pending@approval.test");
        await DrainAsync();

        Assert.Equal("Pending", status);
        Assert.Empty(await EmailsForAsync("pending@approval.test"));
        Assert.DoesNotContain(factory.Resend.Sent, s => s.To == "pending@approval.test");
    }

    [Fact]
    public async Task Accepting_sends_the_ticket()
    {
        await AuthenticateAsync();
        var ev = await CreateEventAsync();
        var (id, _) = await RegisterAsync(ev.Slug, "accepted@approval.test");

        (await SetStatusAsync(ev.Id, id, "Confirmed")).EnsureSuccessStatusCode();
        await DrainAsync();

        var email = Assert.Single(await EmailsForAsync("accepted@approval.test"));
        Assert.Equal(EmailKind.Confirmed, email.Kind);
        Assert.Contains(factory.Resend.Sent, s => s.To == "accepted@approval.test" && s.Subject.StartsWith("You're in"));
    }

    [Fact]
    public async Task Rejecting_sends_one_polite_email_and_blocks_signing_up_again()
    {
        await AuthenticateAsync();
        var ev = await CreateEventAsync();
        var (id, _) = await RegisterAsync(ev.Slug, "rejected@approval.test");

        (await SetStatusAsync(ev.Id, id, "Rejected")).EnsureSuccessStatusCode();
        (await SetStatusAsync(ev.Id, id, "Rejected")).EnsureSuccessStatusCode(); // a double click
        await DrainAsync();

        var email = Assert.Single(await EmailsForAsync("rejected@approval.test"));
        Assert.Equal(EmailKind.Rejected, email.Kind);
        Assert.Contains(factory.Resend.Sent, s => s.To == "rejected@approval.test" && s.Subject.StartsWith("About your registration"));

        var again = await _client.PostAsJsonAsync($"/api/events/{ev.Slug}/register", new
        {
            FullName = "Approval Person", Email = "rejected@approval.test", GuestsCount = 1,
            Phone = "+3160000000", Country = "Yemen", Reason = "Testing"
        });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public void Rejected_email_has_no_ticket_or_cancel_link()
    {
        var registration = new Registration
        {
            FullName = "Sara", Email = "sara@example.com", Status = RegistrationStatus.Rejected,
            Language = "en", ManageToken = Guid.NewGuid()
        };
        var ev = new Event { Slug = "dinner", TitleEn = "Dinner", Date = new DateOnly(2031, 1, 1) };

        var email = RegistrationEmails.Build(registration, ev, EmailKind.Rejected, "https://yemenbreeze.nl")!;

        Assert.DoesNotContain("/registrations/", email.Html);
        Assert.DoesNotContain(email.Attachments, a => a.FileName == "ticket.png");
        Assert.Null(RegistrationEmails.Build(registration, ev, EmailKind.Confirmed, "https://yemenbreeze.nl"));
    }

    [Fact]
    public async Task Admin_can_accept_past_capacity()
    {
        await AuthenticateAsync();
        var ev = await CreateEventAsync(capacity: 1);
        var (first, _) = await RegisterAsync(ev.Slug, "first@approval.test");
        var (second, _) = await RegisterAsync(ev.Slug, "second@approval.test", guests: 2);

        (await SetStatusAsync(ev.Id, first, "Confirmed")).EnsureSuccessStatusCode();
        (await SetStatusAsync(ev.Id, second, "Confirmed")).EnsureSuccessStatusCode();

        var confirmed = await WithDbAsync(db => db.Registrations
            .Where(r => r.EventId == ev.Id && r.Status == RegistrationStatus.Confirmed).SumAsync(r => r.GuestsCount));
        Assert.Equal(3, confirmed);
    }

    [Fact]
    public async Task Pending_registrations_have_no_email_to_resend()
    {
        await AuthenticateAsync();
        var ev = await CreateEventAsync();
        var (id, _) = await RegisterAsync(ev.Slug, "resend-pending@approval.test");

        var resend = await _client.PostAsync($"/api/admin/events/{ev.Id}/registrations/{id}/resend-email", null);

        Assert.Equal(HttpStatusCode.BadRequest, resend.StatusCode);
    }

    [Fact]
    public async Task Events_without_approval_still_confirm_right_away()
    {
        var ev = await CreateEventAsync(requiresApproval: false);

        var (_, status) = await RegisterAsync(ev.Slug, "instant@approval.test");

        Assert.Equal("Confirmed", status);
        Assert.Single(await EmailsForAsync("instant@approval.test"));
    }

    [Fact]
    public async Task Event_shows_the_approval_setting_and_pending_count()
    {
        var ev = await CreateEventAsync();
        await RegisterAsync(ev.Slug, "count-a@approval.test");
        await RegisterAsync(ev.Slug, "count-b@approval.test", guests: 3);

        var dto = await _client.GetFromJsonAsync<JsonElement>($"/api/events/{ev.Slug}");

        Assert.True(dto.GetProperty("requiresApproval").GetBoolean());
        Assert.Equal(4, dto.GetProperty("pendingCount").GetInt32());
        Assert.Equal(10, dto.GetProperty("spotsLeft").GetInt32()); // pending people don't take seats
    }
}
