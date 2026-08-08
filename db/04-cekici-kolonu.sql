/* ============================================================================
   04-cekici-kolonu.sql  —  "Çekici kullanıldı" alanı (mevcut veritabanı için)

   Bu script MEVCUT ve DOLU bir AcrFilo veritabanını günceller:
     1) FleetOrderVehicles tablosuna CekiciKullanildi kolonu ekler (varsayılan 0)
     2) vw_VehicleStatus view'ını yeni kolonu içerecek şekilde günceller
     3) (Varsa) CorrelationId kolonlarını VARCHAR(64)'e genişletir

   IDEMPOTENT: iki kez çalıştırılırsa zarar vermez, veri silmez.

   Çalıştırma:
     sqlcmd -S . -E -d AcrFilo -I -i db\04-cekici-kolonu.sql -b
   ============================================================================ */
SET NOCOUNT ON;
GO

/* --- 1) Kolon --- */
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.FleetOrderVehicles') AND name = 'CekiciKullanildi')
BEGIN
    ALTER TABLE dbo.FleetOrderVehicles
        ADD CekiciKullanildi BIT NOT NULL
            CONSTRAINT DF_FOV_CekiciKullanildi DEFAULT(0);
    PRINT '  + CekiciKullanildi kolonu eklendi (varsayilan 0)';
END
ELSE
    PRINT '  = CekiciKullanildi kolonu zaten var';
GO

/* --- 2) CorrelationId genisletme (canli hata duzeltmesi, tekrar calisirsa zararsiz) --- */
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.LoginAuditLogs') AND name = 'CorrelationId' AND max_length < 64)
BEGIN
    ALTER TABLE dbo.LoginAuditLogs ALTER COLUMN CorrelationId VARCHAR(64) NULL;
    PRINT '  + LoginAuditLogs.CorrelationId -> VARCHAR(64)';
END
GO
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.AuditLogs') AND name = 'CorrelationId' AND max_length < 64)
BEGIN
    ALTER TABLE dbo.AuditLogs ALTER COLUMN CorrelationId VARCHAR(64) NULL;
    PRINT '  + AuditLogs.CorrelationId -> VARCHAR(64)';
END
GO

/* --- 3) View guncelleme (yeni kolon raporlara girsin) --- */
CREATE OR ALTER VIEW dbo.vw_VehicleStatus
AS
SELECT
    v.Id                AS VehicleId,
    v.OrderId,
    v.LineId,
    o.SiparisNo,
    o.CustomerId,
    c.Unvan             AS MusteriUnvani,
    l.SupplierId,
    s.Unvan             AS TedarikciUnvani,
    l.BrandId,
    b.Ad                AS Marka,
    l.Model,
    v.PlakaNo,
    v.TedarikTarihi,
    v.TedarikYeri,
    v.CekiciKullanildi,
    v.PlanlananTeslim,
    v.TeslimYeri,
    v.TeslimAlindi,
    v.GerceklesenTeslim,
    v.IkameVerildi,
    v.IkamePlaka,
    v.IkameIadeTarihi,
    ssh.YapilanAdet     AS SshYapilanAdet,
    CAST(CASE WHEN ssh.YapilanAdet = 4 THEN 1 ELSE 0 END AS BIT) AS SshTamam,
    /* Frontend vehicleStatus()+fieldStatus() (satır 442/430) ile BİREBİR.
       KRİTİK: durum "kaç adım yapıldı"ya DEĞİL, EKSİK adımların PlanlananTeslim'e
       göre en kötü durumuna bağlıdır. Bir adım bile eksik + teslim tarihi geçmiş
       => 'overdue'. Bu, tests/LogicParity ile C# ve JS'e karşı doğrulanmıştır.
       gün farkı: PlanlananTeslim - bugün (frontend daysUntil ile aynı işaret). */
    CASE
        WHEN v.GerceklesenTeslim IS NOT NULL THEN 'done'
        /* En az bir adım eksik mi? (EksikVar=1) */
        WHEN ssh.EksikVar = 1 AND v.PlanlananTeslim IS NULL THEN 'neutral'
        WHEN ssh.EksikVar = 1 AND DATEDIFF(DAY, CAST(SYSDATETIME() AS DATE), v.PlanlananTeslim) <  0 THEN 'overdue'
        WHEN ssh.EksikVar = 1 AND DATEDIFF(DAY, CAST(SYSDATETIME() AS DATE), v.PlanlananTeslim) <= 3 THEN 'soon'
        WHEN ssh.EksikVar = 1 THEN 'neutral'
        /* Tüm adımlar tamam (EksikVar=0): */
        WHEN v.TeslimAlindi = 1 THEN 'ready'
        ELSE 'neutral'
    END                 AS Durum
FROM dbo.FleetOrderVehicles v
JOIN dbo.FleetOrders     o ON o.Id = v.OrderId AND o.IsDeleted = 0
JOIN dbo.FleetOrderLines l ON l.Id = v.LineId
JOIN dbo.Customers       c ON c.Id = o.CustomerId
LEFT JOIN dbo.Suppliers  s ON s.Id = l.SupplierId
LEFT JOIN dbo.Brands     b ON b.Id = l.BrandId
CROSS APPLY (
    /* Araç başına tam 4 SSH satırı garanti (uygulama oluştururken yazıyor).
       YapilanAdet: rapor kolonu; EksikVar: durum mantığı için. */
    SELECT
        SUM(CASE WHEN t.Yapildi = 1 THEN 1 ELSE 0 END) AS YapilanAdet,
        CAST(CASE WHEN SUM(CASE WHEN t.Yapildi = 1 THEN 1 ELSE 0 END) < 4 THEN 1 ELSE 0 END AS BIT) AS EksikVar
    FROM dbo.VehicleSshTasks t
    WHERE t.VehicleId = v.Id
) ssh;
GO

PRINT '04-cekici-kolonu.sql tamamlandi.';
GO
