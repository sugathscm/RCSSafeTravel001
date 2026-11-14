using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeTravel.Core.Services;

namespace SafeTravel.Web.Api;

/// <summary>Admin Portal (Internal): enquiries from hotels and tour operators.</summary>
[Route("api/v1/admin"), Authorize("Staff")]
public class PartnerAdminApi(PartnerService partners) : ApiBase
{
    [HttpGet("partner-enquiries"), Authorize("SuppliersRead")]
    public Task<IReadOnlyList<PartnerEnquiryDto>> Enquiries(CancellationToken ct) => partners.ListAsync(ct);

    [HttpPut("partner-enquiries/{id:int}/handled"), Authorize("Suppliers")]
    public async Task<IActionResult> Handled(int id, OnInput input, CancellationToken ct) { await partners.SetHandledAsync(id, input.On, ct); return NoContent(); }
}
