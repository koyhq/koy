#!/usr/bin/env python3
"""Koy eklenti mağazası derleyicisi (yalnız standart kütüphane).

    python scripts/build-store.py            # paketleri ve store/index.json'u üretir
    python scripts/build-store.py --check    # üretmeden doğrular; sorun varsa çıkış kodu 1

store/src/<id>/ klasörü bir paketin kaynağıdır:
    manifest.json, türe özgü içerik (theme.json / icons/ / background.* / profile.json), isteğe bağlı preview.png
    store.json  yalnız indekse girer (summary, description, homepage, updated, downloads), pakete girmez.

Biçim ve kurallar: docs/EXTENSION-STORE.md. Uygulama tarafındaki doğrulamanın (zip slip, izinli dosyalar,
boyut sınırları) aynısı burada da uygulanır; böylece kötü paket PR aşamasında yakalanır.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STORE = ROOT / "store"
SRC = STORE / "src"
PACKAGES = STORE / "packages"
PREVIEWS = STORE / "previews"
INDEX = STORE / "index.json"
BASE_URL = "https://raw.githubusercontent.com/koyhq/koy/main/store/"

# --- belgedeki sınırlar (docs/EXTENSION-STORE.md) ---
MAX_DOWNLOAD = 25 * 1024 * 1024
MAX_UNPACKED = 50 * 1024 * 1024
MAX_FILES = 512
MAX_INDEX_BYTES = 1024 * 1024
MAX_ITEMS = 500
MAX_SUMMARY = 200
MAX_DESCRIPTION = 2000
MAX_PREVIEW = 1024 * 1024
MAX_THEME_BYTES = 64 * 1024      # UserStyleParser.MaxBytes
MAX_THEME_ENTRIES = 512          # UserStyleParser.MaxEntries
MAX_PROFILE_CHARS = 1_000_000    # SettingsProfileCodec.MaxLength
MAX_BACKGROUND_BYTES = 64 * 1024 * 1024  # LiveBackgroundLimits.MaxFileSizeBytes

FORMAT_NAME = "koy-store-index"
FORMAT_VERSION = 1
KINDS = ("theme", "iconpack", "background", "profile")
# Nokta: "koyhq.nord-theme" gibi ad alanlı kimlikler (belgedeki örnekler). Noktalar tek ve arada olmalı.
ID_RE = re.compile(r"^[a-z][a-z0-9-]*(\.[a-z0-9][a-z0-9-]*)*$")
VERSION_RE = re.compile(r"^(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})$")
DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
RESERVED = {"con", "prn", "aux", "nul"} | {f"com{i}" for i in range(1, 10)} | {f"lpt{i}" for i in range(1, 10)}
# Windows'ta aygıt adı olan dosya adı kökleri (uzantı yok sayılır).
RESERVED_STEMS = {r.upper() for r in RESERVED} | {"CONIN$", "CONOUT$"}
SAFE_NAME_RE = re.compile(r'^[^\x00-\x1f<>"|?*:\\/]+$')

ICON_EXTENSIONS = (".png", ".ico", ".svg")
BACKGROUND_EXTENSIONS = (".gif", ".webp", ".mp4", ".webm")
FIXED_ENTRIES = {"manifest.json", "preview.png"}
ZIP_DATE = (1980, 1, 1, 0, 0, 0)
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"

CONTENT_FILE = {"theme": "theme.json", "profile": "profile.json"}


class StoreError(Exception):
    pass


# ------------------------------------------------------------------ doğrulama

def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def plain_text_ok(value: object, max_len: int) -> bool:
    """ExtensionManifestValidator.IsValidText ile aynı: boş değil, kenar boşluğu yok, denetim/biçim karakteri yok."""
    if not isinstance(value, str) or not value or len(value) > max_len:
        return False
    if value[0].isspace() or value[-1].isspace():
        return False
    import unicodedata
    return all(unicodedata.category(ch) not in ("Cc", "Cf", "Zl", "Zp", "Co", "Cn", "Cs") for ch in value)


def valid_id(mid: object) -> bool:
    """ExtensionManifestValidator.IsValidId ile aynı: her nokta-ayrılmış parça için aygıt adı (con, nul, com1…) yasak."""
    if not isinstance(mid, str) or not ID_RE.match(mid) or mid.endswith("-") or not 2 <= len(mid) <= 64:
        return False
    return not any(part in RESERVED for part in mid.split("."))


def validate_manifest(m: object, expected_id: str | None) -> list[str]:
    errs: list[str] = []
    if not isinstance(m, dict):
        return ["manifest.json bir nesne olmalı"]
    mid = m.get("id")
    if not valid_id(mid):
        errs.append(f"geçersiz id: {mid!r}")
    elif expected_id is not None and mid != expected_id:
        errs.append(f"manifest id ({mid}) klasör adıyla ({expected_id}) aynı değil")
    if not plain_text_ok(m.get("name"), 80):
        errs.append("geçersiz name (boş, 80 karakterden uzun ya da kenarında boşluk/denetim karakteri var)")
    if not isinstance(m.get("version"), str) or not VERSION_RE.match(m["version"]):
        errs.append(f"geçersiz version (major.minor.patch bekleniyor): {m.get('version')!r}")
    if not plain_text_ok(m.get("author"), 80):
        errs.append("geçersiz author")
    if m.get("kind") not in KINDS:
        errs.append(f"geçersiz kind: {m.get('kind')!r} (izinli: {', '.join(KINDS)})")
    lic = m.get("license")
    if not isinstance(lic, str) or not lic.strip() or len(lic) > 64 or re.search(r"\s", lic):
        errs.append("license (SPDX kimliği) zorunlu, boşluksuz ve en çok 64 karakter olmalı")
    caps = m.get("capabilities", [])
    if not isinstance(caps, list):
        errs.append("capabilities liste olmalı")
    elif caps:
        errs.append("capabilities boş olmalı: veri paketleri yetki isteyemez, yetki isteyen paket reddedilir")
    if m.get("entryKind") != "data":
        errs.append('entryKind "data" olmalı')
    return errs


def validate_entry_name(name: str) -> str | None:
    """Zip slip ve Windows'a özgü tuzaklar. Dönüş: hata iletisi ya da None."""
    if not name or name.startswith("/") or "\\" in name or ":" in name:
        return "mutlak yol, ters bölü ya da ':' (sürücü harfi / alternatif veri akışı)"
    parts = name.split("/")
    for part in parts:
        if part in ("", ".", ".."):
            return "boş ya da nokta bileşeni (.., ./, //)"
        if part.endswith((".", " ")):
            return "sonu nokta ya da boşluk olan bileşen"
        if not SAFE_NAME_RE.match(part):
            return "yasak karakter"
        if part.split(".")[0].rstrip(" ").upper() in RESERVED_STEMS:
            return "ayrılmış aygıt adı"
    return None


