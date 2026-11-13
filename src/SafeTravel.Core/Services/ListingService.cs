using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

public class ListingService(AppDbContext db, AuditService audit, IClock clock)
{
    private IQueryable<Listing> Public() => db.Listings.AsNoTracking().Include(l => l.Category)
        .Where(l => l.Status == ListingStatus.Published && l.Category!.IsActive);

    /// <summary>Search used by the Web Portal and the mobile apps. Only published listings in active categories.</summary>
    public async Task<PagedResult<ListingDto>> SearchAsync(ListingQuery f, CancellationToken ct = default)
    {
        var q = Filter(Public(), f);

        if (f.Lat is double lat && f.Lng is double lng)
        {
            var radius = f.RadiusKm ?? 25;
            double dLat = radius / 111.0, dLng = radius / (111.0 * Math.Cos(lat * Math.PI / 180));
            var near = await q.Where(l => l.Latitude >= lat - dLat && l.Latitude <= lat + dLat && l.Longitude >= lng - dLng && l.Longitude <= lng + dLng)
                .Take(2000).ToListAsync(ct);
            var ranked = near.Select(l => (l, d: Geo.DistanceKm(lat, lng, l.Latitude, l.Longitude))).Where(x => x.d <= radius).OrderBy(x => x.d).ToList();
            return new(ranked.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).Select(x => x.l.ToDto(x.d)).ToList(), ranked.Count, f.Page, f.PageSize);
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(l => l.IsFeatured).ThenByDescending(l => l.RatingAverage).ThenBy(l => l.Name)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
        return new(items.Select(l => l.ToDto()).ToList(), total, f.Page, f.PageSize);
    }

