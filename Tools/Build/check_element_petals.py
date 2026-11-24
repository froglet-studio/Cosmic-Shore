#!/usr/bin/env python3
"""The element petals point into the flower's centre - the gate that keeps them there.

Every element flower (the match HUD's ElementalBarsView, the menu's KitElementFlower, the Fleet Codex, the toy
portraits, the icon kit's sheets) is ONE petal sprite drawn five times, stretched over the flower and turned 72 degrees
about the sprite's centre. So the sprite itself carries the layout: the petal sits in the upper half with its apex at
the centre, and an inverted or shrunken sprite redraws every flower in the game with nothing in any code diff.

That happened to Mass: on 2026-10-27 (3ee3213503) mass_petal.png was flipped and set at 60 % alongside the Mass label
icon's flip, so the flower read as a star of five outward triangles with a hole in the middle, and every surface above
drew it for four weeks. Garrett restored it on 2026-11-22: "the petals on the mass flower got flipped out of the
center many weeks ago. please restore it ... harden so that mistake does not come back."

For each Assets/Resources/ElementPetals/<element>_petal.png this checks:

* APEX AT THE CENTRE. The opaque pixel nearest the sprite's centre is within APEX_REACH of it, and the row there is a
  point (the apex), not an edge. An inverted petal has its wide edge nearest the centre and fails both.
* IN THE UPPER HALF. No opaque pixel below the centre row, so the 72-degree turns fan out and never overlap.
* FULL SIZE. The farthest opaque pixel reaches OUTER_REACH of the sprite's side: a petal set at 60 % falls short.
* THE TRACED SHAPE. The sprite matches its outline in ElementPetalGeometry.cs (ScopePetalGeometry.cs for Charge),
  rasterised into the sprite's own bounding box the way that file fits it (longer side 0..1, shorter centred, y up,
  apex at the bottom), at IoU >= MIN_IOU. A petal swapped for another element's, or redrawn upside down inside a
  symmetric box, fails here. To change a petal's shape ON PURPOSE, change the sprite AND its outline in that file in
  the same commit: the outline is what the Serpent's and the Shrike's scopes cut their window to, so the two must
  agree anyway.

Standard library only (a small PNG reader), so it runs on the guard's bare runner.

    python3 Tools/Build/check_element_petals.py              # the gate
    python3 Tools/Build/check_element_petals.py --self-test  # negative controls, including the 2026-10-27 mistake
"""
import os
import re
import struct
import sys
import zlib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
PETALS = os.path.join(ROOT, 'Assets', 'Resources', 'ElementPetals')
GEOMETRY = os.path.join(ROOT, 'Assets', '_Scripts', 'UI', 'View', 'ElementPetalGeometry.cs')
SCOPE_GEOMETRY = os.path.join(ROOT, 'Assets', '_Scripts', 'UI', 'View', 'ScopePetalGeometry.cs')
ELEMENTS = ('Charge', 'Mass', 'Space', 'Time')

APEX_REACH = 0.07    # of the side: the shipped apexes sit 11 px from the centre of 256 (0.043)
APEX_WIDTH = 0.03    # of the side: the row nearest the centre is a point (1-2 px of 256)
OUTER_REACH = 0.40   # of the side: the shipped petals all reach 0.42; the 60 % Mass reached 0.38
MIN_IOU = 0.90       # the shipped petals match their traced outlines at 0.96-1.00; the flipped Mass at 0.34


# ---- PNG -------------------------------------------------------------------------------------------------------------

def read_png_alpha(path):
    """(width, height, rows of alpha 0..255) for an 8-bit PNG (greyscale, grey+alpha, RGB or RGBA; not interlaced)."""
    data = open(path, 'rb').read()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError(f'{path}: not a PNG')
    pos, idat, w = 8, b'', None
    while pos < len(data):
        length, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if kind == b'IHDR':
            w, h, depth, colour, _, _, interlace = struct.unpack('>IIBBBBB', body)
            if depth != 8 or interlace or colour not in (0, 2, 4, 6):
                raise ValueError(f'{path}: unsupported PNG (depth {depth}, colour {colour}, interlace {interlace})')
        elif kind == b'IDAT':
            idat += body
        elif kind == b'IEND':
            break
    bpp = {0: 1, 2: 3, 4: 2, 6: 4}[colour]
    raw = zlib.decompress(idat)
    stride = w * bpp
    prev = bytearray(stride)
    rows = []
    for y in range(h):
        f = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if f == 1:
                line[i] = (line[i] + a) & 255
            elif f == 2:
                line[i] = (line[i] + b) & 255
            elif f == 3:
                line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        prev = line
        if colour in (4, 6):
            rows.append([line[x * bpp + bpp - 1] for x in range(w)])
        else:
            rows.append([255] * w)
    return w, h, rows


