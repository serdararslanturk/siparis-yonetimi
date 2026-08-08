using Acr.Filo.Domain.Common;

namespace Acr.Filo.Domain.Entities.Orders;

/// <summary>SQL: dbo.FleetOrderPaymentPlans. Prototip: line.odeme.planlar[] — PLANLANAN ödeme.</summary>
public class FleetOrderPaymentPlan : AuditableEntity
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public DateOnly PlanTarihi { get; set; }
    public decimal Tutar { get; set; }

    public FleetOrderLine Line { get; set; } = null!;
}
