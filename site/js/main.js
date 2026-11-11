// Alt modüller sürüm etiketiyle yüklenir: güncellemede eski önbellek karışmasın (docs/MAP.md).
import { initReveal } from './reveal.js?v=20261112-2';
import { initTabs } from './tabs.js?v=20261112-2';
import { initVersion } from './version.js?v=20261112-2';
import { initCopy } from './copy.js?v=20261112-2';
import { initNotch } from './notch.js?v=20261112-2';
import { initParallax } from './parallax.js?v=20261112-2';

// Giriş animasyonu fontlar yerleşince başlar; yoksa başlık sıçrar.
const fontsReady = document.fonts?.ready ?? Promise.resolve();
const timeout = new Promise((resolve) => setTimeout(resolve, 1200));
Promise.race([fontsReady, timeout]).then(() => {
  requestAnimationFrame(() => document.body.classList.add('is-ready'));
});

initReveal();
document.querySelectorAll('[data-tabs]').forEach(initTabs);
initCopy();
document.querySelectorAll('[data-notch]').forEach(initNotch);
initParallax();
initVersion();
