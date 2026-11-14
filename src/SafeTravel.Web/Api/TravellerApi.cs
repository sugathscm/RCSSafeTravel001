using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SafeTravel.Core.Entities;
using SafeTravel.Core.Services;
using SafeTravel.Web.Infrastructure;

namespace SafeTravel.Web.Api;

/// <summary>The signed-in traveller's own account: emergency contacts, trip plan, reviews and the "I'm safe" check-in.</summary>
[Route("api/v1/me"), Authorize(Roles = Roles.Traveller)]
public class TravellerApi(TravellerService travellers, ReviewService reviews, ICurrentUser me, UserManager<AppUser> users,
    IConfiguration config, INotificationSender sender) : ApiBase
{
    private int UserId => me.Id ?? throw new RuleException("Your session has ended. Sign in again.", 401);

    /// <summary>"I'm safe" messages need an SMS service. They stay off until Features:SafeCheckIn is set to true.</summary>
    private bool CheckInOn => config.GetValue("Features:SafeCheckIn", false);

    [HttpGet]
    public async Task<AccountDto> Account(CancellationToken ct)
    {
        var u = await users.FindByIdAsync(UserId.ToString());
        return new(u?.FullName ?? me.Name, u?.Email ?? "", await travellers.ContactsAsync(UserId, ct), await travellers.TripAsync(UserId, ct),
            await reviews.ByUserAsync(UserId, ct), CheckInOn);
    }

    [HttpPost("contacts")]
    public Task<ContactDto> AddContact(ContactInput input, CancellationToken ct) => travellers.AddContactAsync(UserId, input, ct);

    [HttpDelete("contacts/{id:int}")]
    public async Task<IActionResult> RemoveContact(int id, CancellationToken ct) { await travellers.RemoveContactAsync(UserId, id, ct); return NoContent(); }

    [HttpPost("trip")]
    public Task<TripStopDto> AddStop(TripStopInput input, CancellationToken ct) => travellers.AddStopAsync(UserId, input, ct);

    [HttpDelete("trip/{id:int}")]
    public async Task<IActionResult> RemoveStop(int id, CancellationToken ct) { await travellers.RemoveStopAsync(UserId, id, ct); return NoContent(); }

    /// <summary>Sends a review for staff to check. It is shown on the listing once approved.</summary>
    [HttpPost("reviews")]
    public async Task<ReviewDto> AddReview(ReviewInput input, CancellationToken ct)
    {
        var u = await users.FindByIdAsync(UserId.ToString());
        return await reviews.SubmitAsync(UserId, u?.FullName ?? me.Name, input, ct);
    }

    /// <summary>Tells every emergency contact that the traveller is safe.</summary>
    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn(CancellationToken ct)
    {
        if (!CheckInOn) throw new RuleException("\"I'm safe\" messages are not switched on yet.");
        var contacts = await travellers.ContactsAsync(UserId, ct);
        if (contacts.Count == 0) throw new RuleException("Add a contact first.");
        var u = await users.FindByIdAsync(UserId.ToString());
        var name = u?.FullName ?? me.Name;
        foreach (var c in contacts)
            await sender.SendAsync("sms", c.Phone, "I'm safe", $"{name} has checked in as safe on Safe Travel Sri Lanka.", ct);
        return Ok(new { sent = contacts.Count });
    }
}

/// <summary>Admin Portal (Internal): traveller reviews waiting to be checked.</summary>
[Route("api/v1/admin"), Authorize("Staff")]
public class ReviewAdminApi(ReviewService reviews, IOutputCacheStore cache) : ApiBase
{
    [HttpGet("reviews")]
    public Task<IReadOnlyList<ReviewDto>> Reviews([FromQuery] string? status, CancellationToken ct) => reviews.AdminListAsync(status, ct);

    [HttpPost("reviews/{id:int}/approve"), Authorize("Content")]
    public async Task<ReviewDto> Approve(int id, CancellationToken ct) { var r = await reviews.DecideAsync(id, true, ct); await cache.EvictAsync(CacheTags.Listings); return r; }

    [HttpPost("reviews/{id:int}/reject"), Authorize("Content")]
    public async Task<ReviewDto> Reject(int id, CancellationToken ct) { var r = await reviews.DecideAsync(id, false, ct); await cache.EvictAsync(CacheTags.Listings); return r; }

    [HttpDelete("reviews/{id:int}"), Authorize("Content")]
    public async Task<IActionResult> Remove(int id, CancellationToken ct) { await reviews.RemoveAsync(id, ct); await cache.EvictAsync(CacheTags.Listings); return NoContent(); }
}
