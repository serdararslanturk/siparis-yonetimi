/* =====================================================================
   ACR Filo Sipariş Yönetim Sistemi — Şema
   Hedef  : SQL Server 2019+
   Sıra   : 01-schema.sql → 02-indexes.sql → 03-seed.sql
   Çalıştırma: sqlcmd -S [SQL_SERVER_ADI] -d [DATABASE_ADI] -i 01-schema.sql -b
   NOT: Bu script idempotenttir; tekrar çalıştırılabilir, veri silmez.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---------------------------------------------------------------------
   COLLATION NOTU (önemli):
   Veritabanı Turkish_CI_AS ile oluşturulmalıdır (bkz. 00-create-database.sql).
   Gerekçe: frontend müşteri/tedarikçi/marka adlarını localeCompare(...,'tr')
   ile sıralıyor ve yinelenen kayıt kontrolünü toLocaleLowerCase('tr-TR')
   ile yapıyor. Turkish_CI_AS bu davranışla birebir örtüşür.

   TUZAK: Turkish_CI_AS altında 'I' ile 'i' EŞİT DEĞİLDİR (Türkçede 'I'→'ı',
   'İ'→'i' eşleşir). Bu yüzden e-posta / kullanıcı adı kolonları açıkça
   Latin1_General_100_CI_AS ile tanımlanmıştır; aksi halde 'INFO@x.com' ile
   'info@x.com' farklı kabul edilir ve login kırılır.
   --------------------------------------------------------------------- */

/* =====================================================================
   1. KİMLİK VE YETKİLENDİRME
   ===================================================================== */

IF OBJECT_ID('dbo.Roles','U') IS NULL
CREATE TABLE dbo.Roles (
    Id              INT             NOT NULL IDENTITY(1,1),
    [Key]           VARCHAR(40)     NOT NULL,               -- 'admin' | 'operasyon' | 'muhasebe'
    [Name]          NVARCHAR(80)    NOT NULL,
    [Description]   NVARCHAR(250)   NULL,
    IsSystem        BIT             NOT NULL CONSTRAINT DF_Roles_IsSystem DEFAULT(0),
    CreatedAt       DATETIME2(3)    NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Roles_Key UNIQUE ([Key])
);
GO

IF OBJECT_ID('dbo.Permissions','U') IS NULL
CREATE TABLE dbo.Permissions (
    Id              INT             NOT NULL IDENTITY(1,1),
    [Key]           VARCHAR(60)     NOT NULL,               -- 'orders.create' vb.
    [Name]          NVARCHAR(120)   NOT NULL,
    [Group]         NVARCHAR(60)    NOT NULL,
    CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Permissions_Key UNIQUE ([Key])
);
GO

IF OBJECT_ID('dbo.RolePermissions','U') IS NULL
CREATE TABLE dbo.RolePermissions (
    RoleId          INT             NOT NULL,
    PermissionId    INT             NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED (RoleId, PermissionId),
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId)
        REFERENCES dbo.Roles(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId)
        REFERENCES dbo.Permissions(Id) ON DELETE CASCADE
);
GO

IF OBJECT_ID('dbo.Users','U') IS NULL
CREATE TABLE dbo.Users (
    Id                  INT             NOT NULL IDENTITY(1,1),
    Email               NVARCHAR(200)   COLLATE Latin1_General_100_CI_AS NOT NULL,
    FullName            NVARCHAR(150)   NOT NULL,
    PasswordHash        NVARCHAR(400)   NULL,               -- NULL = parola henüz atanmadı (bkz. 03-seed.sql)
    SecurityStamp       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_SecurityStamp DEFAULT(NEWID()),
    MustChangePassword  BIT             NOT NULL CONSTRAINT DF_Users_MustChange DEFAULT(0),
    AccessFailedCount   INT             NOT NULL CONSTRAINT DF_Users_AFC DEFAULT(0),
    LockoutEndUtc       DATETIME2(3)    NULL,
    LastLoginAtUtc      DATETIME2(3)    NULL,
    IsActive            BIT             NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT(1),
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT(0),
    CreatedAt           DATETIME2(3)    NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedBy           INT             NULL,
    UpdatedAt           DATETIME2(3)    NULL,
    UpdatedBy           INT             NULL,
    RowVersion          ROWVERSION      NOT NULL,
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id)
);
GO