def allowed_entry(kind: str, name: str) -> bool:
    if name in FIXED_ENTRIES:
        return True
    if kind == "theme":
        return name == "theme.json"
    if kind == "profile":
        return name == "profile.json"
    if kind == "iconpack":
        parts = name.split("/")
        return len(parts) == 2 and parts[0] == "icons" and parts[1].lower().endswith(ICON_EXTENSIONS) \
            and parts[1].rsplit(".", 1)[0] != ""
    if kind == "background":
        return "/" not in name and name.startswith("background.") and name.lower().endswith(BACKGROUND_EXTENSIONS) \
            and name == name.lower()
    return False


def png_size(data: bytes) -> tuple[int, int] | None:
    if len(data) < 24 or data[:8] != PNG_SIGNATURE or data[12:16] != b"IHDR":
        return None
    return struct.unpack(">II", data[16:24])


def validate_content(kind: str, files: dict[str, bytes]) -> list[str]:
    errs: list[str] = []
    if kind == "theme":
        raw = files.get("theme.json")
        if raw is None:
            return ["theme.json eksik"]
        if len(raw) > MAX_THEME_BYTES:
            errs.append(f"theme.json {MAX_THEME_BYTES // 1024} KB'dan büyük")
        try:
            data = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, ValueError):
            return errs + ["theme.json geçerli JSON değil"]
        if not isinstance(data, dict) or not data:
            return errs + ["theme.json boş olmayan bir nesne olmalı"]
        if len(data) > MAX_THEME_ENTRIES:
            errs.append(f"theme.json {MAX_THEME_ENTRIES} girdiden fazla")
        for key, value in data.items():
            # İzinli anahtar kümesinin tamamı uygulamada (AllowedTokens); burada yalnız belirgin hatalar.
            if not key.startswith("Koy"):
                errs.append(f"theme.json: {key!r} bir Koy kaynak anahtarı değil")
            if isinstance(value, str):
                if len(value) > 128:
                    errs.append(f"theme.json: {key} değeri 128 karakterden uzun")
            elif not isinstance(value, (int, float)) or isinstance(value, bool):
                errs.append(f"theme.json: {key} değeri metin ya da sayı olmalı")
    elif kind == "profile":
        raw = files.get("profile.json")
        if raw is None:
            return ["profile.json eksik"]
        try:
            text = raw.decode("utf-8-sig")
            data = json.loads(text)
        except (UnicodeDecodeError, ValueError):
            return ["profile.json geçerli JSON değil"]
        if len(text) > MAX_PROFILE_CHARS:
            errs.append("profile.json çok büyük")
        sections = {"Appearance", "Dock", "Notch", "Music", "Shortcuts", "Focus"}
        if not isinstance(data, dict) or data.get("Format") != "koy-profile":
            errs.append('profile.json: Format "koy-profile" olmalı')
        elif data.get("FormatVersion") != 1:
            errs.append("profile.json: FormatVersion 1 olmalı")
        else:
            name = data.get("Name")
            if not isinstance(name, str) or not name.strip() or len(name.strip()) > 60:
                errs.append("profile.json: Name boş olmamalı, en çok 60 karakter")
            declared = data.get("Sections")
            if not isinstance(declared, list) or not declared or not all(s in sections for s in declared):
                errs.append("profile.json: Sections bilinen bölümlerden oluşan boş olmayan liste olmalı")
            elif not isinstance(data.get("Settings"), dict):
                errs.append("profile.json: Settings nesne olmalı")
            else:
                for s in declared:
                    if not isinstance(data["Settings"].get(s), dict):
                        errs.append(f"profile.json: Settings.{s} eksik")
                    if s in ("Appearance", "Notch"):
                        pass
                machine = [("Appearance", "IconPackFolder"), ("Appearance", "AppIconOverrides"),
                           ("Notch", "LiveBackgroundFilePath"), ("Dock", "StartIconPath")]
                for sec, key in machine:
                    if isinstance(data["Settings"].get(sec), dict) and data["Settings"][sec].get(key):
                        errs.append(f"profile.json: {sec}.{key} makineye özgü, profile yazılmaz")
    elif kind == "iconpack":
        icons = [n for n in files if n.startswith("icons/")]
        if not icons:
            errs.append("icons/ altında en az bir simge olmalı")
        for n in icons:
            if n.lower().endswith(".png") and png_size(files[n]) is None:
                errs.append(f"{n}: geçerli PNG değil")
    elif kind == "background":
        bg = [n for n in files if n.startswith("background.")]
        if len(bg) != 1:
            errs.append("tam olarak bir background.<gif|webp|mp4|webm> olmalı")
        for n in bg:
            if len(files[n]) > MAX_BACKGROUND_BYTES:
                errs.append(f"{n} {MAX_BACKGROUND_BYTES // (1024 * 1024)} MB'dan büyük")
            if n.endswith(".gif") and files[n][:6] not in (b"GIF87a", b"GIF89a"):
                errs.append(f"{n}: geçerli GIF değil")
    if "preview.png" in files:
        size = png_size(files["preview.png"])
        if size is None:
            errs.append("preview.png geçerli PNG değil")
        elif len(files["preview.png"]) > MAX_PREVIEW:
            errs.append("preview.png 1 MB'dan büyük")
    return errs


