using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Email;

namespace YemeniBreeze.Tests;

/// <summary>Stands in for the Resend API. Replies 200 unless a status was scripted for that recipient.</summary>
public sealed class ScriptedResendHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<HttpStatusCode>> _scripted = new();
    public ConcurrentBag<(string To, string Subject)> Sent { get; } = [];

    public void FailNext(string to, params HttpStatusCode[] statuses) =>
        _scripted[to] = new ConcurrentQueue<HttpStatusCode>(statuses);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
        var to = json.RootElement.GetProperty("to")[0].GetString()!;
        if (_scripted.TryGetValue(to, out var queue) && queue.TryDequeue(out var status))
            return new HttpResponseMessage(status) { Content = new StringContent("{\"message\":\"scripted\"}") };

        Sent.Add((to, json.RootElement.GetProperty("subject").GetString()!));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"ok\"}") };
    }
}

/// <summary>An API with Resend "configured" against <see cref="ScriptedResendHandler"/>.</summary>
public class EmailApiFactory : ApiFactory
{
    public ScriptedResendHandler Resend { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Email:Provider", "resend");
        builder.UseSetting("Email:ResendApiKey", "re_test_key");
        builder.UseSetting("Email:SendIntervalMs", "0");
        builder.UseSetting("Email:PublicBaseUrl", "https://yemenbreeze.nl");
        builder.ConfigureServices(services =>
            services.AddHttpClient(EmailService.ResendHttpClient).ConfigurePrimaryHttpMessageHandler(() => Resend));
    }
}

