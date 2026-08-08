using System.Globalization;
using System.Text;
using Acr.Filo.Application.Common;
using Acr.Filo.Application.Reports;
using Acr.Filo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acr.Filo.Infrastructure.Services;

public sealed class ReportService : IReportService
{
    private readonly FiloDbContext _db;
    public ReportService(FiloDbContext db) => _db = db;

    private IQueryable<VehicleStatusView> Filtered(CalendarQuery q)
    {
        var query = _db.VehicleStatusView.AsNoTracking().AsQueryable();
        if (q.CustomerId is { } c) query = query.Where(v => v.CustomerId == c);
        if (q.SupplierId is { } s) query = query.Where(v => v.SupplierId == s);
        return query;
    }

    public async Task<Result<DashboardDto>> DashboardAsync(CancellationToken ct)
    {
        var v = _db.VehicleStatusView.AsNoTracking();
        var byStatus = await v.GroupBy(x => x.Durum).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        int St(string k) => byStatus.FirstOrDefault(x => x.Key == k)?.N ?? 0;

        var toplamArac = await v.CountAsync(ct);
        var toplamSiparis = await v.Select(x => x.OrderId).Distinct().CountAsync(ct);

        var pay = await _db.LinePaymentSummaryView.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new { T = g.Sum(x => x.KalemToplam), O = g.Sum(x => x.OdenenToplam) })
            .FirstOrDefaultAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var bekleyen = await v.CountAsync(x => x.GerceklesenTeslim == null, ct);
        var geciken = await v.CountAsync(x => x.GerceklesenTeslim == null && x.PlanlananTeslim != null && x.PlanlananTeslim < today, ct);
        var yaklasan = await v.CountAsync(x => x.GerceklesenTeslim == null && x.PlanlananTeslim != null
            && x.PlanlananTeslim >= today && x.PlanlananTeslim <= today.AddDays(3), ct);

