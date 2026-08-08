namespace Acr.Filo.Domain.Common;

/// <summary>Ortak denetim alanları. SQL karşılığı: CreatedAt/CreatedBy/UpdatedAt/UpdatedBy.</summary>
public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

/// <summary>Eş zamanlı güncelleme çakışması için rowversion taşıyan kayıtlar.</summary>
public abstract class ConcurrentAuditableEntity : AuditableEntity
{
    /// <summary>SQL: ROWVERSION. EF bunu concurrency token olarak kullanır.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>Soft-delete edilebilen kayıtlar. Fiziksel silme yapılmaz.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
