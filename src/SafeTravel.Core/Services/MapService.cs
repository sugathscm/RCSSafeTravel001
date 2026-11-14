using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

/// <summary>Risk zones on the safety map: areas staff mark as flooded, sliding, closed or unsafe to swim.</summary>
public class MapService(AppDbContext db, AuditService audit)
{
    private static RiskZoneDto Map(RiskZone z) =>
        new(z.Id, z.Type.ToString().ToLowerInvariant(), z.Name, z.Details, z.Source, z.Latitude, z.Longitude, z.RadiusMeters, z.IsActive, z.UpdatedUtc);

    public async Task<IReadOnlyList<RiskZoneDto>> ZonesAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.RiskZones.AsNoTracking();
        if (activeOnly) q = q.Where(z => z.IsActive);
        var list = await q.OrderByDescending(z => z.UpdatedUtc).ThenBy(z => z.Id).Take(500).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<RiskZoneDto> AddAsync(RiskZoneInput input, CancellationToken ct = default)
    {
        var z = new RiskZone();
        Apply(z, input);
        db.RiskZones.Add(z);
        audit.Add($"Added map zone \"{z.Name}\"", nameof(RiskZone));
        await db.SaveChangesAsync(ct);
        return Map(z);
    }

    public async Task<RiskZoneDto> UpdateAsync(int id, RiskZoneInput input, CancellationToken ct = default)
    {
        var z = await db.RiskZones.FindAsync([id], ct) ?? throw RuleException.NotFound("Map zone");
        Apply(z, input);
        audit.Add($"Edited map zone \"{z.Name}\"", nameof(RiskZone), id);
        await db.SaveChangesAsync(ct);
        return Map(z);
    }

    public async Task SetActiveAsync(int id, bool on, CancellationToken ct = default)
    {
        var z = await db.RiskZones.FindAsync([id], ct) ?? throw RuleException.NotFound("Map zone");
        z.IsActive = on;
        z.UpdatedUtc = DateTime.UtcNow;
        audit.Add($"Turned map zone \"{z.Name}\" {(on ? "on" : "off")}", nameof(RiskZone), id);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(int id, CancellationToken ct = default)
    {
        var z = await db.RiskZones.FindAsync([id], ct) ?? throw RuleException.NotFound("Map zone");
        db.RiskZones.Remove(z);
        audit.Add($"Deleted map zone \"{z.Name}\"", nameof(RiskZone), id);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(RiskZone z, RiskZoneInput input)
    {
        z.Type = Enum.Parse<RiskZoneType>(input.Type, true);
        z.Name = input.Name.Trim();
        z.Details = input.Details?.Trim() ?? "";
        z.Source = string.IsNullOrWhiteSpace(input.Source) ? "Alert desk" : input.Source.Trim();
        z.Latitude = input.Latitude;
        z.Longitude = input.Longitude;
        z.RadiusMeters = input.RadiusMeters;
        z.IsActive = input.IsActive;
        z.UpdatedUtc = DateTime.UtcNow;
    }
}
