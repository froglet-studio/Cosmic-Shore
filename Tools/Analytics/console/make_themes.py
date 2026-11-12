"""The console's themes, taken from the game's colour sets (Shombith, 2026-11-06: "the color set should inspire the theme
of this UI too", "the whole UI should change and color sets should be named themes", "dart frog had a metallic brown
right in the game so that theme should be here too", and "bring the sea lines in the bg").

    python3 Tools/Analytics/console/make_themes.py            # write themes.css and theme.js beside this file
    python3 Tools/Analytics/console/make_themes.py --check    # exit 1 if either file drifted from the game
    python3 Tools/Analytics/console/make_themes.py --self-test

A theme is one of the colour sets players pick in the game (Assets/_SO_Assets/Color Palettes/ColorSetCatalog.asset:
Nocturne, Photic, Dart Frog). Every domain in a set is two pigments, a glowing RIM over a deep FACE (Docs/PALETTE.md,
Docs/VesselModels/colour_sets.json: Lagoon over Plum, Mint over Bronze, ...). The values are read from the shipped
SO_ColorSet assets, which are linear HDR, and brought to the screen the way Docs/PALETTE.md section 3 and the Frogma
colour-set round (Docs/MenuRedesignB/rounds/r51_profile_colour_sets.py) read them: a soft shoulder above 1.0, then the
sRGB curve. The rim is a domain's TrailHighlightColor, its face the OutsideBlockColor.

The whole UI follows the theme:
- the ground, panels, lines and text keep the style guide's Deep Sea lightness steps (Docs/UI/froglet-tokens.json) and
  take the hue of the set's signature face (its Jade face: Plum, Deep cyan, Bronze), so Dart Frog is metallic bronze;
- panels are drawn like the game's prisms: the signature rim, faint, over the face, with a light sheen (console.css);
- the call to action and danger are the set's own (EnvironmentColors BrightCTA and Danger);
- the selection and focus are its Jade rim, the data its Blue rim, the four category colours its four rims, and the
  sea lines behind every page (the menu's SeaLines, Docs/MENU_REDESIGN_B.md section 67) its Jade, Blue and Ruby rims.
Where a mark or text colour is too faint on a panel, its OKLab lightness moves until it reaches 3:1 (marks, focus) or
4.5:1 (text); the hue stays the game's.
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
BASE = "deep-sea"           # the lightness steps every theme keeps
# The set the game was authored in (ColorTrace.AuthoredSetId). Its theme is the authored Deep Sea itself and the sea
# material's own colours (Garrett, 2026-11-10, LOCKED: every colour comes from the colour set, and Photic holds the
# values the game was authored in), as ColorSetUITheme.Build gives it from Photic's TracedColors ui-token ids.
AUTHORED_SET = "photic"
SEA_MATERIAL = os.path.join(REPO, "Assets", "_Graphics", "Materials", "Shaders", "UI", "Sea_Window.mat")
# The UI tokens a theme re-hues from its signature face, with the most chroma each may carry.
TINTED = {"ground": 0.05, "ground-deep": 0.035, "surface": 0.06, "surface-raised": 0.08, "line": 0.08,
          "text": 0.02, "text-soft": 0.05, "text-off": 0.03}
# The sea's numbers, from the game's Sea_Window material (Tools/Build/author_ui_lattice.py SEA): lines 46 apart, a
# 480-unit loop, amplitude 9, three layers (far, middle, near) with their alpha, width and seconds a loop.
SEA = {"spacing": 46, "loop": 480, "amplitude": 9,
       "layers": [{"alpha": 0.2, "width": 0.9, "seconds": 90}, {"alpha": 0.3, "width": 1.1, "seconds": 68},
                  {"alpha": 0.42, "width": 1.5, "seconds": 45}]}
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


def mix(c1, c2, t):
    """OKLab mix: t of the way from c1 to c2."""
    a, b = to_oklab(c1), to_oklab(c2)
    return from_oklab(*(x + (y - x) * t for x, y in zip(a, b)))


def nudge(rgb, ground, amount=0.08):
    """A step further from the ground (hover, the highlighted bar)."""
    L, a, b = to_oklab(rgb)
    return from_oklab(max(0.0, min(1.0, L + (amount if luminance(ground) < 0.18 else -amount))), a, b)


def screen(linear):
    """A shipped (linear HDR) colour on screen: a soft shoulder above 1.0, then the sRGB curve (Docs/PALETTE.md 3)."""
    out = []
    for x in linear:
        x = max(0.0, x)
        if x > 1:
            x = x / (1 + 0.25 * (x - 1))
        out.append(_gam(min(1.0, x)))
    return tuple(out)


def tint(token, face, cap):
    """The token's lightness with the face's hue, at the token's own chroma (never more than `cap`)."""
    L, a, b = to_oklab(token)
    _, fa, fb = to_oklab(face)
    c, h = min(math.hypot(a, b), cap), math.atan2(fb, fa)
    return from_oklab(L, c * math.cos(h), c * math.sin(h))


