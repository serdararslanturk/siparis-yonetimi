/* ============================================================================
   ACR Filo — Çeviri Katmanı (bridge.js)
   Frontend'in state.orders (blob) yapısı  <->  API'nin normalize DTO'su.

   Frontend beklediği sipariş:
     { id, musteriUnvani, olusturmaTarihi,
       vehicleLines:[{ id, marka, model, adet, birimBedel, tedarikciUnvani,
                       odeme:{ planlar:[{id,tarih,tutar}], odemeler:[{id,tarih,tutar}] } }],
       vehicles:[{ id, lineId, marka, model, plakaNo, tedarikTarihi, tedarikYeri,
                   planlananTeslim, teslimYeri, gerceklesenTeslim,
                   teslimAlindi:{alindi,tarih},
                   ikame:{verildi,tarih,plaka,iadeTarihi},
                   ssh:{plaka:{yapildi,tarih},hgs:{...},gps:{...},utts:{...}} }] }

   API OrderDetailDto (camelCase JSON):
     { id, siparisNo, customerId, musteriUnvani, olusturmaTarihi,
       vehicleLines:[{ id, brandId, marka, model, adet, birimBedel, supplierId,
                       tedarikciUnvani, kalemToplam, planToplam, odenenToplam,
                       kalanTutar, planEslesiyor, planlar:[{id,tarih,tutar}],
                       odemeler:[{id,tarih,tutar}], rowVersion }],
       vehicles:[{ id, lineId, marka, model, plakaNo, tedarikTarihi, tedarikYeri,
                   planlananTeslim, teslimYeri, teslimAlindi(bool),
                   teslimAlinmaTarihi, gerceklesenTeslim,
                   ikame:{verildi,tarih,plaka,iadeTarihi},
                   ssh:{plaka:{yapildi,tarih},...}, durum, rowVersion }],
       rowVersion }

   Ana farklar:
   - teslimAlindi: API bool + ayrı tarih; frontend {alindi,tarih} nesnesi
   - odeme: API'de line.planlar/line.odemeler düz; frontend line.odeme.{planlar,odemeler}
   - rowVersion: API'de var (çakışma kontrolü), frontend'de yok -> saklıyoruz
   - id: her ikisi de sayısal (API int). Frontend uid()'yi sadece YENİ, henüz
     kaydedilmemiş kalemler için kullanır; kaydedilince API id ile değişir.
   ============================================================================ */

