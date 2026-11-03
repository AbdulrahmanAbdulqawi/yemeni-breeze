using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Email;
using YemeniBreeze.Api.Features.Security;

namespace YemeniBreeze.Api.Features.Feedback;

public record VolunteerEventDto(int Id, string TitleEn, string TitleNl, string TitleAr, DateOnly Date);

/// <summary>Their latest "take part" answer in an event's feedback form (yes or maybe).</summary>
public record VolunteerFeedbackDto(
    int ResponseId, string Interest, VolunteerEventDto Event, int EventsCount,
    string? Ideas, string? AnythingElse, DateTime RepliedAt);

/// <summary>Their latest sign-up on the website's Volunteer page.</summary>
public record VolunteerSignupDto(int Id, DateTime SignedUpAt, List<VolunteerAnswer> Answers);

/// <summary>
/// One person who wants to volunteer: they signed up on the website, said yes or maybe in an
/// event's feedback, or both. People are matched by email, so each shows once.
/// </summary>
public record VolunteerDto(
    string Email, string FullName, string? Phone, string Language,
    string[] Contributions, string? ContributionsOther, DateTime LatestAt, bool Contacted,
    VolunteerSignupDto? Signup, VolunteerFeedbackDto? Feedback);

public record VolunteerContactedRequest(string Email, bool Contacted);

public record VolunteerFormDto(List<VolunteerFormField> Fields, DateTime? UpdatedAt);

public record VolunteerFormUpdate(List<VolunteerFormField>? Fields);

/// <summary>
/// A sign-up from the Volunteer page. <see cref="Answers"/> maps question ids to a string, or to a
/// list of choice ids for "pick several". <see cref="HelpOther"/> is the "Something else" text.
/// </summary>
public record VolunteerSignupRequest(
    string? Language, Dictionary<string, JsonElement>? Answers, string? HelpOther, bool Consent);

public static partial class VolunteerEndpoints
{
    private static readonly string[] Interested = ["yes", "maybe"];