def validate_files(kind: str, files: dict[str, bytes]) -> list[str]:
    """Paketin (ya da kaynak klasörün) dosya kümesi için ortak denetim."""
    errs: list[str] = []
    if len(files) > MAX_FILES:
        errs.append(f"{MAX_FILES} dosyadan fazla")
    if sum(len(b) for b in files.values()) > MAX_UNPACKED:
        errs.append("açılmış toplam boyut 50 MB'ı aşıyor")
    seen_lower: set[str] = set()
    for name in files:
        why = validate_entry_name(name)
        if why:
            errs.append(f"{name!r}: {why}")
            continue
        if name.lower() in seen_lower:
            errs.append(f"{name!r}: büyük/küçük harf farkıyla yinelenen dosya")
        seen_lower.add(name.lower())
        if not allowed_entry(kind, name):
            errs.append(f"{name!r}: bu tür ({kind}) için izinli bir dosya değil")
    return errs + validate_content(kind, files)


# ------------------------------------------------------------------ kaynak okuma

def read_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as ex:
        raise StoreError(f"{path.relative_to(ROOT)}: okunamadı ya da geçersiz JSON ({ex})")


def collect_source(folder: Path) -> dict[str, bytes]:
    """Pakete girecek dosyalar (store.json hariç), sıralı ve '/' ayraçlı adlarla."""
    files: dict[str, bytes] = {}
    for p in sorted(folder.rglob("*")):
        if p.is_symlink():
            raise StoreError(f"{p.relative_to(ROOT)}: sembolik bağ yasak")
        if not p.is_file():
            continue
        rel = p.relative_to(folder).as_posix()
        if rel == "store.json":
            continue
        files[rel] = p.read_bytes()
    return files


