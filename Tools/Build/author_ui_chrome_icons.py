#!/usr/bin/env python3
"""
Author the UI CHROME icon set - the small first-party glyphs the menu's buttons wear.

WHY THIS EXISTS. Six shipped UI references pointed at sprites inside
`Assets/NiceVibrations/Demo/`, a plugin's DEMO folder. Asset Store EULAs treat demo content
separately from the plugin, so shipping a paid Steam build on them is a question nobody
should have to answer. These four glyphs replace them (see Docs/THIRD_PARTY_DECISIONS.md
row 5), and they live under a FIRST-PARTY path on purpose: editing the vendor PNG in place
would have worked and would have hidden the change, leaving the asset path still saying
`NiceVibrations/Demo/...` for the next audit to re-report and the next plugin re-import to
silently revert.

STYLE - this is a sibling of `author_objective_icons.py` and deliberately IMPORTS its
drawing engine rather than copying it, so the two sets cannot drift apart on stroke weight,
supersampling, margin or PNG encoding. Docs/STYLE_FOUNDATION.md sections 1.2, 5 and 9:

  * Line-weight monochrome. Pure white with the shape in ALPHA, because every consumer
    tints at runtime (a baked colour would fight the tint).
  * Angular. Zero corner radius anywhere; butt caps, mitred joins.
  * 256x256 with a 24px margin, matching the vendor sub-sprites they replace so no
    RectTransform moves.

NINETEEN glyphs. Five arrived with the vendor swap - FOUR of them for six replaced references, and one consolidation is deliberate. `CancelButton`
(Menu_Main) and `Close Button` (Arcade Screen) wore two DIFFERENT vendor glyphs while
meaning the same thing; the style guide's icon list names one "X Button", so they share
`chrome_close` here. That is the one intentional look change in the swap. `chrome_lock` and
`chrome_crystal` were added for the style-B component kit (Docs/MENU_REDESIGN_B.md, item 0);
`chrome_plus`, `chrome_star` and `chrome_star_fill` for the arcade (item 3): the party panel's open
seat and the game card's favourite star, off and on; `chrome_globe`, `chrome_pin` and
`chrome_people` for the weekly leaderboard's scope tabs (item 4); `chrome_diamond` for a display-name
rule not yet met in the profile window (item 5 - a met rule wears `chrome_confirm`); `chrome_shuffle` for the
Toy Box's shuffle card (item 8); `chrome_gear`, `chrome_display`, `chrome_gauge` and `chrome_sliders` for
the settings window's four tabs (item 9); `chrome_cube`, `chrome_pad`, `chrome_blades` and `chrome_target` for
Home's four hub buttons - Toy Box, Arcade, Arena, Mission (item 6).

Usage:
    python3 Tools/Build/author_ui_chrome_icons.py            # write the PNGs + .meta files
    python3 Tools/Build/author_ui_chrome_icons.py --check    # verify, non-zero on drift
"""

import argparse
import hashlib
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import author_objective_icons as oi          # the shared drawing engine

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(REPO, "Assets", "_Graphics", "UI", "Chrome")

W = oi.W
fill, stroke, ring = oi.fill, oi.stroke, oi.ring


def guid_for(name):
    """Deterministic guid, stable across runs and machines (asset-surgery skill section 3)."""
    return hashlib.md5(f"CosmicShore/UIChromeIcons/{name}".encode()).hexdigest()


# ---------------------------------------------------------------------------
# The glyphs. Normalised space: x,y in [-1,1], y UP.
# ---------------------------------------------------------------------------

def chrome_close():
    """Close / cancel - the X button. Worn by CancelButton and Close Button alike."""
    m = stroke([(-0.74, 0.74), (0.74, -0.74)], w=W * 1.30)
    m |= stroke([(-0.74, -0.74), (0.74, 0.74)], w=W * 1.30)
    return m


def _arrowhead(tip, back, half):
    """A solid triangle: apex at `tip`, base `2*half` wide, centred `back` behind it."""
    tx, ty = tip
    bx, by = back
    dx, dy = bx - tx, by - ty
    ln = math.hypot(dx, dy)
    nx, ny = -dy / ln * half, dx / ln * half
    return fill([(tx, ty), (bx + nx, by + ny), (bx - nx, by - ny)])


