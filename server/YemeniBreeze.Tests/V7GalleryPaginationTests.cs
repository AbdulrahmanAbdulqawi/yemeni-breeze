using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Tests;

public class V7GalleryPaginationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;

    public V7GalleryPaginationTests(ApiFactory factory)
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

    /// <summary>Seeds gallery items directly (no upload pipeline) and returns the folder they're in.</summary>
    private async Task<int> SeedItemsAsync(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var folder = new MediaFolder { Name = $"page-test-{Guid.NewGuid():N}" };
        db.MediaFolders.Add(folder);
        await db.SaveChangesAsync();

        for (var i = 0; i < count; i++)
            db.GalleryItems.Add(new GalleryItem
            {
                FolderId = folder.Id,
                ImageUrl = $"/api/media/{Guid.NewGuid():N}.webp",
                MediaType = "image",
                SortOrder = i
            });
        await db.SaveChangesAsync();
        return folder.Id;
    }

    [Fact]
    public async Task Page_Slices_By_Skip_And_Take_And_Reports_Total()
    {
        await AuthenticateAsync();
        var folderId = await SeedItemsAsync(25);

        var first = await _client.GetFromJsonAsync<GalleryPageRow>($"/api/gallery/page?folderId={folderId}&skip=0&take=10");
        Assert.Equal(25, first!.Total);
        Assert.Equal(10, first.Items.Count);

        var second = await _client.GetFromJsonAsync<GalleryPageRow>($"/api/gallery/page?folderId={folderId}&skip=10&take=10");
        Assert.Equal(25, second!.Total);
        Assert.Equal(10, second.Items.Count);

        var third = await _client.GetFromJsonAsync<GalleryPageRow>($"/api/gallery/page?folderId={folderId}&skip=20&take=10");
        Assert.Equal(5, third!.Items.Count);

        // no overlap across pages
        var firstIds = first.Items.Select(i => i.Id).ToHashSet();
        Assert.DoesNotContain(second.Items, i => firstIds.Contains(i.Id));
    }

    [Fact]
    public async Task Page_Filters_By_Folder()
    {
        await AuthenticateAsync();
        var folderA = await SeedItemsAsync(3);
        var folderB = await SeedItemsAsync(4);

        var pageA = await _client.GetFromJsonAsync<GalleryPageRow>($"/api/gallery/page?folderId={folderA}&skip=0&take=50");
        var pageB = await _client.GetFromJsonAsync<GalleryPageRow>($"/api/gallery/page?folderId={folderB}&skip=0&take=50");

        Assert.Equal(3, pageA!.Total);
        Assert.Equal(4, pageB!.Total);
    }

    [Fact]
    public async Task Page_Take_Is_Clamped_To_Max_100()
    {
        await AuthenticateAsync();
        var folderId = await SeedItemsAsync(120);

        var page = await _client.GetFromJsonAsync<GalleryPageRow>($"/api/gallery/page?folderId={folderId}&skip=0&take=500");
        Assert.Equal(120, page!.Total);
        Assert.Equal(100, page.Items.Count); // clamped
    }

    [Fact]
    public async Task Page_Order_Matches_Unpaginated_Gallery()
    {
        await AuthenticateAsync();
        var folderId = await SeedItemsAsync(8);

        var full = await _client.GetFromJsonAsync<List<GalleryItemRow>>("/api/gallery");
        var expectedOrder = full!.Where(i => i.FolderId == folderId).Select(i => i.Id).ToList();

        var page = await _client.GetFromJsonAsync<GalleryPageRow>($"/api/gallery/page?folderId={folderId}&skip=0&take=50");
        Assert.Equal(expectedOrder, page!.Items.Select(i => i.Id).ToList());
    }

    private record LoginResult(string Token, string Email, DateTime ExpiresAt);
    private record GalleryPageRow(List<GalleryItemRow> Items, int Total);
    private record GalleryItemRow(int Id, int? FolderId);
}
