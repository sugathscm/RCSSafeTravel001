using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeTravel.Web.Infrastructure;

namespace SafeTravel.Web.Controllers;

/// <summary>Web Portal: the public site. Every page is built from what the admin portals have set.</summary>
[AllowAnonymous]
public class HomeController(PortalData portal) : Controller
{
    /// <summary>One listing on its own page: details, promotion, every product with its price, and the supplier's other listings.</summary>
    [HttpGet("/listing/{id:int}")]
    public async Task<IActionResult> Listing(int id, [FromServices] SafeTravel.Core.Services.ListingService listings,
        [FromServices] SafeTravel.Core.Services.ProductService products, CancellationToken ct)
    {
        SafeTravel.Core.Services.ListingDto listing;
        try { listing = await listings.GetPublicAsync(id, ct); }
        catch (SafeTravel.Core.Services.RuleException) { Response.StatusCode = 404; return View("NotFound"); }
        return View(new ListingPage(listing, await products.PublicAsync(id, ct), await listings.MoreFromSupplierAsync(listing.SupplierId, id, ct)));
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await portal.HomeAsync(ct));

    [HttpGet("/find")]
    public async Task<IActionResult> Find(CancellationToken ct) => View(await portal.HomeAsync(ct));

    [HttpGet("/donate")]
    public async Task<IActionResult> Donate(CancellationToken ct) => View(await portal.HomeAsync(ct));

    [HttpGet("/register")]
    public async Task<IActionResult> Register(CancellationToken ct) => View(await portal.HomeAsync(ct));
}
