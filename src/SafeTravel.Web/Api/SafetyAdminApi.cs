using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SafeTravel.Core.Services;
using SafeTravel.Web.Infrastructure;

namespace SafeTravel.Web.Api;

/// <summary>Admin Portal (Internal): the do and avoid lists of each topic, and the first aid guides.</summary>
[Route("api/v1/admin"), Authorize("Staff")]
public class SafetyAdminApi(SafetyContentService safety, IOutputCacheStore cache) : ApiBase
{
    /// <summary>Every topic with its do and avoid lists, for the editor.</summary>
    [HttpGet("content/topics")]
    public Task<IReadOnlyList<TopicDetailDto>> Topics(CancellationToken ct) => safety.TopicsAsync(false, ct);

    [HttpPut("content/topics/{id:int}/guidance"), Authorize("Content")]
    public async Task<TopicDetailDto> Guidance(int id, TopicGuidanceInput input, CancellationToken ct) { var t = await safety.SetGuidanceAsync(id, input, ct); await cache.EvictAsync(CacheTags.Content); return t; }

    [HttpGet("first-aid")]
    public Task<IReadOnlyList<FirstAidGuideDto>> Guides(CancellationToken ct) => safety.GuidesAsync(false, ct);

    [HttpPost("first-aid"), Authorize("Content")]
    public async Task<FirstAidGuideDto> AddGuide(FirstAidGuideInput input, CancellationToken ct) { var g = await safety.AddGuideAsync(input, ct); await cache.EvictAsync(CacheTags.Content); return g; }

    [HttpPut("first-aid/{id:int}"), Authorize("Content")]
    public async Task<FirstAidGuideDto> EditGuide(int id, FirstAidGuideInput input, CancellationToken ct) { var g = await safety.UpdateGuideAsync(id, input, ct); await cache.EvictAsync(CacheTags.Content); return g; }

    [HttpPut("first-aid/{id:int}/active"), Authorize("Content")]
    public async Task<IActionResult> GuideActive(int id, OnInput input, CancellationToken ct) { await safety.SetGuideActiveAsync(id, input.On, ct); await cache.EvictAsync(CacheTags.Content); return NoContent(); }

    [HttpDelete("first-aid/{id:int}"), Authorize("Content")]
    public async Task<IActionResult> RemoveGuide(int id, CancellationToken ct) { await safety.RemoveGuideAsync(id, ct); await cache.EvictAsync(CacheTags.Content); return NoContent(); }
}