IF OBJECT_ID('dbo.UserRoles','U') IS NULL
CREATE TABLE dbo.UserRoles (
    UserId          INT             NOT NULL,
    RoleId          INT             NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId)
        REFERENCES dbo.Roles(Id) ON DELETE CASCADE
);
GO

IF OBJECT_ID('dbo.RefreshTokens','U') IS NULL
CREATE TABLE dbo.RefreshTokens (
    Id              BIGINT          NOT NULL IDENTITY(1,1),
    UserId          INT             NOT NULL,
    TokenHash       VARBINARY(32)   NOT NULL,               -- SHA-256(token). Ham token DB'de tutulmaz.
    ExpiresAtUtc    DATETIME2(3)    NOT NULL,
    CreatedAtUtc    DATETIME2(3)    NOT NULL CONSTRAINT DF_RT_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedByIp     VARCHAR(45)     NULL,
    RevokedAtUtc    DATETIME2(3)    NULL,
    RevokedReason   NVARCHAR(100)   NULL,
    ReplacedById    BIGINT          NULL,                   -- token rotasyon zinciri
    CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RefreshTokens_Replaced FOREIGN KEY (ReplacedById)
        REFERENCES dbo.RefreshTokens(Id)                    -- self-FK: CASCADE YOK (döngü olur)
);
GO

IF OBJECT_ID('dbo.LoginAuditLogs','U') IS NULL
CREATE TABLE dbo.LoginAuditLogs (
    Id              BIGINT          NOT NULL IDENTITY(1,1),
    UserId          INT             NULL,                   -- bilinmeyen e-posta denemesinde NULL
    AttemptedEmail  NVARCHAR(200)   COLLATE Latin1_General_100_CI_AS NULL,
    Succeeded       BIT             NOT NULL,
    FailureReason   VARCHAR(40)     NULL,                   -- 'invalid_credentials'|'locked_out'|'inactive'
    IpAddress       VARCHAR(45)     NULL,
    UserAgent       NVARCHAR(300)   NULL,
    CorrelationId   VARCHAR(64)     NULL,
    OccurredAtUtc   DATETIME2(3)    NOT NULL CONSTRAINT DF_LAL_OccurredAt DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT PK_LoginAuditLogs PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_LoginAuditLogs_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id)                            -- CASCADE YOK: kullanıcı silinse de iz kalır
);
GO

/* =====================================================================
   2. TANIM TABLOLARI
   Kaynak: masters = { customers:[], suppliers:[], brands:[] } (string dizileri)
   ===================================================================== */

IF OBJECT_ID('dbo.Customers','U') IS NULL
CREATE TABLE dbo.Customers (
    Id          INT             NOT NULL IDENTITY(1,1),
    Unvan       NVARCHAR(200)   NOT NULL,
    IsActive    BIT             NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT(1),
    IsDeleted   BIT             NOT NULL CONSTRAINT DF_Customers_IsDeleted DEFAULT(0),
    CreatedAt   DATETIME2(3)    NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedBy   INT             NULL,
    UpdatedAt   DATETIME2(3)    NULL,
    UpdatedBy   INT             NULL,
    RowVersion  ROWVERSION      NOT NULL,
    CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Customers_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_Customers_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id)
);
GO

IF OBJECT_ID('dbo.Suppliers','U') IS NULL
CREATE TABLE dbo.Suppliers (
    Id          INT             NOT NULL IDENTITY(1,1),
    Unvan       NVARCHAR(200)   NOT NULL,
    IsActive    BIT             NOT NULL CONSTRAINT DF_Suppliers_IsActive DEFAULT(1),
    IsDeleted   BIT             NOT NULL CONSTRAINT DF_Suppliers_IsDeleted DEFAULT(0),
    CreatedAt   DATETIME2(3)    NOT NULL CONSTRAINT DF_Suppliers_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedBy   INT             NULL,
    UpdatedAt   DATETIME2(3)    NULL,
    UpdatedBy   INT             NULL,
    RowVersion  ROWVERSION      NOT NULL,
    CONSTRAINT PK_Suppliers PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Suppliers_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_Suppliers_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id)
);
GO

