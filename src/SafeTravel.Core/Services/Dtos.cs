using System.ComponentModel.DataAnnotations;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

public record CategoryDto(int Id, string Slug, string Name, string Icon, string Kind, bool IsActive, int ListingCount);

public record ListingDto(int Id, string Name, int CategoryId, string Category, string CategorySlug, string Icon, string Kind,
    string Town, string District, string Phone, string Hours, string Description, double Latitude, double Longitude,
    string Status, bool IsVerified, bool IsFeatured, double Rating, int Reviews, int? SupplierId, double? DistanceKm, PromotionDto? Promotion = null);

/// <summary>A running promotion on a listing. Shown as a red badge on the tile.</summary>
public record PromotionDto(string Title, string Text, DateOnly? Until);

public class PromotionInput
{
    [Required, StringLength(80, MinimumLength = 3)] public string Title { get; set; } = "";
    [StringLength(300)] public string? Text { get; set; }
    public DateOnly? Until { get; set; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public class ListingQuery
{
    public string? Kind { get; set; }
    public string? Category { get; set; }
    public string? District { get; set; }
    [MaxLength(80)] public string? Q { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    [Range(0.1, 500)] public double? RadiusKm { get; set; }
    public bool? Featured { get; set; }
    /// <summary>true returns only listings with a running promotion.</summary>
    public bool? Promo { get; set; }
    [Range(1, 10000)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 50;
}

public class ListingInput
{
    [Required, StringLength(160, MinimumLength = 2)] public string Name { get; set; } = "";
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [Required, StringLength(80)] public string Town { get; set; } = "";
    [Required, StringLength(60)] public string District { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(160)] public string? Hours { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    [Range(5.5, 10.2)] public double? Latitude { get; set; }
    [Range(79.0, 82.5)] public double? Longitude { get; set; }
}

public class CategoryInput
{
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(16)] public string? Icon { get; set; }
    [Required, RegularExpression("help|business")] public string Kind { get; set; } = "help";
}

public class SupplierRegistration
{
    [Required, StringLength(160, MinimumLength = 2)] public string BusinessName { get; set; } = "";
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [Required, StringLength(80)] public string Town { get; set; } = "";
    [Required, StringLength(60)] public string District { get; set; } = "";
    [Required, StringLength(120)] public string ContactName { get; set; } = "";
    [Required, Phone, StringLength(30)] public string Phone { get; set; } = "";
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 10)] public string Password { get; set; } = "";
    [StringLength(160)] public string? Hours { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
}

public record DocumentDto(int Id, string Name, string FileName, string Status, DateTime UploadedUtc);

public record SupplierDto(int Id, string BusinessName, string ContactName, string Email, string Phone, string Status,
    string? RejectReason, DateOnly? PaidUntil, int? DaysLeft, int Views, int Calls, int Directions,
    ListingDto? Listing, IReadOnlyList<DocumentDto> Documents, IReadOnlyList<ListingDto> Listings);

public record AlertDto(int Id, string Level, string Type, string Area, string Title, string Source, DateTime PublishedUtc, bool IsActive,
    string Details = "", double? Latitude = null, double? Longitude = null);

/// <summary>A risk zone on the safety map. Type is flood, landslide, roadclosure, beachflag, wildlife or other.</summary>
public record RiskZoneDto(int Id, string Type, string Name, string Details, string Source, double Latitude, double Longitude, int RadiusMeters, bool IsActive, DateTime UpdatedUtc);

public class RiskZoneInput
{
    [Required, RegularExpression("flood|landslide|roadclosure|beachflag|wildlife|other")] public string Type { get; set; } = "other";
    [Required, StringLength(120, MinimumLength = 3)] public string Name { get; set; } = "";
    [StringLength(600)] public string? Details { get; set; }
    [StringLength(120)] public string? Source { get; set; }
    /// <summary>Inside Sri Lanka and its coastal waters.</summary>
    [Range(5.5, 10.2)] public double Latitude { get; set; }
    [Range(79.2, 82.2)] public double Longitude { get; set; }
    [Range(0, 50000)] public int RadiusMeters { get; set; }
    public bool IsActive { get; set; } = true;
}

public record PartnerEnquiryDto(int Id, string BusinessName, string BusinessType, string ContactName, string Email, string Phone, int? GuestsPerMonth, string Message, bool IsHandled, DateTime CreatedUtc);

public class PartnerEnquiryInput
{
    [Required, StringLength(160, MinimumLength = 2)] public string BusinessName { get; set; } = "";
    [Required, RegularExpression("hotel|guesthouse|operator|school|other")] public string BusinessType { get; set; } = "hotel";
    [Required, StringLength(120, MinimumLength = 2)] public string ContactName { get; set; } = "";
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
    [RegularExpression(@"^\+?[0-9 ]{7,20}$", ErrorMessage = "Enter a phone number, for example 077 123 4567.")] public string? Phone { get; set; }
    [Range(0, 1_000_000)] public int? GuestsPerMonth { get; set; }
    [StringLength(1000)] public string? Message { get; set; }
}

public record ContactDto(int Id, string Name, string Phone, string Relationship);

public class ContactInput
{
    [Required, StringLength(120, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, RegularExpression(@"^\+?[0-9 ]{7,20}$", ErrorMessage = "Enter a phone number with its country code, for example +44 7700 900123.")] public string Phone { get; set; } = "";
    [StringLength(60)] public string? Relationship { get; set; }
}

public record TripStopDto(int Id, string Place, DateOnly? Date, string Note);

public class TripStopInput
{
    [Required, StringLength(120, MinimumLength = 2)] public string Place { get; set; } = "";
    public DateOnly? Date { get; set; }
    [StringLength(200)] public string? Note { get; set; }
}

/// <summary>A review of a business. Status is pending, approved or rejected.</summary>
public record ReviewDto(int Id, int ListingId, string ListingName, string AuthorName, int Stars, string Text, string Status, DateTime CreatedUtc);

public class ReviewInput
{
    [Range(1, int.MaxValue)] public int ListingId { get; set; }
    [Range(1, 5)] public int Stars { get; set; }
    [StringLength(1000)] public string? Text { get; set; }
}

/// <summary>Everything on the traveller's account page.</summary>
public record AccountDto(string FullName, string Email, IReadOnlyList<ContactDto> Contacts, IReadOnlyList<TripStopDto> Trip, IReadOnlyList<ReviewDto> Reviews, bool CheckInEnabled);

public class AlertInput
{
    [Required, RegularExpression("red|amber|blue")] public string Level { get; set; } = "amber";
    [Required, StringLength(60)] public string Type { get; set; } = "";
    [Required, StringLength(120)] public string Area { get; set; } = "";
    [Required, StringLength(240, MinimumLength = 5)] public string Title { get; set; } = "";
    [StringLength(120)] public string? Source { get; set; }
    [StringLength(600)] public string? Details { get; set; }
    /// <summary>Optional pin on the safety map. Give both or neither.</summary>
    [Range(5.5, 10.2)] public double? Latitude { get; set; }
    [Range(79.2, 82.2)] public double? Longitude { get; set; }
}

public record PaymentDto(int Id, DateTime CreatedUtc, string Kind, string Payer, decimal Amount, string Currency, string Status, string? Reference);

public class DonationInput
{
    [Range(100, 10_000_000)] public decimal Amount { get; set; }
    [StringLength(160)] public string? Name { get; set; }
    [EmailAddress, StringLength(200)] public string? Email { get; set; }
}

public record NumberDto(int Id, string Label, string Number);
public record TopicDto(int Id, string Name, string Summary, string Icon, string Tone);
public record SponsorDto(int Id, string Name, string Role, bool IsActive);

/// <summary>A topic with its do and avoid lists, as shown on the topic's own page.</summary>
public record TopicDetailDto(int Id, string Name, string Summary, string Icon, string Tone, IReadOnlyList<string> Dos, IReadOnlyList<string> Donts);

public class TopicGuidanceInput
{
    /// <summary>One tip per line.</summary>
    [StringLength(2000)] public string? Dos { get; set; }
    [StringLength(2000)] public string? Donts { get; set; }
}

/// <summary>A first aid guide. VideoEmbedUrl is the player address for a Vimeo or Streamable video, or null.</summary>
public record FirstAidGuideDto(int Id, string Title, string Icon, IReadOnlyList<string> Steps, string? VideoUrl, string? VideoEmbedUrl, bool IsActive);

public class FirstAidGuideInput
{
    [Required, StringLength(120, MinimumLength = 3)] public string Title { get; set; } = "";
    [StringLength(16)] public string? Icon { get; set; }
    /// <summary>One step per line.</summary>
    [Required, StringLength(4000)] public string Steps { get; set; } = "";
    [StringLength(300)] public string? VideoUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public record ProductDto(int Id, int ListingId, string Name, string Description, decimal? Price, string Unit, string? ImageUrl, bool IsActive);

public class ProductInput
{
    [Required, StringLength(120, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(400)] public string? Description { get; set; }
    [Range(0, 100_000_000)] public decimal? Price { get; set; }
    [StringLength(40)] public string? Unit { get; set; }
    public bool IsActive { get; set; } = true;
}

public record AdDto(int Id, int Slot, int SponsorId, string Sponsor, string Headline, string Text, string LinkLabel, string LinkUrl,
    string? ImageUrl, bool IsActive, int Clicks, string Placement);

public class AdInput
{
    [Range(1, int.MaxValue)] public int SponsorId { get; set; }
    [Required, StringLength(120, MinimumLength = 3)] public string Headline { get; set; } = "";
    [StringLength(240)] public string? Text { get; set; }
    [StringLength(40)] public string? LinkLabel { get; set; }
    [Required, StringLength(300), RegularExpression(@"^https://\S+$", ErrorMessage = "The link must start with https://")] public string LinkUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public record HomeContentDto(string HeroTitle, string HeroText, decimal AnnualFee, IReadOnlyDictionary<string, bool> Sections,
    IReadOnlyList<NumberDto> EmergencyNumbers, IReadOnlyList<TopicDto> Topics, IReadOnlyList<SponsorDto> Sponsors);

public class HeroInput
{
    [Required, StringLength(120, MinimumLength = 3)] public string HeroTitle { get; set; } = "";
    [StringLength(400)] public string? HeroText { get; set; }
}

public class NumberInput
{
    [Required, StringLength(60)] public string Label { get; set; } = "";
    [Required, RegularExpression(@"^[0-9+ ]{2,30}$")] public string Number { get; set; } = "";
}

public class TopicInput
{
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, StringLength(400)] public string Summary { get; set; } = "";
    [StringLength(16)] public string? Icon { get; set; }
}

public class SponsorInput
{
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, StringLength(120)] public string Role { get; set; } = "";
}

public record AuditDto(long Id, DateTime WhenUtc, string UserName, string Action);

public static class Map
{
    public static string Slug(this ListingStatus s) => s.ToString().ToLowerInvariant();

    public static ListingDto ToDto(this Listing l, double? distanceKm = null) => new(
        l.Id, l.Name, l.CategoryId, l.Category?.Name ?? "", l.Category?.Slug ?? "", l.Category?.Icon ?? "📍",
        (l.Category?.Kind ?? CategoryKind.Help).ToString().ToLowerInvariant(),
        l.Town, l.District, l.Phone, l.Hours, l.Description, l.Latitude, l.Longitude, l.Status.Slug(),
        l.IsVerified, l.IsFeatured, Math.Round(l.RatingAverage, 1), l.RatingCount, l.SupplierId,
        distanceKm is null ? null : Math.Round(distanceKm.Value, 1),
        l.PromoTitle.Length > 0 && (l.PromoUntil is null || l.PromoUntil >= Today) ? new PromotionDto(l.PromoTitle, l.PromoText, l.PromoUntil) : null);

    /// <summary>Today's date in Sri Lanka (UTC+5:30).</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

    public static ProductDto ToDto(this Product p) => new(p.Id, p.ListingId, p.Name, p.Description, p.Price, p.Unit,
        p.ImagePath is null ? null : $"/api/v1/products/{p.Id}/image?v={p.UpdatedUtc.Ticks}", p.IsActive);

    public static AdDto ToDto(this Advertisement a) => new(a.Id, a.Slot, a.SponsorId, a.Sponsor?.Name ?? "", a.Headline, a.Text,
        string.IsNullOrWhiteSpace(a.LinkLabel) ? "Learn more" : a.LinkLabel, a.LinkUrl,
        a.ImagePath is null ? null : $"/api/v1/ads/{a.Id}/image?v={a.UpdatedUtc.Ticks}", a.IsActive, a.Clicks,
        Advertisement.IsTop(a.Slot) ? "top" : "section");

    public static AlertDto ToDto(this Alert a) => new(a.Id, a.Level.ToString().ToLowerInvariant(), a.Type, a.Area, a.Title, a.Source, a.PublishedUtc, a.IsActive, a.Details, a.Latitude, a.Longitude);

    public static PaymentDto ToDto(this Payment p) => new(p.Id, p.CreatedUtc, p.Kind == PaymentKind.Donation ? "Donation" : "Supplier fee",
        p.PayerName, p.Amount, p.Currency, p.Status.ToString(), p.GatewayReference);

    public static SupplierDto ToDto(this Supplier s, DateOnly today)
    {
        var listings = s.Listings.OrderBy(l => l.Id).Select(l => l.ToDto()).ToList();
        var listing = s.Listings.OrderBy(l => l.Id).FirstOrDefault();
        return new(s.Id, s.BusinessName, s.ContactName, s.Email, s.Phone, s.Status.ToString().ToLowerInvariant(), s.RejectReason, s.PaidUntil,
            s.PaidUntil is null ? null : s.PaidUntil.Value.DayNumber - today.DayNumber, s.Views, s.Calls, s.Directions, listing?.ToDto(),
            s.Documents.OrderBy(d => d.Id).Select(d => new DocumentDto(d.Id, d.Name, d.FileName, d.Status.ToString().ToLowerInvariant(), d.UploadedUtc)).ToList(), listings);
    }
}

/// <summary>A rule was broken (not found, wrong state). The API turns it into a clear error for the caller.</summary>
public class RuleException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
    public static RuleException NotFound(string what) => new($"{what} was not found.", 404);
}
