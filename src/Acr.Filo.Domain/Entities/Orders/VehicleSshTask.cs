using Acr.Filo.Domain.Enums;

namespace Acr.Filo.Domain.Entities.Orders;

/// <summary>
/// SQL: dbo.VehicleSshTasks. Prototip: vehicle.ssh.{plaka|hgs|gps|utts} = {yapildi, tarih}.
/// Araç başına TAM 4 satır (UNIQUE(VehicleId, TaskType) + araç oluşturulurken 4'ü birden yazılır).
///
/// PROTOTİP HATASI #3'ÜN ŞEMA SEVİYESİNDE KAPATILMASI:
/// Prototipte vehicleStatus() .tarih'e (satır 444), orderSSHSummary() .yapildi'ya (satır 1426)
/// bakıyordu; kutuyu işaretleyip tarihi silince ikisi çelişiyordu.
/// Burada CK_VST_YapildiTarih ile "Yapildi=1 ⇔ Tarih dolu" garanti altına alındı.
/// </summary>
public class VehicleSshTask
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public SshTaskType TaskType { get; set; }
    public bool Yapildi { get; set; }
    public DateOnly? Tarih { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public FleetOrderVehicle Vehicle { get; set; } = null!;

    /// <summary>Adımı tamamla. Tarih verilmezse bugün (prototip satır 1981 ile aynı davranış).</summary>
    public void Tamamla(DateOnly? tarih, DateOnly today)
    {
        Yapildi = true;
        Tarih = tarih ?? Tarih ?? today;
    }

    /// <summary>Adımı geri al. CK_VST_YapildiTarih gereği tarih de temizlenmek ZORUNDA.</summary>
    public void GeriAl()
    {
        Yapildi = false;
        Tarih = null;
    }
}
