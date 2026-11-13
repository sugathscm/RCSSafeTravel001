using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;
using SafeTravel.Core.Services;
using SafeTravel.Web.Infrastructure;

namespace SafeTravel.Web.Api;

public record OnInput(bool On);
public record ReasonInput([StringLength(400)] string? Reason);
public record FeeInput([Range(0, 1_000_000)] decimal Fee);
public record UserDto(int Id, string Name, string Email, string Role, bool TwoFactor, bool Active);

public class StaffInput
{
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
    [Required] public string Role { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 10)] public string Password { get; set; } = "";
}

/// <summary>Admin Portal (Internal): staff control everything the Web Portal and the apps show.</summary>
[Route("api/v1/admin"), Authorize("Staff")]
public class AdminApi(DashboardService dashboard, SupplierService suppliers, ListingService listings, ContentService content, AlertService alerts,
    PaymentService payments, AuditService audit, IOutputCacheStore cache, UserManager<AppUser> users, AppDbContext db, TokenService tokens) : ApiBase
{
    private static (int, int) Paging(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 200));

    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard(CancellationToken ct) => dashboard.GetAsync(ct);

    // ----- suppliers -----
    [HttpGet("suppliers"), Authorize("SuppliersRead")]
    public Task<PagedResult<SupplierDto>> Suppliers(string? status, CancellationToken ct, int page = 1, int pageSize = 50)
    { (page, pageSize) = Paging(page, pageSize); return suppliers.ListAsync(status, page, pageSize, ct); }

    [HttpPost("suppliers/{id:int}/approve"), Authorize("Suppliers")]
    public async Task<IActionResult> Approve(int id, CancellationToken ct) { await suppliers.ApproveAsync(id, ct); await cache.EvictAsync(CacheTags.Listings); return NoContent(); }

    [HttpPost("suppliers/{id:int}/reject"), Authorize("Suppliers")]
    public async Task<IActionResult> Reject(int id, ReasonInput input, CancellationToken ct) { await suppliers.RejectAsync(id, input.Reason, ct); await cache.EvictAsync(CacheTags.Listings); return NoContent(); }

    [HttpPost("suppliers/{id:int}/remind"), Authorize("SuppliersRead")]
    public async Task<IActionResult> Remind(int id, CancellationToken ct) { await suppliers.SendReminderAsync(id, ct); return NoContent(); }

    // ----- listings and categories -----
    [HttpGet("listings"), Authorize("SuppliersRead")]
    public Task<PagedResult<ListingDto>> Listings(string? kind, CancellationToken ct, int page = 1, int pageSize = 100)
    { (page, pageSize) = Paging(page, pageSize); return listings.AdminListAsync(kind, page, pageSize, ct); }

    [HttpPost("listings"), Authorize("Suppliers")]
    public async Task<ListingDto> AddHelpPoint(ListingInput input, CancellationToken ct) { var l = await listings.CreateHelpPointAsync(input, ct); await cache.EvictAsync(CacheTags.Listings); return l; }

    [HttpPut("listings/{id:int}"), Authorize("Suppliers")]
    public async Task<ListingDto> EditListing(int id, ListingInput input, CancellationToken ct) { var l = await listings.UpdateAsync(id, input, ct); await cache.EvictAsync(CacheTags.Listings); return l; }

    [HttpPut("listings/{id:int}/visible"), Authorize("Suppliers")]
    public async Task<IActionResult> Visible(int id, OnInput input, CancellationToken ct) { await listings.SetVisibleAsync(id, input.On, ct); await cache.EvictAsync(CacheTags.Listings); return NoContent(); }

    [HttpPut("listings/{id:int}/featured"), Authorize("Suppliers")]
    public async Task<IActionResult> Featured(int id, OnInput input, CancellationToken ct) { await listings.SetFeaturedAsync(id, input.On, ct); await cache.EvictAsync(CacheTags.Listings); return NoContent(); }

    [HttpDelete("listings/{id:int}/promotion"), Authorize("Suppliers")]
    public async Task<IActionResult> RemovePromotion(int id, CancellationToken ct) { await listings.RemovePromotionAsync(id, ct); await cache.EvictAsync(CacheTags.Listings); return NoContent(); }

    [HttpDelete("listings/{id:int}"), Authorize("Suppliers")]
    public async Task<IActionResult> DeleteListing(int id, CancellationToken ct) { await listings.DeleteHelpPointAsync(id, ct); await cache.EvictAsync(CacheTags.Listings); return NoContent(); }

    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken ct) => content.CategoriesAsync(false, ct);

    [HttpPost("categories"), Authorize("Suppliers")]
    public async Task<CategoryDto> AddCategory(CategoryInput input, CancellationToken ct) { var c = await content.AddCategoryAsync(input, ct); await cache.EvictAsync(CacheTags.Listings); return c; }

    [HttpPut("categories/{id:int}/active"), Authorize("Suppliers")]
    public async Task<IActionResult> CategoryActive(int id, OnInput input, CancellationToken ct) { await content.SetCategoryActiveAsync(id, input.On, ct); await cache.EvictAsync(CacheTags.Listings); return NoContent(); }

    // ----- Web Portal content -----
    [HttpPut("content/hero"), Authorize("Content")]
    public async Task<IActionResult> Hero(HeroInput input, CancellationToken ct) { await content.SetHeroAsync(input, ct); await cache.EvictAsync(CacheTags.Content); return NoContent(); }

    [HttpPut("content/sections/{section}"), Authorize("Content")]
    public async Task<IActionResult> Section(string section, OnInput input, CancellationToken ct) { await content.SetSectionAsync(section, input.On, ct); await cache.EvictAsync(CacheTags.Content); return NoContent(); }

    [HttpPut("content/emergency-numbers"), Authorize("Content")]
    public async Task<IActionResult> Numbers(List<NumberInput> numbers, CancellationToken ct) { await content.SetNumbersAsync(numbers, ct); await cache.EvictAsync(CacheTags.Content); return NoContent(); }

    [HttpPost("content/topics"), Authorize("Content")]
    public async Task<TopicDto> AddTopic(TopicInput input, CancellationToken ct) { var t = await content.AddTopicAsync(input, ct); await cache.EvictAsync(CacheTags.Content); return t; }

    [HttpDelete("content/topics/{id:int}"), Authorize("Content")]
    public async Task<IActionResult> RemoveTopic(int id, CancellationToken ct) { await content.RemoveTopicAsync(id, ct); await cache.EvictAsync(CacheTags.Content); return NoContent(); }

    [HttpGet("sponsors")]
    public Task<IReadOnlyList<SponsorDto>> Sponsors(CancellationToken ct) => content.SponsorsAsync(ct);

    [HttpPost("sponsors"), Authorize("Content")]
    public async Task<SponsorDto> AddSponsor(SponsorInput input, CancellationToken ct) { var s = await content.AddSponsorAsync(input, ct); await cache.EvictAsync(CacheTags.Content); return s; }

    [HttpPut("sponsors/{id:int}/active"), Authorize("Content")]
    public async Task<IActionResult> SponsorActive(int id, OnInput input, CancellationToken ct) { await content.SetSponsorActiveAsync(id, input.On, ct); await cache.EvictAsync(CacheTags.Content); return NoContent(); }

    // ----- advertisements -----
    [HttpGet("ads")]
    public Task<IReadOnlyList<AdDto>> Ads(CancellationToken ct) => content.AdsAsync(false, ct);

    [HttpPut("ads/{slot:int}"), Authorize("Content")]
    public async Task<AdDto> SaveAd(int slot, AdInput input, CancellationToken ct) { var a = await content.SaveAdAsync(slot, input, ct); await cache.EvictAsync(CacheTags.Content); return a; }

    /// <summary>Picture for an advertisement. JPG or PNG up to 2 MB.</summary>
    [HttpPost("ads/{slot:int}/image"), Authorize("Content"), RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<AdDto>> AdImage(int slot, IFormFile file, [FromServices] UploadStore uploads, CancellationToken ct)
    {
        if (file.Length is 0 or > 2 * 1024 * 1024) return Problem("Choose a picture up to 2 MB.", statusCode: 400);
        var stored = await uploads.SaveAsync(file, "ads", [".jpg", ".jpeg", ".png"], ct);
        if (stored is null) return Problem("Upload a JPG or PNG picture.", statusCode: 400);
        var (ad, old) = await content.SetAdImageAsync(slot, stored, ct);
        uploads.Delete(old);
        await cache.EvictAsync(CacheTags.Content);
        return ad;
    }

    [HttpDelete("ads/{slot:int}/image"), Authorize("Content")]
    public async Task<AdDto> RemoveAdImage(int slot, [FromServices] UploadStore uploads, CancellationToken ct)
    {
        var (ad, old) = await content.SetAdImageAsync(slot, null, ct);
        uploads.Delete(old);
        await cache.EvictAsync(CacheTags.Content);
        return ad;
    }

    // ----- alerts -----
    [HttpGet("alerts")]
    public Task<IReadOnlyList<AlertDto>> Alerts(CancellationToken ct) => alerts.AllAsync(ct);

    [HttpPost("alerts"), Authorize("Alerts")]
    public async Task<AlertDto> Publish(AlertInput input, CancellationToken ct) { var a = await alerts.PublishAsync(input, ct); await cache.EvictAsync(CacheTags.Alerts); return a; }

    [HttpPut("alerts/{id:int}/active"), Authorize("Alerts")]
    public async Task<IActionResult> AlertActive(int id, OnInput input, CancellationToken ct) { await alerts.SetActiveAsync(id, input.On, ct); await cache.EvictAsync(CacheTags.Alerts); return NoContent(); }

    // ----- payments -----
    [HttpGet("payments"), Authorize("Finance")]
    public async Task<IActionResult> Payments(CancellationToken ct, int page = 1, int pageSize = 50)
    { (page, pageSize) = Paging(page, pageSize); return Ok(new { summary = await payments.SummaryAsync(ct), payments = await payments.ListAsync(page, pageSize, ct) }); }

    [HttpPut("settings/annual-fee"), Authorize("Finance")]
    public async Task<IActionResult> Fee(FeeInput input, CancellationToken ct) { await content.SetAnnualFeeAsync(input.Fee, ct); await cache.EvictAsync(CacheTags.Content); return NoContent(); }

    // ----- users and audit -----
    [HttpGet("users"), Authorize("Admin")]
    public async Task<IReadOnlyList<UserDto>> Users(CancellationToken ct)
    {
        var rows = await (from u in db.Users.AsNoTracking()
                          join ur in db.UserRoles on u.Id equals ur.UserId
                          join r in db.Roles on ur.RoleId equals r.Id
                          where Roles.Staff.Contains(r.Name!)
                          orderby u.Id
                          select new UserDto(u.Id, u.FullName, u.Email!, r.Name!, u.TwoFactorEnabled, u.IsActive)).ToListAsync(ct);
        return rows;
    }

    [HttpPost("users"), Authorize("Admin")]
    public async Task<ActionResult<UserDto>> AddUser(StaffInput input, CancellationToken ct)
    {
        if (!Roles.Staff.Contains(input.Role)) return Problem("Choose a staff role.", statusCode: 400);
        var u = new AppUser { UserName = input.Email.Trim(), Email = input.Email.Trim(), FullName = input.FullName.Trim(), EmailConfirmed = true };
        var created = await users.CreateAsync(u, input.Password);
        if (!created.Succeeded) return Problem(string.Join(" ", created.Errors.Select(e => e.Description)), statusCode: 400);
        await users.AddToRoleAsync(u, input.Role);
        audit.Add($"Added staff member \"{u.FullName}\" as {input.Role}", nameof(AppUser), u.Id);
        await db.SaveChangesAsync(ct);
        return new UserDto(u.Id, u.FullName, u.Email, input.Role, false, true);
    }

    [HttpPut("users/{id:int}/active"), Authorize("Admin")]
    public async Task<IActionResult> UserActive(int id, OnInput input, CancellationToken ct)
    {
        var u = await users.FindByIdAsync(id.ToString());
        if (u is null) return NotFound();
        if (!input.On && u.Id.ToString() == users.GetUserId(User)) return Problem("You cannot switch off your own account.", statusCode: 400);
        u.IsActive = input.On;
        await users.UpdateAsync(u);
        if (!input.On) { await users.UpdateSecurityStampAsync(u); await tokens.RevokeAllAsync(u.Id, ct); }
        audit.Add($"{(input.On ? "Enabled" : "Disabled")} user \"{u.FullName}\"", nameof(AppUser), u.Id);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("audit"), Authorize("Admin")]
    public Task<PagedResult<AuditDto>> Audit(CancellationToken ct, int page = 1, int pageSize = 100)
    { (page, pageSize) = Paging(page, pageSize); return audit.ListAsync(page, pageSize, ct); }
}