def hue(rgb):
    _, a, b = to_oklab(rgb)
    return math.degrees(math.atan2(b, a)) % 360


# ------------------------------------------------------------------ the game's colour sets

def guid_path(guid):
    for name in sorted(os.listdir(PALETTES)):
        if name.endswith(".asset.meta") and re.search(rf"^guid: {guid}$", open(os.path.join(PALETTES, name), encoding="utf-8").read(), re.M):
            return os.path.join(PALETTES, name[:-len(".meta")])
    raise SystemExit(f"make_themes: no colour set asset has guid {guid}")


def read_set(text):
    """Each domain's rim and face on screen, and the set's own call to action and danger."""
    def colour(block_text, field):
        m = re.search(field + r": \{r: %s, g: %s, b: %s" % (NUM, NUM, NUM), block_text)
        if not m:
            raise SystemExit(f"make_themes: a colour set has no {field}")
        return screen(tuple(float(x) for x in m.groups()))
    out = {"rim": {}, "face": {}}
    for key, block in DOMAINS:
        i = text.find("  " + block + ":")
        if i < 0:
            raise SystemExit(f"make_themes: a colour set has no {block} block")
        out["rim"][key] = colour(text[i:i + 6000], "TrailHighlightColor")
        out["face"][key] = colour(text[i:i + 6000], "OutsideBlockColor")
    i = text.find("  EnvironmentColors:")
    out["cta"] = colour(text[i:i + 4000], "BrightCTA")
    out["danger"] = colour(text[i:i + 4000], "Danger")
    return out


def catalog():
    """The sets players can pick, in the catalog's order: [{id, name, line, pigments, colours}]."""
    text = open(CATALOG, encoding="utf-8").read()
    sets = []
    for m in re.finditer(r"^  - id: (\S+)\n    colorSet: \{fileID: \d+, guid: (\w+), type: \d+\}\n    displayName: (.+)\n"
                         r"    line: (.+)\n(?:    unlocked: \d+\n)?(?:    pigments:\n((?:    - .+\n)+))?", text, re.M):
        line = m.group(4).strip()
        if line[:1] in "'\"":
            line = line[1:-1].replace("''", "'")
        pigments = [p.strip()[2:].strip() for p in (m.group(5) or "").splitlines()]
        sets.append({"id": m.group(1), "name": m.group(3).strip(), "line": line, "pigments": pigments,
                     "colours": read_set(open(guid_path(m.group(2)), encoding="utf-8").read())})
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

def sea_colours():
    """The sea material's own line, middle, end and glow colours (Sea_Window.mat), as the screen shows them."""
    text = open(SEA_MATERIAL, encoding="utf-8").read()

    def prop(name):
        m = re.search(r"- %s: \{r: %s, g: %s, b: %s" % (name, NUM, NUM, NUM), text)
        return tuple(float(x) for x in m.groups())
    return {"line": prop("_LineColor"), "mid": prop("_LineColorMid"), "end": prop("_LineColorEnd"),
            "glow": prop("_GlowColor")}


