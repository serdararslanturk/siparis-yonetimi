namespace Acr.Filo.Domain.Entities.Auth;

/// <summary>SQL: dbo.Permissions</summary>
public class Permission
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Group { get; set; } = null!;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// Yetki anahtarları. Bu liste db/03-seed.sql içindeki Permissions MERGE bloğuyla
/// BİREBİR aynı olmalıdır — tools/check-consistency.py bunu makineyle doğrular.
/// </summary>
public static class Permissions
{
    public const string OrdersView        = "orders.view";
    public const string OrdersCreate      = "orders.create";
    public const string OrdersUpdate      = "orders.update";
    public const string OrdersDelete      = "orders.delete";
    public const string VehiclesUpdate    = "vehicles.update";
    public const string PaymentsView      = "payments.view";
    public const string PaymentsPlan      = "payments.plan";
    public const string PaymentsRecord    = "payments.record";
    public const string DefinitionsView   = "definitions.view";
    public const string DefinitionsManage = "definitions.manage";
    public const string ReportsView       = "reports.view";
    public const string ReportsExport     = "reports.export";
    public const string UsersManage       = "users.manage";
    public const string AuditView         = "audit.view";

    public static readonly IReadOnlyList<string> All = new[]
    {
        OrdersView, OrdersCreate, OrdersUpdate, OrdersDelete,
        VehiclesUpdate,
        PaymentsView, PaymentsPlan, PaymentsRecord,
        DefinitionsView, DefinitionsManage,
        ReportsView, ReportsExport,
        UsersManage, AuditView
    };
}
