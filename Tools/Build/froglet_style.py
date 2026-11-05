#!/usr/bin/env python3
"""froglet_style.py - the Froglet design tokens every surface shares: the game's UI, Frogma boards, artifacts, decks,
diagrams, the website and the Fleet Codex. Docs/UI_STYLE_GUIDE.md is the guide; this script writes its tokens.

It READS the game, so the tokens never drift from what players see:
  - Assets/_SO_Assets/UI/UITheme.asset              the UI's chrome and family colours (UIThemeSO)
  - the live colour set (ThemeManagerDataContainer.ColorSet)   the three domain colours
  - Docs/VesselModels/colour_domains.json           Garrett's six colour families (through colour_domains.py)
  - Docs/STYLE_FOUNDATION.md section 4/5/7          the type scale, the sliver, spacing and motion (the numbers below)

and WRITES:
  - Docs/UI/froglet-tokens.json   every theme's tokens, for tools and generators
  - Docs/UI/froglet.css           the same as CSS custom properties, for any HTML page (artifacts, decks, the website)
  - Docs/UI/colour-wheel.png      Garrett's chart as the Colour Domains lab draws it (two HSV discs, the band hatched),
                                  with colour-wheel.json (where each family's label sits); shown on Frogma's Style Guide

    python3 Tools/Build/froglet_style.py            write both files
    python3 Tools/Build/froglet_style.py --check    exit 1 when a file is stale or a rule below fails
    python3 Tools/Build/froglet_style.py --self-test

--check fails when:
  - a written file differs from what the game's assets give now (naming the file);
  - a theme's family colour does not rest in its family, clear of the forbidden band (colour_domains.json), except the
    listed open decisions;
  - a theme's text or family colour misses its contrast on that theme's ground (4.5:1 for text, 3:1 for a line or an
    icon; gold on the light theme is a FILL under dark text, never text);
  - a palette shade (the colours a designer may choose inside each family) is not clear inside its family.

THEMES. Deep Sea is the game's own look and the default everywhere. Shore is its light twin, for documents, print and
daylight web pages. A domain (Jade, Ruby, Gold) is never a theme: it is one accent a page adds with data-fs-domain.
"""
from __future__ import annotations

import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(REPO, ".claude", "skills", "vessel-studio"))
import colour_domains as cd  # noqa: E402
import domain_colors  # noqa: E402

UITHEME = os.path.join(REPO, "Assets", "_SO_Assets", "UI", "UITheme.asset")
OUT_JSON = os.path.join(REPO, "Docs", "UI", "froglet-tokens.json")
OUT_WHEEL = os.path.join(REPO, "Docs", "UI", "colour-wheel.png")
WHEEL_SIZE = 520           # px per disc; two discs (value 1.0 and 0.55) side by side
WHEEL_VALUES = (1.0, 0.55)
PALETTE_L = (0.92, 0.84, 0.76, 0.68, 0.60, 0.52, 0.44, 0.36, 0.28)   # OKLab lightness steps, light to dark
PALETTE_CLEAR = 0.035      # a palette colour sits at least this far (OKLab) inside its family, past the band (0.025)
OUT_CSS = os.path.join(REPO, "Docs", "UI", "froglet.css")

# Open decisions recorded in Docs/UI_STYLE_GUIDE.md (D-numbers there); --check reports these but does not fail.
OPEN: dict = {
    # Garrett's call (the palette's owner): Nocturne, the default palette since 2026-11-03, draws the Gold domain at
    # #FF8F11, inside the forbidden band between Gold and Fire on his chart. Reported, not failed; Docs/PALETTE.md.
    ("deep-sea", "gold"): "Nocturne's Gold domain sits in the band between Gold and Fire (Garrett, Docs/PALETTE.md)",
    ("deep-sea", "gold-fill"): "Nocturne's Gold domain sits in the band between Gold and Fire (Garrett, Docs/PALETTE.md)",
}