def theme_tokens(base, c, authored=False):
    """Every UI token for one theme, as hex, plus the rims and faces. The authored set's theme is the authored Deep
    Sea as it is, with the sea material's own colours; its domain marks, data and rims are still its own pigments."""
    face, rims = c["face"]["jade"], c["rim"]
    if authored:
        t = {k: hx(base[k]) for k in TINTED}
    else:
        t = {k: tint(hx(base[k]), face, cap) for k, cap in TINTED.items()}
    surface, ground = t["surface"], t["ground"]
    if not authored:
        t["text-soft"] = fit(t["text-soft"], surface, TEXT)
    marks = {k: fit(fit(v, ground, MARK), surface, MARK) for k, v in rims.items()}
    out = {f"--fs-{k}": to_hex(v) for k, v in t.items()}
    out.update({
        "--fs-cta": to_hex(fit(c["cta"], surface, MARK)), "--fs-cta-fill": to_hex(c["cta"]),
        "--fs-danger": to_hex(fit(c["danger"], surface, TEXT)),
        "--fs-selection": to_hex(marks["jade"]), "--fs-control": to_hex(marks["jade"]),
        "--fs-jade": to_hex(marks["jade"]), "--fs-ruby": to_hex(marks["ruby"]),
        "--fs-gold": to_hex(marks["gold"]), "--fs-gold-fill": to_hex(rims["gold"]),
        "--fs-purchase": to_hex(marks["gold"]), "--fs-purchase-fill": to_hex(rims["gold"]),
        "--fs-on-fill": to_hex(t["ground"]),
    })
    if authored:
        out.update({f"--fs-{k}": base[k] for k in ("cta", "cta-fill", "danger", "selection", "control", "purchase",
                                                    "purchase-fill")})
    out.update({f"--cs-{k}": to_hex(v) for k, v in marks.items()})
    out.update({f"--series-{n}": f"var(--cs-{k})" for n, (k, _) in enumerate(DOMAINS, 1)})
    data = marks["blue"]
    out.update({"--data": to_hex(data), "--data-hot": to_hex(nudge(data, ground)),
                "--data-text": to_hex(fit(data, surface, TEXT)),
                # the prism look for panels: the signature rim, faint, over the face
                "--rim": to_hex(rims["jade"]), "--face": to_hex(face),
                # the sea: the shader's cyan / periwinkle / violet become the set's Jade, Blue and Ruby rims
                "--sea-1": to_hex(rims["jade"]), "--sea-2": to_hex(rims["blue"]), "--sea-3": to_hex(rims["ruby"]),
                "--sea-wash": to_hex(mix(ground, face, 0.3)), "--sea-glow": to_hex(rims["jade"])})
    if authored:
        sea = sea_colours()
        out.update({"--sea-1": to_hex(sea["line"]), "--sea-2": to_hex(sea["mid"]), "--sea-3": to_hex(sea["end"]),
                    "--sea-glow": to_hex(sea["glow"])})
    report = {"text": contrast(t["text"], surface), "text-soft": contrast(t["text-soft"], surface),
              "danger": contrast(hx(out["--fs-danger"]), surface), "cta": contrast(hx(out["--fs-cta"]), surface),
              **{k: contrast(v, surface) for k, v in marks.items()}, "data-text": contrast(hx(out["--data-text"]), surface),
              "surface-hue": hue(surface)}
    return out, report


def build(tokens=None, sets=None, default=None):
    tokens = tokens or json.load(open(TOKENS, encoding="utf-8"))
    sets = sets or catalog()
    default = default or live_set(sets)
    base = tokens["themes"][BASE]
    head = ("/* themes.css - GENERATED by Tools/Analytics/console/make_themes.py from the game; do not edit by hand.\n"
            "   A theme is a colour set players pick in the game (ColorSetCatalog). Its signature face re-hues the Deep Sea\n"
            "   ground, panels, lines and text; its rims, call to action and danger paint everything else. */\n")
    rows = [f"  --fs-{k}: {v};" for k, v in base.items()] + ["  color-scheme: dark;"]
    css, reports = [head, ":root {\n" + "\n".join(rows) + "\n}\n"], {}
    for s_ in sets:
        vals, reports[s_["id"]] = theme_tokens(base, s_["colours"], s_["id"] == AUTHORED_SET)
        sel = f'[data-fs-theme="{s_["id"]}"]'
        if s_["id"] == default:
            sel = ":root:not([data-fs-theme]), " + sel     # the game's own set, before theme.js runs
        css.append(f"{sel} {{\n" + "\n".join(f"  {k}: {v};" for k, v in vals.items()) + "\n}\n")
    meta = {"default": default, "sea": SEA,
            "themes": [{"id": x["id"], "name": x["name"], "line": x["line"], "pigments": x["pigments"],
                        "swatch": [[to_hex(x["colours"]["rim"][k]), to_hex(x["colours"]["face"][k])] for k, _ in DOMAINS]}
                       for x in sets]}
    js = ("/* theme.js - GENERATED by Tools/Analytics/console/make_themes.py; do not edit by hand.\n"
          "   Loaded in <head> so the page draws in the reader's theme from the first frame. The choice is kept in this\n"
          "   browser only; with none, the theme is the colour set the game runs on. */\n"
          "(function () {\n"
          f"  var T = {json.dumps(meta, ensure_ascii=False, separators=(',', ':'))};\n"
          "  var root = document.documentElement;\n"
          "  function has(id) { for (var i = 0; i < T.themes.length; i++) { if (T.themes[i].id === id) return true; } return false; }\n"
          "  var theme = null;\n"
          "  try { theme = localStorage.getItem('fa-theme'); } catch (e) {}\n"
          "  if (!has(theme)) theme = T.default;\n"
          "  root.setAttribute('data-fs-theme', theme);\n"
          "  T.current = function () { return root.getAttribute('data-fs-theme'); };\n"
          "  T.pick = function (id) {\n"
          "    if (!has(id)) return;\n"
          "    root.setAttribute('data-fs-theme', id);\n"
          "    try { localStorage.setItem('fa-theme', id); } catch (e) {}\n"
          "  };\n"
          "  window.FA_THEMES = T;\n"
          "})();\n")
    return "\n".join(css), js, reports


