// Dock bölümündeki ekran: çentik gerçek üründeki gibi fareyle açılır (kapanırken 400 ms tolerans),
// dokunmatik ve klavyede düğme gibi çalışır. Görünür olunca bir kez kendiliğinden açılıp kapanır.
export function initNotchDemo(root) {
  const zone = root.querySelector('.desktop__zone');
  if (!zone) return;
  let closeTimer;
  let autoTimer;
  let touched = false;

  const set = (open) => {
    root.classList.toggle('is-open', open);
    zone.setAttribute('aria-expanded', String(open));
  };
  const interact = () => {
    touched = true;
    clearTimeout(closeTimer);
    clearTimeout(autoTimer);
  };

  zone.addEventListener('pointerenter', (e) => {
    if (e.pointerType !== 'mouse') return;
    interact();
    set(true);
  });
  zone.addEventListener('pointerleave', (e) => {
    if (e.pointerType !== 'mouse') return;
    closeTimer = setTimeout(() => set(false), 400);
  });
  zone.addEventListener('click', (e) => {
    if (e.pointerType === 'mouse') return; // fare zaten üzerine gelince açtı
    interact();
    set(!root.classList.contains('is-open'));
  });
  zone.addEventListener('blur', () => set(false));

  if (matchMedia('(prefers-reduced-motion: reduce)').matches || !('IntersectionObserver' in window)) return;
  const io = new IntersectionObserver((entries) => {
    if (!entries.some((e) => e.isIntersecting)) return;
    io.disconnect();
    autoTimer = setTimeout(() => {
      if (touched) return;
      set(true);
      autoTimer = setTimeout(() => { if (!touched) set(false); }, 3800);
    }, 900);
  }, { threshold: 0.7 });
  io.observe(root);
}
