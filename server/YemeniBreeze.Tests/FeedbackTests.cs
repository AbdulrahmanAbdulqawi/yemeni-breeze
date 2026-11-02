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

/// <summary>The after-event feedback email, the form behind its link, and the admin view of the answers.</summary>
public class FeedbackTests(EmailApiFactory factory) : IClassFixture<EmailApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private EmailDispatcher Dispatcher => factory.Services.GetRequiredService<EmailDispatcher>();

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<Event> CreatePastEventAsync() => WithDbAsync(async db =>
    {
        var ev = new Event
        {
            Slug = $"feedback-{Guid.NewGuid():N}", TitleEn = "Yemeni Night", TitleAr = "ليلة يمنية",
            Location = "Amsterdam", Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2),
            StartTime = "18:00", EndTime = "21:00", Capacity = 50, Status = EventStatus.Published
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    });

    private Task<Registration> AddGuestAsync(int eventId, string email, RegistrationStatus status = RegistrationStatus.Confirmed,
        bool checkedIn = true, string language = "en") => WithDbAsync(async db =>
    {
        var registration = new Registration
        {
            EventId = eventId, FullName = "Amina Saleh", Email = email, Status = status, Language = language,
            CheckedInAt = checkedIn ? DateTime.UtcNow.AddDays(-2) : null
        };
        db.Registrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
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

    private async Task DrainAsync()
    {
        for (var i = 0; i < 20 && await Dispatcher.ProcessDueAsync(DateTime.UtcNow.AddSeconds(1)) > 0; i++) { }
    }

    private async Task<int> SendAsync(int eventId, string audience)
    {
        var response = await _client.PostAsJsonAsync($"/api/admin/events/{eventId}/feedback/send", new { audience });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("queued").GetInt32();
    }

    [Fact]
    public async Task Send_to_checked_in_reaches_only_people_who_came_and_never_twice()
    {
        await AuthenticateAsync();
        var ev = await CreatePastEventAsync();
        await AddGuestAsync(ev.Id, "came@feedback.test");
        await AddGuestAsync(ev.Id, "no-show@feedback.test", checkedIn: false);
        await AddGuestAsync(ev.Id, "cancelled@feedback.test", RegistrationStatus.Cancelled, checkedIn: false);

        Assert.Equal(1, await SendAsync(ev.Id, "checkedIn"));
        await DrainAsync();
        Assert.Contains(factory.Resend.Sent, s => s.To == "came@feedback.test" && s.Subject.StartsWith("How was Yemeni Night"));
        Assert.DoesNotContain(factory.Resend.Sent, s => s.To == "no-show@feedback.test");

        // Pressing again doesn't email the same person twice; widening the audience adds the no-show only.
        Assert.Equal(0, await SendAsync(ev.Id, "checkedIn"));
        Assert.Equal(1, await SendAsync(ev.Id, "confirmed"));
        await DrainAsync();
        Assert.Contains(factory.Resend.Sent, s => s.To == "no-show@feedback.test");
        Assert.DoesNotContain(factory.Resend.Sent, s => s.To == "cancelled@feedback.test");
    }

    [Fact]
    public async Task Send_to_one_person_reaches_just_them_even_a_second_time()
    {
        await AuthenticateAsync();
        var ev = await CreatePastEventAsync();
        var me = await AddGuestAsync(ev.Id, "just-me@feedback.test");
        await AddGuestAsync(ev.Id, "someone-else@feedback.test");
        var cancelled = await AddGuestAsync(ev.Id, "not-me@feedback.test", RegistrationStatus.Cancelled);

        for (var i = 0; i < 2; i++)
            (await _client.PostAsync($"/api/admin/events/{ev.Id}/feedback/send/{me.Id}", null)).EnsureSuccessStatusCode();
        await DrainAsync();

        Assert.Equal(2, factory.Resend.Sent.Count(s => s.To == "just-me@feedback.test"));
        Assert.DoesNotContain(factory.Resend.Sent, s => s.To == "someone-else@feedback.test");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.PostAsync($"/api/admin/events/{ev.Id}/feedback/send/{cancelled.Id}", null)).StatusCode);
    }

    [Fact]
    public async Task Send_rejects_an_unknown_audience()
    {
        await AuthenticateAsync();
        var ev = await CreatePastEventAsync();
        var response = await _client.PostAsJsonAsync($"/api/admin/events/{ev.Id}/feedback/send", new { audience = "everyone" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Form_saves_step_by_step_and_keeps_only_known_choices()
    {
        var ev = await CreatePastEventAsync();
        var guest = await AddGuestAsync(ev.Id, "form@feedback.test");
        var url = $"/api/feedback/{guest.ManageToken}";

        var form = await _client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Amina", form.GetProperty("firstName").GetString());
        Assert.Equal(JsonValueKind.Null, form.GetProperty("answers").ValueKind);

        // Step 1, with a made-up option code that must be dropped
        var step1 = await _client.PutAsJsonAsync(url, new
        {
            complete = false,
            answers = new { expectations = new[] { "food", "hacked", "people" }, metExpectations = "yes", favouritePart = "  The bint al-sahn  " }
        });
        step1.EnsureSuccessStatusCode();

        // Last step: finished
        var last = await _client.PutAsJsonAsync(url, new
        {
            complete = true,
            answers = new
            {
                expectations = new[] { "people", "food" }, metExpectations = "yes", favouritePart = "The bint al-sahn",
                joinCommunity = "yes", contributions = new[] { "cooking" }, music = new[] { "lahji", "sanaani" },
                ideas = "=HYPERLINK(\"x\")"
            }
        });
        last.EnsureSuccessStatusCode();

        var saved = await WithDbAsync(db => db.FeedbackResponses.SingleAsync(f => f.RegistrationId == guest.Id));
        Assert.Equal("people,food", saved.Expectations); // form order, unknown code gone
        Assert.Equal("sanaani,lahji", saved.Music);
        Assert.Equal("The bint al-sahn", saved.FavouritePart);
        Assert.True(saved.IsComplete);

        // Fixing a typo later keeps it marked finished
        (await _client.PutAsJsonAsync(url, new { complete = false, answers = new { metExpectations = "partly" } })).EnsureSuccessStatusCode();
        Assert.True((await WithDbAsync(db => db.FeedbackResponses.SingleAsync(f => f.RegistrationId == guest.Id))).IsComplete);
    }

    [Fact]
    public async Task Links_of_cancelled_registrations_and_unknown_tokens_do_not_open_the_form()
    {
        var ev = await CreatePastEventAsync();
        var cancelled = await AddGuestAsync(ev.Id, "gone@feedback.test", RegistrationStatus.Cancelled, checkedIn: false);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/feedback/{cancelled.ManageToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/feedback/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Admin_sees_answers_with_counts_and_a_safe_csv()
    {
        await AuthenticateAsync();
        var ev = await CreatePastEventAsync();
        var guest = await AddGuestAsync(ev.Id, "csv@feedback.test");
        await AddGuestAsync(ev.Id, "quiet@feedback.test", checkedIn: false);
        (await _client.PutAsJsonAsync($"/api/feedback/{guest.ManageToken}", new
        {
            complete = true,
            answers = new { joinCommunity = "yes", metExpectations = "yes", ideas = "=cmd, \"quoted\"" }
        })).EnsureSuccessStatusCode();

        var summary = await _client.GetFromJsonAsync<JsonElement>($"/api/admin/events/{ev.Id}/feedback");
        Assert.Equal(2, summary.GetProperty("confirmed").GetInt32());
        Assert.Equal(1, summary.GetProperty("checkedIn").GetInt32());
        Assert.Equal(1, summary.GetProperty("responses").GetInt32());
        Assert.Equal(1, summary.GetProperty("wantToJoin").GetInt32());
        Assert.Equal("csv@feedback.test", summary.GetProperty("rows")[0].GetProperty("email").GetString());

        var csv = await _client.GetStringAsync($"/api/admin/events/{ev.Id}/feedback/export.csv");
        Assert.Contains("Yes, definitely", csv);
        Assert.Contains("\"'=cmd, \"\"quoted\"\"\"", csv); // escaped, and can't run as an Excel formula
    }

    [Fact]
    public void Feedback_email_has_review_and_take_part_buttons_in_the_guest_language()
    {
        var token = Guid.NewGuid();
        var ev = new Event { Slug = "night", TitleEn = "Yemeni Night", TitleAr = "ليلة يمنية", Date = new DateOnly(2026, 10, 25) };
        var registration = new Registration
        {
            FullName = "Amina Saleh", Email = "a@b.test", Status = RegistrationStatus.Confirmed, Language = "ar", ManageToken = token
        };

        var email = RegistrationEmails.Build(registration, ev, EmailKind.Feedback, "https://yemenbreeze.nl/")!;

        Assert.Contains("ليلة يمنية", email.Subject);
        Assert.Contains($"href=\"https://yemenbreeze.nl/feedback/{token:D}?lang=ar\"", email.Html);
        Assert.Contains($"href=\"https://yemenbreeze.nl/feedback/{token:D}?lang=ar&amp;step=2\"", email.Html);
        Assert.Contains("شاركنا رأيك", email.Html);
        Assert.Contains("شارك في فعالياتنا القادمة", email.Html);
        Assert.DoesNotContain("met=", email.Html); // no one-tap answers any more
        Assert.DoesNotContain("join=", email.Html);
        Assert.Contains("dir=\"rtl\"", email.Html);
        Assert.DoesNotContain("cid:ticket-qr", email.Html);
        Assert.Single(email.Attachments); // just the logo: no ticket, no calendar file
        Assert.Contains($"/feedback/{token:D}?lang=ar", email.Text);

        registration.Status = RegistrationStatus.Cancelled;
        Assert.Null(RegistrationEmails.Build(registration, ev, EmailKind.Feedback, "https://yemenbreeze.nl"));
    }

    [Fact]
    public void Feedback_email_has_the_donation_box_only_when_the_event_has_a_donation_link()
    {
        var registration = new Registration
        {
            FullName = "Amina Saleh", Email = "a@b.test", Status = RegistrationStatus.Confirmed, Language = "en",
            ManageToken = Guid.NewGuid()
        };
        var ev = new Event { Slug = "night", TitleEn = "Yemeni Night", Date = new DateOnly(2026, 10, 25) };

        var without = RegistrationEmails.Build(registration, ev, EmailKind.Feedback, "https://yemenbreeze.nl")!;
        Assert.DoesNotContain("voluntary donation", without.Html);

        ev.DonationUrl = "https://donate.example.org/yemen-breeze";
        var with = RegistrationEmails.Build(registration, ev, EmailKind.Feedback, "https://yemenbreeze.nl")!;
        Assert.Contains("voluntary donation", with.Html);
        Assert.Contains("https://donate.example.org/yemen-breeze", with.Html);
        Assert.Contains("Donate: https://donate.example.org/yemen-breeze", with.Text);
    }

    [Fact]
    public void Feedback_email_signs_off_with_the_team_but_not_see_you_soon()
    {
        var registration = new Registration
        {
            FullName = "Amina Saleh", Email = "a@b.test", Status = RegistrationStatus.Confirmed, Language = "en",
            ManageToken = Guid.NewGuid()
        };
        var ev = new Event { Slug = "night", TitleEn = "Yemeni Night", Date = new DateOnly(2026, 10, 25) };

        var feedback = RegistrationEmails.Build(registration, ev, EmailKind.Feedback, "https://yemenbreeze.nl")!;
        Assert.DoesNotContain("See you soon", feedback.Html);
        Assert.DoesNotContain("See you soon", feedback.Text);
        Assert.Contains("The Yemen Breeze team", feedback.Html);

        // The ticket email still has it
        var ticket = RegistrationEmails.Build(registration, ev, EmailKind.Confirmed, "https://yemenbreeze.nl")!;
        Assert.Contains("See you soon", ticket.Html);
    }
}
