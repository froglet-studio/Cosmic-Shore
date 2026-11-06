"""The console's themes, taken from the game (Shombith, 2026-11-06: "use the correct theme ... cosmic shore and
hypersea and have different themes using the color set from the game").

    python3 Tools/Analytics/console/make_themes.py            # write themes.css and theme.js beside this file
    python3 Tools/Analytics/console/make_themes.py --check    # exit 1 if either file drifted from the game
    python3 Tools/Analytics/console/make_themes.py --self-test

Two grounds, each with the game's colour sets:
- HyperSea (dark, the default) takes the style guide's Deep Sea values and Cosmic Shore (light) its Shore values,
  both from Docs/UI/froglet-tokens.json (written from the game's UITheme by Tools/Build/froglet_style.py).
- Every colour set the game offers players (Assets/_SO_Assets/Color Palettes/ColorSetCatalog.asset: Nocturne, Photic,
  Dart Frog) paints the four domain colours, the charts (Blue, the neutral data family), the selection and focus
  (Jade) and the swatches. A domain's colour is its TrailHighlightColor, the same field the Vessel Studio reads
  (.claude/skills/vessel-studio/domain_colors.py). Where a colour is too faint on a ground, its OKLab lightness moves
  until it reaches 3:1 (marks, focus) or 4.5:1 (text); the hue stays the game's.
- The meanings stay the guide's: Fire is a problem and Lime is healthy, whatever the set.
"""
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
TOKENS = os.path.join(REPO, "Docs", "UI", "froglet-tokens.json")
PALETTES = os.path.join(REPO, "Assets", "_SO_Assets", "Color Palettes")
CATALOG = os.path.join(PALETTES, "ColorSetCatalog.asset")
CONTAINER = os.path.join(REPO, "Assets", "_SO_Assets", "ThemeManagerDataContainer.asset")
OUT_CSS = os.path.join(HERE, "themes.css")
OUT_JS = os.path.join(HERE, "theme.js")
GROUNDS = [("hypersea", "HyperSea", "deep-sea", "dark"), ("cosmic-shore", "Cosmic Shore", "shore", "light")]
DOMAINS = [("jade", "JadeColors"), ("ruby", "RubyColors"), ("gold", "GoldColors"), ("blue", "BlueColors")]
NUM = r"(-?[\d.]+(?:e-?\d+)?)"
MARK, TEXT = 3.0, 4.5     # WCAG 2.2: 3:1 for graphics and focus rings, 4.5:1 for text


# ------------------------------------------------------------------ colour

