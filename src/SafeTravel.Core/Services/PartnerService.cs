using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Services;

/// <summary>Enquiries from hotels and tour operators that want to share Safe Travel with their guests.</summary>
public class PartnerService(AppDbContext db, AuditService audit)
{
    private static PartnerEnquiryDto ToDto(PartnerEnquiry p) =>
        new(p.Id, p.BusinessName, p.BusinessType, p.ContactName, p.Email, p.Phone, p.GuestsPerMonth, p.Message, p.IsHandled, p.CreatedUtc);

    public async Task AddAsync(PartnerEnquiryInput input, CancellationToken ct = default)
    {
        db.PartnerEnquiries.Add(new PartnerEnquiry
        {
            BusinessName = input.BusinessName.Trim(), BusinessType = input.BusinessType, ContactName = input.ContactName.Trim(), Email = input.Email.Trim(),
            Phone = input.Phone?.Trim() ?? "", GuestsPerMonth = input.GuestsPerMonth, Message = input.Message?.Trim() ?? ""
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Enquiries for staff: those not yet handled come first, newest first.</summary>
    public async Task<IReadOnlyList<PartnerEnquiryDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await db.PartnerEnquiries.AsNoTracking().OrderBy(p => p.IsHandled).ThenByDescending(p => p.CreatedUtc).Take(500).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task SetHandledAsync(int id, bool on, CancellationToken ct = default)
    {
        var p = await db.PartnerEnquiries.FindAsync([id], ct) ?? throw RuleException.NotFound("Enquiry");
        p.IsHandled = on;
        p.HandledUtc = on ? DateTime.UtcNow : null;
        audit.Add($"Marked the partner enquiry from \"{p.BusinessName}\" as {(on ? "handled" : "open")}", nameof(PartnerEnquiry), id);
        await db.SaveChangesAsync(ct);
    }
}
