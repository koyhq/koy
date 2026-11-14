"""Yerleşik MP4 arka plan döngülerini kesintisiz (seamless) hale getirir ve önizleme PNG'lerini yeniler.

Her video ortasından ikiye bölünür; ikinci yarı başa, ilk yarı sona alınarak ortadaki birleşim
noktası ~0,5 sn çapraz geçişle (xfade) yumuşatılır. Böylece videonun son karesi ile 0. karesi
özgün akıştaki ardışık iki kareye denk gelir (sıçrama sıfıra iner) ve 0. kare karışmamış net
bir kare olarak kalır. Ardından her videonun yeni 0. karesinden eşleşen 192x108 PNG üretilir.

Çalıştır:
    python scripts/loop-backgrounds.py              # işlenmemiş MP4'leri döngüye çevirir + PNG üretir + doğrular
    python scripts/loop-backgrounds.py --force      # tümünü zorla yeniden işler
    python scripts/loop-backgrounds.py --verify     # dosya değiştirmeden yalnızca döngü/PNG doğrulaması yapar
    python scripts/loop-backgrounds.py --only lava  # yalnızca belirtilen önayarı işler
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

REPO_ROOT = Path(__file__).resolve().parent.parent
BACKGROUNDS_DIR = REPO_ROOT / "assets" / "backgrounds"
LOOP_TAG = "koy-seamless-loop"
DEFAULT_FADE_SEC = 0.5
DEFAULT_PNG_SIZE = (192, 108)


def ensure_tools() -> None:
    for tool in ("ffmpeg", "ffprobe"):
        if shutil.which(tool) is None:
            raise RuntimeError(f"Gerekli araç PATH üzerinde bulunamadı: {tool}")


def probe_video(mp4_path: Path) -> dict:
    raw = subprocess.check_output(
        [
            "ffprobe",
            "-v",
            "error",
            "-select_streams",
            "v:0",
            "-show_entries",
            "stream=width,height,r_frame_rate,nb_frames,duration,codec_name,pix_fmt:format_tags=comment",
            "-of",
            "json",
            str(mp4_path),
        ]
    )
    data = json.loads(raw)
    stream = data["streams"][0]
    tags = data.get("format", {}).get("tags", {}) or {}
    stream["comment_tag"] = tags.get("comment", "")
    return stream


def parse_fps(rate_str: str) -> float:
    num, den = rate_str.split("/")
    return float(num) / float(den)


def target_png_size(png_path: Path) -> tuple[int, int]:
    if png_path.exists():
        with Image.open(png_path) as im:
            return im.size
    return DEFAULT_PNG_SIZE


def decode_rgb_frames(mp4_path: Path, width: int, height: int) -> np.ndarray:
    raw = subprocess.check_output(
        [
            "ffmpeg",
            "-v",
            "error",
            "-i",
            str(mp4_path),
            "-f",
            "rawvideo",
            "-pix_fmt",
            "rgb24",
            "-",
        ],
        stderr=subprocess.DEVNULL,
    )
    return np.frombuffer(raw, dtype=np.uint8).reshape(-1, height, width, 3)


def decode_frame0_scaled(mp4_path: Path, width: int, height: int) -> np.ndarray:
    raw = subprocess.check_output(
        [
            "ffmpeg",
            "-v",
            "error",
            "-i",
            str(mp4_path),
            "-vf",
            f"select=eq(n\\,0),scale={width}:{height}:flags=lanczos,format=rgb24",
            "-frames:v",
            "1",
            "-f",
            "rawvideo",
            "-pix_fmt",
            "rgb24",
            "-",
        ],
        stderr=subprocess.DEVNULL,
    )
    return np.frombuffer(raw, dtype=np.uint8).reshape(height, width, 3)


def process_video(mp4_path: Path, fade_sec: float, force: bool) -> bool:
    info = probe_video(mp4_path)
    if not force and info.get("comment_tag") == LOOP_TAG:
        return False

    fps = parse_fps(info["r_frame_rate"])
    nb_frames = int(info.get("nb_frames") or round(float(info["duration"]) * fps))
    mid_frame = nb_frames // 2
    fade_frames = max(1, round(fade_sec * fps))

    if mid_frame <= fade_frames or (nb_frames - mid_frame) <= fade_frames:
        raise ValueError(f"{mp4_path.name}: video {fade_sec}s çapraz geçiş için çok kısa ({nb_frames} kare)")

    tail_frames = nb_frames - mid_frame
    fade_dur = fade_frames / fps
    offset_sec = (tail_frames - fade_frames) / fps

    # İkinci yarı başa, ilk yarı sona alınır; aradaki sıçrama xfade ile ortada eritilir.
    filter_complex = (
        f"[0:v]split=2[a][b];"
        f"[a]trim=start_frame={mid_frame}:end_frame={nb_frames},setpts=PTS-STARTPTS[tail];"
        f"[b]trim=start_frame=0:end_frame={mid_frame},setpts=PTS-STARTPTS[head];"
        f"[tail][head]xfade=transition=fade:duration={fade_dur:.6f}:offset={offset_sec:.6f},format=yuv420p[v]"
    )

    tmp_mp4 = mp4_path.with_suffix(".tmp.mp4")
    try:
        subprocess.run(
            [
                "ffmpeg",
                "-y",
                "-v",
                "error",
                "-i",
                str(mp4_path),
                "-filter_complex",
                filter_complex,
                "-map",
                "[v]",
                "-an",
                "-c:v",
                "libx264",
                "-preset",
                "slow",
                "-crf",
                "20",
                "-pix_fmt",
                "yuv420p",
                "-movflags",
                "+faststart",
                "-metadata",
                f"comment={LOOP_TAG}",
                str(tmp_mp4),
            ],
            check=True,
        )
        os.replace(tmp_mp4, mp4_path)
    finally:
        if tmp_mp4.exists():
            tmp_mp4.unlink()

    extract_poster_png(mp4_path)
    return True


def extract_poster_png(mp4_path: Path) -> None:
    png_path = mp4_path.with_suffix(".png")
    png_w, png_h = target_png_size(png_path)
    frame0 = decode_frame0_scaled(mp4_path, png_w, png_h)
    tmp_png = png_path.with_suffix(".tmp.png")
    try:
        Image.fromarray(frame0, mode="RGB").save(tmp_png, format="PNG", optimize=True)
        os.replace(tmp_png, png_path)
    finally:
        if tmp_png.exists():
            tmp_png.unlink()


def verify_pair(mp4_path: Path) -> dict:
    info = probe_video(mp4_path)
    w, h = int(info["width"]), int(info["height"])
    frames = decode_rgb_frames(mp4_path, w, h).astype(np.float32)

    loop_diff = float(np.mean(np.abs(frames[0] - frames[-1])))
    step01_diff = float(np.mean(np.abs(frames[0] - frames[1])))
    step_diffs = np.mean(np.abs(frames[1:] - frames[:-1]), axis=(1, 2, 3))
    max_step_diff = float(np.max(step_diffs))

    png_path = mp4_path.with_suffix(".png")
    if not png_path.exists():
        raise FileNotFoundError(f"Eşleşen PNG yok: {png_path}")

    with Image.open(png_path) as im:
        png_rgb = im.convert("RGB")
        png_w, png_h = png_rgb.size
        png_arr = np.asarray(png_rgb, dtype=np.float32)

    expected_frame0 = decode_frame0_scaled(mp4_path, png_w, png_h).astype(np.float32)
    png_diff = float(np.mean(np.abs(expected_frame0 - png_arr)))

    return {
        "name": mp4_path.name,
        "frames": len(frames),
        "size": f"{w}x{h}",
        "codec": info["codec_name"],
        "pix_fmt": info["pix_fmt"],
        "loop_diff": loop_diff,
        "step01_diff": step01_diff,
        "max_step_diff": max_step_diff,
        "png_size": f"{png_w}x{png_h}",
        "png_diff": png_diff,
        "bytes": mp4_path.stat().st_size,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="MP4 arka plan döngülerini kesintisizleştirir ve PNG'leri yeniler.")
    parser.add_argument("--fade", type=float, default=DEFAULT_FADE_SEC, help="Çapraz geçiş süresi (saniye, varsayılan 0.5)")
    parser.add_argument("--force", action="store_true", help="Zaten işlenmiş videoları da yeniden işle")
    parser.add_argument("--verify", action="store_true", help="Değişiklik yapmadan yalnızca doğrula")
    parser.add_argument("--only", type=str, default="", help="Yalnızca bu önayar adını işle (uzantısız)")
    args = parser.parse_args()

    ensure_tools()
    mp4_files = sorted(BACKGROUNDS_DIR.glob("*.mp4"))
    if args.only:
        mp4_files = [p for p in mp4_files if p.stem.lower() == args.only.lower()]
    if not mp4_files:
        print("Eşleşen MP4 bulunamadı.", file=sys.stderr)
        return 1

    for mp4 in mp4_files:
        if not args.verify:
            changed = process_video(mp4, args.fade, args.force)
            tag = "güncellendi" if changed else "atlandı"
        else:
            tag = "doğrulandı"

        m = verify_pair(mp4)
        print(
            f"{m['name']:16} [{tag:11}] {m['size']} {m['codec']} {m['pix_fmt']} "
            f"frames={m['frames']:3} loop_diff={m['loop_diff']:.3f} step01={m['step01_diff']:.3f} "
            f"max_step={m['max_step_diff']:.3f} png({m['png_size']})_diff={m['png_diff']:.3f} "
            f"bytes={m['bytes']}"
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
