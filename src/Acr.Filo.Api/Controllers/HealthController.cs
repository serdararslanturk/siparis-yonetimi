using Acr.Filo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Acr.Filo.Api.Controllers;

[AllowAnonymous]
[Route("api/health")]
public sealed class HealthController : ApiControllerBase
{
    private readonly FiloDbContext _db;
    public HealthController(FiloDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        // Şartname madde 13: sağlık kontrolü endpoint'i. DB bağlantısını test eder.
        bool db;
        try { db = await _db.Database.CanConnectAsync(ct); }
        catch { db = false; }
        var body = new { status = db ? "healthy" : "degraded", database = db, timeUtc = DateTime.UtcNow };
        return db ? Ok(body) : StatusCode(503, body);
    }
}
