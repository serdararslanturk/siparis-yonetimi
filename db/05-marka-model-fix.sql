/* ============================================================================
   05-marka-model-fix.sql — Takvim raporlarında kaybolan araçlar düzeltmesi

   SORUN: vw_VehicleStatus view'ında Brands/Suppliers INNER JOIN idi. Bir aracın
   markası veya tedarikçisi silinmişse (ya da BrandId/SupplierId NULL ise), o araç
   view'dan TAMAMEN düşüyordu → tedarik/teslim/SSH takvimlerinde hiç görünmüyordu.

   ÇÖZÜM: Brands ve Suppliers artık LEFT JOIN. Araç her durumda görünür; markası
   yoksa marka alanı boş kalır ama araç kaybolmaz.

   IDEMPOTENT: CREATE OR ALTER — kaç kez çalıştırılırsa çalıştırılsın güvenli.

   Çalıştırma:  sqlcmd -S . -E -d AcrFilo -I -i db\05-marka-model-fix.sql -b
   ============================================================================ */
SET NOCOUNT ON;
GO

/* --- TANI: kaç araç markası/tedarikçisi eksik? (bilgi amaçlı) --- */
DECLARE @markasiz INT, @tedariksiz INT;
SELECT @markasiz = COUNT(*) FROM dbo.FleetOrderLines l
    WHERE l.BrandId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Brands b WHERE b.Id = l.BrandId);
SELECT @tedariksiz = COUNT(*) FROM dbo.FleetOrderLines l
    WHERE l.SupplierId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Suppliers s WHERE s.Id = l.SupplierId);
PRINT '  Markasi eksik/silinmis kalem sayisi: ' + CAST(@markasiz AS VARCHAR(10));
PRINT '  Tedarikcisi eksik/silinmis kalem sayisi: ' + CAST(@tedariksiz AS VARCHAR(10));
PRINT '  (Bu kalemlerdeki araclar artik takvimde GORUNECEK.)';
GO

/* --- View'i LEFT JOIN'li haliyle yeniden olustur --- */
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

PRINT '05-marka-model-fix.sql tamamlandi. Takvim raporlarinda tum araclar gorunur.';
GO
