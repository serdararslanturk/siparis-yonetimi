/* ============================================================================
   ACR Filo — API İstemci Katmanı (api.js)
   Saf HTTP çağrıları + JWT token yönetimi. İş mantığı YOK, çeviri YOK.
   Çeviri (frontend blob <-> API DTO) bridge.js'te. Bu dosya sadece "API'ye
   istek at, cevabı döndür" işini yapar.

   Backend endpoint'leri (controller'lardan birebir):
     POST /api/auth/login            -> {accessToken, refreshToken, user}
     POST /api/auth/refresh          -> aynı
     POST /api/auth/logout
     GET  /api/auth/me               -> user
     GET  /api/orders?tab=&search=&page=  -> {items, total, page, pageSize}
     GET  /api/orders/{id}           -> OrderDetailDto
     POST /api/orders                -> OrderDetailDto
     PUT  /api/orders/{id}
     DELETE /api/orders/{id}
     POST /api/orders/{id}/lines
     PUT  /api/orders/{oid}/lines/{lid}
     DELETE /api/orders/{oid}/lines/{lid}
     PUT  /api/orders/{oid}/vehicles/{vid}
     POST /api/orders/{oid}/lines/{lid}/payments
     DELETE /api/orders/{oid}/lines/{lid}/payments/{pid}
     GET  /api/definitions/{tur}          (tur: customers|suppliers|brands)
     GET  /api/definitions/{tur}/active
     POST /api/definitions/{tur}
     PUT  /api/definitions/{tur}/{id}
     DELETE /api/definitions/{tur}/{id}
     GET  /api/reports/delivery-calendar?from=&to=&customerId=&supplierId=&hideDelivered=
     GET  /api/reports/supply-calendar
     GET  /api/reports/ssh-calendar
     GET  /api/reports/payment-calendar
     GET  /api/dashboard
   ============================================================================ */

