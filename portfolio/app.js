async function loadPageFragments(){
  const main=document.getElementById('main');
  const count=Number(main?.dataset.fragments||0);
  if(!main||!count)return;
  const html=await Promise.all(Array.from({length:count},(_,i)=>fetch(`fragments/part${i+1}.html`).then(r=>{if(!r.ok)throw new Error(`Failed to load part ${i+1}`);return r.text()})));
  main.innerHTML=html.join('\n');
}

loadPageFragments().then(()=>{
const root = document.documentElement;
const $ = s => document.querySelector(s);
const $$ = s => [...document.querySelectorAll(s)];

const state = {
  appIndex: 0,
  currency: 'dzd',
  service: 'web',
  calc: '0',
  motion: true,
  cursor: true,
};

const settingsSheet = $('#settingsSheet');
$('#settingsBtn').addEventListener('click', () => settingsSheet.classList.add('open'));
$('#closeSettingsBtn').addEventListener('click', () => settingsSheet.classList.remove('open'));
$('#sheetBackdrop').addEventListener('click', () => settingsSheet.classList.remove('open'));

function setStored(k, v) { localStorage.setItem(k, v); }
function getStored(k, fallback) { return localStorage.getItem(k) || fallback; }

root.dataset.profile = getStored('portfolio-profile', 'apple');
root.dataset.appearance = getStored('portfolio-appearance', 'light');
state.motion = getStored('portfolio-motion', 'on') === 'on';
state.cursor = getStored('portfolio-cursor', 'on') === 'on';
$('#motionToggle').checked = state.motion;
$('#cursorToggle').checked = state.cursor;

autoButtonState('[data-setting="profile"] .segmented-btn', root.dataset.profile, 'data-value');
autoButtonState('[data-setting="appearance"] .segmented-btn', root.dataset.appearance, 'data-value');

document.querySelectorAll('[data-setting="profile"] .segmented-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    root.dataset.profile = btn.dataset.value;
    setStored('portfolio-profile', btn.dataset.value);
    autoButtonState('[data-setting="profile"] .segmented-btn', btn.dataset.value, 'data-value');
  });
});

document.querySelectorAll('[data-setting="appearance"] .segmented-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    root.dataset.appearance = btn.dataset.value;
    setStored('portfolio-appearance', btn.dataset.value);
    autoButtonState('[data-setting="appearance"] .segmented-btn', btn.dataset.value, 'data-value');
  });
});

$('#motionToggle').addEventListener('change', e => {
  state.motion = e.target.checked;
  setStored('portfolio-motion', state.motion ? 'on' : 'off');
  document.body.classList.toggle('motion-off', !state.motion);
});
$('#cursorToggle').addEventListener('change', e => {
  state.cursor = e.target.checked;
  setStored('portfolio-cursor', state.cursor ? 'on' : 'off');
  $('.cursor-glow').style.display = state.cursor ? 'block' : 'none';
});
document.body.classList.toggle('motion-off', !state.motion);
$('.cursor-glow').style.display = state.cursor ? 'block' : 'none';

const header = document.body;
$('#collapseNavBtn').addEventListener('click', () => header.classList.add('nav-hidden'));
$('#navRestoreBtn').addEventListener('click', () => header.classList.remove('nav-hidden'));

function autoButtonState(selector, value, attr) {
  document.querySelectorAll(selector).forEach(btn => btn.classList.toggle('active', btn.getAttribute(attr) === value));
}

function showApp(index) {
  state.appIndex = index;
  $$('.app-stage').forEach((el, i) => el.classList.toggle('active', i === index));
  $$('.mini-tab').forEach((el, i) => el.classList.toggle('active', i === index));
  $$('[data-app-select]').forEach((el, i) => el.classList.toggle('active', i === index));
  $$('.stack-card').forEach((el, i) => {
    el.classList.toggle('active', i === index);
    el.style.setProperty('--i', Math.abs(i - index));
  });
  $$('[data-copy]').forEach((el, i) => el.classList.toggle('active', i === index));
}
showApp(0);
$$('.mini-tab').forEach((btn, i) => btn.addEventListener('click', () => showApp(i)));
$$('[data-app-select]').forEach((btn, i) => btn.addEventListener('click', () => showApp(i)));
$$('.stack-card').forEach((btn, i) => btn.addEventListener('click', () => showApp(i)));

const stockData = [
  { code: 'AAPL', name: 'Apple Inc.', price: '$223.10', path: 'M10 130 C60 124, 90 90, 140 95 S220 150, 280 96 S330 60, 390 74' },
  { code: 'NVDA', name: 'NVIDIA', price: '$174.70', path: 'M10 150 C60 120, 110 126, 140 112 S220 78, 280 90 S330 130, 390 44' },
  { code: 'MSFT', name: 'Microsoft', price: '$520.80', path: 'M10 112 C50 100, 90 88, 130 96 S220 124, 260 102 S330 52, 390 66' },
  { code: 'AMD', name: 'AMD', price: '$178.20', path: 'M10 144 C60 166, 100 88, 150 100 S220 116, 270 84 S330 90, 390 48' }
];
$$('.ticker').forEach((btn, i) => btn.addEventListener('click', () => {
  $$('.ticker').forEach(t => t.classList.remove('active'));
  btn.classList.add('active');
  $('#stockName').textContent = stockData[i].name;
  $('#stockPrice').textContent = stockData[i].price;
  $('#stockPath').setAttribute('d', stockData[i].path);
}));

const calcDisplay = $('#calcDisplay');
$$('.calc-key').forEach(btn => btn.addEventListener('click', () => {
  const key = btn.dataset.key;
  if (key === 'c') state.calc = '0';
  else if (key === '=') {
    try { state.calc = String(Function(`return (${state.calc})`)()); }
    catch { state.calc = 'Err'; }
  } else {
    state.calc = state.calc === '0' || state.calc === 'Err' ? key : state.calc + key;
  }
  calcDisplay.textContent = state.calc;
}));

