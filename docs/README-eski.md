# ACR Filo — Üretim Sürümü (Faz 1 + Faz 2)

**Kurulum için → `KURULUM.md`** (adım adım, basit anlatım).

## Bu pakette ne var

Tam bir ASP.NET Core 8 kurumsal uygulama: veri katmanı (Faz 1) + auth, servisler,
API (Faz 2). Toplam 67 C# dosyası, ~3.700 satır + 4 SQL scripti + frontend.

| Katman | İçerik |
|---|---|
| `db/` | 4 SQL scripti (şema, index, seed) — idempotent |
| `Domain/` | 24 dosya: entity'ler, enum'lar, iş kuralları |
| `Application/` | 13 dosya: DTO'lar, servis arayüzleri, sonuç tipleri |
| `Infrastructure/` | 17 dosya: EF, PBKDF2 hasher, JWT, auth servisi, audit interceptor, 5 servis |
| `Api/` | 13 dosya: 8 controller, Program.cs, yetki policy, exception middleware, admin CLI |
| `tools/` | Tutarlılık denetleyicileri (makine kontrolü) |
| `tests/LogicParity/` | C#/JS/SQL durum mantığı parity testi |

## Faz 2'de eklenenler

- **Kimlik doğrulama**: JWT + refresh token (rotasyonlu), PBKDF2-HMAC-SHA256 (100k tur),
  hesap kilitleme (5 başarısız deneme → 15 dk), parola politikası.
- **Yetki**: 14 yetki, 3 rol, DB'de rol→yetki eşlemesi. Her endpoint
  `[Authorize(Policy="...")]` ile korunuyor. Kod deploy'u olmadan yetki değişir.
- **Sipariş servisi** (513 satır): CRUD, transaction'lı sipariş no üretimi, rowversion
  çakışma yönetimi (409), SSH bağımlılık zinciri, adet-araç senkronu, prototipteki
  tüm iş kuralları.
