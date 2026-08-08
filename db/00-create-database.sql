/* =====================================================================
   ACR Filo — Veritabanı oluşturma
   Bu script MASTER üzerinde çalışır ve YALNIZCA İLK KURULUMDA çalıştırılır.
   Çalıştırma:
     sqlcmd -S [SQL_SERVER_ADI] -d master -i 00-create-database.sql -b ^
            -v DbName="[DATABASE_ADI]" AppLogin="[SQL_KULLANICI_ADI]"

   UYARI: Bu script veritabanı DÜŞÜRMEZ. Mevcutsa dokunmaz.
   ===================================================================== */
:setvar DbName "AcrFilo"
:setvar AppLogin "acrfilo_app"
GO

IF DB_ID('$(DbName)') IS NULL
BEGIN
    -- Turkish_CI_AS: frontend localeCompare(...,'tr') ve toLocaleLowerCase('tr-TR')
    -- davranışıyla örtüşür. Bkz. 01-schema.sql başındaki COLLATION NOTU.
    DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE [$(DbName)] COLLATE Turkish_CI_AS;';
    EXEC sp_executesql @sql;
    PRINT 'Veritabani olusturuldu: $(DbName) (Turkish_CI_AS)';
END
ELSE
    PRINT 'Veritabani zaten var, dokunulmadi: $(DbName)';
GO

ALTER DATABASE [$(DbName)] SET RECOVERY FULL;
ALTER DATABASE [$(DbName)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
/* READ_COMMITTED_SNAPSHOT: rapor sorguları (4 takvim) yazan işlemleri
   bloklamasın diye. Filo hacminde tempdb maliyeti ihmal edilebilir. */

PRINT '00-create-database.sql tamamlandi.';
GO
