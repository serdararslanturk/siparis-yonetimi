/* =====================================================================
   ACR Filo — Başlangıç verileri
   Çalıştırma:
     sqlcmd -S [SQL_SERVER_ADI] -d [DATABASE_ADI] -i 03-seed.sql -b ^
            -v AdminEmail="[YONETICI_EPOSTA]" AppLogin="[SQL_KULLANICI_ADI]"

   İdempotent: tekrar çalıştırılabilir, mevcut kayıtları ezmez.

   >>> PAROLA HAKKINDA <<<
   Bu script yönetici parolası İÇERMEZ ve bir hash de yazmaz.
   Kullanıcı PasswordHash = NULL, IsActive = 0 olarak oluşturulur; bu haliyle
   HİÇBİR parola ile giriş yapılamaz (kod NULL hash'i erken reddeder).
   Parola şu komutla, ayrı bir adımda atanır:
       dotnet Acr.Filo.Api.dll --set-admin-password
   Komut parolayı konsoldan maskeli okur, PBKDF2-HMAC-SHA256 (100.000 tur) ile
   hashler, kaydı aktifleştirir ve MustChangePassword=1 bırakır.
   ===================================================================== */
:setvar AdminEmail "admin@[SIRKET_DOMAINI]"
:setvar AppLogin "acrfilo_app"
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---- 1. Roller ------------------------------------------------------ */
MERGE dbo.Roles AS t
USING (VALUES
    ('admin',     N'Sistem Yöneticisi', N'Tam yetki: kullanıcı, rol, tanım, audit dahil'),
    ('operasyon', N'Operasyon',         N'Sipariş, araç, SSH ve teslim işlemleri'),
    ('muhasebe',  N'Muhasebe',          N'Ödeme planı ve gerçekleşen ödemeler')
) AS s([Key], [Name], [Description])
ON t.[Key] = s.[Key]
WHEN MATCHED THEN UPDATE SET t.[Name] = s.[Name], t.[Description] = s.[Description], t.IsSystem = 1
WHEN NOT MATCHED THEN INSERT ([Key],[Name],[Description],IsSystem) VALUES (s.[Key],s.[Name],s.[Description],1);
GO

/* ---- 2. Yetkiler ----------------------------------------------------
   Her yetki, uygulamadaki gerçek bir eyleme karşılık gelir. Fazlası yok. */
MERGE dbo.Permissions AS t
USING (VALUES
    ('orders.view',        N'Sipariş görüntüleme',            N'Sipariş'),
    ('orders.create',      N'Sipariş oluşturma',              N'Sipariş'),
    ('orders.update',      N'Sipariş / kalem düzenleme',      N'Sipariş'),
    ('orders.delete',      N'Sipariş / kalem / araç silme',   N'Sipariş'),
    ('vehicles.update',    N'SSH, teslim ve ikame işlemleri', N'Araç'),
    ('payments.view',      N'Finansal alanları görüntüleme',  N'Ödeme'),
    ('payments.plan',      N'Ödeme planı girme / düzenleme',  N'Ödeme'),
    ('payments.record',    N'Gerçekleşen ödeme kaydetme',     N'Ödeme'),
    ('definitions.view',   N'Tanımları görüntüleme',          N'Tanım'),
    ('definitions.manage', N'Tanım ekleme / silme',           N'Tanım'),
    ('reports.view',       N'Takvim raporlarını görüntüleme', N'Rapor'),
    ('reports.export',     N'Excel/CSV dışa aktarma',         N'Rapor'),
    ('users.manage',       N'Kullanıcı ve rol yönetimi',      N'Yönetim'),
    ('audit.view',         N'Audit kayıtlarını görüntüleme',  N'Yönetim')
) AS s([Key],[Name],[Group])
ON t.[Key] = s.[Key]
WHEN MATCHED THEN UPDATE SET t.[Name] = s.[Name], t.[Group] = s.[Group]
WHEN NOT MATCHED THEN INSERT ([Key],[Name],[Group]) VALUES (s.[Key],s.[Name],s.[Group]);
GO

/* ---- 3. Rol → Yetki eşlemesi ----------------------------------------
   Bu tablo sayesinde "kim neyi yapabilir" kod deploy'u OLMADAN değişir. */
DECLARE @Map TABLE (RoleKey VARCHAR(40), PermKey VARCHAR(60));

-- admin: hepsi
INSERT INTO @Map (RoleKey, PermKey) SELECT 'admin', [Key] FROM dbo.Permissions;

