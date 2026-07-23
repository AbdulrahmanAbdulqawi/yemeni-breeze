using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace YemeniBreeze.Tests;

public class V6MediaOriginalTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public V6MediaOriginalTests(ApiFactory factory)
    {
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

    [Fact]
    public async Task Uploading_An_Image_Stores_The_Untouched_Original_Byte_For_Byte()
    {
        await AuthenticateAsync();
        var sourceBytes = CreatePng(2000, 1200);

        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(sourceBytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(part, "file", "photo.png");

        var response = await _client.PostAsync("/api/admin/uploads", content);
        response.EnsureSuccessStatusCode();
        var uploaded = await response.Content.ReadFromJsonAsync<UploadResult>();

        Assert.NotNull(uploaded!.OriginalUrl);
        var original = await _client.GetAsync(uploaded.OriginalUrl);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal("image/png", original.Content.Headers.ContentType!.MediaType);
        Assert.Equal(sourceBytes, await original.Content.ReadAsByteArrayAsync());

        // the resized "large" copy is a re-encoded WebP, not the original PNG bytes
        var resized = await _client.GetAsync(uploaded.Url);
        Assert.Equal("image/webp", resized.Content.Headers.ContentType!.MediaType);
        Assert.NotEqual(sourceBytes, await resized.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Deleting_Gallery_Item_Also_Removes_The_Original_From_Storage()
    {
        await AuthenticateAsync();
        var uploaded = await UploadSingleImageAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/admin/gallery",
            GalleryPayload(uploaded.Url, uploaded.ThumbUrl));
        createResponse.EnsureSuccessStatusCode();
        var item = await createResponse.Content.ReadFromJsonAsync<GalleryItemRow>();

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(uploaded.OriginalUrl)).StatusCode);

        var deleteResponse = await _client.DeleteAsync($"/api/admin/gallery/{item!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(uploaded.OriginalUrl)).StatusCode);
    }

    [Fact]
    public async Task Replacing_Event_Cover_Image_Also_Deletes_The_Old_Original()
    {
        await AuthenticateAsync();
        var slug = $"v6-event-{Guid.NewGuid():N}";

        var oldImage = await UploadSingleImageAsync();
        var created = await _client.PostAsJsonAsync("/api/admin/events", EventPayload(slug, oldImage.Url));
        created.EnsureSuccessStatusCode();
        var eventId = (await created.Content.ReadFromJsonAsync<EventRow>())!.Id;
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(oldImage.OriginalUrl)).StatusCode);

        var newImage = await UploadSingleImageAsync();
        var updated = await _client.PutAsJsonAsync($"/api/admin/events/{eventId}", EventPayload(slug, newImage.Url));
        updated.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(oldImage.OriginalUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(newImage.OriginalUrl)).StatusCode);
    }

    [Fact]
    public async Task Deleting_Event_Also_Removes_The_Cover_Original()
    {
        await AuthenticateAsync();
        var slug = $"v6-event-del-{Guid.NewGuid():N}";
        var image = await UploadSingleImageAsync();

        var created = await _client.PostAsJsonAsync("/api/admin/events", EventPayload(slug, image.Url));
        created.EnsureSuccessStatusCode();
        var eventId = (await created.Content.ReadFromJsonAsync<EventRow>())!.Id;

        var deleted = await _client.DeleteAsync($"/api/admin/events/{eventId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(image.OriginalUrl)).StatusCode);
    }

    [Fact]
    public async Task Replacing_Team_Photo_Also_Deletes_The_Old_Original()
    {
        await AuthenticateAsync();
        var oldPhoto = await UploadSingleImageAsync();

        var created = await _client.PostAsJsonAsync("/api/admin/team", TeamPayload(oldPhoto.Url));
        created.EnsureSuccessStatusCode();
        var memberId = (await created.Content.ReadFromJsonAsync<TeamRow>())!.Id;
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(oldPhoto.OriginalUrl)).StatusCode);

        var newPhoto = await UploadSingleImageAsync();
        var updated = await _client.PutAsJsonAsync($"/api/admin/team/{memberId}", TeamPayload(newPhoto.Url));
        updated.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(oldPhoto.OriginalUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(newPhoto.OriginalUrl)).StatusCode);
    }

    [Fact]
    public async Task Deleting_Team_Member_Also_Removes_The_Photo_Original()
    {
        await AuthenticateAsync();
        var photo = await UploadSingleImageAsync();

        var created = await _client.PostAsJsonAsync("/api/admin/team", TeamPayload(photo.Url));
        created.EnsureSuccessStatusCode();
        var memberId = (await created.Content.ReadFromJsonAsync<TeamRow>())!.Id;

        var deleted = await _client.DeleteAsync($"/api/admin/team/{memberId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(photo.OriginalUrl)).StatusCode);
    }

    // --- Thumbnail cleanup for Event/Team (which have no ThumbUrl column) ---

    [Fact]
    public async Task Deleting_Event_Also_Removes_The_Cover_Thumbnail()
    {
        await AuthenticateAsync();
        var slug = $"v7-event-thumb-{Guid.NewGuid():N}";
        var image = await UploadSingleImageAsync();
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(image.ThumbUrl)).StatusCode);

        var created = await _client.PostAsJsonAsync("/api/admin/events", EventPayload(slug, image.Url));
        created.EnsureSuccessStatusCode();
        var eventId = (await created.Content.ReadFromJsonAsync<EventRow>())!.Id;

        var deleted = await _client.DeleteAsync($"/api/admin/events/{eventId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(image.ThumbUrl)).StatusCode);
    }

    [Fact]
    public async Task Deleting_Team_Member_Also_Removes_The_Photo_Thumbnail()
    {
        await AuthenticateAsync();
        var photo = await UploadSingleImageAsync();
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(photo.ThumbUrl)).StatusCode);

        var created = await _client.PostAsJsonAsync("/api/admin/team", TeamPayload(photo.Url));
        created.EnsureSuccessStatusCode();
        var memberId = (await created.Content.ReadFromJsonAsync<TeamRow>())!.Id;

        var deleted = await _client.DeleteAsync($"/api/admin/team/{memberId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(photo.ThumbUrl)).StatusCode);
    }

    private static object TeamPayload(string? photoUrl) => new
    {
        Name = "V6 Test Person",
        RoleEn = "Volunteer", RoleNl = "Vrijwilliger", RoleAr = "متطوع",
        PhotoUrl = photoUrl,
        BioEn = (string?)null, BioNl = (string?)null, BioAr = (string?)null,
        SortOrder = 0
    };

    private static object EventPayload(string slug, string? imageUrl) => new
    {
        Slug = slug,
        TitleEn = "V6 Cover Test", TitleNl = "V6 Cover Test", TitleAr = "V6 Cover Test",
        DescriptionEn = "", DescriptionNl = "", DescriptionAr = "",
        Date = new DateOnly(2026, 12, 1), StartTime = "10:00", EndTime = "12:00",
        Location = "Amsterdam", Capacity = 10, ImageUrl = imageUrl,
        Status = "Published", IsRegistrationOpen = true
    };

    private static object GalleryPayload(string imageUrl, string? thumbUrl) => new
    {
        EventId = (int?)null,
        FolderId = (int?)null,
        ImageUrl = imageUrl,
        ThumbUrl = thumbUrl,
        MediaType = "image",
        CaptionEn = "", CaptionNl = "", CaptionAr = "",
        SortOrder = 0
    };

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

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(60, 90, 140));
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private record LoginResult(string Token, string Email, DateTime ExpiresAt);
    private record UploadResult(string Url, string ThumbUrl, string OriginalUrl);
    private record GalleryItemRow(int Id);
    private record EventRow(int Id);
    private record TeamRow(int Id);
}
