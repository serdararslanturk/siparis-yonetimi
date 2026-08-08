#!/usr/bin/env python3
"""SQL vw_VehicleStatus CASE dallarinin birebir Python simulasyonu.
DATEDIFF(DAY, bugun, plan) = plan - bugun (gun). Frontend daysUntil ile ayni isaret."""
from datetime import date
import json, sys

TODAY = date(2026, 7, 15)

def datediff_day(plan):  # DATEDIFF(DAY, TODAY, plan)
    return (plan - TODAY).days

def sql_durum(plan, teslim_alindi, gerceklesen, ssh_yapildi):
    """ssh_yapildi: dict type->bool. SQL CASE dallarinin motamot cevirisi."""
    yapilan = sum(1 for v in ssh_yapildi.values() if v)
    eksik_var = yapilan < 4
    if gerceklesen is not None:
        return 'done'
    if eksik_var and plan is None:
        return 'neutral'
    if eksik_var and datediff_day(plan) < 0:
        return 'overdue'
    if eksik_var and datediff_day(plan) <= 3:
        return 'soon'
    if eksik_var:
        return 'neutral'
    if teslim_alindi:
        return 'ready'
    return 'neutral'

def d(s): return None if s is None else date.fromisoformat(s)
def full(x): return {'plaka':x,'hgs':x,'gps':x,'utts':x}

# C#/JS ile BIREBIR ayni vakalar (JS 9 vaka)
cases = [
    ("gerceklesen dolu -> done", "2026-01-01", False, "2026-06-01", {}, "done"),
    ("hepsi tamam + teslim alindi -> ready", "2026-08-01", True, None, full(True), "ready"),
    ("hepsi tamam + teslim ALINMADI -> neutral", "2026-08-01", False, None, full(True), "neutral"),
    ("SSH eksik + plan YOK -> neutral", None, False, None, {}, "neutral"),
    ("SSH eksik + plan gecmiste -> overdue", "2026-07-01", False, None, {}, "overdue"),
    ("SSH eksik + plan 2 gun sonra -> soon", "2026-07-17", False, None, {}, "soon"),
    ("SSH eksik + plan 10 gun sonra -> neutral", "2026-07-25", False, None, {}, "neutral"),
    ("3 adim yapildi 1 eksik plan 10 gun -> neutral", "2026-07-25", False, None,
        {'plaka':True,'hgs':True,'gps':True}, "neutral"),
    ("TUM tamam plan gecmis teslim yok -> neutral", "2026-07-01", False, None, full(True), "neutral"),
]

results, p, f = [], 0, 0
print("=== SQL vw_VehicleStatus simulasyonu (bugun=2026-07-15) ===")
for name, plan, ta, ger, ssh_map, exp in cases:
    ssh = {'plaka':False,'hgs':False,'gps':False,'utts':False}
    for k,v in ssh_map.items(): ssh[k] = v
    got = sql_durum(d(plan), ta, d(ger), ssh)
    ok = got == exp
    print(f"  [{'OK' if ok else 'FAIL'}] {name}")
    if not ok: print(f"       beklenen={exp} geldi={got}"); f += 1
    else: p += 1
    results.append(got)

json.dump(results, open('sql-results.json','w'))
print(f"\nSONUC: {p} gecti, {f} basarisiz")
sys.exit(0 if f == 0 else 1)
