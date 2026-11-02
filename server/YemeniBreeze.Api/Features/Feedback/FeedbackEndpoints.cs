using System.Text;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Email;

namespace YemeniBreeze.Api.Features.Feedback;

/// <summary>The choices on the feedback form. Codes are stored; the labels are for the CSV export.</summary>
public static class FeedbackOptions
{
    public static readonly IReadOnlyDictionary<string, string> Expectations = new Dictionary<string, string>
    {
        ["people"] = "Meeting new people and connecting with the Yemeni community",
        ["culture"] = "Celebrating Yemeni culture and traditions",
        ["food"] = "Enjoying Yemeni food, music and entertainment",
        ["heritage"] = "Learning more about Yemeni heritage",
        ["belonging"] = "Finding a sense of belonging and community",
        ["fun"] = "Simply having a lovely time",
        ["other"] = "Other"
    };

    public static readonly IReadOnlyDictionary<string, string> Met = new Dictionary<string, string>
    {
        ["yes"] = "Yes, absolutely", ["partly"] = "More or less", ["no"] = "No, not quite"
    };

    public static readonly IReadOnlyDictionary<string, string> Join = new Dictionary<string, string>
    {
        ["yes"] = "Yes, definitely", ["maybe"] = "Maybe, would like to know more", ["no"] = "Not at the moment"
    };

    public static readonly IReadOnlyDictionary<string, string> Contributions = new Dictionary<string, string>
    {
        ["organise"] = "Helping organise events",
        ["cooking"] = "Cooking or introducing traditional Yemeni dishes",
        ["traditions"] = "Sharing Yemeni traditions and cultural knowledge",
        ["performing"] = "Music, singing or dancing",
        ["workshops"] = "Organising workshops or activities",
        ["media"] = "Photography, media or communication",
        ["volunteering"] = "Volunteering and helping with events",
        ["other"] = "Other"
    };

    public static readonly IReadOnlyDictionary<string, string> Music = new Dictionary<string, string>
    {
        ["traditional"] = "Traditional Yemeni", ["sanaani"] = "Sanaani", ["lahji"] = "Lahji",
        ["hadhrami"] = "Hadhrami", ["modern"] = "Modern Yemeni", ["mix"] = "A mixture of styles"
    };

    public static readonly IReadOnlyDictionary<string, string> Dances = new Dictionary<string, string>
    {
        ["baraa"] = "Bara'a", ["lahji"] = "Lahji dance", ["folk"] = "Traditional folk performances",
        ["live"] = "Live Yemeni musical performances", ["poetry"] = "Poetry and traditional storytelling",
        ["other"] = "Other suggestions"
    };

    /// <summary>Keeps only known codes, in the form's order, without repeats. Null when nothing is left.</summary>
    public static string? Codes(IEnumerable<string>? picked, IReadOnlyDictionary<string, string> options)
    {
        if (picked is null) return null;
        var set = picked.Select(p => p?.Trim().ToLowerInvariant()).ToHashSet();
        var kept = options.Keys.Where(set.Contains).ToList();
        return kept.Count == 0 ? null : string.Join(',', kept);
    }

    public static string? Code(string? picked, IReadOnlyDictionary<string, string> options)
    {
        var code = picked?.Trim().ToLowerInvariant();
        return code is not null && options.ContainsKey(code) ? code : null;
    }

    public static string[] Split(string? codes) =>
        string.IsNullOrEmpty(codes) ? [] : codes.Split(',', StringSplitOptions.RemoveEmptyEntries);

    public static string Labels(string? codes, IReadOnlyDictionary<string, string> options) =>
        string.Join("; ", Split(codes).Select(c => options.GetValueOrDefault(c, c)));
}

/// <summary>What the form sends and gets back. Lists hold option codes.</summary>
public record FeedbackAnswers(
    string[]? Expectations, string? ExpectationsOther, string? MetExpectations, string? Improvements,
    string? FavouritePart, string? JoinCommunity, string[]? Contributions, string? ContributionsOther,
    string? Dishes, string? RegionalFood, string[]? Music, string[]? Dances, string? DancesOther,
    string? Traditions, string? Missing, string? Engagement, string? Ideas, string? AnythingElse);

public record FeedbackSaveRequest(FeedbackAnswers Answers, bool Complete);

public record FeedbackEventDto(string TitleEn, string TitleNl, string TitleAr, DateOnly Date);

public record FeedbackFormDto(string FirstName, string Language, FeedbackEventDto Event, FeedbackAnswers? Answers, bool IsComplete);

public record FeedbackRowDto(int Id, int RegistrationId, string FullName, string Email, string? Phone,
    FeedbackAnswers Answers, bool IsComplete, DateTime UpdatedAt);

public record FeedbackSummaryDto(
    int Confirmed, int CheckedIn, int EmailsSent, int EmailsPending, int Responses, int Completed,
    int WantToJoin, List<FeedbackRowDto> Rows);

