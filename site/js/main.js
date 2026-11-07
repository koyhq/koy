import { initReveal } from './reveal.js';
import { initTabs } from './tabs.js';
import { initVersion } from './version.js';
import { initCopy } from './copy.js';

// Giriş animasyonu fontlar yerleşince başlar; yoksa başlık sıçrar.
const fontsReady = document.fonts?.ready ?? Promise.resolve();
const timeout = new Promise((resolve) => setTimeout(resolve, 1200));
Promise.race([fontsReady, timeout]).then(() => {
  requestAnimationFrame(() => document.body.classList.add('is-ready'));
});

initReveal();
document.querySelectorAll('[data-tabs]').forEach(initTabs);
initCopy();
initVersion();
