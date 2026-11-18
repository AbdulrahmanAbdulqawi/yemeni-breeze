using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Security;

namespace YemeniBreeze.Api.Features.Contact;

public record ContactRequest(string Name, string Email, string Subject, string Message);

public static class ContactEndpoints
{
    public static void MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contact", async (ContactRequest request, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Message))
                return Results.BadRequest(new { message = "Name, email and message are required." });

            var name = request.Name.Trim();
            var email = request.Email.Trim();
            var subject = request.Subject?.Trim() ?? "";
            var message = request.Message.Trim();
            if (!IsValidEmail(email))
                return Results.BadRequest(new { message = "Please enter a valid email address." });
            if (name.Length > 200 || email.Length > 300 || subject.Length > 300 || message.Length > 4000)
                return Results.BadRequest(new { message = "One of the fields is too long." });

            db.ContactMessages.Add(new ContactMessage
            {
                Name = name,
                Email = email,
                Subject = subject,
                Message = message
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Thank you — we will get back to you." });
        }).RequireRateLimiting(RateLimiting.PublicForms);

        var admin = app.MapGroup("/api/admin/contact").RequireAuthorization();

        admin.MapGet("/", async (AppDbContext db) =>
            await db.ContactMessages.OrderByDescending(c => c.CreatedAt).ToListAsync());

        admin.MapPost("/{id:int}/read", async (int id, AppDbContext db) =>
        {
            var msg = await db.ContactMessages.FindAsync(id);
            if (msg is null) return Results.NotFound();
            msg.IsRead = true;
            await db.SaveChangesAsync();
            return Results.Ok(msg);
        });

        admin.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var msg = await db.ContactMessages.FindAsync(id);
            if (msg is null) return Results.NotFound();
            db.ContactMessages.Remove(msg);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // Same rule as event registration.
    private static bool IsValidEmail(string email) =>
        email.Length <= 254
        && MailAddress.TryCreate(email, out var address)
        && address.Address == email
        && address.Host.Contains('.')
        && !address.Host.StartsWith('.') && !address.Host.EndsWith('.');
}