def chrome_reroll():
    """Roll again - two hooks chasing each other round a broken rectangle.

    A circular refresh arrow is the obvious glyph and is banned: Style Foundation section 5
    says nothing is rounded. The same statement made out of right angles reads as a cycle
    just as fast, and it matches the slivered, orthogonal language of everything around it.
    """
    hw = W * 1.10
    head = W * 1.45
    # Upper hook, travelling left along the top.
    m = stroke([(0.66, -0.06), (0.66, 0.66), (-0.16, 0.66)], w=hw)
    m |= _arrowhead((-0.62, 0.66), (-0.16, 0.66), head)
    # Lower hook, travelling right along the bottom - the same shape point-reflected.
    m |= stroke([(-0.66, 0.06), (-0.66, -0.66), (0.16, -0.66)], w=hw)
    m |= _arrowhead((0.62, -0.66), (0.16, -0.66), head)
    return m


def chrome_confirm():
    """Confirm / save - a check mark, mitred rather than rounded."""
    return stroke([(-0.70, 0.06), (-0.20, -0.54), (0.72, 0.62)], w=W * 1.30)


def chrome_diamond():
    """A rule not yet met - a small hollow diamond, the open counterpart of the confirm tick in
    the profile window's display-name rules (Docs/MENU_REDESIGN_B.md, item 5)."""
    return ring([(0.00, 0.62), (0.62, 0.00), (0.00, -0.62), (-0.62, 0.00)], w=W * 1.05)


def chrome_shuffle():
    """Shuffle - two crossing paths, each ending in a right-pointing arrowhead (the Toy Box's shuffle
    card, Docs/MENU_REDESIGN_B.md item 8)."""
    m = stroke([(-0.82, 0.50), (-0.36, 0.50), (0.36, -0.50), (0.78, -0.50)], w=W * 1.05)
    m |= stroke([(-0.82, -0.50), (-0.36, -0.50), (0.36, 0.50), (0.78, 0.50)], w=W * 1.05)
    m |= stroke([(0.56, 0.74), (0.80, 0.50), (0.56, 0.26)], w=W * 1.05)
    m |= stroke([(0.56, -0.26), (0.80, -0.50), (0.56, -0.74)], w=W * 1.05)
    return m


def _arc(r, a0, a1, n=12):
    return [(r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), r * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
            for i in range(n + 1)]


def chrome_gear():
    """Settings, General - an octagonal gear: a faceted rim with six square teeth and a hub."""
    m = ring([(0.58 * math.cos(math.radians(22.5 + 45 * i)), 0.58 * math.sin(math.radians(22.5 + 45 * i)))
              for i in range(8)], w=W * 1.05)
    for i in range(6):
        a = math.radians(90 + 60 * i)
        m |= stroke([(0.58 * math.cos(a), 0.58 * math.sin(a)), (0.84 * math.cos(a), 0.84 * math.sin(a))], w=W * 1.6)
    m |= ring([(0.22 * math.cos(math.radians(45 * i)), 0.22 * math.sin(math.radians(45 * i))) for i in range(8)], w=W * 0.95)
    return m


def chrome_display():
    """Settings, Display - a monitor: the screen frame, its neck and its foot."""
    m = ring([(-0.80, 0.62), (0.80, 0.62), (0.80, -0.30), (-0.80, -0.30)], w=W * 1.05)
    m |= stroke([(0.00, -0.30), (0.00, -0.62)], w=W * 1.05)
    m |= stroke([(-0.40, -0.66), (0.40, -0.66)], w=W * 1.05)
    return m


def chrome_gauge():
    """Settings, Performance - a speed gauge: a faceted half dial and its needle."""
    # A filled band, not a stroked polyline: the shared stroke squares off its joints, which reads
    # as knuckles on a curve.
    half = W * 1.05 / 2
    m = fill(_arc(0.78 + half, 200, -20, 24) + _arc(0.78 - half, -20, 200, 24))
    m |= stroke([(0.00, -0.22), (0.44, 0.40)], w=W * 1.15)
    return m


