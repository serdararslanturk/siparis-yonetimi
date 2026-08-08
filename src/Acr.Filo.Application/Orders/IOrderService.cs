using Acr.Filo.Application.Common;

namespace Acr.Filo.Application.Orders;

public interface IOrderService
{
    Task<Result<PagedResult<OrderListItemDto>>> ListAsync(OrderListQuery q, CancellationToken ct);
    Task<Result<OrderDetailDto>> GetAsync(int id, CancellationToken ct);
    Task<Result<OrderDetailDto>> CreateAsync(CreateOrderRequest req, CancellationToken ct);
    Task<Result<OrderDetailDto>> UpdateAsync(int id, UpdateOrderRequest req, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CancellationToken ct);              // soft-delete
    Task<Result<OrderDetailDto>> AddLinesAsync(int id, AddLineRequest req, CancellationToken ct);
    Task<Result<OrderDetailDto>> UpdateLineAsync(int orderId, int lineId, UpdateLineRequest req, CancellationToken ct);
    Task<Result<OrderDetailDto>> UpdatePlansAsync(int orderId, int lineId, UpdatePlansRequest req, CancellationToken ct);
    Task<Result> DeleteLineAsync(int orderId, int lineId, CancellationToken ct);
    Task<Result<VehicleDto>> UpdateVehicleAsync(int orderId, int vehicleId, UpdateVehicleRequest req, CancellationToken ct);
    Task<Result<PaymentDto>> AddPaymentAsync(int orderId, int lineId, AddPaymentRequest req, CancellationToken ct);
    Task<Result> DeletePaymentAsync(int orderId, int lineId, int paymentId, CancellationToken ct);
    Task<Result<IReadOnlyList<VehicleEventDto>>> VehicleHistoryAsync(int orderId, int vehicleId, CancellationToken ct);
}

/// <summary>Liste filtresi. tab: acik|teslim|tumu (frontend 3 sekme).</summary>
public sealed class OrderListQuery : PageQuery
{
    public string Tab { get; set; } = "acik";
    public int? CustomerId { get; set; }
}