# The numbers STYLE_FOUNDATION.md owns (v0.3.3, sections 4, 5 and 7). Change them there first, then here.
TYPE = {  # PC @1920 canvas units; the CSS uses them as px at a 1920-wide page
    "display": 48, "h1": 36, "h2": 28, "h3": 22, "body": 18, "body-small": 15,
    "button": 18, "button-small": 14, "data-large": 44, "data": 20, "data-small": 15,
}
SPACE = [4, 8, 12, 16, 24, 32, 48, 64, 96]
SHAPE = {"sliver-large": 14, "sliver-small": 10, "hairline": 1, "stroke": 2}
MOTION = {"micro": (120, "cubic-bezier(0.5, 1, 0.89, 1)"), "std": (200, "cubic-bezier(0.33, 1, 0.68, 1)"),
          "panel": (320, "cubic-bezier(0.22, 1, 0.36, 1)"), "ceremony": (500, "cubic-bezier(0.34, 1.56, 0.64, 1)")}

# Shore, the light twin: chosen inside each family (colour_domains classify, clear of the band) with 4.5:1 on its
# ground. Gold cannot reach 4.5:1 on a light ground inside its family, so on Shore gold is only ever a fill.
SHORE = {
    "ground": "#F4F5FF", "ground-deep": "#E6E9FF", "surface": "#FFFFFF", "surface-raised": "#DDE1F7",
    "line": "#B8C0F0", "text": "#00041F", "text-soft": "#434C89", "text-off": "#8A8FAE",
    "cta": "#487C1F", "cta-fill": "#99FF80", "danger": "#CD373A", "purchase-fill": "#FFAE00",
    "control": "#007D73", "tooltip": "#893BFF", "selection": "#007D73", "commit": "#893BFF",
    "commit-fill": "#893BFF", "on-commit": "#FFFFFF",
    "jade": "#007D73", "ruby": "#893BFF", "gold-fill": "#FFA700", "on-fill": "#00041F",
}

FAMILY_OF = {"cta": "lime", "cta-fill": "lime", "danger": "fire", "purchase": "gold", "purchase-fill": "gold",
             "control": "jade", "tooltip": "ruby", "selection": "jade", "commit": "ruby", "commit-fill": "ruby",
             "jade": "jade", "ruby": "ruby", "gold": "gold", "gold-fill": "gold"}
TEXT_TOKENS = ("text", "text-soft")                       # must read at 4.5:1 on the ground
MARK_TOKENS = ("cta", "danger", "control", "tooltip", "selection", "commit", "jade", "ruby", "gold", "purchase")  # 3:1 at least
FILL_TOKENS = ("cta-fill", "purchase-fill", "gold-fill")   # carry "on-fill" text at 4.5:1
LIGHT_ON_FILL = ("commit-fill",)                           # carry the theme's light text ("on-commit") at 4.5:1


def hx(h: str) -> tuple:
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def to_hex(rgb) -> str:
    return "#%02X%02X%02X" % tuple(max(0, min(255, round(v * 255))) for v in rgb)


def luminance(rgb) -> float:
    f = lambda v: v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4  # noqa: E731
    r, g, b = (f(v) for v in rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a: str, b: str) -> float:
    la, lb = sorted((luminance(hx(a)), luminance(hx(b))), reverse=True)
    return (la + 0.05) / (lb + 0.05)


def uitheme(path: str = UITHEME) -> dict:
    out = {}
    for m in re.finditer(r"^  (\w+): \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: [\d.]+\}", open(path, encoding="utf-8").read(), re.M):
        out[m.group(1)] = to_hex(tuple(float(m.group(i)) for i in (2, 3, 4)))
    return out