def chrome_sliders():
    """Settings, Other - two slider rails, each with its square knob at a different point."""
    m = stroke([(-0.82, 0.36), (0.82, 0.36)], w=W * 0.95)
    m |= stroke([(-0.82, -0.36), (0.82, -0.36)], w=W * 0.95)
    m |= fill([(0.12, 0.58), (0.44, 0.58), (0.44, 0.14), (0.12, 0.14)])
    m |= fill([(-0.46, -0.14), (-0.14, -0.14), (-0.14, -0.58), (-0.46, -0.58)])
    return m


def chrome_arrow_down():
    """Expand / more - a hollow down triangle, the read the vendor sprite it replaces had."""
    return ring([(-0.80, 0.50), (0.80, 0.50), (0.00, -0.74)], w=W * 1.15)


def chrome_vessel_placeholder():
    """A vessel slot with nothing in it yet - the Termite's class icon.

    Termite (VesselClassType 8) is PLANNED and unimplemented, so its icon slots were already
    standing in for art that does not exist; they just happened to be standing in with a
    plugin's demo car and dot-cluster. This says the same thing in the project's own shape
    language: the corner sliver (Style Foundation section 5) around an empty centre, with a
    diamond so it reads as deliberate placeholder art rather than a failed texture load.
    Replacing it is a DESIGN task, not a licence one.
    """
    c, v = 0.74, 0.30                                    # half-extent, sliver depth
    m = ring([(-c + v, c), (c, c), (c, -c + v),          # top-left and bottom-right cut
              (c - v, -c), (-c, -c), (-c, c - v)], w=W * 1.05)
    m |= ring([(0.00, 0.26), (0.26, 0.00), (0.00, -0.26), (-0.26, 0.00)], w=W * 0.95)
    return m


def chrome_lock():
    """Locked - a padlock built from right angles (section 5: nothing is rounded), so the
    shackle is a squared arch rather than the usual hoop. Worn by the style-B kit's locked
    tiles and the locked top-nav tab (Docs/MENU_REDESIGN_B.md, item 0)."""
    m = stroke([(-0.40, -0.02), (-0.40, 0.70), (0.40, 0.70), (0.40, -0.02)], w=W * 1.10)
    m |= ring([(-0.66, 0.02), (0.66, 0.02), (0.66, -0.74), (-0.66, -0.74)], w=W * 1.10)
    m |= stroke([(0.00, -0.24), (0.00, -0.50)], w=W * 1.10)
    return m


def chrome_crystal():
    """Currency - a cut gem: a crown over a long pavilion, the girdle drawn across it.
    The currency bar and Purchase buttons wear it, tinted the purchase role (section 3)."""
    m = ring([(0.00, 0.80), (0.68, 0.24), (0.00, -0.80), (-0.68, 0.24)], w=W * 1.05)
    m |= stroke([(-0.62, 0.24), (0.62, 0.24)], w=W * 0.95)
    return m


def chrome_plus():
    """Add - the open party seat's invite glyph. Butt-capped, like the X it pairs with."""
    m = stroke([(-0.70, 0.00), (0.70, 0.00)], w=W * 1.30)
    m |= stroke([(0.00, -0.70), (0.00, 0.70)], w=W * 1.30)
    return m


def _star(outer=0.84, inner=0.36):
    """A five-point star, point up - straight edges and sharp points (section 5)."""
    return [((outer if k % 2 == 0 else inner) * math.cos(math.pi / 2 + k * math.pi / 5),
             (outer if k % 2 == 0 else inner) * math.sin(math.pi / 2 + k * math.pi / 5) - 0.04)
            for k in range(10)]


def chrome_star():
    """Favourite, off - the star's outline."""
    return ring(_star(), w=W * 1.00)


def chrome_star_fill():
    """Favourite, on - the same star, solid, so on and off share one silhouette."""
    return fill(_star())


def _octagon(r, cx=0.0, cy=0.0):
    return [(cx + r * math.cos(math.pi / 8 + k * math.pi / 4), cy + r * math.sin(math.pi / 8 + k * math.pi / 4))
            for k in range(8)]


