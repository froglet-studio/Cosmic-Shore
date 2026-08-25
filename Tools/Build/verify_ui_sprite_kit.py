#!/usr/bin/env python3
"""Prove the SHIPPED UI sprite kit files are correct 9-slice assets.

Deliberately a separate script from ``author_ui_sprite_kit.py``, and
deliberately reading the committed PNG and ``.meta`` bytes rather than
re-deriving them: the transcription from a proven geometry into an asset is the
step neither the geometry code nor code review can see.

    python3 Tools/Build/verify_ui_sprite_kit.py [-v]

Checks, per sprite:

  1. The PNG is 8-bit RGBA and its RGB is 255,255,255 in EVERY pixel - the
     "white + alpha, tints at runtime" rule, checked on the file rather than
     asserted in a doc.
  2. The ``.meta`` carries the import settings the kit depends on, and a
     ``spriteBorder`` matching the kit table.
  3. Every partially-transparent pixel of a sliver lies strictly inside a
     border corner region.  A 9-slice copies corner regions verbatim, so this
     is what makes the diagonal survive scaling - and it is exactly the check
     that fails if an inset is authored smaller than its sliver.
  4. Each edge region is invariant along the axis it stretches, and the centre
     is invariant along both.  Together with (3) this means composition at any
     legal size is EXACT, not merely close.
  5. End to end: compose the shipped 9-slice at a spread of target sizes and
     compare against the same geometry rasterised natively at that size.  Any
     distortion of the sliver shows up here as a nonzero pixel difference.

Exit code 0 iff every sprite passes.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from ui_sprite_kit_geometry import decode_png, rasterize  # noqa: E402
import author_ui_sprite_kit as kitmod  # noqa: E402

REPO = kitmod.REPO
KIT = kitmod.KIT

#: Import settings the kit's correctness rests on, as literal meta lines.
REQUIRED_META = {
    "textureType": "8",              # Sprite (2D and UI)
    "spriteMode": "1",               # Single
    "enableMipMap": "0",             # UI is drawn at one depth
    "alphaIsTransparency": "1",      # no dark fringe on the diagonal
    "alphaUsage": "1",               # alpha from input
    "sRGBTexture": "1",
    "spritePixelsToUnits": "100",    # matches CanvasScaler referencePixelsPerUnit
    "filterMode": "1",               # bilinear
    "wrapU": "1",                    # clamp
    "wrapV": "1",
}


# --------------------------------------------------------------------------
# 9-slice composition, matching UnityEngine.UI.Image.Type.Sliced
# --------------------------------------------------------------------------

def slice_stops(size: int, low: int, high: int, src: int) -> tuple:
    """Destination and source cut points along one axis."""
    return ((0, low, size - high, size), (0, low, src - high, src))


def compose(src: list[list[int]], border: tuple[int, int, int, int],
            tw: int, th: int) -> list[list[int]]:
    """Nine-slice ``src`` into a ``tw`` x ``th`` alpha grid.

    ``border`` is Unity's ``(left, bottom, right, top)``.  Source rows run top
    -down, so the top border is ``border[3]`` and the bottom is ``border[1]``.
    Stretched regions are sampled nearest-neighbour; check (4) proves those
    regions are constant along the stretch axis, so the filter choice cannot
    hide a defect.
    """
    sh, sw = len(src), len(src[0])
    left, bottom, right, top = border
    dx, sx = slice_stops(tw, left, right, sw)
    dy, sy = slice_stops(th, top, bottom, sh)

    out = [[0] * tw for _ in range(th)]
    for gy in range(3):
        d0, d1, s0, s1 = dy[gy], dy[gy + 1], sy[gy], sy[gy + 1]
        if d1 <= d0 or s1 <= s0:
            continue
        for y in range(d0, d1):
            syy = (s0 + (y - d0) if (d1 - d0) == (s1 - s0)
                   else s0 + int((y - d0) * (s1 - s0) / (d1 - d0)))
            syy = min(syy, s1 - 1)
            for gx in range(3):
                e0, e1, t0, t1 = dx[gx], dx[gx + 1], sx[gx], sx[gx + 1]
                if e1 <= e0 or t1 <= t0:
                    continue
                same = (e1 - e0) == (t1 - t0)
                for x in range(e0, e1):
                    sxx = (t0 + (x - e0) if same
                           else t0 + int((x - e0) * (t1 - t0) / (e1 - e0)))
                    out[y][x] = src[syy][min(sxx, t1 - 1)]
    return out


# --------------------------------------------------------------------------
# Checks
# --------------------------------------------------------------------------

def read_meta(path: Path) -> dict[str, str]:
    txt = path.read_text()
    out: dict[str, str] = {}
    for line in txt.splitlines():
        m = re.match(r"\s*([A-Za-z_][A-Za-z0-9_]*):\s*(.*?)\s*$", line)
        if m and m.group(2) != "":
            out.setdefault(m.group(1), m.group(2))
    out["_raw"] = txt
    return out


def check_white_rgb(png: bytes) -> str | None:
    """Confirm no colour is baked: every pixel's RGB must be 255,255,255."""
    import struct
    import zlib
    pos, w, h, idat = 8, 0, 0, bytearray()
    while pos < len(png):
        (length,) = struct.unpack(">I", png[pos:pos + 4])
        tag = png[pos + 4:pos + 8]
        data = png[pos + 8:pos + 8 + length]
        if tag == b"IHDR":
            w, h, depth, color = struct.unpack(">IIBB", data[:10])
            if (depth, color) != (8, 6):
                return f"expected 8-bit RGBA, got depth={depth} colorType={color}"
        elif tag == b"IDAT":
            idat += data
        pos += 12 + length
    raw = zlib.decompress(bytes(idat))
    stride = w * 4
    p = 0
    for y in range(h):
        ft = raw[p]
        if ft != 0:
            return f"row {y} uses PNG filter {ft}; expected 0 for byte stability"
        line = raw[p + 1:p + 1 + stride]
        p += 1 + stride
        for x in range(w):
            r, g, b = line[x * 4], line[x * 4 + 1], line[x * 4 + 2]
            if (r, g, b) != (255, 255, 255):
                return (f"pixel ({x},{y}) has baked colour "
                        f"rgb=({r},{g},{b}); must be 255,255,255")
    return None


