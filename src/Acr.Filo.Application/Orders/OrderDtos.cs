namespace Acr.Filo.Application.Orders;

// ============================================================================
// Sipariş DTO'ları. Frontend submitNewOrder() (satır ~2100) payload'ı ile
// BİREBİR eşleşir. Alan adları frontend'in JSON'ıyla aynı (camelCase serialize).
// ============================================================================

// ---- Liste satırı (özet) ----
public sealed record OrderListItemDto(
    int Id,
    string SiparisNo,
    int CustomerId,
    string MusteriUnvani,
    DateOnly OlusturmaTarihi,
    int AracSayisi,
    int TeslimEdilenSayisi,
    string GenelDurum,          // vehicleStatuses: overdue|soon|neutral|ready|done
    decimal ToplamTutar,
    decimal OdenenTutar);

// ---- Detay ----
public sealed record OrderDetailDto(
    int Id,
    string SiparisNo,
    int CustomerId,
    string MusteriUnvani,
    DateOnly OlusturmaTarihi,
    IReadOnlyList<OrderLineDto> VehicleLines,
    IReadOnlyList<VehicleDto> Vehicles,
    byte[] RowVersion);

public sealed record OrderLineDto(
    int Id,
    int BrandId,
    string Marka,
    string Model,
    int Adet,
    decimal BirimBedel,
    int SupplierId,
    string TedarikciUnvani,
    decimal KalemToplam,
    decimal PlanToplam,
    decimal OdenenToplam,
    decimal KalanTutar,
    bool PlanEslesiyor,
    IReadOnlyList<PaymentPlanDto> Planlar,
    IReadOnlyList<PaymentDto> Odemeler,
    byte[] RowVersion);

public sealed record PaymentPlanDto(int Id, DateOnly Tarih, decimal Tutar);
public sealed record PaymentDto(int Id, DateOnly Tarih, decimal Tutar);

public sealed record VehicleDto(
    int Id,
    int LineId,
    string Marka,
    string Model,
    string? PlakaNo,
    DateOnly? TedarikTarihi,
    string? TedarikYeri,
    DateOnly? PlanlananTeslim,
    string? TeslimYeri,
    bool TeslimAlindi,
    DateOnly? TeslimAlinmaTarihi,
    DateOnly? GerceklesenTeslim,
    IkameDto Ikame,
    SshDto Ssh,
    string Durum,               // hesaplanan
    byte[] RowVersion,
    bool CekiciKullanildi = false);

public sealed record IkameDto(bool Verildi, DateOnly? Tarih, string? Plaka, DateOnly? IadeTarihi);

/// <summary>SSH 4 adımı. Frontend: ssh.{plaka|hgs|gps|utts} = {yapildi, tarih}.</summary>
public sealed record SshDto(SshStepDto Plaka, SshStepDto Hgs, SshStepDto Gps, SshStepDto Utts);
public sealed record SshStepDto(bool Yapildi, DateOnly? Tarih);

// ============================================================================
// YAZMA (create/update) DTO'ları
// ============================================================================

/// <summary>Yeni sipariş. Frontend: {musteri, lines[]}. Sipariş no SUNUCUDA üretilir.</summary>
public sealed record CreateOrderRequest(
    string MusteriUnvani,                    // yoksa CustomerId; ikisinden biri
    int? CustomerId,
    DateOnly? OlusturmaTarihi,               // null → bugün (frontend todayISO())
    IReadOnlyList<CreateLineRequest> Lines);

/// <summary>Kalem + her adet için otomatik araç üretimi (frontend for i<adet döngüsü).</summary>
public sealed record CreateLineRequest(
    string? Marka,                           // yoksa BrandId
    int? BrandId,
    string Model,
    int Adet,
    decimal BirimBedel,
    string? TedarikciUnvani,                 // yoksa SupplierId
    int? SupplierId,
    DateOnly? TedarikTarihi,
    string? TedarikYeri,
    DateOnly PlanlananTeslim,                // zorunlu (frontend kuralı)
    string TeslimYeri,                       // zorunlu
    IReadOnlyList<PaymentPlanInput> Planlar,
    bool CekiciKullanildi = false);          // tedarikte cekici kullanildi mi

public sealed record PaymentPlanInput(DateOnly Tarih, decimal Tutar);

/// <summary>
/// Kalemin ödeme planını TOPLUCA değiştirir (eskiler silinir, gelenler yazılır).
/// Sipariş oluşturulduktan sonra plan tarih/tutar revizesi için.
/// </summary>
public sealed record UpdatePlansRequest(IReadOnlyList<PaymentPlanInput> Planlar);

/// <summary>Sipariş başlığı düzenleme (yalnızca müşteri + tarih).</summary>
public sealed record UpdateOrderRequest(int? CustomerId, string? MusteriUnvani, DateOnly OlusturmaTarihi, byte[] RowVersion);

/// <summary>Var olan siparişe kalem ekleme (frontend addLineTargetOrderId akışı).</summary>
public sealed record AddLineRequest(IReadOnlyList<CreateLineRequest> Lines);

public sealed record UpdateLineRequest(
    int? BrandId, string? Marka, string Model, int Adet, decimal BirimBedel,
    int? SupplierId, string? TedarikciUnvani, byte[] RowVersion);

// ---- Araç güncelleme (SSH, teslim, ikame) ----
public sealed record UpdateVehicleRequest(
    string? PlakaNo,
    DateOnly? TedarikTarihi, string? TedarikYeri,
    DateOnly? PlanlananTeslim, string? TeslimYeri,
    bool TeslimAlindi, DateOnly? TeslimAlinmaTarihi,
    DateOnly? GerceklesenTeslim,
    IkameDto Ikame,
    SshDto Ssh,
    byte[] RowVersion,
    bool CekiciKullanildi = false);

// ---- Ödeme (yalnızca payments.record) ----
public sealed record AddPaymentRequest(DateOnly Tarih, decimal Tutar);

// ---- Durum değişikliği geçmişi görünümü ----
public sealed record VehicleEventDto(string Alan, string? EskiDeger, string? YeniDeger, DateTime Tarih, string? Kullanici);