def load_store_meta(folder: Path, mid: str) -> tuple[dict, list[str]]:
    errs: list[str] = []
    path = folder / "store.json"
    if not path.is_file():
        return {}, [f"{mid}: store.json eksik"]
    meta = read_json(path)
    if not isinstance(meta, dict):
        return {}, [f"{mid}: store.json nesne olmalı"]
    summary, desc = meta.get("summary"), meta.get("description")
    if not isinstance(summary, str) or not summary.strip() or len(summary) > MAX_SUMMARY:
        errs.append(f"{mid}: summary zorunlu, en çok {MAX_SUMMARY} karakter")
    if not isinstance(desc, str) or not desc.strip() or len(desc) > MAX_DESCRIPTION:
        errs.append(f"{mid}: description zorunlu, en çok {MAX_DESCRIPTION} karakter")
    home = meta.get("homepage")
    if not isinstance(home, str) or not home.startswith("https://"):
        errs.append(f"{mid}: homepage https adresi olmalı")
    upd = meta.get("updated")
    if not isinstance(upd, str) or not DATE_RE.match(upd):
        errs.append(f"{mid}: updated YYYY-MM-DD olmalı")
    dl = meta.get("downloads", 0)
    if not isinstance(dl, int) or isinstance(dl, bool) or dl < 0:
        errs.append(f"{mid}: downloads negatif olmayan tam sayı olmalı")
    return meta, errs


# ------------------------------------------------------------------ zip

def make_zip(files: dict[str, bytes]) -> bytes:
    """Deterministik zip: sıralı adlar, sabit tarih ve öznitelikler."""
    import io
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for name in sorted(files):
            info = zipfile.ZipInfo(name, ZIP_DATE)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = 0o644 << 16
            z.writestr(info, files[name], zipfile.ZIP_DEFLATED, 9)
    return buf.getvalue()


def read_zip(data: bytes) -> tuple[dict[str, bytes] | None, list[str]]:
    """Paketi güvenle okur (açma sınırları önce üstveriden denetlenir). Dosya sistemine bir şey yazmaz."""
    import io
    errs: list[str] = []
    if len(data) > MAX_DOWNLOAD:
        return None, ["paket 25 MB'dan büyük"]
    try:
        z = zipfile.ZipFile(io.BytesIO(data))
    except zipfile.BadZipFile:
        return None, ["geçerli bir zip değil"]
    with z:
        infos = [i for i in z.infolist() if not i.is_dir()]
        if len(infos) > MAX_FILES:
            return None, [f"{MAX_FILES} dosyadan fazla"]
        if sum(i.file_size for i in infos) > MAX_UNPACKED:
            return None, ["açılmış toplam boyut 50 MB'ı aşıyor"]
        files: dict[str, bytes] = {}
        for i in infos:
            if i.filename in files:
                errs.append(f"{i.filename!r}: yinelenen girdi")
                continue
            files[i.filename] = z.read(i)
        if sum(len(b) for b in files.values()) > MAX_UNPACKED:
            return None, ["açılmış toplam boyut 50 MB'ı aşıyor"]
    return files, errs


