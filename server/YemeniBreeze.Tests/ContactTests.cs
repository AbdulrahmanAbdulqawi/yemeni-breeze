using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemeniBreeze.Api.Data;

namespace YemeniBreeze.Tests;

/// <summary>Validation of the public contact form.</summary>
public class ContactTests(EmailApiFactory factory) : IClassFixture<EmailApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> PostAsync(string name, string email, string subject, string message) =>
        _client.PostAsJsonAsync("/api/contact", new { name, email, subject, message });

    private static async Task<string?> MessageOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();

    [Fact]
    public async Task Invalid_email_is_rejected_with_400()
    {
        var response = await PostAsync("Amina", "not-an-email", "Hi", "Hello there");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Please enter a valid email address.", await MessageOf(response));
    }

    [Fact]
    public async Task Message_one_character_over_limit_is_rejected_with_400()
    {
        var response = await PostAsync("Amina", "over@contact.test", "Hi", new string('m', 4001));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("One of the fields is too long.", await MessageOf(response));
    }

    [Fact]
    public async Task Fields_exactly_at_limits_are_saved()
    {
        var email = $"limit-{Guid.NewGuid():N}@contact.test";
        var response = await PostAsync(new string('n', 200), email, new string('s', 300), new string('m', 4000));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.ContactMessages.SingleAsync(c => c.Email == email);
        Assert.Equal(4000, saved.Message.Length);
        Assert.Equal(200, saved.Name.Length);
    }
}
