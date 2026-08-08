# Test Raporu — Faz 1

Bu rapor, bu ortamda **gerçekten çalıştırılmış** testleri belgeler. Aşağıdaki her
sonuç makine çıktısıdır, tahmin değildir. Hepsini tek komutla tekrar üretebilirsiniz:

```bash
bash run-all-tests.sh
```

## Ortam

| Bileşen | Durum |
|---|---|
| .NET SDK | ✅ 8.0.129 kuruldu (Ubuntu deposundan) |
| Node.js | ✅ v22 (frontend JS'i koşmak için) |
| Python | ✅ 3.12 (denetleyici + SQL simülasyonu) |
| NuGet (nuget.org) | ❌ Erişime kapalı (403) — bkz. aşağıdaki sınır |

## Çalıştırılamayan: tam çözüm derlemesi

`Microsoft.EntityFrameworkCore.SqlServer` ve `Microsoft.Data.SqlClient` paketleri
hiçbir izinli hosttan (nuget.org, github packages, azure CDN — hepsi 403) çekilemedi.
Bu yüzden `Api`, `Application`, `Infrastructure` projeleri bu ortamda derlenemedi.
Bunlar sizde tek komutla derlenecek: `dotnet build -c Release`.

**Bunun yerine, derlemenin zaten yakalayamayacağı asıl riskleri koşarak kanıtladım.**

## Çalıştırılan testler

### 1. Domain katmanı — GERÇEK derleme ✅
```
Acr.Filo.Domain -> bin/Release/net8.0/Acr.Filo.Domain.dll
Build succeeded. 0 Warning(s) 0 Error(s)
```
Tüm entity'ler, enum'lar ve iş kuralları C# olarak geçerli. Domain sıfır dış
bağımlılık taşıdığı için NuGet olmadan derlendi.

### 2. Tutarlılık denetleyicisi ✅ (0 hata, 0 uyarı)
SQL şeması ↔ C# entity ↔ EF konfigürasyonu ↔ frontend alan adları, 18 tablo × tüm
kolonlar, iki yönlü. Bir property'nin SQL'de karşılığı yoksa, ya da tip uyuşmuyorsa
yakalar — bu, canlıda `Invalid column name` olarak patlayacak hataların ta kendisi.

### 3. Denetleyicinin öz-testi ✅ (6/6)
"0 hata" diyen araç bozuk da olabilir. 5 gerçekçi hata bilerek enjekte edildi
(kolon adı değişimi, yanlış tip, hayali yetki, SSH büyük harf, olmayan index) —
hepsi yakalandı. Yani denetleyicinin "temiz" demesi gerçek.

### 4. İş mantığı parity — ÜÇ MOTOR ✅
Araç durumu üç yerde hesaplanıyor: C# (`FleetOrderVehicle.Durum()`), SQL
(`vw_VehicleStatus`), frontend JS (`vehicleStatus()`). Üçünün **aynı** sonucu
vermesi kritik — yoksa "ekran şunu, rapor bunu gösteriyor" hatası çıkar.

| Motor | Nasıl koşuldu | Sonuç |
|---|---|---|
| C# `Durum()` | `dotnet run` (gerçek Domain.dll) | 10/10 ✅ |
| Frontend `vehicleStatus()` | `node` (HTML'den birebir kopyalanan JS) | 9/9 ✅ |
| SQL `vw_VehicleStatus` | Python simülasyonu (CASE dalları motamot) | 9/9 ✅ |
| **Cross-check** | JS çıktısı == SQL çıktısı (byte düzeyinde) | **9/9 birebir** ✅ |

Frontend JS testte **değiştirilmedi** — yalnızca `todayISO()` sabit tarih döndürüyor
(test deterministik olsun diye). Mantığın kendisi HTML dosyasındaki satır 413–448 ile
aynı karakter.

### Bu turda düzeltilen kritik hata

İlk teslimde C#/SQL durum mantığım frontend'den **farklıydı**: ben "kaç SSH adımı
yapıldı" sayıyordum; frontend ise her adımı `PlanlananTeslim`'e göre ayrı
değerlendirip en kötüsünü alıyor. Çoğu vakada aynı sonuç çıkar ama **mantık farklı** —
tam da sonradan fark edilen sinsi hata türü. Parity testi bunu ortaya çıkardı;
C# ve SQL frontend'e birebir uyacak şekilde yeniden yazıldı ve üçü de yeşile döndü.

## Frontend JS parametreleriyle ilgili not

Frontend `daysUntil` ve SQL `DATEDIFF(DAY, bugün, plan)` aynı işareti üretir
(plan − bugün). "3 gün ve altı = soon" eşiği üçünde de aynı. Saat/zaman dilimi
sorunu yok çünkü tüm tarihler gün bazında (`date` / `DateOnly`), saat taşımıyor.

## Sizde koşulacaklar (bu ortamda yapılamayanlar)

1. `dotnet restore && dotnet build -c Release` — tam çözüm derlemesi
2. SQL scriptlerini test veritabanında çalıştırma
3. EF migration üretip `db/01-schema.sql` ile karşılaştırma (README'de komut)
4. Faz 2'de: gerçek SQL Server'a karşı integration testleri (bu view'ı gerçek
   veriyle koşup Python simülasyonunu doğrulamak)
