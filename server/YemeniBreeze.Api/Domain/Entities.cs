namespace YemeniBreeze.Api.Domain;

public enum EventStatus
{
    Draft = 0,
    Published = 1,
    Past = 2
}

public class Event
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string TitleNl { get; set; } = "";
    public string TitleAr { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string DescriptionNl { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public DateOnly Date { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string Location { get; set; } = "";
    public int Capacity { get; set; }
    public string? ImageUrl { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public bool IsRegistrationOpen { get; set; }
    /// <summary>New registrations wait as Pending, without any email, until an admin accepts or rejects them.</summary>
    public bool RequiresApproval { get; set; }
    public List<Registration> Registrations { get; set; } = [];
}

public enum RegistrationStatus
{
    Confirmed = 0,
    Waitlisted = 1,
    Cancelled = 2,
    /// <summary>Waiting for an admin to accept or reject (events with RequiresApproval).</summary>
    Pending = 3,
    Rejected = 4
}

public class Registration
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public Event? Event { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public int GuestsCount { get; set; } = 1;
    /// <summary>Free-text "anything we should know?". No longer asked on the form; kept for older registrations.</summary>
    public string? Note { get; set; }
    // Registration questions. Null on rows created before the form asked them.
    public string? Country { get; set; }
    public string? Reason { get; set; }
    /// <summary>Names of the people they bring along. Required when GuestsCount > 1.</summary>
    public string? GuestNames { get; set; }
    public bool? IsVegetarian { get; set; }
    public string? DietaryRestrictions { get; set; }
    public bool WantsUpdates { get; set; }
    public RegistrationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid TicketCode { get; set; } = Guid.NewGuid();
    public DateTime? CheckedInAt { get; set; }
    public string Language { get; set; } = "en";
    /// <summary>Secret for the "manage / cancel my registration" link in emails. Null on rows created before it existed.</summary>
    public Guid? ManageToken { get; set; } = Guid.NewGuid();
}

public enum EmailKind
{
    Confirmed = 0,
    Promoted = 1,
    Waitlisted = 2,
    Cancelled = 3,
    Reminder = 4,
    Rejected = 5
}

public enum EmailStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    /// <summary>Not sent on purpose: no provider configured, or the registration changed before sending.</summary>
    Skipped = 3
}

/// <summary>Outbox row: one email to one registrant, sent (and retried) by the EmailDispatcher.</summary>
public class OutgoingEmail
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public Registration? Registration { get; set; }
    public EmailKind Kind { get; set; }
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
}

public class MediaFolder
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? EventId { get; set; }
    public Event? Event { get; set; }
    public int SortOrder { get; set; }
}

public class GalleryItem
{
    public int Id { get; set; }
    public int? EventId { get; set; }
    public Event? Event { get; set; }
    public int? FolderId { get; set; }
    public MediaFolder? Folder { get; set; }
    public string ImageUrl { get; set; } = "";
    public string? ThumbUrl { get; set; }
    public string MediaType { get; set; } = "image"; // image | video | file
    public string CaptionEn { get; set; } = "";
    public string CaptionNl { get; set; } = "";
    public string CaptionAr { get; set; } = "";
    public int SortOrder { get; set; }
}

public class SiteSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class ContentBlock
{
    public string Key { get; set; } = "";
    public string ValueEn { get; set; } = "";
    public string ValueNl { get; set; } = "";
    public string ValueAr { get; set; } = "";
}

public class TeamMember
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RoleEn { get; set; } = "";
    public string RoleNl { get; set; } = "";
    public string RoleAr { get; set; } = "";
    public string? PhotoUrl { get; set; }
    public string? BioEn { get; set; }
    public string? BioNl { get; set; }
    public string? BioAr { get; set; }
    public string? Slug { get; set; }
    public int SortOrder { get; set; }
}

public class ContactMessage
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
