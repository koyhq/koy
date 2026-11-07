"""Sosyal medya önizleme görselini (1200x630) üretir: site/img/og-koy-mavi.png ve assets/branding/org/koy-sosyal-onizleme.png.

Tasarım: logonun mavi gradyanı (#3B82F6 -> #1D4ED8) zemin, beyaz logo, büyük başlık, adres hapı. Görsel yok, yalnız yazı.
Başlık sitenin hero'sundakiyle aynı olmalı (site/index.html).
Kaynak: assets/branding/dist/koy-horizontal-white-800.png. Yazı tipi Segoe UI (Windows'ta hazır).
Çalıştır: python scripts/generate-og-banner.py
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONTS = Path("C:/Windows/Fonts")
W, H, S = 1200, 630, 2  # S: önce 2 kat büyük çizilir, sonra küçültülür (pürüzsüz kenar)

BLUE_TOP = np.array([59, 130, 246], dtype=np.float32)   # #3B82F6
BLUE_BOTTOM = np.array([29, 78, 216], dtype=np.float32)  # #1D4ED8
WHITE = (255, 255, 255)
SOFT = (191, 219, 254)  # #BFDBFE: ikinci satır, beyazdan bir kademe geri
SUB = (219, 234, 254)   # #DBEAFE


def font(name: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONTS / name), size * S)


def gradient(w: int, h: int) -> Image.Image:
    t = np.linspace(0, 1, h, dtype=np.float32)[:, None, None]
    row = BLUE_TOP * (1 - t) + BLUE_BOTTOM * t
    return Image.fromarray(np.repeat(row, w, axis=1).astype(np.uint8), "RGB")


def main() -> None:
    img = gradient(W * S, H * S).convert("RGBA")
    draw = ImageDraw.Draw(img)

    logo = Image.open(ROOT / "assets/branding/dist/koy-horizontal-white-800.png").convert("RGBA")
    lh = 56 * S
    logo = logo.resize((round(logo.width * lh / logo.height), lh), Image.LANCZOS)
    img.alpha_composite(logo, (72 * S, 56 * S))

    big = font("seguisb.ttf", 112)
    draw.text((72 * S, 150 * S), "Masaüstünü", font=big, fill=WHITE)
    draw.text((72 * S, 274 * S), "hafifçe değiştir.", font=big, fill=SOFT)
    draw.text((76 * S, 448 * S), "Windows 11 için çentik ve dock. Tarayıcı yok, ücretsiz ve açık kaynak.", font=font("segoeui.ttf", 30), fill=SUB)

    # Adres hapı: beyaz zemin, mavi yazı
    label = "koyhq.github.io/koy"
    f = font("seguisb.ttf", 26)
    tw = draw.textlength(label, font=f)
    pw, ph = tw + 60 * S, 56 * S
    x0, y0 = 72 * S, 520 * S
    draw.rounded_rectangle((x0, y0, x0 + pw, y0 + ph), radius=ph / 2, fill=WHITE)
    draw.text((x0 + 30 * S, y0 + 10 * S), label, font=f, fill=(29, 78, 216))

    out = img.convert("RGB").resize((W, H), Image.LANCZOS)
    for rel in ("site/img/og-koy-mavi.png", "assets/branding/org/koy-sosyal-onizleme.png"):
        out.save(ROOT / rel, optimize=True)
        print("yazildi:", rel, (ROOT / rel).stat().st_size // 1024, "KB")


if __name__ == "__main__":
    main()
