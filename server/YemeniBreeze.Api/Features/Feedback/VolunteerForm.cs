using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;

namespace YemeniBreeze.Api.Features.Feedback;

/// <summary>A piece of text in the site's three languages. Dutch and Arabic may be empty; English is the fallback.</summary>
public record LocalizedText(string En, string Nl, string Ar)
{
    public string For(string lang) => lang switch
    {
        "nl" when !string.IsNullOrWhiteSpace(Nl) => Nl,
        "ar" when !string.IsNullOrWhiteSpace(Ar) => Ar,
        _ => En
    };
}

public record VolunteerFormOption(string Id, LocalizedText Label);

/// <summary>
/// One question on the Volunteer form. <see cref="Lock"/> marks the questions that can't be removed:
/// "contact" (name, email: needed to reach people) and "roles" (what they want to help with: the admin filters use it).
/// </summary>
public record VolunteerFormField(
    string Id, string Type, string? Lock, bool Required,
    LocalizedText Label, LocalizedText Help, List<VolunteerFormOption> Options);

/// <summary>
/// An answer to one of the form's other questions, stored on the sign-up with the question's wording
/// at the time, so it still reads right after the question is changed or removed.
/// <see cref="Value"/> holds text (yes/no as "yes" or "no"); <see cref="Choices"/> holds picked choices.
/// </summary>
public record VolunteerAnswer(string FieldId, string Type, LocalizedText Question, string? Value, List<LocalizedText>? Choices);

public static partial class VolunteerFormDefinition
{
    public const string NameId = "name";
    public const string EmailId = "email";
    public const string HelpId = "help";
    /// <summary>The "Something else" choice in the help question: picking it asks what else.</summary>
    public const string OtherOptionId = "other";

    public static readonly string[] Types = ["short", "long", "email", "phone", "one", "many", "yesno", "date"];
    public const int MaxFields = 40;
    public const int MaxOptions = 30;

    private static readonly Dictionary<string, (string Type, string Lock)> Core = new()
    {
        [NameId] = ("short", "contact"),
        [EmailId] = ("email", "contact"),
        [HelpId] = ("many", "roles")
    };

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool HasOptions(string type) => type is "one" or "many";

    private static LocalizedText T(string en, string nl, string ar) => new(en, nl, ar);
    private static LocalizedText None => T("", "", "");

    /// <summary>The form as it is before anyone edits it.</summary>
    public static List<VolunteerFormField> Defaults() =>
    [
        new(NameId, "short", "contact", true, T("Your name", "Je naam", "اسمك"), None, []),
        new(EmailId, "email", "contact", true, T("Your email", "Je e-mailadres", "بريدك الإلكتروني"), None, []),
        new("phone", "phone", null, false, T("Phone number", "Telefoonnummer", "رقم الهاتف"), None, []),
        new(HelpId, "many", "roles", true,
            T("What would you like to help with?", "Waarmee wil je helpen?", "بماذا تودّ أن تساعد؟"),
            T("Pick as many as you like.", "Kies er zoveel als je wilt.", "اختر ما تشاء."),
            [
                new("organise", T("Helping organise events", "Helpen met het organiseren van evenementen", "المساعدة في تنظيم الفعاليات")),
                new("cooking", T("Cooking or introducing traditional Yemeni dishes", "Koken of traditionele Jemenitische gerechten laten proeven", "الطبخ أو تقديم أطباق يمنية تقليدية")),
                new("traditions", T("Sharing Yemeni traditions and cultural knowledge", "Jemenitische tradities en culturele kennis delen", "مشاركة التقاليد والمعرفة الثقافية اليمنية")),
                new("performing", T("Music, singing or dancing", "Muziek, zang of dans", "الموسيقى أو الغناء أو الرقص")),
                new("workshops", T("Organising workshops or activities", "Workshops of activiteiten organiseren", "تنظيم ورش عمل أو أنشطة")),
                new("media", T("Photography, media or communication", "Fotografie, media of communicatie", "التصوير أو الإعلام أو التواصل")),
                new("volunteering", T("Volunteering and helping with events", "Vrijwilligerswerk en helpen bij evenementen", "التطوّع والمساعدة في الفعاليات")),
                new(OtherOptionId, T("Something else", "Iets anders", "شيء آخر"))
            ]),
        new("free", "one", null, false,
            T("When are you usually free?", "Wanneer heb je meestal tijd?", "متى تكون متاحًا عادةً؟"), None,
            [
                new("weekdays", T("Weekdays", "Doordeweeks", "أيام الأسبوع")),
                new("evenings", T("Evenings", "’s Avonds", "في المساء")),
                new("weekends", T("Weekends", "In het weekend", "في عطلة نهاية الأسبوع"))
            ]),
        new("about", "long", null, false,
            T("Tell us a little about yourself", "Vertel iets over jezelf", "حدّثنا قليلًا عن نفسك"),
            T("For example: a skill you have, an idea for an event, or when you are free.",
              "Bijvoorbeeld: iets waar je goed in bent, een idee voor een evenement, of wanneer je tijd hebt.",
              "مثلًا: مهارة لديك، أو فكرة لفعالية، أو متى تكون متاحًا."), [])
    ];

