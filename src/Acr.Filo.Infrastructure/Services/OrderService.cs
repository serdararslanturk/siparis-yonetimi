using Acr.Filo.Application.Abstractions;
using Acr.Filo.Application.Common;
using Acr.Filo.Application.Orders;
using Acr.Filo.Domain.Entities.Definitions;
using Acr.Filo.Domain.Entities.Orders;
using Acr.Filo.Domain.Enums;
using Acr.Filo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acr.Filo.Infrastructure.Services;

public sealed class OrderService : IOrderService
{
    private readonly FiloDbContext _db;
    private readonly IDateTimeProvider _clock;

    public OrderService(FiloDbContext db, IDateTimeProvider clock)
    {
        _db = db; _clock = clock;
    }

    // ---------------------------------------------------------------- LIST
    public async Task<Result<PagedResult<OrderListItemDto>>> ListAsync(OrderListQuery q, CancellationToken ct)
    {
        var query = _db.FleetOrders
            .Include(o => o.Customer)
            .Include(o => o.Lines)
            .Include(o => o.Vehicles).ThenInclude(v => v.SshTasks)
            .AsNoTracking()
            .AsQueryable();

        if (q.CustomerId is { } cid) query = query.Where(o => o.CustomerId == cid);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(o => o.SiparisNo.Contains(s) || o.Customer.Unvan.Contains(s)
                || o.Vehicles.Any(v => v.PlakaNo != null && v.PlakaNo.Contains(s)));
        }

        // Sekme filtresi durum-bazlı olduğu için önce belleğe alıp hesaplıyoruz.
        // Filo hacminde (yüzler-binler) bu güvenli; on-binlerde materyalize view'a geçilir.
        var all = await query.OrderByDescending(o => o.OlusturmaTarihi).ThenByDescending(o => o.Id).ToListAsync(ct);

        var mapped = all.Select(ToListItem).ToList();
        mapped = q.Tab switch
        {
            "teslim" => mapped.Where(m => m.AracSayisi > 0 && m.TeslimEdilenSayisi == m.AracSayisi).ToList(),
            "acik"   => mapped.Where(m => !(m.AracSayisi > 0 && m.TeslimEdilenSayisi == m.AracSayisi)).ToList(),
            _        => mapped, // "tumu"
        };