IF OBJECT_ID('dbo.Brands','U') IS NULL
CREATE TABLE dbo.Brands (
    Id          INT             NOT NULL IDENTITY(1,1),
    Ad          NVARCHAR(100)   NOT NULL,
    IsActive    BIT             NOT NULL CONSTRAINT DF_Brands_IsActive DEFAULT(1),
    IsDeleted   BIT             NOT NULL CONSTRAINT DF_Brands_IsDeleted DEFAULT(0),
    CreatedAt   DATETIME2(3)    NOT NULL CONSTRAINT DF_Brands_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedBy   INT             NULL,
    UpdatedAt   DATETIME2(3)    NULL,
    UpdatedBy   INT             NULL,
    RowVersion  ROWVERSION      NOT NULL,
    CONSTRAINT PK_Brands PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Brands_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_Brands_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id)
);
GO

/* =====================================================================
   3. SİPARİŞ TABLOLARI
   ===================================================================== */

/* Sipariş numarası üretimi — çakışmasız, yıl bazında sıfırlanabilir.
   SEQUENCE yerine tablo kullanıldı çünkü yıl bazlı sıfırlama gerekiyor. */
IF OBJECT_ID('dbo.NumberSequences','U') IS NULL
CREATE TABLE dbo.NumberSequences (
    [Key]       VARCHAR(40)     NOT NULL,
    [Year]      SMALLINT        NOT NULL,
    LastValue   INT             NOT NULL,
    CONSTRAINT PK_NumberSequences PRIMARY KEY CLUSTERED ([Key], [Year])
);
GO

IF OBJECT_ID('dbo.FleetOrders','U') IS NULL
CREATE TABLE dbo.FleetOrders (
    Id                  INT             NOT NULL IDENTITY(1,1),
    SiparisNo           VARCHAR(20)     NOT NULL,           -- 'SIP-2026-000123'
    CustomerId          INT             NOT NULL,
    OlusturmaTarihi     DATE            NOT NULL,           -- iş tarihi (frontend: olusturmaTarihi)
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_FO_IsDeleted DEFAULT(0),
    DeletedAt           DATETIME2(3)    NULL,
    DeletedBy           INT             NULL,
    CreatedAt           DATETIME2(3)    NOT NULL CONSTRAINT DF_FO_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedBy           INT             NULL,
    UpdatedAt           DATETIME2(3)    NULL,
    UpdatedBy           INT             NULL,
    RowVersion          ROWVERSION      NOT NULL,
    CONSTRAINT PK_FleetOrders PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_FleetOrders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
    CONSTRAINT FK_FleetOrders_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_FleetOrders_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_FleetOrders_DeletedBy FOREIGN KEY (DeletedBy) REFERENCES dbo.Users(Id)
);
GO

IF OBJECT_ID('dbo.FleetOrderLines','U') IS NULL
CREATE TABLE dbo.FleetOrderLines (
    Id              INT             NOT NULL IDENTITY(1,1),
    OrderId         INT             NOT NULL,
    BrandId         INT             NOT NULL,
    Model           NVARCHAR(100)   NOT NULL,
    Adet            INT             NOT NULL,
    BirimBedel      DECIMAL(18,2)   NOT NULL,
    SupplierId      INT             NOT NULL,
    CreatedAt       DATETIME2(3)    NOT NULL CONSTRAINT DF_FOL_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedBy       INT             NULL,
    UpdatedAt       DATETIME2(3)    NULL,
    UpdatedBy       INT             NULL,
    RowVersion      ROWVERSION      NOT NULL,
    CONSTRAINT PK_FleetOrderLines PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_FOL_Orders FOREIGN KEY (OrderId)
        REFERENCES dbo.FleetOrders(Id) ON DELETE CASCADE,   -- sipariş soft-delete; cascade yalnız fiziksel temizlikte
    CONSTRAINT FK_FOL_Brands FOREIGN KEY (BrandId) REFERENCES dbo.Brands(Id),
    CONSTRAINT FK_FOL_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(Id),
    CONSTRAINT FK_FOL_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_FOL_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT CK_FOL_Adet CHECK (Adet >= 0),
    CONSTRAINT CK_FOL_BirimBedel CHECK (BirimBedel >= 0)
);
GO