public class EmailOutboxTests(EmailApiFactory factory) : IClassFixture<EmailApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private EmailDispatcher Dispatcher => factory.Services.GetRequiredService<EmailDispatcher>();

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<Event> CreateEventAsync(int capacity, DateOnly? date = null) => WithDbAsync(async db =>
    {
        var ev = new Event
        {
            Slug = $"mail-{Guid.NewGuid():N}", TitleEn = "Mail Test", Location = "Rotterdam",
            Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            StartTime = "18:00", EndTime = "21:00",
            Capacity = capacity, Status = EventStatus.Published, IsRegistrationOpen = true
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    });

    private Task<HttpResponseMessage> RegisterAsync(string slug, string email, int guests = 1, string? website = null) =>
        _client.PostAsJsonAsync($"/api/events/{slug}/register",
            new
            {
                FullName = "Mail Person", Email = email, GuestsCount = guests, Language = "en", Website = website,
                Phone = "+3160000000", Country = "Netherlands", Reason = "Curious",
                GuestNames = guests > 1 ? "A Friend" : null
            });

    private async Task DrainAsync(DateTime? now = null)
    {
        for (var i = 0; i < 20 && await Dispatcher.ProcessDueAsync(now ?? DateTime.UtcNow.AddSeconds(1)) > 0; i++) { }
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
    public async Task Registration_queues_a_ticket_email_that_the_dispatcher_sends()
    {
        var ev = await CreateEventAsync(capacity: 10);
        var response = await RegisterAsync(ev.Slug, "sent@mail.test");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var queued = Assert.Single(await EmailsForAsync("sent@mail.test"));
        Assert.Equal(EmailKind.Confirmed, queued.Kind);
        Assert.Equal(EmailStatus.Pending, queued.Status);

        await DrainAsync();

        var sent = Assert.Single(await EmailsForAsync("sent@mail.test"));
        Assert.Equal(EmailStatus.Sent, sent.Status);
        Assert.NotNull(sent.SentAt);
        Assert.Contains(factory.Resend.Sent, s => s.To == "sent@mail.test" && s.Subject.StartsWith("You're in"));
    }

    [Fact]
    public async Task Transient_failures_are_retried_later()
    {
        var ev = await CreateEventAsync(capacity: 10);
        factory.Resend.FailNext("retry@mail.test", HttpStatusCode.TooManyRequests);
        await RegisterAsync(ev.Slug, "retry@mail.test");

        await DrainAsync();
        var afterFailure = Assert.Single(await EmailsForAsync("retry@mail.test"));
        Assert.Equal(EmailStatus.Pending, afterFailure.Status);
        Assert.Equal(1, afterFailure.Attempts);
        Assert.True(afterFailure.NextAttemptAt > DateTime.UtcNow);
        Assert.Contains("429", afterFailure.LastError);

        await DrainAsync(DateTime.UtcNow.AddMinutes(2));
        var afterRetry = Assert.Single(await EmailsForAsync("retry@mail.test"));
        Assert.Equal(EmailStatus.Sent, afterRetry.Status);
        Assert.Equal(2, afterRetry.Attempts);
    }

    [Fact]
    public async Task Gives_up_after_the_maximum_number_of_attempts()
    {
        var ev = await CreateEventAsync(capacity: 10);
        factory.Resend.FailNext("flaky@mail.test", Enumerable.Repeat(HttpStatusCode.ServiceUnavailable, 10).ToArray());
        await RegisterAsync(ev.Slug, "flaky@mail.test");

        for (var day = 0; day < 8; day++)
            await DrainAsync(DateTime.UtcNow.AddDays(day));

        var failed = Assert.Single(await EmailsForAsync("flaky@mail.test"));
        Assert.Equal(EmailStatus.Failed, failed.Status);
        Assert.Equal(6, failed.Attempts);
    }

    [Fact]
    public async Task Permanent_failures_are_not_retried()
    {
        var ev = await CreateEventAsync(capacity: 10);
        factory.Resend.FailNext("rejected@mail.test", HttpStatusCode.UnprocessableEntity);
        await RegisterAsync(ev.Slug, "rejected@mail.test");

        await DrainAsync();

        var failed = Assert.Single(await EmailsForAsync("rejected@mail.test"));
        Assert.Equal(EmailStatus.Failed, failed.Status);
        Assert.Equal(1, failed.Attempts);
        Assert.Contains("422", failed.LastError);
    }

    [Fact]
    public async Task Ticket_email_is_skipped_when_registration_was_cancelled_before_sending()
    {
        var ev = await CreateEventAsync(capacity: 10);
        await RegisterAsync(ev.Slug, "changed-mind@mail.test");
        await WithDbAsync(async db =>
        {
            var r = await db.Registrations.SingleAsync(x => x.Email == "changed-mind@mail.test");
            r.Status = RegistrationStatus.Cancelled;
            return await db.SaveChangesAsync();
        });

        await DrainAsync();

        Assert.Equal(EmailStatus.Skipped, Assert.Single(await EmailsForAsync("changed-mind@mail.test")).Status);
        Assert.DoesNotContain(factory.Resend.Sent, s => s.To == "changed-mind@mail.test");
    }

    [Fact]
    public async Task Self_cancel_frees_the_seat_promotes_the_waitlist_and_emails_both()
    {
        var ev = await CreateEventAsync(capacity: 1);
        await RegisterAsync(ev.Slug, "leaving@mail.test");
        await RegisterAsync(ev.Slug, "waiting@mail.test");
        var token = await WithDbAsync(async db =>
            (await db.Registrations.SingleAsync(r => r.Email == "leaving@mail.test")).ManageToken!.Value);

        var page = await _client.GetFromJsonAsync<JsonElement>($"/api/registrations/manage/{token}");
        Assert.Equal("Confirmed", page.GetProperty("status").GetString());
        Assert.True(page.GetProperty("canCancel").GetBoolean());
        Assert.Equal(ev.Slug, page.GetProperty("event").GetProperty("slug").GetString());

        var cancel = await _client.PostAsync($"/api/registrations/manage/{token}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        // Clicking the button twice is harmless
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/registrations/manage/{token}/cancel", null)).StatusCode);

        var statuses = await WithDbAsync(db => db.Registrations.Where(r => r.EventId == ev.Id)
            .ToDictionaryAsync(r => r.Email, r => r.Status));
        Assert.Equal(RegistrationStatus.Cancelled, statuses["leaving@mail.test"]);
        Assert.Equal(RegistrationStatus.Confirmed, statuses["waiting@mail.test"]);

        Assert.Contains(await EmailsForAsync("leaving@mail.test"), o => o.Kind == EmailKind.Cancelled);
        Assert.Contains(await EmailsForAsync("waiting@mail.test"), o => o.Kind == EmailKind.Promoted);

        await DrainAsync();
        Assert.Contains(factory.Resend.Sent, s => s.To == "leaving@mail.test" && s.Subject.StartsWith("Your registration is cancelled"));
        Assert.Contains(factory.Resend.Sent, s => s.To == "waiting@mail.test" && s.Subject.StartsWith("A spot opened up"));
    }

    [Fact]
    public async Task Unknown_manage_token_is_not_found()
    {
        var response = await _client.GetAsync($"/api/registrations/manage/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Past_events_cannot_be_cancelled()
    {
        var ev = await CreateEventAsync(capacity: 10, date: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3));
        await RegisterAsync(ev.Slug, "too-late@mail.test");
        var token = await WithDbAsync(async db =>
            (await db.Registrations.SingleAsync(r => r.Email == "too-late@mail.test")).ManageToken!.Value);

        var page = await _client.GetFromJsonAsync<JsonElement>($"/api/registrations/manage/{token}");
        Assert.False(page.GetProperty("canCancel").GetBoolean());
        var cancel = await _client.PostAsync($"/api/registrations/manage/{token}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
    }

    [Fact]
    public async Task Cancelled_person_can_register_again()
    {
        var ev = await CreateEventAsync(capacity: 10);
        await RegisterAsync(ev.Slug, "again@mail.test");
        var token = await WithDbAsync(async db =>
            (await db.Registrations.SingleAsync(r => r.Email == "again@mail.test")).ManageToken!.Value);
        await _client.PostAsync($"/api/registrations/manage/{token}/cancel", null);

        var again = await RegisterAsync(ev.Slug, "again@mail.test", guests: 2);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var registration = await WithDbAsync(db => db.Registrations.SingleAsync(r => r.Email == "again@mail.test"));
        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
        Assert.Equal(2, registration.GuestsCount);
    }

    [Fact]
    public async Task Honeypot_submissions_are_accepted_but_not_stored()
    {
        var ev = await CreateEventAsync(capacity: 10);
        var response = await RegisterAsync(ev.Slug, "bot@mail.test", website: "http://spam.example");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await WithDbAsync(db => db.Registrations.AnyAsync(r => r.Email == "bot@mail.test")));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("Name <someone@mail.test>")]
    [InlineData("two@@mail.test")]
    public async Task Invalid_email_addresses_are_rejected(string email)
    {
        var ev = await CreateEventAsync(capacity: 10);
        var response = await RegisterAsync(ev.Slug, email);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_resend_the_ticket_and_sees_email_status()
    {
        await AuthenticateAsync();
        var ev = await CreateEventAsync(capacity: 10);
        await RegisterAsync(ev.Slug, "lost-it@mail.test");
        await DrainAsync();
        var id = await WithDbAsync(async db => (await db.Registrations.SingleAsync(r => r.Email == "lost-it@mail.test")).Id);

        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/admin/events/{ev.Id}/registrations");
        var row = list.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
        Assert.Equal("Sent", row.GetProperty("lastEmailStatus").GetString());

        var resend = await _client.PostAsync($"/api/admin/events/{ev.Id}/registrations/{id}/resend-email", null);
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        await DrainAsync();

        var emails = await EmailsForAsync("lost-it@mail.test");
        Assert.Equal(2, emails.Count);
        Assert.All(emails, e => Assert.Equal(EmailStatus.Sent, e.Status));
    }

    [Fact]
    public async Task Reminders_are_queued_once_on_the_day_before_from_nine_amsterdam_time()
    {
        var ev = await CreateEventAsync(capacity: 10, date: new DateOnly(2031, 3, 15));
        await RegisterAsync(ev.Slug, "remind-me@mail.test");
        await RegisterAsync(ev.Slug, "just-joined@mail.test");
        await WithDbAsync(async db =>
        {
            // Registered well before the reminder window; the other one signs up the evening before.
            var early = await db.Registrations.SingleAsync(r => r.Email == "remind-me@mail.test");
            early.CreatedAt = new DateTime(2031, 3, 1, 12, 0, 0, DateTimeKind.Utc);
            var late = await db.Registrations.SingleAsync(r => r.Email == "just-joined@mail.test");
            late.CreatedAt = new DateTime(2031, 3, 14, 6, 0, 0, DateTimeKind.Utc);
            return await db.SaveChangesAsync();
        });

        // 08:00 in Amsterdam (CET, UTC+1): too early
        Assert.Equal(0, await Dispatcher.QueueRemindersAsync(new DateTime(2031, 3, 14, 7, 0, 0, DateTimeKind.Utc)));

        // 11:00 in Amsterdam
        var at = new DateTime(2031, 3, 14, 10, 0, 0, DateTimeKind.Utc);
        await Dispatcher.QueueRemindersAsync(at);
        await Dispatcher.QueueRemindersAsync(at.AddMinutes(10)); // no duplicates

        Assert.Single(await EmailsForAsync("remind-me@mail.test"), o => o.Kind == EmailKind.Reminder);
        Assert.DoesNotContain(await EmailsForAsync("just-joined@mail.test"), o => o.Kind == EmailKind.Reminder);
    }
}

/// <summary>A tiny limit, to check the public forms are throttled.</summary>
public class RateLimitedApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:PermitLimit", "2");
    }
}

public class RateLimitTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Registration_is_throttled_per_client()
    {
        var client = factory.CreateClient();
        HttpStatusCode Last = 0;
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/events/does-not-exist/register",
                new { FullName = "Spammer", Email = $"spam{i}@mail.test", GuestsCount = 1 });
            Last = response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, Last);
    }
}
