using Acr.Filo.Domain.Entities.Orders;
using Acr.Filo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acr.Filo.Infrastructure.Persistence.Configurations;

/* Konfigürasyonlar entity başına ayrı dosya yerine katman başına gruplandı;
   16 küçük dosya yerine 4 okunur dosya. db/01-schema.sql ile birebir eşleşmeleri
   tools/check-consistency.py tarafından makineyle doğrulanır. */

public class FleetOrderConfiguration : IEntityTypeConfiguration<FleetOrder>
{
    public void Configure(EntityTypeBuilder<FleetOrder> e)
    {
        e.ToTable("FleetOrders");
        e.HasKey(x => x.Id);

        e.Property(x => x.SiparisNo).HasColumnType("varchar(20)").IsRequired();
        e.Property(x => x.OlusturmaTarihi).HasColumnType("date").IsRequired();
        e.Property(x => x.IsDeleted).HasDefaultValue(false);
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.DeletedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.RowVersion).IsRowVersion();

        e.HasIndex(x => x.SiparisNo).IsUnique().HasDatabaseName("UX_FleetOrders_SiparisNo");

        e.HasOne(x => x.Customer).WithMany(c => c.Orders)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);

        // Soft-delete: silinmiş siparişler hiçbir sorguda görünmez.
        // Yönetici "geri al" senaryosu için IgnoreQueryFilters() kullanılır.
        e.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class FleetOrderLineConfiguration : IEntityTypeConfiguration<FleetOrderLine>
{
    public void Configure(EntityTypeBuilder<FleetOrderLine> e)
    {
        e.ToTable("FleetOrderLines");
        e.HasKey(x => x.Id);

        e.Property(x => x.Model).HasColumnType("nvarchar(100)").IsRequired();
        e.Property(x => x.Adet).IsRequired();
        e.Property(x => x.BirimBedel).HasColumnType("decimal(18,2)").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.RowVersion).IsRowVersion();

        e.HasOne(x => x.Order).WithMany(o => o.Lines)
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Brand).WithMany(b => b.Lines)
            .HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Supplier).WithMany(s => s.Lines)
            .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);

        // Hesaplanan alanlar DB'ye yazılmaz (vw_LinePaymentSummary karşılığı).
        e.Ignore(x => x.KalemToplam);
        e.Ignore(x => x.PlanToplam);
        e.Ignore(x => x.OdenenToplam);
        e.Ignore(x => x.KalanTutar);
        e.Ignore(x => x.PlanEslesiyor);

        e.HasQueryFilter(x => !x.Order.IsDeleted);
    }
}

public class FleetOrderPaymentPlanConfiguration : IEntityTypeConfiguration<FleetOrderPaymentPlan>
{
    public void Configure(EntityTypeBuilder<FleetOrderPaymentPlan> e)
    {
        e.ToTable("FleetOrderPaymentPlans");
        e.HasKey(x => x.Id);

        e.Property(x => x.PlanTarihi).HasColumnType("date").IsRequired();
        e.Property(x => x.Tutar).HasColumnType("decimal(18,2)").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");

        e.HasOne(x => x.Line).WithMany(l => l.PaymentPlans)
            .HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Cascade);

        e.HasQueryFilter(x => !x.Line.Order.IsDeleted);
    }
}

public class FleetOrderPaymentConfiguration : IEntityTypeConfiguration<FleetOrderPayment>
{
    public void Configure(EntityTypeBuilder<FleetOrderPayment> e)
    {
        e.ToTable("FleetOrderPayments");
        e.HasKey(x => x.Id);

        e.Property(x => x.OdemeTarihi).HasColumnType("date").IsRequired();
        e.Property(x => x.Tutar).HasColumnType("decimal(18,2)").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");

        e.HasOne(x => x.Line).WithMany(l => l.Payments)
            .HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Cascade);

        e.HasQueryFilter(x => !x.Line.Order.IsDeleted);
    }
}

public class FleetOrderVehicleConfiguration : IEntityTypeConfiguration<FleetOrderVehicle>
{
    public void Configure(EntityTypeBuilder<FleetOrderVehicle> e)
    {
        e.ToTable("FleetOrderVehicles");
        e.HasKey(x => x.Id);

        e.Property(x => x.PlakaNo).HasColumnType("nvarchar(15)");
        e.Property(x => x.TedarikTarihi).HasColumnType("date");
        e.Property(x => x.TedarikYeri).HasColumnType("nvarchar(200)");
        e.Property(x => x.PlanlananTeslim).HasColumnType("date");
        e.Property(x => x.TeslimYeri).HasColumnType("nvarchar(200)");
        e.Property(x => x.TeslimAlindi).HasDefaultValue(false);
        e.Property(x => x.TeslimAlinmaTarihi).HasColumnType("date");
        e.Property(x => x.GerceklesenTeslim).HasColumnType("date");
        e.Property(x => x.IkameVerildi).HasDefaultValue(false);
        e.Property(x => x.IkameTarihi).HasColumnType("date");
        e.Property(x => x.IkamePlaka).HasColumnType("nvarchar(15)");
        e.Property(x => x.IkameIadeTarihi).HasColumnType("date");
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.RowVersion).IsRowVersion();

        // OrderId üzerinden CASCADE YOK: Order→Lines→Vehicles ile birlikte
        // çoklu cascade yolu oluşur ve SQL Server bunu reddeder.
        e.HasOne(x => x.Order).WithMany(o => o.Vehicles)
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Line).WithMany(l => l.Vehicles)
            .HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Cascade);

        e.HasQueryFilter(x => !x.Order.IsDeleted);
    }
}

public class VehicleSshTaskConfiguration : IEntityTypeConfiguration<VehicleSshTask>
{
    public void Configure(EntityTypeBuilder<VehicleSshTask> e)
    {
        e.ToTable("VehicleSshTasks");
        e.HasKey(x => x.Id);

        // KRİTİK: enum ToString() 'Plaka' yazar, frontend 'plaka' bekler.
        // Açık converter olmadan CHECK constraint CI collation sayesinde geçer
        // ama DB'ye yanlış kasada değer yazılır ve JSON eşleşmez.
        var sshConverter = new ValueConverter<SshTaskType, string>(
            v => SshTaskTypes.ToDb(v),
            v => SshTaskTypes.FromDb(v));

        e.Property(x => x.TaskType)
            .HasConversion(sshConverter)
            .HasColumnType("varchar(8)")
            .IsRequired();

        e.Property(x => x.Yapildi).HasDefaultValue(false);
        e.Property(x => x.Tarih).HasColumnType("date");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");

        e.HasIndex(x => new { x.VehicleId, x.TaskType })
            .IsUnique().HasDatabaseName("UQ_VehicleSshTasks");

        e.HasOne(x => x.Vehicle).WithMany(v => v.SshTasks)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        e.HasQueryFilter(x => !x.Vehicle.Order.IsDeleted);
    }
}