const CURRENCIES = {
  dzd: { label: 'DZD', fmt: v => `${Math.round(v).toLocaleString('en-US')} DZD`, fx: 1 },
  usd: { label: 'USD', fmt: v => `$${Math.round(v).toLocaleString('en-US')}`, fx: 1 / 217.5 },
  gbp: { label: 'GBP', fmt: v => `£${(v / 300).toFixed(v / 300 >= 100 ? 0 : 1)}`, fx: 1 / 300 },
};
function animateValue(el, from, to, format) {
  const duration = state.motion ? 620 : 0;
  const start = performance.now();
  const tick = now => {
    const p = duration ? Math.min(1, (now - start) / duration) : 1;
    const eased = 1 - Math.pow(1 - p, 3);
    const val = from + (to - from) * eased;
    el.textContent = format(val);
    if (p < 1) requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
}

function renderPrices() {
  document.querySelectorAll('.pricing-list:not([hidden]) .price-row').forEach(row => {
    const min = Number(row.dataset.min);
    const max = Number(row.dataset.max);
    const currency = CURRENCIES[state.currency];
    const rangeEl = row.querySelector('.price-range');
    const minVal = min * currency.fx;
    const maxVal = max * currency.fx;
    animateValue(rangeEl, 1, minVal, v => `${currency.fmt(v)} — ${currency.fmt(maxVal)}`);
  });
}
renderPrices();
$$('#currencySwitch .segmented-btn').forEach(btn => btn.addEventListener('click', () => {
  state.currency = btn.dataset.currency;
  autoButtonState('#currencySwitch .segmented-btn', state.currency, 'data-currency');
  renderPrices();
}));
$$('#serviceSwitch .segmented-btn').forEach(btn => btn.addEventListener('click', () => {
  state.service = btn.dataset.service;
  autoButtonState('#serviceSwitch .segmented-btn', state.service, 'data-service');
  document.querySelectorAll('.pricing-list').forEach(list => {
    const active = list.dataset.list === state.service;
    list.hidden = !active;
    list.classList.toggle('active', active);
  });
  renderPrices();
}));

const budgetSlider = $('#budgetSlider');
const budgetValue = $('#budgetValue');
const budgetHint = $('#budgetHint');
function updateBudget() {
  const value = Number(budgetSlider.value);
  budgetValue.textContent = `${value.toLocaleString('en-US')} DZD`;
  let hint = 'Custom quote range';
  if (value <= 7500) hint = 'Simple site fits best';
  else if (value <= 10000) hint = 'Basic site / Simple app range';
  else if (value <= 15000) hint = 'Advanced site / Simple app';
  else if (value <= 30000) hint = 'All you need site / Basic app';
  else if (value <= 60000) hint = 'Advanced app range';
  else hint = 'Full custom app range';
  budgetHint.textContent = hint;
}
budgetSlider.addEventListener('input', updateBudget); updateBudget();

const modal = $('#previewModal');
const frame = $('#previewFrame');
const modalTitle = $('#modalTitle');
$$('.open-preview').forEach(btn => btn.addEventListener('click', () => {
  frame.src = btn.dataset.src;
  modalTitle.textContent = btn.dataset.title || 'Preview';
  modal.showModal();
}));
$('#closePreviewBtn').addEventListener('click', () => { modal.close(); frame.src = ''; });
modal.addEventListener('click', e => { if (e.target === modal) { modal.close(); frame.src = ''; } });

if ('IntersectionObserver' in window) {
  const io = new IntersectionObserver(entries => {
    entries.forEach(entry => {
      if (entry.isIntersecting) entry.target.classList.add('in');
    });
  }, { threshold: .12, rootMargin: '0px 0px -30px 0px' });
  $$('.reveal').forEach(el => io.observe(el));
} else {
  $$('.reveal').forEach(el => el.classList.add('in'));
}

$$('.squish, .squish-surface').forEach(el => {
  ['pointerdown','click'].forEach(evt => el.addEventListener(evt, () => {
    el.classList.remove('squished');
    void el.offsetWidth;
    el.classList.add('squished');
  }));
});

let pointerX = innerWidth / 2;
let pointerY = innerHeight / 2;
const glow = $('.cursor-glow');
window.addEventListener('pointermove', e => { pointerX = e.clientX; pointerY = e.clientY; });
function animateGlow() {
  const x = parseFloat(glow.dataset.x || pointerX);
  const y = parseFloat(glow.dataset.y || pointerY);
  const nx = x + (pointerX - x) * 0.08;
  const ny = y + (pointerY - y) * 0.08;
  glow.style.transform = `translate(${nx - 110}px, ${ny - 110}px)`;
  glow.dataset.x = nx;
  glow.dataset.y = ny;
  requestAnimationFrame(animateGlow);
}
animateGlow();

let touchStartX = 0;
window.addEventListener('touchstart', e => touchStartX = e.changedTouches[0].clientX, { passive: true });
window.addEventListener('touchend', e => {
  const delta = e.changedTouches[0].clientX - touchStartX;
  if (Math.abs(delta) < 80) return;
  if (delta < 0 && !document.body.classList.contains('nav-hidden') && e.changedTouches[0].clientY < 120) document.body.classList.add('nav-hidden');
  if (delta > 0 && document.body.classList.contains('nav-hidden')) document.body.classList.remove('nav-hidden');
}, { passive: true });

}).catch(err=>{console.error(err);document.getElementById('main').innerHTML='<section style="padding:120px 24px;text-align:center;font-family:system-ui">Portfolio failed to load. Please refresh.</section>';});
