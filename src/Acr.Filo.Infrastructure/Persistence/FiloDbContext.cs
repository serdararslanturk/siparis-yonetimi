using Acr.Filo.Domain.Entities.Auth;
using Acr.Filo.Domain.Entities.Definitions;
using Acr.Filo.Domain.Entities.Orders;
using Acr.Filo.Domain.Entities.System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Acr.Filo.Infrastructure.Persistence;

public class FiloDbContext : DbContext
{
    public FiloDbContext(DbContextOptions<FiloDbContext> options) : base(options) { }

    // Kimlik
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginAuditLog> LoginAuditLogs => Set<LoginAuditLog>();

    // Tanımlar
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Temsilci> Temsilciler => Set<Temsilci>();

    // Sipariş
    public DbSet<FleetOrder> FleetOrders => Set<FleetOrder>();
    public DbSet<FleetOrderLine> FleetOrderLines => Set<FleetOrderLine>();
    public DbSet<FleetOrderPaymentPlan> FleetOrderPaymentPlans => Set<FleetOrderPaymentPlan>();
    public DbSet<FleetOrderPayment> FleetOrderPayments => Set<FleetOrderPayment>();
    public DbSet<FleetOrderVehicle> FleetOrderVehicles => Set<FleetOrderVehicle>();
    public DbSet<VehicleSshTask> VehicleSshTasks => Set<VehicleSshTask>();

    // Sistem
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    // Rapor görünümleri (keyless)
    public DbSet<VehicleStatusView> VehicleStatusView => Set<VehicleStatusView>();
    public DbSet<LinePaymentSummaryView> LinePaymentSummaryView => Set<LinePaymentSummaryView>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(FiloDbContext).Assembly);

        // View'lar: keyless, tabloya yazılmaz. Durum SQL'de hesaplanır.
        b.Entity<VehicleStatusView>().HasNoKey().ToView("vw_VehicleStatus");
        b.Entity<LinePaymentSummaryView>().HasNoKey().ToView("vw_LinePaymentSummary");
    }

    /// <summary>
    /// Sipariş numarası üretimi. EF ile DEĞİL, dbo.sp_NextFleetOrderNo ile yapılır:
    /// UPDLOCK+HOLDLOCK ile satır serileştirilir, iki eşzamanlı sipariş aynı numarayı alamaz.
    /// ÇAĞIRAN AÇIK BİR TRANSACTION İÇİNDE OLMALIDIR — aksi halde numara alınır ama
    /// sipariş yazılamazsa boşluk oluşur.
    /// </summary>
    public async Task<string> NextSiparisNoAsync(CancellationToken ct = default)
    {
        var outParam = new SqlParameter
        {
            ParameterName = "@SiparisNo",
            SqlDbType = System.Data.SqlDbType.VarChar,
            Size = 20,
            Direction = System.Data.ParameterDirection.Output
        };
        var yearParam = new SqlParameter("@Year", System.Data.SqlDbType.SmallInt)
        {
            Value = DBNull.Value
        };

        await Database.ExecuteSqlRawAsync(
            "EXEC dbo.sp_NextFleetOrderNo @Year, @SiparisNo OUTPUT",
            new object[] { yearParam, outParam }, ct);

        var value = outParam.Value as string;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Siparis numarasi uretilemedi (sp_NextFleetOrderNo bos dondu).");
        return value;
    }
}
