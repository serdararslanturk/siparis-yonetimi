using Acr.Filo.Domain.Entities.Orders;
using Acr.Filo.Domain.Enums;

// ============================================================================
// LOGIC PARITY — C# iş mantığı ile frontend JS'in AYNI sonucu verdiğini kanıtlar.
// Her vaka: (SSH tarihleri, planlanan teslim, teslim alındı, gerçekleşen teslim)
// -> beklenen durum. Beklenen değerler frontend vehicleStatus()'tan türetildi ve
// ayrıca parity-frontend.js ile bağımsız olarak Node'da doğrulanır.
// ============================================================================

static FleetOrderVehicle V(
    string? plan, bool teslimAlindi, string? gerceklesen,
    (string type, string? tarih)[] ssh)
{
    var v = new FleetOrderVehicle
    {
        PlanlananTeslim = plan is null ? null : DateOnly.Parse(plan),
        TeslimAlindi = teslimAlindi,
        GerceklesenTeslim = gerceklesen is null ? null : DateOnly.Parse(gerceklesen),
    };
    foreach (var (type, tarih) in ssh)
        v.SshTasks.Add(new VehicleSshTask
        {
            TaskType = SshTaskTypes.FromDb(type),
            Yapildi = tarih is not null,
            Tarih = tarih is null ? null : DateOnly.Parse(tarih),
        });
    return v;
}

// Bugün sabit: 2026-07-15 (testler deterministik olmalı)
var today = new DateOnly(2026, 7, 15);
string[] all = { "plaka", "hgs", "gps", "utts" };
(string,string?)[] none = { ("plaka",null),("hgs",null),("gps",null),("utts",null) };
(string,string?)[] full(string d) => new[]{("plaka",(string?)d),("hgs",d),("gps",d),("utts",d)};

var cases = new (string name, FleetOrderVehicle v, string expected)[]
{
    ("gerceklesen dolu -> done (SSH eksik olsa bile)",
        V("2026-01-01", false, "2026-06-01", none), "done"),

    ("hepsi tamam + teslim alindi -> ready",
        V("2026-08-01", true, null, full("2026-07-01")), "ready"),

    ("hepsi tamam + teslim ALINMADI -> neutral",
        V("2026-08-01", false, null, full("2026-07-01")), "neutral"),

    ("SSH eksik + plan YOK -> neutral",
        V(null, false, null, none), "neutral"),

    ("SSH eksik + plan gecmiste -> overdue",
        V("2026-07-01", false, null, none), "overdue"),

    ("SSH eksik + plan 2 gun sonra -> soon",
        V("2026-07-17", false, null, none), "soon"),

    ("SSH eksik + plan 10 gun sonra -> neutral",
        V("2026-07-25", false, null, none), "neutral"),

    // KRITIK VAKA: 3 adim yapildi, 1 eksik, teslim tarihi GECMIS.
    // Eski (yanlis) mantik: yapilanAdet=3<4 -> plan gecmis -> overdue. Ayni cikardi.
    // ama asagidaki vaka ikisini AYIRIR:
    ("3 adim BUGUN yapildi, 1 eksik, plan 10 gun sonra -> neutral",
        V("2026-07-25", false, null, new[]{
            ("plaka",(string?)"2026-07-15"),("hgs","2026-07-15"),
            ("gps","2026-07-15"),("utts",null)}), "neutral"),

    ("TUM adimlar yapildi ama plan gecmis + teslim alinmadi -> neutral (done->neutral)",
        V("2026-07-01", false, null, full("2026-07-10")), "neutral"),
};

int pass = 0, fail = 0;
Console.WriteLine("=== C# Durum() vakalari (bugun=2026-07-15) ===");
foreach (var (name, v, expected) in cases)
{
    var got = v.Durum(today);
    var ok = got == expected;
    Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {name}");
    if (!ok) { Console.WriteLine($"       beklenen={expected} geldi={got}"); fail++; } else pass++;
}

// --- İkame kuralı: verilmediyse tarih/plaka olamaz (CK_FOV_IkameTarihi) ---
Console.WriteLine("\n=== EksikSshAdimlari() sirasi ===");
var vx = V(null, false, null, new[]{
    ("plaka",(string?)"2026-07-01"),("hgs",null),("gps",null),("utts",null)});
var eksik = string.Join(",", vx.EksikSshAdimlari());
var eksikOk = eksik == "hgs,gps,utts";
Console.WriteLine($"  [{(eksikOk ? "OK" : "FAIL")}] eksik adimlar sirali: '{eksik}'");
if (eksikOk) pass++; else fail++;

Console.WriteLine($"\nSONUC: {pass} gecti, {fail} basarisiz");
Environment.Exit(fail == 0 ? 0 : 1);
