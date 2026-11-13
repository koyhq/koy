#!/usr/bin/env python3
"""Mağaza temaları için palet üreticisi ve önizleme çizici (Pillow gerekir; build-store.py gerektirmez).

Bir temanın ~130 renk anahtarını elle seçmek yerine store/palettes/<id>.json içine 20 kadar temel renk yazılır;
bu betik Koy penceresi, menü, çentik ve dock anahtarlarının hepsini o renklerden türetir:

    python scripts/make-theme.py                      # bütün paletler + bütün tema önizlemeleri
    python scripts/make-theme.py kullanici.ege        # yalnız bu palet (theme.json + preview.png)
    python scripts/make-theme.py --preview koyhq.nord-theme   # elle yazılmış temanın yalnız önizlemesi

Çıktı store/src/<id>/theme.json ve preview.png olur. manifest.json ile store.json'u siz yazarsınız; sonra
python scripts/build-store.py paketi üretir. Palet dosyası pakete girmez.

Palet biçimi (store/palettes/<id>.json):
    {
      "colors": {
        "bg", "shell", "surface", "raised", "active", "border",        yüzeyler, koyudan açığa
        "text", "text2", "muted", "disabled",                          yazı, parlaktan sönüğe
        "accent", "ok", "warn", "danger",                              vurgu ve durum
        "red", "yellow", "purple", "green", "blue", "cyan",            çentik sekme renkleri
        "notch" (isteğe bağlı, varsayılan shell), "dock" (isteğe bağlı, varsayılan shell)
      },
      "tint": 0.06,          çentik kartlarının sekme rengine çalma oranı (0 = düz nötr kart)
      "tokens": { }          türetilen değerin üstüne yazılan anahtarlar (yazı tipi, köşe, tek renk düzeltmesi…)
    }

Okunurluk: yazı/zemin karşıtlığı WCAG oranıyla denetlenir; ana metin 4.5'in altındaysa betik hata verir.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:  # pragma: no cover
    sys.exit("Pillow gerekli: pip install pillow")

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "store" / "src"
PALETTES = ROOT / "store" / "palettes"
PREVIEW = (320, 180)
SCALE = 3  # kenar yumuşatma: büyük çiz, küçült

REQUIRED = (
    "bg", "shell", "surface", "raised", "active", "border",
    "text", "text2", "muted", "disabled",
    "accent", "ok", "warn", "danger",
    "red", "yellow", "purple", "green", "blue", "cyan",
)

RGB = tuple[int, int, int]


# ---------------------------------------------------------------- renk yardımcıları

def parse(value: str) -> RGB:
    v = value.strip().lstrip("#")
    if len(v) == 8:
        v = v[2:]
    if len(v) != 6:
        raise ValueError(f"renk #RRGGBB olmalı: {value!r}")
    return int(v[0:2], 16), int(v[2:4], 16), int(v[4:6], 16)


def alpha_of(value: str) -> int:
    v = value.lstrip("#")
    return int(v[0:2], 16) if len(v) == 8 else 255


def hx(c: RGB, alpha: int | None = None) -> str:
    body = "".join(f"{x:02X}" for x in c)
    return f"#{alpha:02X}{body}" if alpha is not None else f"#{body}"


def mix(a: RGB, b: RGB, t: float) -> RGB:
    """a'dan b'ye t oranında karışım (0 = a, 1 = b)."""
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))  # type: ignore[return-value]


def luminance(c: RGB) -> float:
    def ch(x: int) -> float:
        s = x / 255
        return s / 12.92 if s <= 0.03928 else ((s + 0.055) / 1.055) ** 2.4
    r, g, b = (ch(x) for x in c)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a: RGB, b: RGB) -> float:
    la, lb = sorted((luminance(a), luminance(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


# ---------------------------------------------------------------- türetme

def derive(palette: dict) -> dict[str, str]:
    colors = palette.get("colors", {})
    missing = [k for k in REQUIRED if k not in colors]
    if missing:
        raise ValueError(f"palette eksik renk: {', '.join(missing)}")
    p = {k: parse(v) for k, v in colors.items()}
    tint = float(palette.get("tint", 0.06))
    if not 0 <= tint <= 0.5:
        raise ValueError("tint 0 ile 0.5 arasında olmalı")

    bg, shell, surface, raised, active, border = (p[k] for k in ("bg", "shell", "surface", "raised", "active", "border"))
    text, text2, muted, disabled = (p[k] for k in ("text", "text2", "muted", "disabled"))
    accent, ok, warn, danger = (p[k] for k in ("accent", "ok", "warn", "danger"))
    notch = p.get("notch", shell)
    dock = p.get("dock", shell)
    darkest = min((bg, shell, notch), key=luminance)

    # Vurgu üstündeki yazı: hangisi daha okunursa (koyu zemin ya da beyaz).
    on_accent = darkest if contrast(accent, darkest) >= contrast(accent, (255, 255, 255)) else (255, 255, 255)
    accent_text = accent if contrast(accent, bg) >= 4.5 else mix(accent, text, 0.35)

    def soft(c: RGB) -> RGB:
        return mix(c, text, 0.45)

    def card(c: RGB) -> RGB:
        return mix(surface, c, tint)

    def chip(c: RGB) -> RGB:
        return mix(raised, c, tint * 2)

    def track(c: RGB) -> RGB:
        return mix(surface, c, 0.28)

    focus, shelf, clip, system, control, calendar = p["red"], p["yellow"], p["purple"], p["green"], p["blue"], p["cyan"]

    t: dict[str, str] = {
        # Koy penceresi
        "KoyBgWindow": hx(bg),
        "KoyBgShell": hx(shell),
        "KoyBgRow": hx(surface),
        "KoyBgControl": hx(surface),
        "KoyBgSegment": hx(raised),
        "KoyBgHover": hx(raised),
        "KoyBgActive": hx(active),
        "KoyBgNavHover": hx(raised),
        "KoyBgNavActive": hx(active),
        "KoyBorderHairline": hx(border),
        "KoyBorderShell": hx(raised),
        "KoyBorderStrong": hx(active),
        "KoyBorderControl": hx(mix(active, muted, 0.5)),
        "KoyTextPrimary": hx(text),
        "KoyTextSecondary": hx(text2),
        "KoyTextMuted": hx(muted),
        "KoyTextDisabled": hx(disabled),
        "KoyAccent": hx(accent),
        "KoyAccentHover": hx(mix(accent, text, 0.2)),
        "KoyAccentPressed": hx(mix(accent, bg, 0.2)),
        "KoyOnAccent": hx(on_accent),
        "KoyAccentText": hx(accent_text),
        "KoyFocusRing": hx(accent),
        "KoyAccentSubtle": hx(accent, 0x33),
        "KoyBgInfo": hx(mix(bg, control, 0.18)),
        "KoyFgInfo": hx(soft(control)),
        "KoyDangerText": hx(mix(danger, text, 0.25)),
        "KoyDangerBorder": hx(mix(bg, danger, 0.55)),
        "KoyDangerBg": hx(mix(bg, danger, 0.15)),
        "KoyStatusOk": hx(ok),
        "KoyStatusWarn": hx(warn),
        "KoyToggleTrackOff": hx(raised),
        "KoyToggleKnobOff": hx(muted),
        "KoyToggleKnobOn": hx(on_accent),
        # Bağlam menüsü
        "KoyMenuBg": hx(shell),
        "KoyMenuBorder": hx(raised),
        "KoyMenuText": hx(text),
        "KoyMenuTextMuted": hx(muted),
        "KoyMenuHover": hx(raised),
        "KoyMenuSeparator": hx(surface),
        # Çentik
        "KoyNotchSurface": hx(notch),
        "KoyNotchText": hx(text),
        "KoyNotchTextSecondary": hx(text2),
        "KoyNotchTextMuted": hx(muted),
        "KoyNotchTextOnLight": hx(notch),
        "KoyNotchHover": hx(text, 0x1A),
        "KoyNotchPressed": hx(text, 0x33),
        "KoyNotchActive": hx(text, 0x26),
        "KoyNotchDivider": hx(text, 0x20),
        "KoyNotchCard": hx(surface),
        "KoyNotchChip": hx(raised),
        "KoyNotchMuted": hx(muted),
        "KoyNotchDim": hx(mix(muted, disabled, 0.5)),
        "KoyNotchOk": hx(ok),
        "KoyNotchAccentHome": hx(text),
        "KoyNotchAccentFocus": hx(focus),
        "KoyNotchAccentShelf": hx(shelf),
        "KoyNotchAccentClipboard": hx(clip),
        "KoyNotchAccentSystem": hx(system),
        "KoyNotchAccentControl": hx(control),
        "KoyNotchAccentCalendar": hx(calendar),
        "KoyNotchMusicCard": hx(surface),
        "KoyNotchMusicCover": hx(raised),
        "KoyNotchMusicSub": hx(text, 0x9E),
        "KoyNotchMusicSpectrum": hx(text, 0x99),
        "KoyNotchWeatherCard": hx(card(calendar)),
        "KoyNotchWeatherSoft": hx(soft(calendar)),
        "KoyNotchWeatherIconBg": hx(calendar, 0x22),
        "KoyNotchFocusCard": hx(card(focus)),
        "KoyNotchFocusTrack": hx(track(focus)),
        "KoyNotchFocusButton": hx(chip(focus)),
        "KoyNotchFocusSoft": hx(soft(focus)),
        "KoyNotchShelfCard": hx(card(shelf)),
        "KoyNotchShelfChip": hx(chip(shelf)),
        "KoyNotchShelfChipText": hx(soft(shelf)),
        "KoyNotchShelfSoft": hx(soft(shelf)),
        "KoyNotchShelfDash": hx(mix(surface, shelf, 0.4)),
        "KoyNotchControlCard": hx(card(control)),
        "KoyNotchControlTrack": hx(track(control)),
        "KoyNotchControlSoft": hx(soft(control)),
        "KoyNotchControlTile": hx(raised),
        "KoyNotchBrightness": hx(shelf),
        "KoyNotchControlBattery": hx(soft(control)),
        "KoyNotchControlIconBg": hx(control, 0x38),
        "KoyNotchRowHover": hx(mix(surface, text, 0.08)),
        "KoyNotchRowError": hx(danger),
        "KoyNotchCalendarCard": hx(card(calendar)),
        "KoyNotchCalendarChip": hx(chip(calendar)),
        "KoyNotchCalendarSoft": hx(soft(calendar)),
        "KoyNotchClipCard": hx(card(clip)),
        "KoyNotchClipChip": hx(chip(clip)),
        "KoyNotchClipChipText": hx(soft(clip)),
        "KoyNotchClipSoft": hx(soft(clip)),
        "KoyNotchSystemCard": hx(card(system)),
        "KoyNotchSystemTrack": hx(track(system)),
        "KoyNotchSystemSoft": hx(soft(system)),
        "KoyNotchNetCard": hx(card(clip)),
        "KoyNotchNetSoft": hx(soft(clip)),
        "KoyNotchDash": hx(active),
        "KoyNotchCheckBorder": hx(disabled),
        "KoyNotchDoneText": hx(mix(muted, disabled, 0.5)),
        "KoyNotchTrack": hx(active),
        "KoyNotchIconBg": hx(raised),
        # Dock
        "KoyDockSurface": hx(dock),
        "KoyDockCapsuleSurface": hx(bg),
        "KoyDockBorder": hx(raised),
        "KoyDockCapsuleBorder": hx(active),
        "KoyDockSeparator": hx(active),
        "KoyDockDivider": hx(active),
        "KoyDockHover": hx(raised),
        "KoyDockActive": hx(surface),
        "KoyDockPill": hx(surface),
        "KoyDockCover": hx(raised),
        "KoyDockIndicator": hx(accent),
        "KoyDockText": hx(text),
        "KoyDockTextSecondary": hx(muted),
        "KoyDockGlyph": hx(text2),
        "KoyDockClockText": hx(text),
        "KoyDockClockSubtext": hx(muted),
        "KoyDockPreviewSurface": hx(dock, 0xF5),
        "KoyDockPreviewText": hx(text2),
    }
    overrides = palette.get("tokens", {})
    if not isinstance(overrides, dict):
        raise ValueError("tokens bir nesne olmalı")
    t.update(overrides)
    return t


# ---------------------------------------------------------------- okunurluk

CHECKS = (
    # (yazı, zemin, en az, ad)
    ("KoyTextPrimary", "KoyBgWindow", 4.5, "pencere metni"),
    ("KoyTextSecondary", "KoyBgWindow", 4.5, "pencere ikincil metni"),
    ("KoyTextPrimary", "KoyBgRow", 4.5, "satır metni"),
    ("KoyAccentText", "KoyBgWindow", 4.5, "vurgu metni"),
    ("KoyOnAccent", "KoyAccent", 4.5, "vurgu düğmesi metni"),
    ("KoyMenuText", "KoyMenuBg", 4.5, "menü metni"),
    ("KoyNotchText", "KoyNotchSurface", 4.5, "çentik metni"),
    ("KoyNotchText", "KoyNotchCard", 4.5, "çentik kartı metni"),
    ("KoyDockText", "KoyDockSurface", 4.5, "dock metni"),
    ("KoyNotchSurface", "KoyNotchAccentControl", 4.5, "Kontrol döşemesi metni (açık)"),
)
SOFT_CHECKS = (
    ("KoyTextMuted", "KoyBgWindow", 3.0, "soluk metin"),
    ("KoyNotchTextMuted", "KoyNotchSurface", 3.0, "çentik soluk metni"),
    ("KoyDockClockSubtext", "KoyDockSurface", 3.0, "dock saat alt yazısı"),
)


def check_contrast(theme_id: str, t: dict[str, str]) -> list[str]:
    errors: list[str] = []
    for checks, hard in ((CHECKS, True), (SOFT_CHECKS, False)):
        for fg, bg, need, name in checks:
            if fg not in t or bg not in t:
                continue
            ratio = contrast(parse(t[fg]), parse(t[bg]))
            if ratio < need:
                line = f"{theme_id}: {name} karşıtlığı {ratio:.2f} (en az {need})"
                if hard:
                    errors.append(line)
                else:
                    print(f"  uyarı: {line}")
    return errors


# ---------------------------------------------------------------- önizleme

def draw_preview(t: dict[str, str], out: Path) -> None:
    """Mağaza kartı (320×180): üstte açık çentik, solda Koy penceresi, sağda bağlam menüsü, altta dock."""
    def c(key: str, fallback: str = "#808080") -> tuple[int, int, int, int]:
        v = t.get(key, fallback)
        return (*parse(v), alpha_of(v))

    s = SCALE
    w, h = PREVIEW[0] * s, PREVIEW[1] * s
    img = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(img, "RGBA")

    def box(x0, y0, x1, y1, r, fill, outline=None, width=1):
        d.rounded_rectangle((x0 * s, y0 * s, x1 * s, y1 * s), r * s, fill=fill, outline=outline, width=width * s)

    def dot(x, y, r, fill):
        d.ellipse(((x - r) * s, (y - r) * s, (x + r) * s, (y + r) * s), fill=fill)

    # Masaüstü zemini: üstte vurguya çalan, altta koyulaşan geçiş; çentik ve dock zeminden ayrılsın diye.
    top = mix(parse(t["KoyBgWindow"]), parse(t["KoyAccent"]), 0.28)
    bottom = mix(parse(t["KoyBgShell"]), (0, 0, 0), 0.35)
    for y in range(h):
        d.line((0, y, w, y), fill=(*mix(top, bottom, y / h), 255))

    # Koy penceresi
    box(12, 62, 196, 150, 8, c("KoyBgWindow"), c("KoyBorderShell"))
    d.rounded_rectangle((13 * s, 63 * s, 58 * s, 149 * s), 7 * s, fill=c("KoyBgShell"))
    d.rectangle((50 * s, 63 * s, 58 * s, 149 * s), fill=c("KoyBgShell"))
    for i in range(5):
        y = 72 + i * 15
        if i == 1:
            box(17, y - 2, 54, y + 9, 3, c("KoyBgNavActive"))
        box(21, y + 2, 21 + 24 - (i % 2) * 6, y + 5, 2, c("KoyTextSecondary") if i != 1 else c("KoyTextPrimary"))
    box(66, 70, 66 + 58, 75, 2, c("KoyTextPrimary"))
    for i in range(3):
        y = 82 + i * 18
        box(66, y, 188, y + 14, 4, c("KoyBgRow"), c("KoyBorderHairline"))
        box(72, y + 5, 72 + 44 + i * 10, y + 9, 2, c("KoyTextSecondary"))
        on = i != 1
        box(166, y + 3, 182, y + 11, 4, c("KoyAccent") if on else c("KoyToggleTrackOff"))
        kx = 178 if on else 170
        dot(kx, y + 7, 2.6, c("KoyToggleKnobOn") if on else c("KoyToggleKnobOff"))
    box(66, 136, 104, 146, 4, c("KoyAccent"))
    box(72, 140, 98, 142, 1, c("KoyOnAccent"))
    for i, key in enumerate(("KoyStatusOk", "KoyStatusWarn", "KoyDangerBorder")):
        dot(116 + i * 10, 141, 3, c(key))

    # Bağlam menüsü
    box(206, 70, 306, 140, 7, c("KoyMenuBg"), c("KoyMenuBorder"))
    for i in range(4):
        y = 77 + i * 15
        if i == 1:
            box(210, y - 2, 302, y + 10, 4, c("KoyMenuHover"))
        dot(218, y + 4, 2.5, c("KoyMenuTextMuted"))
        box(226, y + 2, 226 + 50 - (i % 3) * 10, y + 5, 2, c("KoyMenuText"))
    d.line((212 * s, 136 * s - s, 300 * s, 136 * s - s), fill=c("KoyMenuSeparator"), width=s)

    # Açık çentik (ekranın üst kenarından iner)
    box(70, -14, 250, 54, 14, c("KoyNotchSurface"))
    tabs = ("KoyNotchAccentHome", "KoyNotchAccentFocus", "KoyNotchAccentCalendar", "KoyNotchAccentShelf",
            "KoyNotchAccentClipboard", "KoyNotchAccentSystem", "KoyNotchAccentControl")
    for i, key in enumerate(tabs):
        x = 86 + i * 13
        if i == 0:
            box(x - 5, 3, x + 5, 11, 4, c("KoyNotchActive"))
        dot(x, 7, 2.4, c(key))
    box(196, 5, 238, 9, 2, c("KoyNotchTextMuted"))
    # Müzik kartı
    box(78, 16, 158, 48, 6, c("KoyNotchMusicCard"))
    box(83, 21, 105, 43, 4, c("KoyNotchMusicCover"))
    box(110, 24, 150, 28, 2, c("KoyNotchText"))
    box(110, 32, 138, 35, 1.5, c("KoyNotchMusicSub"))
    for i in range(8):
        bh = (3, 6, 4, 8, 5, 7, 3, 5)[i]
        box(110 + i * 4, 44 - bh, 112 + i * 4, 44, 1, c("KoyNotchMusicSpectrum"))
    # Odak kartı
    box(162, 16, 242, 48, 6, c("KoyNotchFocusCard"))
    box(168, 22, 196, 26, 2, c("KoyNotchFocusSoft"))
    box(168, 31, 236, 35, 2, c("KoyNotchFocusTrack"))
    box(168, 31, 206, 35, 2, c("KoyNotchAccentFocus"))
    box(168, 39, 186, 44, 2.5, c("KoyNotchFocusButton"))

    # Dock
    box(66, 156, 254, 176, 9, c("KoyDockSurface"), c("KoyDockBorder"))
    icons = ("KoyNotchAccentControl", "KoyNotchAccentFocus", "KoyNotchAccentSystem", "KoyNotchAccentShelf",
             "KoyNotchAccentClipboard", "KoyNotchAccentCalendar")
    for i, key in enumerate(icons):
        x = 78 + i * 18
        if i == 0:
            box(x - 7, 159, x + 7, 173, 4, c("KoyDockHover"))
        box(x - 5, 161, x + 5, 171, 3, c(key))
        if i in (0, 2):
            box(x - 2, 173.5, x + 2, 175, 0.75, c("KoyDockIndicator"))
    d.line((188 * s, 160 * s, 188 * s, 172 * s), fill=c("KoyDockSeparator"), width=s)
    box(200, 161, 240, 165, 2, c("KoyDockClockText"))
    box(206, 168, 234, 171, 1.5, c("KoyDockClockSubtext"))

    img = img.resize(PREVIEW, Image.LANCZOS).convert("RGB")
    img.save(out, "PNG", optimize=True)


# ---------------------------------------------------------------- giriş

def build(theme_id: str) -> list[str]:
    path = PALETTES / f"{theme_id}.json"
    palette = json.loads(path.read_text(encoding="utf-8"))
    theme = derive(palette)
    errors = check_contrast(theme_id, theme)
    if errors:
        return errors
    folder = SRC / theme_id
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "theme.json").write_text(json.dumps(theme, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    draw_preview(theme, folder / "preview.png")
    print(f"{theme_id}: {len(theme)} anahtar, önizleme çizildi")
    return []


def preview_only(theme_id: str) -> list[str]:
    folder = SRC / theme_id
    theme = json.loads((folder / "theme.json").read_text(encoding="utf-8"))
    errors = check_contrast(theme_id, theme)
    draw_preview(theme, folder / "preview.png")
    print(f"{theme_id}: önizleme çizildi")
    return errors


def theme_packages() -> list[str]:
    ids = []
    for folder in sorted(p for p in SRC.iterdir() if p.is_dir()):
        manifest = folder / "manifest.json"
        if manifest.exists() and json.loads(manifest.read_text(encoding="utf-8")).get("kind") == "theme":
            ids.append(folder.name)
    return ids


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):  # Windows konsolunda Türkçe karakterler
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("ids", nargs="*", help="palet kimlikleri (boşsa hepsi)")
    ap.add_argument("--preview", action="store_true", help="paletten üretme, yalnız mevcut theme.json'dan önizleme çiz")
    args = ap.parse_args()

    errors: list[str] = []
    if args.preview:
        for theme_id in args.ids or theme_packages():
            errors += preview_only(theme_id)
    elif args.ids:
        for theme_id in args.ids:
            errors += build(theme_id)
    else:
        palette_ids = sorted(p.stem for p in PALETTES.glob("*.json"))
        for theme_id in palette_ids:
            errors += build(theme_id)
        for theme_id in theme_packages():
            if theme_id not in palette_ids:
                errors += preview_only(theme_id)

    for e in errors:
        print(f"HATA: {e}", file=sys.stderr)
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
