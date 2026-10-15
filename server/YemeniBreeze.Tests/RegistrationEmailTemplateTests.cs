using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Email;

namespace YemeniBreeze.Tests;

public class RegistrationEmailTemplateTests
{
    private static Event SampleEvent() => new()
    {
        Slug = "summer-night", TitleEn = "Summer Night", TitleAr = "ليلة صيفية",
        Date = new DateOnly(2026, 12, 1), StartTime = "18:00", EndTime = "21:00",
        Location = "Rotterdam, Zaal 1"
    };

    private static Registration SampleRegistration(RegistrationStatus status, string lang = "en") => new()
    {
        FullName = "Sara <b>Test</b>", Email = "sara@example.com", GuestsCount = 2,
        Status = status, Language = lang
    };

    [Fact]
    public void Confirmed_email_has_logo_qr_ticket_and_calendar()
    {
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed), SampleEvent(),
            EmailKind.Confirmed, "https://yemenibreeze.nl/")!;

        Assert.Contains("cid:yb-logo", email.Html);
        Assert.Contains("cid:ticket-qr", email.Html);
        Assert.Contains("https://yemenibreeze.nl/events/summer-night", email.Html);
        Assert.Equal(["yemen-breeze.png", "ticket.png", "yemen-breeze-event.ics"],
            email.Attachments.Select(a => a.FileName));
        Assert.True(email.Attachments[0].Content.Length > 1000, "logo resource should be embedded");

        var ics = System.Text.Encoding.UTF8.GetString(email.Attachments[2].Content);
        Assert.Contains("LOCATION:Rotterdam\\, Zaal 1", ics);
    }

    [Fact]
    public void User_input_is_html_encoded()
    {
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed), SampleEvent(),
            EmailKind.Confirmed, "https://yemenibreeze.nl")!;

        Assert.DoesNotContain("<b>Test</b>", email.Html);
        Assert.Contains("Sara &lt;b&gt;Test&lt;/b&gt;", email.Html);
        // Plain text keeps the name as typed
        Assert.Contains("Sara <b>Test</b>", email.Text);
    }

    [Fact]
    public void Arabic_email_is_right_to_left_with_arabic_title()
    {
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed, "ar"), SampleEvent(),
            EmailKind.Confirmed, "https://yemenibreeze.nl")!;

        Assert.Contains("dir=\"rtl\"", email.Html);
        Assert.Contains("ليلة صيفية", email.Subject);
    }

    [Fact]
    public void Waitlist_email_has_no_ticket()
    {
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Waitlisted), SampleEvent(),
            EmailKind.Waitlisted, "https://yemenibreeze.nl")!;

        Assert.DoesNotContain("cid:ticket-qr", email.Html);
        Assert.Single(email.Attachments); // logo only
        Assert.StartsWith("You're on the waitlist", email.Subject);
    }

    [Fact]
    public void Email_that_no_longer_fits_the_status_is_not_built()
    {
        // A ticket email for a registration that was cancelled before it went out
        Assert.Null(RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Cancelled), SampleEvent(),
            EmailKind.Confirmed, "https://yemenibreeze.nl"));
    }
}