    private static IQueryable<Listing> Filter(IQueryable<Listing> q, ListingQuery f)
    {
        if (f.Kind is "help") q = q.Where(l => l.Category!.Kind == CategoryKind.Help);
        else if (f.Kind is "business") q = q.Where(l => l.Category!.Kind == CategoryKind.Business);
        if (!string.IsNullOrWhiteSpace(f.Category)) q = q.Where(l => l.Category!.Slug == f.Category);
        if (!string.IsNullOrWhiteSpace(f.District)) q = q.Where(l => l.District == f.District);
        if (f.Featured == true) q = q.Where(l => l.IsFeatured);
        if (f.Promo == true) { var today = Map.Today; q = q.Where(l => l.PromoTitle != "" && (l.PromoUntil == null || l.PromoUntil >= today)); }
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var like = "%" + f.Q.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
            q = q.Where(l => EF.Functions.Like(l.Name, like) || EF.Functions.Like(l.Town, like) || EF.Functions.Like(l.Category!.Name, like));
        }
        return q;
    }

    public async Task<ListingDto> GetPublicAsync(int id, CancellationToken ct = default) =>
        (await Public().FirstOrDefaultAsync(l => l.Id == id, ct))?.ToDto() ?? throw RuleException.NotFound("Listing");

    /// <summary>The other live listings of the same supplier, shown as "More from this business".</summary>
    public async Task<IReadOnlyList<ListingDto>> MoreFromSupplierAsync(int? supplierId, int exceptId, CancellationToken ct = default) =>
        supplierId is null ? [] : (await Public().Where(l => l.SupplierId == supplierId && l.Id != exceptId).OrderBy(l => l.Id).Take(24).ToListAsync(ct)).Select(l => l.ToDto()).ToList();

    public async Task<IReadOnlyList<string>> DistrictsAsync(CancellationToken ct = default) =>
        await Public().Select(l => l.District).Distinct().OrderBy(d => d).ToListAsync(ct);

    /// <summary>Counts a view, call or direction request for the supplier's results page.</summary>
    public async Task TrackAsync(int id, string what, CancellationToken ct = default)
    {
        var supplierId = await Public().Where(l => l.Id == id).Select(l => l.SupplierId).FirstOrDefaultAsync(ct);
        if (supplierId is null) return;
        var q = db.Suppliers.Where(s => s.Id == supplierId);
        _ = what switch
        {
            "call" => await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.Calls, x => x.Calls + 1), ct),
            "directions" => await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.Directions, x => x.Directions + 1), ct),
            _ => await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.Views, x => x.Views + 1), ct)
        };
    }

    // ----- staff -----

    public async Task<PagedResult<ListingDto>> AdminListAsync(string? kind, int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.Listings.AsNoTracking().Include(l => l.Category).AsQueryable();
        if (kind is "help") q = q.Where(l => l.Category!.Kind == CategoryKind.Help);
        else if (kind is "business") q = q.Where(l => l.Category!.Kind == CategoryKind.Business);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(l => l.Category!.SortOrder).ThenBy(l => l.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(l => l.ToDto()).ToList(), total, page, pageSize);
    }

    /// <summary>Staff add help points (banks, SOS points, police stations and so on). They go live at once.</summary>
    public async Task<ListingDto> CreateHelpPointAsync(ListingInput input, CancellationToken ct = default)
    {
        var cat = await db.Categories.FindAsync([input.CategoryId], ct) ?? throw RuleException.NotFound("Category");
        if (cat.Kind != CategoryKind.Help) throw new RuleException("Business listings are created by suppliers. Choose a help point category.");
        var l = new Listing { Category = cat, Status = ListingStatus.Published, IsVerified = true };
        Apply(l, input, cat);
        db.Listings.Add(l);
        audit.Add($"Added help point \"{l.Name}\"", nameof(Listing));
        await db.SaveChangesAsync(ct);
        return l.ToDto();
    }

    public async Task<ListingDto> UpdateAsync(int id, ListingInput input, CancellationToken ct = default)
    {
        var l = await db.Listings.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw RuleException.NotFound("Listing");
        var cat = await db.Categories.FindAsync([input.CategoryId], ct) ?? throw RuleException.NotFound("Category");
        if (cat.Kind != l.Category!.Kind) throw new RuleException("A listing cannot move between help points and businesses.");
        Apply(l, input, cat);
        audit.Add($"Edited listing \"{l.Name}\"", nameof(Listing), l.Id);
        await db.SaveChangesAsync(ct);
        return l.ToDto();
    }

    public async Task SetVisibleAsync(int id, bool visible, CancellationToken ct = default)
    {
        var l = await db.Listings.FindAsync([id], ct) ?? throw RuleException.NotFound("Listing");
        if (visible && l.Status != ListingStatus.Hidden) throw new RuleException("Only a hidden listing can be shown again.");
        if (!visible && l.Status != ListingStatus.Published) throw new RuleException("Only a live listing can be hidden.");
        l.Status = visible ? ListingStatus.Published : ListingStatus.Hidden;
        l.UpdatedUtc = clock.UtcNow;
        audit.Add($"{(visible ? "Showed" : "Hid")} listing \"{l.Name}\"", nameof(Listing), l.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetFeaturedAsync(int id, bool featured, CancellationToken ct = default)
    {
        var l = await db.Listings.FindAsync([id], ct) ?? throw RuleException.NotFound("Listing");
        l.IsFeatured = featured;
        audit.Add($"{(featured ? "Featured" : "Unfeatured")} \"{l.Name}\"", nameof(Listing), l.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteHelpPointAsync(int id, CancellationToken ct = default)
    {
        var l = await db.Listings.FindAsync([id], ct) ?? throw RuleException.NotFound("Listing");
        if (l.SupplierId is not null) throw new RuleException("A supplier listing cannot be deleted. Hide it instead.");
        db.Listings.Remove(l);
        audit.Add($"Deleted help point \"{l.Name}\"", nameof(Listing), l.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Staff take a promotion down, for example when it breaks the rules.</summary>
    public async Task RemovePromotionAsync(int id, CancellationToken ct = default)
    {
        var l = await db.Listings.FindAsync([id], ct) ?? throw RuleException.NotFound("Listing");
        l.PromoTitle = ""; l.PromoText = ""; l.PromoUntil = null; l.UpdatedUtc = clock.UtcNow;
        audit.Add($"Removed the promotion on \"{l.Name}\"", nameof(Listing), l.Id);
        await db.SaveChangesAsync(ct);
    }

    internal void Apply(Listing l, ListingInput input, Category cat)
    {
        l.Name = input.Name.Trim();
        l.CategoryId = cat.Id;
        l.Category = cat;
        l.Town = input.Town.Trim();
        l.District = input.District.Trim();
        l.Phone = input.Phone?.Trim() ?? "";
        l.Hours = input.Hours?.Trim() ?? "";
        l.Description = input.Description?.Trim() ?? "";
        if (input.Latitude is double lat && input.Longitude is double lng) { l.Latitude = lat; l.Longitude = lng; }
        else if (l.Latitude == 0 && Geo.Districts.TryGetValue(l.District, out var c)) { l.Latitude = c.Lat; l.Longitude = c.Lng; }
        l.UpdatedUtc = clock.UtcNow;
    }
}
