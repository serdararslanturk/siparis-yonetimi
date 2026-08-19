using Acr.Filo.Domain.Entities.Definitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acr.Filo.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> e)
    {
        e.ToTable("Customers");
        e.HasKey(x => x.Id);
        e.Property(x => x.Unvan).HasColumnType("nvarchar(200)").IsRequired();
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.IsDeleted).HasDefaultValue(false);
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.RowVersion).IsRowVersion();
        // Müşteri temsilcisi + ödeme vadesi (gün). Migration 06.
        e.Property(x => x.VadeGun);
        e.Property(x => x.TemsilciId);
        e.HasOne(x => x.Temsilci).WithMany(t => t.Customers)
            .HasForeignKey(x => x.TemsilciId).OnDelete(DeleteBehavior.Restrict);
        // Filtered unique: soft-delete edilmiş unvan yeniden kullanılabilir.
        e.HasIndex(x => x.Unvan).IsUnique()
            .HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_Customers_Unvan");
        e.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TemsilciConfiguration : IEntityTypeConfiguration<Temsilci>
{
    public void Configure(EntityTypeBuilder<Temsilci> e)
    {
        e.ToTable("Temsilciler");
        e.HasKey(x => x.Id);
        e.Property(x => x.Ad).HasColumnType("nvarchar(100)").IsRequired();
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.IsDeleted).HasDefaultValue(false);
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.RowVersion).IsRowVersion();
        e.HasIndex(x => x.Ad).IsUnique()
            .HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_Temsilciler_Ad");
        e.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> e)
    {
        e.ToTable("Suppliers");
        e.HasKey(x => x.Id);
        e.Property(x => x.Unvan).HasColumnType("nvarchar(200)").IsRequired();
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.IsDeleted).HasDefaultValue(false);
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.RowVersion).IsRowVersion();
        e.HasIndex(x => x.Unvan).IsUnique()
            .HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_Suppliers_Unvan");
        e.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> e)
    {
        e.ToTable("Brands");
        e.HasKey(x => x.Id);
        e.Property(x => x.Ad).HasColumnType("nvarchar(100)").IsRequired();
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.IsDeleted).HasDefaultValue(false);
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.RowVersion).IsRowVersion();
        e.HasIndex(x => x.Ad).IsUnique()
            .HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_Brands_Ad");
        e.HasQueryFilter(x => !x.IsDeleted);
    }
}
