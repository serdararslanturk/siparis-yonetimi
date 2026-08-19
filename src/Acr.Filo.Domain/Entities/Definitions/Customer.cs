using Acr.Filo.Domain.Common;
using Acr.Filo.Domain.Entities.Orders;

namespace Acr.Filo.Domain.Entities.Definitions;

/// <summary>
/// SQL: dbo.Customers.
/// Prototipte müşteri bir STRING'di (order.musteriUnvani) ve tanım listesinden
/// silinse bile seedMastersFromOrders() onu geri ekliyordu (satır 552-565).
/// Burada FK ile bağlandı: silme artık soft-delete, mevcut siparişler bozulmaz,
/// unvan düzeltmesi tüm siparişlere yansır.
/// </summary>
public class Customer : ConcurrentAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public string Unvan { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    /// <summary>Ödeme vadesi (gün). Örn. 45 = 45 gün vade. Boş olabilir.</summary>
    public int? VadeGun { get; set; }

    /// <summary>Müşteri temsilcisi (dbo.Temsilciler FK). Boş olabilir.</summary>
    public int? TemsilciId { get; set; }
    public Temsilci? Temsilci { get; set; }

    public ICollection<FleetOrder> Orders { get; set; } = new List<FleetOrder>();
}