IF OBJECT_ID('dbo.FleetOrderPaymentPlans','U') IS NULL
CREATE TABLE dbo.FleetOrderPaymentPlans (
    Id          INT             NOT NULL IDENTITY(1,1),
    LineId      INT             NOT NULL,
    PlanTarihi  DATE            NOT NULL,
    Tutar       DECIMAL(18,2)   NOT NULL,
    CreatedAt   DATETIME2(3)    NOT NULL CONSTRAINT DF_FOPP_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedBy   INT             NULL,
    UpdatedAt   DATETIME2(3)    NULL,
    UpdatedBy   INT             NULL,
    CONSTRAINT PK_FleetOrderPaymentPlans PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_FOPP_Lines FOREIGN KEY (LineId)
        REFERENCES dbo.FleetOrderLines(Id) ON DELETE CASCADE,
    CONSTRAINT FK_FOPP_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_FOPP_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT CK_FOPP_Tutar CHECK (Tutar >= 0)
);
GO

IF OBJECT_ID('dbo.FleetOrderPayments','U') IS NULL
CREATE TABLE dbo.FleetOrderPayments (
    Id          INT             NOT NULL IDENTITY(1,1),
    LineId      INT             NOT NULL,
    OdemeTarihi DATE            NOT NULL,
    Tutar       DECIMAL(18,2)   NOT NULL,
    CreatedAt   DATETIME2(3)    NOT NULL CONSTRAINT DF_FOP_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedBy   INT             NULL,
    UpdatedAt   DATETIME2(3)    NULL,
    UpdatedBy   INT             NULL,
    CONSTRAINT PK_FleetOrderPayments PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_FOP_Lines FOREIGN KEY (LineId)
        REFERENCES dbo.FleetOrderLines(Id) ON DELETE CASCADE,
    CONSTRAINT FK_FOP_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_FOP_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT CK_FOP_Tutar CHECK (Tutar >= 0)
);
GO

IF OBJECT_ID('dbo.FleetOrderVehicles','U') IS NULL
CREATE TABLE dbo.FleetOrderVehicles (
    Id                  INT             NOT NULL IDENTITY(1,1),
    OrderId             INT             NOT NULL,
    LineId              INT             NOT NULL,
    PlakaNo             NVARCHAR(15)    NULL,               -- frontend: plakaNo ('' → NULL)
    -- Tedarikçi bilgileri
    TedarikTarihi       DATE            NULL,
    TedarikYeri         NVARCHAR(200)   NULL,
    CekiciKullanildi    BIT             NOT NULL CONSTRAINT DF_FOV_CekiciKullanildi DEFAULT(0),
    -- Müşteriye teslim bilgileri
    PlanlananTeslim     DATE            NULL,
    TeslimYeri          NVARCHAR(200)   NULL,
    -- Teslim alındı
    TeslimAlindi        BIT             NOT NULL CONSTRAINT DF_FOV_TeslimAlindi DEFAULT(0),
    TeslimAlinmaTarihi  DATE            NULL,
    -- Teslimat yapıldı
    GerceklesenTeslim   DATE            NULL,
    -- İkame araç
    IkameVerildi        BIT             NOT NULL CONSTRAINT DF_FOV_IkameVerildi DEFAULT(0),
    IkameTarihi         DATE            NULL,
    IkamePlaka          NVARCHAR(15)    NULL,
    IkameIadeTarihi     DATE            NULL,
    CreatedAt           DATETIME2(3)    NOT NULL CONSTRAINT DF_FOV_CreatedAt DEFAULT(SYSUTCDATETIME()),
    CreatedBy           INT             NULL,
    UpdatedAt           DATETIME2(3)    NULL,
    UpdatedBy           INT             NULL,
    RowVersion          ROWVERSION      NOT NULL,
    CONSTRAINT PK_FleetOrderVehicles PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_FOV_Orders FOREIGN KEY (OrderId)
        REFERENCES dbo.FleetOrders(Id),                     -- CASCADE YOK: Lines üzerinden çoklu yol oluşur
    CONSTRAINT FK_FOV_Lines FOREIGN KEY (LineId)
        REFERENCES dbo.FleetOrderLines(Id) ON DELETE CASCADE,
    CONSTRAINT FK_FOV_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_FOV_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id),
    -- İş kuralı: teslim alınmadan teslimat yapılamaz (frontend satır 1860 ile aynı kural)
    CONSTRAINT CK_FOV_TeslimSirasi CHECK (GerceklesenTeslim IS NULL OR TeslimAlindi = 1),
    CONSTRAINT CK_FOV_TeslimAlinmaTarihi CHECK (TeslimAlindi = 1 OR TeslimAlinmaTarihi IS NULL),
    CONSTRAINT CK_FOV_IkameTarihi CHECK (IkameVerildi = 1 OR (IkameTarihi IS NULL AND IkameIadeTarihi IS NULL))
);
GO

