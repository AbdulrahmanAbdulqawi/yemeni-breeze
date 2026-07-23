using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YemeniBreeze.Api.Data;

namespace YemeniBreeze.Tests;

public class V4ContentAndTeamTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;

    public V4ContentAndTeamTests(ApiFactory factory)
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

    // --- Content blocks ---

    [Fact]
    public async Task Public_Content_Endpoint_Returns_Seeded_Defaults()
    {
        var content = await _client.GetFromJsonAsync<Dictionary<string, string>>("/api/content?lang=en");
        Assert.Equal("Yemen is more than a war", content!["home.heroTitle"]);
    }

    [Fact]
    public async Task Public_Content_Falls_Back_To_English_When_Language_Blank()
    {
        await AuthenticateAsync();
        // seed a brand-new key with only an English value
        var put = await _client.PutAsJsonAsync("/api/admin/content",
            new Dictionary<string, object> { ["test.fallbackKey"] = new { En = "English only", Nl = "", Ar = "" } });
        put.EnsureSuccessStatusCode();

        var nl = await _client.GetFromJsonAsync<Dictionary<string, string>>("/api/content?lang=nl");
        Assert.Equal("English only", nl!["test.fallbackKey"]);
    }

    [Fact]
    public async Task Admin_Put_Content_Updates_A_Value()
    {
        await AuthenticateAsync();

        var updated = await _client.PutAsJsonAsync("/api/admin/content",
            new Dictionary<string, object>
            {
                ["home.heroCta"] = new { En = "Come say hi", Nl = "Zeg hallo", Ar = "تعال وقل مرحباً" }
            });
        updated.EnsureSuccessStatusCode();

        var content = await _client.GetFromJsonAsync<Dictionary<string, string>>("/api/content?lang=en");
        Assert.Equal("Come say hi", content!["home.heroCta"]);
    }

    [Fact]
    public async Task Reseeding_Does_Not_Overwrite_An_Admin_Edit()
    {
        await AuthenticateAsync();

        var edit = await _client.PutAsJsonAsync("/api/admin/content",
            new Dictionary<string, object>
            {
                ["footer.location"] = new { En = "Rotterdam, The Netherlands", Nl = "Rotterdam", Ar = "روتردام" }
            });
        edit.EnsureSuccessStatusCode();

        // simulate the app restarting and re-running the idempotent startup seed
        using (var scope = _factory.Services.CreateScope())
            await SeedData.RunAsync(scope.ServiceProvider);

        var content = await _client.GetFromJsonAsync<Dictionary<string, string>>("/api/content?lang=en");
        Assert.Equal("Rotterdam, The Netherlands", content!["footer.location"]);
    }

    // --- Team members ---

    [Fact]
    public async Task Team_Member_Without_Bio_Has_No_Slug_And_Is_Not_Publicly_Reachable()
    {
        await AuthenticateAsync();

        var created = await _client.PostAsJsonAsync("/api/admin/team", TeamPayload("No Bio Person"));
        created.EnsureSuccessStatusCode();
        var member = await created.Content.ReadFromJsonAsync<TeamRow>();
        Assert.Null(member!.Slug);

        var list = await _client.GetFromJsonAsync<List<TeamRow>>("/api/team");
        Assert.Contains(list!, m => m.Id == member.Id);
    }

    [Fact]
    public async Task Adding_A_Bio_Generates_A_Slug_And_Removing_It_Clears_The_Slug()
    {
        await AuthenticateAsync();

        var created = await _client.PostAsJsonAsync("/api/admin/team", TeamPayload("Bio Person"));
        var memberId = (await created.Content.ReadFromJsonAsync<TeamRow>())!.Id;

        var withBio = await _client.PutAsJsonAsync($"/api/admin/team/{memberId}",
            TeamPayload("Bio Person", bioEn: "A short biography."));
        withBio.EnsureSuccessStatusCode();
        var row = await withBio.Content.ReadFromJsonAsync<TeamRow>();
        Assert.Equal("bio-person", row!.Slug);

        var publicLookup = await _client.GetAsync($"/api/team/{row.Slug}");
        Assert.Equal(HttpStatusCode.OK, publicLookup.StatusCode);

        var withoutBio = await _client.PutAsJsonAsync($"/api/admin/team/{memberId}", TeamPayload("Bio Person"));
        withoutBio.EnsureSuccessStatusCode();
        var cleared = await withoutBio.Content.ReadFromJsonAsync<TeamRow>();
        Assert.Null(cleared!.Slug);

        var goneNow = await _client.GetAsync($"/api/team/{row.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, goneNow.StatusCode);
    }

    [Fact]
    public async Task Duplicate_Names_Get_Distinct_Slugs()
    {
        await AuthenticateAsync();

        var first = await _client.PostAsJsonAsync("/api/admin/team", TeamPayload("Sam Duplicate", bioEn: "Bio one."));
        var second = await _client.PostAsJsonAsync("/api/admin/team", TeamPayload("Sam Duplicate", bioEn: "Bio two."));

        var firstRow = await first.Content.ReadFromJsonAsync<TeamRow>();
        var secondRow = await second.Content.ReadFromJsonAsync<TeamRow>();

        Assert.Equal("sam-duplicate", firstRow!.Slug);
        Assert.Equal("sam-duplicate-2", secondRow!.Slug);
    }

    [Fact]
    public async Task Unknown_Slug_404s()
    {
        var response = await _client.GetAsync("/api/team/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_A_Team_Member_Removes_Their_Photo_From_Storage()
    {
        await AuthenticateAsync();
        var uploaded = await UploadSingleImageAsync();

        var created = await _client.PostAsJsonAsync("/api/admin/team",
            TeamPayload("Photo Person", photoUrl: uploaded.Url));
        created.EnsureSuccessStatusCode();
        var member = await created.Content.ReadFromJsonAsync<TeamRow>();

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(uploaded.Url)).StatusCode);

        var deleted = await _client.DeleteAsync($"/api/admin/team/{member!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(uploaded.Url)).StatusCode);
    }

    [Fact]
    public async Task Replacing_A_Team_Photo_Deletes_The_Old_One()
    {
        await AuthenticateAsync();
        var oldPhoto = await UploadSingleImageAsync();
        var newPhoto = await UploadSingleImageAsync();

        var created = await _client.PostAsJsonAsync("/api/admin/team",
            TeamPayload("Swap Photo Person", photoUrl: oldPhoto.Url));
        var member = await created.Content.ReadFromJsonAsync<TeamRow>();

        var updated = await _client.PutAsJsonAsync($"/api/admin/team/{member!.Id}",
            TeamPayload("Swap Photo Person", photoUrl: newPhoto.Url));
        updated.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(oldPhoto.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(newPhoto.Url)).StatusCode);
    }

    private static object TeamPayload(string name, string? bioEn = null, string? photoUrl = null) => new
    {
        Name = name,
        RoleEn = "Volunteer", RoleNl = "Vrijwilliger", RoleAr = "متطوع",
        PhotoUrl = photoUrl,
        BioEn = bioEn, BioNl = (string?)null, BioAr = (string?)null,
        SortOrder = 0
    };

    private async Task<UploadResult> UploadSingleImageAsync()
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(CreatePng(400, 300));
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(part, "file", "photo.png");
        var response = await _client.PostAsync("/api/admin/uploads", content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UploadResult>())!;
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(20, 20, 20));
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private record LoginResult(string Token, string Email, DateTime ExpiresAt);
    private record TeamRow(int Id, string Name, string? Slug);
    private record UploadResult(string Url, string ThumbUrl);
}