public record FeedbackSendRequest(string Audience);

public static class FeedbackEndpoints
{
    private const int MaxText = 2000;

    public static void MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: the form behind the link in the feedback email. The link's token is the
        // registration's private manage token, so nobody types a name or email.
        app.MapGet("/api/feedback/{token:guid}", async (Guid token, AppDbContext db) =>
        {
            var registration = await FindAsync(db, token);
            if (registration is null) return Results.NotFound();
            var response = await db.FeedbackResponses.FirstOrDefaultAsync(f => f.RegistrationId == registration.Id);
            return Results.Ok(ToFormDto(registration, response));
        });

        app.MapPut("/api/feedback/{token:guid}", async (Guid token, FeedbackSaveRequest request, AppDbContext db) =>
        {
            var registration = await FindAsync(db, token);
            if (registration is null) return Results.NotFound();
            if (request.Answers is null) return Results.BadRequest(new { message = "No answers sent." });

            var response = await db.FeedbackResponses.FirstOrDefaultAsync(f => f.RegistrationId == registration.Id);
            if (response is null)
            {
                response = new FeedbackResponse { RegistrationId = registration.Id, EventId = registration.EventId };
                db.FeedbackResponses.Add(response);
            }
            Apply(response, request.Answers);
            // Once sent, it stays sent: going back to fix a typo doesn't make it "unfinished" again.
            response.IsComplete |= request.Complete;
            response.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ToFormDto(registration, response));
        });

        var admin = app.MapGroup("/api/admin/events/{eventId:int}/feedback").RequireAuthorization();

        admin.MapGet("/", async (int eventId, AppDbContext db) =>
        {
            if (!await db.Events.AnyAsync(e => e.Id == eventId)) return Results.NotFound();
            var confirmed = await db.Registrations
                .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Confirmed)
                .Select(r => new { r.CheckedInAt })
                .ToListAsync();
            var emails = await db.OutgoingEmails
                .Where(o => o.Kind == EmailKind.Feedback && o.Registration!.EventId == eventId)
                .Select(o => o.Status)
                .ToListAsync();
            var rows = await Rows(db, eventId);

            return Results.Ok(new FeedbackSummaryDto(
                confirmed.Count,
                confirmed.Count(r => r.CheckedInAt is not null),
                emails.Count(s => s == EmailStatus.Sent),
                emails.Count(s => s == EmailStatus.Pending),
                rows.Count,
                rows.Count(r => r.IsComplete),
                rows.Count(r => r.Answers.JoinCommunity == "yes"),
                rows));
        });

        // Queues the feedback email for everyone who came (or everyone confirmed). People who
        // already have one queued or sent are skipped, so pressing the button twice is safe.
        admin.MapPost("/send", async (int eventId, FeedbackSendRequest request, AppDbContext db, EmailSignal emailSignal) =>
        {
            if (!await db.Events.AnyAsync(e => e.Id == eventId)) return Results.NotFound();
            var checkedInOnly = request.Audience?.Trim().ToLowerInvariant() switch
            {
                "checkedin" => true,
                "confirmed" => false,
                _ => (bool?)null
            };
            if (checkedInOnly is null)
                return Results.BadRequest(new { message = "Audience must be 'checkedIn' or 'confirmed'." });

            var due = await db.Registrations
                .Where(r => r.EventId == eventId
                            && r.Status == RegistrationStatus.Confirmed
                            && (!checkedInOnly.Value || r.CheckedInAt != null)
                            && !db.OutgoingEmails.Any(o => o.RegistrationId == r.Id
                                                          && o.Kind == EmailKind.Feedback
                                                          && (o.Status == EmailStatus.Pending || o.Status == EmailStatus.Sent)))
                .ToListAsync();

            foreach (var registration in due)
            {
                registration.ManageToken ??= Guid.NewGuid();
                EmailOutbox.Enqueue(db, registration, EmailKind.Feedback);
            }
            await db.SaveChangesAsync();
            if (due.Count > 0) emailSignal.Wake();
            return Results.Ok(new { queued = due.Count });
        });

        admin.MapGet("/export.csv", async (int eventId, AppDbContext db) =>
        {
            var rows = await Rows(db, eventId);
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(',',
                "Name", "Email", "Phone", "Finished", "UpdatedAt",
                "1. Expecting", "1. Expecting (other)", "2. Met expectations", "3. Could be better",
                "4. Favourite part", "5. Join the community", "6. Could contribute", "6. Could contribute (other)",
                "7. Dishes", "8. Regional food", "9. Music", "10. Dances and performances", "10. Other suggestions",
                "11. Other traditions", "12. Missing", "13. More engaging", "14. Ideas", "15. Anything else"));
            foreach (var r in rows)
            {
                var a = r.Answers;
                sb.AppendLine(string.Join(',',
                    Csv(r.FullName), Csv(r.Email), Csv(r.Phone), r.IsComplete ? "Yes" : "No",
                    r.UpdatedAt.ToString("yyyy-MM-dd HH:mm"),
                    Csv(string.Join("; ", (a.Expectations ?? []).Select(c => FeedbackOptions.Expectations.GetValueOrDefault(c, c)))),
                    Csv(a.ExpectationsOther),
                    Csv(a.MetExpectations is { } met ? FeedbackOptions.Met.GetValueOrDefault(met, met) : null),
                    Csv(a.Improvements), Csv(a.FavouritePart),
                    Csv(a.JoinCommunity is { } join ? FeedbackOptions.Join.GetValueOrDefault(join, join) : null),
                    Csv(string.Join("; ", (a.Contributions ?? []).Select(c => FeedbackOptions.Contributions.GetValueOrDefault(c, c)))),
                    Csv(a.ContributionsOther), Csv(a.Dishes), Csv(a.RegionalFood),
                    Csv(string.Join("; ", (a.Music ?? []).Select(c => FeedbackOptions.Music.GetValueOrDefault(c, c)))),
                    Csv(string.Join("; ", (a.Dances ?? []).Select(c => FeedbackOptions.Dances.GetValueOrDefault(c, c)))),
                    Csv(a.DancesOther), Csv(a.Traditions), Csv(a.Missing), Csv(a.Engagement), Csv(a.Ideas),
                    Csv(a.AnythingElse)));
            }
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
                "text/csv", $"feedback-event-{eventId}.csv");
        });
    }

    /// <summary>Only people who had a place can leave feedback; a cancelled or rejected signup's link stops working.</summary>
    private static Task<Registration?> FindAsync(AppDbContext db, Guid token) =>
        db.Registrations.Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.ManageToken == token && r.Status == RegistrationStatus.Confirmed);

    private static async Task<List<FeedbackRowDto>> Rows(AppDbContext db, int eventId)
    {
        var responses = await db.FeedbackResponses
            .Include(f => f.Registration)
            .Where(f => f.EventId == eventId)
            .ToListAsync();
        return responses
            .OrderByDescending(f => f.UpdatedAt)
            .Select(f => new FeedbackRowDto(f.Id, f.RegistrationId, f.Registration!.FullName, f.Registration.Email,
                f.Registration.Phone, ToAnswers(f), f.IsComplete, f.UpdatedAt))
            .ToList();
    }

    private static void Apply(FeedbackResponse f, FeedbackAnswers a)
    {
        f.Expectations = FeedbackOptions.Codes(a.Expectations, FeedbackOptions.Expectations);
        f.ExpectationsOther = Text(a.ExpectationsOther);
        f.MetExpectations = FeedbackOptions.Code(a.MetExpectations, FeedbackOptions.Met);
        f.Improvements = Text(a.Improvements);
        f.FavouritePart = Text(a.FavouritePart);
        f.JoinCommunity = FeedbackOptions.Code(a.JoinCommunity, FeedbackOptions.Join);
        f.Contributions = FeedbackOptions.Codes(a.Contributions, FeedbackOptions.Contributions);
        f.ContributionsOther = Text(a.ContributionsOther);
        f.Dishes = Text(a.Dishes);
        f.RegionalFood = Text(a.RegionalFood);
        f.Music = FeedbackOptions.Codes(a.Music, FeedbackOptions.Music);
        f.Dances = FeedbackOptions.Codes(a.Dances, FeedbackOptions.Dances);
        f.DancesOther = Text(a.DancesOther);
        f.Traditions = Text(a.Traditions);
        f.Missing = Text(a.Missing);
        f.Engagement = Text(a.Engagement);
        f.Ideas = Text(a.Ideas);
        f.AnythingElse = Text(a.AnythingElse);
    }

    private static FeedbackAnswers ToAnswers(FeedbackResponse f) => new(
        FeedbackOptions.Split(f.Expectations), f.ExpectationsOther, f.MetExpectations, f.Improvements,
        f.FavouritePart, f.JoinCommunity, FeedbackOptions.Split(f.Contributions), f.ContributionsOther,
        f.Dishes, f.RegionalFood, FeedbackOptions.Split(f.Music), FeedbackOptions.Split(f.Dances), f.DancesOther,
        f.Traditions, f.Missing, f.Engagement, f.Ideas, f.AnythingElse);

    private static FeedbackFormDto ToFormDto(Registration r, FeedbackResponse? f)
    {
        var ev = r.Event!;
        var firstName = r.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return new FeedbackFormDto(firstName, r.Language,
            new FeedbackEventDto(ev.TitleEn, ev.TitleNl, ev.TitleAr, ev.Date),
            f is null ? null : ToAnswers(f), f?.IsComplete ?? false);
    }

    private static string? Text(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length > MaxText ? trimmed[..MaxText] : trimmed;
    }

    private static string Csv(string? value)
    {
        value ??= "";
        // A leading = + - @ would run as a formula when the file is opened in Excel.
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }
}
