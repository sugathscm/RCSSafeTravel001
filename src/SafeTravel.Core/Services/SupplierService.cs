using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

public class SupplierService(AppDbContext db, UserManager<AppUser> users, ListingService listings, SettingsService settings,
    AuditService audit, IPaymentGateway gateway, INotificationSender notify, IClock clock)
{
    public static readonly int[] ReminderDays = [30, 14, 7, 0];

    private IQueryable<Supplier> Full() => db.Suppliers.Include(s => s.Documents).Include(s => s.Listings).ThenInclude(l => l.Category);

    public async Task<SupplierDto> GetAsync(int id, CancellationToken ct = default) =>
        (await Full().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct))?.ToDto(clock.Today) ?? throw RuleException.NotFound("Supplier");

    public async Task<PagedResult<SupplierDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = Full().AsNoTracking();
        if (Enum.TryParse<SupplierStatus>(status, true, out var st)) q = q.Where(s => s.Status == st);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(s => s.Status).ThenByDescending(s => s.Id).Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync(ct);
        return new(items.Select(s => s.ToDto(clock.Today)).ToList(), total, page, pageSize);
    }

    /// <summary>A business signs up: creates the sign-in, the supplier record and a listing that waits for review.</summary>
    public async Task<SupplierDto> RegisterAsync(SupplierRegistration r, CancellationToken ct = default)
    {
        var cat = await db.Categories.FindAsync([r.CategoryId], ct) ?? throw RuleException.NotFound("Category");
        if (cat.Kind != CategoryKind.Business || !cat.IsActive) throw new RuleException("Choose a business category.");
        if (await users.FindByEmailAsync(r.Email) is not null) throw new RuleException("That email already has an account. Sign in instead.", 409);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var supplier = new Supplier { BusinessName = r.BusinessName.Trim(), ContactName = r.ContactName.Trim(), Email = r.Email.Trim(), Phone = r.Phone.Trim(), Status = SupplierStatus.Pending };
        var listing = new Listing { Supplier = supplier, Status = ListingStatus.Pending };
        listings.Apply(listing, new ListingInput { Name = r.BusinessName, CategoryId = cat.Id, Town = r.Town, District = r.District, Phone = r.Phone, Hours = r.Hours, Description = r.Description }, cat);
        db.Listings.Add(listing);
        audit.Add($"\"{supplier.BusinessName}\" registered as a new supplier", nameof(Supplier), actorName: supplier.BusinessName);
        await db.SaveChangesAsync(ct);

        var user = new AppUser { UserName = r.Email.Trim(), Email = r.Email.Trim(), FullName = r.ContactName.Trim(), PhoneNumber = r.Phone.Trim(), SupplierId = supplier.Id };
        var created = await users.CreateAsync(user, r.Password);
        if (!created.Succeeded) throw new RuleException(string.Join(" ", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, Roles.Supplier);
        await tx.CommitAsync(ct);

        await notify.SendAsync("email", supplier.Email, "We received your registration", "The admin team will check your documents. Most reviews take two working days.", ct);
        return await GetAsync(supplier.Id, ct);
    }

    public async Task ApproveAsync(int id, CancellationToken ct = default)
    {
        var s = await Full().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw RuleException.NotFound("Supplier");
        if (s.Status == SupplierStatus.Approved) throw new RuleException("This supplier is already approved.");
        s.Status = SupplierStatus.Approved;
        s.ApprovedUtc = clock.UtcNow;
        s.RejectReason = null;
        foreach (var d in s.Documents.Where(d => d.Status == DocumentStatus.Uploaded)) d.Status = DocumentStatus.Verified;
        var paid = s.PaidUntil is DateOnly until && until >= clock.Today;
        foreach (var l in s.Listings) { l.IsVerified = true; l.Status = paid ? ListingStatus.Published : ListingStatus.Unpaid; l.UpdatedUtc = clock.UtcNow; }
        audit.Add($"Approved supplier \"{s.BusinessName}\"", nameof(Supplier), s.Id);
        await db.SaveChangesAsync(ct);
        await notify.SendAsync("email", s.Email, "Your business is approved", "Sign in to the supplier portal and pay the annual fee to go live.", ct);
    }

    public async Task RejectAsync(int id, string? reason, CancellationToken ct = default)
    {
        var s = await Full().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw RuleException.NotFound("Supplier");
        s.Status = SupplierStatus.Rejected;
        s.RejectReason = string.IsNullOrWhiteSpace(reason) ? "Documents could not be verified." : reason.Trim();
        foreach (var l in s.Listings) { l.Status = ListingStatus.Rejected; l.UpdatedUtc = clock.UtcNow; }
        audit.Add($"Rejected supplier \"{s.BusinessName}\"", nameof(Supplier), s.Id);
        await db.SaveChangesAsync(ct);
        await notify.SendAsync("email", s.Email, "About your registration", s.RejectReason, ct);
    }

    public async Task SendReminderAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Suppliers.FindAsync([id], ct) ?? throw RuleException.NotFound("Supplier");
        await notify.SendAsync("email+sms", s.Email, "Your Safe Travel listing renewal", $"Your annual fee is due{(s.PaidUntil is null ? "" : " on " + s.PaidUntil.Value.ToString("d MMM yyyy"))}.", ct);
        audit.Add($"Sent renewal reminder to \"{s.BusinessName}\"", nameof(Supplier), s.Id);
        await db.SaveChangesAsync(ct);
    }

    public const int MaxListings = 20;

    private async Task<Category> BusinessCategoryAsync(int id, CancellationToken ct)
    {
        var cat = await db.Categories.FindAsync([id], ct) ?? throw RuleException.NotFound("Category");
        if (cat.Kind != CategoryKind.Business) throw new RuleException("Choose a business category.");
        return cat;
    }

    private async Task<(Supplier, Listing)> OwnListingAsync(int supplierId, int? listingId, CancellationToken ct)
    {
        var s = await Full().FirstOrDefaultAsync(x => x.Id == supplierId, ct) ?? throw RuleException.NotFound("Supplier");
        var l = (listingId is int id ? s.Listings.FirstOrDefault(x => x.Id == id) : s.Listings.OrderBy(x => x.Id).FirstOrDefault()) ?? throw RuleException.NotFound("Listing");
        return (s, l);
    }

    /// <summary>
    /// A supplier adds another listing, for example a hotel adds its restaurant and its rooms.
    /// It follows the supplier: waits for review while the supplier is in review, and is live at once when the supplier is approved and paid.
    /// </summary>
    public async Task<SupplierDto> AddListingAsync(int supplierId, ListingInput input, CancellationToken ct = default)
    {
        var s = await Full().FirstOrDefaultAsync(x => x.Id == supplierId, ct) ?? throw RuleException.NotFound("Supplier");
        if (s.Status == SupplierStatus.Rejected) throw new RuleException("Your registration was not approved, so new listings cannot be added.");
        if (s.Listings.Count >= MaxListings) throw new RuleException($"You can have up to {MaxListings} listings. Contact the support team for more.");
        var cat = await BusinessCategoryAsync(input.CategoryId, ct);
        if (!cat.IsActive) throw new RuleException("Choose a business category.");

        var approved = s.Status == SupplierStatus.Approved;
        var l = new Listing
        {
            Supplier = s, IsVerified = approved,
            Status = !approved ? ListingStatus.Pending : s.PaidUntil is null ? ListingStatus.Unpaid : s.PaidUntil >= clock.Today ? ListingStatus.Published : ListingStatus.Lapsed
        };
        listings.Apply(l, input, cat);
        s.Listings.Add(l);
        audit.Add($"\"{s.BusinessName}\" added the listing \"{l.Name}\"", nameof(Listing), actorName: s.BusinessName);
        await db.SaveChangesAsync(ct);
        return s.ToDto(clock.Today);
    }

    /// <summary>Updates one of the supplier's listings. Without an id it is the first one, the business itself.</summary>
    public async Task<SupplierDto> UpdateListingAsync(int supplierId, ListingInput input, CancellationToken ct = default, int? listingId = null)
    {
        var (s, l) = await OwnListingAsync(supplierId, listingId, ct);
        var cat = await BusinessCategoryAsync(input.CategoryId, ct);
        var renamed = !string.Equals(l.Name, input.Name.Trim(), StringComparison.Ordinal) || l.CategoryId != cat.Id;
        listings.Apply(l, input, cat);
        if (l.Id == s.Listings.Min(x => x.Id)) s.BusinessName = l.Name;
        audit.Add(renamed ? $"\"{s.BusinessName}\" changed the name or category of \"{l.Name}\" (check it)" : $"\"{s.BusinessName}\" updated the listing \"{l.Name}\"", nameof(Listing), l.Id, s.BusinessName);
        await db.SaveChangesAsync(ct);
        return s.ToDto(clock.Today);
    }

    public async Task<SupplierDto> RemoveListingAsync(int supplierId, int listingId, CancellationToken ct = default)
    {
        var (s, l) = await OwnListingAsync(supplierId, listingId, ct);
        if (l.Id == s.Listings.Min(x => x.Id)) throw new RuleException("This is your main listing. Edit it instead of removing it.");
        s.Listings.Remove(l);
        db.Listings.Remove(l);
        audit.Add($"\"{s.BusinessName}\" removed the listing \"{l.Name}\"", nameof(Listing), l.Id, s.BusinessName);
        await db.SaveChangesAsync(ct);
        return s.ToDto(clock.Today);
    }

    /// <summary>Sets or clears (null) the promotion on a listing. Travellers see it as a red badge on the tile.</summary>
    public async Task<SupplierDto> SetPromotionAsync(int supplierId, int listingId, PromotionInput? input, CancellationToken ct = default)
    {
        var (s, l) = await OwnListingAsync(supplierId, listingId, ct);
        if (input is not null && input.Until is DateOnly until && until < clock.Today) throw new RuleException("Choose an end date that is today or later.");
        l.PromoTitle = input?.Title.Trim() ?? ""; l.PromoText = input?.Text?.Trim() ?? ""; l.PromoUntil = input?.Until; l.UpdatedUtc = clock.UtcNow;
        audit.Add(input is null ? $"\"{s.BusinessName}\" ended the promotion on \"{l.Name}\"" : $"\"{s.BusinessName}\" set a promotion on \"{l.Name}\": {l.PromoTitle}", nameof(Listing), l.Id, s.BusinessName);
        await db.SaveChangesAsync(ct);
        return s.ToDto(clock.Today);
    }

    public async Task<DocumentDto> AddDocumentAsync(int supplierId, string name, string fileName, string storagePath, long size, CancellationToken ct = default)
    {
        var s = await db.Suppliers.FindAsync([supplierId], ct) ?? throw RuleException.NotFound("Supplier");
        var d = new SupplierDocument { SupplierId = s.Id, Name = name, FileName = fileName, StoragePath = storagePath, SizeBytes = size };
        db.SupplierDocuments.Add(d);
        audit.Add($"\"{s.BusinessName}\" uploaded \"{name}\"", nameof(SupplierDocument), actorName: s.BusinessName);
        await db.SaveChangesAsync(ct);
        return new(d.Id, d.Name, d.FileName, "uploaded", d.UploadedUtc);
    }

    public async Task<SupplierDocument> GetDocumentAsync(int documentId, int? onlyForSupplierId, CancellationToken ct = default)
    {
        var d = await db.SupplierDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId, ct) ?? throw RuleException.NotFound("Document");
        if (onlyForSupplierId is int sid && d.SupplierId != sid) throw RuleException.NotFound("Document");
        return d;
    }

    /// <summary>Pays the annual fee. Adds one year from today, or from the current end date when renewing early.</summary>
    public async Task<PaymentDto> PayAnnualFeeAsync(int supplierId, CancellationToken ct = default)
    {
        var s = await Full().FirstOrDefaultAsync(x => x.Id == supplierId, ct) ?? throw RuleException.NotFound("Supplier");
        if (s.Status != SupplierStatus.Approved) throw new RuleException("Payment opens after the admin team approves your registration.");

        var p = new Payment { Kind = PaymentKind.SupplierFee, SupplierId = s.Id, PayerName = s.BusinessName, PayerEmail = s.Email, Amount = await settings.AnnualFeeAsync(ct), Status = PaymentStatus.Pending };
        db.Payments.Add(p);
        await db.SaveChangesAsync(ct);

        var result = await gateway.ChargeAsync(p, ct);
        p.GatewayReference = result.Reference;
        if (result.Paid) ApplyPaidFee(s, p);
        else p.Status = PaymentStatus.Failed;
        await db.SaveChangesAsync(ct);
        return p.ToDto();
    }

    /// <summary>Marks a fee as paid and brings the listing live. Also called when the gateway confirms a payment later.</summary>
    public void ApplyPaidFee(Supplier s, Payment p)
    {
        p.Status = PaymentStatus.Paid;
        p.PaidUtc = clock.UtcNow;
        var from = s.PaidUntil is DateOnly until && until > clock.Today ? until : clock.Today;
        s.PaidUntil = from.AddYears(1);
        foreach (var l in s.Listings.Where(l => l.Status is ListingStatus.Unpaid or ListingStatus.Lapsed)) { l.Status = ListingStatus.Published; l.UpdatedUtc = clock.UtcNow; }
        audit.Add($"\"{s.BusinessName}\" paid the annual fee, live until {s.PaidUntil:yyyy-MM-dd}", nameof(Payment), p.Id, s.BusinessName);
    }

    public async Task<IReadOnlyList<PaymentDto>> PaymentsAsync(int supplierId, CancellationToken ct = default) =>
        (await db.Payments.AsNoTracking().Where(p => p.SupplierId == supplierId).OrderByDescending(p => p.Id).ToListAsync(ct)).Select(p => p.ToDto()).ToList();

    /// <summary>Daily job: hides listings whose fee ran out and sends the 30, 14, 7 and 0 day reminders once each.</summary>
    public async Task<(int Lapsed, int Reminders)> RunRenewalsAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var lapsedListings = await db.Listings.Include(l => l.Supplier)
            .Where(l => l.Status == ListingStatus.Published && l.SupplierId != null && l.Supplier!.PaidUntil != null && l.Supplier.PaidUntil < today).ToListAsync(ct);
        foreach (var l in lapsedListings) { l.Status = ListingStatus.Lapsed; l.UpdatedUtc = clock.UtcNow; }
        if (lapsedListings.Count > 0) audit.Add($"{lapsedListings.Count} listing(s) lapsed because the annual fee was not renewed", actorName: "System");

        var horizon = today.AddDays(ReminderDays.Max());
        var due = await db.Suppliers.Where(s => s.Status == SupplierStatus.Approved && s.PaidUntil != null && s.PaidUntil >= today && s.PaidUntil <= horizon).ToListAsync(ct);
        var sent = 0;
        foreach (var s in due)
        {
            var daysLeft = s.PaidUntil!.Value.DayNumber - today.DayNumber;
            if (!ReminderDays.Contains(daysLeft)) continue;
            if (await db.ReminderLogs.AnyAsync(r => r.SupplierId == s.Id && r.RenewalDate == s.PaidUntil && r.DaysBefore == daysLeft, ct)) continue;
            var channel = daysLeft switch { 30 => "email", 14 => "email+sms", 7 => "email+sms+push", _ => "email+sms" };
            await notify.SendAsync(channel, s.Email, "Your Safe Travel listing renewal",
                daysLeft == 0 ? "Your annual fee is due today. Renew to stay on the map." : $"Your annual fee is due in {daysLeft} days, on {s.PaidUntil:d MMM yyyy}.", ct);
            db.ReminderLogs.Add(new ReminderLog { SupplierId = s.Id, RenewalDate = s.PaidUntil.Value, DaysBefore = daysLeft, Channel = channel });
            sent++;
        }
        await db.SaveChangesAsync(ct);
        return (lapsedListings.Count, sent);
    }
}
