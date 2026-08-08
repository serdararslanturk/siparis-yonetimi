# ACR Filo — Kurulum Kılavuzu

Bu kılavuz uygulamayı sıfırdan çalışır hale getirir. Sırayla takip edin.
Köşeli parantezli `[...]` yerleri kendi değerlerinizle doldurun.

---

## Gerekenler (sunucuda kurulu olmalı)

| Yazılım | Sürüm | Nasıl kontrol edilir |
|---|---|---|
| Windows Server | 2019 / 2022 | — |
| .NET 8 Hosting Bundle | 8.0.x | `dotnet --info` |
| SQL Server | 2019+ | SSMS ile bağlanın |
| IIS | 10 | Sunucu Yöneticisi → Roller |

> **.NET 8 Hosting Bundle** (ASP.NET Core Runtime + IIS modülü) şart. İndirme:
> Microsoft "dotnet 8.0 hosting bundle" araması. Kurduktan sonra IIS'i yeniden başlatın:
> `net stop was /y && net start w3svc`

---

## ADIM 1 — Kodu derleyin (NuGet erişimi olan bir makinede)

Bu adım geliştirici makinesinde veya build sunucusunda yapılır (internet gerekir):

```powershell
cd [PROJE_KLASORU]
dotnet restore
dotnet publish src\Acr.Filo.Api\Acr.Filo.Api.csproj -c Release -o publish
```

`publish\` klasörü IIS'e kopyalanacak her şeyi içerir.

> **Derleme hatası çıkarsa:** hatanın tam metnini bana gönderin. Bu kod bu ortamda
> (NuGet kapalı olduğu için) derlenemedi; makine kontrolüyle doğrulandı ama sizin
> ilk `dotnet build`'iniz kesin doğrulama olacak.

---

## ADIM 2 — Veritabanını kurun

SQL Server'da, sırayla (SSMS veya `sqlcmd`):

```powershell
# 2a. Veritabanını oluştur (Turkish collation ile)
sqlcmd -S [SQL_SUNUCU] -d master -i db\00-create-database.sql -b `
       -v DbName="AcrFilo" AppLogin="acrfilo_app"

# 2b. Uygulama SQL kullanıcısını oluştur (bu komut sizde, aşağıdaki şablonla)
#     Windows Authentication kullanacaksanız bu adımı atlayın (ADIM 4'e bakın).

# 2c. Şema + indexler + başlangıç verisi
sqlcmd -S [SQL_SUNUCU] -d AcrFilo -i db\01-schema.sql  -b
sqlcmd -S [SQL_SUNUCU] -d AcrFilo -i db\02-indexes.sql -b
sqlcmd -S [SQL_SUNUCU] -d AcrFilo -i db\03-seed.sql    -b `
       -v AdminEmail="[YONETICI@SIRKET.COM]" AppLogin="acrfilo_app"
