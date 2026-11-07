// Çentik sekmeleri: ok tuşlarıyla gezinilir, görsel ve metin birlikte değişir.
export function initTabs(root) {
  const tabs = [...root.querySelectorAll('[role="tab"]')];
  const panels = [...root.querySelectorAll('[role="tabpanel"]')];
  const shots = [...root.querySelectorAll('.shot')];

  function select(tab, focus = false) {
    const key = tab.dataset.key;
    for (const t of tabs) {
      const on = t === tab;
      t.setAttribute('aria-selected', String(on));
      t.tabIndex = on ? 0 : -1;
    }
    for (const p of panels) p.hidden = p.dataset.key !== key;
    for (const s of shots) s.classList.toggle('is-active', s.dataset.key === key);
    if (focus) tab.focus();
  }

  for (const t of tabs) t.addEventListener('click', () => select(t));
  root.querySelector('[role="tablist"]').addEventListener('keydown', (e) => {
    const i = tabs.indexOf(document.activeElement);
    if (i < 0) return;
    const step = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 }[e.key];
    if (step) {
      e.preventDefault();
      select(tabs[(i + step + tabs.length) % tabs.length], true);
    } else if (e.key === 'Home' || e.key === 'End') {
      e.preventDefault();
      select(e.key === 'Home' ? tabs[0] : tabs.at(-1), true);
    }
  });
}
