using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

/// <summary>Products and services a supplier sells under a listing: rooms, dishes, tours, rentals, with a price and a picture.</summary>
public class ProductService(AppDbContext db, AuditService audit)
{
    public const int MaxPerListing = 50;

    /// <summary>What travellers see: switched-on products of a listing that is live.</summary>
    public async Task<IReadOnlyList<ProductDto>> PublicAsync(int listingId, CancellationToken ct = default) =>
        (await db.Products.AsNoTracking()
            .Where(p => p.ListingId == listingId && p.IsActive && p.Listing!.Status == ListingStatus.Published && p.Listing.Category!.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct)).Select(p => p.ToDto()).ToList();

    public async Task<IReadOnlyList<ProductDto>> MineAsync(int supplierId, int listingId, CancellationToken ct = default)
    {
        await OwnListingAsync(supplierId, listingId, ct);
        return (await db.Products.AsNoTracking().Where(p => p.ListingId == listingId).OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct)).Select(p => p.ToDto()).ToList();
    }

    public async Task<ProductDto> AddAsync(int supplierId, int listingId, ProductInput input, CancellationToken ct = default)
    {
        var listing = await OwnListingAsync(supplierId, listingId, ct);
        var count = await db.Products.CountAsync(p => p.ListingId == listingId, ct);
        if (count >= MaxPerListing) throw new RuleException($"A listing can have up to {MaxPerListing} products.");
        var p = new Product { ListingId = listingId, SortOrder = count };
        Apply(p, input);
        db.Products.Add(p);
        audit.Add($"\"{listing.Name}\" added the product \"{p.Name}\"", nameof(Product), actorName: listing.Name);
        await db.SaveChangesAsync(ct);
        return p.ToDto();
    }

    public async Task<ProductDto> UpdateAsync(int supplierId, int productId, ProductInput input, CancellationToken ct = default)
    {
        var p = await OwnAsync(supplierId, productId, ct);
        Apply(p, input);
        await db.SaveChangesAsync(ct);
        return p.ToDto();
    }

    /// <summary>Removes the product. Returns its stored picture so the caller can delete the file.</summary>
    public async Task<string?> RemoveAsync(int supplierId, int productId, CancellationToken ct = default)
    {
        var p = await OwnAsync(supplierId, productId, ct);
        db.Products.Remove(p);
        audit.Add($"\"{p.Listing!.Name}\" removed the product \"{p.Name}\"", nameof(Product), p.Id, p.Listing.Name);
        await db.SaveChangesAsync(ct);
        return p.ImagePath;
    }

    /// <summary>Sets or clears the picture. Returns the old stored file so the caller can delete it.</summary>
    public async Task<(ProductDto Product, string? OldPath)> SetImageAsync(int supplierId, int productId, string? storagePath, CancellationToken ct = default)
    {
        var p = await OwnAsync(supplierId, productId, ct);
        var old = p.ImagePath;
        p.ImagePath = storagePath; p.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (p.ToDto(), old);
    }

    public async Task<string?> ImagePathAsync(int productId, CancellationToken ct = default) =>
        await db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.ImagePath).FirstOrDefaultAsync(ct);

    private static void Apply(Product p, ProductInput input)
    {
        p.Name = input.Name.Trim();
        p.Description = input.Description?.Trim() ?? "";
        p.Price = input.Price is decimal price ? decimal.Round(price, 2) : null;
        p.Unit = input.Unit?.Trim() ?? "";
        p.IsActive = input.IsActive;
        p.UpdatedUtc = DateTime.UtcNow;
    }

    private async Task<Listing> OwnListingAsync(int supplierId, int listingId, CancellationToken ct) =>
        await db.Listings.FirstOrDefaultAsync(l => l.Id == listingId && l.SupplierId == supplierId, ct) ?? throw RuleException.NotFound("Listing");

    private async Task<Product> OwnAsync(int supplierId, int productId, CancellationToken ct) =>
        await db.Products.Include(p => p.Listing).FirstOrDefaultAsync(p => p.Id == productId && p.Listing!.SupplierId == supplierId, ct) ?? throw RuleException.NotFound("Product");
}
