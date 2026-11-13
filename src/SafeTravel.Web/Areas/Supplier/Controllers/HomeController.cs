using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SafeTravel.Web.Areas.Supplier.Controllers;

/// <summary>Admin Portal (Supplier). The page is a shell; every screen reads and writes through /api/v1/supplier.</summary>
[Area("Supplier"), Authorize("Supplier")]
public class HomeController : Controller
{
    public IActionResult Index() => View();
}