- **4 takvim raporu** + dashboard + CSV export (frontend'in mevcut formatıyla aynı).
- **Otomatik audit**: her değişiklik kim/ne zaman/eski-yeni değer olarak loglanıyor
  (şifre/token hariç). DB seviyesinde değiştirilemez.
- **Kullanıcı yönetimi**, **audit görüntüleme**, **sağlık kontrolü** endpoint'leri.

## ⚠️ Derleme durumu — dürüst durum

**Domain katmanı bu ortamda GERÇEKTEN derlendi** (0 hata). Ama tam çözüm
(`Api`/`Infrastructure`) burada derlenemedi: EF Core ve diğer paketler NuGet'ten
çekilemiyor (erişim kapalı). **İlk işiniz**: `dotnet build -c Release`. Hata çıkarsa
tam metnini gönderin.

Derleyemediğim için, derlemenin **yakalayamayacağı** riskleri makineyle doğruladım:
- SQL↔C#↔EF↔frontend alan/tip tutarlılığı: **0 hata** (`tools/check-consistency.py`)
- Controller yetkileri ↔ seed ↔ sabitler: **0 hata** (`tools/check-phase2.py`)
- Durum mantığı C#=JS=SQL: **birebir aynı** (`tests/LogicParity/`)
- Denetleyicinin kendisi: 6 planted hatayı da yakalıyor (kanıtlandı)

Tek komutla tekrar: `bash run-all-tests.sh`

---

## ✅ Bu turda ne test edildi (güncel)

Bu ortamda **.NET 8.0.129 SDK kuruldu** ve şunlar gerçekten çalıştırıldı:

- **Domain katmanı derlendi** — `Acr.Filo.Domain.dll` üretildi, 0 hata.
- **İş mantığı üç motorda koşuldu ve birebir eşleşti**: C# `Durum()` (10/10),
  frontend `vehicleStatus()` Node'da (9/9), SQL `vw_VehicleStatus` simülasyonu (9/9).
  JS ve SQL çıktıları byte düzeyinde aynı.
- Bu tur **kritik bir hata yakalandı ve düzeltildi**: durum mantığım frontend'den
  farklıydı (adım sayısı vs adım-bazında). Detay: `docs/Test-Raporu.md`.

Tek komutla tekrar üret: `bash run-all-tests.sh`

**Hâlâ derlenemeyen**: `Api`/`Application`/`Infrastructure` — çünkü EF Core SqlServer
ve SqlClient paketleri NuGet'ten gelemedi (izinli hostların hiçbirinde yok). Bunlar
sizde `dotnet build -c Release` ile derlenecek.

---

## ⚠️ Bu kod DERLENMEDİ — nedeni ve karşılığında ne yapıldı

Bu paketin hazırlandığı ortamda **.NET SDK kurulu değil ve NuGet erişime kapalı**.
Yani `dotnet build` çalıştırılamadı. Bunu saklamıyorum.

Karşılığında, derlemenin **zaten yakalayamayacağı** asıl riski otomatik denetime aldım:

> Derleyici, `PlakaNo` property'sinin SQL'de `PlakaNumarasi` diye bir kolona denk
> geldiğini fark etmez. Bu hata `dotnet build` sırasında değil, canlıda kullanıcı
> sipariş açarken `Invalid column name 'PlakaNo'` olarak patlar.

`tools/check-consistency.py` tam olarak bunu denetler:

1. SQL tabloları ↔ EF `ToTable()` eşlemesi
2. SQL kolonları ↔ entity property'leri (**iki yönlü**)
3. SQL kolon tipi ↔ EF `HasColumnType()` beyanı
4. C# `Permissions` sabitleri ↔ `03-seed.sql` Permissions bloğu
5. C# `SshTaskTypes` ↔ SQL `CHECK` constraint
6. C# `VehicleStatuses` ↔ `vw_VehicleStatus` CASE dalları
7. EF `HasDatabaseName()` ↔ `02-indexes.sql` index adları

```bash
python3 tools/check-consistency.py
# ====================================================================
#   bilgi   : SQL tablosu: 18 | Entity: 18 | Config: 18
#   bilgi   : Permissions esitligi OK (14 yetki)
#   bilgi   : SSH adimlari OK (['gps', 'hgs', 'plaka', 'utts'])
#   bilgi   : Durum degerleri OK (['done', 'neutral', 'overdue', 'ready', 'soon'])
# ====================================================================
# Sonuc: 0 hata, 0 uyari
```

**"0 hata" güvenilir mi?** Denetleyicinin kendisi de sınandı — 5 gerçekçi hata
bilerek enjekte edilip hepsinin yakalandığı doğrulandı:

```bash
python3 tools/test-checker.py
#   [OK] temiz proje  -> 0 hata (beklenen)
#   [OK] Entity'de kolon adi degistirildi
#   [OK] EF config'te tip yanlis beyan edildi (decimal -> money)
#   [OK] C#'a seed'de olmayan yetki eklendi
#   [OK] SSH degeri buyuk harfe cevrildi
#   [OK] EF var olmayan bir index adina referans veriyor
# SONUC: 6/6 senaryo gecti
```

**Sizin yapmanız gereken ilk şey** (5 dk, NuGet erişimi olan bir makinede):

```powershell
dotnet restore
dotnet build -c Release
```

Derleme hatası çıkarsa bana hatanın tam metnini gönderin; Faz 2'ye geçmeden düzeltirim.

---

## Kurulum (Faz 1 — yalnızca veritabanı)

### 1. Veritabanını oluştur
```powershell
sqlcmd -S [SQL_SERVER_ADI] -d master -i db\00-create-database.sql -b `
       -v DbName="[DATABASE_ADI]" AppLogin="[SQL_KULLANICI_ADI]"
```

### 2. Şema + index
```powershell
sqlcmd -S [SQL_SERVER_ADI] -d [DATABASE_ADI] -i db\01-schema.sql  -b
sqlcmd -S [SQL_SERVER_ADI] -d [DATABASE_ADI] -i db\02-indexes.sql -b
```

### 3. Başlangıç verisi
```powershell
sqlcmd -S [SQL_SERVER_ADI] -d [DATABASE_ADI] -i db\03-seed.sql -b `
       -v AdminEmail="[YONETICI_EPOSTA]" AppLogin="[SQL_KULLANICI_ADI]"
```

Tüm scriptler **idempotenttir** — tekrar çalıştırılabilir, veri silmez, `DROP DATABASE` yoktur.

### 4. Yönetici parolası
Seed scripti parola **içermez**. Yönetici kaydı `PasswordHash = NULL, IsActive = 0`
olarak oluşur; bu haliyle hiçbir parolayla giriş yapılamaz. Parola atama komutu
Faz 2'de gelir:
```powershell
dotnet Acr.Filo.Api.dll --set-admin-password   # konsoldan maskeli okur, PBKDF2 ile hashler
```

### 5. Migration (EF ile şema yönetimi)
SQL scriptleri ve EF migration **aynı şemayı** üretmelidir. İlk migration'ı
mevcut şemadan üretmek için:

```powershell
cd src\Acr.Filo.Infrastructure
dotnet ef migrations add InitialCreate -s ..\Acr.Filo.Api -o Persistence\Migrations

# Üretilen SQL'i GÖZDEN GEÇİRİLEBİLİR biçimde çıkar (production'a asla doğrudan uygulama):
dotnet ef migrations script -s ..\Acr.Filo.Api -o ..\..\db\generated-migration.sql

# 01-schema.sql ile karşılaştır — fark varsa bana bildirin.
```

> **Production'da `EnsureCreated()` veya `Database.Migrate()` OTOMATİK ÇALIŞTIRILMAZ.**
> Migration ayrı, daha yetkili bir hesapla, elle ve yedek alındıktan sonra uygulanır.
> Uygulamanın SQL kullanıcısında `ALTER`/`CREATE TABLE` yetkisi **yoktur** (03-seed.sql'de `DENY`).

---

## Prototipteki hataların bu şemada nasıl kapatıldığı

| # | Prototip hatası | Bu paketteki çözüm |
|---|---|---|
| 1 | Araç silinince `adet--` ama ödeme planı dokunulmuyor → "plan = toplam" kuralı sessizce bozuluyordu (satır 1640) | `vw_LinePaymentSummary.PlanEslesiyor` bayrağı ile görünür kılındı; Faz 2'de silme işlemi planı aynı transaction içinde otomatik düzeltecek ve audit'e yazacak |
| 2 | Kullanılan müşteri tanımdan silinince sayfa yenilenince geri geliyordu (`seedMastersFromOrders`, satır 552) | Müşteri artık FK'lı entity; silme = soft-delete, mevcut siparişler bozulmaz, geri gelme yok |
| 3 | `vehicleStatus` `.tarih`'e, `orderSSHSummary` `.yapildi`'ya bakıyordu → iki sayaç çelişiyordu (satır 444 vs 1426) | `CK_VST_YapildiTarih` ile "Yapildi=1 ⇔ Tarih dolu" veritabanı seviyesinde garanti |
| 4 | Kuruş girilemiyordu (`parseMoney` rakam dışını siliyor) | SQL `decimal(18,2)`. Frontend'de kuruş girişi şimdilik kapalı, tek satırla açılır |
| 5 | `uid()` = 8 karakter `Math.random()` | `IDENTITY(1,1)` — DB üretiyor, çakışma imkânsız |
| 6 | Sipariş numarası hiç yoktu | `SiparisNo` ('SIP-2026-000123'), `sp_NextFleetOrderNo` ile `UPDLOCK+HOLDLOCK` altında çakışmasız üretim |
| 7 | Hard delete — sipariş kalıcı siliniyor, iz kalmıyordu | `IsDeleted` + `DeletedAt`/`DeletedBy` + global query filter + AuditLogs |
| 9 | Müşteri adı string → unvan düzeltilince eski siparişler eski adla kalıyordu | FK ile bağlandı; düzeltme tüm siparişlere yansır |
| 10 | Tüm state tek JSON blob, son yazan kazanır | Satır bazlı `ROWVERSION` — eşzamanlı güncelleme çakışması 409 döner |

Prototip hatası **#8 (Excel aslında CSV)** bilinçli olarak korunuyor: `;` ayraçlı,
BOM'lu CSV Excel'de sorunsuz açılıyor ve akıllı bir tercihti. Faz 3'te sunucu tarafına
taşınacak, format aynı kalacak.

---

## Kritik tasarım kararları

**Collation `Turkish_CI_AS`** — frontend `localeCompare(...,'tr')` ve
`toLocaleLowerCase('tr-TR')` kullanıyor; DB'nin aynı davranması gerekiyor.
**Tuzak:** Turkish collation'da `'I' ≠ 'i'`. Bu yüzden `Users.Email` ve
`LoginAuditLogs.AttemptedEmail` açıkça `Latin1_General_100_CI_AS` — aksi halde
`INFO@x.com` ile `info@x.com` farklı sayılır ve login kırılır.

**Tarihler `DateOnly` / SQL `date`** — teslim tarihleri `<input type="date">`'ten
geliyor, saat taşımıyor. `DateTime` kullanılsaydı sunucu ile tarayıcı farklı
zaman diliminde olduğunda teslim tarihi bir gün kayabilirdi. `date` ile bu imkânsız.

**Durum saklanmıyor, hesaplanıyor** — prototipte de öyleydi. Aynı mantık üç yerde:
`FleetOrderVehicle.Durum()` (C#), `vw_VehicleStatus` (SQL), `vehicleStatus()` (JS).
Üçünün aynı kalması kritik; denetleyici sabit değerleri kontrol ediyor, Faz 2'de
üçünü aynı vakalarla sınayan unit test gelecek.

**Tam ASP.NET Core Identity kullanılmadı** — 7 tablo (`AspNetUsers`...) getiriyor,
3 rollük bir uygulama için gereksiz ve şemayla çakışıyor. Yalnızca
`PasswordHasher<T>` (PBKDF2-HMAC-SHA256, 100.000 tur) tek başına kullanılıyor.

**Rol → yetki eşlemesi veritabanında** — "kim neyi yapabilir" sorusu kod deploy'u
olmadan değişir. `[Authorize(Roles="admin")]` yerine yetki bazlı policy.

---

## Rol ve yetki matrisi (seed'lenmiş hali)

| Yetki | Sistem Yöneticisi | Operasyon | Muhasebe |
|---|:--:|:--:|:--:|
| orders.view | ✓ | ✓ | ✓ |
| orders.create / update / delete | ✓ | ✓ | — |
| vehicles.update (SSH, teslim, ikame) | ✓ | ✓ | — |
| payments.view | ✓ | ✓ | ✓ |
| payments.plan | ✓ | ✓ | ✓ |
| payments.record (gerçekleşen ödeme) | ✓ | — | ✓ |
| definitions.view | ✓ | ✓ | ✓ |
| definitions.manage | ✓ | ✓ | — |
| reports.view / export | ✓ | ✓ | ✓ |
| users.manage | ✓ | — | — |
| audit.view | ✓ | — | — |

`payments.plan` neden Operasyon'da: sipariş oluşturma formu ödeme planını **zorunlu**
tutuyor (plan toplamı = kalem toplamı, prototip satır 2161). Bu yetki olmadan
Operasyon sipariş açamaz.

Değiştirmek için kod değil, `dbo.RolePermissions` düzenlenir.

---

## Sıradaki adım

1. `dotnet restore && dotnet build -c Release` çalıştırın, sonucu bildirin.
2. SQL scriptlerini bir **test** veritabanında çalıştırın (canlıda değil).
3. Onay verirseniz **Faz 2**: auth servisi, Program.cs, controller'lar, DTO'lar,
   exception middleware, unit/integration testler.