const Api = (() => {
  const BASE = ''; // aynı origin (IIS'te frontend + API birlikte)
  const TOKEN_KEY = 'acr_access_token';
  const REFRESH_KEY = 'acr_refresh_token';
  const USER_KEY = 'acr_user';

  // --- token saklama (sessionStorage: sekme kapanınca düşsün) ---
  function getToken() { return sessionStorage.getItem(TOKEN_KEY); }
  function getRefresh() { return sessionStorage.getItem(REFRESH_KEY); }
  function getUser() {
    const raw = sessionStorage.getItem(USER_KEY);
    try { return raw ? JSON.parse(raw) : null; } catch { return null; }
  }
  function setSession(loginResponse) {
    sessionStorage.setItem(TOKEN_KEY, loginResponse.accessToken);
    sessionStorage.setItem(REFRESH_KEY, loginResponse.refreshToken);
    sessionStorage.setItem(USER_KEY, JSON.stringify(loginResponse.user));
  }
  function clearSession() {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_KEY);
    sessionStorage.removeItem(USER_KEY);
  }
  function isAuthenticated() { return !!getToken(); }
  function hasPermission(perm) {
    const u = getUser();
    return !!(u && u.permissions && u.permissions.includes(perm));
  }

  // --- çekirdek fetch (token ekler, 401'de refresh dener) ---
  async function request(method, url, body, _retry) {
    const headers = { 'Content-Type': 'application/json' };
    const token = getToken();
    if (token) headers['Authorization'] = 'Bearer ' + token;

    let resp;
    try {
      resp = await fetch(BASE + url, {
        method,
        headers,
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });
    } catch (netErr) {
      // ağ hatası (sunucu kapalı, bağlantı yok)
      throw new ApiError(0, 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.', netErr);
    }

    // 401 -> token süresi dolmuş olabilir, bir kez refresh dene
    if (resp.status === 401 && !_retry && getRefresh()) {
      const refreshed = await tryRefresh();
      if (refreshed) return request(method, url, body, true);
    }

    if (resp.status === 204) return null; // No Content
    if (resp.status === 401) { clearSession(); throw new ApiError(401, 'Oturum sona erdi. Tekrar giriş yapın.'); }

    let data = null;
    const text = await resp.text();
    if (text) { try { data = JSON.parse(text); } catch { data = text; } }

    if (!resp.ok) {
      // ProblemDetails: {title, detail, status}
      const msg = (data && (data.detail || data.title)) || ('İşlem başarısız (HTTP ' + resp.status + ')');
      throw new ApiError(resp.status, msg, data);
    }
    return data;
  }

  async function tryRefresh() {
    try {
      const resp = await fetch(BASE + '/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: getRefresh() }),
      });
      if (!resp.ok) { clearSession(); return false; }
      const data = await resp.json();
      setSession(data);
      return true;
    } catch { clearSession(); return false; }
  }

  class ApiError extends Error {
    constructor(status, message, detail) {
      super(message);
      this.name = 'ApiError';
      this.status = status;
      this.detail = detail;
    }
  }

  // --- AUTH ---
  async function login(email, password) {
    const data = await request('POST', '/api/auth/login', { email, password });
    setSession(data);
    return data.user;
  }
  async function logout() {
    try { await request('POST', '/api/auth/logout', { refreshToken: getRefresh() }); }
    catch { /* logout hatası önemsiz */ }
    clearSession();
  }
  async function me() { return request('GET', '/api/auth/me'); }
  async function changePassword(currentPassword, newPassword) {
    return request('POST', '/api/auth/change-password', { currentPassword, newPassword });
  }

  // --- ORDERS ---
  function listOrders({ tab = 'acik', search = '', page = 1, pageSize = 100, customerId } = {}) {
    const q = new URLSearchParams({ tab, page, pageSize });
    if (search) q.set('search', search);
    if (customerId) q.set('customerId', customerId);
    return request('GET', '/api/orders?' + q.toString());
  }
  function getOrder(id) { return request('GET', '/api/orders/' + id); }
  function createOrder(payload) { return request('POST', '/api/orders', payload); }
  function updateOrder(id, payload) { return request('PUT', '/api/orders/' + id, payload); }
  function deleteOrder(id) { return request('DELETE', '/api/orders/' + id); }
  function addLines(orderId, payload) { return request('POST', '/api/orders/' + orderId + '/lines', payload); }
  function updateLine(orderId, lineId, payload) { return request('PUT', `/api/orders/${orderId}/lines/${lineId}`, payload); }
  function deleteLine(orderId, lineId) { return request('DELETE', `/api/orders/${orderId}/lines/${lineId}`); }
  // Ödeme planını topluca değiştir (tarih/tutar revizesi). planlar: [{tarih,tutar}]
  function updatePlans(orderId, lineId, planlar) {
    return request('PUT', `/api/orders/${orderId}/lines/${lineId}/plans`, { planlar });
  }
  function updateVehicle(orderId, vehicleId, payload) { return request('PUT', `/api/orders/${orderId}/vehicles/${vehicleId}`, payload); }
  function addPayment(orderId, lineId, payload) { return request('POST', `/api/orders/${orderId}/lines/${lineId}/payments`, payload); }
  function deletePayment(orderId, lineId, paymentId) { return request('DELETE', `/api/orders/${orderId}/lines/${lineId}/payments/${paymentId}`); }

  // --- DEFINITIONS ---
  function listDefinitions(tur) { return request('GET', '/api/definitions/' + tur + '/active'); }
  function createDefinition(tur, ad, extra) { return request('POST', '/api/definitions/' + tur, Object.assign({ ad }, extra || {})); }
  function updateDefinition(tur, id, payload) { return request('PUT', `/api/definitions/${tur}/${id}`, payload); }
  function deleteDefinition(tur, id) { return request('DELETE', `/api/definitions/${tur}/${id}`); }

  // --- REPORTS ---
  function calendar(kind, params = {}) {
    const q = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => { if (v !== null && v !== undefined && v !== '') q.set(k, v); });
    const qs = q.toString();
    return request('GET', `/api/reports/${kind}` + (qs ? '?' + qs : ''));
  }
  function dashboard() { return request('GET', '/api/dashboard'); }

  return {
    // auth/session
    login, logout, me, changePassword,
    getUser, isAuthenticated, hasPermission, clearSession,
    // orders
    listOrders, getOrder, createOrder, updateOrder, deleteOrder,
    addLines, updateLine, deleteLine, updatePlans, updateVehicle, addPayment, deletePayment,
    // definitions
    listDefinitions, createDefinition, updateDefinition, deleteDefinition,
    // reports
    calendar, dashboard,
    // error type
    ApiError,
  };
})();