def live_color_set() -> str:
    """The palette the game runs on: the SO_ColorSet ThemeManagerDataContainer.ColorSet names, found by its guid (the
    same rule fleet_codex.py uses), so switching the default colour set (Photic -> Nocturne, 2026-11-03) moves the
    tokens with the game."""
    container = open(os.path.join(REPO, "Assets", "_SO_Assets", "ThemeManagerDataContainer.asset"), encoding="utf-8").read()
    guid = re.search(r"^  ColorSet: \{fileID: \d+, guid: (\w+)", container, re.M).group(1)
    palettes = os.path.join(REPO, "Assets", "_SO_Assets", "Color Palettes")
    for name in sorted(os.listdir(palettes)):
        if name.endswith(".asset.meta") and re.search(rf"^guid: {guid}$", open(os.path.join(palettes, name), encoding="utf-8").read(), re.M):
            return os.path.join(palettes, name[:-len(".meta")])
    raise SystemExit(f"froglet_style: ThemeManagerDataContainer names ColorSet guid {guid}, and no palette has it")


def domains() -> dict:
    cols = domain_colors.asset_colors(open(live_color_set(), encoding="utf-8").read())
    return {k: to_hex(v) for k, v in cols.items() if k in ("jade", "ruby", "gold")}


# ------------------------------------------------------------------ the families' palettes and the colour wheel

def oklab_to_srgb(L: float, a: float, b: float):
    """Inverse OKLab (Ottosson). Returns (r, g, b) in sRGB 0..1, or None when the colour is outside the screen's gamut."""
    l_ = L + 0.3963377774 * a + 0.2158037573 * b
    m_ = L - 0.1055613458 * a - 0.0638541728 * b
    s_ = L - 0.0894841775 * a - 1.2914855480 * b
    l, m, s = l_ ** 3, m_ ** 3, s_ ** 3
    lin = (4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
           -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
           -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s)
    if any(v < -1e-4 or v > 1 + 1e-4 for v in lin):
        return None
    g = lambda v: 12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055  # noqa: E731
    return tuple(min(1.0, max(0.0, g(max(0.0, v)))) for v in lin)


def palettes(defs: dict | None = None) -> dict:
    """For each family, the most colourful shade at each lightness step that sits clear inside it (PALETTE_CLEAR),
    near the family's signal hue: the colours a designer may choose from. Blue adds its greys."""
    import math
    defs = defs or cd.load()
    out = {}
    for fam, meta in defs["families"].items():
        sig = next(a for a in meta["anchors"] if a["id"] == meta["signal"])
        L0, a0, b0 = cd.kite.oklab(hx(sig["hex"]))
        h0 = math.degrees(math.atan2(b0, a0))
        shades = []
        for L in PALETTE_L:
            best = None
            # Stay as close to the family's own hue as possible: the first hue offset (smallest first) that gives a
            # shade clear inside the family wins, at the strongest chroma that hue allows.
            for dh in sorted(range(-36, 37, 2), key=abs):
                h = math.radians(h0 + dh)
                for ci in range(40, 0, -1):
                    C = ci * 0.009
                    rgb = oklab_to_srgb(L, C * math.cos(h), C * math.sin(h))
                    if rgb is None:
                        continue
                    rgb = hx(to_hex(rgb))  # judge the colour as it will be written (rounded to 8 bits)
                    got, _, dist = cd.classify(defs, rgb)
                    if got == fam and dist >= PALETTE_CLEAR:
                        best = (C, dh, rgb)
                        break
                if best and best[0] >= 0.05:
                    break
                best = best if best and best[0] >= 0.05 else None
            if best:
                shades.append({"name": f"{fam}-{round(L * 100)}", "hex": to_hex(best[2]), "L": L})
        if fam == "blue":
            for L in (0.96, 0.80, 0.64, 0.48, 0.32, 0.18):
                rgb = oklab_to_srgb(L, -0.004, -0.018)
                if rgb and cd.classify(defs, rgb)[0] == "blue" and cd.classify(defs, rgb)[2] >= PALETTE_CLEAR:
                    shades.append({"name": f"grey-{round(L * 100)}", "hex": to_hex(rgb), "L": L})
        out[fam] = shades
    return out


