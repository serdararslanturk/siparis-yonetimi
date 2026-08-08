using Acr.Filo.Domain.Common;
using Acr.Filo.Domain.Entities.Definitions;

namespace Acr.Filo.Domain.Entities.Orders;

/// <summary>
/// SQL: dbo.FleetOrders. Prototip karşılığı: state.orders[] elemanı.
/// Prototipe göre iki fark:
///   1) SiparisNo eklendi — prototipte sipariş numarası hiç yoktu.
///   2) Silme SOFT-DELETE — prototipte kalıcı siliniyordu (deleteOrder, satır 1588).
/// </summary>
public class FleetOrder : ConcurrentAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    /// <summary>'SIP-2026-000123'. dbo.sp_NextFleetOrderNo tarafından üretilir.</summary>
    public string SiparisNo { get; set; } = null!;

    public int CustomerId { get; set; }

    /// <summary>İş tarihi (prototip: olusturmaTarihi). Saat/zaman dilimi taşımaz.</summary>
    public DateOnly OlusturmaTarihi { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<FleetOrderLine> Lines { get; set; } = new List<FleetOrderLine>();
    public ICollection<FleetOrderVehicle> Vehicles { get; set; } = new List<FleetOrderVehicle>();
}
