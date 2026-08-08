# ACR Filo — Sipariş Yönetim Sistemi

Filo/araç sipariş takibi için ASP.NET Core 8 + SQL Server + IIS üzerinde çalışan kurumsal web uygulaması. Sipariş → tedarikçi bazında ödeme → SSH hazırlığı (plaka, HGS, GPS, UTTS) → müşteriye teslim akışını tek ekrandan yönetir.

> **Bu repo özeldir.** İçinde iş mantığı ve şema bulunur. Erişim yalnızca davet edilen kişilerdedir.

---

## Teknoloji

| Katman | Teknoloji |
|---|---|
| Backend | ASP.NET Core 8 (Web API) |
| Veritabanı | SQL Server 2019+ (T-SQL şema, view, stored procedure) |
| Kimlik doğrulama | JWT + refresh token, PBKDF2 parola hash |
| Frontend | Tek sayfa HTML/JS (API'ye fetch ile bağlı) |
| Barındırma | IIS (Windows Server) |

---

## Proje yapısı

```
src/
  Acr.Filo.Domain/          Entity'ler, enum'lar, is kurallari
  Acr.Filo.Application/     DTO'lar, servis arayuzleri
  Acr.Filo.Infrastructure/  EF Core, servisler, auth, audit
  Acr.Filo.Api/             Controller'lar, Program.cs, middleware
    wwwroot/                Frontend (index.html, api.js, bridge.js)
db/                         SQL scriptleri (sirayla calistirilir)
tools/                      Tutarlilik denetleyicileri
tests/                      Birim + parity testleri
docs/                       Ek dokumanlar
```

---

## İlk kurulum (sıfırdan)

### Gerekenler
- .NET 8 SDK
- .NET 8 Hosting Bundle (IIS icin)
- SQL Server 2019+
- IIS (Windows Server)

### 1. Kodu al ve derle
```powershell
git clone https://github.com/<KULLANICI>/<REPO>.git
cd <REPO>
dotnet restore
dotnet publish src\Acr.Filo.Api\Acr.Filo.Api.csproj -c Release -o publish
```

### 2. Veritabanini kur
Scriptleri SIRAYLA calistirin (-I bayragi onemli):
```powershell
sqlcmd -S . -E -d master  -i db\00-create-database.sql -b
sqlcmd -S . -E -d AcrFilo -i db\01-schema.sql  -b -I
sqlcmd -S . -E -d AcrFilo -i db\02-indexes.sql -b -I
sqlcmd -S . -E -d AcrFilo -i db\03-seed.sql    -b -I -v AdminEmail="admin@sirket.com" AppLogin="acrfilo_app"
sqlcmd -S . -E -d AcrFilo -i db\04-cekici-kolonu.sql   -b -I
sqlcmd -S . -E -d AcrFilo -i db\05-marka-model-fix.sql -b -I
```
04 ve 05 idempotent migration'dir (mevcut veritabanina da guvenle uygulanir).

### 3. Yapilandirma
src\Acr.Filo.Api\appsettings.Production.example.json dosyasini kopyalayip
appsettings.Production.json yapin, [...] yerlerini doldurun.
Bu dosya .gitignore ile gizlenir - ASLA commit edilmez.

JWT anahtari uretmek icin:
```powershell
[Convert]::ToBase64String((1..48 | %{Get-Random -Max 256}))
```

### 4. IIS'e yerlestir
publish\ icerigini IIS site klasorune kopyalayin, uygulama havuzunu No Managed Code
yapin. Data Protection icin yazilabilir keys\ klasoru ve IIS kimligine SQL erisimi gerekir.
Ayrintili adim adim: KURULUM.md

### 5. Yonetici parolasi ata
```powershell
cd publish
dotnet Acr.Filo.Api.dll --set-admin-password
```

---

## Kullanici yonetimi (komut satiri)

```powershell
dotnet Acr.Filo.Api.dll --add-user       # yeni kullanici (e-posta, ad, rol, parola sorar)
dotnet Acr.Filo.Api.dll --list-users     # kullanicilari listele
dotnet Acr.Filo.Api.dll --set-admin-password
```
Roller: admin (tam yetki), operasyon (siparis/arac/tanim/rapor), muhasebe (odeme/goruntuleme).

---

## Gelistirme akisi (bu repo ile calisma)

Iki kisi gelistirir. HER DEGISIKLIKTEN ONCE guncel kodu cekin, sonra gonderin:

```powershell
git pull                          # en guncel hali al (BASLAMADAN ONCE)
# ... degisikliklerinizi yapin ...
git add .
git commit -m "Ne degistirdiginizi yazin"
git push                          # GitHub'a gonder
```

KURAL: Calismaya baslamadan once mutlaka git pull yapin. Boylece arkadasinizin
degisiklikleri sizde olur ve cakisma yasanmaz.

### Degisiklik turleri
- Sadece frontend (wwwroot/*.html, *.js): derleme gerekmez, IIS'e kopyala + Ctrl+Shift+R
- Backend (src/**/*.cs): dotnet publish + IIS'e kopyala + app pool restart
- Veritabani: yeni bir db/NN-aciklama.sql migration yaz (idempotent), sirayla calistir

---

## Onemli notlar

- appsettings.Production.json ASLA commit edilmez (JWT anahtari + baglanti dizesi icerir).
- bin/, obj/, publish/, keys/, logs/ git'e girmez (.gitignore).
- Veritabani degisiklikleri MIGRATION olarak eklenir (04, 05 gibi), 01-schema.sql sadece sifirdan kurulum icin.
- SQL scriptleri -I bayragi ile calistirilir (QUOTED_IDENTIFIER geregi).