# ---- the traced outlines ---------------------------------------------------------------------------------------------

VEC = re.compile(r'new(?:\s+Vector2)?\(\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*\)')


def outlines():
    """{element: [(x, y), ...]} from ElementPetalGeometry.cs (Mass, Space, Time) and ScopePetalGeometry.cs (Charge)."""
    src = open(GEOMETRY, encoding='utf-8-sig').read()
    out = {}
    for el in ('Mass', 'Space', 'Time'):
        m = re.search(r'static readonly Vector2\[\]\s+' + el + r'\s*=\s*\{(.*?)\};', src, re.S)
        if not m:
            raise ValueError(f'ElementPetalGeometry.cs: no {el} outline')
        out[el] = [(float(a), float(b)) for a, b in VEC.findall(m.group(1))]
    scope = open(SCOPE_GEOMETRY, encoding='utf-8-sig').read()
    m = re.search(r'Vector2\[\]\s+Outline\s*=\s*\{(.*?)\};', scope, re.S)
    if not m:
        raise ValueError('ScopePetalGeometry.cs: no Outline')
    out['Charge'] = [(float(a), float(b)) for a, b in VEC.findall(m.group(1))]
    return out


def inside(poly, x, y):
    hit = False
    for i in range(len(poly)):
        (x1, y1), (x2, y2) = poly[i], poly[(i + 1) % len(poly)]
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            hit = not hit
    return hit


# ---- the checks ------------------------------------------------------------------------------------------------------

def mask_of(rows):
    return [[a >= 128 for a in r] for r in rows]


def check_mask(element, mask, outline):
    """Every way this petal mask breaks the flower, as sentences (empty: it is right)."""
    n = len(mask)
    w = len(mask[0])
    cx, cy = (w - 1) / 2.0, (n - 1) / 2.0
    pts = [(x, y) for y in range(n) for x in range(w) if mask[y][x]]
    if not pts:
        return [f'{element}: the petal sprite is empty']
    errs = []
    near = min(pts, key=lambda p: (p[0] - cx) ** 2 + (p[1] - cy) ** 2)
    far = max(((p[0] - cx) ** 2 + (p[1] - cy) ** 2) ** 0.5 for p in pts)
    near_d = ((near[0] - cx) ** 2 + (near[1] - cy) ** 2) ** 0.5
    if near_d > APEX_REACH * n:
        errs.append(f'{element}: nothing of the petal comes within {APEX_REACH * n:.0f} px of the flower centre '
                    f'(nearest {near_d:.0f} px) - its apex must point INTO the centre (is it flipped?)')
    width = sum(1 for x in range(w) if mask[near[1]][x])
    if width > APEX_WIDTH * n:
        errs.append(f'{element}: the petal\'s edge nearest the centre is {width} px wide - the end at the centre must '
                    f'be the apex, a point (is it flipped?)')
    below = sum(1 for _, y in pts if y > cy + 0.5)
    if below:
        errs.append(f'{element}: {below} px of the petal lie below the centre row - the petal must sit in the upper '
                    f'half so the 72-degree turns fan out')
    if far < OUTER_REACH * n:
        errs.append(f'{element}: the petal reaches only {far / n:.2f} of the sprite from the centre (at least '
                    f'{OUTER_REACH:.2f}) - it has been shrunk')
    # The traced outline, fitted into the petal's bounding box as ElementPetalGeometry fits it.
    x0, x1 = min(p[0] for p in pts), max(p[0] for p in pts) + 1
    y0, y1 = min(p[1] for p in pts), max(p[1] for p in pts) + 1
    side = max(x1 - x0, y1 - y0)
    ox, oy = x0 - (side - (x1 - x0)) / 2.0, y0 - (side - (y1 - y0)) / 2.0
    inter = union = 0
    for y in range(y0, y1):
        for x in range(x0, x1):
            u, v = (x + 0.5 - ox) / side, 1.0 - (y + 0.5 - oy) / side   # y up, as the outline is
            a, b = mask[y][x], inside(outline, u, v)
            inter += a and b
            union += a or b
    iou = inter / union if union else 0.0
    if iou < MIN_IOU:
        errs.append(f'{element}: the sprite matches its traced outline (ElementPetalGeometry / ScopePetalGeometry) '
                    f'at IoU {iou:.2f}, under {MIN_IOU} - it is upside down, another element\'s petal, or redrawn '
                    f'without updating its outline')
    return errs


