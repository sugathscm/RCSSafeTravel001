using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SafeTravel.Core.Entities;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ContentReviewer = "ContentReviewer";
    public const string AlertsOfficer = "AlertsOfficer";
    public const string SupplierManager = "SupplierManager";
    public const string FinanceOfficer = "FinanceOfficer";
    public const string CallCentreAgent = "CallCentreAgent";
    public const string Supplier = "Supplier";
    public const string Traveller = "Traveller";

    public static readonly string[] Staff = [SuperAdmin, ContentReviewer, AlertsOfficer, SupplierManager, FinanceOfficer, CallCentreAgent];
    public static readonly string[] All = [.. Staff, Supplier, Traveller];
}

public class AppUser : IdentityUser<int>
{
    [MaxLength(120)] public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
}

public enum CategoryKind { Help = 0, Business = 1 }

public class Category
{
    public int Id { get; set; }
    [MaxLength(40)] public string Slug { get; set; } = "";
    [MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(16)] public string Icon { get; set; } = "📍";
    public CategoryKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public List<Listing> Listings { get; set; } = [];
}

public enum SupplierStatus { Pending = 0, Approved = 1, Rejected = 2 }

public class Supplier
{
    public int Id { get; set; }
    [MaxLength(160)] public string BusinessName { get; set; } = "";
    [MaxLength(120)] public string ContactName { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    public SupplierStatus Status { get; set; }
    [MaxLength(400)] public string? RejectReason { get; set; }
    /// <summary>Last day covered by the annual fee. Null until the first payment.</summary>
    public DateOnly? PaidUntil { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedUtc { get; set; }
    public int Views { get; set; }
    public int Calls { get; set; }
    public int Directions { get; set; }
    [Timestamp] public byte[]? RowVersion { get; set; }
    public List<SupplierDocument> Documents { get; set; } = [];
    public List<Listing> Listings { get; set; } = [];
}

public enum DocumentStatus { Uploaded = 0, Verified = 1, Rejected = 2 }

public class SupplierDocument
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(200)] public string FileName { get; set; } = "";
    [MaxLength(300)] public string StoragePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;
}

public enum ListingStatus
{
    /// <summary>Waiting for the admin team to review the supplier.</summary>
    Pending = 0,
    /// <summary>Supplier approved, annual fee not paid yet.</summary>
    Unpaid = 1,
    Published = 2,
    /// <summary>Hidden by staff.</summary>
    Hidden = 3,
    /// <summary>Annual fee ran out.</summary>
    Lapsed = 4,
    Rejected = 5
}

public class Listing
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    [MaxLength(160)] public string Name { get; set; } = "";
    [MaxLength(80)] public string Town { get; set; } = "";
    [MaxLength(60)] public string District { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(160)] public string Hours { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public ListingStatus Status { get; set; }
    public bool IsVerified { get; set; }
    public bool IsFeatured { get; set; }
    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    /// <summary>A promotion the supplier runs on this listing. Empty title means none.</summary>
    [MaxLength(80)] public string PromoTitle { get; set; } = "";
    [MaxLength(300)] public string PromoText { get; set; } = "";
    /// <summary>Last day the promotion is shown. Null means until the supplier removes it.</summary>
    public DateOnly? PromoUntil { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[]? RowVersion { get; set; }
}

/// <summary>Something a supplier sells under a listing: a room, a dish, a tour, a rental.</summary>
public class Product
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(400)] public string Description { get; set; } = "";
    /// <summary>Price in LKR. Null shows "Ask for price".</summary>
    [Microsoft.EntityFrameworkCore.Precision(12, 2)] public decimal? Price { get; set; }
    /// <summary>What the price is for, such as "per night" or "per person".</summary>
    [MaxLength(40)] public string Unit { get; set; } = "";
    [MaxLength(200)] public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public enum AlertLevel { Blue = 0, Amber = 1, Red = 2 }