/* SSH hazırlık adımları. Araç başına tam 4 satır: plaka, hgs, gps, utts.
   Frontend: v.ssh.{plaka|hgs|gps|utts} = { yapildi, tarih } */
IF OBJECT_ID('dbo.VehicleSshTasks','U') IS NULL
CREATE TABLE dbo.VehicleSshTasks (
    Id          INT             NOT NULL IDENTITY(1,1),
    VehicleId   INT             NOT NULL,
    TaskType    VARCHAR(8)      NOT NULL,                   -- 'plaka'|'hgs'|'gps'|'utts'
    Yapildi     BIT             NOT NULL CONSTRAINT DF_VST_Yapildi DEFAULT(0),
    Tarih       DATE            NULL,
    UpdatedAt   DATETIME2(3)    NULL,
    UpdatedBy   INT             NULL,
    CONSTRAINT PK_VehicleSshTasks PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_VehicleSshTasks UNIQUE (VehicleId, TaskType),
    CONSTRAINT FK_VST_Vehicles FOREIGN KEY (VehicleId)
        REFERENCES dbo.FleetOrderVehicles(Id) ON DELETE CASCADE,
    CONSTRAINT FK_VST_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id),
    CONSTRAINT CK_VST_TaskType CHECK (TaskType IN ('plaka','hgs','gps','utts')),
    -- Frontend satır 444/1426 çelişkisinin şema seviyesinde kapatılması:
    -- Yapildi=1 ise Tarih zorunlu, Yapildi=0 ise Tarih NULL olmalı.
    CONSTRAINT CK_VST_YapildiTarih CHECK ((Yapildi = 1 AND Tarih IS NOT NULL)
                                       OR (Yapildi = 0 AND Tarih IS NULL))
);
GO

/* =====================================================================
   4. DENETİM (AUDIT)
   ===================================================================== */

IF OBJECT_ID('dbo.AuditLogs','U') IS NULL
CREATE TABLE dbo.AuditLogs (
    Id              BIGINT          NOT NULL IDENTITY(1,1),
    EntityName      VARCHAR(60)     NOT NULL,               -- 'FleetOrderVehicle'
    EntityId        VARCHAR(40)     NOT NULL,
    [Action]        VARCHAR(10)     NOT NULL,               -- 'Insert'|'Update'|'Delete'
    ColumnName      VARCHAR(60)     NULL,                   -- Update'te değişen kolon
    OldValue        NVARCHAR(MAX)   NULL,
    NewValue        NVARCHAR(MAX)   NULL,
    UserId          INT             NULL,
    CorrelationId   VARCHAR(64)     NULL,
    IpAddress       VARCHAR(45)     NULL,
    OccurredAtUtc   DATETIME2(3)    NOT NULL CONSTRAINT DF_AL_OccurredAt DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
    CONSTRAINT CK_AuditLogs_Action CHECK ([Action] IN ('Insert','Update','Delete'))
);
GO

/* Audit kayıtları uygulama kullanıcısı tarafından değiştirilemez.
   Yetki 03-seed.sql'de DENY ile de pekiştirilir; bu tetikleyici ikinci savunma hattıdır. */
