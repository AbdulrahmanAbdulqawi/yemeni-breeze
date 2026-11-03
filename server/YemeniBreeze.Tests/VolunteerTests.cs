using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Tests;

/// <summary>The admin Volunteers page: everyone who said yes or maybe to taking part, across events.</summary>
public class VolunteerTests(EmailApiFactory factory) : IClassFixture<EmailApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<Event> CreateEventAsync(int daysAgo) => WithDbAsync(async db =>
    {
        var ev = new Event
        {
            Slug = $"volunteers-{Guid.NewGuid():N}", TitleEn = "Yemeni Night", TitleAr = "ليلة يمنية",
            Location = "Amsterdam", Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysAgo),
            StartTime = "18:00", EndTime = "21:00", Capacity = 50, Status = EventStatus.Published
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    });

    /// <summary>A confirmed guest at the event who answered the "take part" question.</summary>
    private Task<FeedbackResponse> AnswerAsync(int eventId, string email, string? join, string? contributions = null,
        DateTime? updatedAt = null) => WithDbAsync(async db =>
    {
        var registration = new Registration
        {
            EventId = eventId, FullName = "Sara Haddad", Email = email, Phone = "+31 6 1234 5678",
            Status = RegistrationStatus.Confirmed, Language = "ar"
        };
        db.Registrations.Add(registration);
        await db.SaveChangesAsync();
        var response = new FeedbackResponse
        {
            RegistrationId = registration.Id, EventId = eventId, JoinCommunity = join,
            Contributions = contributions, Ideas = "A cooking evening", UpdatedAt = updatedAt ?? DateTime.UtcNow
        };
        db.FeedbackResponses.Add(response);
        await db.SaveChangesAsync();
        return response;
    });

    private async Task AuthenticateAsync()
    {
        if (_client.DefaultRequestHeaders.Authorization is not null) return;
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@yemenbreeze.nl", password = "ChangeMe!2026" });
        var login = await response.Content.ReadFromJsonAsync<JsonElement>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());
    }

    private async Task<List<JsonElement>> ListAsync()
    {
        var list = await _client.GetFromJsonAsync<List<JsonElement>>("/api/admin/volunteers");
        return list!;
    }

    private static JsonElement? Find(List<JsonElement> list, string email) =>
        list.Cast<JsonElement?>().FirstOrDefault(v => v!.Value.GetProperty("email").GetString() == email);

    [Fact]
    public async Task Lists_yes_and_maybe_once_per_person_with_their_latest_answer()
    {
        await AuthenticateAsync();
        var first = await CreateEventAsync(40);
        var second = await CreateEventAsync(2);
        var tag = Guid.NewGuid().ToString("N")[..8];

        // Came twice: said "maybe" first, then "yes" with what they'd like to do.
        await AnswerAsync(first.Id, $"twice-{tag}@volunteer.test", "maybe", updatedAt: DateTime.UtcNow.AddDays(-39));
        await AnswerAsync(second.Id, $"Twice-{tag}@Volunteer.test", "yes", "cooking,media");
        await AnswerAsync(second.Id, $"maybe-{tag}@volunteer.test", "maybe", "volunteering");
        await AnswerAsync(second.Id, $"no-{tag}@volunteer.test", "no");
        await AnswerAsync(second.Id, $"skipped-{tag}@volunteer.test", null);
        // Said yes once, then "not at the moment": their latest answer leaves them out.
        await AnswerAsync(first.Id, $"changed-{tag}@volunteer.test", "yes", updatedAt: DateTime.UtcNow.AddDays(-39));
        await AnswerAsync(second.Id, $"changed-{tag}@volunteer.test", "no");

        var list = await ListAsync();
        var twice = Find(list, $"Twice-{tag}@Volunteer.test");
        Assert.NotNull(twice);
        var feedback = twice!.Value.GetProperty("feedback");
        Assert.Equal("yes", feedback.GetProperty("interest").GetString());
        Assert.Equal(["cooking", "media"], twice.Value.GetProperty("contributions").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(2, feedback.GetProperty("eventsCount").GetInt32());
        Assert.Equal(second.Id, feedback.GetProperty("event").GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, twice.Value.GetProperty("signup").ValueKind);
        Assert.Equal("ar", twice.Value.GetProperty("language").GetString());
        Assert.Null(Find(list, $"twice-{tag}@volunteer.test"));

        Assert.Equal("maybe", Find(list, $"maybe-{tag}@volunteer.test")!.Value.GetProperty("feedback").GetProperty("interest").GetString());
        Assert.Null(Find(list, $"no-{tag}@volunteer.test"));
        Assert.Null(Find(list, $"skipped-{tag}@volunteer.test"));
        Assert.Null(Find(list, $"changed-{tag}@volunteer.test"));
    }

    [Fact]
    public async Task Marking_contacted_covers_all_their_answers_and_can_be_undone()
    {
        await AuthenticateAsync();
        var first = await CreateEventAsync(40);
        var second = await CreateEventAsync(2);
        var email = $"contact-{Guid.NewGuid():N}@volunteer.test";
        var old = await AnswerAsync(first.Id, email, "yes", updatedAt: DateTime.UtcNow.AddDays(-39));
        var latest = await AnswerAsync(second.Id, email, "yes");

        Assert.False(Find(await ListAsync(), email)!.Value.GetProperty("contacted").GetBoolean());

        var marked = await _client.PostAsJsonAsync("/api/admin/volunteers/contacted",
            new { email = email.ToUpperInvariant(), contacted = true });
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.True(Find(await ListAsync(), email)!.Value.GetProperty("contacted").GetBoolean());
        var stamped = await WithDbAsync(db => db.FeedbackResponses
            .Where(f => f.Id == old.Id || f.Id == latest.Id).Select(f => f.ContactedAt).ToListAsync());
        Assert.All(stamped, when => Assert.NotNull(when));

        await _client.PostAsJsonAsync("/api/admin/volunteers/contacted", new { email, contacted = false });
        Assert.False(Find(await ListAsync(), email)!.Value.GetProperty("contacted").GetBoolean());

        var missing = await _client.PostAsJsonAsync("/api/admin/volunteers/contacted",
            new { email = "nobody@volunteer.test", contacted = true });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Csv_lists_people_with_readable_choices_and_needs_a_login()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/admin/volunteers/export.csv");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        await AuthenticateAsync();
        var ev = await CreateEventAsync(2);
        var email = $"csv-{Guid.NewGuid():N}@volunteer.test";
        await AnswerAsync(ev.Id, email, "yes", "organise,performing");

        var csv = await _client.GetStringAsync("/api/admin/volunteers/export.csv");
        var line = csv.Split('\n').Single(l => l.Contains(email));
        Assert.Contains("Yes, definitely", line);
        Assert.Contains("Helping organise events; Music, singing or dancing", line);
    }
}