def chrome_globe():
    """World scope - an octagonal globe (section 5: no circles) with its equator and a
    meridian lens. The weekly leaderboard's World tab (item 4)."""
    m = ring(_octagon(0.80), w=W * 1.05)
    m |= stroke([(-0.78, 0.00), (0.78, 0.00)], w=W * 0.90)
    m |= ring([(0.00, 0.72), (0.32, 0.38), (0.32, -0.38), (0.00, -0.72), (-0.32, -0.38), (-0.32, 0.38)],
              w=W * 0.90)
    return m


def chrome_pin():
    """Regional scope - a map pin cut from straight edges: a faceted head over a point, with
    a hollow core. The weekly leaderboard's Regional tab (item 4)."""
    m = ring([(0.00, -0.84), (-0.56, 0.06), (-0.56, 0.46), (-0.22, 0.80), (0.22, 0.80),
              (0.56, 0.46), (0.56, 0.06)], w=W * 1.05)
    m |= ring(_octagon(0.20, 0.0, 0.36), w=W * 0.90)
    return m


def chrome_people():
    """Friends scope - two pilots, heads and shoulders, the rear one offset. The weekly
    leaderboard's Friends tab (item 4)."""
    m = ring(_octagon(0.24, -0.20, 0.30), w=W * 0.95)
    m |= stroke([(-0.68, -0.76), (-0.68, -0.24), (-0.40, -0.04), (0.00, -0.04), (0.28, -0.24), (0.28, -0.76)],
                w=W * 0.95)
    m |= stroke([(0.22, 0.62), (0.40, 0.70), (0.58, 0.54), (0.58, 0.28), (0.40, 0.10)], w=W * 0.90)
    m |= stroke([(0.50, -0.04), (0.72, -0.04), (0.84, -0.20), (0.84, -0.60)], w=W * 0.90)
    return m


def chrome_cube():
    """Home, Toy Box - an isometric cube: the hexagon outline and the three edges meeting at its front corner."""
    hexa = [(0.00, 0.86), (0.74, 0.44), (0.74, -0.44), (0.00, -0.86), (-0.74, -0.44), (-0.74, 0.44)]
    m = ring(hexa, w=W * 1.05)
    m |= stroke([(-0.74, 0.44), (0.00, 0.02), (0.74, 0.44)], w=W * 1.0)
    m |= stroke([(0.00, 0.02), (0.00, -0.86)], w=W * 1.0)
    return m


def chrome_pad():
    """Home, Arcade - a gamepad: a faceted body, the d-pad cross on the left and two buttons on the right."""
    m = ring([(-0.86, 0.10), (-0.62, 0.40), (0.62, 0.40), (0.86, 0.10), (0.86, -0.30), (0.56, -0.52),
              (0.30, -0.24), (-0.30, -0.24), (-0.56, -0.52), (-0.86, -0.30)], w=W * 1.0)
    m |= stroke([(-0.50, 0.08), (-0.18, 0.08)], w=W * 0.9)
    m |= stroke([(-0.34, 0.24), (-0.34, -0.08)], w=W * 0.9)
    m |= fill([(0.26, 0.20), (0.40, 0.20), (0.40, 0.06), (0.26, 0.06)])
    m |= fill([(0.46, 0.02), (0.60, 0.02), (0.60, -0.12), (0.46, -0.12)])
    return m


def chrome_blades():
    """Home, Arena - two crossed blades, each with its cross-guard."""
    m = stroke([(-0.74, 0.78), (0.42, -0.38)], w=W * 1.05)
    m |= stroke([(0.74, 0.78), (-0.42, -0.38)], w=W * 1.05)
    m |= stroke([(0.22, -0.56), (0.60, -0.18)], w=W * 1.0)
    m |= stroke([(-0.22, -0.56), (-0.60, -0.18)], w=W * 1.0)
    m |= stroke([(0.50, -0.46), (0.80, -0.76)], w=W * 1.0)
    m |= stroke([(-0.50, -0.46), (-0.80, -0.76)], w=W * 1.0)
    return m