        var total = mapped.Count;
        var page = mapped.Skip(q.Skip).Take(q.PageSize).ToList();
        return Result<PagedResult<OrderListItemDto>>.Success(new PagedResult<OrderListItemDto>
        {
            Items = page, Total = total, Page = q.Page, PageSize = q.PageSize
        });
    }

    // ---------------------------------------------------------------- GET
    public async Task<Result<OrderDetailDto>> GetAsync(int id, CancellationToken ct)
    {
        var o = await LoadFull(id, ct);
        return o is null
            ? Result<OrderDetailDto>.Fail("Sipariş bulunamadı.", ResultCode.NotFound)
            : Result<OrderDetailDto>.Success(ToDetail(o));
    }

    // ---------------------------------------------------------------- CREATE
    public async Task<Result<OrderDetailDto>> CreateAsync(CreateOrderRequest req, CancellationToken ct)
    {
        var check = ValidateLines(req.Lines);
        if (check is not null) return Result<OrderDetailDto>.Fail(check, ResultCode.Validation);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var custResult = await ResolveCustomerAsync(req.CustomerId, req.MusteriUnvani, ct);
            if (!custResult.Ok) return Result<OrderDetailDto>.Fail(custResult.Error!, custResult.Code);
            var customerId = custResult.Value;

            // Sipariş no ÜRETİMİ transaction içinde (çakışmasız).
            var siparisNo = await _db.NextSiparisNoAsync(ct);

            var order = new FleetOrder
            {
                SiparisNo = siparisNo,
                CustomerId = customerId,
                OlusturmaTarihi = req.OlusturmaTarihi ?? _clock.Today,
            };
            _db.FleetOrders.Add(order);
            await _db.SaveChangesAsync(ct); // order.Id

            foreach (var l in req.Lines)
            {
                var lineResult = await BuildLineAsync(order.Id, l, ct);
                if (!lineResult.Ok) { await tx.RollbackAsync(ct); return Result<OrderDetailDto>.Fail(lineResult.Error!, lineResult.Code); }
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var full = await LoadFull(order.Id, ct);
            return Result<OrderDetailDto>.Success(ToDetail(full!));
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            return Result<OrderDetailDto>.Fail("Sipariş kaydedilemedi: " + Root(ex), ResultCode.Conflict);
        }
    }

    // ---------------------------------------------------------------- UPDATE (başlık)
    public async Task<Result<OrderDetailDto>> UpdateAsync(int id, UpdateOrderRequest req, CancellationToken ct)
    {
        var order = await _db.FleetOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return Result<OrderDetailDto>.Fail("Sipariş bulunamadı.", ResultCode.NotFound);

        var cust = await ResolveCustomerAsync(req.CustomerId, req.MusteriUnvani, ct);
        if (!cust.Ok) return Result<OrderDetailDto>.Fail(cust.Error!, cust.Code);

        order.CustomerId = cust.Value;
        order.OlusturmaTarihi = req.OlusturmaTarihi;
        _db.Entry(order).Property(o => o.RowVersion).OriginalValue = req.RowVersion;

        var save = await SaveWithConcurrency(ct);
        if (!save.Ok) return Result<OrderDetailDto>.Fail(save.Error!, save.Code);

        var full = await LoadFull(id, ct);
        return Result<OrderDetailDto>.Success(ToDetail(full!));
    }

    // ---------------------------------------------------------------- DELETE (soft)
    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var order = await _db.FleetOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return Result.Fail("Sipariş bulunamadı.", ResultCode.NotFound);

        // Prototip hard-delete yapıyordu; burada soft-delete + audit.
        order.IsDeleted = true;
        order.DeletedAt = _clock.UtcNow;
        order.DeletedBy = null; // interceptor CurrentUser'dan dolduramaz (silme audit'i ayrı yazılır)
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------------------------------------------------------------- ADD LINES
    public async Task<Result<OrderDetailDto>> AddLinesAsync(int id, AddLineRequest req, CancellationToken ct)
    {
        var order = await _db.FleetOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return Result<OrderDetailDto>.Fail("Sipariş bulunamadı.", ResultCode.NotFound);

        var check = ValidateLines(req.Lines);
        if (check is not null) return Result<OrderDetailDto>.Fail(check, ResultCode.Validation);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var l in req.Lines)
            {
                var r = await BuildLineAsync(id, l, ct);
                if (!r.Ok) { await tx.RollbackAsync(ct); return Result<OrderDetailDto>.Fail(r.Error!, r.Code); }
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var full = await LoadFull(id, ct);
            return Result<OrderDetailDto>.Success(ToDetail(full!));
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            return Result<OrderDetailDto>.Fail("Kalem eklenemedi: " + Root(ex), ResultCode.Conflict);
        }
    }

    // ---------------------------------------------------------------- UPDATE LINE
    public async Task<Result<OrderDetailDto>> UpdateLineAsync(int orderId, int lineId, UpdateLineRequest req, CancellationToken ct)
    {
        var line = await _db.FleetOrderLines
            .Include(l => l.Vehicles)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.OrderId == orderId, ct);
        if (line is null) return Result<OrderDetailDto>.Fail("Kalem bulunamadı.", ResultCode.NotFound);

        var brand = await ResolveBrandAsync(req.BrandId, req.Marka, ct);
        if (!brand.Ok) return Result<OrderDetailDto>.Fail(brand.Error!, brand.Code);
        var supplier = await ResolveSupplierAsync(req.SupplierId, req.TedarikciUnvani, ct);
        if (!supplier.Ok) return Result<OrderDetailDto>.Fail(supplier.Error!, supplier.Code);

        if (req.Adet < 1) return Result<OrderDetailDto>.Fail("Adet en az 1 olmalı.", ResultCode.Validation);
        if (req.BirimBedel <= 0) return Result<OrderDetailDto>.Fail("Birim bedel 0'dan büyük olmalı.", ResultCode.Validation);

        line.BrandId = brand.Value;
        line.SupplierId = supplier.Value;
        line.Model = req.Model.Trim();
        line.BirimBedel = req.BirimBedel;
        _db.Entry(line).Property(l => l.RowVersion).OriginalValue = req.RowVersion;

        // Adet değişimi: araç sayısını senkronla (frontend'de adet düşünce araç siliniyordu).
        var mevcut = line.Vehicles.Count;
        if (req.Adet > mevcut)
        {
            for (var i = 0; i < req.Adet - mevcut; i++)
                _db.FleetOrderVehicles.Add(NewVehicle(orderId, lineId, line.Model, brand.Value, supplier.Value,
                    null, null, null, null, line.Vehicles.FirstOrDefault()?.CekiciKullanildi ?? false));
        }
        else if (req.Adet < mevcut)
        {
            // Fazla araçları kaldır — plakası/teslimi olanları KORU, boşları sil.
            var silinebilir = line.Vehicles
                .Where(v => v.PlakaNo is null && v.GerceklesenTeslim is null && !v.TeslimAlindi)
                .Take(mevcut - req.Adet).ToList();
            if (silinebilir.Count < mevcut - req.Adet)
                return Result<OrderDetailDto>.Fail(
                    "Adet azaltılamıyor: plakası atanmış veya teslim edilmiş araçlar var.", ResultCode.Conflict);
            _db.FleetOrderVehicles.RemoveRange(silinebilir);
        }
        line.Adet = req.Adet;

        var save = await SaveWithConcurrency(ct);
        if (!save.Ok) return Result<OrderDetailDto>.Fail(save.Error!, save.Code);
        var full = await LoadFull(orderId, ct);
        return Result<OrderDetailDto>.Success(ToDetail(full!));
    }

    // ---------------------------------------------------------------- UPDATE PLANS
    /// <summary>
    /// Kalemin ödeme planını topluca değiştirir: eski plan satırları silinir,
    /// gelenler yazılır. Sipariş oluşturulduktan sonra plan revizesi için.
    /// Kural (prototiple aynı): plan toplamı = adet × birim bedel.
    /// </summary>
    public async Task<Result<OrderDetailDto>> UpdatePlansAsync(int orderId, int lineId, UpdatePlansRequest req, CancellationToken ct)
    {
        var line = await _db.FleetOrderLines
            .Include(l => l.PaymentPlans)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.OrderId == orderId, ct);
        if (line is null) return Result<OrderDetailDto>.Fail("Kalem bulunamadı.", ResultCode.NotFound);

        var gelen = (req.Planlar ?? new List<PaymentPlanInput>())
            .Where(p => p.Tutar != 0)
            .ToList();

        var kalemToplam = line.Adet * line.BirimBedel;
        var planToplam = gelen.Sum(p => p.Tutar);
        if (planToplam != kalemToplam)
            return Result<OrderDetailDto>.Fail(
                $"Ödeme planı toplamı ({planToplam:N0} ₺) araç bedeliyle ({kalemToplam:N0} ₺) eşleşmiyor.",
                ResultCode.Validation);

        // Eskileri sil, yenileri yaz (tam değiştirme).
        _db.FleetOrderPaymentPlans.RemoveRange(line.PaymentPlans);
        foreach (var p in gelen)
            _db.FleetOrderPaymentPlans.Add(new FleetOrderPaymentPlan
            {
                LineId = line.Id,
                PlanTarihi = p.Tarih,
                Tutar = p.Tutar
            });

        await _db.SaveChangesAsync(ct);
        var full = await LoadFull(orderId, ct);
        return Result<OrderDetailDto>.Success(ToDetail(full!));
    }

    // ---------------------------------------------------------------- DELETE LINE
    public async Task<Result> DeleteLineAsync(int orderId, int lineId, CancellationToken ct)
    {
        var line = await _db.FleetOrderLines.FirstOrDefaultAsync(l => l.Id == lineId && l.OrderId == orderId, ct);
        if (line is null) return Result.Fail("Kalem bulunamadı.", ResultCode.NotFound);
        // Cascade: araçlar, planlar, ödemeler DB tarafında siliniyor.
        _db.FleetOrderLines.Remove(line);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------------------------------------------------------------- UPDATE VEHICLE
    public async Task<Result<VehicleDto>> UpdateVehicleAsync(int orderId, int vehicleId, UpdateVehicleRequest req, CancellationToken ct)
    {
        var v = await _db.FleetOrderVehicles
            .Include(x => x.SshTasks)
            .FirstOrDefaultAsync(x => x.Id == vehicleId && x.OrderId == orderId, ct);
        if (v is null) return Result<VehicleDto>.Fail("Araç bulunamadı.", ResultCode.NotFound);

        // İş kuralı: teslim alınmadan teslimat yapılamaz (frontend satır 1860 + CK_FOV_TeslimSirasi).
        if (req.GerceklesenTeslim is not null && !req.TeslimAlindi)
            return Result<VehicleDto>.Fail("Teslim alınmadan teslimat işaretlenemez.", ResultCode.Conflict);

        v.PlakaNo = Trim(req.PlakaNo);
        v.TedarikTarihi = req.TedarikTarihi;
        v.TedarikYeri = Trim(req.TedarikYeri);
        v.CekiciKullanildi = req.CekiciKullanildi;
        v.PlanlananTeslim = req.PlanlananTeslim;
        v.TeslimYeri = Trim(req.TeslimYeri);
        v.TeslimAlindi = req.TeslimAlindi;
        v.TeslimAlinmaTarihi = req.TeslimAlindi ? (req.TeslimAlinmaTarihi ?? v.TeslimAlinmaTarihi ?? _clock.Today) : null;
        v.GerceklesenTeslim = req.GerceklesenTeslim;

        // İkame: verilmediyse tüm alanlar temizlenir (CK_FOV_IkameTarihi).
        v.IkameVerildi = req.Ikame.Verildi;
        v.IkameTarihi = req.Ikame.Verildi ? req.Ikame.Tarih : null;
        v.IkamePlaka = req.Ikame.Verildi ? Trim(req.Ikame.Plaka) : null;
        v.IkameIadeTarihi = req.Ikame.Verildi ? req.Ikame.IadeTarihi : null;

        // SSH: bağımlılık zinciri (frontend satır 1984). Plaka yapılmadan diğerleri olamaz.
        ApplySsh(v, SshTaskType.Plaka, req.Ssh.Plaka);
        ApplySsh(v, SshTaskType.Hgs, req.Ssh.Hgs);
        ApplySsh(v, SshTaskType.Gps, req.Ssh.Gps);
        ApplySsh(v, SshTaskType.Utts, req.Ssh.Utts);

        var plakaDone = v.SshTasks.First(t => t.TaskType == SshTaskType.Plaka).Yapildi;
        if (!plakaDone && v.SshTasks.Any(t => t.TaskType != SshTaskType.Plaka && t.Yapildi))
            return Result<VehicleDto>.Fail("HGS/GPS/UTTS için önce Plaka adımı tamamlanmalı.", ResultCode.Conflict);

        _db.Entry(v).Property(x => x.RowVersion).OriginalValue = req.RowVersion;
        var save = await SaveWithConcurrency(ct);
        if (!save.Ok) return Result<VehicleDto>.Fail(save.Error!, save.Code);

        return Result<VehicleDto>.Success(ToVehicle(v));
    }

    // ---------------------------------------------------------------- PAYMENTS (payments.record)
    public async Task<Result<PaymentDto>> AddPaymentAsync(int orderId, int lineId, AddPaymentRequest req, CancellationToken ct)
    {
        var line = await _db.FleetOrderLines
            .Include(l => l.Payments)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.OrderId == orderId, ct);
        if (line is null) return Result<PaymentDto>.Fail("Kalem bulunamadı.", ResultCode.NotFound);
        if (req.Tutar <= 0) return Result<PaymentDto>.Fail("Ödeme tutarı 0'dan büyük olmalı.", ResultCode.Validation);

        var p = new FleetOrderPayment { LineId = lineId, OdemeTarihi = req.Tarih, Tutar = req.Tutar };
        _db.FleetOrderPayments.Add(p);
        await _db.SaveChangesAsync(ct);
        return Result<PaymentDto>.Success(new PaymentDto(p.Id, p.OdemeTarihi, p.Tutar));
    }

    public async Task<Result> DeletePaymentAsync(int orderId, int lineId, int paymentId, CancellationToken ct)
    {
        var p = await _db.FleetOrderPayments
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.LineId == lineId && x.Line.OrderId == orderId, ct);
        if (p is null) return Result.Fail("Ödeme bulunamadı.", ResultCode.NotFound);
        _db.FleetOrderPayments.Remove(p);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------------------------------------------------------------- VEHICLE HISTORY
    public async Task<Result<IReadOnlyList<VehicleEventDto>>> VehicleHistoryAsync(int orderId, int vehicleId, CancellationToken ct)
    {
        var eid = vehicleId.ToString();
        var rows = await _db.AuditLogs
            .Where(a => a.EntityName == nameof(FleetOrderVehicle) && a.EntityId == eid)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Join(_db.Users.IgnoreQueryFilters(), a => a.UserId, u => (int?)u.Id,
                (a, u) => new VehicleEventDto(a.ColumnName ?? a.Action, a.OldValue, a.NewValue, a.OccurredAtUtc, u.Email))
            .ToListAsync(ct);
        return Result<IReadOnlyList<VehicleEventDto>>.Success(rows);
    }

    // ================================================================ helpers

    private void ApplySsh(FleetOrderVehicle v, SshTaskType type, Application.Orders.SshStepDto step)
    {
        var task = v.SshTasks.FirstOrDefault(t => t.TaskType == type);
        if (task is null)
        {
            task = new VehicleSshTask { VehicleId = v.Id, TaskType = type };
            v.SshTasks.Add(task);
        }
        if (step.Yapildi) task.Tamamla(step.Tarih, _clock.Today);
        else task.GeriAl();
        task.UpdatedAt = _clock.UtcNow;

        // Plaka geri alınırsa diğer üçü de sıfırlanır (frontend satır 1984).
        if (type == SshTaskType.Plaka && !step.Yapildi)
            foreach (var other in v.SshTasks.Where(t => t.TaskType != SshTaskType.Plaka))
                other.GeriAl();
    }

    private async Task<Result<int>> BuildLineAsync(int orderId, CreateLineRequest l, CancellationToken ct)
    {
        var brand = await ResolveBrandAsync(l.BrandId, l.Marka, ct);
        if (!brand.Ok) return Result<int>.Fail(brand.Error!, brand.Code);
        var supplier = await ResolveSupplierAsync(l.SupplierId, l.TedarikciUnvani, ct);
        if (!supplier.Ok) return Result<int>.Fail(supplier.Error!, supplier.Code);

        var line = new FleetOrderLine
        {
            OrderId = orderId, BrandId = brand.Value, SupplierId = supplier.Value,
            Model = l.Model.Trim(), Adet = l.Adet, BirimBedel = l.BirimBedel,
        };
        _db.FleetOrderLines.Add(line);
        await _db.SaveChangesAsync(ct); // line.Id

        foreach (var p in l.Planlar.Where(p => p.Tutar != 0))
            _db.FleetOrderPaymentPlans.Add(new FleetOrderPaymentPlan { LineId = line.Id, PlanTarihi = p.Tarih, Tutar = p.Tutar });

        // Her adet için bir araç + 4 SSH satırı (frontend for i<adet).
        for (var i = 0; i < l.Adet; i++)
            _db.FleetOrderVehicles.Add(NewVehicle(orderId, line.Id, line.Model, brand.Value, supplier.Value,
                l.TedarikTarihi, l.TedarikYeri, l.PlanlananTeslim, l.TeslimYeri, l.CekiciKullanildi));

        return Result<int>.Success(line.Id);
    }

    private FleetOrderVehicle NewVehicle(int orderId, int lineId, string model, int brandId, int supplierId,
        DateOnly? tedarik, string? tedarikYeri, DateOnly? plan, string? teslimYeri, bool cekici = false)
    {
        var v = new FleetOrderVehicle
        {
            OrderId = orderId, LineId = lineId,
            TedarikTarihi = tedarik, TedarikYeri = Trim(tedarikYeri),
            PlanlananTeslim = plan, TeslimYeri = Trim(teslimYeri),
            CekiciKullanildi = cekici,
        };
        foreach (var t in SshTaskTypes.All)
            v.SshTasks.Add(new VehicleSshTask { TaskType = t, Yapildi = false, Tarih = null });
        return v;
    }

    private static string? ValidateLines(IReadOnlyList<CreateLineRequest> lines)
    {
        if (lines is null || lines.Count == 0) return "En az bir araç kalemi girin.";
        foreach (var l in lines)
        {
            var etiket = string.IsNullOrWhiteSpace(l.Model) ? "Kalem" : l.Model.Trim();
            if (string.IsNullOrWhiteSpace(l.Model)) return "Model gerekli.";
            if (l.Adet < 1) return $"{etiket}: adet en az 1 olmalı.";
            if (l.BirimBedel <= 0) return $"{etiket}: birim bedel 0'dan büyük olmalı.";
            if (string.IsNullOrWhiteSpace(l.TeslimYeri)) return $"{etiket}: teslim yeri gerekli.";
            if (l.TedarikTarihi is { } td && l.PlanlananTeslim < td)
                return $"{etiket}: tedarik tarihi, müşteriye teslim tarihinden sonra olamaz.";

            // Prototip kuralı: plan toplamı = kalem toplamı (satır 2161).
            var kalemToplam = l.Adet * l.BirimBedel;
            var planToplam = l.Planlar?.Sum(p => p.Tutar) ?? 0;
            if (planToplam != kalemToplam)
                return $"{etiket}: ödeme planı toplamı ({planToplam:N0} ₺) araç bedeliyle ({kalemToplam:N0} ₺) eşleşmiyor.";
        }
        return null;
    }

    // ---- Tanım çözümleme: id verilmişse onu, yoksa unvandan bul/oluştur ----
    private async Task<Result<int>> ResolveCustomerAsync(int? id, string? unvan, CancellationToken ct)
    {
        if (id is { } cid)
            return await _db.Customers.AnyAsync(c => c.Id == cid, ct)
                ? Result<int>.Success(cid) : Result<int>.Fail("Müşteri bulunamadı.", ResultCode.NotFound);
        if (string.IsNullOrWhiteSpace(unvan)) return Result<int>.Fail("Müşteri unvanı gerekli.", ResultCode.Validation);
        var ad = unvan.Trim();
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Unvan == ad, ct);
        if (existing is not null) return Result<int>.Success(existing.Id);
        var c2 = new Customer { Unvan = ad };
        _db.Customers.Add(c2);
        await _db.SaveChangesAsync(ct);
        return Result<int>.Success(c2.Id);
    }

    private async Task<Result<int>> ResolveSupplierAsync(int? id, string? unvan, CancellationToken ct)
    {
        if (id is { } sid)
            return await _db.Suppliers.AnyAsync(s => s.Id == sid, ct)
                ? Result<int>.Success(sid) : Result<int>.Fail("Tedarikçi bulunamadı.", ResultCode.NotFound);
        if (string.IsNullOrWhiteSpace(unvan)) return Result<int>.Fail("Tedarikçi unvanı gerekli.", ResultCode.Validation);
        var ad = unvan.Trim();
        var existing = await _db.Suppliers.FirstOrDefaultAsync(s => s.Unvan == ad, ct);
        if (existing is not null) return Result<int>.Success(existing.Id);
        var s2 = new Supplier { Unvan = ad };
        _db.Suppliers.Add(s2);
        await _db.SaveChangesAsync(ct);
        return Result<int>.Success(s2.Id);
    }

    private async Task<Result<int>> ResolveBrandAsync(int? id, string? ad, CancellationToken ct)
    {
        if (id is { } bid)
            return await _db.Brands.AnyAsync(b => b.Id == bid, ct)
                ? Result<int>.Success(bid) : Result<int>.Fail("Marka bulunamadı.", ResultCode.NotFound);
        if (string.IsNullOrWhiteSpace(ad)) return Result<int>.Fail("Marka gerekli.", ResultCode.Validation);
        var m = ad.Trim();
        var existing = await _db.Brands.FirstOrDefaultAsync(b => b.Ad == m, ct);
        if (existing is not null) return Result<int>.Success(existing.Id);
        var b2 = new Brand { Ad = m };
        _db.Brands.Add(b2);
        await _db.SaveChangesAsync(ct);
        return Result<int>.Success(b2.Id);
    }

    private async Task<Result> SaveWithConcurrency(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); return Result.Success(); }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail("Bu kayıt başka bir kullanıcı tarafından değiştirilmiş. Sayfayı yenileyin.", ResultCode.Conflict);
        }
        catch (DbUpdateException ex)
        {
            return Result.Fail("Kaydedilemedi: " + Root(ex), ResultCode.Conflict);
        }
    }

    private async Task<FleetOrder?> LoadFull(int id, CancellationToken ct) =>
        await _db.FleetOrders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Brand)
            .Include(o => o.Lines).ThenInclude(l => l.Supplier)
            .Include(o => o.Lines).ThenInclude(l => l.PaymentPlans)
            .Include(o => o.Lines).ThenInclude(l => l.Payments)
            .Include(o => o.Vehicles).ThenInclude(v => v.SshTasks)
            .Include(o => o.Vehicles).ThenInclude(v => v.Line).ThenInclude(l => l.Brand)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    private static string Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null! : s.Trim();
    private static string Root(Exception ex) { while (ex.InnerException is not null) ex = ex.InnerException; return ex.Message; }

    // ---- eşleyiciler ----
    private OrderListItemDto ToListItem(FleetOrder o)
    {
        var today = _clock.Today;
        var durum = VehicleStatuses.Worst(o.Vehicles.Select(v => v.Durum(today)));
        var teslim = o.Vehicles.Count(v => v.GerceklesenTeslim is not null);
        var toplam = o.Lines.Sum(l => l.Adet * l.BirimBedel);
        var odenen = o.Lines.Sum(l => l.Payments.Sum(p => p.Tutar));
        return new OrderListItemDto(o.Id, o.SiparisNo, o.CustomerId, o.Customer.Unvan,
            o.OlusturmaTarihi, o.Vehicles.Count, teslim, durum, toplam, odenen);
    }

    private OrderDetailDto ToDetail(FleetOrder o)
    {
        var lines = o.Lines.Select(l => new OrderLineDto(
            l.Id, l.BrandId, l.Brand.Ad, l.Model, l.Adet, l.BirimBedel, l.SupplierId, l.Supplier.Unvan,
            l.KalemToplam, l.PlanToplam, l.OdenenToplam, l.KalanTutar, l.PlanEslesiyor,
            l.PaymentPlans.OrderBy(p => p.PlanTarihi).Select(p => new PaymentPlanDto(p.Id, p.PlanTarihi, p.Tutar)).ToList(),
            l.Payments.OrderBy(p => p.OdemeTarihi).Select(p => new PaymentDto(p.Id, p.OdemeTarihi, p.Tutar)).ToList(),
            l.RowVersion)).ToList();
        var vehicles = o.Vehicles.Select(ToVehicle).ToList();
        return new OrderDetailDto(o.Id, o.SiparisNo, o.CustomerId, o.Customer.Unvan,
            o.OlusturmaTarihi, lines, vehicles, o.RowVersion);
    }

    private VehicleDto ToVehicle(FleetOrderVehicle v)
    {
        SshStepDto Step(SshTaskType t)
        {
            var task = v.SshTasks.FirstOrDefault(x => x.TaskType == t);
            return new SshStepDto(task?.Yapildi ?? false, task?.Tarih);
        }
        var marka = v.Line?.Brand?.Ad ?? "";
        return new VehicleDto(
            v.Id, v.LineId, marka, v.Line?.Model ?? "", v.PlakaNo,
            v.TedarikTarihi, v.TedarikYeri, v.PlanlananTeslim, v.TeslimYeri,
            v.TeslimAlindi, v.TeslimAlinmaTarihi, v.GerceklesenTeslim,
            new IkameDto(v.IkameVerildi, v.IkameTarihi, v.IkamePlaka, v.IkameIadeTarihi),
            new SshDto(Step(SshTaskType.Plaka), Step(SshTaskType.Hgs), Step(SshTaskType.Gps), Step(SshTaskType.Utts)),
            v.Durum(_clock.Today), v.RowVersion, v.CekiciKullanildi);
    }
}