def self_test():
    css, js, reports = build()
    bad = []
    floors = {"text": 7.0, "text-soft": TEXT, "danger": TEXT, "data-text": TEXT, "cta": MARK,
              **{k: MARK for k, _ in DOMAINS}}
    for sid, r in reports.items():
        # The authored Deep Sea is what the game was made in, and the authored set wears it as it is (its soft text and
        # danger sit at 3.6:1 and 4.4:1 on a panel); the floors hold what fit() derives for every other set.
        if sid == AUTHORED_SET:
            continue
        bad += [f"{sid} {k} {r[k]:.2f}:1 < {f}" for k, f in floors.items() if r[k] < f]
    # Shombith: "dart frog had a metallic brown right in the game" - its panels must be bronze, not navy
    if "dartfrog" in reports and not 25 <= reports["dartfrog"]["surface-hue"] <= 90:
        bad.append(f"Dart Frog's panels are hue {reports['dartfrog']['surface-hue']:.0f}, not bronze")
    if len({round(r["surface-hue"]) for r in reports.values()}) < len(reports):
        bad.append("two themes share one panel colour: the whole UI must change with the theme")
    # the linear assets must reach the screen as the palette doc's swatches (Dart Frog Jade: Mint over Bronze, whose
    # swatch colour_sets.json holds; it was #774325 until the 2026-11-10 rules took the bronze into the night)
    with open(os.path.join(REPO, "Docs", "VesselModels", "colour_sets.json"), encoding="utf-8") as fh:
        bronze = json.load(fh)["sets"]["dart_frog"]["domains"]["jade"]["face"]["swatch"].upper()
    for x in catalog():
        if x["id"] == "dartfrog" and to_hex(x["colours"]["face"]["jade"]) != bronze:
            bad.append(f"Dart Frog's Jade face reads {to_hex(x['colours']['face']['jade'])}, not Bronze {bronze}")
        if f'data-fs-theme="{x["id"]}"' not in css or f'"id":"{x["id"]}"' not in js:
            bad.append(f"theme {x['id']} missing from the output")
    if any(w in css for w in ("hypersea", "cosmic-shore")):
        bad.append("a second ground crept back in")
    faint = fit(hx("#B9F5F2"), hx("#FFFFFF"), MARK)
    if contrast(faint, hx("#FFFFFF")) < MARK or not faint[1] > faint[0]:
        bad.append("fit() did not darken a faint teal into a teal at 3:1")
    print("make_themes self-test: " + ("ok" if not bad else "FAILED " + "; ".join(bad)))
    return 0 if not bad else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    css, js, reports = build()
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
    for sid, r in reports.items():
        print(f"{sid:9s} panel hue {r['surface-hue']:5.0f}  " + " ".join(f"{k} {v:.2f}" for k, v in r.items() if k != "surface-hue"))
    print("make_themes: wrote themes.css and theme.js")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
