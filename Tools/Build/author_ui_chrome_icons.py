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

FIVE glyphs, FOUR of them for six replaced references, and one consolidation is deliberate. `CancelButton`
(Menu_Main) and `Close Button` (Arcade Screen) wore two DIFFERENT vendor glyphs while
meaning the same thing; the style guide's icon list names one "X Button", so they share
`chrome_close` here. That is the one intentional look change in the swap.

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


GLYPHS = [
    ("chrome_close",      chrome_close),
    ("chrome_reroll",     chrome_reroll),
    ("chrome_confirm",    chrome_confirm),
    ("chrome_arrow_down", chrome_arrow_down),
    ("chrome_vessel_placeholder", chrome_vessel_placeholder),
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
