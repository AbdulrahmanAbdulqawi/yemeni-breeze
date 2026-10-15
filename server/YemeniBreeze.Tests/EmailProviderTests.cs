using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Email;

namespace YemeniBreeze.Tests;

public class EmailProviderTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"test\"}") };
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static EmailService CreateService(Dictionary<string, string?> settings, HttpMessageHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new EmailService(config, new FakeHttpClientFactory(handler), NullLogger<EmailService>.Instance);
    }

    private static (Registration, Event) ConfirmedRegistration()
    {
        var ev = new Event
        {
            Slug = "test", TitleEn = "Test Event", Date = new DateOnly(2026, 12, 1),
            StartTime = "18:00", EndTime = "21:00", Location = "Rotterdam"
        };
        var registration = new Registration
        {
            FullName = "Sara Test", Email = "sara@example.com",
            Status = RegistrationStatus.Confirmed, Event = ev
        };
        return (registration, ev);
    }

    [Fact]
    public async Task Resend_posts_email_with_inline_qr_and_calendar_attachment()
    {
        var handler = new CapturingHandler();
        var service = CreateService(new()
        {
            ["Email:Provider"] = "resend",
            ["Email:ResendApiKey"] = "re_test_key",
            ["Email:FromAddress"] = "events@example.com",
            ["Email:FromName"] = "Yemen Breeze"
        }, handler);
        var (registration, ev) = ConfirmedRegistration();

        Assert.True(service.IsConfigured);
        await service.SendRegistrationEmailAsync(registration, ev);

        Assert.NotNull(handler.Request);
        Assert.Equal("https://api.resend.com/emails", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("re_test_key", handler.Request.Headers.Authorization?.Parameter);

        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        Assert.Equal("Yemen Breeze <events@example.com>", root.GetProperty("from").GetString());
        Assert.Equal("sara@example.com", root.GetProperty("to")[0].GetString());
        Assert.Contains("Test Event", root.GetProperty("subject").GetString());
        Assert.Contains("cid:ticket-qr", root.GetProperty("html").GetString());
        Assert.Contains("Sara Test", root.GetProperty("text").GetString());

        var attachments = root.GetProperty("attachments").EnumerateArray().ToList();
        Assert.Equal(3, attachments.Count);
        var logo = attachments.Single(a => a.GetProperty("filename").GetString() == "yemen-breeze.png");
        Assert.Equal("yb-logo", logo.GetProperty("content_id").GetString());
        var qr = attachments.Single(a => a.GetProperty("filename").GetString() == "ticket.png");
        Assert.Equal("ticket-qr", qr.GetProperty("content_id").GetString());
        Assert.Equal("image/png", qr.GetProperty("content_type").GetString());
        Assert.NotEmpty(Convert.FromBase64String(qr.GetProperty("content").GetString()!));
        var ics = attachments.Single(a => a.GetProperty("filename").GetString() == "yemen-breeze-event.ics");
        Assert.False(ics.TryGetProperty("content_id", out _));
    }

    [Fact]
    public void Resend_is_picked_automatically_when_only_an_api_key_is_set()
    {
        var service = CreateService(new() { ["Email:ResendApiKey"] = "re_test_key" }, new CapturingHandler());
        Assert.True(service.IsConfigured);
    }

    [Fact]
    public async Task Nothing_is_sent_when_the_chosen_provider_has_no_credentials()
    {
        var handler = new CapturingHandler();
        // SMTP credentials alone don't configure Resend when Resend is chosen explicitly.
        var service = CreateService(new()
        {
            ["Email:Provider"] = "resend",
            ["Email:SmtpHost"] = "smtp.example.com",
            ["Email:SmtpKey"] = "secret"
        }, handler);
        var (registration, ev) = ConfirmedRegistration();

        Assert.False(service.IsConfigured);
        await service.SendRegistrationEmailAsync(registration, ev);
        Assert.Null(handler.Request);
    }
}