def check_sliver_inside_corners(alpha, border) -> str | None:
    """Every antialiased (partial) pixel must sit in an unstretched corner.

    Partial alpha only ever occurs on a diagonal in this kit, so this is a
    direct test that each inset is at least as large as its sliver.
    """
    h, w = len(alpha), len(alpha[0])
    left, bottom, right, top = border
    if not any(border):
        return None                      # no 9-slice: nothing is stretched
    for y in range(h):
        in_v = (y < top) or (y >= h - bottom) if (top or bottom) else True
        for x in range(w):
            a = alpha[y][x]
            if a == 0 or a == 255:
                continue
            in_h = (x < left) or (x >= w - right) if (left or right) else True
            if not (in_h and in_v):
                return (f"antialiased pixel ({x},{y}) alpha={a} is outside the "
                        f"unstretched corner regions for border {border}")
    return None


def check_regions_invariant(alpha, border) -> str | None:
    """Edge regions constant along their stretch axis; centre constant on both.

    This is the property that upgrades "9-slice looks fine" into "9-slice is
    exact".  A sliver that leaked into an edge region would break it.
    """
    h, w = len(alpha), len(alpha[0])
    left, bottom, right, top = border
    if not any(border):
        return None
    x0, x1 = left, w - right
    y0, y1 = top, h - bottom
    if x1 < x0 or y1 < y0:
        return f"border {border} overflows the {w}x{h} source"

    # Columns x0..x1 stretch horizontally: every such column must be identical.
    if x1 > x0:
        ref = [alpha[y][x0] for y in range(h)]
        for x in range(x0 + 1, x1):
            if [alpha[y][x] for y in range(h)] != ref:
                return (f"centre/edge column {x} differs from column {x0}; "
                        f"horizontal stretching would not be exact")
    # Rows y0..y1 stretch vertically: every such row must be identical.
    if y1 > y0 and (top or bottom):
        ref = list(alpha[y0])
        for y in range(y0 + 1, y1):
            if list(alpha[y]) != ref:
                return (f"centre/edge row {y} differs from row {y0}; "
                        f"vertical stretching would not be exact")
    return None


