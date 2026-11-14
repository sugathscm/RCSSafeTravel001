using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

/// <summary>Safety content that has its own pages: the do and avoid lists of each topic, and the first aid guides.</summary>
public class SafetyContentService(AppDbContext db, AuditService audit)
{
    private static readonly Regex Vimeo = new(@"^https://(?:www\.)?vimeo\.com/(\d{6,12})(?:/([0-9a-f]{6,16}))?/?$", RegexOptions.CultureInvariant);
    private static readonly Regex Streamable = new(@"^https://(?:www\.)?streamable\.com/([a-z0-9]{4,12})/?$", RegexOptions.CultureInvariant);

    /// <summary>Text kept one item per line becomes a clean list: trimmed, with no empty lines.</summary>
    public static IReadOnlyList<string> Lines(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The address the video player is embedded from, or null when the link is not a Vimeo or Streamable video.</summary>
    public static string? EmbedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var v = Vimeo.Match(url.Trim());
        if (v.Success) return "https://player.vimeo.com/video/" + v.Groups[1].Value + (v.Groups[2].Success ? "?h=" + v.Groups[2].Value : "");
        var s = Streamable.Match(url.Trim());
        return s.Success ? "https://streamable.com/e/" + s.Groups[1].Value : null;
    }

    private static string Clean(string? text, int maxLines, string what)
    {
        var lines = Lines(text);
        if (lines.Count > maxLines) throw new RuleException($"Keep {what} to {maxLines} lines or fewer.");
        return string.Join('\n', lines);
    }

    // ----- topics -----

    private static TopicDetailDto MapTopic(Topic t) => new(t.Id, t.Name, t.Summary, t.Icon, t.Tone, Lines(t.Dos), Lines(t.Donts));

    public async Task<IReadOnlyList<TopicDetailDto>> TopicsAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.Topics.AsNoTracking();
        if (activeOnly) q = q.Where(t => t.IsActive);
        var list = await q.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync(ct);
        return list.Select(MapTopic).ToList();
    }

    public async Task<TopicDetailDto> TopicAsync(int id, CancellationToken ct = default)
    {
        var t = await db.Topics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct) ?? throw RuleException.NotFound("Topic");
        return MapTopic(t);
    }

    public async Task<TopicDetailDto> SetGuidanceAsync(int id, TopicGuidanceInput input, CancellationToken ct = default)
    {
        var t = await db.Topics.FindAsync([id], ct) ?? throw RuleException.NotFound("Topic");
        t.Dos = Clean(input.Dos, 12, "the \"Do\" list");
        t.Donts = Clean(input.Donts, 12, "the \"Avoid\" list");
        audit.Add($"Edited the do and avoid lists of topic \"{t.Name}\"", nameof(Topic), id);
        await db.SaveChangesAsync(ct);
        return MapTopic(t);
    }

    // ----- first aid guides -----

    private static FirstAidGuideDto MapGuide(FirstAidGuide g) => new(g.Id, g.Title, g.Icon, Lines(g.Steps), g.VideoUrl, EmbedUrl(g.VideoUrl), g.IsActive);

    public async Task<IReadOnlyList<FirstAidGuideDto>> GuidesAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.FirstAidGuides.AsNoTracking();
        if (activeOnly) q = q.Where(g => g.IsActive);
        var list = await q.OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync(ct);
        return list.Select(MapGuide).ToList();
    }

    public async Task<FirstAidGuideDto> AddGuideAsync(FirstAidGuideInput input, CancellationToken ct = default)
    {
        var g = new FirstAidGuide { SortOrder = await db.FirstAidGuides.CountAsync(ct) };
        Apply(g, input);
        db.FirstAidGuides.Add(g);
        audit.Add($"Added first aid guide \"{g.Title}\"", nameof(FirstAidGuide));
        await db.SaveChangesAsync(ct);
        return MapGuide(g);
    }

    public async Task<FirstAidGuideDto> UpdateGuideAsync(int id, FirstAidGuideInput input, CancellationToken ct = default)
    {
        var g = await db.FirstAidGuides.FindAsync([id], ct) ?? throw RuleException.NotFound("First aid guide");
        Apply(g, input);
        audit.Add($"Edited first aid guide \"{g.Title}\"", nameof(FirstAidGuide), id);
        await db.SaveChangesAsync(ct);
        return MapGuide(g);
    }

    public async Task SetGuideActiveAsync(int id, bool on, CancellationToken ct = default)
    {
        var g = await db.FirstAidGuides.FindAsync([id], ct) ?? throw RuleException.NotFound("First aid guide");
        g.IsActive = on;
        g.UpdatedUtc = DateTime.UtcNow;
        audit.Add($"Turned first aid guide \"{g.Title}\" {(on ? "on" : "off")}", nameof(FirstAidGuide), id);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveGuideAsync(int id, CancellationToken ct = default)
    {
        var g = await db.FirstAidGuides.FindAsync([id], ct) ?? throw RuleException.NotFound("First aid guide");
        db.FirstAidGuides.Remove(g);
        audit.Add($"Deleted first aid guide \"{g.Title}\"", nameof(FirstAidGuide), id);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(FirstAidGuide g, FirstAidGuideInput input)
    {
        var steps = Clean(input.Steps, 12, "the steps");
        if (steps.Length == 0) throw new RuleException("Write at least one step.");
        var video = string.IsNullOrWhiteSpace(input.VideoUrl) ? null : input.VideoUrl.Trim();
        if (video is not null && EmbedUrl(video) is null)
            throw new RuleException("The video link must be a Vimeo or Streamable link, for example https://vimeo.com/123456789.");
        g.Title = input.Title.Trim();
        g.Icon = string.IsNullOrWhiteSpace(input.Icon) ? "🩹" : input.Icon.Trim();
        g.Steps = steps;
        g.VideoUrl = video;
        g.IsActive = input.IsActive;
        g.UpdatedUtc = DateTime.UtcNow;
    }
}
