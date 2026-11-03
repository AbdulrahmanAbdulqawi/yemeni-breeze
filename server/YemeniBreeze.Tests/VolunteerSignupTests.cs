using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Email;
using YemeniBreeze.Api.Features.Feedback;

namespace YemeniBreeze.Tests;

/// <summary>The website's Volunteer page: the sign-up, the form the admin edits, and how sign-ups show on the admin list.</summary>
public class VolunteerSignupTests(EmailApiFactory factory) : IClassFixture<EmailApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Back to the default questions, so each test starts from the same form.</summary>
    private Task ResetFormAsync() => WithDbAsync(async db =>
    {
        db.VolunteerForms.RemoveRange(db.VolunteerForms);
        return await db.SaveChangesAsync();
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

    private static string NewEmail(string what) => $"{what}-{Guid.NewGuid():N}@signup.test";

    private Task<HttpResponseMessage> SignUpAsync(object answers, bool consent = true, string language = "en",
        string? helpOther = null) =>
        _client.PostAsJsonAsync("/api/volunteers", new { language, answers, helpOther, consent });

    private async Task<JsonElement?> FindAsync(string email)
    {
        var list = await _client.GetFromJsonAsync<List<JsonElement>>("/api/admin/volunteers");
        return list!.Cast<JsonElement?>().FirstOrDefault(v =>
            string.Equals(v!.Value.GetProperty("email").GetString(), email, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<VolunteerFormField>> AdminFormAsync() =>
        (await _client.GetFromJsonAsync<VolunteerFormDto>("/api/admin/volunteers/form"))!.Fields;

    private Task<HttpResponseMessage> SaveFormAsync(List<VolunteerFormField> fields) =>
        _client.PutAsJsonAsync("/api/admin/volunteers/form", new { fields });

    [Fact]
    public async Task Public_form_has_the_default_questions()
    {
        await ResetFormAsync();
        var form = await _client.GetFromJsonAsync<JsonElement>("/api/volunteer-form");
        var fields = form.GetProperty("fields").EnumerateArray().ToList();

        Assert.Equal(["name", "email", "phone", "help", "free", "about"], fields.Select(f => f.GetProperty("id").GetString()));
        var help = fields.Single(f => f.GetProperty("id").GetString() == "help");
        Assert.Equal("roles", help.GetProperty("lock").GetString());
        Assert.Equal("بماذا تودّ أن تساعد؟", help.GetProperty("label").GetProperty("ar").GetString());
        Assert.Contains(help.GetProperty("options").EnumerateArray(), o => o.GetProperty("id").GetString() == "cooking");
    }

    [Fact]
    public async Task Signing_up_lists_the_person_with_their_answers_and_queues_a_thank_you_email()
    {
        await ResetFormAsync();
        await AuthenticateAsync();
        var email = NewEmail("nadia");

        var response = await SignUpAsync(new
        {
            name = "Nadia Al-Hamdani", email, phone = "+31 6 1122 3344",
            help = new[] { "media", "other" }, free = "weekends", about = "I take photos."
        }, language: "nl", helpOther: "Translating flyers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var person = (await FindAsync(email))!.Value;
        Assert.Equal("Nadia Al-Hamdani", person.GetProperty("fullName").GetString());
        Assert.Equal("+31 6 1122 3344", person.GetProperty("phone").GetString());
        Assert.Equal("nl", person.GetProperty("language").GetString());
        Assert.Equal(["media", "other"], person.GetProperty("contributions").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal("Translating flyers", person.GetProperty("contributionsOther").GetString());
        Assert.False(person.GetProperty("contacted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, person.GetProperty("feedback").ValueKind);

        var answers = person.GetProperty("signup").GetProperty("answers").EnumerateArray().ToList();
        var free = answers.Single(a => a.GetProperty("fieldId").GetString() == "free");
        Assert.Equal("When are you usually free?", free.GetProperty("question").GetProperty("en").GetString());
        Assert.Equal("In het weekend", free.GetProperty("choices")[0].GetProperty("nl").GetString());
        Assert.Equal("I take photos.", answers.Single(a => a.GetProperty("fieldId").GetString() == "about")
            .GetProperty("value").GetString());

        var queued = await WithDbAsync(db => db.OutgoingEmails
            .Where(o => o.VolunteerSignup!.Email == email).ToListAsync());
        Assert.Equal(EmailKind.VolunteerThanks, Assert.Single(queued).Kind);

        var signup = await WithDbAsync(db => db.VolunteerSignups.SingleAsync(s => s.Email == email));
        var thanks = RegistrationEmails.BuildVolunteerThanks(signup, "https://yemenbreeze.nl");
        Assert.Contains("vrijwilliger", thanks.Subject);
        Assert.Contains("Beste Nadia", thanks.Html);
        Assert.Contains("https://yemenbreeze.nl/events", thanks.Text);
    }

    [Fact]
    public async Task Sign_ups_are_checked_against_the_form()
    {
        await ResetFormAsync();
        var email = NewEmail("checked");
        var ok = new { name = "Omar", email, help = new[] { "cooking" } };

        async Task<string?> RejectedField(HttpResponseMessage r)
        {
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("field").GetString();
        }

        Assert.Equal("consent", await RejectedField(await SignUpAsync(ok, consent: false)));
        Assert.Equal("help", await RejectedField(await SignUpAsync(new { name = "Omar", email })));
        Assert.Equal("help", await RejectedField(await SignUpAsync(new { name = "Omar", email, help = new[] { "juggling" } })));
        Assert.Equal("email", await RejectedField(await SignUpAsync(new { name = "Omar", email = "not-an-email", help = new[] { "cooking" } })));
        Assert.Equal("free", await RejectedField(await SignUpAsync(new { name = "Omar", email, help = new[] { "cooking" }, free = "mondays" })));
        Assert.Equal("name", await RejectedField(await SignUpAsync(new { name = "  ", email, help = new[] { "cooking" } })));

        Assert.Equal(HttpStatusCode.OK, (await SignUpAsync(ok)).StatusCode);
    }

    [Fact]
    public async Task Someone_who_signed_up_and_said_yes_in_feedback_shows_once_and_is_contacted_everywhere()
    {
        await ResetFormAsync();
        await AuthenticateAsync();
        var email = NewEmail("both");
        await WithDbAsync(async db =>
        {
            var ev = new Event
            {
                Slug = $"signup-{Guid.NewGuid():N}", TitleEn = "Yemeni Night", Location = "Utrecht",
                Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5), StartTime = "18:00", EndTime = "21:00",
                Capacity = 50, Status = EventStatus.Published
            };
            var registration = new Registration
            {
                Event = ev, FullName = "Omar Saleh", Email = email, Phone = "+31 6 2345 6789",
                Status = RegistrationStatus.Confirmed, Language = "en"
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync();
            db.FeedbackResponses.Add(new FeedbackResponse
            {
                RegistrationId = registration.Id, EventId = ev.Id, JoinCommunity = "yes",
                Contributions = "organise,cooking", UpdatedAt = DateTime.UtcNow.AddDays(-4)
            });
            return await db.SaveChangesAsync();
        });

        await SignUpAsync(new { name = "Omar Saleh", email = email.ToUpperInvariant(), help = new[] { "media", "cooking" } });

        var list = await _client.GetFromJsonAsync<List<JsonElement>>("/api/admin/volunteers");
        var rows = list!.Where(v => string.Equals(v.GetProperty("email").GetString(), email, StringComparison.OrdinalIgnoreCase)).ToList();
        var person = Assert.Single(rows);
        Assert.Equal("yes", person.GetProperty("feedback").GetProperty("interest").GetString());
        Assert.NotEqual(JsonValueKind.Null, person.GetProperty("signup").ValueKind);
        Assert.Equal(["media", "cooking", "organise"], person.GetProperty("contributions").EnumerateArray().Select(c => c.GetString()));
        // The sign-up has no phone, so the one from their registration is shown.
        Assert.Equal("+31 6 2345 6789", person.GetProperty("phone").GetString());

        await _client.PostAsJsonAsync("/api/admin/volunteers/contacted", new { email, contacted = true });
        Assert.True((await FindAsync(email))!.Value.GetProperty("contacted").GetBoolean());
        var stamped = await WithDbAsync(db => db.VolunteerSignups
            .Where(s => s.Email.ToLower() == email).Select(s => s.ContactedAt).ToListAsync());
        Assert.All(stamped, when => Assert.NotNull(when));
    }

    [Fact]
    public async Task Admin_can_add_change_reorder_and_remove_questions()
    {
        await ResetFormAsync();
        var anonymous = await factory.CreateClient().PutAsJsonAsync("/api/admin/volunteers/form", new { fields = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await AuthenticateAsync();

        var fields = await AdminFormAsync();
        fields.RemoveAll(f => f.Id == "phone");
        var about = fields.Single(f => f.Id == "about");
        fields[fields.IndexOf(about)] = about with { Label = new LocalizedText("A few words about you", "", "") };
        fields.Insert(2, new VolunteerFormField("q-drive", "yesno", null, true,
            new LocalizedText("Do you have a car?", "Heb je een auto?", "هل لديك سيارة؟"),
            new LocalizedText("", "", ""), []));
        Assert.Equal(HttpStatusCode.OK, (await SaveFormAsync(fields)).StatusCode);

        var form = await _client.GetFromJsonAsync<JsonElement>("/api/volunteer-form");
        Assert.Equal(["name", "email", "q-drive", "help", "free", "about"],
            form.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("id").GetString()));

        var email = NewEmail("drive");
        var missing = await SignUpAsync(new { name = "Lina", email, help = new[] { "volunteering" } });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        // A phone answer for a question that's gone is ignored.
        var sent = await SignUpAsync(new Dictionary<string, object>
        {
            ["name"] = "Lina", ["email"] = email, ["help"] = new[] { "volunteering" },
            ["phone"] = "+31 6", ["about"] = "Hi", ["q-drive"] = "yes"
        });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        // Removing the question afterwards keeps the answer, with the wording it had.
        fields.RemoveAll(f => f.Id == "q-drive");
        Assert.Equal(HttpStatusCode.OK, (await SaveFormAsync(fields)).StatusCode);
        var person = (await FindAsync(email))!.Value;
        Assert.Equal(JsonValueKind.Null, person.GetProperty("phone").ValueKind);
        var answers = person.GetProperty("signup").GetProperty("answers").EnumerateArray().ToList();
        var drive = answers.Single(a => a.GetProperty("fieldId").GetString() == "q-drive");
        Assert.Equal("Do you have a car?", drive.GetProperty("question").GetProperty("en").GetString());
        Assert.Equal("yes", drive.GetProperty("value").GetString());
        Assert.Equal("A few words about you", answers.Single(a => a.GetProperty("fieldId").GetString() == "about")
            .GetProperty("question").GetProperty("en").GetString());

        await ResetFormAsync();
    }

    [Fact]
    public async Task The_core_questions_stay_and_broken_forms_are_refused()
    {
        await ResetFormAsync();
        await AuthenticateAsync();
        var fields = await AdminFormAsync();

        var noName = fields.Where(f => f.Id != "name").ToList();
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveFormAsync(noName)).StatusCode);

        var duplicate = fields.Append(fields[2]).ToList();
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveFormAsync(duplicate)).StatusCode);

        var noChoices = fields.Append(new VolunteerFormField("q-empty", "one", null, false,
            new LocalizedText("Pick one", "", ""), new LocalizedText("", "", ""), [])).ToList();
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveFormAsync(noChoices)).StatusCode);

        var noWording = fields.Append(new VolunteerFormField("q-blank", "short", null, false,
            new LocalizedText(" ", "Iets", ""), new LocalizedText("", "", ""), [])).ToList();
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveFormAsync(noWording)).StatusCode);

        // Name and email keep their type and stay required; a lock sent by the page is not trusted.
        var tampered = fields.Select(f => f.Id switch
        {
            "name" => f with { Type = "long", Required = false },
            "free" => f with { Lock = "contact" },
            _ => f
        }).ToList();
        Assert.Equal(HttpStatusCode.OK, (await SaveFormAsync(tampered)).StatusCode);
        var saved = await AdminFormAsync();
        var name = saved.Single(f => f.Id == "name");
        Assert.Equal("short", name.Type);
        Assert.True(name.Required);
        Assert.Null(saved.Single(f => f.Id == "free").Lock);

        await ResetFormAsync();
    }
}
