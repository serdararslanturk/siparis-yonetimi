using Acr.Filo.Domain.Common;
using Acr.Filo.Domain.Enums;

namespace Acr.Filo.Domain.Entities.Orders;

/// <summary>
/// SQL: dbo.FleetOrderVehicles. Prototip karşılığı: order.vehicles[] elemanı.
/// Kalemdeki her adet için BİR araç satırı üretilir (prototip satır 2179).
/// </summary>
public class FleetOrderVehicle : ConcurrentAuditableEntity
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int LineId { get; set; }

    /// <summary>Prototipte '' idi; burada NULL. Plaka benzersiz DEĞİL (ikame/iade senaryosu).</summary>
    public string? PlakaNo { get; set; }

    // --- Tedarikçi bilgileri
    public DateOnly? TedarikTarihi { get; set; }
    public string? TedarikYeri { get; set; }

    /// <summary>Tedarik sırasında çekici kullanıldı mı? (raporlarda gösterilir)</summary>
    public bool CekiciKullanildi { get; set; }

    // --- Müşteriye teslim bilgileri
    public DateOnly? PlanlananTeslim { get; set; }
    public string? TeslimYeri { get; set; }

    // --- Teslim alındı (teslimattan ÖNCE gelmek zorunda)
    public bool TeslimAlindi { get; set; }
    public DateOnly? TeslimAlinmaTarihi { get; set; }

    // --- Teslimat yapıldı
    public DateOnly? GerceklesenTeslim { get; set; }

    // --- İkame araç
    public bool IkameVerildi { get; set; }
    public DateOnly? IkameTarihi { get; set; }
    public string? IkamePlaka { get; set; }
    public DateOnly? IkameIadeTarihi { get; set; }

    public FleetOrder Order { get; set; } = null!;
    public FleetOrderLine Line { get; set; } = null!;
    public ICollection<VehicleSshTask> SshTasks { get; set; } = new List<VehicleSshTask>();

    /// <summary>
    /// Frontend vehicleStatus() (satır 442) ve SQL vw_VehicleStatus ile BİREBİR aynı mantık.
    /// Üçünün aynı kalması kritik — tools/check-consistency.py bunu doğrular.
    /// </summary>
    public string Durum(DateOnly today)
    {
        // Frontend vehicleStatus() (satır 442) ile BİREBİR aynı.
        if (GerceklesenTeslim is not null) return VehicleStatuses.Done;

        // Her SSH adımı, PlanlananTeslim'e göre AYRI AYRI değerlendirilir
        // (frontend: ssh[k].tarih ile fieldStatus(planlananTeslim, tarih)).
        // Bir aracın 4 adımından herhangi biri eksik ve teslim tarihi geçmişse
        // araç 'overdue' olur — kaç adımın yapıldığı değil, EKSİK OLANIN durumu belirler.
        var worst = VehicleStatuses.Done;
        foreach (var t in SshTaskTypes.All)
        {
            var task = SshTasks.FirstOrDefault(x => x.TaskType == t);
            var done = task?.Yapildi == true;               // tarih değil, Yapildi bayrağı (CK ile tarih zaten senkron)
            var fs = FieldStatus(PlanlananTeslim, done, today);
            if (VehicleStatuses.Rank(fs) > VehicleStatuses.Rank(worst)) worst = fs;
        }

        if (worst == VehicleStatuses.Done)
            return TeslimAlindi ? VehicleStatuses.Ready : VehicleStatuses.Neutral;
        return worst;
    }

    /// <summary>Frontend fieldStatus(due, done) (satır 430) birebir karşılığı.</summary>
    private static string FieldStatus(DateOnly? due, bool done, DateOnly today)
    {
        if (done) return VehicleStatuses.Done;
        if (due is null) return VehicleStatuses.Neutral;
        var d = due.Value.DayNumber - today.DayNumber;
        if (d < 0) return VehicleStatuses.Overdue;
        if (d <= 3) return VehicleStatuses.Soon;
        return VehicleStatuses.Neutral;
    }

    /// <summary>Teslim takviminde gösterilen eksik işlemler listesi (frontend vehicleMissingItems, satır 639).</summary>
    public IReadOnlyList<string> EksikSshAdimlari() =>
        SshTasks.Where(t => !t.Yapildi)
                .OrderBy(t => (int)t.TaskType)
                .Select(t => SshTaskTypes.ToDb(t.TaskType))
                .ToList();
}
