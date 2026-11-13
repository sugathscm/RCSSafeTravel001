using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using SafeTravel.Core.Services;
using SafeTravel.Web.Infrastructure;

namespace SafeTravel.Web.Api;

/// <summary>Admin Portal (Supplier): a business registers, keeps its listing up to date and pays the annual fee.</summary>
[Route("api/v1")]
public class SupplierApi(SupplierService suppliers, SettingsService settings, IOutputCacheStore cache, IConfiguration config, IWebHostEnvironment env) : ApiBase
{
    private static readonly Dictionary<string, byte[][]> Signatures = new()
    {
        [".pdf"] = ["%PDF"u8.ToArray()],
        [".png"] = [[0x89, 0x50, 0x4E, 0x47]],
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]]
    };

    private int Me => User.SupplierId() ?? throw new RuleException("This account is not linked to a business.", 403);
    private string UploadRoot => Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Uploads:Path"] ?? "App_Data/uploads"));

    [HttpPost("suppliers/register"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<SupplierDto>> Register(SupplierRegistration input, CancellationToken ct) =>
        CreatedAtAction(nameof(Overview), await suppliers.RegisterAsync(input, ct));

    [HttpGet("supplier/me"), Authorize("Supplier")]
    public async Task<IActionResult> Overview(CancellationToken ct) =>
        Ok(new { supplier = await suppliers.GetAsync(Me, ct), annualFee = await settings.AnnualFeeAsync(ct), reminderDays = SupplierService.ReminderDays });

    [HttpPut("supplier/me/listing"), Authorize("Supplier")]
    public async Task<SupplierDto> UpdateListing(ListingInput input, CancellationToken ct)
    {
        var dto = await suppliers.UpdateListingAsync(Me, input, ct);
        await cache.EvictAsync(CacheTags.Listings);
        return dto;
    }

    /// <summary>Adds another listing, for example a hotel adds its restaurant.</summary>
    [HttpPost("supplier/me/listings"), Authorize("Supplier"), EnableRateLimiting("write")]
    public Task<SupplierDto> AddListing(ListingInput input, CancellationToken ct) => Changed(suppliers.AddListingAsync(Me, input, ct));

    [HttpPut("supplier/me/listings/{id:int}"), Authorize("Supplier")]
    public Task<SupplierDto> UpdateOneListing(int id, ListingInput input, CancellationToken ct) => Changed(suppliers.UpdateListingAsync(Me, input, ct, id));

    [HttpDelete("supplier/me/listings/{id:int}"), Authorize("Supplier")]
    public Task<SupplierDto> RemoveListing(int id, CancellationToken ct) => Changed(suppliers.RemoveListingAsync(Me, id, ct));

    /// <summary>Starts or changes the promotion on a listing. Travellers see a red badge on the tile.</summary>
    [HttpPut("supplier/me/listings/{id:int}/promotion"), Authorize("Supplier")]
    public Task<SupplierDto> SetPromotion(int id, PromotionInput input, CancellationToken ct) => Changed(suppliers.SetPromotionAsync(Me, id, input, ct));

    [HttpDelete("supplier/me/listings/{id:int}/promotion"), Authorize("Supplier")]
    public Task<SupplierDto> EndPromotion(int id, CancellationToken ct) => Changed(suppliers.SetPromotionAsync(Me, id, null, ct));

    // ----- products under a listing -----
    [HttpGet("supplier/me/listings/{id:int}/products"), Authorize("Supplier")]
    public Task<IReadOnlyList<ProductDto>> Products(int id, [FromServices] ProductService products, CancellationToken ct) => products.MineAsync(Me, id, ct);

    [HttpPost("supplier/me/listings/{id:int}/products"), Authorize("Supplier"), EnableRateLimiting("write")]
    public async Task<ProductDto> AddProduct(int id, ProductInput input, [FromServices] ProductService products, CancellationToken ct)
    { var p = await products.AddAsync(Me, id, input, ct); await cache.EvictAsync(CacheTags.Listings); return p; }

    [HttpPut("supplier/me/products/{id:int}"), Authorize("Supplier")]
    public async Task<ProductDto> UpdateProduct(int id, ProductInput input, [FromServices] ProductService products, CancellationToken ct)
    { var p = await products.UpdateAsync(Me, id, input, ct); await cache.EvictAsync(CacheTags.Listings); return p; }

    [HttpDelete("supplier/me/products/{id:int}"), Authorize("Supplier")]
    public async Task<IActionResult> RemoveProduct(int id, [FromServices] ProductService products, [FromServices] UploadStore uploads, CancellationToken ct)
    { uploads.Delete(await products.RemoveAsync(Me, id, ct)); await cache.EvictAsync(CacheTags.Listings); return NoContent(); }

    /// <summary>Picture of a product. JPG or PNG up to 2 MB.</summary>
    [HttpPost("supplier/me/products/{id:int}/image"), Authorize("Supplier"), RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<ProductDto>> ProductImage(int id, IFormFile file, [FromServices] ProductService products, [FromServices] UploadStore uploads, CancellationToken ct)
    {
        if (file.Length is 0 or > 2 * 1024 * 1024) return Problem("Choose a picture up to 2 MB.", statusCode: 400);
        var stored = await uploads.SaveAsync(file, "products", [".jpg", ".jpeg", ".png"], ct);
        if (stored is null) return Problem("Upload a JPG or PNG picture.", statusCode: 400);
        try
        {
            var (p, old) = await products.SetImageAsync(Me, id, stored, ct);
            uploads.Delete(old);
            await cache.EvictAsync(CacheTags.Listings);
            return p;
        }
        catch { uploads.Delete(stored); throw; }
    }

    private async Task<SupplierDto> Changed(Task<SupplierDto> change)
    {
        var dto = await change;
        await cache.EvictAsync(CacheTags.Listings);
        return dto;
    }

    /// <summary>Uploads a verification document. PDF, JPG or PNG up to 5 MB. Files are stored outside the web root.</summary>
    [HttpPost("supplier/me/documents"), Authorize("Supplier"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm, Required, StringLength(120)] string name, IFormFile file, CancellationToken ct)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length is 0 or > 5 * 1024 * 1024) return Problem("Choose a file up to 5 MB.", statusCode: 400);
        if (!Signatures.TryGetValue(ext, out var magic)) return Problem("Upload a PDF, JPG or PNG file.", statusCode: 400);

        await using var input = file.OpenReadStream();
        var head = new byte[8];
        var read = await input.ReadAsync(head, ct);
        if (!magic.Any(m => read >= m.Length && head.AsSpan(0, m.Length).SequenceEqual(m))) return Problem("That file is not a real PDF, JPG or PNG.", statusCode: 400);
        input.Position = 0;

        var dir = Path.Combine(UploadRoot, Me.ToString());
        Directory.CreateDirectory(dir);
        var stored = Guid.NewGuid().ToString("N") + ext;
        await using (var output = System.IO.File.Create(Path.Combine(dir, stored))) await input.CopyToAsync(output, ct);
        return await suppliers.AddDocumentAsync(Me, name.Trim(), Path.GetFileName(file.FileName), Path.Combine(Me.ToString(), stored), file.Length, ct);
    }

    /// <summary>Downloads a document. Suppliers see only their own; staff who manage suppliers see all.</summary>
    [HttpGet("documents/{id:int}"), Authorize]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var staff = User.IsInRole(SafeTravel.Core.Entities.Roles.SuperAdmin) || User.IsInRole(SafeTravel.Core.Entities.Roles.SupplierManager);
        if (!staff && User.SupplierId() is null) return Forbid();
        var d = await suppliers.GetDocumentAsync(id, staff ? null : User.SupplierId(), ct);
        var path = Path.GetFullPath(Path.Combine(UploadRoot, d.StoragePath));
        if (!path.StartsWith(UploadRoot, StringComparison.Ordinal) || !System.IO.File.Exists(path)) return NotFound();
        var type = Path.GetExtension(path) switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" };
        return PhysicalFile(path, type, d.FileName);
    }

    [HttpPost("supplier/me/pay"), Authorize("Supplier"), EnableRateLimiting("write")]
    public async Task<PaymentDto> Pay(CancellationToken ct)
    {
        var p = await suppliers.PayAnnualFeeAsync(Me, ct);
        await cache.EvictAsync(CacheTags.Listings);
        return p;
    }

    [HttpGet("supplier/me/payments"), Authorize("Supplier")]
    public Task<IReadOnlyList<PaymentDto>> Payments(CancellationToken ct) => suppliers.PaymentsAsync(Me, ct);
}
