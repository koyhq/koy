"""Sosyal medya önizleme görselini (1200x630) üretir: site/img/og-koy.png ve assets/branding/org/koy-sosyal-onizleme.png.

Kaynaklar: docs/screenshots'tan işlenmiş site/img/centik-ana.webp ve assets/branding/dist/koy-horizontal-color-800.png.
Yazı tipi Segoe UI (Windows'ta hazır). Çalıştır: python scripts/generate-og-banner.py
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONTS = Path("C:/Windows/Fonts")
W, H, S = 1200, 630, 2  # S: önce 2 kat büyük çizilir, sonra küçültülür (pürüzsüz kenar)

BG = (245, 245, 248)
INK = (11, 11, 15)
MUTED = (98, 98, 106)


def font(name: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONTS / name), size * S)


def centered(draw: ImageDraw.ImageDraw, parts: list[tuple[str, tuple, ImageFont.FreeTypeFont]], y: int) -> None:
    widths = [draw.textlength(t, font=f) for t, _, f in parts]
    x = (W * S - sum(widths)) / 2
    for (text, color, f), w in zip(parts, widths):
        draw.text((x, y * S), text, font=f, fill=color)
        x += w


def main() -> None:
    img = Image.new("RGB", (W * S, H * S), BG)
    draw = ImageDraw.Draw(img)

    # Çentik, gerçekte olduğu gibi sayfanın tepesinden sarkar (sitedeki hero ile aynı kompozisyon).
    shot = Image.open(ROOT / "site/img/centik-ana.webp").convert("RGB")
    fw = 560 * S
    fh = round(fw * shot.height / shot.width)
    shot = shot.resize((fw, fh), Image.LANCZOS)
    mask = Image.new("L", (fw, fh), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, -60 * S, fw, fh), radius=28 * S, fill=255)
    img.paste(shot, ((W * S - fw) // 2, 0), mask)

    logo = Image.open(ROOT / "assets/branding/dist/koy-horizontal-color-800.png").convert("RGBA")
    lh = 46 * S
    logo = logo.resize((round(logo.width * lh / logo.height), lh), Image.LANCZOS)
    img.paste(logo, (56 * S, 40 * S), logo)

    bold = font("seguisb.ttf", 68)
    centered(draw, [("Çentik yukarıda. ", INK, bold), ("Dock aşağıda.", MUTED, bold)], 352)
    centered(draw, [("Windows 11 için hafif çentik ve dock. Tarayıcı yok, ücretsiz ve açık kaynak.", MUTED, font("segoeui.ttf", 27))], 462)

    # Adres hapı
    label = "koyhq.github.io/koy"
    f = font("seguisb.ttf", 25)
    tw = draw.textlength(label, font=f)
    pw, ph = tw + 56 * S, 52 * S
    x0, y0 = (W * S - pw) / 2, 538 * S
    draw.rounded_rectangle((x0, y0, x0 + pw, y0 + ph), radius=ph / 2, fill=INK)
    draw.text((x0 + 28 * S, y0 + 9 * S), label, font=f, fill=(245, 245, 248))

    out = img.resize((W, H), Image.LANCZOS)
    for rel in ("site/img/og-koy.png", "assets/branding/org/koy-sosyal-onizleme.png"):
        out.save(ROOT / rel, optimize=True)
        print("yazıldı:", rel, (ROOT / rel).stat().st_size // 1024, "KB")


if __name__ == "__main__":
    main()
