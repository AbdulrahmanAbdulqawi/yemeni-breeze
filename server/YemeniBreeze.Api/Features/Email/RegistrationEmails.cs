using System.Globalization;
using System.Net;
using System.Text;
using QRCoder;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Email;

/// <summary>A rendered email, ready for any provider.</summary>
public record EmailContent(string Subject, string Html, string Text, IReadOnlyList<EmailAttachment> Attachments);

/// <summary>
/// Builds the registration emails (confirmed, promoted, waitlisted, reminder, cancelled, rejected) in the site's identity:
/// espresso browns, cream paper, gold accents, the qamariya logo and its stained-glass colours.
/// Markup is table-based with inline styles so it survives Gmail, Outlook and Apple Mail.
/// </summary>
public static class RegistrationEmails
{
    public const string LogoContentId = "yb-logo";
    public const string QrContentId = "ticket-qr";

    // Same account the site's footer and contact page link to
    private const string InstagramUrl = "https://instagram.com/Yemeni.breeze";
    private const string InstagramHandle = "@Yemeni.breeze";

    // Design tokens, mirrored from client/src/styles.scss
    private const string Brown = "#310f02";
    private const string BrownSoft = "#4b2312";
    private const string Maroon = "#693b28";
    private const string Cream = "#fdf9ea";
    private const string Surface = "#f2eedf";
    private const string SurfaceLow = "#f8f4e5";
    private const string Outline = "#d6c2bb";
    private const string Gold = "#ffe088";
    private const string GoldDim = "#e9c349";
    private const string Ink = "#1c1c13";

    // The logo's stained-glass panes, used as a thin colour strip
    private static readonly string[] GlassColours =
        ["#5e1d04", "#8f1b04", "#db4440", "#ed9e42", "#edcb4e", "#3bab4b", "#2b2be0"];

