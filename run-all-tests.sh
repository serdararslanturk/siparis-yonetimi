#!/usr/bin/env bash
# ACR Filo — tüm otomatik testleri tek komutta koşar.
# Kullanım: bash run-all-tests.sh
set -u
export DOTNET_CLI_TELEMETRY_OPTOUT=1
cd "$(dirname "$0")"
fail=0
line(){ printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

line "1/5  Tutarlilik denetleyicisi (SQL <-> C# <-> EF <-> frontend)"
python3 tools/check-consistency.py || fail=1

line "2/5  Denetleyicinin oz-testi (bilerek hata enjekte, yakalaniyor mu?)"
python3 tools/test-checker.py || fail=1

line "2b   Faz 2: controller yetkileri <-> seed <-> C# sabitleri"
python3 tools/check-phase2.py || fail=1

line "3/5  Domain katmani derleniyor"
if command -v dotnet >/dev/null 2>&1; then
  dotnet build src/Acr.Filo.Domain/Acr.Filo.Domain.csproj -c Release --nologo 2>&1 \
    | grep -E "Build succeeded|error|Warning\(s\)" || fail=1
else
  echo "  (dotnet yok — bu adim atlandi)"
fi

line "4/5  Is mantigi parity: C# Durum() = frontend vehicleStatus() = SQL view"
if command -v dotnet >/dev/null 2>&1; then
  dotnet run --project tests/LogicParity/LogicParity.csproj -c Release --nologo 2>/dev/null || fail=1
fi
if command -v node >/dev/null 2>&1; then
  ( cd tests/LogicParity && node parity-frontend.mjs ) || fail=1
fi
( cd tests/LogicParity && python3 parity_sql.py ) || fail=1

line "5/5  Cross-check: JS ve SQL ciktilari birebir ayni mi?"
( cd tests/LogicParity && python3 - <<'PY'
import json,sys
js=json.load(open('js-results.json')); sql=json.load(open('sql-results.json'))
print("  JS  :",js); print("  SQL :",sql)
sys.exit(0 if js==sql else 1)
PY
) || fail=1

echo
if [ $fail -eq 0 ]; then
  printf '\033[1;32m════ TUM TESTLER GECTI ════\033[0m\n'
else
  printf '\033[1;31m════ BAZI TESTLER BASARISIZ ════\033[0m\n'
fi
exit $fail
