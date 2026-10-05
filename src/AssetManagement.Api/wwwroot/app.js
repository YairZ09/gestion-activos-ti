'use strict';

const state = { page: 1, pageSize: 10, totalPages: 1, filters: {}, current: null };
const $ = (sel) => document.querySelector(sel);

const session = {
  get() { try { return JSON.parse(sessionStorage.getItem('session')); } catch { return null; } },
  set(s) { sessionStorage.setItem('session', JSON.stringify(s)); },
  clear() { sessionStorage.removeItem('session'); },
};
const isAdmin = () => session.get()?.role === 'Administrador';

class ApiError extends Error {
  constructor(status, problem) {
    super(describeProblem(problem));
    this.status = status;
  }
}

function describeProblem(p) {
  if (!p) return 'Error de comunicacion con el servidor.';
  if (p.errors) return Object.values(p.errors).flat().join('\n');
  return p.detail || p.title || 'Ocurrio un error.';
}

async function api(path, { method = 'GET', body } = {}) {
  const headers = { Accept: 'application/json' };
  const s = session.get();
  if (s) headers.Authorization = `Bearer ${s.accessToken}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const res = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const data = res.status === 204 ? null : await res.json().catch(() => null);

  if (res.status === 401 && s) { logout(); throw new ApiError(401, { detail: 'La sesion expiro. Inicia sesion de nuevo.' }); }
  if (!res.ok) throw new ApiError(res.status, data);
  return data;
}

function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (k === 'class') node.className = v;
    else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
    else node.setAttribute(k, v);
  }
  for (const c of children) node.append(c instanceof Node ? c : document.createTextNode(c ?? ''));
  return node;
}
const fmtDateTime = (iso) => new Date(iso).toLocaleString();
const formData = (form) => Object.fromEntries(new FormData(form).entries());
const emptyToNull = (obj) => Object.fromEntries(Object.entries(obj).map(([k, v]) => [k, v === '' ? null : v]));

function openDialog(id, asset) {
  const dlg = document.getElementById(id);
  dlg.querySelectorAll('[data-asset-code]').forEach((n) => (n.textContent = asset?.assetCode ?? ''));
  dlg.querySelectorAll('.error').forEach((n) => (n.textContent = ''));
  dlg.showModal();
  return dlg;
}
document.querySelectorAll('[data-close]').forEach((b) => b.addEventListener('click', () => b.closest('dialog').close()));

function showView() {
  const s = session.get();
  $('#login-view').classList.toggle('hidden', !!s);
  $('#app-view').classList.toggle('hidden', !s);
  if (s) {
    $('#who').textContent = `${s.username} (${s.role})`;
    document.querySelectorAll('.admin-only').forEach((n) => n.classList.toggle('hidden', !isAdmin()));
    loadAssets();
  }
}

function logout() { session.clear(); showView(); }

$('#login-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  $('#login-error').textContent = '';
  try {
    const res = await api('/api/auth/login', { method: 'POST', body: formData(e.target) });
    session.set(res);
    e.target.reset();
    showView();
  } catch (err) {
    $('#login-error').textContent = err.message;
  }
});
$('#logout').addEventListener('click', logout);

async function loadAssets() {
  $('#list-error').textContent = '';
  const params = new URLSearchParams({ page: state.page, pageSize: state.pageSize });
  for (const [k, v] of Object.entries(state.filters)) if (v) params.set(k, v);
  try {
    const result = await api(`/api/assets?${params}`);
    state.totalPages = Math.max(1, result.totalPages);
    renderAssets(result.items);
    $('#page-info').textContent = `Pagina ${result.page} de ${state.totalPages} - ${result.totalCount} activos`;
    $('#prev').disabled = state.page <= 1;
    $('#next').disabled = state.page >= state.totalPages;
  } catch (err) {
    $('#list-error').textContent = err.message;
  }
}

function renderAssets(items) {
  const tbody = $('#assets');
  tbody.replaceChildren();
  if (items.length === 0) {
    tbody.append(el('tr', {}, el('td', { colspan: '9', class: 'muted' }, 'Sin resultados')));
    return;
  }
  for (const a of items) {
    const actions = el('td', {});
    if (a.status === 'Disponible') actions.append(el('button', { class: 'link', onclick: () => startAssign(a) }, 'Asignar'));
    if (a.status === 'Asignado') actions.append(el('button', { class: 'link', onclick: () => startReturn(a) }, 'Devolver'));
    actions.append(el('button', { class: 'link', onclick: () => showHistory(a) }, 'Historial'));

    tbody.append(el('tr', {},
      el('td', {}, a.assetCode),
      el('td', {}, a.serialNumber ?? '-'),
      el('td', {}, a.category),
      el('td', {}, `${a.brand} ${a.model}`),
      el('td', {}, a.ownershipType + (a.supplierName ? ` - ${a.supplierName}` : '')),
      el('td', {}, el('span', { class: `badge st-${a.status}` }, a.status)),
      el('td', {}, a.currentLocation),
      el('td', {}, a.assignedEmployeeName ?? '-'),
      actions));
  }
}

$('#filters').addEventListener('submit', (e) => {
  e.preventDefault();
  state.filters = formData(e.target);
  state.page = 1;
  loadAssets();
});
$('#prev').addEventListener('click', () => { state.page--; loadAssets(); });
$('#next').addEventListener('click', () => { state.page++; loadAssets(); });

$('#new-asset').addEventListener('click', async () => {
  const dlg = openDialog('asset-dialog');
  const select = dlg.querySelector('[name=supplierId]');
  select.replaceChildren(el('option', { value: '' }, '(ninguno)'));
  try {
    const suppliers = await api('/api/suppliers?pageSize=100');
    for (const s of suppliers.items) select.append(el('option', { value: s.id }, `${s.name} (${s.services.join(', ')})`));
  } catch (err) {
    dlg.querySelector('.error').textContent = err.message;
  }
});

$('#asset-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const form = e.target;
  const body = emptyToNull(formData(form));
  body.supplierId = body.supplierId ? Number(body.supplierId) : null;
  try {
    await api('/api/assets', { method: 'POST', body });
    form.reset();
    form.closest('dialog').close();
    loadAssets();
  } catch (err) {
    form.querySelector('.error').textContent = err.message;
  }
});

async function startAssign(asset) {
  state.current = asset;
  const dlg = openDialog('assign-dialog', asset);
  const select = dlg.querySelector('[name=employeeId]');
  select.replaceChildren();
  try {
    const employees = await api('/api/employees?isActive=true&pageSize=100');
    for (const emp of employees.items) select.append(el('option', { value: emp.id }, `${emp.fullName} (${emp.employeeNumber})`));
    if (employees.items.length === 0) dlg.querySelector('.error').textContent = 'No hay colaboradores activos.';
  } catch (err) {
    dlg.querySelector('.error').textContent = err.message;
  }
}

$('#assign-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const form = e.target;
  const { employeeId, notes } = emptyToNull(formData(form));
  try {
    await api(`/api/assets/${state.current.id}/assign`, { method: 'POST', body: { employeeId: Number(employeeId), notes } });
    form.reset();
    form.closest('dialog').close();
  } catch (err) {
    form.querySelector('.error').textContent = err.message;
  }
  loadAssets();
});

function startReturn(asset) {
  state.current = asset;
  openDialog('return-dialog', asset);
}

$('#return-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const form = e.target;
  try {
    await api(`/api/assets/${state.current.id}/return`, { method: 'POST', body: emptyToNull(formData(form)) });
    form.reset();
    form.closest('dialog').close();
  } catch (err) {
    form.querySelector('.error').textContent = err.message;
  }
  loadAssets();
});

function describeMovement(m) {
  switch (m.movementType) {
    case 'Asignacion': return `A ${m.employeeName}`;
    case 'Devolucion': return `De ${m.employeeName}`;
    case 'CambioEstado': return `${m.fromStatus} -> ${m.toStatus}`;
    case 'CambioUbicacion': return `${m.fromLocation} -> ${m.toLocation}`;
    case 'Alta': return `En ${m.toLocation}`;
    default: return '';
  }
}

async function showHistory(asset) {
  const dlg = openDialog('history-dialog', asset);
  const tbody = dlg.querySelector('#history');
  tbody.replaceChildren(el('tr', {}, el('td', { colspan: '5', class: 'muted' }, 'Cargando...')));
  try {
    const movements = await api(`/api/assets/${asset.id}/history`);
    tbody.replaceChildren(...movements.map((m) => el('tr', {},
      el('td', {}, fmtDateTime(m.performedAt)),
      el('td', {}, m.movementType),
      el('td', {}, describeMovement(m)),
      el('td', {}, m.performedBy),
      el('td', {}, m.notes ?? ''))));
  } catch (err) {
    tbody.replaceChildren(el('tr', {}, el('td', { colspan: '5', class: 'error' }, err.message)));
  }
}

showView();
