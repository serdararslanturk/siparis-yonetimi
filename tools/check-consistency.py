#!/usr/bin/env python3
"""
ACR Filo — Tutarlılık denetleyicisi

Neyi doğrular:
  1. SQL tabloları  <-> EF ToTable() eşlemesi
  2. SQL kolonları  <-> Entity property'leri (iki yönlü)
  3. SQL kolon tipi <-> EF HasColumnType() beyanı
  4. C# Permissions sabitleri <-> 03-seed.sql Permissions MERGE bloğu
  5. C# SshTaskTypes <-> SQL CHECK constraint
  6. C# VehicleStatuses <-> vw_VehicleStatus CASE dalları
  7. EF HasDatabaseName() <-> 02-indexes.sql index adları

Neden var: bu ortamda .NET SDK ve NuGet yok, kod DERLENEMİYOR.
Derleme zaten isim/tip uyuşmazlığını yakalamaz — o hata çalışma anında
"Invalid column name" olarak patlar. Bu script tam olarak onu yakalar.

Kullanım: python3 tools/check-consistency.py
Çıkış kodu: 0 = temiz, 1 = uyuşmazlık var
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
errors, warnings, infos = [], [], []


def read(p):
    return (ROOT / p).read_text(encoding="utf-8")


# ---------------------------------------------------------------- SQL parse
def parse_sql_tables(sql):
    """CREATE TABLE dbo.X ( ... ) bloklarını kolon adı->tip olarak çıkarır."""
    tables = {}
    for m in re.finditer(
        r"CREATE\s+TABLE\s+dbo\.(\w+)\s*\((.*?)\n\);", sql, re.S | re.I
    ):
        name, body = m.group(1), m.group(2)
        cols = {}
        for line in body.split("\n"):
            line = line.strip()
            if not line or line.startswith("--") or line.startswith("/*"):
                continue
            # kısıt satırlarını atla
            if re.match(r"^(CONSTRAINT|PRIMARY|FOREIGN|UNIQUE|CHECK)\b", line, re.I):
                continue
            # cok satirli CONSTRAINT bloklarinin devam satirlari kolon DEGILDIR
            if re.match(r"^(REFERENCES|ON\s+DELETE|ON\s+UPDATE|OR\s+\(|AND\b)", line, re.I):
                continue
            cm = re.match(r"^\[?(\w+)\]?\s+([A-Z0-9_]+(?:\([^)]*\))?)", line, re.I)
            if cm:
                col, typ = cm.group(1), cm.group(2).upper().replace(" ", "")
                cols[col] = typ
        tables[name] = cols
    return tables


def parse_sql_indexes(sql):
    """CREATE INDEX ifadeleri + tablo ici CONSTRAINT ... UNIQUE/PRIMARY KEY adlari.
    Ikincisi de arka planda bir index olusturur; EF HasDatabaseName() bunlari kullanabilir."""
    idx = set(re.findall(r"CREATE\s+(?:UNIQUE\s+)?NONCLUSTERED\s+INDEX\s+(\w+)", sql, re.I))
    idx |= set(re.findall(r"CONSTRAINT\s+(\w+)\s+UNIQUE\b", sql, re.I))
    idx |= set(re.findall(r"CONSTRAINT\s+(\w+)\s+PRIMARY\s+KEY\b", sql, re.I))
    return idx


# ---------------------------------------------------------------- C# parse
def parse_entities():
    """Entity sınıfı -> {property: c# tipi}"""
    ents = {}
    for f in (ROOT / "src/Acr.Filo.Domain/Entities").rglob("*.cs"):
        src = f.read_text(encoding="utf-8")
        src = re.sub(r"//.*?$", "", src, flags=re.M)
        src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
        for cm in re.finditer(
            r"public\s+class\s+(\w+)\s*(?::\s*[^\{]+)?\{(.*?)\n\}", src, re.S
        ):
            cname, body = cm.group(1), cm.group(2)
            props = {}
            for pm in re.finditer(
                r"public\s+([\w\?\[\]<>\.]+)\s+(\w+)\s*\{\s*get;\s*set;\s*\}", body
            ):
                props[pm.group(2)] = pm.group(1)
            ents[cname] = props
    # taban sınıflardan gelen alanlar
    base_audit = {
        "CreatedAt": "DateTime", "CreatedBy": "int?",
        "UpdatedAt": "DateTime?", "UpdatedBy": "int?",
    }
    base_conc = dict(base_audit, RowVersion="byte[]")
    for f in (ROOT / "src/Acr.Filo.Domain/Entities").rglob("*.cs"):
        src = f.read_text(encoding="utf-8")
        for cm in re.finditer(r"public\s+class\s+(\w+)\s*:\s*([^\{]+)\{", src):
            cname, bases = cm.group(1), cm.group(2)
            if cname not in ents:
                continue
            if "ConcurrentAuditableEntity" in bases:
                for k, v in base_conc.items():
                    ents[cname].setdefault(k, v)
            elif "AuditableEntity" in bases:
                for k, v in base_audit.items():
                    ents[cname].setdefault(k, v)
    return ents


def parse_configs():
    """EF config -> {entity: {'table':..,'cols':{prop:sqltype},'renames':{prop:col},
                             'ignored':set(), 'indexes':set()}}"""
    cfgs = {}
    for f in (ROOT / "src/Acr.Filo.Infrastructure/Persistence/Configurations").glob("*.cs"):
        src = f.read_text(encoding="utf-8")
        for cm in re.finditer(
            r"class\s+\w+\s*:\s*IEntityTypeConfiguration<(\w+)>\s*\{(.*?)\n\}", src, re.S
        ):
            ent, body = cm.group(1), cm.group(2)
            tm = re.search(r'\.ToTable\("(\w+)"\)', body)
            cols, renames, ignored = {}, {}, set()
            # e.Property(x => x.Foo)....HasColumnType("bar")
            for pm in re.finditer(
                r"\.Property\(\s*x\s*=>\s*x\.(\w+)\s*\)((?:\s*\.\w+\([^;]*?\))*)\s*;",
                body, re.S,
            ):
                prop, chain = pm.group(1), pm.group(2)
                ctm = re.search(r'\.HasColumnType\("([^"]+)"\)', chain)
                cnm = re.search(r'\.HasColumnName\("(\w+)"\)', chain)
                if ctm:
                    cols[prop] = ctm.group(1).upper().replace(" ", "")
                if cnm:
                    renames[prop] = cnm.group(1)
                if ".IsRowVersion()" in chain:
                    cols[prop] = "ROWVERSION"
            for im in re.finditer(r"\.Ignore\(\s*x\s*=>\s*x\.(\w+)\s*\)", body):
                ignored.add(im.group(1))
            idxs = set(re.findall(r'\.HasDatabaseName\("(\w+)"\)', body))
            cfgs[ent] = {
                "table": tm.group(1) if tm else None,
                "cols": cols, "renames": renames,
                "ignored": ignored, "indexes": idxs,
            }
    return cfgs


# ---------------------------------------------------------------- normalize
def norm_type(t):
    t = t.upper().replace(" ", "")
    t = t.replace("DATETIME2(3)", "DATETIME2(3)")
    return t


NAV_HINT = re.compile(r"^(ICollection|List|IReadOnlyList)<")


def is_nav(cs_type, ents):
    if NAV_HINT.match(cs_type):
        return True
    base = cs_type.rstrip("?")
    return base in ents  # başka entity'ye referans = navigation


# ---------------------------------------------------------------- checks
def main():
    schema = read("db/01-schema.sql")
    idx_sql = read("db/02-indexes.sql")
    seed = read("db/03-seed.sql")

    sql_tables = parse_sql_tables(schema)
    sql_indexes = parse_sql_indexes(schema) | parse_sql_indexes(idx_sql)
    ents = parse_entities()
    cfgs = parse_configs()

    infos.append(f"SQL tablosu: {len(sql_tables)} | Entity: {len(ents)} | Config: {len(cfgs)}")

    # 1 + 2 + 3 --------------------------------------------------
    for ent, cfg in sorted(cfgs.items()):
        table = cfg["table"]
        if not table:
            errors.append(f"[{ent}] ToTable() yok")
            continue
        if table not in sql_tables:
            errors.append(f"[{ent}] ToTable(\"{table}\") -> SQL'de boyle bir tablo YOK")
            continue
        if ent not in ents:
            errors.append(f"[{ent}] config var ama entity sinifi bulunamadi")
            continue

        sqlcols = sql_tables[table]
        props = ents[ent]
        renames = cfg["renames"]

        # entity -> SQL
        for prop, cs_type in sorted(props.items()):
            if prop in cfg["ignored"] or is_nav(cs_type, ents):
                continue
            col = renames.get(prop, prop)
            if col not in sqlcols:
                errors.append(
                    f"[{ent}.{prop}] -> kolon '{col}' {table} tablosunda YOK "
                    f"(SQL kolonlari: {', '.join(sorted(sqlcols))})"
                )

        # SQL -> entity
        mapped = {renames.get(p, p) for p in props if p not in cfg["ignored"]}
        for col in sorted(sqlcols):
            if col not in mapped:
                warnings.append(f"[{table}.{col}] SQL'de var, entity'de karsiligi YOK")

        # tip karsilastirmasi
        for prop, declared in sorted(cfg["cols"].items()):
            col = renames.get(prop, prop)
            if col not in sqlcols:
                continue
            actual = norm_type(sqlcols[col])
            decl = norm_type(declared)
            if decl != actual:
                errors.append(
                    f"[{ent}.{prop}] tip uyusmuyor: EF='{declared}' vs SQL='{sqlcols[col]}'"
                )

    # entity var ama config yok
    concrete = {e for e in ents if e not in ("AuditableEntity", "ConcurrentAuditableEntity")}
    for e in sorted(concrete - set(cfgs)):
        warnings.append(f"[{e}] entity var ama EF konfigurasyonu YOK")

    # 4 — Permissions ---------------------------------------------
    cs_perm = set(re.findall(
        r'public const string \w+\s*=\s*"([\w\.]+)"',
        read("src/Acr.Filo.Domain/Entities/Auth/Permission.cs")))
    perm_block = re.search(r"MERGE dbo\.Permissions.*?USING \(VALUES(.*?)\) AS s", seed, re.S)
    sql_perm = set(re.findall(r"\('([\w\.]+)'", perm_block.group(1))) if perm_block else set()
    if cs_perm != sql_perm:
        for p in sorted(cs_perm - sql_perm):
            errors.append(f"[Permission] C#'ta var, 03-seed.sql'de YOK: {p}")
        for p in sorted(sql_perm - cs_perm):
            errors.append(f"[Permission] seed'de var, C#'ta YOK: {p}")
    else:
        infos.append(f"Permissions esitligi OK ({len(cs_perm)} yetki)")

    # seed'deki rol->yetki eslemesi gecerli yetkilere mi bakiyor?
    map_block = re.search(r"INSERT INTO @Map .*?VALUES(.*?);", seed, re.S)
    if map_block:
        used = set(re.findall(r"'(?:admin|operasyon|muhasebe)'\s*,\s*'([\w\.]+)'", seed))
        for p in sorted(used - sql_perm):
            errors.append(f"[RolePermission] tanimsiz yetkiye referans: {p}")

    # 5 — SSH ------------------------------------------------------
    cs_ssh = set(re.findall(
        r'public const string \w+\s*=\s*"(\w+)"',
        read("src/Acr.Filo.Domain/Enums/SshTaskType.cs")))
    ck = re.search(r"CK_VST_TaskType CHECK \(TaskType IN \(([^)]+)\)\)", schema)
    sql_ssh = set(re.findall(r"'(\w+)'", ck.group(1))) if ck else set()
    if cs_ssh != sql_ssh:
        errors.append(f"[SSH] C#={sorted(cs_ssh)} vs SQL CHECK={sorted(sql_ssh)}")
    else:
        infos.append(f"SSH adimlari OK ({sorted(sql_ssh)})")

    # 6 — Durum ----------------------------------------------------
    cs_st = set(re.findall(
        r'public const string \w+\s*=\s*"(\w+)"',
        read("src/Acr.Filo.Domain/Enums/VehicleStatus.cs")))
    view = re.search(r"CREATE VIEW dbo\.vw_VehicleStatus(.*?)\nGO", schema, re.S)
    sql_st = set(re.findall(r"THEN '(\w+)'", view.group(1))) if view else set()
    sql_st |= set(re.findall(r"ELSE '(\w+)'\s*\n\s*END\s+AS Durum", view.group(1))) if view else set()
    if cs_st != sql_st:
        errors.append(f"[Durum] C#={sorted(cs_st)} vs vw_VehicleStatus={sorted(sql_st)}")
    else:
        infos.append(f"Durum degerleri OK ({sorted(sql_st)})")

    # 7 — Index adlari ---------------------------------------------
    ef_idx = set()
    for c in cfgs.values():
        ef_idx |= c["indexes"]
    for n in sorted(ef_idx - sql_indexes):
        errors.append(f"[Index] EF '{n}' adini kullaniyor ama SQL scriptlerinde YOK")

    # ---------------------------------------------------------------
    print("=" * 68)
    for i in infos:
        print(f"  bilgi   : {i}")
    for w in warnings:
        print(f"  UYARI   : {w}")
    for e in errors:
        print(f"  HATA    : {e}")
    print("=" * 68)
    print(f"Sonuc: {len(errors)} hata, {len(warnings)} uyari")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
