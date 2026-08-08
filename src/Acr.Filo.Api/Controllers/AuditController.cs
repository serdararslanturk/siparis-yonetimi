using Acr.Filo.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Acr.Filo.Api.Controllers;

[Authorize(Policy = "audit.view")]
[Route("api/audit")]
public sealed class AuditController : ApiControllerBase
{
    private readonly IAuditService _svc;
    public AuditController(IAuditService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AuditQuery q, CancellationToken ct)
        => ToResponse(await _svc.ListAsync(q, ct));
}
