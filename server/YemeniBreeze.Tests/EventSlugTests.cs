using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Tests;

public class EventSlugTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;

    public EventSlugTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record SlugLoginResult(string Token);

    private async Task AuthenticateAsync()
    {
        if (_client.DefaultRequestHeaders.Authorization is not null) return;
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@yemenbreeze.nl", password = "ChangeMe!2026" });
        var login = await response.Content.ReadFromJsonAsync<SlugLoginResult>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    private async Task<(int id, string slug)> SeedEventAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event
        {
            Slug = $"slug-{Guid.NewGuid():N}",
            TitleEn = "Slug Event",
            Date = new DateOnly(2026, 12, 1),
            Capacity = 10,
            Status = EventStatus.Published,
            IsRegistrationOpen = true
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (ev.Id, ev.Slug);
    }

    private static object Input(string slug) => new
    {
        Slug = slug,
        TitleEn = "T", TitleNl = "T", TitleAr = "T",
        DescriptionEn = "D", DescriptionNl = "D", DescriptionAr = "D",
        Date = new DateOnly(2026, 12, 1),
        StartTime = "18:00", EndTime = "20:00", Location = "Here",
        Capacity = 10, ImageUrl = (string?)null,
        Status = EventStatus.Draft, IsRegistrationOpen = true
    };

    [Fact]
    public async Task Slug_Differing_Only_By_Case_Or_Spaces_Gets_Conflict_On_Create_And_Update()
    {
        await AuthenticateAsync();
        var (_, existing) = await SeedEventAsync();
        var (otherId, _) = await SeedEventAsync();
        var variant = "  " + existing.ToUpperInvariant() + " ";

        var create = await _client.PostAsJsonAsync("/api/admin/events", Input(variant));
        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);

        var update = await _client.PutAsJsonAsync($"/api/admin/events/{otherId}", Input(variant));
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("my event")]
    [InlineData("my/event")]
    [InlineData("my--event")]
    [InlineData("-my-event")]
    [InlineData("my-event-")]
    [InlineData("my_event")]
    public async Task Badly_Formatted_Slug_Gets_BadRequest_On_Create_And_Update(string slug)
    {
        await AuthenticateAsync();
        var (id, _) = await SeedEventAsync();

        var create = await _client.PostAsJsonAsync("/api/admin/events", Input(slug));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        var update = await _client.PutAsJsonAsync($"/api/admin/events/{id}", Input(slug));
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Valid_Slug_With_Capitals_And_Spaces_Is_Saved_Normalized()
    {
        await AuthenticateAsync();
        var slug = $"ok-{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync("/api/admin/events", Input(" " + slug.ToUpperInvariant() + " "));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }
}
