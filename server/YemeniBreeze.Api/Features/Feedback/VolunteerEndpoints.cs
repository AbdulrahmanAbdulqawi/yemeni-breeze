using System.Text;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Feedback;

public record VolunteerEventDto(int Id, string TitleEn, string TitleNl, string TitleAr, DateOnly Date);

/// <summary>
/// One person who said "yes" or "maybe" to taking part. Someone who came to several events shows once,
/// with their latest answers; <see cref="Id"/> is that latest feedback response.
/// </summary>
public record VolunteerDto(
    int Id, string FullName, string Email, string? Phone, string Language, string Interest,
    string[] Contributions, string? ContributionsOther, string? Ideas, string? AnythingElse,
    VolunteerEventDto Event, int EventsCount, DateTime UpdatedAt, bool Contacted);

public record VolunteerContactedRequest(bool Contacted);

public static class VolunteerEndpoints
{
    private static readonly string[] Interested = ["yes", "maybe"];

    public static void MapVolunteerEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/volunteers").RequireAuthorization();

        admin.MapGet("/", async (AppDbContext db) => Results.Ok(await ListAsync(db)));

        // Marks the person (all their feedback responses, matched by email) as contacted or not.
        admin.MapPost("/{id:int}/contacted", async (int id, VolunteerContactedRequest request, AppDbContext db) =>
        {
            var response = await db.FeedbackResponses.Include(f => f.Registration)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (response is null) return Results.NotFound();

            var email = Key(response.Registration!.Email);
            var all = await db.FeedbackResponses
                .Where(f => f.Registration!.Email.Trim().ToLower() == email)
                .ToListAsync();
            var when = request.Contacted ? DateTime.UtcNow : (DateTime?)null;
            foreach (var f in all) f.ContactedAt = when;
            await db.SaveChangesAsync();
            return Results.Ok(new { contacted = request.Contacted });
        });

        admin.MapGet("/export.csv", async (AppDbContext db) =>
        {
            var rows = await ListAsync(db);
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(',', "Name", "Email", "Phone", "Language", "Interest", "Wants to help with",
                "Other", "Ideas", "Anything else", "Latest event", "Events", "Replied", "Contacted"));
            foreach (var v in rows)
            {
                sb.AppendLine(string.Join(',',
                    Csv(v.FullName), Csv(v.Email), Csv(v.Phone), Csv(v.Language),
                    Csv(FeedbackOptions.Join.GetValueOrDefault(v.Interest, v.Interest)),
                    Csv(string.Join("; ", v.Contributions.Select(c => FeedbackOptions.Contributions.GetValueOrDefault(c, c)))),
                    Csv(v.ContributionsOther), Csv(v.Ideas), Csv(v.AnythingElse),
                    Csv(v.Event.TitleEn), v.EventsCount, v.UpdatedAt.ToString("yyyy-MM-dd"),
                    v.Contacted ? "Yes" : "No"));
            }
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
                "text/csv", "volunteers.csv");
        });
    }

    private static async Task<List<VolunteerDto>> ListAsync(AppDbContext db)
    {
        var responses = await db.FeedbackResponses
            .Include(f => f.Registration).ThenInclude(r => r!.Event)
            .Where(f => f.JoinCommunity != null)
            .ToListAsync();

        // Their latest answer decides: someone who said "yes" once and "not at the moment" later is left out.
        return responses
            .GroupBy(f => Key(f.Registration!.Email))
            .Select(group =>
            {
                var latest = group.OrderByDescending(f => f.UpdatedAt).First();
                return new { latest, group };
            })
            .Where(x => Interested.Contains(x.latest.JoinCommunity))
            .OrderByDescending(x => x.latest.UpdatedAt)
            .Select(x =>
            {
                var f = x.latest;
                var r = f.Registration!;
                var ev = r.Event!;
                return new VolunteerDto(f.Id, r.FullName, r.Email, r.Phone, r.Language, f.JoinCommunity!,
                    FeedbackOptions.Split(f.Contributions), f.ContributionsOther, f.Ideas, f.AnythingElse,
                    new VolunteerEventDto(ev.Id, ev.TitleEn, ev.TitleNl, ev.TitleAr, ev.Date),
                    x.group.Select(g => g.EventId).Distinct().Count(), f.UpdatedAt,
                    x.group.Any(g => g.ContactedAt != null));
            })
            .ToList();
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
}
