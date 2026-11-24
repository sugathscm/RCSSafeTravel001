using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

/// <summary>Everything the Web Portal home page shows: headline, sections, numbers, topics, sponsors, categories.</summary>
public class ContentService(AppDbContext db, SettingsService settings, AuditService audit)
{
    public async Task<HomeContentDto> HomeAsync(CancellationToken ct = default)
    {
        var all = await settings.AllAsync(ct);
        var sections = SettingKeys.Sections.ToDictionary(s => s, s => !all.TryGetValue(SettingKeys.SectionPrefix + s, out var v) || v == "true");
        return new(
            all.GetValueOrDefault(SettingKeys.HeroTitle, "Explore Sri Lanka. We've got your back."),
            all.GetValueOrDefault(SettingKeys.HeroText, ""),
            await settings.AnnualFeeAsync(ct), sections,
            await NumbersAsync(ct),
            await db.Topics.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder).ThenBy(t => t.Id).Select(t => new TopicDto(t.Id, t.Name, t.Summary, t.Icon, t.Tone)).ToListAsync(ct),
            await db.Sponsors.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).Select(s => new SponsorDto(s.Id, s.Name, s.Role, s.IsActive)).ToListAsync(ct));
    }

    public async Task<IReadOnlyList<NumberDto>> NumbersAsync(CancellationToken ct = default) =>
        await db.EmergencyNumbers.AsNoTracking().OrderBy(n => n.SortOrder).ThenBy(n => n.Id).Select(n => new NumberDto(n.Id, n.Label, n.Number)).ToListAsync(ct);

    public async Task SetHeroAsync(HeroInput input, CancellationToken ct = default)
    {
        await settings.SetAsync(SettingKeys.HeroTitle, input.HeroTitle.Trim(), ct);
        await settings.SetAsync(SettingKeys.HeroText, input.HeroText?.Trim() ?? "", ct);
        audit.Add("Changed the home page headline", nameof(SiteSetting));
        await db.SaveChangesAsync(ct);
    }

    public async Task SetSectionAsync(string section, bool on, CancellationToken ct = default)
    {
        if (!SettingKeys.Sections.Contains(section)) throw RuleException.NotFound("Section");
        await settings.SetAsync(SettingKeys.SectionPrefix + section, on ? "true" : "false", ct);
        audit.Add($"Turned home page section \"{section}\" {(on ? "on" : "off")}", nameof(SiteSetting));
        await db.SaveChangesAsync(ct);
    }

    public async Task SetAnnualFeeAsync(decimal fee, CancellationToken ct = default)
    {
        if (fee < 0 || fee > 1_000_000) throw new RuleException("Enter a fee between 0 and 1,000,000.");
        await settings.SetAsync(SettingKeys.AnnualFee, fee.ToString(CultureInfo.InvariantCulture), ct);
        audit.Add($"Set the supplier annual fee to LKR {fee:N0}", nameof(SiteSetting));
        await db.SaveChangesAsync(ct);
    }

    public async Task SetNumbersAsync(IReadOnlyList<NumberInput> numbers, CancellationToken ct = default)
    {
        if (numbers.Count is 0 or > 12) throw new RuleException("Keep between 1 and 12 emergency numbers.");
        db.EmergencyNumbers.RemoveRange(await db.EmergencyNumbers.ToListAsync(ct));
        db.EmergencyNumbers.AddRange(numbers.Select((n, i) => new EmergencyNumber { Label = n.Label.Trim(), Number = n.Number.Trim(), SortOrder = i }));
        audit.Add("Edited emergency numbers", nameof(EmergencyNumber));
        await db.SaveChangesAsync(ct);
    }

    public async Task<TopicDto> AddTopicAsync(TopicInput input, CancellationToken ct = default)
    {
        var tones = new[] { "blue", "teal", "green", "rose", "sand", "olive" };
        var count = await db.Topics.CountAsync(ct);
        var t = new Topic { Name = input.Name.Trim(), Summary = input.Summary.Trim(), Icon = string.IsNullOrWhiteSpace(input.Icon) ? "📘" : input.Icon.Trim(), Tone = tones[count % tones.Length], SortOrder = count };
        db.Topics.Add(t);
        audit.Add($"Added topic \"{t.Name}\"", nameof(Topic));
        await db.SaveChangesAsync(ct);
        return new(t.Id, t.Name, t.Summary, t.Icon, t.Tone);
    }

    public async Task RemoveTopicAsync(int id, CancellationToken ct = default)
    {
        var t = await db.Topics.FindAsync([id], ct) ?? throw RuleException.NotFound("Topic");
        db.Topics.Remove(t);
        audit.Add($"Removed topic \"{t.Name}\"", nameof(Topic), id);
        await db.SaveChangesAsync(ct);
    }

    // ----- categories -----

    public async Task<IReadOnlyList<CategoryDto>> CategoriesAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.Categories.AsNoTracking();
        if (activeOnly) q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.Kind).ThenBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new CategoryDto(c.Id, c.Slug, c.Name, c.Icon, c.Kind == CategoryKind.Help ? "help" : "business", c.IsActive,
                activeOnly ? c.Listings.Count(l => l.Status == ListingStatus.Published) : c.Listings.Count)).ToListAsync(ct);
    }

    public async Task<CategoryDto> AddCategoryAsync(CategoryInput input, CancellationToken ct = default)
    {
        var name = input.Name.Trim();
        var slug = new string(name.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) && ch < 128 ? ch : '-').ToArray()).Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        if (slug.Length == 0) slug = "category";
        if (slug.Length > 34) slug = slug[..34];
        var baseSlug = slug;
        for (var i = 2; await db.Categories.AnyAsync(c => c.Slug == slug, ct); i++) slug = $"{baseSlug}-{i}";
        var c = new Category { Name = name, Slug = slug, Icon = string.IsNullOrWhiteSpace(input.Icon) ? "📍" : input.Icon.Trim(), Kind = input.Kind == "business" ? CategoryKind.Business : CategoryKind.Help, SortOrder = await db.Categories.CountAsync(ct) };
        db.Categories.Add(c);
        audit.Add($"Added category \"{c.Name}\"", nameof(Category));
        await db.SaveChangesAsync(ct);
        return new(c.Id, c.Slug, c.Name, c.Icon, input.Kind, true, 0);
    }

    public async Task SetCategoryActiveAsync(int id, bool on, CancellationToken ct = default)
    {
        var c = await db.Categories.FindAsync([id], ct) ?? throw RuleException.NotFound("Category");
        c.IsActive = on;
        audit.Add($"Turned category \"{c.Name}\" {(on ? "on" : "off")}", nameof(Category), id);
        await db.SaveChangesAsync(ct);
    }

    // ----- sponsors -----

    public async Task<IReadOnlyList<SponsorDto>> SponsorsAsync(CancellationToken ct = default) =>
        await db.Sponsors.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Select(s => new SponsorDto(s.Id, s.Name, s.Role, s.IsActive)).ToListAsync(ct);

    public async Task<SponsorDto> AddSponsorAsync(SponsorInput input, CancellationToken ct = default)
    {
        var s = new Sponsor { Name = input.Name.Trim(), Role = input.Role.Trim(), SortOrder = await db.Sponsors.CountAsync(ct) };
        db.Sponsors.Add(s);
        audit.Add($"Added sponsor \"{s.Name}\"", nameof(Sponsor));
        await db.SaveChangesAsync(ct);
        return new(s.Id, s.Name, s.Role, true);
    }

    public async Task SetSponsorActiveAsync(int id, bool on, CancellationToken ct = default)
    {
        var s = await db.Sponsors.FindAsync([id], ct) ?? throw RuleException.NotFound("Sponsor");
        s.IsActive = on;
        audit.Add($"Turned sponsor \"{s.Name}\" {(on ? "on" : "off")}", nameof(Sponsor), id);
        await db.SaveChangesAsync(ct);
    }

    // ----- advertisements: three places in the Sponsored section and two top banners (see Advertisement) -----

    /// <summary>Advertisements in slot order. The public list leaves out switched-off ones and those of a switched-off sponsor.</summary>
    public async Task<IReadOnlyList<AdDto>> AdsAsync(bool publicOnly, CancellationToken ct = default)
    {
        var q = db.Advertisements.AsNoTracking().Include(a => a.Sponsor).AsQueryable();
        if (publicOnly) q = q.Where(a => a.IsActive && a.Sponsor!.IsActive);
        return (await q.OrderBy(a => a.Slot).ToListAsync(ct)).Select(a => a.ToDto()).ToList();
    }

    public async Task<AdDto> SaveAdAsync(int slot, AdInput input, CancellationToken ct = default)
    {
        if (slot is < 1 or > Advertisement.Slots) throw RuleException.NotFound("Advertisement place");
        var sponsor = await db.Sponsors.FindAsync([input.SponsorId], ct) ?? throw new RuleException("Choose a sponsor.");
        var ad = await db.Advertisements.FirstOrDefaultAsync(a => a.Slot == slot, ct);
        if (ad is null) db.Advertisements.Add(ad = new Advertisement { Slot = slot });
        ad.SponsorId = sponsor.Id; ad.Sponsor = sponsor;
        ad.Headline = input.Headline.Trim(); ad.Text = input.Text?.Trim() ?? ""; ad.LinkLabel = input.LinkLabel?.Trim() ?? "";
        ad.LinkUrl = input.LinkUrl.Trim(); ad.IsActive = input.IsActive; ad.UpdatedUtc = DateTime.UtcNow;
        audit.Add($"Saved advertisement {slot} for \"{sponsor.Name}\" ({(ad.IsActive ? "on" : "off")})", nameof(Advertisement));
        await db.SaveChangesAsync(ct);
        return ad.ToDto();
    }

    /// <summary>Sets or clears the picture. Returns the old stored file so the caller can delete it.</summary>
    public async Task<(AdDto Ad, string? OldPath)> SetAdImageAsync(int slot, string? storagePath, CancellationToken ct = default)
    {
        var ad = await db.Advertisements.Include(a => a.Sponsor).FirstOrDefaultAsync(a => a.Slot == slot, ct)
                 ?? throw new RuleException("Save the advertisement text first, then add the picture.");
        var old = ad.ImagePath;
        ad.ImagePath = storagePath; ad.UpdatedUtc = DateTime.UtcNow;
        audit.Add($"{(storagePath is null ? "Removed" : "Changed")} the picture of advertisement {slot}", nameof(Advertisement), ad.Id);
        await db.SaveChangesAsync(ct);
        return (ad.ToDto(), old);
    }

    public async Task<string?> AdImagePathAsync(int id, CancellationToken ct = default) =>
        await db.Advertisements.AsNoTracking().Where(a => a.Id == id).Select(a => a.ImagePath).FirstOrDefaultAsync(ct);

    public Task TrackAdClickAsync(int id, CancellationToken ct = default) =>
        db.Advertisements.Where(a => a.Id == id && a.IsActive).ExecuteUpdateAsync(s => s.SetProperty(a => a.Clicks, a => a.Clicks + 1), ct);
}

