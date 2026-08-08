using Acr.Filo.Domain.Common;
using Acr.Filo.Domain.Entities.Orders;

namespace Acr.Filo.Domain.Entities.Definitions;

/// <summary>SQL: dbo.Brands. Prototip karşılığı: masters.brands[] (string).
/// Model hâlâ serbest metin — kapsam kararı gereği model ağacı kurulmadı.</summary>
public class Brand : ConcurrentAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<FleetOrderLine> Lines { get; set; } = new List<FleetOrderLine>();
}
