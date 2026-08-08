using Acr.Filo.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Acr.Filo.Api.Controllers;

[Authorize]
public sealed class OrdersController : ApiControllerBase
{
    private readonly IOrderService _svc;
    public OrdersController(IOrderService svc) => _svc = svc;

    [HttpGet]
    [Authorize(Policy = "orders.view")]
    public async Task<IActionResult> List([FromQuery] OrderListQuery q, CancellationToken ct)
        => ToResponse(await _svc.ListAsync(q, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = "orders.view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => ToResponse(await _svc.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "orders.create")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req, CancellationToken ct)
        => ToResponse(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = "orders.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderRequest req, CancellationToken ct)
        => ToResponse(await _svc.UpdateAsync(id, req, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "orders.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => ToResponse(await _svc.DeleteAsync(id, ct));

    [HttpPost("{id:int}/lines")]
    [Authorize(Policy = "orders.update")]
    public async Task<IActionResult> AddLines(int id, [FromBody] AddLineRequest req, CancellationToken ct)
        => ToResponse(await _svc.AddLinesAsync(id, req, ct));

    [HttpPut("{orderId:int}/lines/{lineId:int}")]
    [Authorize(Policy = "orders.update")]
    public async Task<IActionResult> UpdateLine(int orderId, int lineId, [FromBody] UpdateLineRequest req, CancellationToken ct)
        => ToResponse(await _svc.UpdateLineAsync(orderId, lineId, req, ct));

    /// <summary>Kalemin ödeme planını topluca değiştirir (tarih/tutar revizesi).</summary>
    [HttpPut("{orderId:int}/lines/{lineId:int}/plans")]
    [Authorize(Policy = "orders.update")]
    public async Task<IActionResult> UpdatePlans(int orderId, int lineId, [FromBody] UpdatePlansRequest req, CancellationToken ct)
        => ToResponse(await _svc.UpdatePlansAsync(orderId, lineId, req, ct));

    [HttpDelete("{orderId:int}/lines/{lineId:int}")]
    [Authorize(Policy = "orders.delete")]
    public async Task<IActionResult> DeleteLine(int orderId, int lineId, CancellationToken ct)
        => ToResponse(await _svc.DeleteLineAsync(orderId, lineId, ct));

    [HttpPut("{orderId:int}/vehicles/{vehicleId:int}")]
    [Authorize(Policy = "vehicles.update")]
    public async Task<IActionResult> UpdateVehicle(int orderId, int vehicleId, [FromBody] UpdateVehicleRequest req, CancellationToken ct)
        => ToResponse(await _svc.UpdateVehicleAsync(orderId, vehicleId, req, ct));

    [HttpGet("{orderId:int}/vehicles/{vehicleId:int}/history")]
    [Authorize(Policy = "orders.view")]
    public async Task<IActionResult> VehicleHistory(int orderId, int vehicleId, CancellationToken ct)
        => ToResponse(await _svc.VehicleHistoryAsync(orderId, vehicleId, ct));

    [HttpPost("{orderId:int}/lines/{lineId:int}/payments")]
    [Authorize(Policy = "payments.record")]
    public async Task<IActionResult> AddPayment(int orderId, int lineId, [FromBody] AddPaymentRequest req, CancellationToken ct)
        => ToResponse(await _svc.AddPaymentAsync(orderId, lineId, req, ct));

    [HttpDelete("{orderId:int}/lines/{lineId:int}/payments/{paymentId:int}")]
    [Authorize(Policy = "payments.record")]
    public async Task<IActionResult> DeletePayment(int orderId, int lineId, int paymentId, CancellationToken ct)
        => ToResponse(await _svc.DeletePaymentAsync(orderId, lineId, paymentId, ct));
}
