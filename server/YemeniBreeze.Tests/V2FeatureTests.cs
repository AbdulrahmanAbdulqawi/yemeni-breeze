using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Tests;

public class V2FeatureTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;

    public V2FeatureTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task AuthenticateAsync()
    {
        if (_client.DefaultRequestHeaders.Authorization is not null) return;
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@yemenibreeze.nl", password = "ChangeMe!2026" });
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    private async Task<(int id, string slug)> CreateEventAsync(int capacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event
        {
            Slug = $"v2-event-{Guid.NewGuid():N}",
            TitleEn = "V2 Event",
            Date = new DateOnly(2026, 12, 1),
            Capacity = capacity,
            Status = EventStatus.Published,
            IsRegistrationOpen = true
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (ev.Id, ev.Slug);
    }

    private async Task<RegistrationRow> RegisterAsync(string slug, string email, int guests)
    {
        var response = await _client.PostAsJsonAsync($"/api/events/{slug}/register",
            new { FullName = "Person " + email, Email = email, GuestsCount = guests, Language = "en" });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RegisterResult>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reg = await db.Registrations.SingleAsync(r => r.Id == result!.Id);
        return new RegistrationRow(reg.Id, reg.Status.ToString(), reg.TicketCode);
    }

    // --- Auto-promotion on cancellation ---

    [Fact]
    public async Task Cancelling_Confirmed_Promotes_Earliest_Fitting_Waitlisted()
    {
        await AuthenticateAsync();
        var (eventId, slug) = await CreateEventAsync(capacity: 3);

        var first = await RegisterAsync(slug, "first@v2.nl", 3);        // fills the event
        var bigWait = await RegisterAsync(slug, "big@v2.nl", 5);        // never fits (5 > 3)
        var smallWait = await RegisterAsync(slug, "small@v2.nl", 2);    // fits after cancel

        Assert.Equal("Confirmed", first.Status);
        Assert.Equal("Waitlisted", bigWait.Status);
        Assert.Equal("Waitlisted", smallWait.Status);

        var cancel = await _client.PostAsJsonAsync(
            $"/api/admin/events/{eventId}/registrations/{first.Id}/status",
            new { Status = "Cancelled" });
        cancel.EnsureSuccessStatusCode();

        var rows = await _client.GetFromJsonAsync<List<RegistrationRow2>>(
            $"/api/admin/events/{eventId}/registrations");

        Assert.Equal("Waitlisted", rows!.Single(r => r.Id == bigWait.Id).Status);   // 5 still doesn't fit
        Assert.Equal("Confirmed", rows.Single(r => r.Id == smallWait.Id).Status);   // 2 fits, promoted
    }

    [Fact]
    public async Task Cancelling_Does_Not_Promote_When_Nothing_Fits()
    {
        await AuthenticateAsync();
        var (eventId, slug) = await CreateEventAsync(capacity: 2);

        var confirmed = await RegisterAsync(slug, "c@v2.nl", 1);
        await RegisterAsync(slug, "c2@v2.nl", 1);                  // event now full (2/2)
        var waitlisted = await RegisterAsync(slug, "w@v2.nl", 2);  // needs 2 seats

        await _client.PostAsJsonAsync(
            $"/api/admin/events/{eventId}/registrations/{confirmed.Id}/status",
            new { Status = "Cancelled" });                          // frees only 1 seat

        var rows = await _client.GetFromJsonAsync<List<RegistrationRow2>>(
            $"/api/admin/events/{eventId}/registrations");
        Assert.Equal("Waitlisted", rows!.Single(r => r.Id == waitlisted.Id).Status);
    }

    // --- Check-in ---

    [Fact]
    public async Task Checkin_Marks_Registration_And_Detects_Duplicates()
    {
        await AuthenticateAsync();
        var (eventId, slug) = await CreateEventAsync(capacity: 10);
        var reg = await RegisterAsync(slug, "door@v2.nl", 2);

        var first = await _client.PostAsJsonAsync(
            $"/api/admin/events/{eventId}/checkin", new { TicketCode = reg.TicketCode.ToString() });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<CheckinResult>();
        Assert.False(firstResult!.AlreadyCheckedIn);
        Assert.NotNull(firstResult.Registration.CheckedInAt);

        var second = await _client.PostAsJsonAsync(
            $"/api/admin/events/{eventId}/checkin", new { TicketCode = reg.TicketCode.ToString() });
        var secondResult = await second.Content.ReadFromJsonAsync<CheckinResult>();
        Assert.True(secondResult!.AlreadyCheckedIn);
    }

    [Fact]
    public async Task Checkin_Rejects_Unknown_And_Invalid_Tickets()
    {
        await AuthenticateAsync();
        var (eventId, _) = await CreateEventAsync(capacity: 5);

        var unknown = await _client.PostAsJsonAsync(
            $"/api/admin/events/{eventId}/checkin", new { TicketCode = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var invalid = await _client.PostAsJsonAsync(
            $"/api/admin/events/{eventId}/checkin", new { TicketCode = "not-a-guid" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    // --- Uploads ---

    [Fact]
    public async Task Batch_Upload_Resizes_And_Returns_Thumbs()
    {
        await AuthenticateAsync();

        using var content = new MultipartFormDataContent();
        for (var i = 0; i < 2; i++)
        {
            var bytes = CreatePng(2000, 1200);
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(part, "files", $"photo{i}.png");
        }

        var response = await _client.PostAsync("/api/admin/uploads/batch", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<UploadResult>>();
        Assert.Equal(2, results!.Count);
        Assert.All(results, r =>
        {
            Assert.EndsWith(".webp", r.Url);
            Assert.EndsWith("-thumb.webp", r.ThumbUrl);
        });
    }

    [Fact]
    public async Task Upload_Rejects_Disallowed_Extension()
    {
        await AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent([1, 2, 3]);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(part, "file", "notes.pdf");

        var response = await _client.PostAsync("/api/admin/uploads", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task File_Upload_Stores_Video_And_Serves_It_Back()
    {
        await AuthenticateAsync();

        var payload = new byte[64 * 1024];
        Random.Shared.NextBytes(payload);
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(payload);
        part.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(part, "file", "clip.mp4");

        var response = await _client.PostAsync("/api/admin/uploads/file", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var uploaded = await response.Content.ReadFromJsonAsync<UploadedFileResult>();
        Assert.Equal("video", uploaded!.Kind);
        Assert.StartsWith("/api/media/", uploaded.Url);

        // media proxy is public and returns the exact bytes with the right content-type
        var served = await _client.GetAsync(uploaded.Url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("video/mp4", served.Content.Headers.ContentType!.MediaType);
        Assert.Equal(payload, await served.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task File_Upload_Rejects_Executable()
    {
        await AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent([1, 2, 3, 4]);
        content.Add(part, "file", "malware.exe");

        var response = await _client.PostAsync("/api/admin/uploads/file", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Media_Proxy_Rejects_Path_Traversal_Keys()
    {
        var response = await _client.GetAsync("/api/media/..%2f..%2fappsettings.json");
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);
    }

    // --- Storage cleanup on delete/replace ---

    [Fact]
    public async Task Deleting_Gallery_Item_Removes_Its_Media_From_Storage()
    {
        await AuthenticateAsync();
        var uploaded = await UploadSingleImageAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/admin/gallery", new
        {
            EventId = (int?)null,
            ImageUrl = uploaded.Url,
            ThumbUrl = uploaded.ThumbUrl,
            MediaType = "image",
            CaptionEn = "", CaptionNl = "", CaptionAr = "",
            SortOrder = 0
        });
        createResponse.EnsureSuccessStatusCode();
        var item = await createResponse.Content.ReadFromJsonAsync<GalleryItemRow>();

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(uploaded.Url)).StatusCode);

        var deleteResponse = await _client.DeleteAsync($"/api/admin/gallery/{item!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(uploaded.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(uploaded.ThumbUrl)).StatusCode);
    }

    [Fact]
    public async Task Replacing_Event_Cover_Image_Deletes_The_Old_One_From_Storage()
    {
        await AuthenticateAsync();
        var (eventId, slug) = await CreateEventAsync(capacity: 10);

        var oldImage = await UploadSingleImageAsync();
        var setOld = await _client.PutAsJsonAsync($"/api/admin/events/{eventId}", EventPayload(slug, oldImage.Url));
        setOld.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(oldImage.Url)).StatusCode);

        var newImage = await UploadSingleImageAsync();
        var setNew = await _client.PutAsJsonAsync($"/api/admin/events/{eventId}", EventPayload(slug, newImage.Url));
        setNew.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(oldImage.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(newImage.Url)).StatusCode);
    }

    [Fact]
    public async Task Deleting_Event_Removes_Its_Cover_Image_From_Storage()
    {
        await AuthenticateAsync();
        var (eventId, slug) = await CreateEventAsync(capacity: 10);

        var image = await UploadSingleImageAsync();
        var setImage = await _client.PutAsJsonAsync($"/api/admin/events/{eventId}", EventPayload(slug, image.Url));
        setImage.EnsureSuccessStatusCode();

        var deleteResponse = await _client.DeleteAsync($"/api/admin/events/{eventId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(image.Url)).StatusCode);
    }

    private async Task<UploadResult> UploadSingleImageAsync()
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(CreatePng(800, 600));
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(part, "file", "photo.png");
        var response = await _client.PostAsync("/api/admin/uploads", content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UploadResult>())!;
    }

    private static object EventPayload(string slug, string? imageUrl) => new
    {
        Slug = slug,
        TitleEn = "Cover Test", TitleNl = "Cover Test", TitleAr = "Cover Test",
        DescriptionEn = "", DescriptionNl = "", DescriptionAr = "",
        Date = new DateOnly(2026, 12, 1), StartTime = "10:00", EndTime = "12:00",
        Location = "Amsterdam", Capacity = 10, ImageUrl = imageUrl,
        Status = "Published", IsRegistrationOpen = true
    };

    private record GalleryItemRow(int Id);

    private record UploadedFileResult(string Url, string Kind, string ContentType);

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(143, 27, 4));
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private record LoginResult(string Token, string Email, DateTime ExpiresAt);
    private record RegisterResult(int Id, string Status, string EventSlug);
    private record RegistrationRow(int Id, string Status, Guid TicketCode);
    private record RegistrationRow2(int Id, string Status, DateTime? CheckedInAt);
    private record CheckinResult(bool AlreadyCheckedIn, RegistrationRow2 Registration);
    private record UploadResult(string Url, string ThumbUrl);
}