def wheel_png(path: str, defs: dict | None = None) -> dict:
    """Renders Garrett's chart the way the Colour Domains lab draws it (an HSV disc: hue clockwise from red at the top,
    saturation outward), one disc per value in WHEEL_VALUES, each pixel in its own colour inside its family and hatched
    dark in the forbidden band. Returns each family's label position per disc (fractions of the image)."""
    import numpy as np
    from PIL import Image
    defs = defs or cd.load()
    fams = list(defs["cells"])
    N = np.array([defs["cells"][f]["n"] for f in fams]); C = np.array([defs["cells"][f]["c"] for f in fams])
    W = WHEEL_SIZE
    img = np.zeros((W, W * len(WHEEL_VALUES), 4), dtype=np.uint8)
    yy, xx = np.mgrid[0:W, 0:W] + 0.5
    cx = cy = W / 2; R = W / 2 - 4
    dx, dy = xx - cx, yy - cy; r = np.hypot(dx, dy)
    hue = (np.degrees(np.arctan2(dx, -dy)) + 360) % 360; sat = np.clip(r / R, 0, 1)
    labels = {}
    for k, V in enumerate(WHEEL_VALUES):
        h6 = hue / 60.0; i = np.floor(h6).astype(int) % 6; f = h6 - np.floor(h6)
        p, q, t = V * (1 - sat), V * (1 - sat * f), V * (1 - sat * (1 - f))
        rgb = np.zeros((W, W, 3))
        for idx, (a, b, c) in enumerate([(V, t, p), (q, V, p), (p, V, t), (p, q, V), (t, p, V), (V, p, q)]):
            m = i == idx
            for ch, arr in enumerate((a, b, c)):
                rgb[..., ch][m] = (arr if np.ndim(arr) else np.full((W, W), arr))[m]
        lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
        lms = np.cbrt(np.stack([lin @ np.array([0.4122214708, 0.5363325363, 0.0514459929]),
                                lin @ np.array([0.2119034982, 0.6806995451, 0.1073969566]),
                                lin @ np.array([0.0883024619, 0.2817188376, 0.6299787005])], -1))
        lab = np.stack([lms @ np.array([0.2104542553, 0.7936177850, -0.0040720468]),
                        lms @ np.array([1.9779984951, -2.4285922050, 0.4505937099]),
                        lms @ np.array([0.0259040371, 0.7827717662, -0.8086757660])], -1)
        score = lab @ N.T + C
        top = score.argmax(-1)
        dist = np.full((W, W), np.inf)
        for g in range(len(fams)):
            dn = np.linalg.norm(N[top] - N[g], axis=-1)
            d = (np.take_along_axis(score, top[..., None], -1)[..., 0] - score[..., g]) / np.where(dn == 0, 1, dn)
            dist = np.where(top == g, dist, np.minimum(dist, d))
        band = dist < defs["margin"] / 2
        out = (rgb * 255).astype(float)
        ground = np.array([3, 5, 26], float)
        hatch = ((xx + yy) % 9) < 2
        out[band] = out[band] * 0.22 + ground * 0.78
        out[band & hatch] = out[band & hatch] * 0.35 / 0.22 * 0 + np.array([230, 233, 255]) * 0.45 + (rgb[band & hatch] * 255) * 0.35
        inside = r <= R
        tile = np.zeros((W, W, 4), dtype=np.uint8)
        tile[..., :3] = np.clip(out, 0, 255).astype(np.uint8); tile[..., 3] = np.where(inside, 255, 0)
        img[:, k * W:(k + 1) * W] = tile
        for gi, fam in enumerate(fams):
            msk = inside & (~band) & (top == gi) & (sat > 0.45)
            if msk.sum() > 200:
                labels.setdefault(fam, []).append([round(float((xx[msk].mean() + k * W) / (W * len(WHEEL_VALUES))), 4),
                                                   round(float(yy[msk].mean() / W), 4)])
    Image.fromarray(img, "RGBA").save(path, optimize=True)
    return {"values": list(WHEEL_VALUES), "labels": labels, "image": os.path.relpath(path, REPO)}


