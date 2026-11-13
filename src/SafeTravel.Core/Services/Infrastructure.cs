using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

/// <summary>Who is making the current request. Implemented by the web host.</summary>
public interface ICurrentUser
{
    int? Id { get; }
    string Name { get; }
    string? Ip { get; }
}

public interface IClock { DateTime UtcNow { get; } DateOnly Today { get; } }

public class SystemClock : IClock
{
    // Sri Lanka is UTC+05:30 all year. Renewal dates are judged on the local day.
    private static readonly TimeSpan Offset = TimeSpan.FromMinutes(330);
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow + Offset);
}

public class AuditService(AppDbContext db, ICurrentUser user)
{
    /// <summary>Adds an audit entry to the current unit of work. It is saved with the change it describes.</summary>
    public void Add(string action, string? entity = null, object? entityId = null, string? actorName = null)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            UserId = user.Id,
            UserName = actorName ?? user.Name,
            Action = action.Length > 400 ? action[..400] : action,
            Entity = entity,
            EntityId = entityId?.ToString(),
            Ip = user.Ip
        });
    }

    public async Task<PagedResult<AuditDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var q = db.AuditEntries.AsNoTracking().OrderByDescending(a => a.Id);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).Select(a => new AuditDto(a.Id, a.WhenUtc, a.UserName, a.Action)).ToListAsync(ct);
        return new(items, total, page, pageSize);
    }
}

public class SettingsService(AppDbContext db, IMemoryCache cache)
{
    private const string CacheKey = "site-settings";

    public async Task<IReadOnlyDictionary<string, string>> AllAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(CacheKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await db.SiteSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        }))!;

    public async Task<string> GetAsync(string key, string fallback = "", CancellationToken ct = default) =>
        (await AllAsync(ct)).TryGetValue(key, out var v) ? v : fallback;

    public async Task<decimal> AnnualFeeAsync(CancellationToken ct = default) =>
        decimal.TryParse(await GetAsync(SettingKeys.AnnualFee, "1000", ct), System.Globalization.CultureInfo.InvariantCulture, out var fee) ? fee : 1000m;

    /// <summary>Stages a setting change. The caller saves it together with its audit entry.</summary>
    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var row = await db.SiteSettings.FindAsync([key], ct);
        if (row is null) db.SiteSettings.Add(new SiteSetting { Key = key, Value = value });
        else row.Value = value;
        cache.Remove(CacheKey);
    }

    public void Invalidate() => cache.Remove(CacheKey);
}

/// <summary>Sends email, SMS and app notifications. The default writes to the log until providers are connected.</summary>
public interface INotificationSender
{
    Task SendAsync(string channel, string to, string subject, string body, CancellationToken ct = default);
}

public class LogNotificationSender(ILogger<LogNotificationSender> log) : INotificationSender
{
    public Task SendAsync(string channel, string to, string subject, string body, CancellationToken ct = default)
    {
        log.LogInformation("Notification [{Channel}] to {To}: {Subject}", channel, to, subject);
        return Task.CompletedTask;
    }
}

public record GatewayResult(bool Paid, string Reference, string? RedirectUrl = null);

/// <summary>Payment gateway. Swap the sandbox for the real provider (for example PayHere) once the merchant account is open.</summary>
public interface IPaymentGateway
{
    string Name { get; }
    Task<GatewayResult> ChargeAsync(Payment payment, CancellationToken ct = default);
}

/// <summary>Development gateway: approves every payment at once and takes no money.</summary>
public class SandboxPaymentGateway : IPaymentGateway
{
    public string Name => "Sandbox";
    public Task<GatewayResult> ChargeAsync(Payment payment, CancellationToken ct = default) =>
        Task.FromResult(new GatewayResult(true, "SBX-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()));
}

public static class Geo
{
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6371;
        double dLat = (lat2 - lat1) * Math.PI / 180, dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>District centres, used to place a listing on the map when no coordinates are given.</summary>
    public static readonly IReadOnlyDictionary<string, (double Lat, double Lng)> Districts = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
    {
        ["Ampara"] = (7.30, 81.67), ["Anuradhapura"] = (8.31, 80.40), ["Badulla"] = (6.99, 81.05), ["Batticaloa"] = (7.72, 81.69),
        ["Colombo"] = (6.93, 79.86), ["Galle"] = (6.05, 80.22), ["Gampaha"] = (7.09, 79.99), ["Hambantota"] = (6.12, 81.12),
        ["Jaffna"] = (9.66, 80.02), ["Kalutara"] = (6.58, 79.96), ["Kandy"] = (7.29, 80.63), ["Kegalle"] = (7.25, 80.35),
        ["Kilinochchi"] = (9.38, 80.40), ["Kurunegala"] = (7.49, 80.36), ["Mannar"] = (8.98, 79.90), ["Matale"] = (7.47, 80.62),
        ["Matara"] = (5.95, 80.54), ["Monaragala"] = (6.87, 81.35), ["Mullaitivu"] = (9.27, 80.81), ["Nuwara Eliya"] = (6.95, 80.79),
        ["Polonnaruwa"] = (7.94, 81.00), ["Puttalam"] = (8.03, 79.83), ["Ratnapura"] = (6.68, 80.40), ["Trincomalee"] = (8.57, 81.23),
        ["Vavuniya"] = (8.75, 80.50)
    };
}
