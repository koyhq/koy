// Hayalet yazı kaydırırken çok hafif kayar (yalnız transform). Ekran dışındayken izlenmez.
export function initParallax() {
  if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  const items = [...document.querySelectorAll('[data-parallax]')].map((el) => ({ el, host: el.parentElement, visible: false }));
  if (!items.length || !('IntersectionObserver' in window)) return;

  let queued = false;
  const update = () => {
    queued = false;
    for (const it of items) {
      if (!it.visible) continue;
      const r = it.host.getBoundingClientRect();
      const p = (r.top + r.height / 2 - innerHeight / 2) / innerHeight;
      it.el.style.setProperty('--py', `${(p * -90).toFixed(1)}px`);
    }
  };
  const schedule = () => { if (!queued) { queued = true; requestAnimationFrame(update); } };

  const io = new IntersectionObserver((entries) => {
    for (const e of entries) items.find((it) => it.host === e.target).visible = e.isIntersecting;
    schedule();
  });
  items.forEach((it) => io.observe(it.host));
  addEventListener('scroll', schedule, { passive: true });
  addEventListener('resize', schedule);
}
