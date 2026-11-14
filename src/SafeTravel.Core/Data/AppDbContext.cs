using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<int>, int>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierDocument> SupplierDocuments => Set<SupplierDocument>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Advertisement> Advertisements => Set<Advertisement>();
    public DbSet<EmergencyNumber> EmergencyNumbers => Set<EmergencyNumber>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<FirstAidGuide> FirstAidGuides => Set<FirstAidGuide>();
    public DbSet<RiskZone> RiskZones => Set<RiskZone>();
    public DbSet<TravellerContact> TravellerContacts => Set<TravellerContact>();
    public DbSet<TripStop> TripStops => Set<TripStop>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Category>().HasIndex(x => x.Slug).IsUnique();

        b.Entity<Listing>(e =>
        {
            e.HasIndex(x => new { x.Status, x.CategoryId });
            e.HasIndex(x => new { x.Status, x.District });
            e.HasIndex(x => x.SupplierId);
            e.HasOne(x => x.Category).WithMany(c => c.Listings).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier).WithMany(s => s.Listings).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Product>(e =>
        {
            e.HasIndex(x => new { x.ListingId, x.SortOrder });
            e.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Advertisement>(e =>
        {
            e.HasIndex(x => x.Slot).IsUnique();
            e.HasOne(x => x.Sponsor).WithMany().HasForeignKey(x => x.SponsorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Supplier>(e =>
        {
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.PaidUntil);
            e.HasIndex(x => x.Email);
        });

        b.Entity<AppUser>().HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.Kind, x.Status });
            e.HasIndex(x => x.CreatedUtc);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Alert>().HasIndex(x => new { x.IsActive, x.PublishedUtc });
        b.Entity<RiskZone>().HasIndex(x => x.IsActive);

        // A traveller's own data goes when the account goes.
        b.Entity<TravellerContact>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TripStop>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Review>(e =>
        {
            e.HasIndex(x => new { x.ListingId, x.Status });
            e.HasIndex(x => new { x.UserId, x.ListingId }).IsUnique();
            e.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
            // Restrict keeps SQL Server to one cascade path into Reviews.
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AuditEntry>().HasIndex(x => x.WhenUtc);
        b.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<ReminderLog>().HasIndex(x => new { x.SupplierId, x.RenewalDate, x.DaysBefore }).IsUnique();

        // SQLite (used by the automated tests) has no rowversion type, so the concurrency token is left to SQL Server.
        if (Database.ProviderName?.Contains("Sqlite") == true)
        {
            b.Entity<Supplier>().Ignore(x => x.RowVersion);
            b.Entity<Listing>().Ignore(x => x.RowVersion);
        }
    }
}
