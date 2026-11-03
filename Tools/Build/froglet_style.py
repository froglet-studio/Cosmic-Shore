#!/usr/bin/env python3
"""froglet_style.py - the Froglet design tokens every surface shares: the game's UI, Frogma boards, artifacts, decks,
diagrams, the website and the Fleet Codex. Docs/UI_STYLE_GUIDE.md is the guide; this script writes its tokens.

It READS the game, so the tokens never drift from what players see:
  - Assets/_SO_Assets/UI/UITheme.asset              the UI's chrome and family colours (UIThemeSO)
  - Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset   the three domain colours (through domain_colors.py)
  - Docs/VesselModels/colour_domains.json           Garrett's six colour families (through colour_domains.py)
  - Docs/STYLE_FOUNDATION.md section 4/5/7          the type scale, the sliver, spacing and motion (the numbers below)

and WRITES:
  - Docs/UI/froglet-tokens.json   every theme's tokens, for tools and generators
  - Docs/UI/froglet.css           the same as CSS custom properties, for any HTML page (artifacts, decks, the website)

    python3 Tools/Build/froglet_style.py            write both files
    python3 Tools/Build/froglet_style.py --check    exit 1 when a file is stale or a rule below fails
    python3 Tools/Build/froglet_style.py --self-test

--check fails when:
  - a written file differs from what the game's assets give now (naming the file);
  - a theme's family colour does not rest in its family, clear of the forbidden band (colour_domains.json), except the
    listed open decisions;
  - a theme's text or family colour misses its contrast on that theme's ground (4.5:1 for text, 3:1 for a line or an
    icon; gold on the light theme is a FILL under dark text, never text).

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
OUT_CSS = os.path.join(REPO, "Docs", "UI", "froglet.css")

# Open decisions recorded in Docs/UI_STYLE_GUIDE.md (D-numbers there); --check reports these but does not fail.
OPEN = {
    ("deep-sea", "selection"): "D3: the selection blue #3DB2FF sits in the band between Blue and Jade (COLOR_DOMAINS.md)",
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
    "control": "#007D73", "tooltip": "#893BFF", "selection": "#0A61FF",
    "jade": "#007D73", "ruby": "#893BFF", "gold-fill": "#FFA700", "on-fill": "#00041F",
}

FAMILY_OF = {"cta": "lime", "cta-fill": "lime", "danger": "fire", "purchase": "gold", "purchase-fill": "gold",
             "control": "jade", "tooltip": "ruby", "selection": "blue", "jade": "jade", "ruby": "ruby",
             "gold": "gold", "gold-fill": "gold"}
TEXT_TOKENS = ("text", "text-soft")                       # must read at 4.5:1 on the ground
MARK_TOKENS = ("cta", "danger", "control", "tooltip", "selection", "jade", "ruby", "gold", "purchase")  # 3:1 at least
FILL_TOKENS = ("cta-fill", "purchase-fill", "gold-fill")   # carry "on-fill" text at 4.5:1


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


def domains() -> dict:
    cols = domain_colors.asset_colors(open(os.path.join(REPO, domain_colors.ASSET), encoding="utf-8").read())
    return {k: to_hex(v) for k, v in cols.items() if k in ("jade", "ruby", "gold")}


def build(theme_asset: dict | None = None, dom: dict | None = None) -> dict:
    t = theme_asset if theme_asset is not None else uitheme()
    d = dom if dom is not None else domains()
    deep = {
        "ground": t["surfaceVeryDark"], "ground-deep": t["surfaceBlack"], "surface": t["surfaceDark"],
        "surface-raised": t["surfaceLight"], "line": t["surfaceLight"], "text": t["textLight"],
        "text-soft": t["neutralLightest"], "text-off": t["inactiveLight"],
        "cta": t["cta"], "cta-fill": t["cta"], "danger": t["danger"], "purchase": t["purchase"],
        "purchase-fill": t["purchase"], "control": t["controlActive"], "tooltip": t["tooltip"],
        "selection": t["selection"], "jade": d["jade"], "ruby": d["ruby"], "gold": d["gold"], "gold-fill": d["gold"],
        "on-fill": t["surfaceVeryDark"],
    }
    return {
        "format": "froglet-tokens/1",
        "generated_by": "python3 Tools/Build/froglet_style.py",
        "guide": "Docs/UI_STYLE_GUIDE.md",
        "sources": ["Assets/_SO_Assets/UI/UITheme.asset", "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset",
                    "Docs/VesselModels/colour_domains.json", "Docs/STYLE_FOUNDATION.md"],
        "themes": {"deep-sea": deep, "shore": dict(SHORE)},
        "fonts": {"body": "Aldrich", "label": "Chakra Petch", "label-weights": [500, 600, 700],
                  "google": "https://fonts.googleapis.com/css2?family=Aldrich&family=Chakra+Petch:wght@500;600;700&display=swap"},
        "type": TYPE, "space": SPACE, "shape": SHAPE,
        "motion": {k: {"ms": v[0], "ease": v[1]} for k, v in MOTION.items()},
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
    return fails, opens


def rendered() -> dict:
    t = build()
    return {OUT_JSON: json.dumps(t, indent=2) + "\n", OUT_CSS: css(t)}


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
