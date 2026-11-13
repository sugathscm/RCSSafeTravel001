using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;
using SafeTravel.Core.Services;

namespace SafeTravel.Web.Infrastructure;

public class CurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    private ClaimsPrincipal? User => http.HttpContext?.User;
    public int? Id => int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string Name => User?.FindFirstValue("name") ?? User?.Identity?.Name ?? "Public";
    public string? Ip => http.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

public static class PrincipalExtensions
{
    public static int? SupplierId(this ClaimsPrincipal user) => int.TryParse(user.FindFirstValue("supplier_id"), out var id) ? id : null;
}

/// <summary>Adds the display name and the supplier id to the sign-in, for the cookie and for tokens alike.</summary>
public class AppClaimsFactory(UserManager<AppUser> users, RoleManager<IdentityRole<int>> roles, Microsoft.Extensions.Options.IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole<int>>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var id = await base.GenerateClaimsAsync(user);
        id.AddClaim(new Claim("name", string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "" : user.FullName));
        if (user.SupplierId is int sid) id.AddClaim(new Claim("supplier_id", sid.ToString()));
        return id;
    }
}

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "SafeTravel";
    public string Audience { get; set; } = "SafeTravel.Apps";
    public int AccessMinutes { get; set; } = 15;
    public int RefreshDays { get; set; } = 30;
}

public record TokenPair(string AccessToken, DateTime ExpiresUtc, string RefreshToken);

/// <summary>Short-lived access tokens and rotating refresh tokens for the mobile apps.</summary>
public class TokenService(AppDbContext db, IUserClaimsPrincipalFactory<AppUser> claims, Microsoft.Extensions.Options.IOptions<JwtOptions> options)
{
    private readonly JwtOptions _o = options.Value;

    public static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<TokenPair> IssueAsync(AppUser user, CancellationToken ct = default)
    {
        var principal = await claims.CreateAsync(user);
        var expires = DateTime.UtcNow.AddMinutes(_o.AccessMinutes);
        var jwt = new JwtSecurityToken(_o.Issuer, _o.Audience, principal.Claims, expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key)), SecurityAlgorithms.HmacSha256));
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = Hash(refresh), ExpiresUtc = DateTime.UtcNow.AddDays(_o.RefreshDays) });
        await db.SaveChangesAsync(ct);
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expires, refresh);
    }

    /// <summary>Swaps a refresh token for a new pair. A token works once; reuse of an old one signs the user out everywhere.</summary>
    public async Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var row = await db.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (row?.User is null) return null;
        if (!row.IsActive || !row.User.IsActive)
        {
            if (row.RevokedUtc != null) await RevokeAllAsync(row.UserId, ct);
            return null;
        }
        row.RevokedUtc = DateTime.UtcNow;
        return await IssueAsync(row.User, ct);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        await db.RefreshTokens.Where(r => r.TokenHash == hash && r.RevokedUtc == null).ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedUtc, DateTime.UtcNow), ct);
    }

    public Task RevokeAllAsync(int userId, CancellationToken ct = default) =>
        db.RefreshTokens.Where(r => r.UserId == userId && r.RevokedUtc == null).ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedUtc, DateTime.UtcNow), ct);
}

/// <summary>
/// Browser calls to the API ride on the sign-in cookie, so every change must carry the anti-forgery token.
/// Mobile calls use a bearer token, which a hostile page cannot send, so they are exempt.
/// </summary>
public class ApiAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var req = context.HttpContext.Request;
        if (HttpMethods.IsGet(req.Method) || HttpMethods.IsHead(req.Method) || HttpMethods.IsOptions(req.Method)) return;
        if (req.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return;
        if (context.HttpContext.User.Identity?.IsAuthenticated != true && !req.Cookies.Keys.Any(k => k.StartsWith(".SafeTravel.Auth"))) return;
        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
            context.Result = new ObjectResult(new ProblemDetails { Status = 400, Title = "This page is out of date. Reload it and try again." }) { StatusCode = 400 };
    }
}

public static class CacheTags
{
    public const string Content = "content", Listings = "listings", Alerts = "alerts";

    private static long _version;
    /// <summary>Goes up on every admin change. Page caches include it in their key.</summary>
    public static long Version => Interlocked.Read(ref _version);

    public static async Task EvictAsync(this IOutputCacheStore store, params string[] tags)
    {
        Interlocked.Increment(ref _version);
        foreach (var t in tags) await store.EvictByTagAsync(t, default);
    }
}

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string Csp = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https://*.tile.openstreetmap.org; " +
                               "font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";

    public Task Invoke(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=(), payment=(self)";
        h["Content-Security-Policy"] = Csp;
        return next(ctx);
    }
}

/// <summary>Runs the renewal job: hides lapsed listings and sends the 30, 14, 7 and 0 day reminders.</summary>
public class RenewalWorker(IServiceScopeFactory scopes, ILogger<RenewalWorker> log, IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!config.GetValue("Renewals:Enabled", true)) return;
        var every = TimeSpan.FromHours(Math.Max(1, config.GetValue("Renewals:EveryHours", 6)));
        await Task.Delay(TimeSpan.FromSeconds(20), stop).ContinueWith(_ => { }, TaskScheduler.Default);
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var (lapsed, reminders) = await scope.ServiceProvider.GetRequiredService<SupplierService>().RunRenewalsAsync(stop);
                var store = scope.ServiceProvider.GetRequiredService<IOutputCacheStore>();
                if (lapsed > 0) await store.EvictAsync(CacheTags.Listings);
                log.LogInformation("Renewal job: {Lapsed} listing(s) lapsed, {Reminders} reminder(s) sent", lapsed, reminders);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Renewal job failed"); }
            await Task.Delay(every, stop).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }
}
