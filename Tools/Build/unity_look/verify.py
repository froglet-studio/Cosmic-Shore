#!/usr/bin/env python3
"""verify.py - the look-sheet gate: a captured PNG is a real frame, not a blank one.

A capture that rendered nothing comes out uniform (black, or one flat colour); a capture of the wrong size came from
the wrong path. Both are failures a person would otherwise be the first to notice. Gates, each with a negative
control in --self-test:

  exists      the file is there and decodes as a PNG
  size        width x height match the manifest record (when a manifest is given)
  not-blank   fewer than 97 % of the pixels share the modal colour (within 4 levels), and the mean luminance
              is above 2 / 255 (an all-black frame) and below 253 / 255 (an all-white frame)
  manifest    every record in manifest.jsonl with no error names a file that passes the gates above

    python3 Tools/Build/unity_look/verify.py Logs/LookSheet            # every PNG the manifest names
    python3 Tools/Build/unity_look/verify.py a.png b.png                # named files
    python3 Tools/Build/unity_look/verify.py --json Logs/LookSheet
    python3 Tools/Build/unity_look/verify.py --self-test

Exit 0 when every file passes, 1 when one fails, 2 on a usage or setup error. Needs Pillow (the icon kit uses it).
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import tempfile

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    Image = None

UNIFORM_SHARE = 0.97
TOLERANCE = 4
DARK = 2.0
BRIGHT = 253.0


def analyse(path: str) -> dict:
    """The measurements behind the gates for one PNG."""
    out = {"path": path, "exists": os.path.isfile(path)}
    if not out["exists"]:
        return out
    try:
        with Image.open(path) as im:
            im = im.convert("RGB")
            out["width"], out["height"] = im.size
            small = im.resize((min(im.width, 256), min(im.height, 144)))
            px = list(small.getdata())
    except Exception as exc:  # noqa: BLE001 - any decode failure is the finding
        out["decode_error"] = f"{type(exc).__name__}: {exc}"
        return out
    n = len(px)
    lum = sum(0.299 * r + 0.587 * g + 0.114 * b for r, g, b in px) / max(1, n)
    # the modal colour, on a coarse grid so near-identical pixels count together
    buckets: dict[tuple, int] = {}
    for r, g, b in px:
        key = (r // (TOLERANCE * 2), g // (TOLERANCE * 2), b // (TOLERANCE * 2))
        buckets[key] = buckets.get(key, 0) + 1
    mode_share = max(buckets.values()) / max(1, n)
    out.update({"mean_luminance": round(lum, 2), "modal_share": round(mode_share, 4)})
    return out


def gates(info: dict, expect: tuple[int, int] | None = None) -> list[tuple[str, bool, str]]:
    rows: list[tuple[str, bool, str]] = []
    if not info.get("exists"):
        rows.append(("exists", False, "missing"))
        return rows
    if "decode_error" in info:
        rows.append(("exists", False, info["decode_error"]))
        return rows
    rows.append(("exists", True, f"{info['width']}x{info['height']}"))
    if expect:
        ok = (info["width"], info["height"]) == tuple(expect)
        rows.append(("size", ok, f"expected {expect[0]}x{expect[1]}"))
    lum, share = info["mean_luminance"], info["modal_share"]
    if share >= UNIFORM_SHARE:
        rows.append(("not-blank", False, f"{share:.0%} of the pixels are one colour"))
    elif lum <= DARK:
        rows.append(("not-blank", False, f"mean luminance {lum} (black)"))
    elif lum >= BRIGHT:
        rows.append(("not-blank", False, f"mean luminance {lum} (white)"))
    else:
        rows.append(("not-blank", True, f"modal share {share:.0%}, luminance {lum}"))
    return rows


def read_manifest(folder: str) -> list[dict]:
    path = os.path.join(folder, "manifest.jsonl")
    if not os.path.isfile(path):
        return []
    records = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                records.append(json.loads(line))
            except json.JSONDecodeError:
                records.append({"path": "", "error": f"unreadable manifest line: {line[:60]}"})
    return records


def check(targets: list[str]) -> tuple[list[dict], bool]:
    """Every PNG named, or every PNG a folder's manifest names (and the manifest's own errors)."""
    results: list[dict] = []
    ok = True
    for target in targets:
        if os.path.isdir(target):
            records = read_manifest(target)
            if not records:
                results.append({"path": target, "gates": [("manifest", False, "no manifest.jsonl or it is empty")]})
                ok = False
                continue
            for rec in records:
                if rec.get("error"):
                    results.append({"path": rec.get("path", ""), "label": rec.get("label", ""),
                                    "gates": [("manifest", False, rec["error"])]})
                    ok = False
                    continue
                path = rec.get("path", "")
                if not os.path.isabs(path) and not os.path.isfile(path):
                    path = os.path.join(target, os.path.basename(path))
                expect = (rec["width"], rec["height"]) if rec.get("width") and rec.get("height") else None
                rows = gates(analyse(path), expect)
                results.append({"path": path, "label": rec.get("label", ""), "gates": rows})
                ok = ok and all(r[1] for r in rows)
        else:
            rows = gates(analyse(target))
            results.append({"path": target, "gates": rows})
            ok = ok and all(r[1] for r in rows)
    return results, ok


def render(results: list[dict]) -> str:
    lines = []
    for res in results:
        label = f" [{res['label']}]" if res.get("label") else ""
        for name, passed, why in res["gates"]:
            lines.append(f"{'ok  ' if passed else 'FAIL'}  {name:<10} {res['path']}{label} :: {why}")
    return "\n".join(lines)


def _png(path: str, size: tuple[int, int], painter) -> None:
    im = Image.new("RGB", size)
    px = im.load()
    for y in range(size[1]):
        for x in range(size[0]):
            px[x, y] = painter(x, y)
    im.save(path)


def self_test() -> int:
    if Image is None:
        print("verify: Pillow is not installed", file=sys.stderr)
        return 2
    with tempfile.TemporaryDirectory() as tmp:
        black = os.path.join(tmp, "black.png")
        flat = os.path.join(tmp, "flat.png")
        white = os.path.join(tmp, "white.png")
        scene = os.path.join(tmp, "scene.png")
        _png(black, (64, 36), lambda x, y: (0, 0, 0))
        _png(flat, (64, 36), lambda x, y: (40, 60, 90))
        _png(white, (64, 36), lambda x, y: (255, 255, 255))
        _png(scene, (64, 36), lambda x, y: ((x * 7) % 256, (y * 13) % 256, ((x + y) * 3) % 256))
        # negative controls
        for p in (black, flat, white):
            rows = gates(analyse(p))
            assert not all(r[1] for r in rows), (p, rows)
        rows = gates(analyse(scene))
        assert all(r[1] for r in rows), rows
        rows = gates(analyse(scene), (64, 36))
        assert all(r[1] for r in rows), rows
        rows = gates(analyse(scene), (1920, 1080))
        assert not all(r[1] for r in rows), rows
        rows = gates(analyse(os.path.join(tmp, "nope.png")))
        assert rows == [("exists", False, "missing")], rows
        # a manifest folder: one good record, one errored record, one record naming a blank frame
        with open(os.path.join(tmp, "manifest.jsonl"), "w", encoding="utf-8") as fh:
            fh.write(json.dumps({"path": scene, "width": 64, "height": 36, "label": "good", "error": ""}) + "\n")
            fh.write(json.dumps({"path": "", "label": "bad", "error": "no graphics device"}) + "\n")
            fh.write(json.dumps({"path": black, "width": 64, "height": 36, "label": "blank", "error": ""}) + "\n")
        results, ok = check([tmp])
        assert not ok and len(results) == 3, results
        assert all(r[1] for r in results[0]["gates"]), results[0]
        assert results[1]["gates"][0][0] == "manifest", results[1]
        assert not all(r[1] for r in results[2]["gates"]), results[2]
    print("verify: self-test OK")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("targets", nargs="*", help="PNG files, or a folder holding manifest.jsonl")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    if Image is None:
        print("verify: Pillow is not installed (pip install Pillow)", file=sys.stderr)
        return 2
    if not args.targets:
        ap.print_usage()
        return 2
    results, ok = check(args.targets)
    if args.json:
        print(json.dumps({"ok": ok, "results": results}, indent=2))
    else:
        print(render(results))
        print("LOOK SHEET: " + ("ALL OK" if ok else "FAILED"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
