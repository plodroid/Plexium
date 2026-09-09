(() => {
  const root = document.documentElement;
  const themeButton = document.getElementById('themeToggle');
  const storedTheme = localStorage.getItem('portfolio-appearance');
  const systemDark = matchMedia('(prefers-color-scheme: dark)');

  const applyTheme = value => {
    root.dataset.theme = value;
    root.style.colorScheme = value === 'system' ? 'light dark' : value;
  };

  applyTheme(storedTheme === 'light' || storedTheme === 'dark' ? storedTheme : 'system');

  themeButton?.addEventListener('click', () => {
    const effectiveDark = root.dataset.theme === 'dark' || (root.dataset.theme === 'system' && systemDark.matches);
    const next = effectiveDark ? 'light' : 'dark';
    applyTheme(next);
    localStorage.setItem('portfolio-appearance', next);
  });

  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  const revealItems = [...document.querySelectorAll('.reveal')];
  if (reducedMotion.matches || !('IntersectionObserver' in window)) {
    revealItems.forEach(item => item.classList.add('in'));
  } else {
    const observer = new IntersectionObserver(entries => {
      entries.forEach(entry => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('in');
        observer.unobserve(entry.target);
      });
    }, { threshold: 0.12, rootMargin: '0px 0px -24px' });
    revealItems.forEach(item => observer.observe(item));
  }

  const header = document.getElementById('siteHeader');
  let lastScroll = window.scrollY;
  let ticking = false;
  const updateHeader = () => {
    const current = window.scrollY;
    header?.classList.toggle('scrolled', current > 24);
    header?.classList.toggle('header-hidden', current > 180 && current > lastScroll + 6);
    if (current < lastScroll - 6) header?.classList.remove('header-hidden');
    lastScroll = current;
    ticking = false;
  };
  addEventListener('scroll', () => {
    if (ticking) return;
    ticking = true;
    requestAnimationFrame(updateHeader);
  }, { passive: true });
  updateHeader();

  const showcaseTabs = [...document.querySelectorAll('.showcase-tab')];
  const showcaseImages = [...document.querySelectorAll('.showcase-image')];
  const showcaseTitle = document.getElementById('showcaseTitle');
  showcaseTabs.forEach(tab => {
    tab.addEventListener('click', () => {
      const key = tab.dataset.showcase;
      showcaseTabs.forEach(button => {
        const active = button === tab;
        button.classList.toggle('active', active);
        button.setAttribute('aria-selected', String(active));
      });
      showcaseImages.forEach(image => image.classList.toggle('active', image.dataset.showcaseImage === key));
      if (showcaseTitle) showcaseTitle.textContent = tab.dataset.title || '';
    });
  });

  const priceTabs = [...document.querySelectorAll('.segment')];
  const pricePanels = [...document.querySelectorAll('.price-panel')];
  const indicator = document.querySelector('.segment-indicator');
  const moveIndicator = tab => {
    if (!indicator || !tab) return;
    indicator.style.transform = `translateX(${tab.offsetLeft}px)`;
    indicator.style.width = `${tab.offsetWidth}px`;
  };
  priceTabs.forEach(tab => {
    tab.addEventListener('click', () => {
      priceTabs.forEach(button => {
        const active = button === tab;
        button.classList.toggle('active', active);
        button.setAttribute('aria-selected', String(active));
      });
      pricePanels.forEach(panel => {
        const active = panel.id === tab.dataset.target;
        panel.classList.toggle('active', active);
        panel.hidden = !active;
      });
      moveIndicator(tab);
    });
  });
  requestAnimationFrame(() => moveIndicator(document.querySelector('.segment.active')));
  addEventListener('resize', () => moveIndicator(document.querySelector('.segment.active')), { passive: true });

  const DZD_PER_GBP = 300;
  const USD_PER_GBP = 1.355;
  const neat = value => Math.abs(value - Math.round(value)) < 0.05 ? Math.round(value).toLocaleString('en-GB') : value.toFixed(1);
  document.querySelectorAll('.price-row[data-min][data-max]').forEach(row => {
    const min = Number(row.dataset.min);
    const max = Number(row.dataset.max);
    const minGbp = min / DZD_PER_GBP;
    const maxGbp = max / DZD_PER_GBP;
    const minUsd = minGbp * USD_PER_GBP;
    const maxUsd = maxGbp * USD_PER_GBP;
    row.querySelector('.gbp').textContent = `≈ £${neat(minGbp)}–£${neat(maxGbp)}`;
    row.querySelector('.usd').textContent = `≈ $${Math.round(minUsd).toLocaleString('en-US')}–$${Math.round(maxUsd).toLocaleString('en-US')}`;
  });

  const lightbox = document.getElementById('lightbox');
  const lightboxImage = document.getElementById('lightboxImage');
  const closeLightbox = () => lightbox?.open && lightbox.close();
  document.querySelectorAll('.open-lightbox').forEach(button => {
    button.addEventListener('click', () => {
      if (!lightbox || !lightboxImage) return;
      lightboxImage.src = button.dataset.image || '';
      lightboxImage.alt = button.dataset.alt || '';
      lightbox.showModal();
    });
  });
  document.getElementById('lightboxClose')?.addEventListener('click', closeLightbox);
  lightbox?.addEventListener('click', event => {
    if (event.target === lightbox) closeLightbox();
  });

  const brief = `Project type: Website / App\nBusiness / project:\nWhat it needs to do:\nPages or main screens:\nStyle / reference I like:\nMust-have features:\nContent I already have (logo, photos, text, menu, prices):\nTarget budget:\nAnything else:`;
  const copyButton = document.getElementById('copyBrief');
  const copyStatus = document.getElementById('copyStatus');
  let copyTimer;
  copyButton?.addEventListener('click', async () => {
    clearTimeout(copyTimer);
    try {
      await navigator.clipboard.writeText(brief);
      copyButton.textContent = 'Copied';
      copyButton.classList.add('success');
      if (copyStatus) copyStatus.textContent = 'Project brief copied to your clipboard.';
    } catch {
      if (copyStatus) copyStatus.textContent = 'Your browser blocked clipboard access. You can copy the fields shown above manually.';
    }
    copyTimer = setTimeout(() => {
      copyButton.textContent = 'Copy project brief';
      copyButton.classList.remove('success');
      if (copyStatus) copyStatus.textContent = '';
    }, 2400);
  });
})();
