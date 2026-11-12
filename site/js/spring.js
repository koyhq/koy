// Gerçek yay fiziği için Motion (motion.dev) isteğe bağlı yüklenir. Gelmezse ya da hareket azaltılmışsa
// CSS geçişleri aynı işi yapar; sayfa hiçbir zaman bu kütüphaneye muhtaç kalmaz.
const MOTION_URL = 'https://cdn.jsdelivr.net/npm/motion@11.11.17/+esm';

export const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;

// Uygulamadaki Core/Animation/Spring ile aynı değerler: sertlik 300, sönüm 26, kütle 1 (açılışta hafif seker).
export const OPEN = { type: 'spring', stiffness: 300, damping: 26, mass: 1 };
// Kapanışta kritik sönüm (2·√(sertlik·kütle)): çentik hedefin içine çökmeden oturur.
export const CLOSE = { type: 'spring', stiffness: 300, damping: 2 * Math.sqrt(300), mass: 1 };

let loading;
export function loadMotion() {
  if (reduceMotion) return Promise.resolve(null);
  loading ??= import(MOTION_URL).catch(() => null);
  return loading;
}
