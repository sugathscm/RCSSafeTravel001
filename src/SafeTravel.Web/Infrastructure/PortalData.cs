using Microsoft.Extensions.Caching.Memory;
using SafeTravel.Core.Services;

namespace SafeTravel.Web.Infrastructure;

public record ListingPage(ListingDto Listing, IReadOnlyList<ProductDto> Products, IReadOnlyList<ListingDto> More);

/// <summary>One safety topic on its own page, with the emergency numbers shown beside it.</summary>
public record TopicPage(TopicDetailDto Topic, IReadOnlyList<NumberDto> Numbers);

/// <summary>The first aid page: every guide in the list, the one that is open, and the ambulance number for the call button.</summary>
public record FirstAidPage(IReadOnlyList<FirstAidGuideDto> Guides, FirstAidGuideDto? Guide, string? Ambulance);

public record EmergencyPage(IReadOnlyList<NumberDto> Numbers, IReadOnlyList<ListingDto> Hospitals);

public record PortalHome(HomeContentDto Content, IReadOnlyList<AlertDto> Alerts, IReadOnlyList<CategoryDto> Categories, IReadOnlyList<ListingDto> Businesses, IReadOnlyList<AdDto> Ads);

/// <summary>
/// What the Web Portal pages show, read from the database and held in memory for a few seconds.
/// Any change made in an admin portal bumps <see cref="CacheTags.Version"/>, so the next visitor sees it at once.
/// </summary>
public class PortalData(IMemoryCache cache, ContentService content, AlertService alerts, ListingService listings)
{
    public async Task<PortalHome> HomeAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync($"portal-home:{CacheTags.Version}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return new PortalHome(await content.HomeAsync(ct), await alerts.ActiveAsync(ct), await content.CategoriesAsync(true, ct),
                (await listings.SearchAsync(new ListingQuery { Kind = "business", PageSize = 6 }, ct)).Items, await content.AdsAsync(true, ct));
        }))!;
}