# ------------------------------------------------------------------ derleme / denetim

def package_name(mid: str, version: str) -> str:
    return f"{mid}-{version}.koyext"


def build_item(mid: str, manifest: dict, meta: dict, pkg_bytes: bytes, preview: bytes) -> dict:
    name = package_name(mid, manifest["version"])
    item = {
        "id": mid,
        "name": manifest["name"],
        "kind": manifest["kind"],
        "version": manifest["version"],
        "author": manifest["author"],
        "license": manifest["license"],
        "summary": meta["summary"],
        "description": meta["description"],
        "homepage": meta["homepage"],
        "updated": meta["updated"],
        "downloads": meta.get("downloads", 0),
        "package": {"url": f"{BASE_URL}packages/{name}", "sha256": sha256(pkg_bytes), "size": len(pkg_bytes)},
        "preview": {"url": f"{BASE_URL}previews/{mid}.png", "sha256": sha256(preview)},
    }
    return item


def render_index(items: list[dict]) -> str:
    # Deterministik zaman damgası: en yeni 'updated' günü (her çalıştırmada değişmez).
    generated = (max((i["updated"] for i in items), default="1970-01-01")) + "T00:00:00Z"
    doc = {"format": FORMAT_NAME, "formatVersion": FORMAT_VERSION, "generated": generated, "items": items}
    return json.dumps(doc, indent=2, ensure_ascii=False) + "\n"


