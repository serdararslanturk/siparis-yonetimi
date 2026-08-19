/* =====================================================================
   ACR Filo — Migration 06: Müşteriye Vade (gün) + Temsilci
   - Yeni tablo: dbo.Temsilciler (tanım listesi, Brands ile aynı yapı)
   - Customers.VadeGun  INT NULL         (ödeme vadesi, gün)
   - Customers.TemsilciId INT NULL  FK -> dbo.Temsilciler(Id)
   İdempotenttir: tekrar çalıştırılabilir, mevcut veriye zarar vermez.
   Çalıştırma (canlı):
     sqlcmd -S [SUNUCU] -d AcrFilo -i db\06-musteri-vade-temsilci.sql -b -I
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

/* ---- 1. Temsilciler tablosu ---------------------------------------- */
IF OBJECT_ID('dbo.Temsilciler','U') IS NULL
BEGIN
    CREATE TABLE dbo.Temsilciler (
        Id          INT             NOT NULL IDENTITY(1,1),
        Ad          NVARCHAR(100)   NOT NULL,
        IsActive    BIT             NOT NULL CONSTRAINT DF_Temsilciler_IsActive  DEFAULT(1),
        IsDeleted   BIT             NOT NULL CONSTRAINT DF_Temsilciler_IsDeleted DEFAULT(0),
        CreatedAt   DATETIME2(3)    NOT NULL CONSTRAINT DF_Temsilciler_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CreatedBy   INT             NULL,
        UpdatedAt   DATETIME2(3)    NULL,
        UpdatedBy   INT             NULL,
        RowVersion  ROWVERSION      NOT NULL,
        CONSTRAINT PK_Temsilciler PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Temsilciler_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_Temsilciler_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id)
    );
    PRINT 'dbo.Temsilciler olusturuldu.';
END
ELSE
    PRINT 'dbo.Temsilciler zaten var, dokunulmadi.';
GO

/* Filtered unique: soft-delete edilen ad yeniden kullanilabilir. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Temsilciler_Ad' AND object_id = OBJECT_ID('dbo.Temsilciler'))
    CREATE UNIQUE INDEX UX_Temsilciler_Ad ON dbo.Temsilciler(Ad) WHERE IsDeleted = 0;
GO

/* ---- 2. Customers.VadeGun ------------------------------------------ */
IF COL_LENGTH('dbo.Customers','VadeGun') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD VadeGun INT NULL;
    PRINT 'Customers.VadeGun eklendi.';
END
ELSE
    PRINT 'Customers.VadeGun zaten var.';
GO

/* ---- 3. Customers.TemsilciId + FK ---------------------------------- */
IF COL_LENGTH('dbo.Customers','TemsilciId') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD TemsilciId INT NULL;
    PRINT 'Customers.TemsilciId eklendi.';
END
ELSE
    PRINT 'Customers.TemsilciId zaten var.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Customers_Temsilci')
BEGIN
    ALTER TABLE dbo.Customers
        ADD CONSTRAINT FK_Customers_Temsilci FOREIGN KEY (TemsilciId) REFERENCES dbo.Temsilciler(Id);
    PRINT 'FK_Customers_Temsilci eklendi.';
END
ELSE
    PRINT 'FK_Customers_Temsilci zaten var.';
GO

PRINT '06-musteri-vade-temsilci.sql tamamlandi.';
GO