    private static readonly Lazy<byte[]> Logo = new(() =>
    {
        using var stream = typeof(RegistrationEmails).Assembly
            .GetManifestResourceStream("YemeniBreeze.Api.Features.Email.Assets.email-logo.png")
            ?? throw new InvalidOperationException("Email logo resource is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    });

    /// <summary>
    /// Renders the email of the given kind, or returns null when it no longer fits the
    /// registration (e.g. a ticket email for a registration that has since been cancelled).
    /// </summary>
    public static EmailContent? Build(Registration r, Event ev, EmailKind kind, string publicBaseUrl)
    {
        var requiredStatus = kind switch
        {
            EmailKind.Waitlisted => RegistrationStatus.Waitlisted,
            EmailKind.Cancelled => RegistrationStatus.Cancelled,
            EmailKind.Rejected => RegistrationStatus.Rejected,
            _ => RegistrationStatus.Confirmed
        };
        if (r.Status != requiredStatus) return null;

        var t = Strings(r.Language);
        var baseUrl = publicBaseUrl.TrimEnd('/');
        var eventUrl = $"{baseUrl}/events/{Uri.EscapeDataString(ev.Slug)}";
        var manageUrl = kind is not (EmailKind.Cancelled or EmailKind.Rejected) && r.ManageToken is { } token
            ? $"{baseUrl}/registrations/{token:D}"
            : null;
        var withTicket = r.Status == RegistrationStatus.Confirmed;

        var key = kind switch
        {
            EmailKind.Promoted => "promoted",
            EmailKind.Waitlisted => "waitlist",
            EmailKind.Cancelled => "cancelled",
            EmailKind.Reminder => "reminder",
            EmailKind.Rejected => "rejected",
            _ => "confirmed"
        };
        var subject = string.Format(t[key + "Subject"], EventTitle(r, ev));
        // A "sorry" email stays short and personal: the event's name as the heading, no details.
        var showEvent = kind != EmailKind.Rejected;
        var heading = showEvent ? t[key + "Heading"] : EventTitle(r, ev);
        var intro = t[key + "Intro"];
        var greeting = string.Format(t["greeting"], FirstName(r.FullName));
        var preheader = t[key + "Preheader"];
        var note = t.GetValueOrDefault(key + "Note");

        var attachments = new List<EmailAttachment>
        {
            new("yemen-breeze.png", Logo.Value, "image/png", LogoContentId)
        };
        if (withTicket)
        {
            attachments.Add(new("ticket.png", GenerateTicketQr(r.TicketCode), "image/png", QrContentId));
            attachments.Add(new("yemen-breeze-event.ics",
                Encoding.UTF8.GetBytes(BuildIcs(r, ev, eventUrl)), "text/calendar"));
        }

        var body = new StringBuilder();
        body.Append(Paragraph(H(greeting)));
        body.Append(Paragraph(H(intro)));
        if (showEvent)
        {
            if (AbsoluteUrl(ev.ImageUrl, baseUrl) is { } imageUrl) body.Append(EventImage(imageUrl, eventUrl, EventTitle(r, ev)));
            body.Append(EventCard(r, ev, t));
        }
        if (withTicket) body.Append(TicketCard(r, t));
        if (note is not null) body.Append(Paragraph(H(note)));
        // Emails that carry the event's details ask people to keep them to registered guests.
        var askNotToShare = kind is EmailKind.Confirmed or EmailKind.Promoted or EmailKind.Reminder or EmailKind.Waitlisted;
        if (askNotToShare) body.Append(ShareNote(t, eventUrl));
        if (showEvent) body.Append(Button(t["viewEvent"], eventUrl));
        if (withTicket) body.Append(Small(t["calendarNote"]));
        // Only in the "you're in" emails, and only when the event has a donation link.
        var donationUrl = kind is EmailKind.Confirmed or EmailKind.Promoted ? DonationLink(ev) : null;
        if (donationUrl is not null) body.Append(DonationBlock(t, donationUrl));
        if (manageUrl is not null) body.Append(ManageLink(t, manageUrl));

        var html = Layout(r.Language, subject, preheader, heading, body.ToString(), t, baseUrl);
        var text = PlainText(r, ev, t, heading, greeting, intro, note, withTicket, showEvent ? eventUrl : null,
            askNotToShare, donationUrl, manageUrl, baseUrl);
        return new EmailContent(subject, html, text, attachments);
    }

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string FirstName(string fullName) =>
        fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;

    /// <summary>Uploaded images are stored as site-relative paths; mail clients need the full address.</summary>
    private static string? AbsoluteUrl(string? url, string baseUrl) =>
        string.IsNullOrWhiteSpace(url) ? null
        : url.StartsWith("http://") || url.StartsWith("https://") ? url
        : url.StartsWith('/') ? baseUrl + url
        : null;

    private static bool IsRtl(string lang) => lang == "ar";

    private static string EventTitle(Registration r, Event ev) => r.Language switch
    {
        "nl" when !string.IsNullOrWhiteSpace(ev.TitleNl) => ev.TitleNl,
        "ar" when !string.IsNullOrWhiteSpace(ev.TitleAr) => ev.TitleAr,
        _ => ev.TitleEn
    };

    private static string FormatDate(DateOnly date, string lang)
    {
        var culture = lang switch { "nl" => "nl-NL", "ar" => "ar-EG", _ => "en-GB" };
        try
        {
            return date.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo(culture));
        }
        catch (CultureNotFoundException)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }

    private static string TimeRange(Event ev) =>
        string.IsNullOrWhiteSpace(ev.EndTime) ? ev.StartTime : $"{ev.StartTime} – {ev.EndTime}";

    /// <summary>Short, human-readable reference printed under the QR code.</summary>
    private static string TicketRef(Guid ticket) => ticket.ToString("N")[..8].ToUpperInvariant();

    private static byte[] GenerateTicketQr(Guid ticketCode)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(ticketCode.ToString(), QRCodeGenerator.ECCLevel.M);
        // Brown modules on white keep scanners happy while matching the palette.
        return new PngByteQRCode(data).GetGraphic(10, [0x31, 0x0f, 0x02, 0xff], [0xff, 0xff, 0xff, 0xff]);
    }

    // ---------- HTML building blocks ----------

    private static string HeadingFont(string lang) => IsRtl(lang)
        ? "Amiri,'Times New Roman',serif"
        : "'Libre Caslon Text',Georgia,'Times New Roman',serif";

    private static string BodyFont(string lang) => IsRtl(lang)
        ? "Cairo,Tahoma,Arial,sans-serif"
        : "Manrope,'Segoe UI',Helvetica,Arial,sans-serif";

    private static string Paragraph(string html) =>
        $"""<p style="margin:0 0 18px;font-size:16px;line-height:1.65;color:{Ink}">{html}</p>""";

    private static string Small(string text) =>
        $"""<p style="margin:14px 0 0;font-size:13px;line-height:1.5;color:{Maroon};text-align:center">{H(text)}</p>""";

    private static string Button(string label, string url) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:26px auto 0">
          <tr><td bgcolor="{Maroon}" style="border-radius:999px">
            <a href="{H(url)}" style="display:inline-block;padding:13px 30px;font-size:15px;font-weight:700;color:{Cream};text-decoration:none;border-radius:999px">{H(label)}</a>
          </td></tr>
        </table>
        """;

    private static string ManageLink(IReadOnlyDictionary<string, string> t, string url) => $"""
        <p style="margin:22px 0 0;padding-top:16px;border-top:1px solid {Outline};font-size:13px;line-height:1.5;color:{Maroon};text-align:center">
          {H(t["cantMakeIt"])} <a href="{H(url)}" style="color:{Maroon};font-weight:700">{H(t["manageLink"])}</a>
        </p>
        """;

    private static string DetailRow(string icon, string label, string value, bool ltr = false) => $"""
        <tr>
          <td width="34" valign="top" style="padding:10px 0 10px;font-size:18px;line-height:1">{icon}</td>
          <td valign="top" style="padding:8px 0">
            <div style="font-size:11px;letter-spacing:1.5px;text-transform:uppercase;color:{Maroon};font-weight:700">{H(label)}</div>
            <div style="font-size:15px;color:{Ink};padding-top:2px">{(ltr ? $"<span dir=\"ltr\" style=\"unicode-bidi:isolate;display:inline-block\">{H(value)}</span>" : H(value))}</div>
          </td>
        </tr>
        """;

    private static string ShareNote(IReadOnlyDictionary<string, string> t, string eventUrl) => $"""
        <p style="margin:0 0 18px;padding:12px 16px;background:{SurfaceLow};border:1px dashed {GoldDim};border-radius:10px;font-size:14px;line-height:1.6;color:{Ink}">
          🙏 {H(t["shareNote"])} <a href="{H(eventUrl)}" style="color:{Maroon};font-weight:700">{H(t["shareNoteLink"])}</a>
        </p>
        """;

    /// <summary>The event's donation link, if it is a full http(s) address.</summary>
    private static string? DonationLink(Event ev) =>
        Uri.TryCreate(ev.DonationUrl?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : null;

    private static string DonationBlock(IReadOnlyDictionary<string, string> t, string url) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
               style="margin:26px 0 0;background:{SurfaceLow};border:1px solid {Outline};border-radius:14px">
          <tr><td style="padding:20px 22px;text-align:center">
            <div style="font-size:22px;line-height:1">🤍</div>
            <p style="margin:10px 0 0;font-size:15px;line-height:1.6;color:{Ink}">{H(t["donationText"])}</p>
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:16px auto 0">
              <tr><td style="border:2px solid {GoldDim};border-radius:999px">
                <a href="{H(url)}" style="display:inline-block;padding:10px 26px;font-size:15px;font-weight:700;color:{Maroon};text-decoration:none;border-radius:999px">{H(t["donationButton"])}</a>
              </td></tr>
            </table>
          </td></tr>
        </table>
        """;

    /// <summary>The event's cover photo, linking to the event page.</summary>
    private static string EventImage(string imageUrl, string eventUrl, string alt) => $"""
        <a href="{H(eventUrl)}" style="display:block;text-decoration:none;margin:0 0 18px">
          <img src="{H(imageUrl)}" width="520" alt="{H(alt)}" style="display:block;width:100%;max-width:520px;height:auto;border:0;border-radius:14px"/>
        </a>
        """;

    private static string EventCard(Registration r, Event ev, IReadOnlyDictionary<string, string> t)
    {
        var rows = new StringBuilder();
        rows.Append(DetailRow("📅", t["dateLabel"], FormatDate(ev.Date, r.Language)));
        if (!string.IsNullOrWhiteSpace(ev.StartTime)) rows.Append(DetailRow("🕓", t["timeLabel"], TimeRange(ev), ltr: true));
        if (!string.IsNullOrWhiteSpace(ev.Location)) rows.Append(DetailRow("📍", t["locationLabel"], ev.Location));

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
                   style="margin:6px 0 20px;background:{SurfaceLow};border:1px solid {Outline};border-radius:14px">
              <tr><td style="padding:20px 22px 12px">
                <div style="font-family:{HeadingFont(r.Language)};font-size:21px;line-height:1.3;color:{Maroon};font-weight:700;padding-bottom:6px">{H(EventTitle(r, ev))}</div>
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">{rows}</table>
              </td></tr>
            </table>
            """;
    }

    private static string TicketCard(Registration r, IReadOnlyDictionary<string, string> t) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
               style="margin:4px 0 4px;background:{Brown};border-radius:18px">
          <tr><td align="center" style="padding:22px 22px 0">
            <div style="font-size:11px;letter-spacing:3px;text-transform:uppercase;color:{GoldDim};font-weight:700">{H(t["ticketLabel"])}</div>
            <div style="font-size:14px;color:{Cream};opacity:.85;padding-top:6px">{H(t["ticketNote"])}</div>
          </td></tr>
          <tr><td align="center" style="padding:18px 22px 0">
            <!-- Arch frame, echoing the qamariya window -->
            <table role="presentation" cellpadding="0" cellspacing="0" border="0">
              <tr><td bgcolor="#ffffff" style="background:#ffffff;border:3px solid {GoldDim};border-radius:134px 134px 16px 16px;padding:52px 34px 26px">
                <img src="cid:{QrContentId}" width="200" height="200" alt="{H(t["qrAlt"])}" style="display:block;width:200px;height:200px;border:0"/>
              </td></tr>
            </table>
          </td></tr>
          <tr><td style="padding:20px 22px 0">
            <div style="border-top:2px dashed {BrownSoft};height:1px;line-height:1px;font-size:1px">&nbsp;</div>
          </td></tr>
          <tr><td align="center" style="padding:16px 22px 22px">
            <div style="font-family:{HeadingFont(r.Language)};font-size:20px;color:{Gold};font-weight:700">{H(r.FullName)}</div>
            <div style="font-size:14px;color:{Cream};padding-top:4px">{r.GuestsCount} {H(r.GuestsCount == 1 ? t["guest"] : t["guests"])} &nbsp;·&nbsp; {H(t["refLabel"])} <span style="font-family:'Courier New',monospace;letter-spacing:2px;color:{Gold}">{TicketRef(r.TicketCode)}</span></div>
          </td></tr>
        </table>
        """;

    private static string GlassStrip()
    {
        var cells = string.Concat(GlassColours.Select(c =>
            $"""<td bgcolor="{c}" style="background:{c};height:6px;line-height:6px;font-size:1px">&nbsp;</td>"""));
        return $"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>{cells}</tr></table>""";
    }

    private static string Layout(string lang, string subject, string preheader, string heading, string body,
        IReadOnlyDictionary<string, string> t, string baseUrl)
    {
        var dir = IsRtl(lang) ? "rtl" : "ltr";
        var align = IsRtl(lang) ? "right" : "left";
        // Gmail drops <html dir>, so every block that holds text carries dir itself.
        return $$"""
            <!doctype html>
            <html dir="{{dir}}" lang="{{lang}}">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta name="color-scheme" content="light">
              <meta name="supported-color-schemes" content="light">
              <title>{{H(subject)}}</title>
              <link href="https://fonts.googleapis.com/css2?family=Libre+Caslon+Text:wght@400;700&family=Manrope:wght@400;600;700&family=Amiri:wght@400;700&family=Cairo:wght@400;600;700&display=swap" rel="stylesheet">
              <style>
                body { margin:0; padding:0; -webkit-text-size-adjust:100%; }
                a { color: {{Maroon}}; }
                @media (max-width:620px) {
                  .yb-pad { padding-left:20px !important; padding-right:20px !important; }
                  .yb-h1 { font-size:24px !important; }
                }
              </style>
            </head>
            <body dir="{{dir}}" style="margin:0;padding:0;background:{{Surface}}">
              <div dir="{{dir}}" style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent">{{H(preheader)}}</div>
              <table role="presentation" dir="{{dir}}" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="{{Surface}}" style="background:{{Surface}}">
                <tr><td align="center" style="padding:28px 12px">
                  <table role="presentation" dir="{{dir}}" width="100%" cellpadding="0" cellspacing="0" border="0"
                         style="max-width:600px;background:{{Cream}};border-radius:20px;overflow:hidden;border:1px solid {{Outline}}">
                    <tr><td>{{GlassStrip()}}</td></tr>
                    <tr><td align="center" style="padding:30px 24px 22px">
                      <a href="{{H(baseUrl)}}" style="text-decoration:none">
                        <img src="cid:{{LogoContentId}}" width="200" alt="Yemen Breeze · نسمات اليمن" style="display:block;width:200px;max-width:70%;height:auto;border:0;margin:0 auto"/>
                      </a>
                    </td></tr>
                    <tr><td class="yb-pad" dir="{{dir}}" bgcolor="{{Brown}}" style="background:{{Brown}};padding:26px 40px;text-align:center">
                      <h1 class="yb-h1" style="margin:0;font-family:{{HeadingFont(lang)}};font-size:28px;line-height:1.3;font-weight:700;color:{{Gold}}">{{H(heading)}}</h1>
                    </td></tr>
                    <tr><td class="yb-pad" dir="{{dir}}" style="padding:32px 40px 36px;text-align:{{align}};font-family:{{BodyFont(lang)}};color:{{Ink}}">
                      {{body}}
                      <p style="margin:30px 0 0;font-size:16px;line-height:1.6;color:{{Ink}}">{{H(t["signoff"])}}<br/>
                        <strong style="font-family:{{HeadingFont(lang)}};color:{{Maroon}};font-size:18px">{{H(t["team"])}}</strong></p>
                    </td></tr>
                    <tr><td class="yb-pad" dir="{{dir}}" bgcolor="{{BrownSoft}}" style="background:{{BrownSoft}};padding:24px 40px;text-align:center;font-family:{{BodyFont(lang)}}">
                      <div style="font-family:{{HeadingFont(lang)}};font-size:15px;line-height:1.6;color:{{Gold}}">{{H(t["footer"])}}</div>
                      <div style="padding-top:10px;font-size:13px;color:{{Cream}}">
                        {{H(t["followUs"])}}
                        <a href="{{InstagramUrl}}" style="color:{{Cream}};font-weight:700;text-decoration:underline"><span dir="ltr" style="unicode-bidi:isolate">{{InstagramHandle}}</span></a>
                      </div>
                      <div style="padding-top:10px;font-size:12px;line-height:1.5;color:{{Outline}}">{{H(t["whyReceived"])}}</div>
                    </td></tr>
                    <tr><td>{{GlassStrip()}}</td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string PlainText(Registration r, Event ev, IReadOnlyDictionary<string, string> t,
        string heading, string greeting, string intro, string? note, bool confirmed,
        string? eventUrl, bool askNotToShare, string? donationUrl, string? manageUrl, string baseUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("YEMEN BREEZE · نسمات اليمن").AppendLine();
        sb.AppendLine(heading).AppendLine();
        sb.AppendLine(greeting).AppendLine();
        sb.AppendLine(intro).AppendLine();
        if (eventUrl is not null) // no event details in a "sorry" email
        {
            sb.AppendLine(EventTitle(r, ev));
            sb.AppendLine($"{t["dateLabel"]}: {FormatDate(ev.Date, r.Language)}");
            if (!string.IsNullOrWhiteSpace(ev.StartTime)) sb.AppendLine($"{t["timeLabel"]}: \u2066{TimeRange(ev)}\u2069");
            if (!string.IsNullOrWhiteSpace(ev.Location)) sb.AppendLine($"{t["locationLabel"]}: {ev.Location}");
            sb.AppendLine();
        }
        if (confirmed)
        {
            sb.AppendLine($"{t["ticketLabel"]}: {r.FullName} — {r.GuestsCount} {(r.GuestsCount == 1 ? t["guest"] : t["guests"])}");
            sb.AppendLine($"{t["refLabel"]} {TicketRef(r.TicketCode)}");
            sb.AppendLine(t["ticketNote"]).AppendLine();
        }
        if (note is not null) sb.AppendLine(note).AppendLine();
        if (askNotToShare && eventUrl is not null)
            sb.AppendLine(t["shareNote"]).AppendLine($"{t["shareNoteLink"]}: {eventUrl}").AppendLine();
        if (eventUrl is not null) sb.AppendLine($"{t["viewEvent"]}: {eventUrl}").AppendLine();
        if (donationUrl is not null) sb.AppendLine(t["donationText"]).AppendLine($"{t["donationButton"]}: {donationUrl}").AppendLine();
        if (manageUrl is not null) sb.AppendLine($"{t["cantMakeIt"]} {t["manageLink"]}: {manageUrl}").AppendLine();
        sb.AppendLine(t["signoff"]).AppendLine(t["team"]).AppendLine();
        sb.AppendLine(t["footer"]).AppendLine($"{t["followUs"]} {InstagramHandle} — {InstagramUrl}");
        return sb.ToString();
    }

    // ---------- Calendar ----------

    private static string IcsText(string value) => value
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
        .Replace("\r\n", "\\n").Replace("\n", "\\n");

    private static string BuildIcs(Registration r, Event ev, string eventUrl)
    {
        static string LocalStamp(DateOnly date, string time)
        {
            var parts = time.Split(':');
            var local = new DateTime(date.Year, date.Month, date.Day,
                parts.Length > 0 && int.TryParse(parts[0], out var h) ? h : 0,
                parts.Length > 1 && int.TryParse(parts[1], out var m) ? m : 0, 0);
            return local.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
        }

        return $"""
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Yemen Breeze//Events//EN
            CALSCALE:GREGORIAN
            METHOD:PUBLISH
            BEGIN:VTIMEZONE
            TZID:Europe/Amsterdam
            BEGIN:DAYLIGHT
            TZOFFSETFROM:+0100
            TZOFFSETTO:+0200
            TZNAME:CEST
            DTSTART:19700329T020000
            RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU
            END:DAYLIGHT
            BEGIN:STANDARD
            TZOFFSETFROM:+0200
            TZOFFSETTO:+0100
            TZNAME:CET
            DTSTART:19701025T030000
            RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU
            END:STANDARD
            END:VTIMEZONE
            BEGIN:VEVENT
            UID:{r.TicketCode}@yemenibreeze
            DTSTAMP:{DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}
            DTSTART;TZID=Europe/Amsterdam:{LocalStamp(ev.Date, ev.StartTime)}
            DTEND;TZID=Europe/Amsterdam:{LocalStamp(ev.Date, ev.EndTime)}
            SUMMARY:{IcsText(EventTitle(r, ev))}
            LOCATION:{IcsText(ev.Location)}
            URL:{eventUrl}
            END:VEVENT
            END:VCALENDAR
            """.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    // ---------- Copy ----------

    private static IReadOnlyDictionary<string, string> Strings(string lang) => lang switch
    {
        "nl" => new Dictionary<string, string>
        {
            ["confirmedSubject"] = "Je bent erbij! {0} 🎉",
            ["confirmedHeading"] = "Joepie, je bent erbij!",
            ["confirmedIntro"] = "Wat leuk dat je komt! Bewaar deze mail goed: je ticket staat hieronder. Laat de QR-code zien bij de deur, dan regelen wij de rest.",
            ["confirmedPreheader"] = "Je ticket zit erin — tot snel!",
            ["promotedSubject"] = "Er is een plek voor je vrij! {0} 🎉",
            ["promotedHeading"] = "Goed nieuws: er is een plek voor je!",
            ["promotedIntro"] = "Iemand kon toch niet komen, en die plek is nu van jou! Je ticket staat hieronder. We kijken ernaar uit je te zien.",
            ["promotedPreheader"] = "Er is een plek voor je vrijgekomen — je ticket zit erin.",
            ["waitlistSubject"] = "Je staat op de wachtlijst voor {0}",
            ["waitlistHeading"] = "Je staat op de lijst!",
            ["waitlistIntro"] = "Het evenement zit nu vol, maar geen zorgen: plannen veranderen vaak.",
            ["waitlistNote"] = "Zodra er een plek vrijkomt, sturen we je ticket meteen naar je inbox.",
            ["waitlistPreheader"] = "We laten het je meteen weten als er een plek vrijkomt.",
            ["reminderSubject"] = "Tot morgen bij {0}!",
            ["reminderHeading"] = "Tot morgen!",
            ["reminderIntro"] = "Een kleine herinnering: we kijken ernaar uit je morgen te zien! Je ticket staat hieronder, houd het klaar bij de deur.",
            ["reminderPreheader"] = "Morgen is het zover — je ticket zit erin.",
            ["cancelledSubject"] = "Je inschrijving is geannuleerd — {0}",
            ["cancelledHeading"] = "Geen probleem, het is geregeld",
            ["cancelledIntro"] = "We hebben je inschrijving geannuleerd. Fijn dat je het liet weten: je plek gaat nu naar iemand op de wachtlijst.",
            ["cancelledNote"] = "Toch van gedachten veranderd? Je kunt je gewoon opnieuw inschrijven op de evenementpagina, zolang er plek is.",
            ["cancelledPreheader"] = "Je annulering is gelukt.",
            ["rejectedSubject"] = "Over je aanmelding — {0}",
            ["rejectedIntro"] = "Wat leuk dat je erbij wilde zijn! Helaas kunnen we je deze keer geen plek geven.",
            ["rejectedNote"] = "We hopen je heel graag te zien bij een van onze volgende evenementen.",
            ["rejectedPreheader"] = "Een bericht over je aanmelding.",
            ["greeting"] = "Beste {0},",
            ["shareNote"] = "Een klein verzoek: deel de details van dit evenement alsjeblieft niet met mensen die zich niet hebben ingeschreven. Ken je iemand die ook wil komen? Die kan zich inschrijven via onze website.",
            ["shareNoteLink"] = "Hier inschrijven",
            ["donationText"] = "Wil je Yemeni Breeze steunen? Dan kun je via de link hieronder een vrijwillige donatie doen. Met je steun kunnen we culturele evenementen blijven organiseren en onze impact in de gemeenschap laten groeien.",
            ["donationButton"] = "Doneren",
            ["cantMakeIt"] = "Kun je toch niet komen?",
            ["manageLink"] = "Annuleer je inschrijving",
            ["dateLabel"] = "Datum",
            ["timeLabel"] = "Tijd",
            ["locationLabel"] = "Locatie",
            ["ticketLabel"] = "Je ticket",
            ["ticketNote"] = "Laat deze QR-code zien bij de deur",
            ["qrAlt"] = "QR-code van je ticket",
            ["refLabel"] = "Ref.",
            ["guest"] = "persoon",
            ["guests"] = "personen",
            ["viewEvent"] = "Bekijk het evenement",
            ["calendarNote"] = "We hebben een agenda-uitnodiging meegestuurd, zodat je het niet vergeet.",
            ["signoff"] = "Tot snel!",
            ["team"] = "Team Yemen Breeze",
            ["footer"] = "Wij VERBINDEN culturen. Wij TONEN Jemen. Wij BOUWEN bruggen.",
            ["followUs"] = "Volg ons op Instagram:",
            ["whyReceived"] = "Je ontvangt deze e-mail omdat je je hebt ingeschreven voor een evenement van Yemen Breeze."
        },
        "ar" => new Dictionary<string, string>
        {
            ["confirmedSubject"] = "أهلًا بك! تسجيلك مؤكد في {0} 🎉",
            ["confirmedHeading"] = "يا هلا! تسجيلك مؤكد",
            ["confirmedIntro"] = "يسعدنا انضمامك لنا في هذه الفعالية! احتفظ بهذه الرسالة، فتذكرتك في الأسفل. فقط أظهر رمز QR عند الباب، والباقي علينا.",
            ["confirmedPreheader"] = "تذكرتك هنا — نراك قريبًا!",
            ["promotedSubject"] = "خبر حلو: صار لك مقعد في {0} 🎉",
            ["promotedHeading"] = "خبر حلو: صار لك مقعد!",
            ["promotedIntro"] = "أحد المسجلين لم يتمكن من الحضور، ومقعده صار لك الآن! تذكرتك في الأسفل، ونحن متحمسون لرؤيتك.",
            ["promotedPreheader"] = "صار لك مقعد — تذكرتك هنا.",
            ["waitlistSubject"] = "أنت على قائمة الانتظار في {0}",
            ["waitlistHeading"] = "اسمك على القائمة!",
            ["waitlistIntro"] = "الفعالية ممتلئة حاليًا، لكن لا تقلق، فالخطط تتغير كثيرًا.",
            ["waitlistNote"] = "أول ما يتوفّر مقعد، سنرسل تذكرتك مباشرة إلى بريدك.",
            ["waitlistPreheader"] = "سنخبرك فور توفّر مقعد.",
            ["reminderSubject"] = "نراك غدًا في {0}!",
            ["reminderHeading"] = "نراك غدًا!",
            ["reminderIntro"] = "تذكير صغير: ننتظرك غدًا بكل شوق! تذكرتك في الأسفل، جهّزها عند الباب.",
            ["reminderPreheader"] = "الموعد غدًا — تذكرتك هنا.",
            ["cancelledSubject"] = "تم إلغاء تسجيلك في {0}",
            ["cancelledHeading"] = "ولا يهمك، تم الإلغاء",
            ["cancelledIntro"] = "ألغينا تسجيلك، وشكرًا لأنك أخبرتنا! سيذهب مقعدك الآن لشخص ينتظر.",
            ["cancelledNote"] = "غيّرت رأيك؟ يمكنك التسجيل من جديد من صفحة الفعالية ما دامت هناك مقاعد.",
            ["cancelledPreheader"] = "تم إلغاء تسجيلك بنجاح.",
            ["rejectedSubject"] = "بخصوص طلب تسجيلك في {0}",
            ["rejectedIntro"] = "سعدنا كثيرًا برغبتك في الانضمام إلينا! للأسف لا نستطيع أن نوفّر لك مقعدًا هذه المرة.",
            ["rejectedNote"] = "نتمنى أن نراك في إحدى فعالياتنا القادمة.",
            ["rejectedPreheader"] = "رسالة بخصوص طلب تسجيلك.",
            ["greeting"] = "مرحبًا {0}،",
            ["shareNote"] = "طلب صغير: نرجو ألّا تشارك تفاصيل الفعالية مع أي شخص غير مسجّل. هل تعرف أحدًا يرغب في الانضمام؟ يمكنه التسجيل عبر موقعنا.",
            ["shareNoteLink"] = "سجّل من هنا",
            ["donationText"] = "إذا رغبت في دعم نسمات اليمن، يمكنك تقديم تبرّع طوعي عبر الرابط أدناه. دعمك يساعدنا على الاستمرار في تنظيم الفعاليات الثقافية وتوسيع أثرنا في المجتمع.",
            ["donationButton"] = "تبرّع الآن",
            ["cantMakeIt"] = "لن تتمكن من الحضور؟",
            ["manageLink"] = "ألغِ تسجيلك",
            ["dateLabel"] = "التاريخ",
            ["timeLabel"] = "الوقت",
            ["locationLabel"] = "المكان",
            ["ticketLabel"] = "تذكرتك",
            ["ticketNote"] = "أظهر رمز QR هذا عند الباب",
            ["qrAlt"] = "رمز QR لتذكرتك",
            ["refLabel"] = "المرجع",
            ["guest"] = "شخص",
            ["guests"] = "أشخاص",
            ["viewEvent"] = "عرض الفعالية",
            ["calendarNote"] = "أرفقنا لك دعوة تقويم حتى لا تنسى الموعد.",
            ["signoff"] = "نراك قريبًا!",
            ["team"] = "فريق نسمات اليمن",
            ["footer"] = "نَصِلُ الثقافات. نُظهِرُ اليمن. نَبني الجسور.",
            ["followUs"] = "تابعنا على إنستغرام:",
            ["whyReceived"] = "تصلك هذه الرسالة لأنك سجّلت في إحدى فعاليات نسمات اليمن."
        },
        _ => new Dictionary<string, string>
        {
            ["confirmedSubject"] = "You're in! {0} 🎉",
            ["confirmedHeading"] = "Yay, you're in!",
            ["confirmedIntro"] = "We're so happy you're joining us! Keep this email handy: your ticket is right below. Just show the QR code at the door and we'll take care of the rest.",
            ["confirmedPreheader"] = "Your ticket is inside — see you there!",
            ["promotedSubject"] = "A spot opened up for you! {0} 🎉",
            ["promotedHeading"] = "Good news: a spot opened up!",
            ["promotedIntro"] = "Someone couldn't make it, and their spot is now yours! Your ticket is right below. We can't wait to see you.",
            ["promotedPreheader"] = "A spot opened up for you — your ticket is inside.",
            ["waitlistSubject"] = "You're on the waitlist for {0}",
            ["waitlistHeading"] = "You're on the list!",
            ["waitlistIntro"] = "The event is full right now, but don't worry: plans change all the time.",
            ["waitlistNote"] = "The moment a spot opens up, we'll send your ticket straight to your inbox.",
            ["waitlistPreheader"] = "We'll let you know the moment a spot opens up.",
            ["reminderSubject"] = "See you tomorrow at {0}!",
            ["reminderHeading"] = "See you tomorrow!",
            ["reminderIntro"] = "Just a little reminder: we're looking forward to seeing you tomorrow! Your ticket is below, so have it ready at the door.",
            ["reminderPreheader"] = "It's tomorrow — your ticket is inside.",
            ["cancelledSubject"] = "Your registration is cancelled — {0}",
            ["cancelledHeading"] = "No worries, it's cancelled",
            ["cancelledIntro"] = "We've cancelled your registration. Thanks for letting us know: your spot now goes to someone on the waitlist.",
            ["cancelledNote"] = "Changed your mind? You can sign up again on the event page while there are spots left.",
            ["cancelledPreheader"] = "Your cancellation went through.",
            ["rejectedSubject"] = "About your registration — {0}",
            ["rejectedIntro"] = "We loved that you wanted to join us! Unfortunately, we can't offer you a place at this event this time.",
            ["rejectedNote"] = "We really hope to see you at one of our next events.",
            ["rejectedPreheader"] = "A message about your registration.",
            ["greeting"] = "Dear {0},",
            ["shareNote"] = "A small request: please keep the event details to yourself and don't share them with anyone who hasn't registered. Know someone who'd like to join? They can sign up through our website.",
            ["shareNoteLink"] = "Register here",
            ["donationText"] = "If you would like to support Yemeni Breeze, you can make a voluntary donation through the link below. Your support helps us continue organizing cultural events and growing our community impact.",
            ["donationButton"] = "Donate",
            ["cantMakeIt"] = "Can't make it after all?",
            ["manageLink"] = "Cancel your registration",
            ["dateLabel"] = "Date",
            ["timeLabel"] = "Time",
            ["locationLabel"] = "Location",
            ["ticketLabel"] = "Your ticket",
            ["ticketNote"] = "Show this QR code at the door",
            ["qrAlt"] = "Your ticket QR code",
            ["refLabel"] = "Ref.",
            ["guest"] = "guest",
            ["guests"] = "guests",
            ["viewEvent"] = "View the event",
            ["calendarNote"] = "We've attached a calendar invite so you don't forget.",
            ["signoff"] = "See you soon!",
            ["team"] = "The Yemen Breeze team",
            ["footer"] = "We CONNECT cultures. We SHOWCASE Yemen. We BUILD bridges.",
            ["followUs"] = "Follow us on Instagram:",
            ["whyReceived"] = "You're receiving this email because you registered for a Yemen Breeze event."
        }
    };
}