def build(theme_asset: dict | None = None, dom: dict | None = None) -> dict:
    t = theme_asset if theme_asset is not None else uitheme()
    d = dom if dom is not None else domains()
    deep = {
        "ground": t["surfaceVeryDark"], "ground-deep": t["surfaceBlack"], "surface": t["surfaceDark"],
        "surface-raised": t["surfaceLight"], "line": t["surfaceLight"], "text": t["textLight"],
        "text-soft": t["neutralLightest"], "text-off": t["inactiveLight"],
        "cta": t["cta"], "cta-fill": t["cta"], "danger": t["danger"], "purchase": t["purchase"],
        "purchase-fill": t["purchase"], "control": t["controlActive"], "tooltip": t["tooltip"],
        # Shombith 2026-11-05 (D3): Jade is the family of what you can pick and change again, so the selection and the
        # focus ring are Jade (controlActive); Ruby is what you cannot take back (Ready), so a commit is Ruby. The game's
        # UITheme.selection is still the old sky blue until the Unity change lands (Docs/UI_STYLE_GUIDE.md section 13).
        "selection": t["controlActive"], "commit": t["tooltip"], "commit-fill": t["tooltip"], "on-commit": "#FFFFFF",
        "jade": d["jade"], "ruby": d["ruby"], "gold": d["gold"], "gold-fill": d["gold"],
        "on-fill": t["surfaceVeryDark"],
    }
    return {
        "format": "froglet-tokens/1",
        "generated_by": "python3 Tools/Build/froglet_style.py",
        "guide": "Docs/UI_STYLE_GUIDE.md",
        "sources": ["Assets/_SO_Assets/UI/UITheme.asset", os.path.relpath(live_color_set(), REPO) + " (the live palette)",
                    "Docs/VesselModels/colour_domains.json", "Docs/STYLE_FOUNDATION.md"],
        "themes": {"deep-sea": deep, "shore": dict(SHORE)},
        "fonts": {"body": "Aldrich", "label": "Chakra Petch", "label-weights": [500, 600, 700],
                  "google": "https://fonts.googleapis.com/css2?family=Aldrich&family=Chakra+Petch:wght@500;600;700&display=swap"},
        "type": TYPE, "space": SPACE, "shape": SHAPE,
        "motion": {k: {"ms": v[0], "ease": v[1]} for k, v in MOTION.items()},
        "palettes": palettes(),
        "meanings": {
            "lime": "a call to action the player can still change: join, accept, claim (always green)",
            "fire": "danger: bad, avoid it or deal with it",
            "gold": "spending, and the Gold domain",
            "jade": "a choice you can pick and change again (a hull pick, a friend, the selected thing, the focus ring), and the Jade domain",
            "ruby": "a commitment you cannot take back (Play, which launches a game; Ready), and the Ruby domain",
            "blue": "neutral: text, panels, lines, every grey",
        },
    }


