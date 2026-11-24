#!/usr/bin/env python3
"""sheet.py - the look sheet: every capture of a run on one labelled contact sheet, the way the vessel-model and
icon kits put a hull or a glyph beside its yardstick before anyone else looks.

    python3 Tools/Build/unity_look/sheet.py Logs/LookSheet                      # -> Logs/LookSheet/sheet.jpg
    python3 Tools/Build/unity_look/sheet.py Logs/LookSheet --out Docs/Look/menu_2026-11-07.jpg --commit abc123
    python3 Tools/Build/unity_look/sheet.py Logs/LookSheet --beside Docs/MenuRedesignB/board/Hangar.png
    python3 Tools/Build/unity_look/sheet.py --self-test

Reads manifest.jsonl (written by LookSheetCapture) and lays the captures out in rows of --columns tiles (default 2),
each tile labelled with its label, scene, game time and size; --beside puts one reference image (a picked Frogma
board, an earlier approved sheet) as the first tile, so the build and the board sit side by side. A record with an
error becomes a grey tile that says so: a missing capture is shown, never hidden. Exit 0; 1 when nothing could be
laid out; 2 on a setup error. Needs Pillow.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import tempfile

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:  # pragma: no cover
    Image = None

TILE_W = 640
GUTTER = 16
LABEL_H = 44
BG = (18, 20, 28)
INK = (220, 224, 236)
WARN = (255, 110, 90)


def _font(size: int):
    for name in ("DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "Arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def _records(folder: str) -> list[dict]:
    path = os.path.join(folder, "manifest.jsonl")
    if not os.path.isfile(path):
        return []
    out = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line:
                try:
                    out.append(json.loads(line))
                except json.JSONDecodeError:
                    out.append({"path": "", "label": "unreadable manifest line", "error": line[:80]})
    return out


def _tile(record: dict, folder: str, font) -> Image.Image:
    path = record.get("path", "")
    if path and not os.path.isabs(path) and not os.path.isfile(path):
        path = os.path.join(folder, os.path.basename(path))
    label = record.get("label") or os.path.basename(path) or "(no label)"
    sub = " ".join(s for s in (
        record.get("scene", ""),
        f"t={record['gameTime']:.1f}s" if isinstance(record.get("gameTime"), (int, float)) else "",
        f"{record.get('width', '')}x{record.get('height', '')}" if record.get("width") else "",
        record.get("method", ""),
    ) if s)
    error = record.get("error", "")
    tile_h = int(TILE_W * 9 / 16)
    tile = Image.new("RGB", (TILE_W, tile_h + LABEL_H), BG)
    draw = ImageDraw.Draw(tile)
    if error or not path or not os.path.isfile(path):
        draw.rectangle((0, 0, TILE_W, tile_h), fill=(48, 48, 56))
        draw.text((12, tile_h // 2 - 10), error or "missing capture", fill=WARN, font=font)
    else:
        try:
            with Image.open(path) as im:
                im = im.convert("RGB")
                im.thumbnail((TILE_W, tile_h))
                tile.paste(im, ((TILE_W - im.width) // 2, (tile_h - im.height) // 2))
        except Exception as exc:  # noqa: BLE001
            draw.rectangle((0, 0, TILE_W, tile_h), fill=(48, 48, 56))
            draw.text((12, tile_h // 2 - 10), f"unreadable: {type(exc).__name__}", fill=WARN, font=font)
    draw.text((8, tile_h + 6), label[:70], fill=INK, font=font)
    draw.text((8, tile_h + 24), sub[:90], fill=(150, 156, 176), font=_font(12))
    return tile


def build(folder: str, out: str, columns: int = 2, beside: str | None = None, commit: str = "", title: str = "") -> int:
    records = _records(folder)
    if beside:
        records.insert(0, {"path": beside, "label": "beside: " + os.path.basename(beside), "method": "reference"})
    if not records:
        print(f"sheet: nothing to lay out in {folder} (no manifest.jsonl)", file=sys.stderr)
        return 1
    font = _font(14)
    tiles = [_tile(r, folder, font) for r in records]
    columns = max(1, columns)
    rows = (len(tiles) + columns - 1) // columns
    tile_h = tiles[0].height
    header = 36
    sheet = Image.new("RGB", (columns * TILE_W + (columns + 1) * GUTTER, header + rows * (tile_h + GUTTER) + GUTTER), BG)
    draw = ImageDraw.Draw(sheet)
    head = title or f"Look sheet: {os.path.basename(os.path.abspath(folder))}"
    if commit:
        head += f"  commit {commit[:10]}"
    head += f"  {len(records)} tiles"
    draw.text((GUTTER, 10), head, fill=INK, font=_font(16))
    for i, tile in enumerate(tiles):
        x = GUTTER + (i % columns) * (TILE_W + GUTTER)
        y = header + GUTTER + (i // columns) * (tile_h + GUTTER)
        sheet.paste(tile, (x, y))
    os.makedirs(os.path.dirname(os.path.abspath(out)) or ".", exist_ok=True)
    sheet.save(out, quality=88)
    print(f"sheet: wrote {out} ({sheet.width}x{sheet.height}, {len(records)} tiles)")
    return 0


def self_test() -> int:
    if Image is None:
        print("sheet: Pillow is not installed", file=sys.stderr)
        return 2
    with tempfile.TemporaryDirectory() as tmp:
        good = os.path.join(tmp, "good.png")
        Image.new("RGB", (320, 180), (90, 120, 200)).save(good)
        with open(os.path.join(tmp, "manifest.jsonl"), "w", encoding="utf-8") as fh:
            fh.write(json.dumps({"path": good, "label": "Home (PC)", "scene": "Menu_Main", "width": 320, "height": 180, "gameTime": 3.5, "method": "render-to-texture", "error": ""}) + "\n")
            fh.write(json.dumps({"path": "", "label": "Hangar (PC)", "error": "no graphics device"}) + "\n")
        out = os.path.join(tmp, "sheet.jpg")
        assert build(tmp, out, columns=2, commit="deadbeef00") == 0
        with Image.open(out) as im:
            assert im.width == 2 * TILE_W + 3 * GUTTER, im.size
        assert build(os.path.join(tmp, "empty"), os.path.join(tmp, "x.jpg")) == 1  # negative control: no manifest
    print("sheet: self-test OK")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("folder", nargs="?", help="the capture folder (holds manifest.jsonl)")
    ap.add_argument("--out", help="the sheet to write (default <folder>/sheet.jpg)")
    ap.add_argument("--columns", type=int, default=2)
    ap.add_argument("--beside", help="a reference image laid first (a picked board, an approved sheet)")
    ap.add_argument("--commit", default="")
    ap.add_argument("--title", default="")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    if Image is None:
        print("sheet: Pillow is not installed (pip install Pillow)", file=sys.stderr)
        return 2
    if not args.folder:
        ap.print_usage()
        return 2
    return build(args.folder, args.out or os.path.join(args.folder, "sheet.jpg"), args.columns, args.beside, args.commit, args.title)


if __name__ == "__main__":
    sys.exit(main())
