using System.Net.Http.Headers;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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

    /// <summary>Runs the background outbox sender. Tests switch it off and drive it by hand.</summary>
    public bool DispatcherEnabled { get; set; } = true;
    /// <summary>Queue a reminder for confirmed registrants the day before the event.</summary>
    public bool RemindersEnabled { get; set; } = true;
    /// <summary>Pause between two sends, to stay under the provider's rate limit.</summary>
    public int SendIntervalMs { get; set; } = 600;
    /// <summary>Give up on an email after this many failed tries.</summary>
    public int MaxAttempts { get; set; } = 6;

    public string ResolvedProvider =>
        !string.IsNullOrWhiteSpace(Provider) ? Provider.Trim().ToLowerInvariant()
        : !string.IsNullOrEmpty(ResendApiKey) ? "resend"
        : "smtp";
}

/// <summary>A file attached to an email. When <see cref="ContentId"/> is set it is embedded inline (cid:).</summary>
public record EmailAttachment(string FileName, byte[] Content, string ContentType, string? ContentId = null);

/// <summary>A failed send. <see cref="Transient"/> failures (rate limits, outages) are worth retrying.</summary>
public class EmailSendException(string message, bool transient, Exception? inner = null)
    : Exception(message, inner)
{
    public bool Transient { get; } = transient;
}

/// <summary>Delivers one rendered email through the configured provider. Queueing and retries live in EmailDispatcher.</summary>
public class EmailService(IConfiguration config, IHttpClientFactory httpClientFactory)
{
    public const string ResendHttpClient = "resend";

    public EmailOptions Options =>
        config.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();

    public bool IsConfigured => Options.ResolvedProvider switch
    {
        "resend" => !string.IsNullOrEmpty(Options.ResendApiKey),
        "smtp" => !string.IsNullOrEmpty(Options.SmtpHost) && !string.IsNullOrEmpty(Options.SmtpKey),
        _ => false
    };

    /// <summary>Sends the email or throws <see cref="EmailSendException"/>.</summary>
    public async Task SendAsync(string toName, string toEmail, EmailContent email, CancellationToken ct = default)
    {
        var options = Options;
        if (options.ResolvedProvider == "resend")
        {
            await SendViaResendAsync(options, toEmail, email, ct);
            return;
        }

        try
        {
            await SendViaSmtpAsync(options, toName, toEmail, email, ct);
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode is SmtpErrorCode.RecipientNotAccepted
                                                  or SmtpErrorCode.SenderNotAccepted
                                              && (int)ex.StatusCode >= 500)
        {
            throw new EmailSendException($"SMTP rejected the message: {ex.Message}", transient: false, ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new EmailSendException($"SMTP send failed: {ex.Message}", transient: true, ex);
        }
    }

    private static async Task SendViaSmtpAsync(EmailOptions options, string toName, string toEmail,
        EmailContent email, CancellationToken ct)
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
        await client.ConnectAsync(options.SmtpHost!, options.SmtpPort, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(options.SmtpUser!, options.SmtpKey!, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    // https://resend.com/docs/api-reference/emails/send-email
    private async Task SendViaResendAsync(EmailOptions options, string toEmail, EmailContent email, CancellationToken ct)
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

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new EmailSendException($"Could not reach Resend: {ex.Message}", transient: true, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new EmailSendException("Resend timed out.", transient: true, ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode) return;
            var body = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode;
            // 429 = rate limited, 5xx = Resend trouble: try again later. Other 4xx (bad address,
            // unverified domain, bad key) won't fix themselves.
            var transient = status == 429 || status >= 500;
            throw new EmailSendException($"Resend returned {status}: {body}", transient);
        }
    }
}
