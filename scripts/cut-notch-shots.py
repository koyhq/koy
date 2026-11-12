"""Gri zemine çekilmiş çentik ekran görüntülerini saydam zemine çevirir ve boşluğu kırpar.

Site çentiği gece mavisi zeminin (.aurora) üstünde gösterir; görüntünün gri zemini orada kutu gibi
görünür. Zemin kenardan taşmayla (flood fill) bulunur; zemine değen kenar pikselleri gri ile çentik siyahının
karışımından alfa alır, böylece kulaklar ve köşeler yumuşak kalır. Çentiğin içi olduğu gibi kalır.

Çalıştır: python scripts/cut-notch-shots.py site/img/centik-ana.webp [başka dosyalar...]
Dosyanın üzerine yazar. Zaten saydam olan görüntüye ikinci kez uygulama.
"""
import sys
from collections import deque

from PIL import Image

NOTCH = (10, 10, 11)  # çentik zemini (#0A0A0B)
TOLERANCE = 10


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def cut(path):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    px = im.load()
    bg = px[1, 1]

    def near(c):
        return all(abs(c[i] - bg[i]) <= TOLERANCE for i in range(3))

    is_bg = bytearray(w * h)
    queue = deque([(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)])
    while queue:
        x, y = queue.popleft()
        i = y * w + x
        if is_bg[i] or not near(px[x, y]):
            continue
        is_bg[i] = 1
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not is_bg[ny * w + nx]:
                queue.append((nx, ny))

    out = Image.new("RGBA", (w, h))
    op = out.load()
    lb, ln = lum(bg), lum(NOTCH)
    for y in range(h):
        for x in range(w):
            if is_bg[y * w + x]:
                op[x, y] = (0, 0, 0, 0)
                continue
            c = px[x, y]
            edge = any(
                0 <= x + dx < w and 0 <= y + dy < h and is_bg[(y + dy) * w + x + dx]
                for dx in (-1, 0, 1) for dy in (-1, 0, 1)
            )
            if edge and lum(c) > ln:
                alpha = max(0.0, min(1.0, 1 - (lum(c) - ln) / (lb - ln)))
                op[x, y] = (*NOTCH, round(alpha * 255))
            else:
                op[x, y] = (*c, 255)

    out = out.crop(out.getbbox())
    out.save(path, "WEBP", quality=90, alpha_quality=100, method=6)
    print(path, out.size)


if __name__ == "__main__":
    for arg in sys.argv[1:]:
        cut(arg)
