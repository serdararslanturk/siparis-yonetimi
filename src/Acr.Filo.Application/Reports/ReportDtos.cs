using Acr.Filo.Application.Common;

namespace Acr.Filo.Application.Reports;

// Dört takvim + dashboard. Hepsi sunucu taraflı, vw_VehicleStatus üzerinden.
public sealed record CalendarQuery(DateOnly? From, DateOnly? To, int? CustomerId, int? SupplierId, bool HideDelivered = true);

public sealed record DeliveryCalendarRow(int VehicleId, int OrderId, string SiparisNo, string MusteriUnvani,
    string? TedarikciUnvani, string Marka, string Model, string? PlakaNo,
    DateOnly? PlanlananTeslim, string? TeslimYeri, bool TeslimAlindi, string Durum, IReadOnlyList<string> EksikSsh);

public sealed record SupplyCalendarRow(int VehicleId, string SiparisNo, string MusteriUnvani,
    string? TedarikciUnvani, string Marka, string Model, string? PlakaNo, DateOnly? TedarikTarihi, string? TedarikYeri,
    bool CekiciKullanildi = false);

public sealed record SshCalendarRow(int VehicleId, string SiparisNo, string MusteriUnvani, string Marka, string Model,
    string? PlakaNo, DateOnly? PlanlananTeslim, IReadOnlyList<string> EksikSsh, string Durum,
    bool CekiciKullanildi = false);

public sealed record PaymentCalendarRow(int LineId, int OrderId, string SiparisNo, string MusteriUnvani,
    string? TedarikciUnvani, DateOnly PlanTarihi, decimal PlanTutar, decimal OdenenToplam, decimal KalanTutar);

public sealed record DashboardDto(
    int ToplamSiparis, int ToplamArac,
    int Overdue, int Soon, int Neutral, int Ready, int Done,
    int BekleyenTeslim, int YaklasanTeslim, int GecikenTeslim,
    decimal ToplamTutar, decimal OdenenTutar, decimal KalanTutar);

public interface IReportService
{
    Task<Result<DashboardDto>> DashboardAsync(CancellationToken ct);
    Task<Result<IReadOnlyList<DeliveryCalendarRow>>> DeliveryCalendarAsync(CalendarQuery q, CancellationToken ct);
    Task<Result<IReadOnlyList<SupplyCalendarRow>>> SupplyCalendarAsync(CalendarQuery q, CancellationToken ct);
    Task<Result<IReadOnlyList<SshCalendarRow>>> SshCalendarAsync(CalendarQuery q, CancellationToken ct);
    Task<Result<IReadOnlyList<PaymentCalendarRow>>> PaymentCalendarAsync(CalendarQuery q, CancellationToken ct);
    /// <summary>CSV (BOM'lu, ; ayraçlı) — frontend'in mevcut Excel davranışıyla aynı format.</summary>
    Task<Result<byte[]>> ExportDeliveryCsvAsync(CalendarQuery q, CancellationToken ct);
}
