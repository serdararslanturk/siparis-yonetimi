# Yayınlama (Deploy) Kılavuzu

GitHub'dan çektiğiniz kodu sunucuya alma. Gizli ayarlarınıza (JWT anahtarı,
bağlantı dizesi) **hiç dokunmadan** çalışır.

---

## İlk kurulum (sadece bir kez, her sunucuda)

1. Kodu GitHub'dan al:
   ```powershell
   cd C:\Users\Administrator\Desktop
   git clone https://github.com/serdararslanturk/siparis-yonetimi.git
   cd siparis-yonetimi
   ```

2. Config'i oluştur (bir kez):
   ```powershell
   Copy-Item "src\Acr.Filo.Api\appsettings.Production.example.json" "src\Acr.Filo.Api\appsettings.Production.json"
   notepad "src\Acr.Filo.Api\appsettings.Production.json"
   ```
   `[...]` yerlerini doldurun (JWT anahtarı, bağlantı dizesi).
   > Bu dosya IIS klasöründe zaten varsa (mevcut kurulum), oraya dokunmayın.

3. İlk yayınlama:
   ```powershell
   .\deploy\deploy.ps1
   ```

---

## Günlük akış (her değişiklikte)

### Değişikliği yapan kişi (siz veya arkadaşınız)
```powershell
# ÇALIŞMAYA BAŞLAMADAN ÖNCE:
git pull                    # arkadaşının son değişikliklerini al

# ... kodu değiştir ...

git add .
git commit -m "Ne değiştirdiğinizi yazın"
git push                    # GitHub'a gönder
```

### Sunucuya alma (yayına geçirme)
Sunucuda, proje klasöründe (PowerShell yönetici olarak):
```powershell
cd C:\Users\Administrator\Desktop\siparis-yonetimi
git pull                    # GitHub'daki güncel kodu çek
.\deploy\deploy.ps1         # derle + yayınla (config'e dokunmadan)
```
Tarayıcıda **Ctrl+Shift+R**.

---

## Hızlı seçenekler

**Sadece frontend değiştiyse** (wwwroot içindeki html/js — derleme gereksiz):
```powershell
.\deploy\deploy.ps1 -SadeceFrontend
```

**Farklı IIS klasörü / havuz adı** (varsayılan değilse):
```powershell
.\deploy\deploy.ps1 -SitePath "D:\web\filo" -AppPool "FiloHavuz"
```

**Veritabanı değişikliği varsa** (yeni bir `db\NN-*.sql` migration eklendiyse):
Deploy'dan önce onu çalıştırın:
```powershell
sqlcmd -S . -E -d AcrFilo -I -i db\NN-aciklama.sql -b
```

---

## Script ne yapıyor (özet)

1. `dotnet publish` ile derler
2. IIS havuzunu durdurur
3. Derlenen dosyaları IIS klasörüne kopyalar — **`appsettings.Production.json`, `keys/`, `logs/` HARİÇ** (bunlar sunucuda kalır)
4. Havuzu başlatır
5. Sağlık kontrolü yapar

Yani gizli ayarlarınız her deploy'da olduğu yerde kalır; siz sadece kodu güncellersiniz.
