// ============================================================================
// Frontend arac-teslim-takip.html'den BIREBIR kopyalanan durum mantigi.
// (satir 413-448). Tek degisiklik: todayISO() sabit tarih dondurur ki test
// deterministik olsun. Baska HICBIR sey degistirilmedi.
// ============================================================================
const TODAY = "2026-07-15";
function todayISO(){ return TODAY; }

// --- satir 440 ---
const STATUS_RANK = {overdue:4, soon:3, neutral:2, ready:1, done:0};
// --- satir 414 ---
function daysUntil(d){ if(!d) return null; const a=new Date(d+"T00:00:00"), b=new Date(todayISO()+"T00:00:00"); return Math.round((a-b)/86400000); }
// --- satir 430 ---
function fieldStatus(due, done){
  if(done) return 'done';
  if(!due) return 'neutral';
  const d = daysUntil(due);
  if(d < 0) return 'overdue';
  if(d <= 3) return 'soon';
  return 'neutral';
}
// --- satir 442 ---
function vehicleStatus(v){
  if(v.gerceklesenTeslim) return 'done';
  const items = ['plaka','hgs','gps','utts'].map(k => fieldStatus(v.planlananTeslim, v.ssh[k].tarih));
  const worst = items.reduce((a,b)=> STATUS_RANK[b] > STATUS_RANK[a] ? b : a, 'done');
  const teslimAlindiOk = !!(v.teslimAlindi && v.teslimAlindi.alindi);
  if(worst === 'done'){
    return teslimAlindiOk ? 'ready' : 'neutral';
  }
  return worst;
}

// Ayni vakalar (C# tarafiyla birebir ayni girdi)
function mkSsh(map){ return {plaka:{tarih:map.plaka||null},hgs:{tarih:map.hgs||null},gps:{tarih:map.gps||null},utts:{tarih:map.utts||null}}; }
const F = d => ({plaka:d,hgs:d,gps:d,utts:d});

const cases = [
  ["gerceklesen dolu -> done", {planlananTeslim:"2026-01-01",teslimAlindi:{alindi:false},gerceklesenTeslim:"2026-06-01",ssh:mkSsh({})}, "done"],
  ["hepsi tamam + teslim alindi -> ready", {planlananTeslim:"2026-08-01",teslimAlindi:{alindi:true},gerceklesenTeslim:null,ssh:mkSsh(F("2026-07-01"))}, "ready"],
  ["hepsi tamam + teslim ALINMADI -> neutral", {planlananTeslim:"2026-08-01",teslimAlindi:{alindi:false},gerceklesenTeslim:null,ssh:mkSsh(F("2026-07-01"))}, "neutral"],
  ["SSH eksik + plan YOK -> neutral", {planlananTeslim:null,teslimAlindi:{alindi:false},gerceklesenTeslim:null,ssh:mkSsh({})}, "neutral"],
  ["SSH eksik + plan gecmiste -> overdue", {planlananTeslim:"2026-07-01",teslimAlindi:{alindi:false},gerceklesenTeslim:null,ssh:mkSsh({})}, "overdue"],
  ["SSH eksik + plan 2 gun sonra -> soon", {planlananTeslim:"2026-07-17",teslimAlindi:{alindi:false},gerceklesenTeslim:null,ssh:mkSsh({})}, "soon"],
  ["SSH eksik + plan 10 gun sonra -> neutral", {planlananTeslim:"2026-07-25",teslimAlindi:{alindi:false},gerceklesenTeslim:null,ssh:mkSsh({})}, "neutral"],
  ["3 adim BUGUN yapildi, 1 eksik, plan 10 gun sonra -> neutral", {planlananTeslim:"2026-07-25",teslimAlindi:{alindi:false},gerceklesenTeslim:null,ssh:mkSsh({plaka:"2026-07-15",hgs:"2026-07-15",gps:"2026-07-15"})}, "neutral"],
  ["TUM adimlar yapildi ama plan gecmis + teslim alinmadi -> neutral", {planlananTeslim:"2026-07-01",teslimAlindi:{alindi:false},gerceklesenTeslim:null,ssh:mkSsh(F("2026-07-10"))}, "neutral"],
];

let pass=0, fail=0;
const results = [];
console.log("=== Frontend vehicleStatus() vakalari (Node, bugun=2026-07-15) ===");
for(const [name, v, expected] of cases){
  const got = vehicleStatus(v);
  const ok = got === expected;
  console.log(`  [${ok?"OK":"FAIL"}] ${name}`);
  if(!ok){ console.log(`       beklenen=${expected} geldi=${got}`); fail++; } else pass++;
  results.push({name, got});
}
console.log(`\nSONUC: ${pass} gecti, ${fail} basarisiz`);
// C# ile karsilastirmak icin makine-okunur cikti
import fs from 'fs';
fs.writeFileSync('js-results.json', JSON.stringify(results.map(r=>r.got)));
process.exit(fail===0?0:1);
