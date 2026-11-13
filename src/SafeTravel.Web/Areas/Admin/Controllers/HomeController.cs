using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeTravel.Web.Areas.Admin.Controllers;

/// <summary>Admin Portal (Internal). The page is a shell; every screen reads and writes through /api/v1/admin.</summary>
[Area("Admin"), Authorize("Staff")]
public class HomeController : Controller
{
    public IActionResult Index() => View();
}
