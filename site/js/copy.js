// "Kopyala" düğmeleri: hedef öğenin metnini panoya yazar, sonucu düğmede gösterir.
export function initCopy() {
  document.querySelectorAll('[data-copy]').forEach((btn) => {
    const target = document.querySelector(btn.dataset.copy);
    const label = btn.querySelector('[data-copy-label]');
    if (!target || !label) return;
    const original = label.textContent;
    btn.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(target.textContent.trim());
        label.textContent = 'Kopyalandı';
      } catch {
        const range = document.createRange();
        range.selectNodeContents(target);
        const sel = getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
        label.textContent = 'Seçildi, Ctrl+C';
      }
      setTimeout(() => { label.textContent = original; }, 1800);
    });
  });
}
