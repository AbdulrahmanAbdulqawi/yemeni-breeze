using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection!));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection?.Dispose();
    }
}

public class RegistrationWaitlistTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;

    public RegistrationWaitlistTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> CreateEventAsync(int capacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = $"test-event-{Guid.NewGuid():N}";
        db.Events.Add(new Event
        {
            Slug = slug,
            TitleEn = "Test Event",
            Date = new DateOnly(2026, 12, 1),
            Capacity = capacity,
            Status = EventStatus.Published,
            IsRegistrationOpen = true
        });
        await db.SaveChangesAsync();
        return slug;
    }

    private static object Registration(string email, int guests = 1) =>
        new { FullName = "Test Person", Email = email, Phone = "+3160000000", GuestsCount = guests, Note = "" };

    [Fact]
    public async Task Registers_As_Confirmed_While_Capacity_Remains()
    {
        var slug = await CreateEventAsync(capacity: 3);

        var response = await _client.PostAsJsonAsync($"/api/events/{slug}/register", Registration("a@test.nl", 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegistrationResult>();
        Assert.Equal("Confirmed", result!.Status);
    }

    [Fact]
    public async Task Waitlists_When_Capacity_Would_Be_Exceeded()
    {
        var slug = await CreateEventAsync(capacity: 2);

        await _client.PostAsJsonAsync($"/api/events/{slug}/register", Registration("first@test.nl", 2));
        var response = await _client.PostAsJsonAsync($"/api/events/{slug}/register", Registration("second@test.nl", 1));

        var result = await response.Content.ReadFromJsonAsync<RegistrationResult>();
        Assert.Equal("Waitlisted", result!.Status);
    }

    [Fact]
    public async Task Rejects_Duplicate_Email_For_Same_Event()
    {
        var slug = await CreateEventAsync(capacity: 10);

        await _client.PostAsJsonAsync($"/api/events/{slug}/register", Registration("dup@test.nl"));
        var response = await _client.PostAsJsonAsync($"/api/events/{slug}/register", Registration("dup@test.nl"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_When_Registration_Closed()
    {
        var slug = await CreateEventAsync(capacity: 10);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ev = await db.Events.SingleAsync(e => e.Slug == slug);
            ev.IsRegistrationOpen = false;
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync($"/api/events/{slug}/register", Registration("late@test.nl"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Endpoints_Require_Authentication()
    {
        var response = await _client.GetAsync("/api/admin/events");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record RegistrationResult(int Id, string Status, string EventSlug);
}
