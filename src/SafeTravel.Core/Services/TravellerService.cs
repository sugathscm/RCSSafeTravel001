using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

/// <summary>A traveller's own data: emergency contacts and the trip plan. Every call is limited to the signed-in traveller.</summary>
public class TravellerService(AppDbContext db)
{
    public const int MaxContacts = 5, MaxStops = 20;

    public async Task<IReadOnlyList<ContactDto>> ContactsAsync(int userId, CancellationToken ct = default) =>
        await db.TravellerContacts.AsNoTracking().Where(c => c.UserId == userId).OrderBy(c => c.Id)
            .Select(c => new ContactDto(c.Id, c.Name, c.Phone, c.Relationship)).ToListAsync(ct);

    public async Task<ContactDto> AddContactAsync(int userId, ContactInput input, CancellationToken ct = default)
    {
        if (await db.TravellerContacts.CountAsync(c => c.UserId == userId, ct) >= MaxContacts)
            throw new RuleException($"You can keep up to {MaxContacts} contacts. Remove one to add another.");
        var c = new TravellerContact { UserId = userId, Name = input.Name.Trim(), Phone = input.Phone.Trim(), Relationship = input.Relationship?.Trim() ?? "" };
        db.TravellerContacts.Add(c);
        await db.SaveChangesAsync(ct);
        return new(c.Id, c.Name, c.Phone, c.Relationship);
    }

    public async Task RemoveContactAsync(int userId, int id, CancellationToken ct = default)
    {
        var c = await db.TravellerContacts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct) ?? throw RuleException.NotFound("Contact");
        db.TravellerContacts.Remove(c);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The trip plan in date order. Stops without a date come last.</summary>
    public async Task<IReadOnlyList<TripStopDto>> TripAsync(int userId, CancellationToken ct = default)
    {
        var stops = await db.TripStops.AsNoTracking().Where(s => s.UserId == userId).ToListAsync(ct);
        return stops.OrderBy(s => s.Date is null).ThenBy(s => s.Date).ThenBy(s => s.Id).Select(s => new TripStopDto(s.Id, s.Place, s.Date, s.Note)).ToList();
    }

    public async Task<TripStopDto> AddStopAsync(int userId, TripStopInput input, CancellationToken ct = default)
    {
        if (await db.TripStops.CountAsync(s => s.UserId == userId, ct) >= MaxStops)
            throw new RuleException($"A trip plan holds up to {MaxStops} stops. Remove one to add another.");
        var s = new TripStop { UserId = userId, Place = input.Place.Trim(), Date = input.Date, Note = input.Note?.Trim() ?? "" };
        db.TripStops.Add(s);
        await db.SaveChangesAsync(ct);
        return new(s.Id, s.Place, s.Date, s.Note);
    }

    public async Task RemoveStopAsync(int userId, int id, CancellationToken ct = default)
    {
        var s = await db.TripStops.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct) ?? throw RuleException.NotFound("Trip stop");
        db.TripStops.Remove(s);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Traveller reviews of businesses. A review waits for staff, and only an approved one counts in the listing's rating.</summary>
public class ReviewService(AppDbContext db, AuditService audit)
{
    private static ReviewDto ToDto(Review r) =>
        new(r.Id, r.ListingId, r.Listing?.Name ?? "", r.AuthorName, r.Stars, r.Text, r.Status.ToString().ToLowerInvariant(), r.CreatedUtc);

    /// <summary>"Emma Wilson" is shown as "Emma W.".</summary>
    public static string ShortName(string fullName)
    {
        var parts = (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "Traveller" : parts.Length == 1 ? parts[0] : $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";
    }

    /// <summary>The approved reviews of a listing, newest first.</summary>
    public async Task<IReadOnlyList<ReviewDto>> ForListingAsync(int listingId, CancellationToken ct = default)
    {
        var list = await db.Reviews.AsNoTracking().Include(r => r.Listing).Where(r => r.ListingId == listingId && r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.CreatedUtc).Take(50).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ReviewDto>> ByUserAsync(int userId, CancellationToken ct = default)
    {
        var list = await db.Reviews.AsNoTracking().Include(r => r.Listing).Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedUtc).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<ReviewDto> SubmitAsync(int userId, string fullName, ReviewInput input, CancellationToken ct = default)
    {
        var listing = await db.Listings.Include(l => l.Category).FirstOrDefaultAsync(l => l.Id == input.ListingId && l.Status == ListingStatus.Published, ct)
            ?? throw RuleException.NotFound("Listing");
        if (listing.Category?.Kind != CategoryKind.Business) throw new RuleException("Reviews are for businesses only.");

        var r = await db.Reviews.FirstOrDefaultAsync(x => x.ListingId == listing.Id && x.UserId == userId, ct);
        if (r is not null && r.Status == ReviewStatus.Approved) throw new RuleException("You have already reviewed this place.");
        if (r is null) { r = new Review { ListingId = listing.Id, UserId = userId }; db.Reviews.Add(r); }
        r.AuthorName = ShortName(fullName);
        r.Stars = input.Stars;
        r.Text = input.Text?.Trim() ?? "";
        r.Status = ReviewStatus.Pending;
        r.CreatedUtc = DateTime.UtcNow;
        r.DecidedUtc = null;
        await db.SaveChangesAsync(ct);
        r.Listing = listing;
        return ToDto(r);
    }

    /// <summary>Reviews for staff: those waiting come first.</summary>
    public async Task<IReadOnlyList<ReviewDto>> AdminListAsync(string? status, CancellationToken ct = default)
    {
        IQueryable<Review> q = db.Reviews.AsNoTracking().Include(r => r.Listing);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReviewStatus>(status, true, out var s)) q = q.Where(r => r.Status == s);
        var list = await q.OrderBy(r => r.Status).ThenByDescending(r => r.CreatedUtc).Take(200).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<ReviewDto> DecideAsync(int id, bool approve, CancellationToken ct = default)
    {
        var r = await db.Reviews.Include(x => x.Listing).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw RuleException.NotFound("Review");
        if (r.Status != ReviewStatus.Pending) throw new RuleException("This review has already been decided.");
        r.Status = approve ? ReviewStatus.Approved : ReviewStatus.Rejected;
        r.DecidedUtc = DateTime.UtcNow;
        if (approve && r.Listing is not null)
        {
            // Add this rating to the listing's running average.
            var l = r.Listing;
            l.RatingAverage = (l.RatingAverage * l.RatingCount + r.Stars) / (l.RatingCount + 1);
            l.RatingCount++;
        }
        audit.Add($"{(approve ? "Approved" : "Rejected")} a review of \"{r.Listing?.Name}\"", nameof(Review), id);
        await db.SaveChangesAsync(ct);
        return ToDto(r);
    }

    public async Task RemoveAsync(int id, CancellationToken ct = default)
    {
        var r = await db.Reviews.Include(x => x.Listing).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw RuleException.NotFound("Review");
        if (r.Status == ReviewStatus.Approved && r.Listing is not null && r.Listing.RatingCount > 0)
        {
            // Take this rating back out of the listing's running average.
            var l = r.Listing;
            l.RatingAverage = l.RatingCount == 1 ? 0 : (l.RatingAverage * l.RatingCount - r.Stars) / (l.RatingCount - 1);
            l.RatingCount--;
        }
        db.Reviews.Remove(r);
        audit.Add($"Deleted a review of \"{r.Listing?.Name}\"", nameof(Review), id);
        await db.SaveChangesAsync(ct);
    }
}
