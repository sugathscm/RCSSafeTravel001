using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SafeTravel.Core.Entities;
using SafeTravel.Core.Services;

namespace SafeTravel.Web.Controllers;

/// <summary>Web Portal: the signed-in traveller's account page.</summary>
[Authorize(Roles = Roles.Traveller)]
public class TravellerController(TravellerService travellers, ReviewService reviews, ICurrentUser me, UserManager<AppUser> users, IConfiguration config) : Controller
{
    [HttpGet("/account")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var id = me.Id ?? 0;
        var u = await users.FindByIdAsync(id.ToString());
        return View(new AccountDto(u?.FullName ?? me.Name, u?.Email ?? "", await travellers.ContactsAsync(id, ct), await travellers.TripAsync(id, ct),
            await reviews.ByUserAsync(id, ct), config.GetValue("Features:SafeCheckIn", false)));
    }
}
