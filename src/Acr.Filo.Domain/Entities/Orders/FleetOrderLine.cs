using Acr.Filo.Domain.Common;
using Acr.Filo.Domain.Entities.Definitions;

namespace Acr.Filo.Domain.Entities.Orders;

/// <summary>
/// SQL: dbo.FleetOrderLines. Prototip karşılığı: order.vehicleLines[] elemanı.
/// Her kalem KENDİ tedarikçisine sahiptir (prototip migrateOrder, satır 1386:
/// tedarikçi sipariş başlığından kaleme taşınmıştı).
/// </summary>
public class FleetOrderLine : ConcurrentAuditableEntity
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int BrandId { get; set; }

    /// <summary>Serbest metin — kapsam kararı gereği model tanım ağacı kurulmadı.</summary>
    public string Model { get; set; } = null!;

    public int Adet { get; set; }
    public decimal BirimBedel { get; set; }
    public int SupplierId { get; set; }

    public FleetOrder Order { get; set; } = null!;
    public Brand Brand { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public ICollection<FleetOrderPaymentPlan> PaymentPlans { get; set; } = new List<FleetOrderPaymentPlan>();
    public ICollection<FleetOrderPayment> Payments { get; set; } = new List<FleetOrderPayment>();
    public ICollection<FleetOrderVehicle> Vehicles { get; set; } = new List<FleetOrderVehicle>();

    // --- Hesaplanan alanlar. Frontend satır 452-455 birebir karşılığı.
    // EF bunları haritalamaz; SQL tarafındaki eşdeğeri vw_LinePaymentSummary'dir.

    /// <summary>lineTotal(l) = l.adet * l.birimBedel</summary>
    public decimal KalemToplam => Adet * BirimBedel;

    /// <summary>linePlanToplam(l)</summary>
    public decimal PlanToplam => PaymentPlans.Sum(p => p.Tutar);

    /// <summary>lineOdenenToplam(l)</summary>
    public decimal OdenenToplam => Payments.Sum(p => p.Tutar);

    /// <summary>lineKalanTutar(l) = max(total - ödenen, 0)</summary>
    public decimal KalanTutar => Math.Max(KalemToplam - OdenenToplam, 0m);

    /// <summary>Sipariş oluştururken ZORUNLU olan kural (frontend satır 2161).</summary>
    public bool PlanEslesiyor => PlanToplam == KalemToplam;
}