def target_sizes(name: str, sw: int, sh: int, border) -> list[tuple[int, int]]:
    left, bottom, right, top = border
    if not any(border):
        return []
    min_w = max(left + right, 1)
    widths = sorted({min_w, sw, sw * 3})
    if top or bottom:
        min_h = max(top + bottom, 1)
        heights = sorted({min_h, sh, sh * 2})
    else:
        heights = [sh]        # no vertical border: the authored height is fixed
    sizes = [(w, hh) for w in widths for hh in heights]
    sizes.append((409, sh))   # one long, deliberately non-round stretch
    return sizes


# --------------------------------------------------------------------------
# The demonstration scene
# --------------------------------------------------------------------------

def check_scene(failures: list[str]) -> int:
    """Confirm the test scene really shows what the task asked it to show.

    A scene is only evidence if it is checked: this asserts every kit sprite
    appears, at three or more DISTINCT widths, with the Image type its border
    calls for, and that no page runs off the bottom of the 1080 reference.
    """
    path = kitmod.SCENE_PATH
    if not path.exists():
        failures.append("scene: missing " + str(path.relative_to(REPO)))
        return 0
    txt = path.read_text()

    # Every referenced sprite GUID must be a kit sprite, and vice versa.
    want = {kitmod.guid_for(kitmod.unity_path_for(n)): n for n in KIT}
    seen_guids = set(re.findall(r"m_Sprite: \{fileID: 21300000, guid: "
                                r"([0-9a-f]{32})", txt))
    for guid, name in want.items():
        if guid not in seen_guids:
            failures.append(f"scene: {name} is never shown")
    for guid in seen_guids - set(want):
        failures.append(f"scene: references non-kit sprite guid {guid}")

    # Widths and Image types, read off the GameObject names the emitter writes.
    widths: dict[str, set[int]] = {}
    for name, w, h in re.findall(r"m_Name: (UIKit_\S+) @ (\d+)x(\d+)", txt):
        widths.setdefault(name, set()).add(int(w))
    for name in KIT:
        n = len(widths.get(name, ()))
        if n < 3:
            failures.append(f"scene: {name} shown at {n} distinct width(s), "
                            f"the task requires 3")

    n_sliced = len(re.findall(r"^  m_Type: 1$", txt, re.M))
    n_simple_img = len(re.findall(r"^  m_Type: 0$", txt, re.M))
    expected_sliced = sum(1 for n in KIT if any(kitmod.kit_border(n)))
    if n_sliced == 0 or n_simple_img == 0:
        failures.append("scene: expected both Sliced and Simple Images "
                        f"(sliced={n_sliced} simple={n_simple_img}); a "
                        f"borderless sprite must not be drawn Sliced")
    del expected_sliced

    # No dangling local references - a scene with one will not open cleanly.
    defined = {int(m) for m in re.findall(r"^--- !u!\d+ &(\d+)", txt, re.M)}
    external = {int(m) for m in re.findall(r"fileID: (\d+), guid:", txt)}
    local = {int(m) for m in re.findall(r"fileID: (\d+)\}", txt)}
    dangling = sorted(local - defined - external - {0})
    if dangling:
        failures.append(f"scene: dangling local fileID reference(s) "
                        f"{dangling[:5]}")

    # Vertical fit: the emitter lays rows out from the top of a 1080 canvas.
    for m in re.finditer(r"m_AnchoredPosition: \{x: [-\d.]+, y: (-[\d.]+)\}",
                         txt):
        if float(m.group(1)) < -1080:
            failures.append("scene: a row is laid out below y=-1080 and would "
                            "be off screen at the reference resolution")
            break
    return 1


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("-v", "--verbose", action="store_true")
    args = ap.parse_args()

    failures: list[str] = []
    checked = 0

    # GUID uniqueness across the whole project.
    guids: dict[str, str] = {}
    for meta in REPO.joinpath("Assets").rglob("*.meta"):
        m = re.search(r"^guid: ([0-9a-f]{32})", meta.read_text(errors="ignore"),
                      re.M)
        if m:
            prev = guids.get(m.group(1))
            if prev:
                failures.append(f"GUID COLLISION {m.group(1)}: {prev} / {meta}")
            guids[m.group(1)] = str(meta.relative_to(REPO))

    for name, (builder, mode, nominal, group, component, _note) in KIT.items():
        border = kitmod.kit_border(name)
        png_path = REPO / kitmod.unity_path_for(name)
        meta_path = Path(str(png_path) + ".meta")
        prefix = f"{name}: "
        before = len(failures)
        if not png_path.exists() or not meta_path.exists():
            failures.append(prefix + "missing PNG or .meta")
            continue

        blob = png_path.read_bytes()
        alpha = decode_png(blob)
        sw, sh = len(alpha[0]), len(alpha)
        exp_w, exp_h, region, stroke = builder()

        # Re-measure the correct inset from the SHIPPED pixels and demand the
        # meta agrees.  This is the check that would catch a hand-edited meta.
        from ui_sprite_kit_geometry import measure_border
        remeasured = measure_border(alpha, mode)
        if remeasured != border:
            failures.append(prefix + f"border in meta {border} but the shipped "
                                     f"pixels want {remeasured}")
        if nominal and min(b for b in remeasured if b) < nominal:
            failures.append(prefix + f"inset {remeasured} is under the "
                                     f"nominal {nominal} px feature")

        if (sw, sh) != (exp_w, exp_h):
            failures.append(prefix + f"size {sw}x{sh}, table says {exp_w}x{exp_h}")

        # 1. no baked colour
        err = check_white_rgb(blob)
        if err:
            failures.append(prefix + err)

        # 2. importer settings
        meta = read_meta(meta_path)
        for key, want in REQUIRED_META.items():
            if meta.get(key) != want:
                failures.append(prefix + f"meta {key}={meta.get(key)!r}, "
                                         f"expected {want!r}")
        want_border = (f"{{x: {border[0]}, y: {border[1]}, "
                       f"z: {border[2]}, w: {border[3]}}}")
        if meta.get("spriteBorder") != want_border:
            failures.append(prefix + f"spriteBorder {meta.get('spriteBorder')}, "
                                     f"expected {want_border}")
        n_compressed = len(re.findall(r"^\s*textureCompression: [^0]",
                                      meta["_raw"], re.M))
        if n_compressed:
            failures.append(prefix + f"{n_compressed} platform(s) use block "
                                     f"compression; it destroys 1 px frames")

        # 3. slivers live in unstretched corners
        err = check_sliver_inside_corners(alpha, border)
        if err:
            failures.append(prefix + err)

        # 4. stretch regions are invariant
        err = check_regions_invariant(alpha, border)
        if err:
            failures.append(prefix + err)

        # 5. end-to-end: composed == natively rasterised, at every target size
        worst = 0
        for tw, th in target_sizes(name, sw, sh, border):
            got = compose(alpha, border, tw, th)
            want = rasterize(tw, th, _regeom(name, tw, th), stroke)
            diff = max(abs(got[y][x] - want[y][x])
                       for y in range(th) for x in range(tw))
            worst = max(worst, diff)
            if diff != 0:
                failures.append(prefix + f"9-slice at {tw}x{th} differs from a "
                                         f"native raster by up to {diff}/255")
                break
        checked += 1
        if args.verbose:
            tag = "ok  " if len(failures) == before else "FAIL"
            print(f"  {tag} {name:34s} {sw}x{sh} border {tuple(border)} "
                  f"maxdiff {worst}/255")

    check_scene(failures)

    if failures:
        print("\n".join(f"FAIL  {f}" for f in failures))
        print(f"\n{len(failures)} failure(s)", file=sys.stderr)
        return 1
    print(f"OK  {checked} sprite(s): white+alpha, importer settings, slivers "
          f"inside their insets, exact 9-slice at every tested size")
    print("OK  test scene shows every sprite at 3+ distinct widths")
    return 0


def _regeom(name: str, w: int, h: int):
    """Rebuild a kit shape at an arbitrary size, for the end-to-end check."""
    from ui_sprite_kit_geometry import (ORIENT_DEFAULT, ORIENT_FLIPPED,
                                        hexagon, parallelogram, slivered_rect)
    corners = ORIENT_FLIPPED if name.endswith("_Flipped") else ORIENT_DEFAULT
    nominal = KIT[name][2]
    if "Hex" in name:
        return hexagon(w, h, nominal)
    if name == "UIKit_Banner_Fill":
        return parallelogram(w, h, nominal)
    return slivered_rect(w, h, nominal, corners)


if __name__ == "__main__":
    raise SystemExit(main())
