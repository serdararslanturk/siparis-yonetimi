# ============================================================================
#  deploy.ps1  —  ACR Filo tek-komut yayınlama
#
#  NE YAPAR:
#    1. Backend'i derler (dotnet publish)
#    2. IIS uygulama havuzunu durdurur
#    3. Derlenen dosyaları IIS klasörüne kopyalar
#       — appsettings.Production.json, keys/, logs/ HARİÇ (dokunulmaz!)
#    4. Havuzu tekrar başlatır
#    5. Sağlık kontrolü yapar
#
#  Yani: GitHub'dan 'git pull' ile kodu çektikten sonra bu script'i çalıştırın,
#  gizli ayarlarınıza (JWT anahtarı, bağlantı dizesi) hiç dokunmadan yayına alır.
#
#  KULLANIM (proje kökünde, PowerShell'i yönetici olarak açıp):
#     .\deploy\deploy.ps1
#
#  Frontend değiştiyse (sadece wwwroot) derlemeyi atlamak için:
#     .\deploy\deploy.ps1 -SadeceFrontend
# ============================================================================

param(
    [string]$SitePath   = "C:\inetpub\acrfilo",     # IIS site klasörü
    [string]$AppPool    = "AcrFiloPool",             # Uygulama havuzu adı
    [string]$HealthUrl  = "http://localhost:8089/api/health",
    [switch]$SadeceFrontend                          # sadece wwwroot kopyala, derleme yok
)

$ErrorActionPreference = "Stop"
$kok = Split-Path -Parent $PSScriptRoot   # proje kökü (deploy klasörünün üstü)
$csproj = Join-Path $kok "src\Acr.Filo.Api\Acr.Filo.Api.csproj"
$publish = Join-Path $kok "publish"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " ACR Filo — Yayınlama" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Proje kökü : $kok"
Write-Host "IIS klasör : $SitePath"
Write-Host "Havuz      : $AppPool"
Write-Host ""

# --- Ön kontrol ---
if (-not (Test-Path $SitePath)) {
    Write-Host "HATA: IIS klasörü bulunamadı: $SitePath" -ForegroundColor Red
    Write-Host "  -SitePath parametresiyle doğru yolu verin." -ForegroundColor Yellow
    exit 1
}
$prodConfig = Join-Path $SitePath "appsettings.Production.json"
if (-not (Test-Path $prodConfig)) {
    Write-Host "UYARI: $prodConfig yok!" -ForegroundColor Yellow
    Write-Host "  İlk kurulumda appsettings.Production.example.json'ı kopyalayıp" -ForegroundColor Yellow
    Write-Host "  düzenlemeniz gerekir. Yine de devam ediliyor..." -ForegroundColor Yellow
    Write-Host ""
}

Import-Module WebAdministration -ErrorAction SilentlyContinue

# --- 1) Derle (frontend-only değilse) ---
if (-not $SadeceFrontend) {
    Write-Host "[1/5] Derleniyor (dotnet publish)..." -ForegroundColor Green
    dotnet publish $csproj -c Release -o $publish
    if ($LASTEXITCODE -ne 0) {
        Write-Host "HATA: Derleme başarısız. Yukarıdaki hataları düzeltin." -ForegroundColor Red
        exit 1
    }
    Write-Host "  Derleme tamam." -ForegroundColor Green
} else {
    Write-Host "[1/5] Frontend-only mod: derleme atlandı." -ForegroundColor Yellow
}
Write-Host ""

# --- 2) Havuzu durdur ---
Write-Host "[2/5] Uygulama havuzu durduruluyor..." -ForegroundColor Green
try { Stop-WebAppPool -Name $AppPool; Start-Sleep -Seconds 3 } catch {
    Write-Host "  (Havuz zaten durmuş olabilir, devam.)" -ForegroundColor Yellow
}

# --- 3) Kopyala (gizli/kalıcı dosyalar HARİÇ) ---
Write-Host "[3/5] Dosyalar kopyalanıyor (config/keys/logs korunuyor)..." -ForegroundColor Green
if ($SadeceFrontend) {
    # Sadece wwwroot
    $kaynakWww = Join-Path $kok "src\Acr.Filo.Api\wwwroot"
    Copy-Item "$kaynakWww\*" -Destination (Join-Path $SitePath "wwwroot") -Recurse -Force
    Write-Host "  wwwroot kopyalandı." -ForegroundColor Green
} else {
    # Tüm publish — ama koru: appsettings.Production.json, keys, logs
    Get-ChildItem "$publish\*" -Exclude "appsettings.Production.json","keys","logs" |
        Copy-Item -Destination $SitePath -Recurse -Force
    Write-Host "  Backend + frontend kopyalandı (gizli ayarlar korundu)." -ForegroundColor Green
}
Write-Host ""

# --- 4) Havuzu başlat ---
Write-Host "[4/5] Uygulama havuzu başlatılıyor..." -ForegroundColor Green
Start-WebAppPool -Name $AppPool
Start-Sleep -Seconds 5

# --- 5) Sağlık kontrolü ---
Write-Host "[5/5] Sağlık kontrolü..." -ForegroundColor Green
try {
    $resp = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 15
    if ($resp.StatusCode -eq 200) {
        Write-Host "  ✓ Sağlıklı: $($resp.Content)" -ForegroundColor Green
        Write-Host ""
        Write-Host "==================================================" -ForegroundColor Cyan
        Write-Host " YAYINLAMA BAŞARILI" -ForegroundColor Green
        Write-Host "==================================================" -ForegroundColor Cyan
    } else {
        Write-Host "  ⚠ Beklenmeyen durum kodu: $($resp.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠ Sağlık kontrolü yanıt vermedi." -ForegroundColor Yellow
    Write-Host "    Uygulama başlıyor olabilir; birkaç saniye sonra tarayıcıda kontrol edin." -ForegroundColor Yellow
    Write-Host "    Hata: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Tarayıcıda Ctrl+Shift+R ile yenilemeyi unutmayın." -ForegroundColor Cyan