def check(petal_dir=PETALS):
    lines = outlines()
    errs = []
    for el in ELEMENTS:
        path = os.path.join(petal_dir, f'{el.lower()}_petal.png')
        if not os.path.isfile(path):
            errs.append(f'{el}: {os.path.relpath(path, ROOT)} is missing')
            continue
        w, h, rows = read_png_alpha(path)
        if w != h:
            errs.append(f'{el}: the petal sprite is {w}x{h} - it must be square, turned about its centre')
            continue
        errs += check_mask(el, mask_of(rows), lines[el])
    return errs


# ---- negative controls -----------------------------------------------------------------------------------------------

def flip_in_box(mask):
    """Mirror the petal top-to-bottom inside its own bounding box (it stays in the upper half, apex now outward)."""
    n = len(mask)
    ys = [y for y in range(n) if any(mask[y])]
    y0, y1 = ys[0], ys[-1]
    out = [[False] * len(mask[0]) for _ in range(n)]
    for y in range(y0, y1 + 1):
        out[y0 + y1 - y] = list(mask[y])
    return out


def scale_about_box(mask, k):
    n, w = len(mask), len(mask[0])
    pts = [(x, y) for y in range(n) for x in range(w) if mask[y][x]]
    cx = (min(p[0] for p in pts) + max(p[0] for p in pts)) / 2.0
    cy = (min(p[1] for p in pts) + max(p[1] for p in pts)) / 2.0
    return [[0 <= (sx := round(cx + (x - cx) / k)) < w and 0 <= (sy := round(cy + (y - cy) / k)) < n
             and mask[sy][sx] for x in range(w)] for y in range(n)]


def self_test():
    lines = outlines()
    masks = {el: mask_of(read_png_alpha(os.path.join(PETALS, f'{el.lower()}_petal.png'))[2]) for el in ELEMENTS}
    failures = 0

    def expect(name, errs, want_fail):
        nonlocal failures
        ok = bool(errs) == want_fail
        failures += not ok
        print(f'  {"ok  " if ok else "FAIL"} {name}: {"caught - " + errs[0] if errs else "passes"}')

    for el in ELEMENTS:
        expect(f'positive control: the shipped {el} petal', check_mask(el, masks[el], lines[el]), False)
    m = masks['Mass']
    expect('the 2026-10-27 mistake: Mass flipped and set at 60 %',
           check_mask('Mass', scale_about_box(flip_in_box(m), 0.6), lines['Mass']), True)
    expect('Mass flipped in place', check_mask('Mass', flip_in_box(m), lines['Mass']), True)
    expect('Mass shrunk to 60 %', check_mask('Mass', scale_about_box(m, 0.6), lines['Mass']), True)
    expect('Mass turned 180 degrees (into the lower half)', check_mask('Mass', [r[::-1] for r in m[::-1]],
                                                                       lines['Mass']), True)
    expect('the Time petal shipped as Mass', check_mask('Mass', masks['Time'], lines['Mass']), True)
    expect('Charge flipped in place', check_mask('Charge', flip_in_box(masks['Charge']), lines['Charge']), True)
    expect('an empty sprite', check_mask('Space', [[False] * 256 for _ in range(256)], lines['Space']), True)
    print('check_element_petals self-test: ' + ('OK' if not failures else f'{failures} FAILED'))
    return 1 if failures else 0


def main(argv):
    if '--self-test' in argv:
        return self_test()
    errs = check()
    for e in errs:
        print('FAIL ' + e)
    print('check_element_petals: ' + ('OK (4 petals point into the centre, full size, matching their outlines)'
                                      if not errs else f'{len(errs)} finding(s)'))
    return 1 if errs else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
