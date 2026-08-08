#!/usr/bin/env python3
"""Faz 2 tutarlilik: controller yetkileri <-> seed yetkileri <-> C# Permissions sabitleri.
Ayrica servis arayuzu <-> implementasyon method sayisi kabaca eslesiyor mu."""
import re, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent
errors, infos = [], []
def read(p): return (ROOT/p).read_text(encoding='utf-8')

# 1) Controller'larda kullanilan tum policy string'leri
ctrl_policies = set()
for f in (ROOT/'src/Acr.Filo.Api/Controllers').glob('*.cs'):
    for m in re.finditer(r'Authorize\(Policy\s*=\s*"([\w\.]+)"\)', f.read_text(encoding='utf-8')):
        ctrl_policies.add(m.group(1))

# 2) Seed'deki yetkiler
seed = read('db/03-seed.sql')
block = re.search(r"MERGE dbo\.Permissions.*?USING \(VALUES(.*?)\) AS s", seed, re.S)
seed_perms = set(re.findall(r"\('([\w\.]+)'", block.group(1))) if block else set()

# 3) C# Permissions sabitleri
cs_perms = set(re.findall(r'"([\w\.]+)"', read('src/Acr.Filo.Domain/Entities/Auth/Permission.cs')))
cs_perms = {p for p in cs_perms if '.' in p}

infos.append(f"Controller policy: {len(ctrl_policies)} | Seed yetki: {len(seed_perms)} | C# sabit: {len(cs_perms)}")

# Her controller policy'si seed'de OLMALI
for p in sorted(ctrl_policies - seed_perms):
    errors.append(f"[Controller] '{p}' policy'si kullaniliyor ama 03-seed.sql'de YOK -> kimse erisemez")

# Her controller policy'si C# sabiti OLMALI (tutarlilik)
for p in sorted(ctrl_policies - cs_perms):
    errors.append(f"[Controller] '{p}' policy'si Permissions sabitlerinde YOK")

# 4) Servis arayuzu <-> impl eslesme (method adlari)
pairs = [
    ('src/Acr.Filo.Application/Orders/IOrderService.cs', 'src/Acr.Filo.Infrastructure/Services/OrderService.cs'),
    ('src/Acr.Filo.Application/Definitions/DefinitionDtos.cs', 'src/Acr.Filo.Infrastructure/Services/DefinitionService.cs'),
    ('src/Acr.Filo.Application/Users/UserDtos.cs', 'src/Acr.Filo.Infrastructure/Services/UserService.cs'),
    ('src/Acr.Filo.Application/Reports/ReportDtos.cs', 'src/Acr.Filo.Infrastructure/Services/ReportService.cs'),
    ('src/Acr.Filo.Application/Auth/IAuthService.cs', 'src/Acr.Filo.Infrastructure/Auth/AuthService.cs'),
]
for iface_f, impl_f in pairs:
    iface = read(iface_f)
    impl = read(impl_f)
    # arayuzdeki Task<...> MethodName( imzalarini bul
    methods = set(re.findall(r'Task(?:<[^>]+>)?\s+(\w+)\s*\(', iface))
    for m in sorted(methods):
        if not re.search(rf'\b(public\s+(async\s+)?)?Task(?:<[^>]+>)?\s+{m}\s*\(', impl):
            # implementasyonda public async Task ... m( var mi
            if f' {m}(' not in impl and f'>{m}(' not in impl:
                errors.append(f"[{Path(impl_f).name}] arayuzdeki '{m}' metodu implementasyonda bulunamadi")
    if methods:
        infos.append(f"{Path(iface_f).name}: {len(methods)} metod arayuzde")

print("="*66)
for i in infos: print(f"  bilgi : {i}")
for e in errors: print(f"  HATA  : {e}")
print("="*66)
print(f"Sonuc: {len(errors)} hata")
sys.exit(1 if errors else 0)
