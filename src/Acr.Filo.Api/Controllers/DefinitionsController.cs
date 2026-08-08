using Acr.Filo.Application.Common;
using Acr.Filo.Application.Definitions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Acr.Filo.Api.Controllers;

[Authorize]
[Route("api/definitions")]
public sealed class DefinitionsController : ApiControllerBase
{
    private readonly IDefinitionService _svc;
    public DefinitionsController(IDefinitionService svc) => _svc = svc;

    // tur: customers | suppliers | brands
    [HttpGet("{tur}")]
    [Authorize(Policy = "definitions.view")]
    public async Task<IActionResult> List(string tur, [FromQuery] PageQuery q, CancellationToken ct)
        => ToResponse(await _svc.ListAsync(tur, q, ct));

    [HttpGet("{tur}/active")]
    [Authorize(Policy = "definitions.view")]
    public async Task<IActionResult> Active(string tur, CancellationToken ct)
        => ToResponse(await _svc.AllActiveAsync(tur, ct));

    [HttpPost("{tur}")]
    [Authorize(Policy = "definitions.manage")]
    public async Task<IActionResult> Create(string tur, [FromBody] CreateDefinitionRequest req, CancellationToken ct)
        => ToResponse(await _svc.CreateAsync(tur, req, ct));

    [HttpPut("{tur}/{id:int}")]
    [Authorize(Policy = "definitions.manage")]
    public async Task<IActionResult> Update(string tur, int id, [FromBody] UpdateDefinitionRequest req, CancellationToken ct)
        => ToResponse(await _svc.UpdateAsync(tur, id, req, ct));

    [HttpDelete("{tur}/{id:int}")]
    [Authorize(Policy = "definitions.manage")]
    public async Task<IActionResult> Delete(string tur, int id, CancellationToken ct)
        => ToResponse(await _svc.DeleteAsync(tur, id, ct));
}
