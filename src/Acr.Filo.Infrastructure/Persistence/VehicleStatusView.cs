namespace Acr.Filo.Infrastructure.Persistence;

/// <summary>dbo.vw_VehicleStatus — keyless. Durum SQL'de hesaplanır (parity ile doğrulandı).</summary>
public sealed class VehicleStatusView
{
    public int VehicleId { get; set; }
    public int OrderId { get; set; }
    public int LineId { get; set; }
    public string SiparisNo { get; set; } = "";
    public int CustomerId { get; set; }
    public string MusteriUnvani { get; set; } = "";
    public int SupplierId { get; set; }
    public string? TedarikciUnvani { get; set; }
    public int BrandId { get; set; }
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public string? PlakaNo { get; set; }
    public DateOnly? TedarikTarihi { get; set; }
    public string? TedarikYeri { get; set; }
    public bool CekiciKullanildi { get; set; }
    public DateOnly? PlanlananTeslim { get; set; }
    public string? TeslimYeri { get; set; }
    public bool TeslimAlindi { get; set; }
    public DateOnly? GerceklesenTeslim { get; set; }
    public bool IkameVerildi { get; set; }
    public string? IkamePlaka { get; set; }
    public DateOnly? IkameIadeTarihi { get; set; }
    public int SshYapilanAdet { get; set; }
    public bool SshTamam { get; set; }
    public string Durum { get; set; } = "";
}

/// <summary>dbo.vw_LinePaymentSummary — keyless.</summary>
public sealed class LinePaymentSummaryView
{
    public int LineId { get; set; }
    public int OrderId { get; set; }
    public int SupplierId { get; set; }
    public decimal KalemToplam { get; set; }
    public decimal PlanToplam { get; set; }
    public decimal OdenenToplam { get; set; }
    public decimal KalanTutar { get; set; }
    public bool PlanEslesiyor { get; set; }
}
