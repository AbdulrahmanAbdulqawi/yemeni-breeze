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
            EmailKind.Confirmed, "https://yemenbreeze.nl/")!;

        Assert.Contains("cid:yb-logo", email.Html);
        Assert.Contains("cid:ticket-qr", email.Html);
        Assert.Contains("https://yemenbreeze.nl/events/summer-night", email.Html);
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
            EmailKind.Confirmed, "https://yemenbreeze.nl")!;

        Assert.DoesNotContain("<b>Test</b>", email.Html);
        Assert.Contains("Sara &lt;b&gt;Test&lt;/b&gt;", email.Html);
        // Plain text keeps the name as typed
        Assert.Contains("Sara <b>Test</b>", email.Text);
    }

    [Fact]
    public void Arabic_email_is_right_to_left_with_arabic_title()
    {
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed, "ar"), SampleEvent(),
            EmailKind.Confirmed, "https://yemenbreeze.nl")!;

        Assert.Contains("dir=\"rtl\"", email.Html);
        Assert.Contains("ليلة صيفية", email.Subject);
    }

    [Fact]
    public void Waitlist_email_has_no_ticket()
    {
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Waitlisted), SampleEvent(),
            EmailKind.Waitlisted, "https://yemenbreeze.nl")!;

        Assert.DoesNotContain("cid:ticket-qr", email.Html);
        Assert.Single(email.Attachments); // logo only
        Assert.StartsWith("You're on the waitlist", email.Subject);
    }

    [Fact]
    public void Arabic_heading_and_footer_are_right_to_left_too()
    {
        // Gmail drops <html dir>, so the heading bar and footer need their own dir or "!" lands on the wrong side.
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed, "ar"), SampleEvent(),
            EmailKind.Confirmed, "https://yemenbreeze.nl")!;

        Assert.Contains("<body dir=\"rtl\"", email.Html);
        Assert.Contains("dir=\"rtl\" bgcolor=\"#310f02\"", email.Html);   // heading bar
        Assert.Contains("dir=\"rtl\" bgcolor=\"#4b2312\"", email.Html);   // footer
        Assert.DoesNotContain(".ics", email.Html);                         // no scrambled "(.ics)" in RTL text
    }

    [Fact]
    public void Footer_links_to_instagram_instead_of_the_domain()
    {
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed), SampleEvent(),
            EmailKind.Confirmed, "https://yemenbreeze.nl")!;

        Assert.Contains("https://instagram.com/Yemeni.breeze", email.Html);
        Assert.Contains("@Yemeni.breeze", email.Text);
        Assert.DoesNotContain(">yemenbreeze.nl<", email.Html);
    }

    [Fact]
    public void Event_photo_is_shown_with_a_full_address()
    {
        var ev = SampleEvent();
        ev.ImageUrl = "/api/media/events/cover.webp";

        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed), ev,
            EmailKind.Confirmed, "https://yemenbreeze.nl")!;

        Assert.Contains("src=\"https://yemenbreeze.nl/api/media/events/cover.webp\"", email.Html);
    }

    [Fact]
    public void Ticket_email_greets_by_first_name()
    {
        var registration = SampleRegistration(RegistrationStatus.Confirmed);
        registration.FullName = "Sara Ahmed";

        var email = RegistrationEmails.Build(registration, SampleEvent(), EmailKind.Confirmed, "https://yemenbreeze.nl")!;

        Assert.Contains("Dear Sara,", email.Html);
        Assert.Contains("Dear Sara,", email.Text);
    }

    [Fact]
    public void Rejection_email_names_the_event_but_leaves_out_its_details()
    {
        var registration = SampleRegistration(RegistrationStatus.Rejected);
        registration.FullName = "Sara Ahmed";
        var ev = SampleEvent();
        ev.ImageUrl = "/api/media/events/cover.webp";

        var email = RegistrationEmails.Build(registration, ev, EmailKind.Rejected, "https://yemenbreeze.nl")!;

        Assert.Contains("Dear Sara,", email.Html);
        Assert.Contains("Summer Night</h1>", email.Html);          // the event's name is the heading
        Assert.DoesNotContain("Thanks for signing up", email.Html);
        foreach (var detail in new[] { "Rotterdam", "18:00", "cover.webp", "View the event", "/events/summer-night" })
        {
            Assert.DoesNotContain(detail, email.Html);
            Assert.DoesNotContain(detail, email.Text);
        }
    }

    [Fact]
    public void Ticket_email_has_the_donation_message_only_when_the_event_has_a_link()
    {
        var ev = SampleEvent();
        var without = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed), ev,
            EmailKind.Confirmed, "https://yemenbreeze.nl")!;
        Assert.DoesNotContain("voluntary donation", without.Html);
        Assert.DoesNotContain("voluntary donation", without.Text);

        ev.DonationUrl = "https://www.paypal.com/donate/?hosted_button_id=ABC123";
        var with = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed), ev,
            EmailKind.Confirmed, "https://yemenbreeze.nl")!;
        Assert.Contains("If you would like to support Yemeni Breeze, you can make a voluntary donation", with.Html);
        Assert.Contains("href=\"https://www.paypal.com/donate/?hosted_button_id=ABC123\"", with.Html);
        Assert.Contains("https://www.paypal.com/donate/?hosted_button_id=ABC123", with.Text);
    }

    [Theory]
    [InlineData(RegistrationStatus.Confirmed, EmailKind.Confirmed, true)]
    [InlineData(RegistrationStatus.Confirmed, EmailKind.Promoted, true)]
    [InlineData(RegistrationStatus.Confirmed, EmailKind.Reminder, true)]
    [InlineData(RegistrationStatus.Waitlisted, EmailKind.Waitlisted, true)]
    [InlineData(RegistrationStatus.Rejected, EmailKind.Rejected, false)]
    [InlineData(RegistrationStatus.Cancelled, EmailKind.Cancelled, false)]
    public void Emails_with_event_details_ask_not_to_share_them(RegistrationStatus status, EmailKind kind, bool asks)
    {
        var email = RegistrationEmails.Build(SampleRegistration(status), SampleEvent(), kind, "https://yemenbreeze.nl")!;

        const string note = "please keep the event details to yourself"; // no apostrophe: HTML encodes it
        Assert.Equal(asks, email.Html.Contains(note));
        Assert.Equal(asks, email.Text.Contains(note));
        if (asks) Assert.Contains("Register here", email.Html);
    }

    [Theory]
    [InlineData(RegistrationStatus.Rejected, EmailKind.Rejected)]
    [InlineData(RegistrationStatus.Waitlisted, EmailKind.Waitlisted)]
    [InlineData(RegistrationStatus.Cancelled, EmailKind.Cancelled)]
    public void Other_emails_never_ask_for_a_donation(RegistrationStatus status, EmailKind kind)
    {
        var ev = SampleEvent();
        ev.DonationUrl = "https://example.org/donate";

        var email = RegistrationEmails.Build(SampleRegistration(status), ev, kind, "https://yemenbreeze.nl")!;

        Assert.DoesNotContain("example.org/donate", email.Html);
    }

    [Theory]
    [InlineData(RegistrationStatus.Confirmed, EmailKind.Confirmed, true)]
    [InlineData(RegistrationStatus.Confirmed, EmailKind.Promoted, true)]
    [InlineData(RegistrationStatus.Confirmed, EmailKind.Reminder, true)]
    [InlineData(RegistrationStatus.Waitlisted, EmailKind.Waitlisted, false)]
    [InlineData(RegistrationStatus.Rejected, EmailKind.Rejected, false)]
    [InlineData(RegistrationStatus.Cancelled, EmailKind.Cancelled, false)]
    public void Exact_address_is_only_sent_to_people_with_a_spot(RegistrationStatus status, EmailKind kind, bool sent)
    {
        var ev = SampleEvent();
        ev.ExactAddress = "Coolsingel 40\n3011 AD Rotterdam";

        var email = RegistrationEmails.Build(SampleRegistration(status), ev, kind, "https://yemenbreeze.nl")!;

        Assert.Equal(sent, email.Html.Contains("Coolsingel 40"));
        Assert.Equal(sent, email.Text.Contains("Coolsingel 40"));
        if (!sent) return;
        Assert.Contains("Coolsingel 40</span><br/><span dir=\"auto\" style=\"unicode-bidi:isolate\">3011 AD Rotterdam</span>", email.Html);
        Assert.Contains("https://www.google.com/maps/search/?api=1&amp;query=Coolsingel%2040%0A3011%20AD%20Rotterdam", email.Html);
        Assert.Contains("Address: Coolsingel 40, 3011 AD Rotterdam", email.Text);
        var ics = System.Text.Encoding.UTF8.GetString(email.Attachments.Single(a => a.FileName.EndsWith(".ics")).Content);
        Assert.Contains("LOCATION:Coolsingel 40\\n3011 AD Rotterdam", ics);
    }

    [Fact]
    public void Rejection_email_says_we_will_email_if_a_spot_opens_up()
    {
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Rejected), SampleEvent(),
            EmailKind.Rejected, "https://yemenbreeze.nl")!;

        Assert.Contains("we can’t offer you a place at this event just yet.", email.Html);
        // The note's two parts are separate paragraphs
        Assert.Contains("we’ll let you know by email.</p>", email.Html);
        Assert.Contains(">We really hope to see you at the event!</p>", email.Html);
        Assert.Contains("As soon as a spot becomes available, we’ll let you know by email.\n\nWe really hope to see you at the event!",
            email.Text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Ticket_email_without_an_exact_address_has_no_address_row()
    {
        var email = RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Confirmed), SampleEvent(),
            EmailKind.Confirmed, "https://yemenbreeze.nl")!;

        Assert.DoesNotContain("Open in Maps", email.Html);
        Assert.DoesNotContain("Address:", email.Text);
    }

    [Theory]
    [InlineData("https://yemenbreeze.nl,https://www.yemenbreeze.nl", "https://yemenbreeze.nl")]
    [InlineData(" https://yemenbreeze.nl/ , https://www.yemenbreeze.nl", "https://yemenbreeze.nl")]
    [InlineData("https://yemenbreeze.nl", "https://yemenbreeze.nl")]
    public void Email_links_use_one_site_address_when_several_are_configured(string configured, string expected)
    {
        Assert.Equal(expected, new EmailOptions { PublicBaseUrl = configured }.SiteUrl);
    }

    [Fact]
    public void Email_that_no_longer_fits_the_status_is_not_built()
    {
        // A ticket email for a registration that was cancelled before it went out
        Assert.Null(RegistrationEmails.Build(SampleRegistration(RegistrationStatus.Cancelled), SampleEvent(),
            EmailKind.Confirmed, "https://yemenbreeze.nl"));
    }
}
