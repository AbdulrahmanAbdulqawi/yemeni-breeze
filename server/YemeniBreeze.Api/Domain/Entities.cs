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
    public List<Registration> Registrations { get; set; } = [];
}

public enum RegistrationStatus
{
    Confirmed = 0,
    Waitlisted = 1,
    Cancelled = 2
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
    public string? Note { get; set; }
    public RegistrationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid TicketCode { get; set; } = Guid.NewGuid();
    public DateTime? CheckedInAt { get; set; }
    public string Language { get; set; } = "en";
}

public class GalleryItem
{
    public int Id { get; set; }
    public int? EventId { get; set; }
    public Event? Event { get; set; }
    public string ImageUrl { get; set; } = "";
    public string? ThumbUrl { get; set; }
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