const Bridge = (() => {

  /* ---- API OrderDetailDto -> frontend order ---- */
  function apiOrderToFrontend(dto) {
    return {
      id: String(dto.id),
      _apiId: dto.id,  // sayisal API id (gerekince)
      siparisNo: dto.siparisNo,        // frontend'de gösterim için (prototipte yoktu, artık var)
      customerId: dto.customerId,
      musteriUnvani: dto.musteriUnvani,
      olusturmaTarihi: dto.olusturmaTarihi,
      rowVersion: dto.rowVersion,      // gizli, çakışma kontrolü için
      vehicleLines: (dto.vehicleLines || []).map(apiLineToFrontend),
      vehicles: (dto.vehicles || []).map(apiVehicleToFrontend),
    };
  }

  function apiLineToFrontend(l) {
    return {
      id: String(l.id),
      _apiId: l.id,
      brandId: l.brandId,
      marka: l.marka,
      model: l.model,
      adet: l.adet,
      birimBedel: l.birimBedel,
      supplierId: l.supplierId,
      tedarikciUnvani: l.tedarikciUnvani,
      rowVersion: l.rowVersion,
      // frontend line.odeme.{planlar,odemeler} yapısına sar
      odeme: {
        planlar: (l.planlar || []).map(p => ({ id: String(p.id), _apiId: p.id, tarih: p.tarih, tutar: p.tutar })),
        odemeler: (l.odemeler || []).map(p => ({ id: String(p.id), _apiId: p.id, tarih: p.tarih, tutar: p.tutar })),
      },
    };
  }

  function apiVehicleToFrontend(v) {
    return {
      id: String(v.id),
      _apiId: v.id,
      lineId: String(v.lineId),
      _apiLineId: v.lineId,
      marka: v.marka,
      model: v.model,
      plakaNo: v.plakaNo || '',
      tedarikTarihi: v.tedarikTarihi || null,
      tedarikYeri: v.tedarikYeri || '',
      cekiciKullanildi: !!v.cekiciKullanildi,
      planlananTeslim: v.planlananTeslim || null,
      teslimYeri: v.teslimYeri || '',
      gerceklesenTeslim: v.gerceklesenTeslim || null,
      rowVersion: v.rowVersion,
      // API bool+tarih -> frontend {alindi,tarih}
      teslimAlindi: {
        alindi: !!v.teslimAlindi,
        tarih: v.teslimAlinmaTarihi || null,
      },
      ikame: {
        verildi: !!(v.ikame && v.ikame.verildi),
        tarih: (v.ikame && v.ikame.tarih) || null,
        plaka: (v.ikame && v.ikame.plaka) || '',
        iadeTarihi: (v.ikame && v.ikame.iadeTarihi) || null,
      },
      ssh: {
        plaka: sshStep(v.ssh && v.ssh.plaka),
        hgs: sshStep(v.ssh && v.ssh.hgs),
        gps: sshStep(v.ssh && v.ssh.gps),
        utts: sshStep(v.ssh && v.ssh.utts),
      },
    };
  }
  function sshStep(s) {
    return { yapildi: !!(s && s.yapildi), tarih: (s && s.tarih) || null };
  }

  /* ---- frontend line (yeni sipariş formundan) -> API CreateLineRequest ---- */
  function frontendLineToCreateRequest(l) {
    return {
      marka: l.marka,
      brandId: l.brandId || null,
      model: l.model,
      adet: l.adet,
      birimBedel: l.birimBedel,
      tedarikciUnvani: l.tedarikciUnvani,
      supplierId: l.supplierId || null,
      tedarikTarihi: l.tedarikTarihi || null,
      tedarikYeri: l.tedarikYeri || null,
      cekiciKullanildi: !!l.cekiciKullanildi,
      planlananTeslim: l.planlananTeslim,          // zorunlu
      teslimYeri: l.teslimYeri,                    // zorunlu
      planlar: (l.planlar || l.odeme && l.odeme.planlar || [])
        .filter(p => p.tarih)
        .map(p => ({ tarih: p.tarih, tutar: p.tutar || 0 })),
    };
  }

  /* ---- frontend yeni sipariş -> API CreateOrderRequest ---- */
  function frontendOrderToCreateRequest(musteriUnvani, lines, olusturmaTarihi) {
    return {
      musteriUnvani: musteriUnvani,
      customerId: null,
      olusturmaTarihi: olusturmaTarihi || null,
      lines: lines.map(frontendLineToCreateRequest),
    };
  }

  /* ---- frontend vehicle -> API UpdateVehicleRequest ---- */
  function frontendVehicleToUpdateRequest(v) {
    return {
      plakaNo: v.plakaNo || null,
      tedarikTarihi: v.tedarikTarihi || null,
      tedarikYeri: v.tedarikYeri || null,
      cekiciKullanildi: !!v.cekiciKullanildi,
      planlananTeslim: v.planlananTeslim || null,
      teslimYeri: v.teslimYeri || null,
      // frontend {alindi,tarih} -> API bool + ayrı tarih
      teslimAlindi: !!(v.teslimAlindi && v.teslimAlindi.alindi),
      teslimAlinmaTarihi: (v.teslimAlindi && v.teslimAlindi.tarih) || null,
      gerceklesenTeslim: v.gerceklesenTeslim || null,
      ikame: {
        verildi: !!(v.ikame && v.ikame.verildi),
        tarih: (v.ikame && v.ikame.tarih) || null,
        plaka: (v.ikame && v.ikame.plaka) || null,
        iadeTarihi: (v.ikame && v.ikame.iadeTarihi) || null,
      },
      ssh: {
        plaka: outStep(v.ssh && v.ssh.plaka),
        hgs: outStep(v.ssh && v.ssh.hgs),
        gps: outStep(v.ssh && v.ssh.gps),
        utts: outStep(v.ssh && v.ssh.utts),
      },
      rowVersion: v.rowVersion || null,
    };
  }
  function outStep(s) {
    return { yapildi: !!(s && s.yapildi), tarih: (s && s.tarih) || null };
  }

  return {
    apiOrderToFrontend,
    apiLineToFrontend,
    apiVehicleToFrontend,
    frontendOrderToCreateRequest,
    frontendLineToCreateRequest,
    frontendVehicleToUpdateRequest,
  };
})();
