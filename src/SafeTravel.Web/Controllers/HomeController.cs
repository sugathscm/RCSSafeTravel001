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

    [HttpGet("/topics")]
    public async Task<IActionResult> Topics(CancellationToken ct) => View(await portal.HomeAsync(ct));

    /// <summary>One safety topic: what to do and what to avoid.</summary>
    [HttpGet("/topics/{id:int}")]
    public async Task<IActionResult> Topic(int id, [FromServices] SafeTravel.Core.Services.SafetyContentService safety, CancellationToken ct)
    {
        SafeTravel.Core.Services.TopicDetailDto topic;
        try { topic = await safety.TopicAsync(id, ct); }
        catch (SafeTravel.Core.Services.RuleException) { Response.StatusCode = 404; return View("NotFound"); }
        return View(new TopicPage(topic, (await portal.HomeAsync(ct)).Content.EmergencyNumbers));
    }

    /// <summary>First aid guides. Without an id the first guide is open.</summary>
    [HttpGet("/first-aid"), HttpGet("/first-aid/{id:int}")]
    public async Task<IActionResult> FirstAid(int? id, [FromServices] SafeTravel.Core.Services.SafetyContentService safety, CancellationToken ct)
    {
        var guides = await safety.GuidesAsync(true, ct);
        var guide = id is null ? guides.FirstOrDefault() : guides.FirstOrDefault(g => g.Id == id);
        if (id is not null && guide is null) { Response.StatusCode = 404; return View("NotFound"); }
        var ambulance = (await portal.HomeAsync(ct)).Content.EmergencyNumbers.FirstOrDefault(n => n.Label.Contains("Ambulance", StringComparison.OrdinalIgnoreCase))?.Number;
        return View(new FirstAidPage(guides, guide, ambulance));
    }

    [HttpGet("/emergency")]
    public async Task<IActionResult> Emergency([FromServices] SafeTravel.Core.Services.ListingService listings, CancellationToken ct)
    {
        var home = await portal.HomeAsync(ct);
        var hospitals = (await listings.SearchAsync(new SafeTravel.Core.Services.ListingQuery { Kind = "help", Category = "hospital", PageSize = 8 }, ct)).Items;
        return View(new EmergencyPage(home.Content.EmergencyNumbers, hospitals));
    }

    [HttpGet("/register")]
    public async Task<IActionResult> Register(CancellationToken ct) => View(await portal.HomeAsync(ct));
}