IF OBJECT_ID('dbo.TR_AuditLogs_NoChange','TR') IS NOT NULL DROP TRIGGER dbo.TR_AuditLogs_NoChange;
GO
CREATE TRIGGER dbo.TR_AuditLogs_NoChange ON dbo.AuditLogs
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, 'AuditLogs kayitlari degistirilemez veya silinemez.', 1;
END
GO

/* =====================================================================
   5. SİPARİŞ NUMARASI ÜRETİMİ
   Çakışmasız: UPDLOCK+HOLDLOCK ile satır serileştirilir.
   Çağıran transaction içinde çağrılmalıdır.
   ===================================================================== */
IF OBJECT_ID('dbo.sp_NextFleetOrderNo','P') IS NOT NULL DROP PROCEDURE dbo.sp_NextFleetOrderNo;
GO
CREATE PROCEDURE dbo.sp_NextFleetOrderNo
    @Year       SMALLINT = NULL,
    @SiparisNo  VARCHAR(20) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @Year IS NULL SET @Year = YEAR(SYSDATETIME());

    DECLARE @Next INT;
    DECLARE @Key VARCHAR(40) = 'FleetOrder';

    UPDATE dbo.NumberSequences WITH (UPDLOCK, HOLDLOCK)
        SET @Next = LastValue = LastValue + 1
    WHERE [Key] = @Key AND [Year] = @Year;

    IF @Next IS NULL
    BEGIN
        INSERT INTO dbo.NumberSequences ([Key], [Year], LastValue) VALUES (@Key, @Year, 1);
        SET @Next = 1;
    END

    SET @SiparisNo = 'SIP-' + CAST(@Year AS VARCHAR(4)) + '-'
                   + RIGHT('000000' + CAST(@Next AS VARCHAR(6)), 6);
END
GO

/* =====================================================================
   6. RAPOR GÖRÜNÜMLERİ
   Durum SAKLANMAZ — hesaplanır. Frontend vehicleStatus() (satır 442) birebir
   karşılığı. Dört takvim raporu da bu view üzerinden çalışır.
   Sıra: overdue(4) > soon(3) > neutral(2) > ready(1) > done(0)
   ===================================================================== */
IF OBJECT_ID('dbo.vw_VehicleStatus','V') IS NOT NULL DROP VIEW dbo.vw_VehicleStatus;
GO
CREATE VIEW dbo.vw_VehicleStatus
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

/* Kalem bazında ödeme özeti — Ödeme takvimi ve "kalan borç" için.
   Frontend: lineTotal / lineOdenenToplam / lineKalanTutar (satır 452-455) */
IF OBJECT_ID('dbo.vw_LinePaymentSummary','V') IS NOT NULL DROP VIEW dbo.vw_LinePaymentSummary;
GO
CREATE VIEW dbo.vw_LinePaymentSummary
AS
SELECT
    l.Id                AS LineId,
    l.OrderId,
    l.SupplierId,
    l.Adet * l.BirimBedel                       AS KalemToplam,
    ISNULL(pl.PlanToplam, 0)                    AS PlanToplam,
    ISNULL(od.OdenenToplam, 0)                  AS OdenenToplam,
    CASE WHEN (l.Adet * l.BirimBedel) - ISNULL(od.OdenenToplam, 0) < 0 THEN 0
         ELSE (l.Adet * l.BirimBedel) - ISNULL(od.OdenenToplam, 0) END AS KalanTutar,
    CAST(CASE WHEN ISNULL(pl.PlanToplam,0) = l.Adet * l.BirimBedel THEN 1 ELSE 0 END AS BIT) AS PlanEslesiyor
FROM dbo.FleetOrderLines l
OUTER APPLY (SELECT SUM(p.Tutar) AS PlanToplam   FROM dbo.FleetOrderPaymentPlans p WHERE p.LineId = l.Id) pl
OUTER APPLY (SELECT SUM(p.Tutar) AS OdenenToplam FROM dbo.FleetOrderPayments     p WHERE p.LineId = l.Id) od;
GO

PRINT '01-schema.sql tamamlandi.';
GO
