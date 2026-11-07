// Son sürüm numarası GitHub'dan gelir; gelmezse rozet hiç görünmez (uydurma sayı yok).
const URL = 'https://api.github.com/repos/koyhq/koy/releases/latest';
const KEY = 'koy-son-surum';

function readCache() {
  try {
    const raw = sessionStorage.getItem(KEY);
    return raw ? JSON.parse(raw) : null;
  } catch { return null; }
}

function writeCache(value) {
  try { sessionStorage.setItem(KEY, JSON.stringify(value)); } catch { /* özel pencere */ }
}

export async function initVersion() {
  const badge = document.getElementById('surum');
  const label = document.getElementById('surum-no');
  if (!badge || !label) return;

  let release = readCache();
  if (!release) {
    try {
      const res = await fetch(URL, { headers: { Accept: 'application/vnd.github+json' } });
      if (!res.ok) return;
      const data = await res.json();
      release = { tag: data.tag_name, url: data.html_url };
      writeCache(release);
    } catch { return; }
  }
  if (!release?.tag) return;
  label.textContent = release.tag;
  if (release.url?.startsWith('https://github.com/koyhq/koy/')) badge.href = release.url;
  badge.hidden = false;
}