    public static async Task<(List<VolunteerFormField> Fields, DateTime? UpdatedAt)> LoadAsync(AppDbContext db)
    {
        var row = await db.VolunteerForms.OrderBy(f => f.Id).FirstOrDefaultAsync();
        if (row is null) return (Defaults(), null);
        var fields = JsonSerializer.Deserialize<List<VolunteerFormField>>(row.FieldsJson, Json);
        return (fields is { Count: > 0 } ? fields : Defaults(), row.UpdatedAt);
    }

    /// <summary>Checks an edited form and returns it cleaned up, or the reason it can't be saved.</summary>
    public static (List<VolunteerFormField>? Fields, string? Error) Validate(List<VolunteerFormField>? input)
    {
        if (input is null || input.Count == 0) return (null, "The form needs at least one question.");
        if (input.Count > MaxFields) return (null, $"The form can have up to {MaxFields} questions.");

        var result = new List<VolunteerFormField>();
        var ids = new HashSet<string>();
        foreach (var f in input)
        {
            var id = f.Id?.Trim() ?? "";
            if (!IdPattern().IsMatch(id)) return (null, "A question has an invalid id.");
            if (!ids.Add(id)) return (null, "Two questions have the same id.");

            var label = Clean(f.Label, 200);
            if (label.En == "") return (null, "Every question needs English wording.");
            var help = Clean(f.Help, 300);

            string type;
            string? locked = null;
            var required = f.Required;
            if (Core.TryGetValue(id, out var core))
            {
                // The core questions keep their answer type; name and email are always required.
                type = core.Type;
                locked = core.Lock;
                if (core.Lock == "contact") required = true;
            }
            else
            {
                type = f.Type?.Trim() ?? "";
                if (!Types.Contains(type) || type == "email") return (null, $"“{label.En}” has an unknown answer type.");
            }

            var options = new List<VolunteerFormOption>();
            if (HasOptions(type))
            {
                var optionIds = new HashSet<string>();
                foreach (var o in f.Options ?? [])
                {
                    var optionId = o.Id?.Trim() ?? "";
                    if (!IdPattern().IsMatch(optionId) || !optionIds.Add(optionId))
                        return (null, $"“{label.En}” has a choice with an invalid or repeated id.");
                    var optionLabel = Clean(o.Label, 200);
                    if (optionLabel.En == "") return (null, $"Every choice in “{label.En}” needs English wording.");
                    options.Add(new VolunteerFormOption(optionId, optionLabel));
                }
                if (options.Count == 0) return (null, $"“{label.En}” needs at least one choice.");
                if (options.Count > MaxOptions) return (null, $"“{label.En}” can have up to {MaxOptions} choices.");
            }

            result.Add(new VolunteerFormField(id, type, locked, required, label, help, options));
        }

        if (Core.Keys.Any(k => !ids.Contains(k)))
            return (null, "The name, email and “What would you like to help with?” questions can't be removed.");
        return (result, null);
    }

    private static LocalizedText Clean(LocalizedText? text, int max) =>
        new(Trim(text?.En, max), Trim(text?.Nl, max), Trim(text?.Ar, max));

    private static string Trim(string? value, int max)
    {
        var v = value?.Trim() ?? "";
        return v.Length > max ? v[..max] : v;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,39}$")]
    private static partial Regex IdPattern();
}
