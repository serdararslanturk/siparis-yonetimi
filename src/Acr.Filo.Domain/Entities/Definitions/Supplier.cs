using Acr.Filo.Domain.Common;
using Acr.Filo.Domain.Entities.Orders;

namespace Acr.Filo.Domain.Entities.Definitions;

/// <summary>SQL: dbo.Suppliers. Prototip karşılığı: masters.suppliers[] (string).</summary>
public class Supplier : ConcurrentAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public string Unvan { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<FleetOrderLine> Lines { get; set; } = new List<FleetOrderLine>();
}
