// Etkileşimli çentik (Dock demosu ve hero aynı kodu kullanır): gerçek üründeki gibi fareyle açılır,
// kapanırken 400 ms tolerans tanır. Dokunmatikte dokununca açılıp kapanır, dışarı dokununca kapanır.
// Klavyede düğme gibi çalışır: odaklanınca açılır, Esc kapatır.
// data-notch="demo": kapalı başlar, görünür olunca bir kez kendiliğinden açılıp kapanır.
// data-notch="hero": girişte açık gelir (ilk izlenim, JS yoksa da böyle), kimse dokunmazsa bir süre sonra kapanır.
const CLOSE_DELAY = 400;

export function initNotch(root) {
  const zone = root.querySelector('[data-notch-zone]');
  if (!zone) return;
  const hero = root.dataset.notch === 'hero';
  const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let closeTimer;
  let autoTimer;
  let touched = false;

  const isOpen = () => root.classList.contains('is-open');
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
    closeTimer = setTimeout(() => set(false), CLOSE_DELAY);
  });
  zone.addEventListener('click', (e) => {
    if (e.pointerType === 'mouse') return; // fare zaten üzerine gelince açtı
    interact();
    set(!isOpen());
  });
  // Yalnız klavye odağı açar: dokununca düğme de odaklanır, o zaman click zaten açıp kapatıyor.
  zone.addEventListener('focus', () => {
    if (!zone.matches(':focus-visible')) return;
    interact();
    set(true);
  });
  zone.addEventListener('blur', () => set(false));
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape' || !isOpen()) return;
    interact();
    set(false);
  });
  // Dokunmatikte dışarı dokunmak kapatır (fare için pointerleave var).
  document.addEventListener('pointerdown', (e) => {
    if (e.pointerType === 'mouse' || !isOpen() || root.contains(e.target)) return;
    interact();
    set(false);
  });

  if (hero) {
    if (reduceMotion) return; // hareket istemeyene çentik açık kalır, fareyle ayrılınca kapanır
    autoTimer = setTimeout(() => { if (!touched) set(false); }, 4800);
    return;
  }

  if (reduceMotion || !('IntersectionObserver' in window)) return;
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