public class AlertService(AppDbContext db, AuditService audit, INotificationSender notify, IClock clock)
{
    public async Task<IReadOnlyList<AlertDto>> ActiveAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return (await db.Alerts.AsNoTracking().Where(a => a.IsActive && (a.ExpiresUtc == null || a.ExpiresUtc > now))
            .OrderByDescending(a => a.PublishedUtc).Take(50).ToListAsync(ct)).Select(a => a.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<AlertDto>> AllAsync(CancellationToken ct = default) =>
        (await db.Alerts.AsNoTracking().OrderByDescending(a => a.PublishedUtc).Take(200).ToListAsync(ct)).Select(a => a.ToDto()).ToList();

    public async Task<AlertDto> PublishAsync(AlertInput input, CancellationToken ct = default)
    {
        if ((input.Latitude is null) != (input.Longitude is null)) throw new RuleException("Give both the latitude and the longitude, or leave both empty.");
        var a = new Alert { Level = Enum.Parse<AlertLevel>(input.Level, true), Type = input.Type.Trim(), Area = input.Area.Trim(), Title = input.Title.Trim(), Source = string.IsNullOrWhiteSpace(input.Source) ? "Alert desk" : input.Source.Trim(), PublishedUtc = clock.UtcNow, Details = input.Details?.Trim() ?? "", Latitude = input.Latitude, Longitude = input.Longitude };
        db.Alerts.Add(a);
        audit.Add($"Published alert \"{a.Title}\"", nameof(Alert));
        await db.SaveChangesAsync(ct);
        await notify.SendAsync("push", "area:" + a.Area, a.Type, a.Title, ct);
        return a.ToDto();
    }

    public async Task SetActiveAsync(int id, bool on, CancellationToken ct = default)
    {
        var a = await db.Alerts.FindAsync([id], ct) ?? throw RuleException.NotFound("Alert");
        a.IsActive = on;
        audit.Add($"{(on ? "Reactivated" : "Expired")} alert \"{a.Title}\"", nameof(Alert), id);
        await db.SaveChangesAsync(ct);
    }
}

public record PaymentSummary(decimal Donations, decimal SupplierFees, int Failed);
public record DashboardDto(int LiveListings, int PendingSuppliers, int ActiveAlerts, decimal CollectedThisMonth, int LapsedListings,
    IReadOnlyList<SupplierDto> NeedsReview, IReadOnlyList<ListingDto> Lapsed, IReadOnlyList<AuditDto> Latest, IReadOnlyList<int> WeeklyRegistrations);

public class PaymentService(AppDbContext db, IPaymentGateway gateway, AuditService audit, IClock clock)
{
    public async Task<PaymentDto> DonateAsync(DonationInput input, CancellationToken ct = default)
    {
        var p = new Payment { Kind = PaymentKind.Donation, PayerName = string.IsNullOrWhiteSpace(input.Name) ? "Anonymous donor" : input.Name.Trim(), PayerEmail = input.Email?.Trim(), Amount = decimal.Round(input.Amount, 2), Status = PaymentStatus.Pending };
        db.Payments.Add(p);
        await db.SaveChangesAsync(ct);
        var r = await gateway.ChargeAsync(p, ct);
        p.GatewayReference = r.Reference;
        p.Status = r.Paid ? PaymentStatus.Paid : PaymentStatus.Failed;
        if (r.Paid) { p.PaidUtc = clock.UtcNow; audit.Add($"Donation of LKR {p.Amount:N0} received", nameof(Payment), p.Id, p.PayerName); }
        await db.SaveChangesAsync(ct);
        return p.ToDto();
    }

