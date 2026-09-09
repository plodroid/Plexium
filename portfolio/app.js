(() => {
  const root = document.documentElement;
  const themeButton = document.getElementById('themeToggle');
  const savedTheme = localStorage.getItem('plexium-portfolio-theme');
  if (savedTheme === 'light' || savedTheme === 'dark') {
    root.dataset.theme = savedTheme;
  } else {
    root.dataset.theme = matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  }

  themeButton?.addEventListener('click', () => {
    const next = root.dataset.theme === 'dark' ? 'light' : 'dark';
    root.dataset.theme = next;
    localStorage.setItem('plexium-portfolio-theme', next);
  });

  const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const reveals = [...document.querySelectorAll('.reveal')];
  if (reduceMotion || !('IntersectionObserver' in window)) {
    reveals.forEach(el => el.classList.add('in'));
  } else {
    const observer = new IntersectionObserver(entries => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        entry.target.classList.add('in');
        observer.unobserve(entry.target);
      }
    }, { threshold: 0.12, rootMargin: '0px 0px -24px' });
    reveals.forEach(el => observer.observe(el));
  }

  const tabs = [...document.querySelectorAll('.price-tab')];
  const panels = [...document.querySelectorAll('.price-panel')];
  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      tabs.forEach(other => {
        const active = other === tab;
        other.classList.toggle('active', active);
        other.setAttribute('aria-selected', String(active));
      });
      panels.forEach(panel => {
        const active = panel.id === tab.dataset.target;
        panel.classList.toggle('active', active);
        panel.hidden = !active;
        if (active) panel.querySelectorAll('.reveal').forEach(el => el.classList.add('in'));
      });
    });
  });

  const DZD_PER_GBP = 300;
  const USD_PER_GBP = 1.355;
  const gbp = dzd => dzd / DZD_PER_GBP;
  const usd = dzd => gbp(dzd) * USD_PER_GBP;
  const neat = value => {
    if (Math.abs(value - Math.round(value)) < 0.05) return Math.round(value).toLocaleString('en-GB');
    return value.toFixed(1);
  };

  document.querySelectorAll('.price-card[data-min][data-max]').forEach(card => {
    const min = Number(card.dataset.min);
    const max = Number(card.dataset.max);
    card.querySelector('.gbp').textContent = `≈ £${neat(gbp(min))}–£${neat(gbp(max))}`;
    card.querySelector('.usd').textContent = `≈ $${Math.round(usd(min)).toLocaleString('en-US')}–$${Math.round(usd(max)).toLocaleString('en-US')}`;
  });

  const lightbox = document.getElementById('lightbox');
  const lightboxImage = document.getElementById('lightboxImage');
  const closeLightbox = () => {
    if (lightbox?.open) lightbox.close();
  };

  document.querySelectorAll('.open-lightbox').forEach(button => {
    button.addEventListener('click', () => {
      if (!lightbox || !lightboxImage) return;
      lightboxImage.src = button.dataset.image;
      lightboxImage.alt = button.dataset.alt || '';
      lightbox.showModal();
    });
  });
  document.getElementById('lightboxClose')?.addEventListener('click', closeLightbox);
  lightbox?.addEventListener('click', event => {
    if (event.target === lightbox) closeLightbox();
  });

  const brief = `Project type: Website / App
Business / project:
What it needs to do:
Pages or main screens:
Style / demo I like:
Must-have features:
Content I already have (logo, photos, text, menu, prices):
Target budget:
Anything else:`;

  const copyButton = document.getElementById('copyBrief');
  const copyStatus = document.getElementById('copyStatus');
  copyButton?.addEventListener('click', async () => {
    try {
      await navigator.clipboard.writeText(brief);
      copyStatus.textContent = 'Project brief copied.';
      copyButton.textContent = 'Copied';
    } catch {
      copyStatus.textContent = 'Could not copy automatically — select the brief from your browser instead.';
    }
    setTimeout(() => {
      copyButton.textContent = 'Copy a project brief';
      copyStatus.textContent = '';
    }, 2200);
  });
})();
