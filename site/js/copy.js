// "Kopyala" düğmeleri: hedef öğenin metnini panoya yazar, sonucu düğmede gösterir.
export function initCopy() {
  document.querySelectorAll('[data-copy]').forEach((btn) => {
    const target = document.querySelector(btn.dataset.copy);
    const label = btn.querySelector('[data-copy-label]');
    if (!target || !label) return;
    btn.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(target.textContent.trim());
        label.textContent = window.KoyI18n.t('copy.done');
      } catch {
        const range = document.createRange();
        range.selectNodeContents(target);
        const sel = getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
        label.textContent = window.KoyI18n.t('copy.selected');
      }
      setTimeout(() => { label.textContent = window.KoyI18n.t('copy.label'); }, 1800);
    });
  });
}