    public static void MapVolunteerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/volunteer-form", async (AppDbContext db) =>
        {
            var (fields, _) = await VolunteerFormDefinition.LoadAsync(db);
            return Results.Ok(new { fields });
        });

        app.MapPost("/api/volunteers", async (VolunteerSignupRequest request, AppDbContext db, EmailSignal emailSignal) =>
        {
            var (fields, _) = await VolunteerFormDefinition.LoadAsync(db);
            var (signup, error, field) = ReadSignup(request, fields);
            if (signup is null) return Results.BadRequest(new { message = error, field });

            db.VolunteerSignups.Add(signup);
            EmailOutbox.EnqueueVolunteerThanks(db, signup);
            await db.SaveChangesAsync();
            emailSignal.Wake();
            return Results.Ok(new { message = "Thank you — we will get in touch soon." });
        }).RequireRateLimiting(RateLimiting.PublicForms);

        var admin = app.MapGroup("/api/admin/volunteers").RequireAuthorization();

        admin.MapGet("/", async (AppDbContext db) => Results.Ok(await ListAsync(db)));

        // Marks the person (every sign-up and feedback answer with their email) as contacted or not.
        admin.MapPost("/contacted", async (VolunteerContactedRequest request, AppDbContext db) =>
        {
            var email = Key(request.Email ?? "");
            if (email == "") return Results.NotFound();
            var responses = await db.FeedbackResponses
                .Where(f => f.Registration!.Email.Trim().ToLower() == email)
                .ToListAsync();
            var signups = await db.VolunteerSignups
                .Where(s => s.Email.Trim().ToLower() == email)
                .ToListAsync();
            if (responses.Count == 0 && signups.Count == 0) return Results.NotFound();

            var when = request.Contacted ? DateTime.UtcNow : (DateTime?)null;
            foreach (var f in responses) f.ContactedAt = when;
            foreach (var s in signups) s.ContactedAt = when;
            await db.SaveChangesAsync();
            return Results.Ok(new { contacted = request.Contacted });
        });

        admin.MapGet("/export.csv", async (AppDbContext db) =>
        {
            var rows = await ListAsync(db);
            var (fields, _) = await VolunteerFormDefinition.LoadAsync(db);
            var roleLabels = fields.FirstOrDefault(f => f.Id == VolunteerFormDefinition.HelpId)?.Options
                .ToDictionary(o => o.Id, o => o.Label.En) ?? [];
            string Role(string code) =>
                roleLabels.GetValueOrDefault(code) ?? FeedbackOptions.Contributions.GetValueOrDefault(code, code);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(',', "Name", "Email", "Phone", "Language", "Came from", "Wants to help with",
                "Other", "Signed up", "Sign-up answers", "Feedback answer", "Ideas", "Anything else",
                "Latest event", "Events", "Replied", "Contacted"));
            foreach (var v in rows)
            {
                var source = new List<string>();
                if (v.Signup is not null) source.Add("Website sign-up");
                if (v.Feedback is not null) source.Add("Event feedback");
                var answers = v.Signup?.Answers.Select(a => $"{a.Question.En}: {AnswerText(a)}") ?? [];
                sb.AppendLine(string.Join(',',
                    Csv(v.FullName), Csv(v.Email), Csv(v.Phone), Csv(v.Language), Csv(string.Join(" + ", source)),
                    Csv(string.Join("; ", v.Contributions.Select(Role))), Csv(v.ContributionsOther),
                    v.Signup?.SignedUpAt.ToString("yyyy-MM-dd") ?? "",
                    Csv(string.Join("; ", answers)),
                    Csv(v.Feedback is null ? null : FeedbackOptions.Join.GetValueOrDefault(v.Feedback.Interest, v.Feedback.Interest)),
                    Csv(v.Feedback?.Ideas), Csv(v.Feedback?.AnythingElse), Csv(v.Feedback?.Event.TitleEn),
                    v.Feedback?.EventsCount.ToString(CultureInfo.InvariantCulture) ?? "",
                    v.Feedback?.RepliedAt.ToString("yyyy-MM-dd") ?? "",
                    v.Contacted ? "Yes" : "No"));
            }
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
                "text/csv", "volunteers.csv");
        });

        admin.MapGet("/form", async (AppDbContext db) =>
        {
            var (fields, updatedAt) = await VolunteerFormDefinition.LoadAsync(db);
            return Results.Ok(new VolunteerFormDto(fields, updatedAt));
        });

        admin.MapPut("/form", async (VolunteerFormUpdate update, AppDbContext db) =>
        {
            var (fields, error) = VolunteerFormDefinition.Validate(update.Fields);
            if (fields is null) return Results.BadRequest(new { message = error });

            var row = await db.VolunteerForms.OrderBy(f => f.Id).FirstOrDefaultAsync();
            if (row is null) db.VolunteerForms.Add(row = new VolunteerForm());
            row.FieldsJson = JsonSerializer.Serialize(fields, VolunteerFormDefinition.Json);
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new VolunteerFormDto(fields, row.UpdatedAt));
        });
    }

    /// <summary>Checks a sign-up against the current form and turns it into a row, or says what's wrong.</summary>
    private static (VolunteerSignup? Signup, string? Error, string? Field) ReadSignup(
        VolunteerSignupRequest request, List<VolunteerFormField> fields)
    {
        if (!request.Consent) return (null, "Please tick the box so we can contact you.", "consent");

        var answers = request.Answers ?? [];
        var signup = new VolunteerSignup { Language = request.Language is "nl" or "ar" ? request.Language : "en" };
        var extra = new List<VolunteerAnswer>();
        var phoneTaken = false;

        foreach (var f in fields)
        {
            answers.TryGetValue(f.Id, out var raw);
            string? value = null;
            List<VolunteerFormOption> picked = [];

            switch (f.Type)
            {
                case "one":
                    value = Text(raw);
                    if (value is not null)
                    {
                        var option = f.Options.FirstOrDefault(o => o.Id == value);
                        if (option is null) return (null, $"Please pick one of the choices for “{f.Label.En}”.", f.Id);
                        picked = [option];
                    }
                    break;
                case "many":
                    foreach (var id in List(raw).Distinct())
                    {
                        var option = f.Options.FirstOrDefault(o => o.Id == id);
                        if (option is null) return (null, $"Please pick from the choices for “{f.Label.En}”.", f.Id);
                        picked.Add(option);
                    }
                    break;
                case "yesno":
                    value = Text(raw);
                    if (value is not null and not ("yes" or "no")) return (null, $"Please answer yes or no to “{f.Label.En}”.", f.Id);
                    break;
                case "date":
                    value = Text(raw);
                    if (value is not null && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        return (null, $"Please enter a date for “{f.Label.En}”.", f.Id);
                    break;
                default:
                    value = Text(raw);
                    var max = f.Type switch { "long" => 2000, "email" => 300, "phone" => 50, _ => 200 };
                    if (value is { Length: var length } && length > max)
                        return (null, $"The answer to “{f.Label.En}” is too long.", f.Id);
                    break;
            }

            var empty = value is null && picked.Count == 0;
            if (f.Required && empty) return (null, $"Please answer “{f.Label.En}”.", f.Id);

            switch (f.Id)
            {
                case VolunteerFormDefinition.NameId:
                    signup.FullName = value!;
                    break;
                case VolunteerFormDefinition.EmailId:
                    if (!EmailPattern().IsMatch(value!)) return (null, "Please enter a valid email address.", f.Id);
                    signup.Email = value!;
                    break;
                case VolunteerFormDefinition.HelpId:
                    signup.Contributions = picked.Count > 0 ? string.Join(',', picked.Select(o => o.Id)) : null;
                    var other = request.HelpOther?.Trim();
                    if (picked.Any(o => o.Id == VolunteerFormDefinition.OtherOptionId) && !string.IsNullOrEmpty(other))
                        signup.ContributionsOther = other.Length > 300 ? other[..300] : other;
                    break;
                default:
                    if (empty) break;
                    // The first phone question fills the phone number the admin list shows and searches.
                    if (f.Type == "phone" && !phoneTaken)
                    {
                        signup.Phone = value;
                        phoneTaken = true;
                        break;
                    }
                    extra.Add(new VolunteerAnswer(f.Id, f.Type, f.Label, value,
                        picked.Count > 0 ? picked.Select(o => o.Label).ToList() : null));
                    break;
            }
        }

        signup.AnswersJson = JsonSerializer.Serialize(extra, VolunteerFormDefinition.Json);
        return (signup, null, null);
    }

    private static string? Text(JsonElement? raw)
    {
        if (raw is not { } e) return null;
        var text = e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.GetRawText(),
            _ => null
        };
        text = text?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static IEnumerable<string> List(JsonElement? raw) =>
        raw is { ValueKind: JsonValueKind.Array } e
            ? e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!.Trim())
            : [];

    private static string AnswerText(VolunteerAnswer a) =>
        a.Choices is { Count: > 0 } ? string.Join(", ", a.Choices.Select(c => c.En))
        : a.Type == "yesno" ? (a.Value == "yes" ? "Yes" : "No")
        : a.Value ?? "";

    private static List<VolunteerAnswer> ParseAnswers(string json)
    {
        try { return JsonSerializer.Deserialize<List<VolunteerAnswer>>(json, VolunteerFormDefinition.Json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static async Task<List<VolunteerDto>> ListAsync(AppDbContext db)
    {
        var responses = await db.FeedbackResponses
            .Include(f => f.Registration).ThenInclude(r => r!.Event)
            .Where(f => f.JoinCommunity != null)
            .ToListAsync();
        var signups = await db.VolunteerSignups.ToListAsync();

        var feedbackByPerson = responses.GroupBy(f => Key(f.Registration!.Email)).ToDictionary(g => g.Key, g => g.ToList());
        var signupsByPerson = signups.GroupBy(s => Key(s.Email)).ToDictionary(g => g.Key, g => g.ToList());

        var people = new List<VolunteerDto>();
        foreach (var key in feedbackByPerson.Keys.Union(signupsByPerson.Keys))
        {
            var answers = feedbackByPerson.GetValueOrDefault(key) ?? [];
            var personSignups = signupsByPerson.GetValueOrDefault(key) ?? [];

            // Their latest feedback answer decides: "yes" once and "not at the moment" later leaves it out.
            var latestAnswer = answers.MaxBy(f => f.UpdatedAt);
            if (latestAnswer is not null && !Interested.Contains(latestAnswer.JoinCommunity)) latestAnswer = null;
            var latestSignup = personSignups.MaxBy(s => s.CreatedAt);
            if (latestAnswer is null && latestSignup is null) continue;

            VolunteerFeedbackDto? feedback = null;
            if (latestAnswer is not null)
            {
                var ev = latestAnswer.Registration!.Event!;
                feedback = new VolunteerFeedbackDto(latestAnswer.Id, latestAnswer.JoinCommunity!,
                    new VolunteerEventDto(ev.Id, ev.TitleEn, ev.TitleNl, ev.TitleAr, ev.Date),
                    answers.Select(a => a.EventId).Distinct().Count(),
                    latestAnswer.Ideas, latestAnswer.AnythingElse, latestAnswer.UpdatedAt);
            }
            var signupDto = latestSignup is null ? null
                : new VolunteerSignupDto(latestSignup.Id, latestSignup.CreatedAt, ParseAnswers(latestSignup.AnswersJson));

            // Name, email and language come from whatever they did most recently.
            var registration = latestAnswer?.Registration;
            var signupIsNewer = latestSignup is not null &&
                                (latestAnswer is null || latestSignup.CreatedAt >= latestAnswer.UpdatedAt);
            var contributions = FeedbackOptions.Split(latestSignup?.Contributions)
                .Concat(FeedbackOptions.Split(latestAnswer?.Contributions))
                .Distinct().ToArray();

            people.Add(new VolunteerDto(
                signupIsNewer ? latestSignup!.Email : registration!.Email,
                signupIsNewer ? latestSignup!.FullName : registration!.FullName,
                (signupIsNewer ? latestSignup!.Phone : registration!.Phone) ?? latestSignup?.Phone ?? registration?.Phone,
                signupIsNewer ? latestSignup!.Language : registration!.Language,
                contributions,
                latestSignup?.ContributionsOther ?? latestAnswer?.ContributionsOther,
                new[] { latestSignup?.CreatedAt, latestAnswer?.UpdatedAt }.Max()!.Value,
                answers.Any(a => a.ContactedAt != null) || personSignups.Any(s => s.ContactedAt != null),
                signupDto, feedback));
        }
        return people.OrderByDescending(p => p.LatestAt).ToList();
    }

    private static string Key(string email) => email.Trim().ToLowerInvariant();

    private static string Csv(string? value)
    {
        value ??= "";
        // A leading = + - @ would run as a formula when the file is opened in Excel.
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