def chrome_target():
    """Home, Mission - a target: two octagonal rings (section 5: no circles) and four ticks."""
    m = ring(_octagon(0.62), w=W * 1.0)
    m |= ring(_octagon(0.26), w=W * 0.95)
    for a, b in (((0, 0.70), (0, 0.92)), ((0, -0.70), (0, -0.92)), ((0.70, 0), (0.92, 0)), ((-0.70, 0), (-0.92, 0))):
        m |= stroke([a, b], w=W * 1.0)
    return m


GLYPHS = [
    ("chrome_close",      chrome_close),
    ("chrome_reroll",     chrome_reroll),
    ("chrome_confirm",    chrome_confirm),
    ("chrome_arrow_down", chrome_arrow_down),
    ("chrome_vessel_placeholder", chrome_vessel_placeholder),
    ("chrome_lock",       chrome_lock),
    ("chrome_crystal",    chrome_crystal),
    ("chrome_plus",       chrome_plus),
    ("chrome_star",       chrome_star),
    ("chrome_star_fill",  chrome_star_fill),
    ("chrome_globe",      chrome_globe),
    ("chrome_pin",        chrome_pin),
    ("chrome_people",     chrome_people),
    ("chrome_diamond",    chrome_diamond),
    ("chrome_shuffle",    chrome_shuffle),
    ("chrome_gear",       chrome_gear),
    ("chrome_display",    chrome_display),
    ("chrome_gauge",      chrome_gauge),
    ("chrome_sliders",    chrome_sliders),
    ("chrome_cube",       chrome_cube),
    ("chrome_pad",        chrome_pad),
    ("chrome_blades",     chrome_blades),
    ("chrome_target",     chrome_target),
]


def build():
    """name -> (png bytes, meta text), with the same invariants the objective set asserts."""
    out = {}
    for name, fn in GLYPHS:
        mask = fn()
        assert mask is not None, f"{name}: {fn.__name__}() returned None - truncated body?"
        rgba = oi.render(mask)
        a = rgba[..., 3]
        M = oi.MARGIN
        assert a[:M].max() == 0 and a[-M:].max() == 0, f"{name}: artwork in v-margin"
        assert a[:, :M].max() == 0 and a[:, -M:].max() == 0, f"{name}: artwork in h-margin"
        ink = float((a > 0).mean())
        assert 0.02 < ink < 0.60, f"{name}: implausible ink coverage {ink:.3f}"
        out[name] = (oi.encode_png(rgba),
                     oi.META.format(guid=guid_for(name), sprite_id=guid_for(name + "/sprite")))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="verify on-disk output, non-zero on drift")
    args = ap.parse_args()

    built = build()
    folder_meta = oi.FOLDER_META.format(guid=guid_for("__folder__"))

    if args.check:
        drift = []
        if not os.path.isdir(OUT_DIR):
            print(f"FAIL missing {OUT_DIR}")
            return 1
        got = open(OUT_DIR + ".meta", "rb").read() if os.path.exists(OUT_DIR + ".meta") else b""
        if got != folder_meta.encode():
            drift.append("Chrome.meta")
        for name, (png, meta) in built.items():
            for ext, want in ((".png", png), (".png.meta", meta.encode())):
                p = os.path.join(OUT_DIR, name + ext)
                have = open(p, "rb").read() if os.path.exists(p) else b""
                if have != want:
                    drift.append(name + ext)
        for d in drift:
            print(f"DRIFT {d}")
        print(("FAIL: %d file(s) differ - re-run without --check" % len(drift)) if drift
              else "OK: %d UI chrome icons match" % len(built))
        return 1 if drift else 0

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "w", newline="\n") as f:
        f.write(folder_meta)
    for name, (png, meta) in built.items():
        with open(os.path.join(OUT_DIR, name + ".png"), "wb") as f:
            f.write(png)
        with open(os.path.join(OUT_DIR, name + ".png.meta"), "w", newline="\n") as f:
            f.write(meta)
        print(f"wrote {name}.png  guid={guid_for(name)}")
    print(f"\n{len(built)} icons -> {os.path.relpath(OUT_DIR, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