-- operasyon: sipariş + araç + tanım + rapor. Gerçekleşen ödeme kaydı YOK.
INSERT INTO @Map (RoleKey, PermKey) VALUES
    ('operasyon','orders.view'),        ('operasyon','orders.create'),
    ('operasyon','orders.update'),      ('operasyon','orders.delete'),
    ('operasyon','vehicles.update'),    ('operasyon','payments.view'),
    ('operasyon','payments.plan'),      ('operasyon','definitions.view'),
    ('operasyon','definitions.manage'), ('operasyon','reports.view'),
    ('operasyon','reports.export');
/* payments.plan operasyonda: sipariş oluşturma formu ödeme planını ZORUNLU
   tutuyor (frontend satır 2161 — plan toplamı kalem toplamına eşit olmalı).
   Bu yetki olmadan sipariş açılamaz. */

-- muhasebe: sipariş görür, ödemeyi yönetir. Araç/SSH/teslim işlemi YOK.
INSERT INTO @Map (RoleKey, PermKey) VALUES
    ('muhasebe','orders.view'),      ('muhasebe','payments.view'),
    ('muhasebe','payments.plan'),    ('muhasebe','payments.record'),
    ('muhasebe','definitions.view'), ('muhasebe','reports.view'),
    ('muhasebe','reports.export');

MERGE dbo.RolePermissions AS t
USING (
    SELECT r.Id AS RoleId, p.Id AS PermissionId
    FROM @Map m
    JOIN dbo.Roles r       ON r.[Key] = m.RoleKey
    JOIN dbo.Permissions p ON p.[Key] = m.PermKey
) AS s
ON t.RoleId = s.RoleId AND t.PermissionId = s.PermissionId
WHEN NOT MATCHED BY TARGET THEN INSERT (RoleId, PermissionId) VALUES (s.RoleId, s.PermissionId)
WHEN NOT MATCHED BY SOURCE AND t.RoleId IN (SELECT Id FROM dbo.Roles WHERE IsSystem = 1)
    THEN DELETE;   -- sistem rollerinde elle eklenmiş fazlalıkları temizle
GO

/* ---- 4. Yönetici kabuğu (parolasız, pasif) -------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'$(AdminEmail)')
BEGIN
    INSERT INTO dbo.Users (Email, FullName, PasswordHash, MustChangePassword, IsActive)
    VALUES (N'$(AdminEmail)', N'Sistem Yöneticisi', NULL, 1, 0);

    INSERT INTO dbo.UserRoles (UserId, RoleId)
    SELECT u.Id, r.Id FROM dbo.Users u CROSS JOIN dbo.Roles r
    WHERE u.Email = N'$(AdminEmail)' AND r.[Key] = 'admin';

    PRINT 'Yonetici kabugu olusturuldu: $(AdminEmail) (PASIF, parolasiz)';
    PRINT '>>> SIRADAKI ADIM: dotnet Acr.Filo.Api.dll --set-admin-password';
END
ELSE
    PRINT 'Yonetici zaten var, dokunulmadi: $(AdminEmail)';
GO

/* ---- 5. Sipariş numarası sayacı ------------------------------------- */
DECLARE @Y SMALLINT = YEAR(SYSDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.NumberSequences WHERE [Key]='FleetOrder' AND [Year]=@Y)
    INSERT INTO dbo.NumberSequences ([Key],[Year],LastValue) VALUES ('FleetOrder', @Y, 0);
GO

/* ---- 6. Uygulama SQL kullanıcısının yetkileri -----------------------
   sysadmin YOK, db_owner YOK. Yalnızca gereken minimum.
   Migration ayrı ve daha yetkili bir hesapla çalıştırılır (bkz. docs/SQL-Kurulum.md). */
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$(AppLogin)')
BEGIN
    GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [$(AppLogin)];
    GRANT EXECUTE ON SCHEMA::dbo TO [$(AppLogin)];

    -- Audit kayıtları uygulama tarafından DEĞİŞTİRİLEMEZ / SİLİNEMEZ.
    -- (Tetikleyici ilk savunma hattı; bu DENY ikincisi.)
    DENY UPDATE, DELETE ON OBJECT::dbo.AuditLogs TO [$(AppLogin)];
    DENY UPDATE, DELETE ON OBJECT::dbo.LoginAuditLogs TO [$(AppLogin)];

    -- Şema değişikliği yetkisi uygulamada yok.
    DENY ALTER, CREATE TABLE, REFERENCES TO [$(AppLogin)];

    PRINT 'Uygulama kullanicisi yetkilendirildi: $(AppLogin)';
END
ELSE
    PRINT 'UYARI: $(AppLogin) bu veritabaninda yok. Once docs/SQL-Kurulum.md B adimini calistirin.';
GO

PRINT '03-seed.sql tamamlandi.';
GO
