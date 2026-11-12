#!/usr/bin/env python3
"""Mağaza örnek paketlerinin görsellerini üretir (Pillow gerekir; build-store.py gerektirmez).

Üretilen dosyalar store/src/<id>/ altına yazılır ve depoya girer; bu betik yalnız örnekleri yeniden
çizmek istenince çalıştırılır:
    pip install pillow
    python scripts/make-store-samples.py

Her şey basit geometriden çizilir; hiçbir marka logosu ya da dış görsel kullanılmaz.
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:  # pragma: no cover
    sys.exit("Pillow gerekli: pip install pillow")

SRC = Path(__file__).resolve().parent.parent / "store" / "src"
PREVIEW = (320, 180)


def hex_rgb(value: str) -> tuple[int, int, int]:
    value = value.lstrip("#")
    if len(value) == 8:
        value = value[2:]
    return int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16)


def font(size: int) -> ImageFont.ImageFont:
    for name in ("segoeui.ttf", "arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def save_png(img: Image.Image, path: Path) -> None:
    img.save(path, "PNG", optimize=True)


# ---------------------------------------------------------------- temalar

def theme_preview(theme_id: str) -> None:
    folder = SRC / theme_id
    t = json.loads((folder / "theme.json").read_text(encoding="utf-8"))
    c = {k: hex_rgb(v) for k, v in t.items() if isinstance(v, str) and v.startswith("#")}
    img = Image.new("RGB", PREVIEW, c["KoyBgShell"])
    d = ImageDraw.Draw(img)
    # Pencere
    d.rounded_rectangle((14, 14, 306, 166), 10, fill=c["KoyBgWindow"], outline=c["KoyBorderShell"], width=2)
    # Gezinme sütunu
    d.rectangle((16, 16, 78, 164), fill=c["KoyBgShell"])
    for i in range(5):
        y = 28 + i * 24
        if i == 1:
            d.rounded_rectangle((22, y, 72, y + 16), 5, fill=c["KoyBgNavActive"])
        d.rounded_rectangle((28, y + 5, 28 + 26 - (i % 2) * 6, y + 11), 3, fill=c["KoyTextSecondary"])
    # Başlık ve satırlar
    d.rounded_rectangle((92, 26, 92 + 80, 34), 4, fill=c["KoyTextPrimary"])
    for i in range(3):
        y = 46 + i * 28
        d.rounded_rectangle((92, y, 294, y + 22), 6, fill=c["KoyBgRow"], outline=c["KoyBorderHairline"])
        d.rounded_rectangle((100, y + 8, 100 + 60 + i * 14, y + 14), 3, fill=c["KoyTextSecondary"])
        on = i != 1
        d.rounded_rectangle((262, y + 5, 286, y + 17), 6, fill=c["KoyAccent"] if on else c["KoyToggleTrackOff"])
        kx = 276 if on else 268
        d.ellipse((kx - 4, y + 7, kx + 4, y + 15), fill=c["KoyToggleKnobOn"] if on else c["KoyToggleKnobOff"])
    # Düğme ve durum noktaları
    d.rounded_rectangle((92, 136, 150, 156), 6, fill=c["KoyAccent"])
    d.rounded_rectangle((100, 143, 142, 149), 3, fill=c["KoyOnAccent"])
    for i, key in enumerate(("KoyStatusOk", "KoyStatusWarn", "KoyDangerBorder")):
        d.ellipse((166 + i * 18, 140, 178 + i * 18, 152), fill=c[key])
    save_png(img, folder / "preview.png")


# ---------------------------------------------------------------- simge paketi

ICON_SIZE = 128
ICON_COLOR = (226, 230, 238, 255)
ICON_BG = (60, 66, 82, 255)


def _icon_canvas() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    # 4x büyük çiz, küçült: yumuşak kenar.
    s = ICON_SIZE * 4
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def _finish(img: Image.Image) -> Image.Image:
    return img.resize((ICON_SIZE, ICON_SIZE), Image.LANCZOS)


def _tile(d: ImageDraw.ImageDraw) -> None:
    d.rounded_rectangle((32, 32, 480, 480), 110, fill=ICON_BG)


def icon_ring() -> Image.Image:  # halka ve içinde nokta (Firefox için)
    img, d = _icon_canvas()
    _tile(d)
    d.ellipse((120, 120, 392, 392), outline=ICON_COLOR, width=34)
    d.ellipse((210, 170, 350, 310), fill=ICON_COLOR)
    return _finish(img)


def icon_code() -> Image.Image:  # köşeli ayraçlar (Visual Studio Code için)
    img, d = _icon_canvas()
    _tile(d)
    d.line([(220, 150), (130, 256), (220, 362)], fill=ICON_COLOR, width=34, joint="curve")
    d.line([(292, 150), (382, 256), (292, 362)], fill=ICON_COLOR, width=34, joint="curve")
    return _finish(img)


def icon_folder() -> Image.Image:
    img, d = _icon_canvas()
    _tile(d)
    d.rounded_rectangle((110, 150, 250, 210), 22, fill=ICON_COLOR)
    d.rounded_rectangle((110, 180, 402, 360), 26, fill=ICON_COLOR)
    return _finish(img)


def icon_bars() -> Image.Image:  # daire içinde ses çubukları (Spotify için)
    img, d = _icon_canvas()
    _tile(d)
    d.ellipse((100, 100, 412, 412), outline=ICON_COLOR, width=30)
    for i, h in enumerate((70, 130, 100, 150, 80)):
        x = 168 + i * 42
        d.rounded_rectangle((x, 256 - h // 2, x + 24, 256 + h // 2), 12, fill=ICON_COLOR)
    return _finish(img)


def icon_notepad() -> Image.Image:
    img, d = _icon_canvas()
    _tile(d)
    d.rounded_rectangle((136, 100, 376, 412), 26, outline=ICON_COLOR, width=26)
    for y in (190, 256, 322):
        d.rounded_rectangle((186, y, 326, y + 18), 9, fill=ICON_COLOR)
    return _finish(img)


def icon_calculator() -> Image.Image:
    img, d = _icon_canvas()
    _tile(d)
    d.rounded_rectangle((140, 100, 372, 412), 28, outline=ICON_COLOR, width=26)
    d.rounded_rectangle((176, 136, 336, 196), 12, fill=ICON_COLOR)
    for r in range(3):
        for col in range(3):
            x, y = 180 + col * 54, 226 + r * 54
            d.ellipse((x, y, x + 34, y + 34), fill=ICON_COLOR)
    return _finish(img)


def icon_terminal() -> Image.Image:
    img, d = _icon_canvas()
    _tile(d)
    d.rounded_rectangle((96, 128, 416, 384), 30, outline=ICON_COLOR, width=26)
    d.line([(160, 208), (216, 256), (160, 304)], fill=ICON_COLOR, width=26, joint="curve")
    d.rounded_rectangle((246, 296, 346, 318), 11, fill=ICON_COLOR)
    return _finish(img)


def icon_gear() -> Image.Image:  # daire + dişler
    img, d = _icon_canvas()
    _tile(d)
    cx = cy = 256
    for k in range(8):
        a = math.radians(k * 45)
        x, y = cx + 130 * math.cos(a), cy + 130 * math.sin(a)
        d.ellipse((x - 26, y - 26, x + 26, y + 26), fill=ICON_COLOR)
    d.ellipse((cx - 120, cy - 120, cx + 120, cy + 120), fill=ICON_COLOR)
    d.ellipse((cx - 56, cy - 56, cx + 56, cy + 56), fill=ICON_BG)
    return _finish(img)


# Dosya adı (uzantısız) uygulama anahtarıyla eşleşir (IconPackFiles).
ICONS = {
    "Firefox": icon_ring,
    "Visual Studio Code": icon_code,
    "File Explorer": icon_folder,
    "Spotify": icon_bars,
    "Notepad": icon_notepad,
    "Calculator": icon_calculator,
    "Windows Terminal": icon_terminal,
    "Settings": icon_gear,
}


def iconpack(pack_id: str) -> None:
    folder = SRC / pack_id
    (folder / "icons").mkdir(exist_ok=True)
    tiles = []
    for name, draw in ICONS.items():
        icon = draw()
        save_png(icon, folder / "icons" / f"{name}.png")
        tiles.append(icon)
    img = Image.new("RGB", PREVIEW, (30, 33, 42))
    d = ImageDraw.Draw(img)
    d.text((16, 12), "Mono Icons", fill=(226, 230, 238), font=font(16))
    for i, icon in enumerate(tiles):
        col, row = i % 4, i // 4
        small = icon.resize((56, 56), Image.LANCZOS)
        img.paste(small, (24 + col * 74, 46 + row * 66), small)
    save_png(img, folder / "preview.png")


# ---------------------------------------------------------------- canlı arka plan

BG_W, BG_H = 620, 352
BG_FRAMES = 12
BG_COLORS = 40  # az renk = küçük GIF; yumuşak bantlar koyu zeminde göze batmaz


def aurora(pack_id: str) -> None:
    folder = SRC / pack_id
    base = (10, 22, 34)
    layers = [((20, 150, 130), 0.0), ((30, 110, 170), 2.1), ((110, 60, 160), 4.2)]
    # Düşük çözünürlükte hesapla, büyüt: hızlı ve pürüzsüz.
    gw, gh = BG_W // 4, BG_H // 4
    frames = []
    for f in range(BG_FRAMES):
        phase = 2 * math.pi * f / BG_FRAMES  # son kareden sonra ilk kare gelir: kusursuz döngü
        small = Image.new("RGB", (gw, gh))
        px = small.load()
        for yy in range(gh):
            for xx in range(gw):
                x, y = xx / gw, yy / gh
                r, g, b = base
                for k, (col, off) in enumerate(layers):
                    cx = 0.5 + 0.38 * math.cos(phase + off)
                    cy = 0.45 + 0.28 * math.sin(phase * (1 + (k % 2)) + off)
                    rad = 0.34 + 0.05 * k
                    v = math.exp(-(((x - cx) ** 2 + (y - cy) ** 2) / (rad * rad))) * 0.85
                    r += (col[0] - r) * v
                    g += (col[1] - g) * v
                    b += (col[2] - b) * v
                px[xx, yy] = (int(r), int(g), int(b))
        frames.append(small.resize((BG_W, BG_H), Image.BICUBIC))
    # Bütün kareler için ortak palet: kareler arası renk titremesi olmaz.
    sheet = Image.new("RGB", (BG_W, BG_H * BG_FRAMES))
    for i, fr in enumerate(frames):
        sheet.paste(fr, (0, i * BG_H))
    palette = sheet.quantize(colors=BG_COLORS, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    out = [fr.quantize(palette=palette, dither=Image.Dither.NONE) for fr in frames]
    target = folder / "background.gif"
    out[0].save(target, save_all=True, append_images=out[1:], duration=160, loop=0, optimize=True, disposal=1)
    prev = frames[0].resize(PREVIEW, Image.LANCZOS)
    ImageDraw.Draw(prev).text((14, 140), "Aurora", fill=(235, 242, 248), font=font(20))
    save_png(prev, folder / "preview.png")
    print(f"background.gif: {target.stat().st_size / 1024:.0f} KB")


# ---------------------------------------------------------------- profil

def profile_preview(pack_id: str) -> None:
    folder = SRC / pack_id
    img = Image.new("RGB", PREVIEW, (24, 26, 32))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((70, 20, 250, 52), 16, fill=(10, 10, 12), outline=(60, 64, 76))  # çentik
    d.text((16, 76), "Minimal", fill=(236, 238, 244), font=font(22))
    d.text((16, 108), "Appearance  Focus", fill=(150, 156, 172), font=font(13))
    d.rounded_rectangle((16, 134, 304, 150), 8, fill=(40, 43, 52))
    d.rounded_rectangle((16, 134, 146, 150), 8, fill=(98, 142, 214))
    d.text((16, 156), "50 / 10 / 20 min", fill=(150, 156, 172), font=font(12))
    save_png(img, folder / "preview.png")


def main() -> None:
    for theme_id in ("koyhq.nord-theme", "koyhq.solarized-theme", "koyhq.high-contrast-theme"):
        theme_preview(theme_id)
    iconpack("koyhq.mono-icons")
    aurora("koyhq.aurora-background")
    profile_preview("koyhq.minimal-profile")
    print("tamam")


if __name__ == "__main__":
    main()
