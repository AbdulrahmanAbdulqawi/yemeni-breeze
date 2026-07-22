using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

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

            db.ContactMessages.Add(new ContactMessage
            {
                Name = request.Name.Trim(),
                Email = request.Email.Trim(),
                Subject = request.Subject?.Trim() ?? "",
                Message = request.Message.Trim()
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Thank you — we will get back to you." });
        });

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
}
