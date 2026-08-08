/* =====================================================================
   ACR Filo — Indexler
   Her index, uygulamadaki GERÇEK bir sorguya dayanır. Spekülatif index yok.
   Çalıştırma: sqlcmd -S [SQL_SERVER_ADI] -d [DATABASE_ADI] -i 02-indexes.sql -b
   ===================================================================== */
SET NOCOUNT ON;
GO

/* ---- Tanımlar: benzersizlik + aktif liste ---------------------------
   Filtered unique: soft-delete edilmiş kayıt aynı unvanla yeniden açılabilsin.
   Turkish_CI_AS sayesinde 'Acarlar' ile 'ACARLAR' aynı sayılır — frontend'in
   toLocaleLowerCase('tr-TR') karşılaştırmasıyla (satır 575) birebir aynı. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Customers_Unvan')
CREATE UNIQUE NONCLUSTERED INDEX UX_Customers_Unvan
    ON dbo.Customers (Unvan) WHERE IsDeleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Suppliers_Unvan')
CREATE UNIQUE NONCLUSTERED INDEX UX_Suppliers_Unvan
    ON dbo.Suppliers (Unvan) WHERE IsDeleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Brands_Ad')
CREATE UNIQUE NONCLUSTERED INDEX UX_Brands_Ad
    ON dbo.Brands (Ad) WHERE IsDeleted = 0;
GO

/* ---- Kullanıcı: login lookup ---------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_Email')
CREATE UNIQUE NONCLUSTERED INDEX UX_Users_Email
    ON dbo.Users (Email) WHERE IsDeleted = 0;
GO

/* ---- RefreshToken: her /auth/refresh çağrısı bu index'i kullanır ----- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_RefreshTokens_TokenHash')
CREATE UNIQUE NONCLUSTERED INDEX UX_RefreshTokens_TokenHash
    ON dbo.RefreshTokens (TokenHash);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_UserId_Expires')
CREATE NONCLUSTERED INDEX IX_RefreshTokens_UserId_Expires
    ON dbo.RefreshTokens (UserId, ExpiresAtUtc) WHERE RevokedAtUtc IS NULL;
GO

/* ---- Sipariş numarası benzersizliği --------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_FleetOrders_SiparisNo')
CREATE UNIQUE NONCLUSTERED INDEX UX_FleetOrders_SiparisNo
    ON dbo.FleetOrders (SiparisNo);
GO

/* ---- Sipariş listesi: sekmeler + arama (frontend renderOrderList) ----
   Liste her zaman IsDeleted=0 + tarihe göre azalan sıralı geliyor. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FleetOrders_Liste')
CREATE NONCLUSTERED INDEX IX_FleetOrders_Liste
    ON dbo.FleetOrders (OlusturmaTarihi DESC, Id DESC)
    INCLUDE (SiparisNo, CustomerId)
    WHERE IsDeleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FleetOrders_CustomerId')
CREATE NONCLUSTERED INDEX IX_FleetOrders_CustomerId
    ON dbo.FleetOrders (CustomerId) WHERE IsDeleted = 0;
GO

/* ---- Kalem → sipariş / tedarikçi ------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FleetOrderLines_OrderId')
CREATE NONCLUSTERED INDEX IX_FleetOrderLines_OrderId
    ON dbo.FleetOrderLines (OrderId) INCLUDE (BrandId, SupplierId, Model, Adet, BirimBedel);
GO
/* Tedarik takvimi ve Ödeme takvimi tedarikçiye göre filtreliyor
   (populateTedarikTedarikciFilter / payCalTedarikciFilter) */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FleetOrderLines_SupplierId')
CREATE NONCLUSTERED INDEX IX_FleetOrderLines_SupplierId
    ON dbo.FleetOrderLines (SupplierId) INCLUDE (OrderId);
GO

/* ---- Ödeme planı / ödemeler ------------------------------------------
   Ödeme takvimi PlanTarihi'ne göre gruplayıp sıralıyor
   (buildPaymentCalendarGroups). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FOPP_LineId')
CREATE NONCLUSTERED INDEX IX_FOPP_LineId
    ON dbo.FleetOrderPaymentPlans (LineId) INCLUDE (PlanTarihi, Tutar);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FOPP_PlanTarihi')
CREATE NONCLUSTERED INDEX IX_FOPP_PlanTarihi
    ON dbo.FleetOrderPaymentPlans (PlanTarihi) INCLUDE (LineId, Tutar);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FOP_LineId')
CREATE NONCLUSTERED INDEX IX_FOP_LineId
    ON dbo.FleetOrderPayments (LineId) INCLUDE (OdemeTarihi, Tutar);
GO

/* ---- Araçlar ---------------------------------------------------------
   Teslim takvimi: PlanlananTeslim'e göre grupla + teslim edilmişleri gizle
   (calHideDelivered varsayılan true) */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FOV_PlanlananTeslim')
CREATE NONCLUSTERED INDEX IX_FOV_PlanlananTeslim
    ON dbo.FleetOrderVehicles (PlanlananTeslim)
    INCLUDE (OrderId, LineId, PlakaNo, TeslimYeri, TeslimAlindi)
    WHERE GerceklesenTeslim IS NULL;
GO
/* Tedarik takvimi: TedarikTarihi'ne göre grupla */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FOV_TedarikTarihi')
CREATE NONCLUSTERED INDEX IX_FOV_TedarikTarihi
    ON dbo.FleetOrderVehicles (TedarikTarihi)
    INCLUDE (OrderId, LineId, PlakaNo, TedarikYeri)
    WHERE GerceklesenTeslim IS NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FOV_OrderId')
CREATE NONCLUSTERED INDEX IX_FOV_OrderId
    ON dbo.FleetOrderVehicles (OrderId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FOV_LineId')
CREATE NONCLUSTERED INDEX IX_FOV_LineId
    ON dbo.FleetOrderVehicles (LineId);
GO
/* Plaka araması (unifiedFilterPlaka). Plaka benzersiz DEĞİL: aynı plaka
   farklı siparişlerde geçebilir (ikame/iade senaryosu). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FOV_PlakaNo')
CREATE NONCLUSTERED INDEX IX_FOV_PlakaNo
    ON dbo.FleetOrderVehicles (PlakaNo) WHERE PlakaNo IS NOT NULL;
GO

/* ---- SSH: UQ_VehicleSshTasks (VehicleId, TaskType) zaten kapsıyor ----
   SSH Takvimi "eksik olanlar" sorgusu için ek filtered index: */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VST_Eksikler')
CREATE NONCLUSTERED INDEX IX_VST_Eksikler
    ON dbo.VehicleSshTasks (VehicleId) INCLUDE (TaskType) WHERE Yapildi = 0;
GO

/* ---- Audit: en sık sorgu "şu kaydın geçmişi" ve "şu tarih aralığı" --- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Entity')
CREATE NONCLUSTERED INDEX IX_AuditLogs_Entity
    ON dbo.AuditLogs (EntityName, EntityId, OccurredAtUtc DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_OccurredAt')
CREATE NONCLUSTERED INDEX IX_AuditLogs_OccurredAt
    ON dbo.AuditLogs (OccurredAtUtc DESC) INCLUDE (UserId, EntityName, [Action]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LoginAuditLogs_OccurredAt')
CREATE NONCLUSTERED INDEX IX_LoginAuditLogs_OccurredAt
    ON dbo.LoginAuditLogs (OccurredAtUtc DESC) INCLUDE (UserId, Succeeded, IpAddress);
GO

PRINT '02-indexes.sql tamamlandi.';
GO
