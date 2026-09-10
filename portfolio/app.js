async function loadPageFragments() {
  const main = document.getElementById('main');
  const count = Number(main?.dataset.fragments || 0);
  if (!main || !count) return;
  const html = await Promise.all(
    Array.from({ length: count }, (_, i) =>
      fetch(`fragments/part${i + 1}.html`).then(response => {
        if (!response.ok) throw new Error(`Failed to load part ${i + 1}`);
        return response.text();
      })
    )
  );
  main.innerHTML = html.join('\n');
}

loadPageFragments().then(() => {
  const root = document.documentElement;
  const $ = selector => document.querySelector(selector);
  const $$ = selector => [...document.querySelectorAll(selector)];
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  const state = { appIndex: 0, currency: 'dzd', service: 'web', calc: '0' };

  const appearance = localStorage.getItem('portfolio-appearance') || 'light';
  root.dataset.appearance = ['light', 'dark', 'system'].includes(appearance) ? appearance : 'light';

  const setActiveValue = (selector, value, attribute) => {
    $$(selector).forEach(button => button.classList.toggle('active', button.getAttribute(attribute) === value));
  };
  setActiveValue('[data-setting="appearance"] .segmented-btn', root.dataset.appearance, 'data-value');

  const settingsSheet = $('#settingsSheet');
  const settingsButton = $('#settingsBtn');
  const settingsPanel = settingsSheet?.querySelector('.sheet-panel');
  const openSettings = () => {
    if (!settingsSheet || !settingsPanel) return;
    settingsSheet.classList.add('open');
    settingsSheet.setAttribute('aria-hidden', 'false');
    settingsButton?.setAttribute('aria-expanded', 'true');
    if (!reducedMotion.matches) {
      settingsPanel.animate(
        [
          { opacity: 0, transform: 'translateY(-8px) scale(.985)' },
          { opacity: 1, transform: 'translateY(0) scale(1)' }
        ],
        { duration: 240, easing: 'cubic-bezier(.16,1,.3,1)' }
      );
    }
  };
  const closeSettings = () => {
    if (!settingsSheet || !settingsPanel) return;
    const finish = () => {
      settingsSheet.classList.remove('open');
      settingsSheet.setAttribute('aria-hidden', 'true');
      settingsButton?.setAttribute('aria-expanded', 'false');
    };
    if (reducedMotion.matches) return finish();
    const animation = settingsPanel.animate(
      [
        { opacity: 1, transform: 'translateY(0) scale(1)' },
        { opacity: 0, transform: 'translateY(-6px) scale(.99)' }
      ],
      { duration: 160, easing: 'cubic-bezier(.4,0,1,1)' }
    );
    animation.addEventListener('finish', finish, { once: true });
  };
  settingsButton?.addEventListener('click', openSettings);
  $('#closeSettingsBtn')?.addEventListener('click', closeSettings);
  $('#sheetBackdrop')?.addEventListener('click', closeSettings);
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && settingsSheet?.classList.contains('open')) closeSettings();
  });

  $$('[data-setting="appearance"] .segmented-btn').forEach(button => {
    button.addEventListener('click', () => {
      root.dataset.appearance = button.dataset.value;
      localStorage.setItem('portfolio-appearance', button.dataset.value);
      setActiveValue('[data-setting="appearance"] .segmented-btn', button.dataset.value, 'data-value');
    });
  });

  function showApp(index) {
    state.appIndex = index;
    $$('.app-stage').forEach((element, i) => element.classList.toggle('active', i === index));
    $$('.mini-tab').forEach((element, i) => element.classList.toggle('active', i === index));
    $$('[data-app-select]').forEach((element, i) => element.classList.toggle('active', i === index));
    $$('.stack-card').forEach((element, i) => element.classList.toggle('active', i === index));
    $$('[data-copy]').forEach((element, i) => element.classList.toggle('active', i === index));
  }
  showApp(0);
  $$('.mini-tab').forEach((button, i) => button.addEventListener('click', () => showApp(i)));
  $$('[data-app-select]').forEach((button, i) => button.addEventListener('click', () => showApp(i)));
  $$('.stack-card').forEach((button, i) => button.addEventListener('click', () => showApp(i)));

  const copyBlocks = $$('[data-copy]');
  if ('IntersectionObserver' in window && copyBlocks.length) {
    const storyObserver = new IntersectionObserver(entries => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
      if (!visible) return;
      const index = Number(visible.target.dataset.copy);
      if (Number.isFinite(index)) showApp(index);
    }, { threshold: [0.35, 0.55, 0.75], rootMargin: '-24% 0px -38% 0px' });
    copyBlocks.forEach(block => storyObserver.observe(block));
  }

  const stockData = [
    { name: 'Apple Inc.', price: '$223.10', path: 'M10 130 C60 124, 90 90, 140 95 S220 150, 280 96 S330 60, 390 74' },
    { name: 'NVIDIA', price: '$174.70', path: 'M10 150 C60 120, 110 126, 140 112 S220 78, 280 90 S330 130, 390 44' },
    { name: 'Microsoft', price: '$520.80', path: 'M10 112 C50 100, 90 88, 130 96 S220 124, 260 102 S330 52, 390 66' },
    { name: 'AMD', price: '$178.20', path: 'M10 144 C60 166, 100 88, 150 100 S220 116, 270 84 S330 90, 390 48' }
  ];
  $$('.ticker').forEach((button, i) => button.addEventListener('click', () => {
    $$('.ticker').forEach(item => item.classList.remove('active'));
    button.classList.add('active');
    $('#stockName').textContent = stockData[i].name;
    $('#stockPrice').textContent = stockData[i].price;
    $('#stockPath').setAttribute('d', stockData[i].path);
  }));

  const calcDisplay = $('#calcDisplay');
  $$('.calc-key').forEach(button => button.addEventListener('click', () => {
    const key = button.dataset.key;
    if (key === 'c') state.calc = '0';
    else if (key === '=') {
      try { state.calc = String(Function(`return (${state.calc})`)()); }
      catch { state.calc = 'Err'; }
    } else state.calc = state.calc === '0' || state.calc === 'Err' ? key : state.calc + key;
    calcDisplay.textContent = state.calc;
  }));

  const currencies = {
    dzd: { format: value => `${Math.round(value).toLocaleString('en-US')} DZD`, fx: 1 },
    usd: { format: value => `$${Math.round(value).toLocaleString('en-US')}`, fx: 1 / 217.5 },
    gbp: { format: value => `£${(value / 300).toFixed(value / 300 >= 100 ? 0 : 1)}`, fx: 1 / 300 }
  };
  const animateNumber = (element, from, to, format) => {
    if (!element) return;
    if (reducedMotion.matches) {
      element.textContent = format(to);
      return;
    }
    const started = performance.now();
    const duration = 520;
    const tick = now => {
      const progress = Math.min(1, (now - started) / duration);
      const eased = 1 - Math.pow(1 - progress, 3);
      element.textContent = format(from + (to - from) * eased);
      if (progress < 1) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  };
  function renderPrices() {
    const currency = currencies[state.currency];
    $$('.pricing-list:not([hidden]) .price-row').forEach(row => {
      const min = Number(row.dataset.min) * currency.fx;
      const max = Number(row.dataset.max) * currency.fx;
      const range = row.querySelector('.price-range');
      animateNumber(range, 0, min, value => `${currency.format(value)} — ${currency.format(max)}`);
    });
  }
  $$('#currencySwitch .segmented-btn').forEach(button => button.addEventListener('click', () => {
    state.currency = button.dataset.currency;
    setActiveValue('#currencySwitch .segmented-btn', state.currency, 'data-currency');
    renderPrices();
  }));
  $$('#serviceSwitch .segmented-btn').forEach(button => button.addEventListener('click', () => {
    state.service = button.dataset.service;
    setActiveValue('#serviceSwitch .segmented-btn', state.service, 'data-service');
    $$('.pricing-list').forEach(list => {
      const active = list.dataset.list === state.service;
      list.hidden = !active;
      list.classList.toggle('active', active);
    });
    renderPrices();
  }));
  renderPrices();

  const budgetSlider = $('#budgetSlider');
  const budgetValue = $('#budgetValue');
  const budgetHint = $('#budgetHint');
  function updateBudget() {
    if (!budgetSlider || !budgetValue || !budgetHint) return;
    const value = Number(budgetSlider.value);
    budgetValue.textContent = `${value.toLocaleString('en-US')} DZD`;
    if (value <= 7500) budgetHint.textContent = 'Simple site fits best';
    else if (value <= 10000) budgetHint.textContent = 'Basic site / Simple app range';
    else if (value <= 15000) budgetHint.textContent = 'Advanced site / Simple app';
    else if (value <= 30000) budgetHint.textContent = 'All you need site / Basic app';
    else if (value <= 60000) budgetHint.textContent = 'Advanced app range';
    else budgetHint.textContent = 'Full custom app range';
  }
  budgetSlider?.addEventListener('input', updateBudget);
  updateBudget();

  const modal = $('#previewModal');
  const frame = $('#previewFrame');
  const modalTitle = $('#modalTitle');
  const modalWindow = modal?.querySelector('.modal-window');
  const openPreview = button => {
    if (!modal || !frame) return;
    frame.src = button.dataset.src;
    modalTitle.textContent = button.dataset.title || 'Preview';
    modal.showModal();
    if (!reducedMotion.matches && modalWindow) {
      modalWindow.animate(
        [
          { opacity: 0, transform: 'translateY(10px) scale(.985)' },
          { opacity: 1, transform: 'translateY(0) scale(1)' }
        ],
        { duration: 260, easing: 'cubic-bezier(.16,1,.3,1)' }
      );
    }
  };
  const closePreview = () => {
    if (!modal?.open) return;
    modal.close();
    if (frame) frame.src = '';
  };
  $$('.open-preview').forEach(button => button.addEventListener('click', () => openPreview(button)));
  $('#closePreviewBtn')?.addEventListener('click', closePreview);
  modal?.addEventListener('click', event => { if (event.target === modal) closePreview(); });

  const revealTargets = new Set($$('.reveal'));
  const extras = [
    '.section .section-head', '.continuous-gallery', '.sticky-copy .copy-block', '.sticky-visual',
    '.concept-card', '.compare-card', '.pricing-toolbar', '.pricing-wrap', '.budget-box',
    '.support-box', '.faq-list details', '.contact-copy', '.contact-form', '.footer-inner'
  ];
  extras.forEach(selector => $$(selector).forEach(element => revealTargets.add(element)));
  revealTargets.forEach(element => element.classList.add('reveal'));
  $$('.section').forEach(section => {
    const items = [...section.querySelectorAll('.reveal')];
    items.forEach((element, index) => element.style.setProperty('--reveal-delay', `${Math.min(index * 55, 220)}ms`));
  });

  if (reducedMotion.matches || !('IntersectionObserver' in window)) {
    revealTargets.forEach(element => element.classList.add('in'));
  } else {
    const revealObserver = new IntersectionObserver(entries => {
      entries.forEach(entry => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('in');
        revealObserver.unobserve(entry.target);
      });
    }, { threshold: 0.08, rootMargin: '0px 0px -10% 0px' });
    revealTargets.forEach(element => revealObserver.observe(element));
  }

  document.body.classList.add('portfolio-ready');
}).catch(error => {
  console.error(error);
  document.getElementById('main').innerHTML = '<section style="padding:120px 24px;text-align:center;font-family:system-ui">Portfolio failed to load. Please refresh.</section>';
});
