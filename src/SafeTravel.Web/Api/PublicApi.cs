using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using SafeTravel.Core.Entities;
using SafeTravel.Core.Services;
using SafeTravel.Web.Infrastructure;

namespace SafeTravel.Web.Api;

[ApiController, ServiceFilter(typeof(ApiAntiforgeryFilter))]
public abstract class ApiBase : ControllerBase;

/// <summary>Open endpoints read by the Web Portal and the Android and iOS apps.</summary>
[Route("api/v1"), AllowAnonymous]
public class PublicApi(ContentService content, ListingService listings, AlertService alerts, PaymentService payments) : ApiBase
{
    [HttpGet("content/home"), OutputCache(PolicyName = "content")]
    public Task<HomeContentDto> Home(CancellationToken ct) => content.HomeAsync(ct);

    [HttpGet("categories"), OutputCache(PolicyName = "listings")]
    public Task<IReadOnlyList<CategoryDto>> Categories(CancellationToken ct) => content.CategoriesAsync(true, ct);

    [HttpGet("districts"), OutputCache(PolicyName = "listings")]
    public Task<IReadOnlyList<string>> Districts(CancellationToken ct) => listings.DistrictsAsync(ct);

    [HttpGet("listings"), OutputCache(PolicyName = "listings")]
    public Task<PagedResult<ListingDto>> Listings([FromQuery] ListingQuery query, CancellationToken ct) => listings.SearchAsync(query, ct);

    [HttpGet("listings/{id:int}"), OutputCache(PolicyName = "listings")]
    public Task<ListingDto> Listing(int id, CancellationToken ct) => listings.GetPublicAsync(id, ct);

    /// <summary>Products and prices of a listing: rooms, dishes, tours and so on.</summary>
    [HttpGet("listings/{id:int}/products"), OutputCache(PolicyName = "listings")]
    public Task<IReadOnlyList<ProductDto>> Products(int id, [FromServices] ProductService products, CancellationToken ct) => products.PublicAsync(id, ct);

    /// <summary>The approved traveller reviews of a listing, newest first.</summary>
    [HttpGet("listings/{id:int}/reviews"), OutputCache(PolicyName = "listings")]
    public Task<IReadOnlyList<ReviewDto>> Reviews(int id, [FromServices] ReviewService reviews, CancellationToken ct) => reviews.ForListingAsync(id, ct);

    [HttpGet("products/{id:int}/image"), ResponseCache(Duration = 86400)]
    public async Task<IActionResult> ProductImage(int id, [FromServices] ProductService products, [FromServices] UploadStore uploads, CancellationToken ct)
    {
        var path = uploads.Resolve(await products.ImagePathAsync(id, ct));
        return path is null ? NotFound() : PhysicalFile(path, path.EndsWith(".png") ? "image/png" : "image/jpeg");
    }

    /// <summary>Counts a view, a call or a direction request so suppliers can see their results.</summary>
    [HttpPost("listings/{id:int}/track/{what:regex(^(view|call|directions)$)}"), EnableRateLimiting("write")]
    public async Task<IActionResult> Track(int id, string what, CancellationToken ct) { await listings.TrackAsync(id, what, ct); return NoContent(); }

    [HttpGet("alerts"), OutputCache(PolicyName = "alerts")]
    public Task<IReadOnlyList<AlertDto>> Alerts(CancellationToken ct) => alerts.ActiveAsync(ct);

    /// <summary>Safety topics with their do and avoid lists.</summary>
    [HttpGet("topics"), OutputCache(PolicyName = "content")]
    public Task<IReadOnlyList<TopicDetailDto>> Topics([FromServices] SafetyContentService safety, CancellationToken ct) => safety.TopicsAsync(true, ct);

    [HttpGet("topics/{id:int}"), OutputCache(PolicyName = "content")]
    public Task<TopicDetailDto> Topic(int id, [FromServices] SafetyContentService safety, CancellationToken ct) => safety.TopicAsync(id, ct);

    /// <summary>First aid guides: steps in order and, where one is set, the video player address.</summary>
    [HttpGet("first-aid"), OutputCache(PolicyName = "content")]
    public Task<IReadOnlyList<FirstAidGuideDto>> FirstAid([FromServices] SafetyContentService safety, CancellationToken ct) => safety.GuidesAsync(true, ct);

    /// <summary>Active risk zones for the safety map: flood and landslide areas, road closures, red-flag beaches.</summary>
    [HttpGet("risk-zones"), OutputCache(PolicyName = "alerts")]
    public Task<IReadOnlyList<RiskZoneDto>> RiskZones([FromServices] MapService map, CancellationToken ct) => map.ZonesAsync(true, ct);

    [HttpGet("emergency-numbers"), OutputCache(PolicyName = "content")]
    public Task<IReadOnlyList<NumberDto>> Numbers(CancellationToken ct) => content.NumbersAsync(ct);

    /// <summary>The sponsor advertisements, up to three, in the order they are shown.</summary>
    [HttpGet("ads"), OutputCache(PolicyName = "content")]
    public Task<IReadOnlyList<AdDto>> Ads(CancellationToken ct) => content.AdsAsync(true, ct);

    [HttpGet("ads/{id:int}/image"), ResponseCache(Duration = 86400)]
    public async Task<IActionResult> AdImage(int id, [FromServices] UploadStore uploads, CancellationToken ct)
    {
        var path = uploads.Resolve(await content.AdImagePathAsync(id, ct));
        return path is null ? NotFound() : PhysicalFile(path, path.EndsWith(".png") ? "image/png" : "image/jpeg");
    }