    public async Task<PagedResult<PaymentDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.Payments.AsNoTracking().OrderByDescending(p => p.Id);
        var total = await q.CountAsync(ct);
        return new((await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct)).Select(p => p.ToDto()).ToList(), total, page, pageSize);
    }

    public async Task<PaymentSummary> SummaryAsync(CancellationToken ct = default)
    {
        // Summed as double so the same query runs on SQL Server and on the SQLite test database.
        var paid = await db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Paid).GroupBy(p => p.Kind)
            .Select(g => new { g.Key, Total = g.Sum(p => (double)p.Amount) }).ToListAsync(ct);
        decimal Of(PaymentKind k) => (decimal)(paid.FirstOrDefault(x => x.Key == k)?.Total ?? 0);
        return new(Of(PaymentKind.Donation), Of(PaymentKind.SupplierFee), await db.Payments.CountAsync(p => p.Status == PaymentStatus.Failed, ct));
    }

    public async Task<decimal> CollectedSinceAsync(DateTime fromUtc, CancellationToken ct = default) =>
        (decimal)(await db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Paid && p.CreatedUtc >= fromUtc).SumAsync(p => (double?)p.Amount, ct) ?? 0);
}

public class DashboardService(AppDbContext db, PaymentService payments, IClock clock)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var pending = await db.Suppliers.AsNoTracking().Include(s => s.Documents).Include(s => s.Listings).ThenInclude(l => l.Category)
            .Where(s => s.Status == SupplierStatus.Pending).OrderBy(s => s.Id).Take(10).AsSplitQuery().ToListAsync(ct);
        var lapsed = await db.Listings.AsNoTracking().Include(l => l.Category).Where(l => l.Status == ListingStatus.Lapsed).OrderBy(l => l.Name).Take(10).ToListAsync(ct);
        var since = now.Date.AddDays(-55);
        var created = await db.Suppliers.AsNoTracking().Where(s => s.CreatedUtc >= since).Select(s => s.CreatedUtc).ToListAsync(ct);
        var weeks = Enumerable.Range(0, 8).Select(i => created.Count(d => (int)((d - since).TotalDays / 7) == i)).ToList();
        return new(
            await db.Listings.CountAsync(l => l.Status == ListingStatus.Published && l.Category!.IsActive, ct),
            await db.Suppliers.CountAsync(s => s.Status == SupplierStatus.Pending, ct),
            await db.Alerts.CountAsync(a => a.IsActive, ct),
            await payments.CollectedSinceAsync(monthStart, ct),
            await db.Listings.CountAsync(l => l.Status == ListingStatus.Lapsed, ct),
            pending.Select(s => s.ToDto(clock.Today)).ToList(), lapsed.Select(l => l.ToDto()).ToList(),
            await db.AuditEntries.AsNoTracking().OrderByDescending(a => a.Id).Take(5).Select(a => new AuditDto(a.Id, a.WhenUtc, a.UserName, a.Action)).ToListAsync(ct),
            weeks);
    }
}
