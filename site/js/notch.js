// Etkileşimli çentik (Dock demosu ve hero aynı kodu kullanır): gerçek üründeki gibi fareyle açılır,
// kapanırken 400 ms tolerans tanır. Dokunmatikte dokununca açılıp kapanır, dışarı dokununca kapanır.
// Klavyede düğme gibi çalışır: odaklanınca açılır, Esc kapatır.
// data-notch="demo": kapalı başlar, görünür olunca bir kez kendiliğinden açılıp kapanır.
// data-notch="hero": kapalı başlar; fareyle (dokunmatikte dokununca) açılır.
// Motion yüklenirse açılış ve kapanış gerçek yayla (uygulamadaki değerler) oynar; yüklenmezse CSS geçişi.
import { loadMotion, OPEN, CLOSE } from './spring.js?v=20261112-2';

const CLOSE_DELAY = 400;
const CLOSED = { scaleX: 0.448, scaleY: 0.097 }; // kapalı çentiğin açık görüntüye oranı (415/926, 51/528)

export function initNotch(root) {
  const zone = root.querySelector('[data-notch-zone]');
  if (!zone) return;
  const hero = root.dataset.notch === 'hero';
  const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let closeTimer;
  let autoTimer;
  let touched = false;

  const openImg = root.querySelector('.stage__open');
  const closedImg = root.querySelector('.stage__closed');
  let motion = null;

  const isOpen = () => root.classList.contains('is-open');
  const set = (open) => {
    if (open === isOpen()) return;
    root.classList.toggle('is-open', open);
    zone.setAttribute('aria-expanded', String(open));
    if (!motion) return;
    motion.animate(openImg, open ? { scaleX: 1, scaleY: 1 } : CLOSED, open ? OPEN : CLOSE);
    motion.animate(openImg, { opacity: open ? 1 : 0 }, { duration: open ? 0.1 : 0.16, delay: open ? 0 : 0.22 });
    motion.animate(closedImg, { opacity: open ? 0 : 1 }, { duration: 0.12, delay: open ? 0 : 0.26 });
  };

  loadMotion().then((m) => {
    if (!m || !openImg || !closedImg) return;
    motion = m;
    root.classList.add('has-spring');
    // Başlangıç durumu Motion'a bildirilir; yoksa ilk animasyon ölçek 1'den başlar.
    const open = isOpen();
    m.animate(openImg, open ? { scaleX: 1, scaleY: 1, opacity: 1 } : { ...CLOSED, opacity: 0 }, { duration: 0 });
    m.animate(closedImg, { opacity: open ? 0 : 1 }, { duration: 0 });
  });
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

  // Hero kendiliğinden açılmaz: ziyaretçi üst kenara gelip kendisi açar.
  if (hero) return;

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
