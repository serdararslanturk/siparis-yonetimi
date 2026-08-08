using Acr.Filo.Domain.Common;

namespace Acr.Filo.Domain.Entities.Orders;

/// <summary>SQL: dbo.FleetOrderPayments. Prototip: line.odeme.odemeler[] — GERÇEKLEŞEN ödeme.
/// Bu kaydı yalnızca payments.record yetkisi olan (Muhasebe / Admin) girebilir.</summary>
public class FleetOrderPayment : AuditableEntity
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public DateOnly OdemeTarihi { get; set; }
    public decimal Tutar { get; set; }

    public FleetOrderLine Line { get; set; } = null!;
}
