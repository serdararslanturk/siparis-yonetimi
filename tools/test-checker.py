#!/usr/bin/env python3
"""
check-consistency.py'nin KENDİSİNİ sınar.

Neden: hiç hata bulamayan bir denetleyici, ya proje temizdir ya da denetleyici
bozuktur. İkisini ayırt etmenin tek yolu, bilerek hata yerleştirip yakalanmasını
beklemektir. Bu script projeyi geçici bir klasöre kopyalar, sırayla gerçek
hayatta olabilecek 5 hatayı enjekte eder ve her birinde denetleyicinin
BAŞARISIZ olmasını (exit 1) bekler.

Kullanım: python3 tools/test-checker.py
"""
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# (ad, dosya, aranan, yerine, beklenen hata parçası)
CASES = [
    (
        "Entity'de kolon adi degistirildi (SQL'de karsiligi kalmaz)",
        "src/Acr.Filo.Domain/Entities/Orders/FleetOrderVehicle.cs",
        "public string? PlakaNo { get; set; }",
        "public string? PlakaNumarasi { get; set; }",
        "PlakaNumarasi",
    ),
    (
        "EF config'te tip yanlis beyan edildi (decimal -> money)",
        "src/Acr.Filo.Infrastructure/Persistence/Configurations/OrderConfigurations.cs",
        'e.Property(x => x.BirimBedel).HasColumnType("decimal(18,2)").IsRequired();',
        'e.Property(x => x.BirimBedel).HasColumnType("money").IsRequired();',
        "tip uyusmuyor",
    ),
    (
        "C#'a seed'de olmayan yetki eklendi",
        "src/Acr.Filo.Domain/Entities/Auth/Permission.cs",
        'public const string AuditView         = "audit.view";',
        'public const string AuditView         = "audit.view";\n    public const string Hayali = "orders.approve";',
        "orders.approve",
    ),
    (
        "SSH degeri buyuk harfe cevrildi (frontend 'plaka' bekler)",
        "src/Acr.Filo.Domain/Enums/SshTaskType.cs",
        'public const string Plaka = "plaka";',
        'public const string Plaka = "Plaka";',
        "[SSH]",
    ),
    (
        "EF var olmayan bir index adina referans veriyor",
        "src/Acr.Filo.Infrastructure/Persistence/Configurations/OrderConfigurations.cs",
        'HasDatabaseName("UX_FleetOrders_SiparisNo")',
        'HasDatabaseName("IX_Olmayan_Index")',
        "IX_Olmayan_Index",
    ),
]


def run(work: Path):
    r = subprocess.run(
        [sys.executable, str(work / "tools/check-consistency.py")],
        capture_output=True, text=True, cwd=str(work),
    )
    return r.returncode, r.stdout


def main():
    # 0. Temiz halde GEÇMELİ
    with tempfile.TemporaryDirectory() as td:
        work = Path(td) / "proj"
        shutil.copytree(ROOT, work)
        code, out = run(work)
        if code != 0:
            print("BASARISIZ: temiz proje hata veriyor, once onu duzeltin\n" + out)
            return 1
        print("  [OK] temiz proje  -> 0 hata (beklenen)")

    failed = 0
    for name, rel, old, new, expect in CASES:
        with tempfile.TemporaryDirectory() as td:
            work = Path(td) / "proj"
            shutil.copytree(ROOT, work)
            f = work / rel
            s = f.read_text(encoding="utf-8")
            if old not in s:
                print(f"  [KURULUM HATASI] '{name}': aranan metin bulunamadi -> {rel}")
                failed += 1
                continue
            f.write_text(s.replace(old, new, 1), encoding="utf-8")

            code, out = run(work)
            caught = code != 0 and expect in out
            print(f"  [{'OK' if caught else 'BASARISIZ'}] {name}")
            if not caught:
                failed += 1
                print(f"      beklenen: '{expect}' | exit={code}")
                print("      " + "\n      ".join(out.strip().splitlines()[-6:]))

    print("-" * 68)
    if failed:
        print(f"SONUC: {failed}/{len(CASES)+1} senaryo BASARISIZ — denetleyiciye guvenilemez.")
        return 1
    print(f"SONUC: {len(CASES)+1}/{len(CASES)+1} senaryo gecti — denetleyici gercekten calisiyor.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