def run(check: bool) -> int:
    errors: list[str] = []
    items: list[dict] = []
    wanted_packages: set[str] = set()
    wanted_previews: set[str] = set()
    pending_writes: list[tuple[Path, bytes]] = []

    if not SRC.is_dir():
        print("store/src bulunamadı", file=sys.stderr)
        return 1

    for folder in sorted(p for p in SRC.iterdir() if p.is_dir()):
        mid = folder.name
        try:
            files = collect_source(folder)
            if "manifest.json" not in files:
                errors.append(f"{mid}: manifest.json eksik")
                continue
            manifest = read_json(folder / "manifest.json")
            merrs = validate_manifest(manifest, mid)
            if merrs:
                errors += [f"{mid}: {e}" for e in merrs]
                continue
            kind = manifest["kind"]
            errors += [f"{mid}: {e}" for e in validate_files(kind, files)]
            if "preview.png" not in files:
                errors.append(f"{mid}: preview.png eksik (mağaza kartı için zorunlu)")
                continue
            meta, serrs = load_store_meta(folder, mid)
            errors += serrs
            if serrs:
                continue

            pkg_name = package_name(mid, manifest["version"])
            pkg_path = PACKAGES / pkg_name
            preview = files["preview.png"]
            wanted_packages.add(pkg_name)
            wanted_previews.add(f"{mid}.png")

            # Diskteki paket kaynakla aynı içerikteyse olduğu gibi korunur: zlib sürümleri farklı bayt
            # üretebilir, sha256 gereksiz yere değişmesin. Yalnız içerik değiştiyse yeniden sıkıştırılır.
            pkg_bytes: bytes | None = None
            if pkg_path.is_file():
                existing = pkg_path.read_bytes()
                got, zerrs = read_zip(existing)
                if got is not None and not zerrs and got == files:
                    pkg_bytes = existing
                    # Sıra ve öznitelik de belirlenime uymalı (elle yeniden zip'lenmiş paketi yakala).
                elif check:
                    errors.append(f"{mid}: {pkg_name} kaynakla uyumsuz ya da bozuk"
                                  + (f" ({'; '.join(zerrs)})" if zerrs else "") + "; build-store.py çalıştırın")
            elif check:
                errors.append(f"{mid}: {pkg_name} yok; build-store.py çalıştırın")
            if pkg_bytes is None:
                pkg_bytes = make_zip(files)
                if not check:
                    pending_writes.append((pkg_path, pkg_bytes))

            # Üretilen/mevcut paket de aynı kurallardan geçmeli.
            got, zerrs = read_zip(pkg_bytes)
            if got is None or zerrs:
                errors += [f"{mid}: paket: {e}" for e in (zerrs or ["okunamadı"])]
                continue
            errors += [f"{mid}: paket: {e}" for e in validate_files(kind, got)]
            pm = json.loads(got["manifest.json"]) if "manifest.json" in got else {}
            for field in ("id", "version", "kind"):
                if pm.get(field) != manifest[field]:
                    errors.append(f"{mid}: paket manifestindeki {field} kaynakla aynı değil")
            if len(pkg_bytes) > MAX_DOWNLOAD:
                errors.append(f"{mid}: paket 25 MB'dan büyük")

            prev_path = PREVIEWS / f"{mid}.png"
            if prev_path.is_file():
                if prev_path.read_bytes() != preview:
                    if check:
                        errors.append(f"{mid}: previews/{mid}.png kaynaktaki preview.png ile aynı değil")
                    else:
                        pending_writes.append((prev_path, preview))
            elif check:
                errors.append(f"{mid}: previews/{mid}.png yok; build-store.py çalıştırın")
            else:
                pending_writes.append((prev_path, preview))

            items.append(build_item(mid, manifest, meta, pkg_bytes, preview))
        except StoreError as ex:
            errors.append(str(ex))

    # Kimlikler benzersiz olmalı (klasör adı = id, yine de açıkça denetle) ve sınırlar.
    if len({i["id"] for i in items}) != len(items):
        errors.append("yinelenen id")
    if len(items) > MAX_ITEMS:
        errors.append(f"indeks {MAX_ITEMS} öğeden fazla")

    index_text = render_index(items)
    if len(index_text.encode("utf-8")) > MAX_INDEX_BYTES:
        errors.append("index.json 1 MB'dan büyük")

    # Yetim dosyalar
    for directory, wanted, suffix in ((PACKAGES, wanted_packages, ".koyext"), (PREVIEWS, wanted_previews, ".png")):
        if directory.is_dir():
            for p in sorted(directory.iterdir()):
                if p.is_file() and p.suffix == suffix and p.name not in wanted:
                    if check:
                        errors.append(f"{p.relative_to(ROOT).as_posix()}: kaynağı olmayan dosya")
                    else:
                        p.unlink()
                        print(f"silindi: {p.relative_to(ROOT).as_posix()}")

    if check:
        if not INDEX.is_file():
            errors.append("store/index.json yok; build-store.py çalıştırın")
        elif INDEX.read_text(encoding="utf-8") != index_text:
            errors.append("store/index.json güncel değil; python scripts/build-store.py çalıştırıp sonucu ekleyin")
        if errors:
            print("Mağaza denetimi BAŞARISIZ:", file=sys.stderr)
            for e in errors:
                print(f"  - {e}", file=sys.stderr)
            return 1
        print(f"Mağaza denetimi tamam: {len(items)} öğe, index.json güncel.")
        return 0

    if errors:
        print("Derleme durduruldu:", file=sys.stderr)
        for e in errors:
            print(f"  - {e}", file=sys.stderr)
        return 1

    PACKAGES.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    for path, data in pending_writes:
        path.write_bytes(data)
        print(f"yazıldı: {path.relative_to(ROOT).as_posix()}")
    # newline="\n": Windows'ta da aynı baytlar (CI'da --check aynı metni bekler).
    with open(INDEX, "w", encoding="utf-8", newline="\n") as f:
        f.write(index_text)
    print(f"yazıldı: store/index.json ({len(items)} öğe)")
    return 0


def main() -> int:
    # Windows konsolunda Türkçe karakterler bozulmasın.
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description="Koy eklenti mağazası derleyicisi")
    ap.add_argument("--check", action="store_true", help="dosya yazmadan doğrula; fark ya da hata varsa çıkış kodu 1")
    args = ap.parse_args()
    return run(args.check)


if __name__ == "__main__":
    sys.exit(main())