```

Bu scriptler **idempotenttir**: yanlışlıkla iki kez çalıştırırsanız zarar vermez,
veri silmez.

### SQL kullanıcısı oluşturma (2b — SQL Authentication kullanıyorsanız)

```sql
USE master;
CREATE LOGIN acrfilo_app WITH PASSWORD = '[GUCLU_PAROLA]';
USE AcrFilo;
CREATE USER acrfilo_app FOR LOGIN acrfilo_app;
-- Yetkiler 03-seed.sql tarafından otomatik verilir (sadece gerekli minimum).
```

---

## ADIM 3 — Yapılandırma dosyasını hazırlayın

`publish\appsettings.Production.json.template` dosyasını **kopyalayıp**
`appsettings.Production.json` yapın ve şu üç şeyi doldurun:

**1. Veritabanı bağlantısı** (biri):
```json
"FiloDb": "Server=[SQL_SUNUCU];Database=AcrFilo;User Id=acrfilo_app;Password=[PAROLA];Encrypt=True;TrustServerCertificate=False"
```

**2. JWT imza anahtarı** (en az 32 karakter, rastgele üretin):
```powershell
[Convert]::ToBase64String((1..48 | %{Get-Random -Max 256}))
```
Çıkan değeri `Jwt:SigningKey`'e yapıştırın.

**3. Frontend adresi** (frontend ayrı bir adresteyse):
```json
"Cors": { "AllowedOrigins": [ "https://[UYGULAMA_ADRESI]" ] }
```
Frontend ile API aynı IIS sitesindeyse bu listeyi **boş** bırakın.

> Şablonun içinde her satırın açıklaması var. Sadece `[...]` yerlerini doldurun.

---

## ADIM 4 — IIS'e yerleştirin

1. `publish\` klasörünü sunucuya kopyalayın, örn. `C:\inetpub\acrfilo`
2. IIS Yöneticisi → **Uygulama Havuzu** ekleyin:
   - .NET CLR sürümü: **Yönetilen kod yok** (No Managed Code)
   - Kimlik: `ApplicationPoolIdentity` (varsayılan)
3. IIS Yöneticisi → **Web Sitesi** ekleyin:
   - Fiziksel yol: `C:\inetpub\acrfilo`
   - Bağlama: `https`, port 443, sertifikanızı seçin
4. `logs\` klasörünün yazılabilir olduğundan emin olun (uygulama oraya log yazar).

### Windows Authentication kullanacaksanız (SQL parolası olmadan — en güvenli)

- Uygulama havuzu kimliğine SQL'de erişim verin:
  ```sql
  CREATE LOGIN [IIS APPPOOL\acrfilo] FROM WINDOWS;
  USE AcrFilo; CREATE USER [IIS APPPOOL\acrfilo] FOR LOGIN [IIS APPPOOL\acrfilo];
  ```
- Bağlantı dizesinde parola yerine: `Integrated Security=true`
- Sonra `03-seed.sql`'i `AppLogin="IIS APPPOOL\acrfilo"` ile bir kez daha çalıştırın
  (yalnız yetki bölümü işler).

---

## ADIM 5 — Yönetici parolasını atayın

Seed scripti parola **içermez** (güvenlik). Yönetici hesabı pasif oluşturulur.
Parolayı atamak için, `publish\` klasöründe:

```powershell
dotnet Acr.Filo.Api.dll --set-admin-password
```

Komut parolayı ekranda göstermeden sorar, PBKDF2 ile hashler, hesabı aktifleştirir.
Parola politikası: en az 12 karakter, büyük+küçük harf+rakam.

---

## ADIM 6 — Çalışıyor mu kontrol edin

```powershell
# Sağlık kontrolü (kimlik gerektirmez)
curl https://[UYGULAMA_ADRESI]/api/health
# Beklenen: {"status":"healthy","database":true,...}
```

Sonra tarayıcıda `https://[UYGULAMA_ADRESI]` açın, yönetici e-postası + attığınız
parola ile giriş yapın.

---

## Sorun giderme

| Belirti | Olası neden | Çözüm |
|---|---|---|
| `500.19` veya `500.30` | Hosting Bundle yok | ADIM'daki Hosting Bundle'ı kurun, IIS restart |
| `/api/health` → `database:false` | Bağlantı dizesi yanlış | `appsettings.Production.json` FiloDb satırı |
| Login → "parola atanmamış" | ADIM 5 atlandı | `--set-admin-password` çalıştırın |
| Login → "Encrypt" / sertifika hatası | SQL sertifikası geçersiz | Bağlantıda `Encrypt=True` + geçerli SQL sertifikası |
| Sayfa açılıyor ama API 401 | JWT SigningKey boş | ADIM 3'te anahtar üretip yapıştırın |
| Yetki hatası (403) | Kullanıcının rolü yetersiz | Doğru — rol/yetki matrisine bakın (README) |

Sağlık kontrolü `healthy` dönüyor ve giriş yapabiliyorsanız kurulum tamamdır.
