using Acr.Filo.Domain.Common;

namespace Acr.Filo.Domain.Entities.Definitions;

/// <summary>
/// SQL: dbo.Temsilciler. Müşteri temsilcisi (satış temsilcisi) tanım listesi.
/// Brands ile aynı yapıda: serbest ad, soft-delete, filtered unique.
/// Müşteriye FK ile bağlanır (Customer.TemsilciId).
/// </summary>
public class Temsilci : ConcurrentAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