def hx(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def to_hex(rgb):
    return "#%02X%02X%02X" % tuple(max(0, min(255, round(v * 255))) for v in rgb)


def _lin(v):
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def _gam(v):
    v = max(0.0, min(1.0, v))
    return 12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055


def luminance(rgb):
    r, g, b = (_lin(v) for v in rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a, b):
    la, lb = sorted((luminance(a), luminance(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


def to_oklab(rgb):
    r, g, b = (_lin(v) for v in rgb)
    l = (0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b) ** (1 / 3)
    m = (0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b) ** (1 / 3)
    s = (0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b) ** (1 / 3)
    return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s)


def from_oklab(L, a, b):
    l = (L + 0.3963377774 * a + 0.2158037573 * b) ** 3
    m = (L - 0.1055613458 * a - 0.0638541728 * b) ** 3
    s = (L - 0.0894841775 * a - 1.2914855480 * b) ** 3
    return tuple(_gam(v) for v in (4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                                   -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
                                   -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s))


def fit(rgb, ground, ratio):
    """The colour with its hue kept, made lighter (on a dark ground) or darker (on a light one) until it reaches
    `ratio` against the ground. Already there: unchanged."""
    if contrast(rgb, ground) >= ratio:
        return rgb
    L, a, b = to_oklab(rgb)
    step = 0.01 if luminance(ground) < 0.18 else -0.01
    for _ in range(100):
        L = max(0.0, min(1.0, L + step))
        out = from_oklab(L, a, b)
        if contrast(out, ground) >= ratio or L in (0.0, 1.0):
            return out
    return out


def nudge(rgb, ground, amount=0.08):
    """A step further from the ground (hover, the highlighted bar)."""
    L, a, b = to_oklab(rgb)
    return from_oklab(max(0.0, min(1.0, L + (amount if luminance(ground) < 0.18 else -amount))), a, b)


# ------------------------------------------------------------------ the game's colour sets

def guid_path(guid):
    for name in sorted(os.listdir(PALETTES)):
        if name.endswith(".asset.meta") and re.search(rf"^guid: {guid}$", open(os.path.join(PALETTES, name), encoding="utf-8").read(), re.M):
            return os.path.join(PALETTES, name[:-len(".meta")])
    raise SystemExit(f"make_themes: no colour set asset has guid {guid}")


def set_colors(text):
    """Each domain's TrailHighlightColor, the field the Vessel Studio's domain_colors.py reads."""
    out = {}
    for key, block in DOMAINS:
        i = text.find("  " + block + ":")
        if i < 0:
            raise SystemExit(f"make_themes: a colour set has no {block} block")
        m = re.search(r"TrailHighlightColor: \{r: %s, g: %s, b: %s" % (NUM, NUM, NUM), text[i:i + 6000])
        if not m:
            raise SystemExit(f"make_themes: {block} has no TrailHighlightColor")
        out[key] = tuple(max(0.0, min(1.0, float(x))) for x in m.groups())
    return out


def catalog():
    """The sets players can pick, in the catalog's order: [{id, name, line, colors}]."""
    text = open(CATALOG, encoding="utf-8").read()
    sets = []
    for m in re.finditer(r"^  - id: (\S+)\n    colorSet: \{fileID: \d+, guid: (\w+), type: \d+\}\n    displayName: (.+)\n"
                         r"    line: (.+)\n", text, re.M):
        line = m.group(4).strip()
        if line[:1] in "'\"":
            line = line[1:-1].replace("''", "'")
        sets.append({"id": m.group(1), "name": m.group(3).strip(), "line": line,
                     "colors": set_colors(open(guid_path(m.group(2)), encoding="utf-8").read())})
    if not sets:
        raise SystemExit("make_themes: ColorSetCatalog lists no colour sets")
    return sets


def live_set(sets):
    """The set the game runs on (ThemeManagerDataContainer.ColorSet), the console's default."""
    guid = re.search(r"^  ColorSet: \{fileID: \d+, guid: (\w+)", open(CONTAINER, encoding="utf-8").read(), re.M).group(1)
    path = guid_path(guid)
    text = open(CATALOG, encoding="utf-8").read()
    for s in sets:
        g = re.search(rf"^  - id: {re.escape(s['id'])}\n    colorSet: \{{fileID: \d+, guid: (\w+)", text, re.M).group(1)
        if guid_path(g) == path:
            return s["id"]
    return sets[0]["id"]


# ------------------------------------------------------------------ the files

def build(tokens=None, sets=None, default=None):
    tokens = tokens or json.load(open(TOKENS, encoding="utf-8"))
    sets = sets or catalog()
    default = default or live_set(sets)
    head = ("/* themes.css - GENERATED by Tools/Analytics/console/make_themes.py from the game; do not edit by hand.\n"
            "   Grounds: HyperSea (Deep Sea) and Cosmic Shore (Shore), Docs/UI/froglet-tokens.json. Colour sets: the game's\n"
            "   ColorSetCatalog (each domain's TrailHighlightColor), fitted to 3:1 for marks and 4.5:1 for text. */\n")
    css, report = [head], []
    for gid, gname, tid, scheme in GROUNDS:
        theme = tokens["themes"][tid]
        rows = [f"  --fs-{k}: {v};" for k, v in theme.items()] + [f"  color-scheme: {scheme};"]
        css.append(f'[data-fs-theme="{gid}"] {{\n' + "\n".join(rows) + "\n}\n")
    for gid, gname, tid, _ in GROUNDS:
        theme = tokens["themes"][tid]
        ground, surface = hx(theme["ground"]), hx(theme["surface"])
        for s in sets:
            c = s["colors"]
            mark = {k: fit(v, ground, MARK) for k, v in c.items()}
            mark = {k: fit(v, surface, MARK) for k, v in mark.items()}   # charts sit on panels
            data = mark["blue"]
            rows = [f"  --cs-{k}: {to_hex(v)};" for k, v in mark.items()]
            rows += [f"  --cs-swatch-{k}: {to_hex(v)};" for k, v in c.items()]
            rows += [f"  --series-{n}: var(--cs-{k});" for n, (k, _) in enumerate(DOMAINS, 1)]
            rows += [f"  --data: {to_hex(data)};", f"  --data-hot: {to_hex(nudge(data, ground))};",
                     f"  --data-text: {to_hex(fit(data, surface, TEXT))};",
                     f"  --fs-selection: {to_hex(mark['jade'])};"]
            css.append(f'[data-fs-theme="{gid}"][data-cs-set="{s["id"]}"] {{\n' + "\n".join(rows) + "\n}\n")
            report.append((gid, s["id"], {k: round(contrast(v, surface), 2) for k, v in mark.items()},
                           round(contrast(fit(data, surface, TEXT), surface), 2)))
    meta = {"grounds": [[g[0], g[1]] for g in GROUNDS], "default": default,
            "sets": [{"id": s["id"], "name": s["name"], "line": s["line"],
                      "swatch": [to_hex(s["colors"][k]) for k, _ in DOMAINS]} for s in sets]}
    js = ("/* theme.js - GENERATED by Tools/Analytics/console/make_themes.py; do not edit by hand.\n"
          "   Loaded in <head> so the page draws in the viewer's theme from the first frame. The choice is kept in this\n"
          "   browser only; with none, the ground follows the viewer's light/dark setting and the set is the game's. */\n"
          "(function () {\n"
          f"  var T = {json.dumps(meta, ensure_ascii=False, separators=(',', ':'))};\n"
          "  var root = document.documentElement;\n"
          "  function get(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }\n"
          "  function put(k, v) { try { localStorage.setItem(k, v); } catch (e) {} }\n"
          "  function has(list, id) { for (var i = 0; i < list.length; i++) { if ((list[i].id || list[i][0]) === id) return true; } return false; }\n"
          "  var light = window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches;\n"
          "  var ground = get('fa-theme'), set = get('fa-set');\n"
          "  if (!has(T.grounds, ground)) ground = light ? 'cosmic-shore' : 'hypersea';\n"
          "  if (!has(T.sets, set)) set = T.default;\n"
          "  root.setAttribute('data-fs-theme', ground);\n"
          "  root.setAttribute('data-cs-set', set);\n"
          "  T.current = function () { return { ground: root.getAttribute('data-fs-theme'), set: root.getAttribute('data-cs-set') }; };\n"
          "  T.pick = function (kind, id) {\n"
          "    if (kind === 'ground' && has(T.grounds, id)) { root.setAttribute('data-fs-theme', id); put('fa-theme', id); }\n"
          "    if (kind === 'set' && has(T.sets, id)) { root.setAttribute('data-cs-set', id); put('fa-set', id); }\n"
          "  };\n"
          "  window.FA_THEMES = T;\n"
          "})();\n")
    return "\n".join(css), js, report


def self_test():
    tokens = json.load(open(TOKENS, encoding="utf-8"))
    css, js, report = build(tokens)
    bad = []
    for gid, sid, marks, text in report:
        bad += [f"{gid}/{sid} {k} {v}:1" for k, v in marks.items() if v < MARK]
        if text < TEXT:
            bad.append(f"{gid}/{sid} data text {text}:1")
    # a faint colour on a light ground must come out darker, with its hue
    faint = fit(hx("#B9F5F2"), hx("#FFFFFF"), MARK)
    if contrast(faint, hx("#FFFFFF")) < MARK or not faint[1] > faint[0]:
        bad.append("fit() did not darken a faint teal into a teal at 3:1")
    for s in catalog():
        if f'data-cs-set="{s["id"]}"' not in css or f'"id":"{s["id"]}"' not in js:
            bad.append(f"set {s['id']} missing from the output")
    print("make_themes self-test: " + ("ok" if not bad else "FAILED " + "; ".join(bad)))
    return 0 if not bad else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    css, js, report = build()
    if "--check" in argv:
        stale = [p for p, body in ((OUT_CSS, css), (OUT_JS, js))
                 if not os.path.exists(p) or open(p, encoding="utf-8").read() != body]
        for p in stale:
            print(f"make_themes: {os.path.relpath(p, REPO)} is stale: run python3 Tools/Analytics/console/make_themes.py")
        if not stale:
            print("make_themes: themes.css and theme.js match the game")
        return 1 if stale else 0
    for p, body in ((OUT_CSS, css), (OUT_JS, js)):
        with open(p, "w", encoding="utf-8") as handle:
            handle.write(body)
    for gid, sid, marks, text in report:
        print(f"{gid:13s} {sid:9s} marks on panel " + " ".join(f"{k} {v}" for k, v in marks.items()) + f"  data text {text}")
    print("make_themes: wrote themes.css and theme.js")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