public class Alert
{
    public int Id { get; set; }
    public AlertLevel Level { get; set; }
    [MaxLength(60)] public string Type { get; set; } = "";
    [MaxLength(120)] public string Area { get; set; } = "";
    [MaxLength(240)] public string Title { get; set; } = "";
    [MaxLength(120)] public string Source { get; set; } = "";
    public DateTime PublishedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresUtc { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Longer advice, shown on the alerts page and the safety map.</summary>
    [MaxLength(600)] public string Details { get; set; } = "";
    /// <summary>Where the alert applies, when staff pinned it on the map. Both are set, or neither.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public enum RiskZoneType { Flood = 0, Landslide = 1, RoadClosure = 2, BeachFlag = 3, Wildlife = 4, Other = 5 }

/// <summary>An area on the safety map that staff mark as risky: a flood or landslide zone, a closed road, a red-flag beach.</summary>
public class RiskZone
{
    public int Id { get; set; }
    public RiskZoneType Type { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(600)] public string Details { get; set; } = "";
    [MaxLength(120)] public string Source { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>Radius of the marked area in metres. 0 shows a pin only.</summary>
    public int RadiusMeters { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public enum PaymentKind { Donation = 0, SupplierFee = 1 }
public enum PaymentStatus { Pending = 0, Paid = 1, Failed = 2 }

public class Payment
{
    public int Id { get; set; }
    public PaymentKind Kind { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    [MaxLength(160)] public string PayerName { get; set; } = "";
    [MaxLength(200)] public string? PayerEmail { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(3)] public string Currency { get; set; } = "LKR";
    public PaymentStatus Status { get; set; }
    [MaxLength(80)] public string? GatewayReference { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidUtc { get; set; }
}

public class Sponsor
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(120)] public string Role { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>One of the three advertisement places on the Web Portal and in the apps. Each belongs to a sponsor.</summary>
public class Advertisement
{
    public const int Slots = 3;

    public int Id { get; set; }
    /// <summary>1, 2 or 3: the position on the page.</summary>
    public int Slot { get; set; }
    public int SponsorId { get; set; }
    public Sponsor? Sponsor { get; set; }
    [MaxLength(120)] public string Headline { get; set; } = "";
    [MaxLength(240)] public string Text { get; set; } = "";
    [MaxLength(40)] public string LinkLabel { get; set; } = "";
    [MaxLength(300)] public string LinkUrl { get; set; } = "";
    /// <summary>Stored file under the uploads folder, or null for a text-only advertisement.</summary>
    [MaxLength(200)] public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public int Clicks { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class EmergencyNumber
{
    public int Id { get; set; }
    [MaxLength(60)] public string Label { get; set; } = "";
    [MaxLength(30)] public string Number { get; set; } = "";
    public int SortOrder { get; set; }
}

public class Topic
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(400)] public string Summary { get; set; } = "";
    [MaxLength(16)] public string Icon { get; set; } = "📘";
    [MaxLength(20)] public string Tone { get; set; } = "blue";
    /// <summary>What to do, one tip per line. Shown on the topic's own page.</summary>
    [MaxLength(2000)] public string Dos { get; set; } = "";
    /// <summary>What to avoid, one tip per line.</summary>
    [MaxLength(2000)] public string Donts { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A step-by-step first aid guide, with an optional video hosted on Vimeo or Streamable.</summary>
public class FirstAidGuide
{
    public int Id { get; set; }
    [MaxLength(120)] public string Title { get; set; } = "";
    [MaxLength(16)] public string Icon { get; set; } = "🩹";
    /// <summary>The steps in order, one per line.</summary>
    [MaxLength(4000)] public string Steps { get; set; } = "";
    [MaxLength(300)] public string? VideoUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Key and value settings that drive the Web Portal (headline, sections, annual fee).</summary>
public class SiteSetting
{
    [Key, MaxLength(80)] public string Key { get; set; } = "";
    [MaxLength(2000)] public string Value { get; set; } = "";
}

public static class SettingKeys
{
    public const string HeroTitle = "hero.title";
    public const string HeroText = "hero.text";
    public const string AnnualFee = "supplier.annualFee";
    public const string SectionPrefix = "section.";
    public static readonly string[] Sections = ["alerts", "ads", "map", "businesses", "topics", "donate", "sponsors"];
}

public class AuditEntry
{
    public long Id { get; set; }
    public DateTime WhenUtc { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    [MaxLength(200)] public string UserName { get; set; } = "";
    [MaxLength(400)] public string Action { get; set; } = "";
    [MaxLength(60)] public string? Entity { get; set; }
    [MaxLength(40)] public string? EntityId { get; set; }
    [MaxLength(64)] public string? Ip { get; set; }
}

/// <summary>Refresh token for the mobile apps. Only a hash is stored.</summary>
public class RefreshToken
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    [MaxLength(88)] public string TokenHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public bool IsActive => RevokedUtc == null && ExpiresUtc > DateTime.UtcNow;
}

public class ReminderLog
{
    public long Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    /// <summary>Renewal date this reminder was for, so each reminder is sent once per cycle.</summary>
    public DateOnly RenewalDate { get; set; }
    public int DaysBefore { get; set; }
    [MaxLength(40)] public string Channel { get; set; } = "";
    public DateTime SentUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Someone a traveller wants told in an emergency: family or a friend.</summary>
public class TravellerContact
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(60)] public string Relationship { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>One stop on a traveller's trip plan.</summary>
public class TripStop
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    [MaxLength(120)] public string Place { get; set; } = "";
    public DateOnly? Date { get; set; }
    [MaxLength(200)] public string Note { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public enum ReviewStatus { Pending = 0, Approved = 1, Rejected = 2 }

/// <summary>A traveller's rating of a business. Staff check it before it is shown; one review per traveller per listing.</summary>
public class Review
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    public int UserId { get; set; }
    /// <summary>The name shown with the review: first name and the initial of the last name.</summary>
    [MaxLength(120)] public string AuthorName { get; set; } = "";
    public int Stars { get; set; }
    [MaxLength(1000)] public string Text { get; set; } = "";
    public ReviewStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedUtc { get; set; }
}

/// <summary>A hotel, guesthouse or tour operator that asked to share Safe Travel with its guests.</summary>
public class PartnerEnquiry
{
    public int Id { get; set; }
    [MaxLength(160)] public string BusinessName { get; set; } = "";
    /// <summary>hotel, guesthouse, operator, school or other.</summary>
    [MaxLength(20)] public string BusinessType { get; set; } = "hotel";
    [MaxLength(120)] public string ContactName { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    public int? GuestsPerMonth { get; set; }
    [MaxLength(1000)] public string Message { get; set; } = "";
    public bool IsHandled { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? HandledUtc { get; set; }
}
