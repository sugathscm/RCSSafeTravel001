using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SafeTravel.Core.Services;
using SafeTravel.Web.Infrastructure;

namespace SafeTravel.Web.Api;

/// <summary>Admin Portal (Internal): the risk zones drawn on the safety map.</summary>
[Route("api/v1/admin"), Authorize("Staff")]
public class MapAdminApi(MapService map, IOutputCacheStore cache) : ApiBase
{
    [HttpGet("risk-zones")]
    public Task<IReadOnlyList<RiskZoneDto>> Zones(CancellationToken ct) => map.ZonesAsync(false, ct);

    [HttpPost("risk-zones"), Authorize("Alerts")]
    public async Task<RiskZoneDto> AddZone(RiskZoneInput input, CancellationToken ct) { var z = await map.AddAsync(input, ct); await cache.EvictAsync(CacheTags.Alerts); return z; }

    [HttpPut("risk-zones/{id:int}"), Authorize("Alerts")]
    public async Task<RiskZoneDto> EditZone(int id, RiskZoneInput input, CancellationToken ct) { var z = await map.UpdateAsync(id, input, ct); await cache.EvictAsync(CacheTags.Alerts); return z; }

    [HttpPut("risk-zones/{id:int}/active"), Authorize("Alerts")]
    public async Task<IActionResult> ZoneActive(int id, OnInput input, CancellationToken ct) { await map.SetActiveAsync(id, input.On, ct); await cache.EvictAsync(CacheTags.Alerts); return NoContent(); }

    [HttpDelete("risk-zones/{id:int}"), Authorize("Alerts")]
    public async Task<IActionResult> RemoveZone(int id, CancellationToken ct) { await map.RemoveAsync(id, ct); await cache.EvictAsync(CacheTags.Alerts); return NoContent(); }
}
