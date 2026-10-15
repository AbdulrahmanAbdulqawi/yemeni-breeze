using System.Net.Http.Headers;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Email;

public class EmailOptions
{
    /// <summary>"smtp" or "resend". When empty, Resend is used if <see cref="ResendApiKey"/> is set, otherwise SMTP.</summary>
    public string? Provider { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpKey { get; set; }
    public string? ResendApiKey { get; set; }
    public string FromAddress { get; set; } = "noreply@yemenibreeze.nl";
    public string FromName { get; set; } = "Yemen Breeze";
    public string PublicBaseUrl { get; set; } = "http://localhost:4200";

    public string ResolvedProvider =>
        !string.IsNullOrWhiteSpace(Provider) ? Provider.Trim().ToLowerInvariant()
        : !string.IsNullOrEmpty(ResendApiKey) ? "resend"
        : "smtp";
}

/// <summary>A file attached to an email. When <see cref="ContentId"/> is set it is embedded inline (cid:).</summary>
public record EmailAttachment(string FileName, byte[] Content, string ContentType, string? ContentId = null);

public class EmailService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<EmailService> logger)
{
    public const string ResendHttpClient = "resend";

    private EmailOptions Options =>
        config.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();

    public bool IsConfigured => Options.ResolvedProvider switch
    {
        "resend" => !string.IsNullOrEmpty(Options.ResendApiKey),
        "smtp" => !string.IsNullOrEmpty(Options.SmtpHost) && !string.IsNullOrEmpty(Options.SmtpKey),
        _ => false
    };

    /// <summary>Sends the right email for a registration's current status. Never throws.</summary>
    public async Task SendRegistrationEmailAsync(Registration registration, Event ev, bool promoted = false)
    {
        try
        {
            var options = Options;
            var email = RegistrationEmails.Build(registration, ev, promoted, options.PublicBaseUrl);
            if (email is null) return;

            if (!IsConfigured)
            {
                logger.LogInformation(
                    "Email not configured — would send '{Subject}' to {Email}. QR/ics generated: {HasTicket}",
                    email.Subject, registration.Email, registration.Status == RegistrationStatus.Confirmed);
                return;
            }

            if (options.ResolvedProvider == "resend")
                await SendViaResendAsync(options, registration.FullName, registration.Email, email);
            else
                await SendViaSmtpAsync(options, registration.FullName, registration.Email, email);

            logger.LogInformation("Sent '{Subject}' to {Email} via {Provider}",
                email.Subject, registration.Email, options.ResolvedProvider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send registration email to {Email}", registration.Email);
        }
    }

    private static async Task SendViaSmtpAsync(EmailOptions options, string toName, string toEmail, EmailContent email)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = email.Subject;

        var builder = new BodyBuilder();
        foreach (var a in email.Attachments)
        {
            var contentType = ContentType.Parse(a.ContentType);
            if (a.ContentId is null)
            {
                builder.Attachments.Add(a.FileName, a.Content, contentType);
            }
            else
            {
                var linked = builder.LinkedResources.Add(a.FileName, a.Content, contentType);
                linked.ContentId = a.ContentId;
            }
        }
        builder.HtmlBody = email.Html;
        builder.TextBody = email.Text;
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(options.SmtpHost!, options.SmtpPort, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(options.SmtpUser!, options.SmtpKey!);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    // https://resend.com/docs/api-reference/emails/send-email
    private async Task SendViaResendAsync(EmailOptions options, string toName, string toEmail, EmailContent email)
    {
        var payload = new Dictionary<string, object>
        {
            ["from"] = $"{options.FromName} <{options.FromAddress}>",
            ["to"] = new[] { toEmail },
            ["subject"] = email.Subject,
            ["html"] = email.Html,
            ["text"] = email.Text
        };
        if (email.Attachments.Count > 0)
        {
            payload["attachments"] = email.Attachments.Select(a =>
            {
                var item = new Dictionary<string, string>
                {
                    ["filename"] = a.FileName,
                    ["content"] = Convert.ToBase64String(a.Content),
                    ["content_type"] = a.ContentType
                };
                if (a.ContentId is not null) item["content_id"] = a.ContentId;
                return item;
            }).ToArray();
        }

        var client = httpClientFactory.CreateClient(ResendHttpClient);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ResendApiKey);

        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Resend returned {(int)response.StatusCode} for {toEmail} ({toName}): {body}");
        }
    }
}
