using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using QRCoder;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Email;

public class EmailOptions
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpKey { get; set; }
    public string FromAddress { get; set; } = "noreply@yemenibreeze.nl";
    public string FromName { get; set; } = "Yemeni Breeze";
    public string PublicBaseUrl { get; set; } = "http://localhost:4200";
}

public class EmailService(IConfiguration config, ILogger<EmailService> logger)
{
    private EmailOptions Options =>
        config.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();

    public bool IsConfigured =>
        !string.IsNullOrEmpty(Options.SmtpHost) && !string.IsNullOrEmpty(Options.SmtpKey);

    /// <summary>Sends the right email for a registration's current status. Never throws.</summary>
    public async Task SendRegistrationEmailAsync(Registration registration, Event ev, bool promoted = false)
    {
        try
        {
            var (subject, html) = registration.Status switch
            {
                RegistrationStatus.Confirmed => BuildConfirmed(registration, ev, promoted),
                RegistrationStatus.Waitlisted => BuildWaitlisted(registration, ev),
                _ => (null, null)
            };
            if (subject is null || html is null) return;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(Options.FromName, Options.FromAddress));
            message.To.Add(new MailboxAddress(registration.FullName, registration.Email));
            message.Subject = subject;

            var builder = new BodyBuilder();

            if (registration.Status == RegistrationStatus.Confirmed)
            {
                var qr = GenerateTicketQr(registration.TicketCode);
                var qrImage = builder.LinkedResources.Add("ticket.png", qr, new ContentType("image", "png"));
                qrImage.ContentId = "ticket-qr";
                builder.Attachments.Add("yemeni-breeze-event.ics",
                    Encoding.UTF8.GetBytes(BuildIcs(registration, ev)),
                    new ContentType("text", "calendar"));
            }

            builder.HtmlBody = html;
            message.Body = builder.ToMessageBody();

            if (!IsConfigured)
            {
                logger.LogInformation(
                    "Email not configured — would send '{Subject}' to {Email}. QR/ics generated: {HasTicket}",
                    subject, registration.Email, registration.Status == RegistrationStatus.Confirmed);
                return;
            }

            using var client = new SmtpClient();
            await client.ConnectAsync(Options.SmtpHost, Options.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(Options.SmtpUser, Options.SmtpKey);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            logger.LogInformation("Sent '{Subject}' to {Email}", subject, registration.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send registration email to {Email}", registration.Email);
        }
    }

    private static byte[] GenerateTicketQr(Guid ticketCode)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(ticketCode.ToString(), QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(10);
    }

    private static string EventTitle(Registration r, Event ev) => r.Language switch
    {
        "nl" when !string.IsNullOrWhiteSpace(ev.TitleNl) => ev.TitleNl,
        "ar" when !string.IsNullOrWhiteSpace(ev.TitleAr) => ev.TitleAr,
        _ => ev.TitleEn
    };

    private (string, string) BuildConfirmed(Registration r, Event ev, bool promoted)
    {
        var t = Strings(r.Language);
        var title = EventTitle(r, ev);
        var subject = promoted
            ? string.Format(t["promotedSubject"], title)
            : string.Format(t["confirmedSubject"], title);
        var heading = promoted ? t["promotedHeading"] : t["confirmedHeading"];
        var intro = promoted ? t["promotedIntro"] : t["confirmedIntro"];

        var body = $"""
            <p>{intro}</p>
            {EventBlock(r, ev, t)}
            <p>{t["ticketNote"]}</p>
            <p style="text-align:center"><img src="cid:ticket-qr" width="220" height="220" alt="QR ticket"/></p>
            <p style="text-align:center;color:#8f1b04;font-weight:bold">{r.FullName} — {r.GuestsCount} {t["guests"]}</p>
            """;
        return (subject, Wrap(r.Language, heading, body, t));
    }

    private (string, string) BuildWaitlisted(Registration r, Event ev)
    {
        var t = Strings(r.Language);
        var subject = string.Format(t["waitlistSubject"], EventTitle(r, ev));
        var body = $"""
            <p>{t["waitlistIntro"]}</p>
            {EventBlock(r, ev, t)}
            <p>{t["waitlistNote"]}</p>
            """;
        return (subject, Wrap(r.Language, t["waitlistHeading"], body, t));
    }

    private static string EventBlock(Registration r, Event ev, IReadOnlyDictionary<string, string> t) => $"""
        <table style="margin:16px 0;border-inline-start:4px solid #ed9e42;padding-inline-start:12px">
          <tr><td style="padding:2px 8px;color:#8f1b04;font-weight:bold">{EventTitle(r, ev)}</td></tr>
          <tr><td style="padding:2px 8px">📅 {ev.Date:yyyy-MM-dd} &nbsp; 🕓 {ev.StartTime}–{ev.EndTime}</td></tr>
          <tr><td style="padding:2px 8px">📍 {ev.Location}</td></tr>
        </table>
        """;

    private string Wrap(string lang, string heading, string body, IReadOnlyDictionary<string, string> t)
    {
        var dir = lang == "ar" ? "rtl" : "ltr";
        return $"""
            <!doctype html>
            <html dir="{dir}" lang="{lang}">
            <body style="margin:0;background:#fbf3da;font-family:Georgia,'Times New Roman',serif;color:#47180a">
              <div style="max-width:560px;margin:0 auto;padding:24px 16px">
                <div style="background:#5b1f05;border-radius:14px 14px 0 0;padding:24px;text-align:center">
                  <div style="color:#f6e7be;font-size:22px;letter-spacing:2px">YEMENI BREEZE</div>
                  <div style="color:#ed9e42;font-size:14px">نسمات اليمن</div>
                </div>
                <div style="background:#ffffff;padding:28px;border-radius:0 0 14px 14px">
                  <h1 style="color:#8f1b04;font-size:22px;margin-top:0">{heading}</h1>
                  {body}
                  <p style="margin-top:28px">{t["signoff"]}<br/><strong>Yemeni Breeze</strong></p>
                </div>
                <p style="text-align:center;color:#8f1b04;opacity:.7;font-size:12px;margin-top:16px">
                  {t["footer"]} · <a href="{Options.PublicBaseUrl}" style="color:#8f1b04">{Options.PublicBaseUrl.Replace("https://", "").Replace("http://", "")}</a>
                </p>
              </div>
            </body>
            </html>
            """;
    }

    private string BuildIcs(Registration r, Event ev)
    {
        static string ToUtcStamp(DateOnly date, string time)
        {
            var parts = time.Split(':');
            var local = new DateTime(date.Year, date.Month, date.Day,
                parts.Length > 0 && int.TryParse(parts[0], out var h) ? h : 0,
                parts.Length > 1 && int.TryParse(parts[1], out var m) ? m : 0, 0);
            return local.ToString("yyyyMMdd'T'HHmmss");
        }

        return $"""
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Yemeni Breeze//Events//EN
            BEGIN:VEVENT
            UID:{r.TicketCode}@yemenibreeze
            DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}
            DTSTART;TZID=Europe/Amsterdam:{ToUtcStamp(ev.Date, ev.StartTime)}
            DTEND;TZID=Europe/Amsterdam:{ToUtcStamp(ev.Date, ev.EndTime)}
            SUMMARY:{EventTitle(r, ev)}
            LOCATION:{ev.Location}
            URL:{Options.PublicBaseUrl}/events/{ev.Slug}
            END:VEVENT
            END:VCALENDAR
            """.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    private static IReadOnlyDictionary<string, string> Strings(string lang) => lang switch
    {
        "nl" => new Dictionary<string, string>
        {
            ["confirmedSubject"] = "Je bent erbij — {0}",
            ["confirmedHeading"] = "Je inschrijving is bevestigd! 🎉",
            ["confirmedIntro"] = "Wat fijn dat je erbij bent. Hieronder vind je de details en je toegangsticket.",
            ["promotedSubject"] = "Er is een plek vrijgekomen — {0}",
            ["promotedHeading"] = "Goed nieuws: je bent van de wachtlijst!",
            ["promotedIntro"] = "Er is een plek vrijgekomen en die is nu van jou. Hieronder vind je je toegangsticket.",
            ["waitlistSubject"] = "Je staat op de wachtlijst — {0}",
            ["waitlistHeading"] = "Je staat op de wachtlijst",
            ["waitlistIntro"] = "Het evenement zit momenteel vol, maar plannen veranderen vaak.",
            ["waitlistNote"] = "We mailen je direct zodra er een plek vrijkomt.",
            ["ticketNote"] = "Laat deze QR-code zien bij de ingang:",
            ["guests"] = "personen",
            ["signoff"] = "Tot snel,",
            ["footer"] = "Wij VERBINDEN culturen. Wij TONEN Jemen. Wij BOUWEN bruggen."
        },
        "ar" => new Dictionary<string, string>
        {
            ["confirmedSubject"] = "تم تأكيد تسجيلك — {0}",
            ["confirmedHeading"] = "تم تأكيد تسجيلك! 🎉",
            ["confirmedIntro"] = "يسعدنا انضمامك إلينا. تجد أدناه التفاصيل وتذكرة الدخول الخاصة بك.",
            ["promotedSubject"] = "توفر مقعد لك — {0}",
            ["promotedHeading"] = "خبر سار: انتقلت من قائمة الانتظار!",
            ["promotedIntro"] = "توفر مقعد وأصبح لك الآن. تجد أدناه تذكرة الدخول الخاصة بك.",
            ["waitlistSubject"] = "أنت على قائمة الانتظار — {0}",
            ["waitlistHeading"] = "أنت على قائمة الانتظار",
            ["waitlistIntro"] = "الفعالية مكتملة حاليًا، لكن الخطط كثيرًا ما تتغير.",
            ["waitlistNote"] = "سنراسلك فورًا عند توفر مقعد.",
            ["ticketNote"] = "أظهر رمز الاستجابة السريعة هذا عند المدخل:",
            ["guests"] = "أشخاص",
            ["signoff"] = "إلى اللقاء قريبًا،",
            ["footer"] = "نَصِلُ الثقافات. نُظهِرُ اليمن. نَبني الجسور."
        },
        _ => new Dictionary<string, string>
        {
            ["confirmedSubject"] = "You're in — {0}",
            ["confirmedHeading"] = "Your registration is confirmed! 🎉",
            ["confirmedIntro"] = "We're delighted you're joining us. Your details and entry ticket are below.",
            ["promotedSubject"] = "A spot opened up — {0}",
            ["promotedHeading"] = "Good news: you're off the waitlist!",
            ["promotedIntro"] = "A spot opened up and it's now yours. Your entry ticket is below.",
            ["waitlistSubject"] = "You're on the waitlist — {0}",
            ["waitlistHeading"] = "You're on the waitlist",
            ["waitlistIntro"] = "The event is currently full, but plans often change.",
            ["waitlistNote"] = "We'll email you the moment a spot opens up.",
            ["ticketNote"] = "Show this QR code at the entrance:",
            ["guests"] = "guests",
            ["signoff"] = "See you soon,",
            ["footer"] = "We CONNECT cultures. We SHOWCASE Yemen. We BUILD bridges."
        }
    };
}
