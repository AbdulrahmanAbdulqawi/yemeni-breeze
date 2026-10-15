using System.Globalization;
using System.Net;
using System.Text;
using QRCoder;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Email;

/// <summary>A rendered email, ready for any provider.</summary>
public record EmailContent(string Subject, string Html, string Text, IReadOnlyList<EmailAttachment> Attachments);

/// <summary>
/// Builds the registration emails (confirmed, promoted, waitlisted, reminder, cancelled) in the site's identity:
/// espresso browns, cream paper, gold accents, the qamariya logo and its stained-glass colours.
/// Markup is table-based with inline styles so it survives Gmail, Outlook and Apple Mail.
/// </summary>
public static class RegistrationEmails
{
    public const string LogoContentId = "yb-logo";
    public const string QrContentId = "ticket-qr";

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
            _ => RegistrationStatus.Confirmed
        };
        if (r.Status != requiredStatus) return null;

        var t = Strings(r.Language);
        var baseUrl = publicBaseUrl.TrimEnd('/');
        var eventUrl = $"{baseUrl}/events/{Uri.EscapeDataString(ev.Slug)}";
        var manageUrl = kind != EmailKind.Cancelled && r.ManageToken is { } token
            ? $"{baseUrl}/registrations/{token:D}"
            : null;
        var withTicket = r.Status == RegistrationStatus.Confirmed;

        var key = kind switch
        {
            EmailKind.Promoted => "promoted",
            EmailKind.Waitlisted => "waitlist",
            EmailKind.Cancelled => "cancelled",
            EmailKind.Reminder => "reminder",
            _ => "confirmed"
        };
        var subject = string.Format(t[key + "Subject"], EventTitle(r, ev));
        var heading = t[key + "Heading"];
        var intro = t[key + "Intro"];
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
        body.Append(Paragraph(H(intro)));
        body.Append(EventCard(r, ev, t));
        if (withTicket) body.Append(TicketCard(r, t));
        if (note is not null) body.Append(Paragraph(H(note)));
        body.Append(Button(t["viewEvent"], eventUrl));
        if (withTicket) body.Append(Small(t["calendarNote"]));
        if (manageUrl is not null) body.Append(ManageLink(t, manageUrl));

        var html = Layout(r.Language, subject, preheader, heading, body.ToString(), t, baseUrl);
        var text = PlainText(r, ev, t, heading, intro, note, withTicket, eventUrl, manageUrl, baseUrl);
        return new EmailContent(subject, html, text, attachments);
    }

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "");

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
        var displayUrl = baseUrl.Replace("https://", "").Replace("http://", "");
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
            <body style="margin:0;padding:0;background:{{Surface}}">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent">{{H(preheader)}}</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="{{Surface}}" style="background:{{Surface}}">
                <tr><td align="center" style="padding:28px 12px">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
                         style="max-width:600px;background:{{Cream}};border-radius:20px;overflow:hidden;border:1px solid {{Outline}}">
                    <tr><td>{{GlassStrip()}}</td></tr>
                    <tr><td align="center" style="padding:30px 24px 22px">
                      <a href="{{H(baseUrl)}}" style="text-decoration:none">
                        <img src="cid:{{LogoContentId}}" width="200" alt="Yemen Breeze · نسمات اليمن" style="display:block;width:200px;max-width:70%;height:auto;border:0;margin:0 auto"/>
                      </a>
                    </td></tr>
                    <tr><td class="yb-pad" bgcolor="{{Brown}}" style="background:{{Brown}};padding:26px 40px;text-align:center">
                      <h1 class="yb-h1" style="margin:0;font-family:{{HeadingFont(lang)}};font-size:28px;line-height:1.3;font-weight:700;color:{{Gold}}">{{H(heading)}}</h1>
                    </td></tr>
                    <tr><td class="yb-pad" dir="{{dir}}" style="padding:32px 40px 36px;text-align:{{align}};font-family:{{BodyFont(lang)}};color:{{Ink}}">
                      {{body}}
                      <p style="margin:30px 0 0;font-size:16px;line-height:1.6;color:{{Ink}}">{{H(t["signoff"])}}<br/>
                        <strong style="font-family:{{HeadingFont(lang)}};color:{{Maroon}};font-size:18px">{{H(t["team"])}}</strong></p>
                    </td></tr>
                    <tr><td class="yb-pad" bgcolor="{{BrownSoft}}" style="background:{{BrownSoft}};padding:24px 40px;text-align:center;font-family:{{BodyFont(lang)}}">
                      <div style="font-family:{{HeadingFont(lang)}};font-size:15px;line-height:1.6;color:{{Gold}}">{{H(t["footer"])}}</div>
                      <div style="padding-top:10px;font-size:13px">
                        <a href="{{H(baseUrl)}}" style="color:{{Cream}};text-decoration:underline">{{H(displayUrl)}}</a>
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
        string heading, string intro, string? note, bool confirmed, string eventUrl, string? manageUrl, string baseUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("YEMEN BREEZE · نسمات اليمن").AppendLine();
        sb.AppendLine(heading).AppendLine();
        sb.AppendLine(intro).AppendLine();
        sb.AppendLine(EventTitle(r, ev));
        sb.AppendLine($"{t["dateLabel"]}: {FormatDate(ev.Date, r.Language)}");
        if (!string.IsNullOrWhiteSpace(ev.StartTime)) sb.AppendLine($"{t["timeLabel"]}: \u2066{TimeRange(ev)}\u2069");
        if (!string.IsNullOrWhiteSpace(ev.Location)) sb.AppendLine($"{t["locationLabel"]}: {ev.Location}");
        sb.AppendLine();
        if (confirmed)
        {
            sb.AppendLine($"{t["ticketLabel"]}: {r.FullName} — {r.GuestsCount} {(r.GuestsCount == 1 ? t["guest"] : t["guests"])}");
            sb.AppendLine($"{t["refLabel"]} {TicketRef(r.TicketCode)}");
            sb.AppendLine(t["ticketNote"]).AppendLine();
        }
        if (note is not null) sb.AppendLine(note).AppendLine();
        sb.AppendLine($"{t["viewEvent"]}: {eventUrl}").AppendLine();
        if (manageUrl is not null) sb.AppendLine($"{t["cantMakeIt"]} {t["manageLink"]}: {manageUrl}").AppendLine();
        sb.AppendLine(t["signoff"]).AppendLine(t["team"]).AppendLine();
        sb.AppendLine(t["footer"]).AppendLine(baseUrl);
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
            ["confirmedSubject"] = "Je bent erbij — {0}",
            ["confirmedHeading"] = "Je inschrijving is bevestigd!",
            ["confirmedIntro"] = "Wat fijn dat je erbij bent. Hieronder vind je de details en je persoonlijke toegangsticket.",
            ["confirmedPreheader"] = "Je toegangsticket met QR-code zit in deze e-mail.",
            ["promotedSubject"] = "Er is een plek vrijgekomen — {0}",
            ["promotedHeading"] = "Goed nieuws: je bent van de wachtlijst!",
            ["promotedIntro"] = "Er is een plek vrijgekomen en die is nu van jou. Hieronder vind je je toegangsticket.",
            ["promotedPreheader"] = "Er is een plek voor je vrijgekomen — je ticket zit erbij.",
            ["waitlistSubject"] = "Je staat op de wachtlijst — {0}",
            ["waitlistHeading"] = "Je staat op de wachtlijst",
            ["waitlistIntro"] = "Het evenement zit momenteel vol, maar plannen veranderen vaak.",
            ["waitlistNote"] = "We mailen je direct zodra er een plek vrijkomt — met je ticket erbij.",
            ["waitlistPreheader"] = "We laten het je meteen weten als er een plek vrijkomt.",
            ["reminderSubject"] = "Morgen: {0}",
            ["reminderHeading"] = "Tot morgen!",
            ["reminderIntro"] = "Een kleine herinnering: we kijken ernaar uit je morgen te verwelkomen. Je ticket staat hieronder — houd het klaar bij de ingang.",
            ["reminderPreheader"] = "Morgen is het zover — je ticket zit erbij.",
            ["cancelledSubject"] = "Je inschrijving is geannuleerd — {0}",
            ["cancelledHeading"] = "Je inschrijving is geannuleerd",
            ["cancelledIntro"] = "We hebben je inschrijving voor dit evenement geannuleerd. Dank je wel dat je het liet weten — je plek gaat naar iemand op de wachtlijst.",
            ["cancelledNote"] = "Toch van gedachten veranderd? Je kunt je opnieuw inschrijven op de evenementpagina zolang er plekken zijn.",
            ["cancelledPreheader"] = "Je annulering is verwerkt.",
            ["cantMakeIt"] = "Kun je toch niet komen?",
            ["manageLink"] = "Annuleer je inschrijving",
            ["dateLabel"] = "Datum",
            ["timeLabel"] = "Tijd",
            ["locationLabel"] = "Locatie",
            ["ticketLabel"] = "Toegangsticket",
            ["ticketNote"] = "Laat deze QR-code zien bij de ingang",
            ["qrAlt"] = "QR-code van je ticket",
            ["refLabel"] = "Ref.",
            ["guest"] = "persoon",
            ["guests"] = "personen",
            ["viewEvent"] = "Bekijk het evenement",
            ["calendarNote"] = "De agenda-uitnodiging (.ics) zit als bijlage bij deze e-mail.",
            ["signoff"] = "Tot snel,",
            ["team"] = "Team Yemen Breeze",
            ["footer"] = "Wij VERBINDEN culturen. Wij TONEN Jemen. Wij BOUWEN bruggen.",
            ["whyReceived"] = "Je ontvangt deze e-mail omdat je je hebt ingeschreven voor een evenement van Yemen Breeze."
        },
        "ar" => new Dictionary<string, string>
        {
            ["confirmedSubject"] = "تم تأكيد تسجيلك — {0}",
            ["confirmedHeading"] = "تم تأكيد تسجيلك!",
            ["confirmedIntro"] = "يسعدنا انضمامك إلينا. تجد أدناه تفاصيل الفعالية وتذكرة الدخول الخاصة بك.",
            ["confirmedPreheader"] = "تذكرة الدخول مع رمز QR موجودة في هذه الرسالة.",
            ["promotedSubject"] = "توفّر مقعد لك — {0}",
            ["promotedHeading"] = "خبر سار: انتقلت من قائمة الانتظار!",
            ["promotedIntro"] = "توفّر مقعد وأصبح لك الآن. تجد أدناه تذكرة الدخول الخاصة بك.",
            ["promotedPreheader"] = "توفّر مقعد لك — تذكرتك مرفقة.",
            ["waitlistSubject"] = "أنت على قائمة الانتظار — {0}",
            ["waitlistHeading"] = "أنت على قائمة الانتظار",
            ["waitlistIntro"] = "الفعالية مكتملة حاليًا، لكن الخطط كثيرًا ما تتغير.",
            ["waitlistNote"] = "سنراسلك فورًا عند توفّر مقعد — ومعها تذكرتك.",
            ["waitlistPreheader"] = "سنخبرك فورًا عند توفّر مقعد.",
            ["reminderSubject"] = "غدًا: {0}",
            ["reminderHeading"] = "نراك غدًا!",
            ["reminderIntro"] = "تذكير صغير: نتطلع لاستقبالك غدًا. تجد تذكرتك أدناه — اجعلها جاهزة عند المدخل.",
            ["reminderPreheader"] = "الفعالية غدًا — تذكرتك مرفقة.",
            ["cancelledSubject"] = "تم إلغاء تسجيلك — {0}",
            ["cancelledHeading"] = "تم إلغاء تسجيلك",
            ["cancelledIntro"] = "ألغينا تسجيلك في هذه الفعالية. شكرًا لإعلامنا — سيذهب مقعدك إلى أحد المنتظرين.",
            ["cancelledNote"] = "غيّرت رأيك؟ يمكنك التسجيل مجددًا من صفحة الفعالية ما دامت المقاعد متاحة.",
            ["cancelledPreheader"] = "تمت معالجة إلغاء تسجيلك.",
            ["cantMakeIt"] = "لن تتمكن من الحضور؟",
            ["manageLink"] = "ألغِ تسجيلك",
            ["dateLabel"] = "التاريخ",
            ["timeLabel"] = "الوقت",
            ["locationLabel"] = "المكان",
            ["ticketLabel"] = "تذكرة الدخول",
            ["ticketNote"] = "أظهر رمز QR هذا عند المدخل",
            ["qrAlt"] = "رمز QR لتذكرتك",
            ["refLabel"] = "المرجع",
            ["guest"] = "شخص",
            ["guests"] = "أشخاص",
            ["viewEvent"] = "عرض الفعالية",
            ["calendarNote"] = "دعوة التقويم (.ics) مرفقة بهذه الرسالة.",
            ["signoff"] = "إلى اللقاء قريبًا،",
            ["team"] = "فريق نسمات اليمن",
            ["footer"] = "نَصِلُ الثقافات. نُظهِرُ اليمن. نَبني الجسور.",
            ["whyReceived"] = "تصلك هذه الرسالة لأنك سجّلت في إحدى فعاليات نسمات اليمن."
        },
        _ => new Dictionary<string, string>
        {
            ["confirmedSubject"] = "You're in — {0}",
            ["confirmedHeading"] = "Your registration is confirmed!",
            ["confirmedIntro"] = "We're delighted you're joining us. Your event details and personal entry ticket are below.",
            ["confirmedPreheader"] = "Your entry ticket with QR code is inside.",
            ["promotedSubject"] = "A spot opened up — {0}",
            ["promotedHeading"] = "Good news: you're off the waitlist!",
            ["promotedIntro"] = "A spot opened up and it's now yours. Your entry ticket is below.",
            ["promotedPreheader"] = "A spot opened up for you — your ticket is inside.",
            ["waitlistSubject"] = "You're on the waitlist — {0}",
            ["waitlistHeading"] = "You're on the waitlist",
            ["waitlistIntro"] = "The event is currently full, but plans often change.",
            ["waitlistNote"] = "We'll email you the moment a spot opens up — with your ticket included.",
            ["waitlistPreheader"] = "We'll let you know the moment a spot opens up.",
            ["reminderSubject"] = "Tomorrow: {0}",
            ["reminderHeading"] = "See you tomorrow!",
            ["reminderIntro"] = "A quick reminder: we're looking forward to welcoming you tomorrow. Your ticket is below — have it ready at the entrance.",
            ["reminderPreheader"] = "It's tomorrow — your ticket is inside.",
            ["cancelledSubject"] = "Your registration is cancelled — {0}",
            ["cancelledHeading"] = "Your registration is cancelled",
            ["cancelledIntro"] = "We've cancelled your registration for this event. Thank you for letting us know — your spot goes to someone on the waitlist.",
            ["cancelledNote"] = "Changed your mind? You can register again on the event page while spots are available.",
            ["cancelledPreheader"] = "Your cancellation has been processed.",
            ["cantMakeIt"] = "Can't make it after all?",
            ["manageLink"] = "Cancel your registration",
            ["dateLabel"] = "Date",
            ["timeLabel"] = "Time",
            ["locationLabel"] = "Location",
            ["ticketLabel"] = "Entry ticket",
            ["ticketNote"] = "Show this QR code at the entrance",
            ["qrAlt"] = "Your ticket QR code",
            ["refLabel"] = "Ref.",
            ["guest"] = "guest",
            ["guests"] = "guests",
            ["viewEvent"] = "View the event",
            ["calendarNote"] = "A calendar invite (.ics) is attached to this email.",
            ["signoff"] = "See you soon,",
            ["team"] = "The Yemen Breeze team",
            ["footer"] = "We CONNECT cultures. We SHOWCASE Yemen. We BUILD bridges.",
            ["whyReceived"] = "You're receiving this email because you registered for a Yemen Breeze event."
        }
    };
}