def css(tokens: dict) -> str:
    def block(sel: str, theme: dict) -> list:
        return [f"{sel} {{"] + [f"  --fs-{k}: {v};" for k, v in theme.items()] + ["}"]
    deep, shore = tokens["themes"]["deep-sea"], tokens["themes"]["shore"]
    lines = ["/* froglet.css - GENERATED by Tools/Build/froglet_style.py from the game's assets; do not edit by hand.",
             "   The guide: Docs/UI_STYLE_GUIDE.md. Deep Sea is the default; data-fs-theme=\"shore\" is the light twin;",
             "   data-fs-theme=\"auto\" follows the viewer's light/dark setting. data-fs-domain=\"jade|ruby|gold\" adds",
             "   one domain accent (--fs-domain). Fonts: link " + tokens["fonts"]["google"] + " */", ""]
    lines += block(':root, [data-fs-theme="deep-sea"]', deep) + [""]
    lines += block('[data-fs-theme="shore"]', shore) + [""]
    lines += ["@media (prefers-color-scheme: light) {"] + ["  " + l for l in block('[data-fs-theme="auto"]', shore)] + ["}", ""]
    lines += [":root {", "  --fs-font-body: 'Aldrich', 'Chakra Petch', sans-serif;", "  --fs-font-label: 'Chakra Petch', 'Aldrich', sans-serif;"]
    lines += [f"  --fs-type-{k}: {v}px;" for k, v in tokens["type"].items()]
    lines += [f"  --fs-space-{i}: {v}px;" for i, v in enumerate(tokens["space"])]
    lines += [f"  --fs-{k}: {v}px;" for k, v in tokens["shape"].items()]
    lines += [f"  --fs-dur-{k}: {v['ms']}ms;\n  --fs-ease-{k}: {v['ease']};" for k, v in tokens["motion"].items()]
    for fam, shades in tokens.get("palettes", {}).items():
        lines += [f"  --fs-{sh['name']}: {sh['hex']};" for sh in shades]
    lines += ["}", ""]
    for name in ("jade", "ruby", "gold"):
        lines += [f'[data-fs-domain="{name}"] {{ --fs-domain: var(--fs-{name}, var(--fs-{name}-fill)); }}']
    lines += ["",
              "/* The corner sliver: two opposite corners cut, nothing rounded (STYLE_FOUNDATION.md section 5). */",
              ".fs-sliver { clip-path: polygon(var(--fs-sliver-large) 0, 100% 0, 100% calc(100% - var(--fs-sliver-large)),",
              "  calc(100% - var(--fs-sliver-large)) 100%, 0 100%, 0 var(--fs-sliver-large)); }",
              ".fs-sliver-small { clip-path: polygon(var(--fs-sliver-small) 0, 100% 0, 100% calc(100% - var(--fs-sliver-small)),",
              "  calc(100% - var(--fs-sliver-small)) 100%, 0 100%, 0 var(--fs-sliver-small)); }",
              ".fs-label { font-family: var(--fs-font-label); font-weight: 600; text-transform: uppercase; letter-spacing: .12em; }",
              ".fs-num { font-variant-numeric: tabular-nums; }",
              "@media (prefers-reduced-motion: reduce) { :root { --fs-dur-micro: 0ms; --fs-dur-std: 0ms; --fs-dur-panel: 0ms; --fs-dur-ceremony: 0ms; } }",
              ""]
    return "\n".join(lines)


def problems(tokens: dict, defs: dict | None = None) -> tuple[list, list]:
    """(failures, open decisions) for the rules in the module docstring."""
    defs = defs or cd.load()
    fails, opens = [], []
    for name, theme in tokens["themes"].items():
        ground = theme["ground"]
        for k, fam in FAMILY_OF.items():
            if k not in theme:
                continue
            got, top, dist = cd.classify(defs, hx(theme[k]))
            if got != fam:
                msg = f"{name}.{k} {theme[k]} is {'in the band near ' + top if got is None else 'in ' + got} (family {fam})"
                (opens if (name, k) in OPEN else fails).append(msg + (f" - {OPEN[(name, k)]}" if (name, k) in OPEN else ""))
        for k in TEXT_TOKENS:
            if contrast(theme[k], ground) < 4.5:
                fails.append(f"{name}.{k} {theme[k]} on {ground} is {contrast(theme[k], ground):.2f}:1, text needs 4.5:1")
        for k in MARK_TOKENS:
            if k in theme and contrast(theme[k], ground) < 3.0:
                fails.append(f"{name}.{k} {theme[k]} on {ground} is {contrast(theme[k], ground):.2f}:1, a mark needs 3:1")
        for k in FILL_TOKENS:
            if k in theme and contrast(theme["on-fill"], theme[k]) < 4.5:
                fails.append(f"{name}.on-fill on {k} is {contrast(theme['on-fill'], theme[k]):.2f}:1, needs 4.5:1")
        for k in LIGHT_ON_FILL:
            if k in theme and contrast(theme["on-commit"], theme[k]) < 4.5:
                fails.append(f"{name}.on-commit on {k} is {contrast(theme['on-commit'], theme[k]):.2f}:1, needs 4.5:1")
    for fam, shades in tokens.get("palettes", {}).items():
        for sh in shades:
            got, top, dist = cd.classify(defs, hx(sh["hex"]))
            if got != fam or dist < PALETTE_CLEAR - 1e-6:
                fails.append(f"palette {sh['name']} {sh['hex']} is not {PALETTE_CLEAR} inside {fam} ({got or 'band'}, {dist:.3f})")
    return fails, opens