        return Result<DashboardDto>.Success(new DashboardDto(
            toplamSiparis, toplamArac,
            St("overdue"), St("soon"), St("neutral"), St("ready"), St("done"),
            bekleyen, yaklasan, geciken,
            pay?.T ?? 0, pay?.O ?? 0, Math.Max((pay?.T ?? 0) - (pay?.O ?? 0), 0)));
    }

    public async Task<Result<IReadOnlyList<DeliveryCalendarRow>>> DeliveryCalendarAsync(CalendarQuery q, CancellationToken ct)
    {
        var query = Filtered(q).Where(v => v.PlanlananTeslim != null);
        if (q.HideDelivered) query = query.Where(v => v.GerceklesenTeslim == null);
        if (q.From is { } f) query = query.Where(v => v.PlanlananTeslim >= f);
        if (q.To is { } t) query = query.Where(v => v.PlanlananTeslim <= t);

        var rows = await query.OrderBy(v => v.PlanlananTeslim).ToListAsync(ct);
        // Eksik SSH adımları ayrı sorgu yerine tek seferde:
        var ids = rows.Select(r => r.VehicleId).ToList();
        var eksikMap = await _db.VehicleSshTasks.AsNoTracking()
            .Where(t => ids.Contains(t.VehicleId) && !t.Yapildi)
            .Select(t => new { t.VehicleId, t.TaskType })
            .ToListAsync(ct);

        var list = rows.Select(v => new DeliveryCalendarRow(
            v.VehicleId, v.OrderId, v.SiparisNo, v.MusteriUnvani, v.TedarikciUnvani, v.Marka, v.Model, v.PlakaNo,
            v.PlanlananTeslim, v.TeslimYeri, v.TeslimAlindi, v.Durum,
            eksikMap.Where(e => e.VehicleId == v.VehicleId)
                    .Select(e => Acr.Filo.Domain.Enums.SshTaskTypes.ToDb(e.TaskType)).ToList())).ToList();
        return Result<IReadOnlyList<DeliveryCalendarRow>>.Success(list);
    }

    public async Task<Result<IReadOnlyList<SupplyCalendarRow>>> SupplyCalendarAsync(CalendarQuery q, CancellationToken ct)
    {
        var query = Filtered(q).Where(v => v.TedarikTarihi != null);
        if (q.HideDelivered) query = query.Where(v => v.GerceklesenTeslim == null);
        if (q.From is { } f) query = query.Where(v => v.TedarikTarihi >= f);
        if (q.To is { } t) query = query.Where(v => v.TedarikTarihi <= t);
        var rows = await query.OrderBy(v => v.TedarikTarihi).Select(v => new SupplyCalendarRow(
            v.VehicleId, v.SiparisNo, v.MusteriUnvani, v.TedarikciUnvani, v.Marka, v.Model, v.PlakaNo,
            v.TedarikTarihi, v.TedarikYeri, v.CekiciKullanildi)).ToListAsync(ct);
        return Result<IReadOnlyList<SupplyCalendarRow>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<SshCalendarRow>>> SshCalendarAsync(CalendarQuery q, CancellationToken ct)
    {
        // SSH takvimi: eksik adımı olan araçlar (SshTamam=false), teslim edilmemiş.
        var query = Filtered(q).Where(v => !v.SshTamam && v.GerceklesenTeslim == null);
        if (q.From is { } f) query = query.Where(v => v.PlanlananTeslim >= f);
        if (q.To is { } t) query = query.Where(v => v.PlanlananTeslim <= t);
        var rows = await query.OrderBy(v => v.PlanlananTeslim).ToListAsync(ct);

        var ids = rows.Select(r => r.VehicleId).ToList();
        var eksikMap = await _db.VehicleSshTasks.AsNoTracking()
            .Where(t => ids.Contains(t.VehicleId) && !t.Yapildi)
            .Select(t => new { t.VehicleId, t.TaskType }).ToListAsync(ct);

        var list = rows.Select(v => new SshCalendarRow(
            v.VehicleId, v.SiparisNo, v.MusteriUnvani, v.Marka, v.Model, v.PlakaNo, v.PlanlananTeslim,
            eksikMap.Where(e => e.VehicleId == v.VehicleId)
                    .Select(e => Acr.Filo.Domain.Enums.SshTaskTypes.ToDb(e.TaskType)).ToList(), v.Durum,
            v.CekiciKullanildi)).ToList();
        return Result<IReadOnlyList<SshCalendarRow>>.Success(list);
    }

    public async Task<Result<IReadOnlyList<PaymentCalendarRow>>> PaymentCalendarAsync(CalendarQuery q, CancellationToken ct)
    {
        // Ödeme takvimi: plan tarihlerine göre. Kalem bazlı özet view ile birleştir.
        var plans = _db.FleetOrderPaymentPlans.AsNoTracking()
            .Include(p => p.Line).ThenInclude(l => l.Order).ThenInclude(o => o.Customer)
            .Include(p => p.Line).ThenInclude(l => l.Supplier)
            .AsQueryable();
        if (q.From is { } f) plans = plans.Where(p => p.PlanTarihi >= f);
        if (q.To is { } t) plans = plans.Where(p => p.PlanTarihi <= t);
        if (q.CustomerId is { } c) plans = plans.Where(p => p.Line.Order.CustomerId == c);
        if (q.SupplierId is { } s) plans = plans.Where(p => p.Line.SupplierId == s);

        var raw = await plans.OrderBy(p => p.PlanTarihi).ToListAsync(ct);
        var lineIds = raw.Select(p => p.LineId).Distinct().ToList();
        var sums = await _db.LinePaymentSummaryView.AsNoTracking()
            .Where(v => lineIds.Contains(v.LineId)).ToListAsync(ct);

        var list = raw.Select(p =>
        {
            var sum = sums.FirstOrDefault(v => v.LineId == p.LineId);
            return new PaymentCalendarRow(p.LineId, p.Line.OrderId, p.Line.Order.SiparisNo,
                p.Line.Order.Customer.Unvan, p.Line.Supplier.Unvan, p.PlanTarihi, p.Tutar,
                sum?.OdenenToplam ?? 0, sum?.KalanTutar ?? 0);
        }).ToList();
        return Result<IReadOnlyList<PaymentCalendarRow>>.Success(list);
    }

    public async Task<Result<byte[]>> ExportDeliveryCsvAsync(CalendarQuery q, CancellationToken ct)
    {
        var r = await DeliveryCalendarAsync(q, ct);
        if (!r.Ok) return Result<byte[]>.Fail(r.Error!, r.Code);

        // Frontend'in mevcut formatı: ; ayraçlı, BOM'lu, Türkçe. Excel sorunsuz açar.
        var sb = new StringBuilder();
        sb.AppendLine("Sipariş No;Müşteri;Tedarikçi;Marka;Model;Plaka;Planlanan Teslim;Teslim Yeri;Durum;Eksik SSH");
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        foreach (var v in r.Value!)
        {
            string C(string? x) => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"";
            sb.Append(C(v.SiparisNo)).Append(';').Append(C(v.MusteriUnvani)).Append(';')
              .Append(C(v.TedarikciUnvani)).Append(';').Append(C(v.Marka)).Append(';').Append(C(v.Model)).Append(';')
              .Append(C(v.PlakaNo)).Append(';').Append(C(v.PlanlananTeslim?.ToString("dd.MM.yyyy", tr))).Append(';')
              .Append(C(v.TeslimYeri)).Append(';').Append(C(v.Durum)).Append(';')
              .Append(C(string.Join(", ", v.EksikSsh))).Append('\n');
        }
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        return Result<byte[]>.Success(bom.Concat(body).ToArray());
    }
}
