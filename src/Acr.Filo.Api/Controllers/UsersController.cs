using Acr.Filo.Application.Common;
using Acr.Filo.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Acr.Filo.Api.Controllers;

[Authorize(Policy = "users.manage")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserService _svc;
    public UsersController(IUserService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery q, CancellationToken ct)
        => ToResponse(await _svc.ListAsync(q, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
        => ToResponse(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req, CancellationToken ct)
        => ToResponse(await _svc.UpdateAsync(id, req, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
        => ToResponse(await _svc.DeactivateAsync(id, ct));

    [HttpGet("/api/roles")]
    public async Task<IActionResult> Roles(CancellationToken ct)
        => ToResponse(await _svc.RolesAsync(ct));
}