    [HttpPost("ads/{id:int}/click"), EnableRateLimiting("write")]
    public async Task<IActionResult> AdClick(int id, CancellationToken ct) { await content.TrackAdClickAsync(id, ct); return NoContent(); }

    [HttpPost("donations"), EnableRateLimiting("write")]
    public Task<PaymentDto> Donate(DonationInput input, CancellationToken ct) => payments.DonateAsync(input, ct);
}

public class LoginInput
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    /// <summary>Six-digit code from the authenticator app, when two-step sign-in is on.</summary>
    public string? Code { get; set; }
}

public class RegisterInput
{
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 10)] public string Password { get; set; } = "";
    [Required, StringLength(120)] public string FullName { get; set; } = "";
}

public record RefreshInput([Required] string RefreshToken);
public record CodeInput([Required, RegularExpression(@"^\d{6}$")] string Code);
public record SessionDto(int Id, string Email, string Name, IList<string> Roles, int? SupplierId, bool TwoFactor);
public record LoginResult(string AccessToken, DateTime ExpiresUtc, string RefreshToken, SessionDto User);

/// <summary>Checks a password (and the second step when it is on). Shared by the API and the sign-in page.</summary>
public class SignInChecker(UserManager<AppUser> users, SignInManager<AppUser> signIn)
{
    public enum Outcome { Ok, Wrong, Locked, NeedsCode }

    public async Task<(Outcome, AppUser?)> CheckAsync(string email, string password, string? code)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive) return (Outcome.Wrong, null);
        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut) return (Outcome.Locked, null);
        if (!result.Succeeded) return (Outcome.Wrong, null);
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(code)) return (Outcome.NeedsCode, null);
            var ok = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code.Replace(" ", ""));
            if (!ok) { await users.AccessFailedAsync(user); return (Outcome.Wrong, null); }
        }
        return (Outcome.Ok, user);
    }
}

[Route("api/v1/auth")]
public class AuthApi(UserManager<AppUser> users, SignInChecker checker, TokenService tokens, AuditService audit, SafeTravel.Core.Data.AppDbContext db) : ApiBase
{
    private async Task<SessionDto> Session(AppUser u) => new(u.Id, u.Email!, u.FullName, await users.GetRolesAsync(u), u.SupplierId, u.TwoFactorEnabled);

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResult>> Login(LoginInput input, CancellationToken ct)
    {
        var (outcome, user) = await checker.CheckAsync(input.Email, input.Password, input.Code);
        switch (outcome)
        {
            case SignInChecker.Outcome.Locked: return Problem("Too many wrong attempts. Try again in 15 minutes.", statusCode: 423);
            case SignInChecker.Outcome.NeedsCode: return Problem("Enter the code from your authenticator app.", statusCode: 401, type: "two-factor-required");
            case SignInChecker.Outcome.Wrong: return Problem("The email or password is not right.", statusCode: 401);
        }
        var pair = await tokens.IssueAsync(user!, ct);
        return new LoginResult(pair.AccessToken, pair.ExpiresUtc, pair.RefreshToken, await Session(user!));
    }

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenPair>> Refresh(RefreshInput input, CancellationToken ct) =>
        await tokens.RefreshAsync(input.RefreshToken, ct) is { } pair ? pair : Problem("Sign in again.", statusCode: 401);

    [HttpPost("logout"), AllowAnonymous]
    public async Task<IActionResult> Logout(RefreshInput input, CancellationToken ct) { await tokens.RevokeAsync(input.RefreshToken, ct); return NoContent(); }

    /// <summary>Traveller account, the same sign-in on the Web Portal and in the app.</summary>
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResult>> Register(RegisterInput input, CancellationToken ct)
    {
        var user = new AppUser { UserName = input.Email.Trim(), Email = input.Email.Trim(), FullName = input.FullName.Trim() };
        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded) return Problem(string.Join(" ", created.Errors.Select(e => e.Description)), statusCode: 400);
        await users.AddToRoleAsync(user, Roles.Traveller);
        var pair = await tokens.IssueAsync(user, ct);
        return new LoginResult(pair.AccessToken, pair.ExpiresUtc, pair.RefreshToken, await Session(user));
    }

    [HttpGet("me"), Authorize]
    public async Task<ActionResult<SessionDto>> Me() => await users.GetUserAsync(User) is { } u ? await Session(u) : Unauthorized();

    /// <summary>Starts two-step sign-in: returns the key to add to an authenticator app.</summary>
    [HttpPost("two-factor/setup"), Authorize]
    public async Task<IActionResult> TwoFactorSetup()
    {
        var u = await users.GetUserAsync(User);
        if (u is null) return Unauthorized();
        var key = await users.GetAuthenticatorKeyAsync(u);
        if (string.IsNullOrEmpty(key)) { await users.ResetAuthenticatorKeyAsync(u); key = await users.GetAuthenticatorKeyAsync(u); }
        var uri = $"otpauth://totp/{Uri.EscapeDataString("Safe Travel")}:{Uri.EscapeDataString(u.Email!)}?secret={key}&issuer={Uri.EscapeDataString("Safe Travel")}&digits=6";
        return Ok(new { key, uri, enabled = u.TwoFactorEnabled });
    }

    [HttpPost("two-factor/enable"), Authorize]
    public async Task<IActionResult> TwoFactorEnable(CodeInput input, CancellationToken ct)
    {
        var u = await users.GetUserAsync(User);
        if (u is null) return Unauthorized();
        if (!await users.VerifyTwoFactorTokenAsync(u, users.Options.Tokens.AuthenticatorTokenProvider, input.Code)) return Problem("That code is not right. Check the time on your phone and try again.", statusCode: 400);
        await users.SetTwoFactorEnabledAsync(u, true);
        audit.Add("Turned on two-step sign-in");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