def rendered() -> dict:
    t = build()
    return {OUT_JSON: json.dumps(t, indent=2) + "\n", OUT_CSS: css(t)}


def wheel_meta() -> dict:
    """The wheel's label positions, recomputed into a scratch file so --check never writes the committed image."""
    import tempfile
    with tempfile.TemporaryDirectory() as td:
        return wheel_png(os.path.join(td, "w.png"))


def self_test() -> int:
    bad = []
    t = build()
    f, o = problems(t)
    if f:
        bad.append(f"the shipped tokens fail: {f}")
    m = json.loads(json.dumps(t))
    m["themes"]["shore"]["danger"] = "#3DB2FF"
    if not any("shore.danger" in x for x in problems(m)[0]):
        bad.append("a danger token in the wrong family is not named")
    m = json.loads(json.dumps(t))
    m["themes"]["shore"]["text-soft"] = "#B8C0F0"
    if not any("shore.text-soft" in x and "4.5:1" in x for x in problems(m)[0]):
        bad.append("low-contrast text is not named")
    m = json.loads(json.dumps(t))
    m["themes"]["shore"]["gold-fill"] = "#805700"
    if not any("gold-fill" in x for x in problems(m)[0]):
        bad.append("a gold fill outside gold is not named")
    if t["themes"]["deep-sea"]["selection"] != t["themes"]["deep-sea"]["control"]:
        bad.append("the selection is not Jade (D3, 2026-11-05)")
    if any(len(v) < 3 for v in t["palettes"].values()):
        bad.append(f"a family has fewer than 3 palette shades: { {k: len(v) for k, v in t['palettes'].items()} }")
    m = json.loads(json.dumps(t))
    m["palettes"]["jade"].append({"name": "jade-x", "hex": "#3DB2FF", "L": 0.7})
    if not any("jade-x" in x for x in problems(m)[0]):
        bad.append("a palette shade in the band is not named")
    if contrast("#FFFFFF", "#000000") < 20.9 or abs(contrast("#777777", "#FFFFFF") - 4.48) > 0.02:
        bad.append("the contrast formula is off")
    print("froglet_style self-test:", "OK" if not bad else "FAILED")
    for b in bad:
        print("  FAIL", b)
    return 1 if bad else 0


def main(argv: list) -> int:
    if "--self-test" in argv:
        return self_test()
    files = rendered()
    if "--check" in argv:
        rc = 0
        for path, text in files.items():
            cur = open(path, encoding="utf-8").read() if os.path.exists(path) else None
            if cur != text:
                print(f"STALE {os.path.relpath(path, REPO)} - run python3 Tools/Build/froglet_style.py")
                rc = 1
        f, o = problems(build())
        for x in f:
            print("FAIL", x)
        for x in o:
            print("open", x)
        print("froglet_style --check:", "ok" if rc == 0 and not f else "FAILED")
        return 1 if (rc or f) else 0
    os.makedirs(os.path.dirname(OUT_JSON), exist_ok=True)
    meta = wheel_png(OUT_WHEEL)
    with open(os.path.join(os.path.dirname(OUT_JSON), "colour-wheel.json"), "w", encoding="utf-8") as fh:
        fh.write(json.dumps(meta, indent=2) + "\n")
    print("wrote", os.path.relpath(OUT_WHEEL, REPO), "and colour-wheel.json")
    for path, text in files.items():
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(text)
        print("wrote", os.path.relpath(path, REPO))
    f, o = problems(build())
    for x in f + o:
        print(("FAIL " if x in f else "open ") + x)
    return 1 if f else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
