using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<GalleryItem> GalleryItems => Set<GalleryItem>();
    public DbSet<MediaFolder> MediaFolders => Set<MediaFolder>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Event>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.TitleEn).HasMaxLength(200);
            e.Property(x => x.TitleNl).HasMaxLength(200);
            e.Property(x => x.TitleAr).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(200);
            e.Property(x => x.Location).HasMaxLength(300);
        });

        builder.Entity<Registration>(r =>
        {
            r.Property(x => x.FullName).HasMaxLength(200);
            r.Property(x => x.Email).HasMaxLength(300);
            r.Property(x => x.Phone).HasMaxLength(50);
            r.Property(x => x.Note).HasMaxLength(1000);
            r.HasOne(x => x.Event)
                .WithMany(x => x.Registrations)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            r.HasIndex(x => new { x.EventId, x.Email }).IsUnique();
            r.HasIndex(x => x.TicketCode).IsUnique();
            r.Property(x => x.Language).HasMaxLength(5);
        });

        builder.Entity<SiteSetting>(s =>
        {
            s.HasKey(x => x.Key);
            s.Property(x => x.Key).HasMaxLength(100);
            s.Property(x => x.Value).HasMaxLength(1000);
        });

        builder.Entity<MediaFolder>(f =>
        {
            f.Property(x => x.Name).HasMaxLength(200);
            f.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GalleryItem>(g =>
        {
            g.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.SetNull);
            g.HasOne(x => x.Folder)
                .WithMany()
                .HasForeignKey(x => x.FolderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ContactMessage>(c =>
        {
            c.Property(x => x.Name).HasMaxLength(200);
            c.Property(x => x.Email).HasMaxLength(300);
            c.Property(x => x.Subject).HasMaxLength(300);
            c.Property(x => x.Message).HasMaxLength(4000);
        });
    }
}
