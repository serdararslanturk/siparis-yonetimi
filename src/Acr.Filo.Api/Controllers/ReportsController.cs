using Acr.Filo.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Acr.Filo.Api.Controllers;

[Authorize(Policy = "reports.view")]
public sealed class ReportsController : ApiControllerBase
{
    private readonly IReportService _svc;
    public ReportsController(IReportService svc) => _svc = svc;

    [HttpGet("/api/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
        => ToResponse(await _svc.DashboardAsync(ct));

    [HttpGet("delivery-calendar")]
    public async Task<IActionResult> Delivery([FromQuery] CalendarQuery q, CancellationToken ct)
        => ToResponse(await _svc.DeliveryCalendarAsync(q, ct));

    [HttpGet("supply-calendar")]
    public async Task<IActionResult> Supply([FromQuery] CalendarQuery q, CancellationToken ct)
        => ToResponse(await _svc.SupplyCalendarAsync(q, ct));

    [HttpGet("ssh-calendar")]
    public async Task<IActionResult> Ssh([FromQuery] CalendarQuery q, CancellationToken ct)
        => ToResponse(await _svc.SshCalendarAsync(q, ct));

    [HttpGet("payment-calendar")]
    public async Task<IActionResult> Payment([FromQuery] CalendarQuery q, CancellationToken ct)
        => ToResponse(await _svc.PaymentCalendarAsync(q, ct));

    [HttpGet("delivery-calendar/export")]
    [Authorize(Policy = "reports.export")]
    public async Task<IActionResult> ExportDelivery([FromQuery] CalendarQuery q, CancellationToken ct)
    {
        var r = await _svc.ExportDeliveryCsvAsync(q, ct);
        if (!r.Ok) return Fail(r.Error, (int)r.Code);
        return File(r.Value!, "text/csv; charset=utf-8", $"teslim-takvimi-{DateTime.Now:yyyyMMdd}.csv");
    }
}
