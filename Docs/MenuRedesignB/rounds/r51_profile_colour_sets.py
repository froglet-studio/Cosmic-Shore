#!/usr/bin/env python3
"""Rev 51 boards: colour sets in the Profile, unlocked by quests, and the repaint between them.

Shombith, 2026-11-05: "create mockups inside profile where we can change these themes from there. remember it will
be something that the player unlocks with quest system. So it should something inside the profile view, once done
bring up the loading screen like when cell is change that the theme is changing and apply the theme."

The sets are Garrett's (Docs/VesselModels/colour_sets.json, Docs/PALETTE.md section 1): Nocturne (the default since
2026-11-03), Photic (the original) and Dart Frog. Every prism, vessel, trail and crystal on these boards is drawn in its
set's SHIPPED values, read from the SO_ColorSet assets (linear HDR, tone-clamped to sRGB the way
Docs/PALETTE.md section 3 says to read them), so a board shows what the palette really paints, not a guess.

The Profile is the built one (Rev 13, section 69): the top nav with Home lit, Profile / Your pilot and BACK, the pilot
card, then the record. The kit is the style guide's (Docs/UI_STYLE_GUIDE.md): Jade for a pick you can change, lime for
the call to action, grey with a reason for locked, the corner sliver, Aldrich + Chakra Petch, Deep Sea ground.

    python3 Docs/MenuRedesignB/rounds/r51_profile_colour_sets.py <root> 51
"""
import json
import math
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2])
PAL_DIR = REPO / "Assets/_SO_Assets/Color Palettes"

# ---------------------------------------------------------------- palette, from the shipped assets

def parse_set(name):
    out, cur = {}, None
    for line in (PAL_DIR / f"{name}.asset").read_text().splitlines():
        m = re.match(r"^  (\w+):\s*$", line)
        if m:
            cur = m.group(1)
            out[cur] = {}
            continue
        m = re.match(r"^    (\w+): \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+)", line)
        if m and cur:
            out[cur][m.group(1)] = tuple(float(x) for x in m.group(2, 3, 4))
    return out


def srgb(c):
    """Linear HDR -> a screen hex: a soft shoulder above 1.0 (bloom is not drawable on a board), then the sRGB curve."""
    def g(x):
        x = max(0.0, x)
        if x > 1:
            x = x / (1 + 0.25 * (x - 1))
        x = min(1.0, x)
        return 12.92 * x if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055
    return "#%02X%02X%02X" % tuple(round(g(v) * 255) for v in c)


def mix(a, b, t):
    a = [int(a[i:i + 2], 16) for i in (1, 3, 5)]
    b = [int(b[i:i + 2], 16) for i in (1, 3, 5)]
    return "#%02X%02X%02X" % tuple(round(x + (y - x) * t) for x, y in zip(a, b))


DOMS = [("jade", "JadeColors", "Jade"), ("ruby", "RubyColors", "Ruby"), ("gold", "GoldColors", "Gold"), ("blue", "BlueColors", "Blue")]


def palette(asset):
    s = parse_set(asset)
    pal = {}
    for key, field, _ in DOMS:
        d = s[field]
        f, r = srgb(d["OutsideBlockColor"]), srgb(d["InsideBlockColor"])
        sf, sr = srgb(d["ShieldedOutsideBlockColor"]), srgb(d["ShieldedInsideBlockColor"])
        xf, xr = srgb(d["SuperShieldedOutsideBlockColor"]), srgb(d["SuperShieldedInsideBlockColor"])
        pal[key] = {"f": f, "r": r, "m": mix(f, r, .3), "sf": sf, "sr": sr, "sm": mix(sf, sr, .3),
                    "xf": xf, "xr": xr, "xm": mix(xf, xr, .3), "v": srgb(d["ShipColor2"]), "t": srgb(d["TrailHighlightColor"])}
    e = s["EnvironmentColors"]
    pal["cta"] = srgb(e["BrightCTA"])
    pal["danger"] = srgb(e["Danger"])
    return pal


SETS_JSON = json.loads((REPO / "Docs/VesselModels/colour_sets.json").read_text())["sets"]


def pairs(key):
    d = SETS_JSON[key]["domains"]
    return {k: (d[k]["rim"]["name"], d[k]["face"]["name"]) for k, _, _ in DOMS}


GREY = {"f": "#141936", "r": "#434C89", "m": "#232A52", "sf": "#1A2044", "sr": "#5A63A0", "sm": "#2A325E",
        "xf": "#20274F", "xr": "#747BAD", "xm": "#343D6E", "v": "#747BAD", "t": "#434C89"}

SETS = [
    {"id": "nocturne", "name": "Nocturne", "status": "equipped",
     "line": "Every prism a lantern: a glowing rim over a deep jewel face.",
     "by": "Garrett's set. The default since 3 Nov.", "pal": palette("NocturneColorSetSO"), "pairs": pairs("nocturne"),
     "how": "Yours from the start"},
    {"id": "photic", "name": "Photic", "status": "owned",
     "line": "The original sunlit sea: one bright hue per domain.",
     "by": "The look the game had until 3 Nov.", "pal": palette("PhoticColorSetSO"),
     "pairs": {"jade": ("Cyan", "Deep cyan"), "ruby": ("Violet", "Deep violet"), "gold": ("Amber", "Umber"),
               "blue": ("Periwinkle", "Indigo")},
     "how": "Unlocked for every pilot",
     "quest": {"n": 2, "mode": "Hex Race", "goals": [["Rank first at Intensity 2", True], ["Rank first at Intensity 3", False]]}},
    {"id": "dartfrog", "name": "Dart Frog", "status": "locked",
     "line": "Two frog pigments per domain: mint over bronze, bumblebee over indigo.",
     "by": "Garrett's set, from his photo of a green-and-bronze dart frog.", "pal": palette("DartFrogColorSetSO"),
     "pairs": pairs("dart_frog"), "how": "Unlocked for every pilot",
     "quest": {"n": 4, "mode": "Maelstrom", "goals": [["Play Intensity 2", True], ["Play Intensity 3", False]]}},
    {"id": "colourblind", "name": "Clear Sight", "status": "coming",
     "line": "Protan, Deutan and Tritan: the same rules, tuned for each kind of colour blindness.",
     "by": "Planned in colour_sets.json. Free for everyone, never behind a quest.", "pal": None,
     "pairs": {"jade": ("Protan", ""), "ruby": ("Deutan", ""), "gold": ("Tritan", ""), "blue": ("", "")},
     "how": "Always free - in design"},
]
SET = {s["id"]: s for s in SETS}

# ---------------------------------------------------------------- kit

AVATAR = "/_blob/38bfeb3650b431b674f4b1bf94a21e1a"
SEA_BG = "/_blob/391619dde523174b4a9ead64e169b67a"
NAV = [("HANGAR", "/_blob/34f5a50f1275fef42808fe9df3bf6af5"), ("ARK", "/_blob/e5b050c5a676193c650e94d873ad40e2"),
       ("HOME", "/_blob/a5bd4d8d1de7b494567fc3de75525e1e"), ("PORT", "/_blob/d0e850bc927559f1a499868d26f2a3de")]

JADE, LIME, GOLD, RUBY = "#00D4FF", "#99FF80", "#FFAE00", "#A600FF"
TEXT, SOFT, LINE, PANEL = "#E6E9FF", "#747BAD", "#2B3266", "#080C28"

HELMET = """<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#00010A}
.lab{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.18em;text-transform:uppercase;color:#8C93C7}
.cap{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}
.num{font-variant-numeric:tabular-nums}
.abs{position:absolute}
.sl{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}
.sls{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}
.oct{clip-path:polygon(29% 0,71% 0,100% 29%,100% 71%,71% 100%,29% 100%,0 71%,0 29%)}
.hexc{clip-path:polygon(25% 0,75% 0,100% 50%,75% 100%,25% 100%,0 50%)}
.kc{display:inline-flex;align-items:center;justify-content:center;min-width:26px;height:22px;padding:0 6px;box-sizing:border-box;border:1px solid #434C89;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:12px;color:#C9CDEA}
.btn{box-sizing:border-box;display:inline-flex;align-items:center;justify-content:center;gap:10px;height:52px;padding:0 22px;white-space:nowrap;border:0;cursor:pointer;font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:16px;letter-spacing:.14em;text-transform:uppercase;text-decoration:none;clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}
.cta{background:#99FF80;color:#00010A}
.sec{background:#2B3266;color:#E6E9FF}
.gho{background:#00010A;color:#C9CDEA;box-shadow:inset 0 0 0 1px #434C89}
.off{background:#151A3A;color:#5A6196;cursor:default}
.btn .kc{border-color:currentColor;color:inherit;opacity:.8}
.btn:focus-visible,.pick:focus-visible,.tabw:focus-visible{outline:2px solid #00D4FF;outline-offset:2px}
.pick,.tabw{background:none;border:0;padding:0;cursor:pointer;color:inherit;font:inherit;text-align:left}
.panel{background:rgba(8,12,40,.92);box-shadow:inset 0 0 0 1px #2B3266}
.rule{border-top:1px solid #222645}
.pz{position:absolute}
.pz svg{display:block;overflow:visible}
@keyframes ldFill{0%{height:0%}8%{height:6%}40%{height:52%}70%{height:84%}88%{height:100%}100%{height:100%}}
@keyframes ldBar{0%{width:0%}8%{width:6%}40%{width:52%}70%{width:84%}88%{width:100%}100%{width:100%}}
@keyframes ldT1{0%,6%{opacity:.25}12%,100%{opacity:1}}
@keyframes ldT2{0%,30%{opacity:.25}38%,100%{opacity:1}}
@keyframes ldT3{0%,58%{opacity:.25}66%,100%{opacity:1}}
@keyframes ldT4{0%,80%{opacity:.25}88%,100%{opacity:1}}
@keyframes ldU1{0%{width:0}10%,100%{width:100%}}
@keyframes ldU2{0%,12%{width:0}36%,100%{width:100%}}
@keyframes ldU3{0%,38%{width:0}64%,100%{width:100%}}
@keyframes ldU4{0%,66%{width:0}86%,100%{width:100%}}
@keyframes ldIn{from{opacity:0}to{opacity:1}}
@keyframes tstIn{from{opacity:0;transform:translateX(40px)}to{opacity:1;transform:none}}
</style>
</helmet>"""


def kc(t):
    return f'<span class="kc">{t}</span>'


def sea_lines(w=1920, h=1080, top=0, alpha=.32):
    lines = []
    for k, (amp, per, y0, col) in enumerate([(18, 620, .34, "#49B8FF"), (24, 820, .58, "#7F98FF"), (14, 540, .8, "#9C8CFF")]):
        pts = " ".join(f"{x},{y0 * h + amp * math.sin(2 * math.pi * x / per + k):.1f}" for x in range(0, w + 1, 16))
        lines.append(f'<polyline points="{pts}" fill="none" stroke="{col}" stroke-opacity="{alpha}" stroke-width="1.5"></polyline>')
    return (f'<svg aria-hidden="true" width="{w}" height="{h}" style="position:absolute;left:0;top:{top}px">'
            + "".join(lines) + "</svg>")


def ground():
    return (f'<img src="{SEA_BG}" alt="" style="position:absolute;left:0;top:0;width:1920px;height:1080px;object-fit:cover">'
            '<div class="abs" style="left:0;top:84px;width:1920px;height:996px;background:linear-gradient(180deg,rgba(8,22,84,.95) 0%,'
            'rgba(3,9,44,.95) 48%,rgba(0,2,14,.96) 100%)"></div>' + sea_lines(top=84, h=996, alpha=.22))


def topnav():
    items = []
    for name, icon in NAV:
        on = name == "HOME"
        u = f'<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:{JADE}"></span>' if on else ""
        items.append(f'<div style="position:relative;display:flex;align-items:center;gap:10px;font-family:\'Chakra Petch\';font-weight:600;'
                     f'font-size:17px;letter-spacing:.12em;color:{TEXT if on else SOFT}"><img src="{icon}" alt="" style="width:26px;height:26px;'
                     f'object-fit:contain;opacity:{1 if on else .7}">{name}{u}</div>')
    return ('<nav aria-label="Main" style="position:absolute;left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.75);border-bottom:1px solid #222645;'
            'display:flex;align-items:center;justify-content:center;gap:48px;z-index:5">' + kc("LT") + "".join(items) + kc("RT") + "</nav>")


def header():
    return ('<div class="abs" style="left:40px;top:106px;display:flex;flex-direction:column;gap:6px"><span class="lab">Profile</span>'
            '<div style="font-size:30px">Your pilot</div></div>'
            '<div class="abs" style="right:40px;top:108px"><button class="btn gho" type="button">' + kc("B") + 'Back</button></div>')


def stat(label, value, sub):
    return (f'<div style="border-left:2px solid #434C89;padding:2px 0 2px 14px"><div class="lab" style="font-size:11px">{label}</div>'
            f'<div class="num" style="font-size:30px;margin-top:4px;white-space:nowrap">{value}</div>'
            f'<div style="font-size:12px;color:{SOFT};margin-top:2px">{sub}</div></div>')


def pilot_card(extra="", h=190, stats=5):
    st = [("Fly time", "5h 59m", "every match and freestyle"), ("Games", "148", "completed"), ("Sessions", "37", "times you launched"),
          ("Crystals", "8,450", "collected"), ("Pilot since", "Oct 2026", "first launch")][:stats]
    return (f'<section aria-label="Pilot" class="sl panel" style="position:absolute;left:40px;top:176px;width:1840px;height:{h}px">'
            '<div style="display:flex;align-items:center;gap:30px;padding:0 34px;height:100%;box-sizing:border-box">'
            f'<div class="oct" style="width:132px;height:132px;background:{LINE};padding:2px;box-sizing:border-box;flex:none">'
            f'<img class="oct" src="{AVATAR}" alt="Your avatar" style="width:128px;height:128px;object-fit:cover"></div>'
            '<div style="min-width:280px"><div class="lab">Pilot</div><div style="font-size:46px;margin-top:2px">Shomboss</div>'
            '<div style="display:flex;gap:12px;margin-top:12px"><button class="btn sec" type="button" style="height:44px">Change avatar</button>'
            '<button class="btn sec" type="button" style="height:44px">Rename</button></div></div>'
            '<div style="width:1px;align-self:stretch;margin:28px 0;background:#222645"></div>'
            f'<div style="display:grid;grid-template-columns:repeat({stats}, minmax(0, 1fr));gap:26px;flex:1">' + "".join(stat(*s) for s in st) + "</div>"
            + extra + "</div></section>")


def prism_svg(w, h, f, m, r, glow=True):
    """A prism as the game draws it: its face, the lit facet, the rim (Docs/PALETTE.md section 2)."""
    sh = f"filter:drop-shadow(0 0 {max(3, w // 6)}px {r}88);" if glow else ""
    return (f'<svg width="{w}" height="{h}" viewBox="0 0 40 80" preserveAspectRatio="none" style="{sh}" aria-hidden="true">'
            f'<polygon points="20,0 0,34 20,80 22,38" fill="{f}"></polygon><polygon points="20,0 40,34 20,80 22,38" fill="{m}"></polygon>'
            f'<polygon points="20,0 40,34 20,80 0,34" fill="none" stroke="{r}" stroke-width="2.2" stroke-linejoin="round" vector-effect="non-scaling-stroke"></polygon>'
            f'<line x1="20" y1="0" x2="22" y2="38" stroke="{r}" stroke-opacity=".55" stroke-width="1.2" vector-effect="non-scaling-stroke"></line></svg>')


def trio_static(pal, size=26):
    pal = pal or {k: GREY for k, _, _ in DOMS}
    return ('<div style="display:flex;align-items:flex-end;gap:6px">' + "".join(
        prism_svg(size, size * 2, pal[k]["f"], pal[k]["m"], pal[k]["r"]) for k, _, _ in DOMS) + "</div>")


# the preview cell: where each prism sits (px inside a 760 x 430 frame), its domain and its tier (0 prism, 1 shielded, 2 super)
def _layout():
    rnd = 0x5EED

    def r():
        nonlocal rnd
        rnd = (rnd * 1103515245 + 12345) & 0x7FFFFFFF
        return rnd / 0x7FFFFFFF
    out = []
    centres = {"jade": (150, 230), "ruby": (600, 220), "gold": (380, 120), "blue": (390, 330)}
    for d, (cx, cy) in centres.items():
        for i in range(7):
            a = r() * math.tau
            rad = 30 + r() * 95
            s = 18 + r() * 22
            tier = 0 if i < 4 else (1 if i < 6 else 2)
            out.append({"x": round(cx + math.cos(a) * rad - s / 2), "y": round(cy + math.sin(a) * rad * .6 - s), "w": round(s),
                        "h": round(s * 2), "d": d, "t": tier})
    out.sort(key=lambda p: p["y"])
    return out


LAYOUT = _layout()

TRAIL = "M -10 360 C 120 330, 220 300, 330 250 S 520 170, 640 150"


def preview_static(pal, w=760, h=430, scale=1.0):
    pal = pal or {k: GREY for k, _, _ in DOMS}
    sx, sy = w / 760, h / 430
    out = [f'<div style="position:relative;width:{w}px;height:{h}px;overflow:hidden;background:radial-gradient(ellipse 80% 70% at 50% 45%,#07104A,#00041F 70%,#00010A)">']
    out.append(hex_lattice(w, h, .16))
    for p in LAYOUT:
        c = pal[p["d"]]
        k = ["", "s", "x"][p["t"]]
        out.append(f'<div class="pz" style="left:{p["x"] * sx:.0f}px;top:{p["y"] * sy:.0f}px">'
                   + prism_svg(round(p["w"] * scale), round(p["h"] * scale), c[k + "f"], c[k + "m"], c[k + "r"]) + "</div>")
    out.append(f'<svg class="abs" style="left:0;top:0" width="{w}" height="{h}" viewBox="0 0 760 430" preserveAspectRatio="none" aria-hidden="true">'
               f'<path d="{TRAIL}" fill="none" stroke="{pal["jade"]["t"]}" stroke-width="5" stroke-linecap="round" stroke-opacity=".85"></path>'
               f'<polygon points="640,150 612,140 618,152 612,164" fill="{pal["jade"]["v"]}"></polygon></svg>')
    cta = (pal.get("cta") if isinstance(pal, dict) and "cta" in pal else "#5A63A0")
    out.append(f'<div class="abs hexc" style="left:{690 * sx:.0f}px;top:{60 * sy:.0f}px;width:30px;height:26px;background:{cta};'
               f'filter:drop-shadow(0 0 8px {cta})"></div></div>')
    return "".join(out)


def hex_lattice(w, h, alpha):
    s = 46
    paths = []
    for row in range(int(h / (s * .866)) + 2):
        for col in range(int(w / (s * 1.5)) + 2):
            cx = col * s * 1.5
            cy = row * s * 1.732 + (s * .866 if col % 2 else 0)
            pts = " ".join(f"{cx + s * math.cos(math.pi / 3 * i):.0f},{cy + s * math.sin(math.pi / 3 * i):.0f}" for i in range(6))
            paths.append(f'<polygon points="{pts}"></polygon>')
    return (f'<svg class="abs" style="left:0;top:0" width="{w}" height="{h}" aria-hidden="true"><g fill="none" stroke="#3D57B8" '
            f'stroke-opacity="{alpha}" stroke-width="1">' + "".join(paths[:600]) + "</g></svg>")


ICON_LOCK = '<svg width="14" height="16" viewBox="0 0 14 16" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><rect x="1.5" y="7" width="11" height="8"></rect><path d="M4 7V5a3 3 0 0 1 6 0v2"></path></svg>'
ICON_TICK = '<svg width="14" height="14" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M2 7.5l3.2 3L12 3.5"></path></svg>'
ICON_DOT = '<svg width="14" height="14" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M7 1.5l5 2.9v5.2L7 12.5 2 9.6V4.4z"></path></svg>'
ICON_ARROW = '<svg width="22" height="12" viewBox="0 0 22 12" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M1 6h19M15 1.5L20 6l-5 4.5"></path></svg>'


def state_line(s, status):
    if status == "equipped":
        return f'<span class="cap" style="display:inline-flex;gap:8px;align-items:center;font-size:12px;color:{LIME}">{ICON_TICK}<span style="color:{TEXT}">Equipped</span></span>'
    if status == "owned":
        return f'<span class="cap" style="font-size:12px;color:#C9CDEA">Owned - {s["how"]}</span>'
    if status == "progress":
        q = s["quest"]
        done = sum(1 for _, d in q["goals"] if d)
        return (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="cap" style="display:inline-flex;gap:8px;align-items:center;'
                f'font-size:12px;color:{SOFT}">{ICON_LOCK}Quest {q["n"]} - {q["mode"]} - {done} of {len(q["goals"])} goals</span>'
                f'<div style="height:4px;background:#1A1F44"><div style="height:4px;width:{100 * done / len(q["goals"]):.0f}%;background:#C9CDEA"></div></div></div>')
    if status == "notstarted":
        q = s["quest"]
        return (f'<span class="cap" style="display:inline-flex;gap:8px;align-items:center;font-size:12px;color:{SOFT}">{ICON_LOCK}'
                f'Unlocks after Quest {q["n"]} - {q["mode"]}</span>')
    return f'<span class="cap" style="font-size:12px;color:{SOFT}">In design - always free</span>'


def card_static(s, status, w=None, h=200, focus=False):
    locked = status in ("progress", "notstarted", "coming")
    ring = f"box-shadow:inset 0 0 0 2px {JADE}" if focus else f"box-shadow:inset 0 0 0 1px {LINE}"
    wcss = f"{w}px" if w else "100%"
    return (f'<div class="sl" style="position:relative;width:{wcss};height:{h}px;background:{"#060A26" if not locked else "#05081E"};{ring};box-sizing:border-box;'
            f'padding:18px 20px;display:flex;flex-direction:column;gap:10px">'
            f'<div style="display:flex;justify-content:space-between;align-items:flex-start"><div><div style="font-size:24px;'
            f'color:{TEXT if not locked else "#B4B9DD"}">{s["name"]}</div><div style="font-size:12px;color:{SOFT};margin-top:4px;max-width:{(w or 380) - 150}px">'
            f'{s["line"]}</div></div><div style="opacity:{1 if status != "notstarted" else .55}">{trio_static(s["pal"], 20)}</div></div>'
            f'<div style="flex:1"></div>{state_line(s, status)}</div>')


def board(title, body, logic="class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", w=1920, h=1080, props=None):
    p = {"$preview": {"width": w, "height": h}}
    p.update(props or {})
    return ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n'
            f'<title>{title}</title>\n<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n{HELMET}\n'
            f'<div style="width: {w}px; height: {h}px; position: relative; overflow: hidden; background: #00041F; color: {TEXT}; '
            f"font-family: 'Aldrich', 'Chakra Petch', sans-serif\">\n{body}\n</div>\n</x-dc>\n"
            f"<script type=\"text/x-dc\" data-dc-script data-props='{json.dumps(p)}'>\n{logic}\n</script>\n</body>\n</html>\n")


def title_of(key):
    canvas = json.loads((ROOT / "canvas.json").read_text())
    entry = canvas["boards"].get(f"R{REV}{key}.dc.html") or {}
    return entry.get("title") or f"Rev {REV} {key}"


# ---------------------------------------------------------------- the shared logic (A, B, Locked, Applied)

JS_SETS = [{"id": s["id"], "name": s["name"], "line": s["line"], "by": s["by"], "how": s["how"], "status": s["status"],
            "pal": s["pal"] or {k: GREY for k, _, _ in DOMS},
            "cta": (s["pal"] or {}).get("cta", "#5A63A0"),
            "quest": s.get("quest"),
            "pairs": [{"dom": n, "rim": s["pairs"][k][0], "face": s["pairs"][k][1],
                       "rc": (s["pal"] or {k: GREY})[k]["r"] if s["pal"] else GREY["r"],
                       "fc": (s["pal"] or {k: GREY})[k]["f"] if s["pal"] else GREY["f"]} for k, _, n in DOMS]}
           for s in SETS]

CORE = r"""const SETS = __SETS__;
const LAYOUT = __LAYOUT__;
const TIER = ['', 's', 'x'];
class Component extends DCLogic {
constructor(p){ super(p); this.state = { sel: this.props.start ?? 'nocturne', eq: this.props.equipped ?? 'nocturne', phase: this.props.phase ?? 'pick', open: this.props.open ?? true }; }
componentWillUnmount(){ if (this.t) clearTimeout(this.t); }
status(s){
  const unlocked = this.props.dartFrogUnlocked ?? false;
  if (s.id === this.state.eq) return 'equipped';
  if (s.status === 'coming') return 'coming';
  if (s.status === 'locked' && !unlocked) return 'locked';
  return 'owned';
}
renderVals() {
const st = this.state;
const cur = SETS.find(s => s.id === st.sel) || SETS[0];
const was = SETS.find(s => s.id === st.eq) || SETS[0];
const cs = this.status(cur);
const trio = (s) => ['jade', 'ruby', 'gold', 'blue'].map(k => ({ f: s.pal[k].f, m: s.pal[k].m, r: s.pal[k].r }));
const cards = SETS.map(s => {
  const k = this.status(s);
  const q = s.quest; const done = q ? q.goals.filter(g => g[1]).length : 0;
  return { name: s.name, line: s.line, trio: trio(s), on: s.id === st.sel,
    ring: s.id === st.sel ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #2B3266',
    pressed: s.id === st.sel ? 'true' : 'false', dim: k === 'locked' || k === 'coming' ? '.55' : '1',
    isEq: k === 'equipped', isOwned: k === 'owned', isLocked: k === 'locked', isComing: k === 'coming',
    how: s.how, qline: q ? 'Quest ' + q.n + ' - ' + q.mode + ' - ' + done + ' of ' + q.goals.length + ' goals' : '',
    qw: q ? Math.round(100 * done / q.goals.length) + '%' : '0%',
    pick: () => this.setState({ sel: s.id }) };
});
const cn = {}; cards.forEach((c, i) => { cn[SETS[i].id] = c; });
const back = LAYOUT.map(p => { const c = was.pal[p.d], t = TIER[p.t]; return { x: p.x, y: p.y, w: p.w, h: p.h, f: c[t + 'f'], m: c[t + 'm'], r: c[t + 'r'], g: '' }; });
const prisms = LAYOUT.map(p => { const c = cur.pal[p.d], t = TIER[p.t]; return { x: p.x, y: p.y, w: p.w, h: p.h, f: c[t + 'f'], m: c[t + 'm'], r: c[t + 'r'], g: 'filter:drop-shadow(0 0 ' + Math.max(3, Math.round(p.w / 6)) + 'px ' + c[t + 'r'] + '88)' }; });
const swap = ['jade', 'ruby', 'gold', 'blue'].map((k, i) => ({ dom: cur.pairs[i].dom, of: was.pal[k].f, orr: was.pal[k].r, nf: cur.pal[k].f, nr: cur.pal[k].r }));
const q = cur.quest;
const goals = q ? q.goals.map(g => ({ t: g[0], done: g[1], todo: !g[1] })) : [];
return {
  cards, cn, back, prisms, swap, goals,
  cur: { name: cur.name, line: cur.line, by: cur.by, how: cur.how, pairs: cur.pairs, trail: cur.pal.jade.t, vessel: cur.pal.jade.v, cta: cur.cta,
    quest: q ? 'Quest ' + q.n + ' - ' + q.mode : '', bands: ['jade', 'ruby', 'gold', 'blue'].map(k => ({ c: cur.pal[k].r, f: cur.pal[k].f })),
    oldBands: ['jade', 'ruby', 'gold', 'blue'].map(k => ({ c: was.pal[k].r, f: was.pal[k].f })) },
  was: { name: was.name },
  canApply: cs === 'owned' && st.phase === 'pick', isEq: cs === 'equipped', isLocked: cs === 'locked', isComing: cs === 'coming',
  loading: st.phase === 'loading', done: st.phase === 'done', open: st.open, closed: !st.open,
  apply: () => { if (cs !== 'owned') return; this.setState({ phase: 'loading', open: false });
    this.t = setTimeout(() => this.setState({ phase: 'done', eq: this.state.sel }), 3400); },
  dismiss: () => this.setState({ phase: 'pick' }),
  openWin: () => this.setState({ open: true, phase: 'pick' }),
  closeWin: () => this.setState({ open: false, sel: this.state.eq })
};
}
}"""


def core_js():
    return CORE.replace("__SETS__", json.dumps(JS_SETS)).replace("__LAYOUT__", json.dumps(LAYOUT))


PROPS_INTERACTIVE = {"dartFrogUnlocked": {"editor": "boolean", "default": True, "section": "State"},
                     "start": {"editor": "enum", "options": ["nocturne", "photic", "dartfrog", "colourblind"], "default": "nocturne", "section": "State"}}


def dyn_prism(item, wexpr, hexpr):
    return (f'<svg width="{wexpr}" height="{hexpr}" viewBox="0 0 40 80" preserveAspectRatio="none" style="{{{{{item}.g}}}}" aria-hidden="true">'
            f'<polygon points="20,0 0,34 20,80 22,38" fill="{{{{{item}.f}}}}"></polygon><polygon points="20,0 40,34 20,80 22,38" fill="{{{{{item}.m}}}}"></polygon>'
            f'<polygon points="20,0 40,34 20,80 0,34" fill="none" stroke="{{{{{item}.r}}}}" stroke-width="2.2" stroke-linejoin="round" vector-effect="non-scaling-stroke"></polygon></svg>')


def dyn_trio(size=20):
    return ('<div style="display:flex;align-items:flex-end;gap:6px"><sc-for list="{{c.trio}}" as="t" hint-placeholder-count="4">'
            f'<svg width="{size}" height="{size * 2}" viewBox="0 0 40 80" preserveAspectRatio="none" aria-hidden="true">'
            '<polygon points="20,0 0,34 20,80 22,38" fill="{{t.f}}"></polygon><polygon points="20,0 40,34 20,80 22,38" fill="{{t.m}}"></polygon>'
            '<polygon points="20,0 40,34 20,80 0,34" fill="none" stroke="{{t.r}}" stroke-width="2.2" stroke-linejoin="round" vector-effect="non-scaling-stroke"></polygon>'
            '</svg></sc-for></div>')


def dyn_state():
    return ('<sc-if value="{{c.isEq}}" hint-placeholder-val="{{ true }}"><span class="cap" style="display:inline-flex;gap:8px;align-items:center;font-size:12px;'
            f'color:{LIME}">{ICON_TICK}<span style="color:{TEXT}">Equipped</span></span></sc-if>'
            '<sc-if value="{{c.isOwned}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:12px;color:#C9CDEA">Owned - {{c.how}}</span></sc-if>'
            '<sc-if value="{{c.isLocked}}" hint-placeholder-val="{{ false }}"><div style="display:flex;flex-direction:column;gap:6px">'
            f'<span class="cap" style="display:inline-flex;gap:8px;align-items:center;font-size:12px;color:{SOFT}">{ICON_LOCK}' + '{{c.qline}}</span>'
            '<div style="height:4px;background:#1A1F44"><div style="height:4px;width:{{c.qw}};background:#C9CDEA"></div></div></div></sc-if>'
            f'<sc-if value="{{{{c.isComing}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="font-size:12px;color:{SOFT}">' + '{{c.how}}</span></sc-if>')


def dyn_card(w, h):
    return (f'<button class="pick sl" type="button" aria-pressed="{{{{c.pressed}}}}" onClick="{{{{c.pick}}}}" style="position:relative;width:{w}px;height:{h}px;'
            'background:#060A26;box-shadow:{{c.ring}};box-sizing:border-box;padding:18px 20px;display:flex;flex-direction:column;gap:10px">'
            '<div style="display:flex;justify-content:space-between;align-items:flex-start;width:100%"><div><div style="font-size:24px">{{c.name}}</div>'
            f'<div style="font-size:12px;color:{SOFT};margin-top:4px;max-width:{w - 170}px;line-height:1.4">' + '{{c.line}}</div></div>'
            '<div style="opacity:{{c.dim}}">' + dyn_trio(18) + '</div></div><div style="flex:1"></div>' + dyn_state() + '</button>')


def set_card(s):
    """A's card: static words and a small cell in the set's colours; the selection and state come from cn.<id>."""
    i = s["id"]
    st = SET[i]
    q = st.get("quest")
    locked_line = (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="cap" style="display:inline-flex;gap:8px;align-items:center;font-size:12px;'
                   f'color:{SOFT}">{ICON_LOCK}' + '{{cn.%s.qline}}</span><div style="height:4px;background:#1A1F44"><div style="height:4px;width:{{cn.%s.qw}};'
                   'background:#C9CDEA"></div></div></div>' % (i, i)) if q else ""
    return (f'<button class="pick sl" type="button" aria-pressed="{{{{cn.{i}.pressed}}}}" onClick="{{{{cn.{i}.pick}}}}" style="position:relative;width:490px;height:250px;'
            f'background:#060A26;box-shadow:{{{{cn.{i}.ring}}}};box-sizing:border-box;overflow:hidden">'
            f'<div class="abs" style="left:0;right:0;top:78px;height:112px;opacity:{{{{cn.{i}.dim}}}}">{preview_static(st["pal"], 490, 112, .5)}</div>'
            f'<div class="abs" style="left:20px;top:16px;right:20px"><div style="font-size:24px">{st["name"]}</div>'
            f'<div style="font-size:12px;color:{SOFT};margin-top:4px" class="ell">{st["line"]}</div></div>'
            f'<div class="abs" style="left:20px;right:20px;bottom:16px">'
            f'<sc-if value="{{{{cn.{i}.isEq}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="display:inline-flex;gap:8px;align-items:center;font-size:12px;color:{LIME}">{ICON_TICK}<span style="color:{TEXT}">Equipped</span></span></sc-if>'
            f'<sc-if value="{{{{cn.{i}.isOwned}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="font-size:12px;color:#C9CDEA">Owned - {st["how"]}</span></sc-if>'
            f'<sc-if value="{{{{cn.{i}.isLocked}}}}" hint-placeholder-val="{{{{ false }}}}">{locked_line}</sc-if>'
            f'<sc-if value="{{{{cn.{i}.isComing}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="font-size:12px;color:{SOFT}">{st["how"]}</span></sc-if>'
            '</div></button>')


def dyn_preview(w=760, h=430):
    sx, sy = w / 760, h / 430
    return (f'<div style="position:relative;width:{w}px;height:{h}px;overflow:hidden;background:radial-gradient(ellipse 80% 70% at 50% 45%,#07104A,#00041F 70%,#00010A)">'
            + hex_lattice(w, h, .16)
            + f'<div class="abs" style="left:0;top:0;width:760px;height:430px;transform:scale({sx:.4f},{sy:.4f});transform-origin:0 0">'
            '<sc-for list="{{prisms}}" as="p" hint-placeholder-count="28"><div class="pz" style="left:{{p.x}}px;top:{{p.y}}px">'
            + dyn_prism("p", "{{p.w}}", "{{p.h}}") + '</div></sc-for>'
            f'<svg class="abs" style="left:0;top:0" width="760" height="430" aria-hidden="true"><path d="{TRAIL}" fill="none" stroke="{{{{cur.trail}}}}" '
            'stroke-width="5" stroke-linecap="round" stroke-opacity=".85"></path><polygon points="640,150 612,140 618,152 612,164" fill="{{cur.vessel}}"></polygon></svg>'
            '<div class="abs hexc" style="left:690px;top:60px;width:30px;height:26px;background:{{cur.cta}}"></div></div></div>')


def dyn_pairs():
    return ('<div style="display:grid;grid-template-columns:repeat(2, minmax(0, 1fr));gap:10px 24px"><sc-for list="{{cur.pairs}}" as="pr" hint-placeholder-count="4">'
            '<div style="display:flex;align-items:center;gap:10px"><span class="lab" style="width:44px;font-size:11px">{{pr.dom}}</span>'
            '<span style="width:18px;height:18px;background:{{pr.rc}}"></span><span style="width:18px;height:18px;background:{{pr.fc}};box-shadow:inset 0 0 0 1px #434C89"></span>'
            '<span style="font-size:14px;color:#C9CDEA" class="ell">{{pr.rim}} over {{pr.face}}</span></div></sc-for></div>')


def loading_markup(anim):
    """The cell-change veil (EnvironmentLoadVeil, section 47.4) with its words for a colour-set change.
    `anim` is the CSS timing every layer shares: one loop, the steps keyed by percentages (no per-element delays)."""
    hexpts = "polygon(25% 0,75% 0,100% 50%,75% 100%,25% 100%,0 50%)"
    steps = [("Mixing the palette", "the four domain material sets are built from the new set"),
             ("Repainting prisms", "every prism in the cell takes its new rim and face"),
             ("Recolouring vessels and trails", "hulls, trails, crystals and the HUD's domain colours"),
             ("Opening the cell", "the veil lifts on the repainted HyperSea")]
    rows = []
    for i, (t, sub) in enumerate(steps, 1):
        rows.append(f'<div style="position:relative;display:flex;gap:16px;align-items:flex-start;padding:12px 0;animation:ldT{i} {anim}">'
                    f'<span style="color:#C9CDEA;margin-top:3px">{ICON_DOT}</span><div><div class="cap" style="font-size:17px">{t}</div>'
                    f'<div style="font-size:13px;color:{SOFT};margin-top:3px">{sub}</div></div>'
                    f'<span class="abs" style="left:0;bottom:0;height:1px;background:#C9CDEA;animation:ldU{i} {anim}"></span></div>')
    return ('<div class="abs" style="inset:0;background:#00010A">' + hex_lattice(1920, 1080, .22)
            + '<div class="abs" style="inset:0;background:radial-gradient(ellipse 60% 60% at 35% 50%,rgba(0,1,10,0),rgba(0,1,10,.85) 80%)"></div>'
            # the omni crystal hex: the old set above the line, the new set rising from the bottom
            + f'<div class="abs" style="left:250px;top:300px;width:480px;height:416px;clip-path:{hexpts};background:#0B1038">'
            '<div class="abs" style="inset:0;display:flex;opacity:.35"><sc-for list="{{cur.oldBands}}" as="b" hint-placeholder-count="4">'
            '<div style="flex:1;background:linear-gradient(180deg,{{b.c}},{{b.f}})"></div></sc-for></div>'
            f'<div class="abs" style="left:0;right:0;bottom:0;display:flex;animation:ldFill {anim}"><sc-for list="{{{{cur.bands}}}}" as="b" hint-placeholder-count="4">'
            '<div style="flex:1;background:linear-gradient(180deg,{{b.c}},{{b.f}})"></div></sc-for>'
            '<span class="abs" style="left:0;right:0;top:0;height:2px;background:#FFFFFF"></span></div></div>'
            '<svg class="abs" style="left:250px;top:300px" width="480" height="416" viewBox="0 0 480 416" aria-hidden="true"><g fill="none" stroke="#E6E9FF" stroke-opacity=".7" stroke-width="2">'
            '<polygon points="120,0 360,0 480,208 360,416 120,416 0,208"></polygon></g><g stroke="#E6E9FF" stroke-opacity=".22" stroke-width="1.5">'
            '<line x1="120" y1="0" x2="360" y2="416"></line><line x1="360" y1="0" x2="120" y2="416"></line><line x1="0" y1="208" x2="480" y2="208"></line></g></svg>'
            # the words
            '<div class="abs" style="left:860px;top:250px;width:900px">'
            '<div style="display:flex;align-items:center;gap:16px"><span style="width:60px;height:1px;background:#434C89"></span>'
            '<span class="lab" style="font-size:15px">Changing colour set</span><span style="width:60px;height:1px;background:#434C89"></span></div>'
            '<div style="font-size:96px;line-height:1.05;margin-top:14px">{{cur.name}}</div>'
            f'<div style="font-size:20px;color:#C9CDEA;margin-top:8px">' + '{{cur.line}}</div>'
            f'<div class="cap" style="display:flex;align-items:center;gap:12px;font-size:13px;color:{SOFT};margin-top:14px">' + '<span>{{was.name}}</span>'
            + ICON_ARROW + '<span style="color:#E6E9FF">{{cur.name}}</span></div>'
            # old -> new swatches
            '<div style="display:grid;grid-template-columns:repeat(4, minmax(0, 1fr));gap:18px;margin-top:30px"><sc-for list="{{swap}}" as="s" hint-placeholder-count="4">'
            '<div style="display:flex;flex-direction:column;gap:8px"><span class="lab" style="font-size:11px">{{s.dom}}</span><div style="display:flex;align-items:center;gap:8px">'
            '<span style="width:22px;height:22px;background:{{s.orr}};opacity:.6"></span><span style="width:22px;height:22px;background:{{s.of}};opacity:.6;box-shadow:inset 0 0 0 1px #434C89"></span>'
            f'<span style="color:{SOFT}">' + ICON_ARROW + '</span><span style="width:22px;height:22px;background:{{s.nr}}"></span>'
            '<span style="width:22px;height:22px;background:{{s.nf}};box-shadow:inset 0 0 0 1px #434C89"></span></div></div></sc-for></div>'
            f'<div style="margin-top:34px;border-top:1px solid #222645">' + "".join(rows) + '</div></div>'
            # footer
            '<div class="abs" style="left:250px;right:160px;bottom:70px;display:flex;align-items:flex-end;gap:40px">'
            '<div style="flex:1"><span class="lab" style="font-size:12px">Tip</span><div style="font-size:16px;color:#C9CDEA;margin-top:6px">'
            'A colour set changes how you see the HyperSea. Your domain, your squad and what other pilots see stay the same.</div></div>'
            '<div style="width:520px"><div style="display:flex;justify-content:space-between"><span class="lab" style="font-size:12px">Loading</span></div>'
            f'<div style="height:4px;background:#1A1F44;margin-top:10px"><div style="height:4px;background:#E6E9FF;animation:ldBar {anim}"></div></div></div></div></div>')


def profile_tabs_board(key, *, start="nocturne", equipped="nocturne", phase="pick", unlocked=True, focus_note=None):
    """Option A (and the Locked / Applied states): Record | Colour sets | Audio Studio, the set cards and a big preview."""
    tabs = []
    for name, on in (("Record", False), ("Colour sets", True), ("Audio Studio", False)):
        u = f'<span class="abs" style="left:0;right:0;bottom:-11px;height:2px;background:{JADE}"></span>' if on else ""
        tabs.append(f'<button class="tabw cap" type="button" aria-selected="{"true" if on else "false"}" role="tab" style="position:relative;font-size:18px;'
                    f'color:{TEXT if on else SOFT}">{name}{u}</button>')
    tabrow = ('<div role="tablist" class="abs" style="left:40px;top:400px;width:1840px;display:flex;align-items:center;gap:40px;padding-bottom:10px;'
              'border-bottom:1px solid #222645">' + kc("LB") + "".join(tabs) + kc("RB") + '<span style="flex:1"></span>'
              f'<span style="font-size:14px;color:{SOFT}">Colour sets change every prism, vessel, trail and crystal - only for you.</span></div>')
    grid = ('<div class="abs" style="left:40px;top:470px;width:1000px;display:grid;grid-template-columns:repeat(2, minmax(0, 1fr));gap:20px">'
            + "".join(set_card(s) for s in SETS) + '</div>')
    right = ('<section aria-label="Preview" class="abs" style="left:1060px;top:470px;width:820px;height:540px;box-sizing:border-box;padding:24px 30px">'
             '<div class="abs sl panel" style="inset:0"></div><div style="position:relative">'
             '<div style="display:flex;justify-content:space-between;align-items:baseline"><div><span class="lab">Preview</span>'
             '<div style="font-size:32px;margin-top:4px">{{cur.name}}</div></div><span style="font-size:13px;color:' + SOFT + '">{{cur.by}}</span></div>'
             '<div class="sl" style="margin-top:14px;overflow:hidden">' + dyn_preview(760, 300) + '</div>'
             '<div style="margin-top:16px">' + dyn_pairs() + '</div></div>'
             # the action, straddling the panel's foot
             '<div class="abs" style="left:30px;right:30px;bottom:-26px;display:flex;gap:14px;align-items:center;justify-content:flex-end">'
             '<sc-if value="{{canApply}}" hint-placeholder-val="{{ false }}"><button class="btn cta" type="button" onClick="{{apply}}">' + kc("A") + 'Apply</button></sc-if>'
             '<sc-if value="{{isEq}}" hint-placeholder-val="{{ true }}"><span class="btn off">' + ICON_TICK + 'Equipped</span></sc-if>'
             '<sc-if value="{{isLocked}}" hint-placeholder-val="{{ false }}"><span style="font-size:14px;color:' + SOFT + ';margin-right:6px">Finish {{cur.quest}} to use it</span>'
             f'<a class="btn sec" href="R{REV}Unlock.dc.html">' + kc("Y") + 'Open Voyage</a><span class="btn off">' + ICON_LOCK + 'Apply</span></sc-if>'
             '<sc-if value="{{isComing}}" hint-placeholder-val="{{ false }}"><span style="font-size:14px;color:' + SOFT + '">In design. Free for every pilot when it ships.</span></sc-if>'
             '</div></section>')
    locked_goals = ('<sc-if value="{{isLocked}}" hint-placeholder-val="{{ false }}"><div class="abs sl" style="left:1090px;top:782px;width:760px;'
                    'background:rgba(0,1,10,.92);box-shadow:inset 0 0 0 1px #434C89;padding:16px 22px;box-sizing:border-box;display:flex;gap:28px;align-items:center">'
                    f'<span style="color:{SOFT}">{ICON_LOCK}</span><div style="flex:1"><div class="cap" style="font-size:14px">Earned in Episode 1 - ' + '{{cur.quest}}</div>'
                    '<div style="display:flex;gap:26px;margin-top:8px"><sc-for list="{{goals}}" as="g" hint-placeholder-count="2"><span style="display:inline-flex;gap:8px;align-items:center;font-size:14px">'
                    f'<sc-if value="{{{{g.done}}}}" hint-placeholder-val="{{{{ true }}}}"><span style="color:{LIME}">{ICON_TICK}</span></sc-if>'
                    f'<sc-if value="{{{{g.todo}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="color:{SOFT}">{ICON_DOT}</span></sc-if>' + '{{g.t}}</span></sc-for></div></div></div></sc-if>')
    toast = ('<sc-if value="{{done}}" hint-placeholder-val="{{ false }}"><div role="status" class="abs sls" style="right:40px;top:104px;width:460px;background:#00010A;'
             'box-shadow:inset 0 0 0 1px #434C89;display:flex;z-index:8;animation:tstIn 320ms cubic-bezier(.22,1,.36,1) both">'
             f'<span style="width:3px;background:{LIME}"></span><div style="padding:14px 18px"><div class="cap" style="font-size:14px">Colour set applied</div>'
             f'<div style="font-size:14px;color:#C9CDEA;margin-top:4px">' + '{{cur.name}} is on every prism, vessel and trail.</div></div>'
             '<button class="pick" type="button" onClick="{{dismiss}}" aria-label="Close" style="margin-left:auto;padding:0 14px;color:' + SOFT + '">'
             '<svg width="12" height="12" viewBox="0 0 12 12" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M1 1l10 10M11 1L1 11"></path></svg></button></div></sc-if>')
    overlay = ('<sc-if value="{{loading}}" hint-placeholder-val="{{ false }}"><div class="abs" style="inset:0;z-index:20;animation:ldIn 200ms both">'
               + loading_markup("3.4s linear 1 both") + '</div></sc-if>')
    # behind the veil: the menu cell, in the EQUIPPED set's colours (it repaints when a set is applied)
    backdrop = ('<div class="abs" aria-hidden="true" style="left:-120px;top:84px;width:760px;height:430px;transform:scale(2.85,2.4);transform-origin:0 0;opacity:.13">'
                '<sc-for list="{{back}}" as="p" hint-placeholder-count="28"><div class="pz" style="left:{{p.x}}px;top:{{p.y}}px">'
                + dyn_prism("p", "{{p.w}}", "{{p.h}}") + '</div></sc-for></div>')
    body = (ground() + backdrop + topnav() + header() + pilot_card() + tabrow + grid + right + locked_goals + toast + overlay)
    props = dict(PROPS_INTERACTIVE)
    props["start"] = dict(props["start"], default=start)
    props["dartFrogUnlocked"] = dict(props["dartFrogUnlocked"], default=unlocked)
    logic = core_js().replace("this.props.equipped ?? 'nocturne'", f"this.props.equipped ?? '{equipped}'").replace(
        "this.props.phase ?? 'pick'", f"this.props.phase ?? '{phase}'")
    return board(title_of(key), body, logic, props=props)


def chip_board():
    """Option B: the colour set on the pilot card; CHANGE opens the Colour sets window."""
    chip = ('<div style="width:1px;align-self:stretch;margin:28px 0;background:#222645"></div>'
            '<div style="width:330px;flex:none"><div class="lab">Colour set</div><div style="font-size:26px;margin-top:4px">{{was.name}}</div>'
            '<div style="display:flex;align-items:center;gap:16px;margin-top:10px"><sc-for list="{{cards}}" as="c" hint-placeholder-count="1">'
            '<sc-if value="{{c.isEq}}" hint-placeholder-val="{{ true }}">' + dyn_trio(16) + '</sc-if></sc-for>'
            '<button class="btn sec" type="button" style="height:44px" onClick="{{openWin}}">' + kc("X") + 'Change</button></div></div>')
    record = ('<div class="abs sl panel" style="left:40px;top:400px;width:1100px;height:630px;padding:24px 30px;box-sizing:border-box">'
              '<span class="lab">Favourites</span><div style="display:grid;grid-template-columns:repeat(2, minmax(0, 1fr));gap:24px;margin-top:16px">'
              '<div style="height:220px;box-shadow:inset 0 0 0 1px #222645;padding:18px 20px;box-sizing:border-box"><span class="lab" style="font-size:11px">Favourite vessel</span>'
              '<div style="font-size:34px;margin-top:6px">Sparrow</div><div style="font-size:13px;color:' + SOFT + '">most flown - 2h 40m</div></div>'
              '<div style="height:220px;box-shadow:inset 0 0 0 1px #222645;padding:18px 20px;box-sizing:border-box"><span class="lab" style="font-size:11px">Favourite pilot</span>'
              '<div style="font-size:34px;margin-top:6px">Feist</div><div style="font-size:13px;color:' + SOFT + '">The Space Sparrow</div></div></div>'
              '<div class="rule" style="margin-top:26px;padding-top:18px"><span class="lab">Fly time by vessel</span>'
              + "".join(f'<div style="display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid #161B3D;font-size:17px"><span>{v}</span>'
                        f'<span class="num" style="color:#C9CDEA">{t}</span></div>' for v, t in (("Sparrow", "2h 40m"), ("Squirrel", "1h 15m"), ("Manta", "51m"), ("Dolphin", "34m")))
              + '</div></div>'
              '<div class="abs sl panel" style="left:1160px;top:400px;width:720px;height:630px;padding:24px 30px;box-sizing:border-box"><span class="lab">Audio Studio</span>'
              + "".join(f'<div style="display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #161B3D;font-size:17px"><span>{t}</span>'
                        f'<span class="cap" style="font-size:12px;color:{SOFT}">Play</span></div>' for t in ("Space Gliding", "Sky Fire", "Cosmic Shore 2 Tune", "Chill Time 3", "I've Waited So Long", "Cosmic Shore 3", "Severity"))
              + '</div>')
    rows = ('<sc-for list="{{cards}}" as="c" hint-placeholder-count="4"><button class="pick sl" type="button" aria-pressed="{{c.pressed}}" onClick="{{c.pick}}" '
            'style="position:relative;width:440px;height:118px;background:#060A26;box-shadow:{{c.ring}};box-sizing:border-box;padding:14px 18px;display:flex;gap:18px;align-items:center">'
            '<div style="opacity:{{c.dim}}">' + dyn_trio(16) + '</div><div style="flex:1;display:flex;flex-direction:column;gap:8px"><div style="font-size:22px">{{c.name}}</div>'
            + dyn_state() + '</div></button></sc-for>')
    window = ('<sc-if value="{{open}}" hint-placeholder-val="{{ true }}"><div class="abs" style="inset:0;background:rgba(0,1,10,.72);z-index:9"></div>'
              '<section role="dialog" aria-label="Colour sets" class="abs" style="left:240px;top:150px;width:1440px;height:800px;z-index:10;box-sizing:border-box;padding:30px 36px">'
              '<div class="abs sl" style="inset:0;background:#00010A;box-shadow:inset 0 0 0 1px #434C89"></div><div style="position:relative">'
              '<div style="display:flex;justify-content:space-between;align-items:baseline"><div><span class="lab">Profile</span><div style="font-size:36px;margin-top:4px">Colour sets</div></div>'
              f'<span style="font-size:15px;color:{SOFT}">Changes every prism, vessel, trail and crystal - only for you. Every set is unlocked for now.</span></div>'
              '<div style="display:flex;gap:30px;margin-top:24px"><div style="display:flex;flex-direction:column;gap:14px">' + rows + '</div>'
              '<div style="flex:1"><div class="sl" style="overflow:hidden">' + dyn_preview(890, 440) + '</div><div style="display:flex;justify-content:space-between;'
              'align-items:baseline;margin-top:16px"><div style="font-size:28px">{{cur.name}}</div><span style="font-size:13px;color:' + SOFT + '">{{cur.by}}</span></div>'
              '<div style="margin-top:12px">' + dyn_pairs() + '</div></div></div></div>'
              '<div class="abs" style="left:36px;right:36px;bottom:-26px;display:flex;gap:14px;align-items:center;justify-content:center">'
              '<sc-if value="{{canApply}}" hint-placeholder-val="{{ false }}"><button class="btn cta" type="button" onClick="{{apply}}">' + kc("A") + 'Apply</button></sc-if>'
              '<sc-if value="{{isEq}}" hint-placeholder-val="{{ true }}"><span class="btn off">' + ICON_TICK + 'Equipped</span></sc-if>'
              f'<sc-if value="{{{{isLocked}}}}" hint-placeholder-val="{{{{ false }}}}"><a class="btn sec" href="R{REV}Unlock.dc.html">' + kc("Y") + 'Open Voyage</a>'
              '<span class="btn off">' + ICON_LOCK + 'Finish {{cur.quest}}</span></sc-if>'
              '<sc-if value="{{isComing}}" hint-placeholder-val="{{ false }}"><span class="btn off">In design - always free</span></sc-if>'
              '<button class="btn gho" type="button" onClick="{{closeWin}}">' + kc("B") + 'Cancel</button></div></section></sc-if>')
    toast = ('<sc-if value="{{done}}" hint-placeholder-val="{{ false }}"><div role="status" class="abs sls" style="right:40px;top:104px;width:460px;background:#00010A;'
             'box-shadow:inset 0 0 0 1px #434C89;display:flex;z-index:8;animation:tstIn 320ms cubic-bezier(.22,1,.36,1) both">'
             f'<span style="width:3px;background:{LIME}"></span><div style="padding:14px 18px"><div class="cap" style="font-size:14px">Colour set applied</div>'
             '<div style="font-size:14px;color:#C9CDEA;margin-top:4px">{{cur.name}} is on every prism, vessel and trail.</div></div></div></sc-if>')
    overlay = ('<sc-if value="{{loading}}" hint-placeholder-val="{{ false }}"><div class="abs" style="inset:0;z-index:20;animation:ldIn 200ms both">'
               + loading_markup("3.4s linear 1 both") + '</div></sc-if>')
    body = ground() + topnav() + header() + pilot_card(chip, stats=3) + record + window + toast + overlay
    props = dict(PROPS_INTERACTIVE)
    props["start"] = dict(props["start"], default="photic")
    return board(title_of("B"), body, core_js(), props=props)


def shelf_board():
    """Option C: a shelf of large set cards under the pilot card; the record moves down."""
    cards = []
    order = [("nocturne", "equipped", False), ("photic", "owned", True), ("dartfrog", "owned", False), ("colourblind", "coming", False)]
    for sid, status, focus in order:
        s = SET[sid]
        locked = status in ("progress", "coming")
        ring = f"box-shadow:inset 0 0 0 2px {JADE}" if focus else f"box-shadow:inset 0 0 0 1px {LINE}"
        act = (f'<div class="abs" style="left:24px;right:24px;bottom:-26px;display:flex;justify-content:center"><button class="btn cta" type="button">{kc("A")}Apply</button></div>'
               if focus else "")
        cards.append(f'<div style="position:relative;width:440px;height:470px">'
                     f'<div class="sl" style="position:absolute;inset:0;background:#060A26;{ring};overflow:hidden">'
                     f'<div style="opacity:{.5 if locked else 1}">{preview_static(s["pal"], 440, 300, .9)}</div>'
                     f'<div style="padding:16px 22px;display:flex;flex-direction:column;gap:8px"><div style="font-size:28px">{s["name"]}</div>'
                     f'<div style="font-size:13px;color:{SOFT};line-height:1.4">{s["line"]}</div><div style="margin-top:6px">{state_line(s, status)}</div></div></div>{act}</div>')
    shelf = ('<div class="abs" style="left:40px;top:400px;width:1840px;display:flex;align-items:baseline;justify-content:space-between">'
             f'<span class="lab" style="font-size:15px">Colour sets</span><span style="font-size:14px;color:{SOFT}">LB / RB or the stick to move - '
             'every set unlocked for now</span></div><div class="abs" style="left:40px;top:436px;width:1840px;display:flex;gap:26px">' + "".join(cards) + '</div>')
    record = ('<div class="abs" style="left:40px;top:960px;width:1840px;display:flex;gap:24px;opacity:.6">'
              + "".join(f'<div class="sl panel" style="flex:1;height:140px;padding:18px 24px;box-sizing:border-box"><span class="lab">{t}</span>'
                        f'<div style="font-size:15px;color:#C9CDEA;margin-top:10px">{d}</div></div>'
                        for t, d in (("Favourites", "Sparrow - Feist"), ("Fly time by vessel", "Sparrow 2h 40m - Squirrel 1h 15m - Manta 51m"),
                                     ("Audio Studio", "7 of 9 tracks - now Space Gliding"))) + '</div>')
    body = ground() + topnav() + header() + pilot_card(h=200) + shelf + record
    return board(title_of("C"), body)


def compare_board():
    states = [("equipped", "Equipped"), ("owned", "Owned - every set, for now"), ("progress", "Later: quest in progress"), ("notstarted", "Later: quest not started")]
    rows = []
    for s in SETS:
        if s["id"] == "colourblind":
            cells = [card_static(s, "coming"), '<div></div>', '<div></div>', '<div></div>']
            cells[0] = card_static(s, "coming")
        else:
            cells = []
            for st, _ in states:
                if st in ("progress", "notstarted") and "quest" not in s:
                    cells.append(f'<div style="height:200px;display:flex;align-items:center;justify-content:center;font-size:13px;color:#434C89;'
                                 f'box-shadow:inset 0 0 0 1px #161B3D">Never locked: the default</div>')
                else:
                    cells.append(card_static(s, st))
        rows.append(f'<div style="display:grid;grid-template-columns:260px repeat(4, minmax(0, 1fr));gap:22px;align-items:center">'
                    f'<div><div style="font-size:26px">{s["name"]}</div><div style="margin-top:10px">{trio_static(s["pal"], 18)}</div>'
                    f'<div style="font-size:12px;color:{SOFT};margin-top:10px;line-height:1.4">{s["by"]}</div></div>' + "".join(cells) + '</div>')
    head = ('<div style="display:grid;grid-template-columns:260px repeat(4, minmax(0, 1fr));gap:22px;margin-bottom:8px"><span></span>'
            + "".join(f'<span class="lab" style="font-size:14px">{n}</span>' for _, n in states) + '</div>')
    strip = ('<div style="display:flex;gap:22px;margin-top:20px">' + "".join(
        f'<div style="flex:1"><div class="lab" style="font-size:11px;margin-bottom:6px">{s["name"]} - the cell</div><div class="sl" style="overflow:hidden">'
        f'{preview_static(s["pal"], 420, 150, .6)}</div></div>' for s in SETS) + '</div>')
    body = (f'<div style="padding:40px 56px"><span class="lab">Rev {REV} - compare</span><div style="font-size:34px;margin:6px 0 4px">Every colour set, in every state its card can show</div>'
            f'<div style="font-size:15px;color:{SOFT};margin-bottom:22px">The prisms are each set\'s shipped values (SO_ColorSet, normal tier, Jade Ruby Gold Blue). '
            'The selection is one Jade line. For now every set is unlocked (Shombith, 5 Nov); the two Later columns are for when quests give sets.</div>'
            + head + '<div style="display:flex;flex-direction:column;gap:18px">' + "".join(rows) + '</div></div>')
    return board(title_of("Compare"), body)


def unlock_board():
    quests = [("1", "Scurry", "Rank first at Intensity 3", "done", ""), ("2", "Hex Race", "Rank first at Intensity 3", "done", "Photic colour set"),
              ("3", "Joust", "Win at Intensity 3", "done", ""), ("4", "Maelstrom", "Play Intensity 3", "claim", "Dart Frog colour set"),
              ("5", "Vessel Hangar", "Complete all game mode quests", "locked", "")]
    rows = []
    for n, mode, goal, st, reward in quests:
        if st == "done":
            right = f'<span class="cap" style="display:inline-flex;gap:8px;align-items:center;font-size:12px;color:{LIME}">{ICON_TICK}<span style="color:#C9CDEA">Complete</span></span>'
        elif st == "claim":
            right = f'<span class="cap" style="display:inline-flex;gap:8px;align-items:center;font-size:12px;color:{LIME}">{ICON_TICK}<span style="color:#C9CDEA">Claimed</span></span>'
        else:
            right = f'<span class="cap" style="display:inline-flex;gap:8px;align-items:center;font-size:12px;color:{SOFT}">{ICON_LOCK}Unlocks after Quest 4</span>'
        rw = (f'<div style="font-size:13px;color:#C9CDEA;margin-top:4px">Reward: {reward}</div>' if reward else "")
        ring = f"box-shadow:inset 0 0 0 2px {JADE}" if st == "claim" else "box-shadow:inset 0 0 0 1px #222645"
        rows.append(f'<div class="sls" style="display:flex;align-items:center;gap:18px;padding:16px 20px;background:#060A26;{ring}">'
                    f'<span class="num" style="font-size:28px;width:34px;color:{SOFT}">{n}</span><div style="flex:1"><div style="font-size:22px">{mode}</div>'
                    f'<div style="font-size:13px;color:{SOFT};margin-top:2px">{goal}</div>{rw}</div>{right}</div>')
    behind = ('<div class="abs sl panel" style="left:40px;top:120px;width:1840px;height:920px;padding:30px 36px;box-sizing:border-box">'
              '<div style="display:flex;justify-content:space-between;align-items:baseline"><div><span class="lab">Voyage</span><div style="font-size:34px;margin-top:4px">Episode 1</div></div>'
              f'<span class="cap" style="font-size:14px;color:#C9CDEA">4 of 5 complete</span></div><div style="display:flex;flex-direction:column;gap:12px;margin-top:22px;width:820px">'
              + "".join(rows) + '</div></div>')
    df = SET["dartfrog"]
    win = ('<div class="abs" style="inset:0;background:rgba(0,1,10,.7)"></div>'
           '<section role="dialog" aria-label="New colour set" class="abs" style="left:880px;top:190px;width:900px;height:720px;box-sizing:border-box;padding:30px 36px">'
           '<div class="abs sl" style="inset:0;background:#00010A;box-shadow:inset 0 0 0 1px #434C89"></div><div style="position:relative">'
           f'<span class="lab" style="color:{LIME}">Quest 4 reward</span><div style="font-size:20px;color:#C9CDEA;margin-top:4px">New colour set</div>'
           f'<div style="font-size:64px;margin-top:4px">{df["name"]}</div><div style="font-size:16px;color:#C9CDEA;margin-top:6px">{df["line"]}</div>'
           f'<div class="sl" style="overflow:hidden;margin-top:20px">{preview_static(df["pal"], 828, 300)}</div>'
           '<div style="display:grid;grid-template-columns:repeat(2, minmax(0, 1fr));gap:10px 24px;margin-top:18px">'
           + "".join(f'<div style="display:flex;align-items:center;gap:10px"><span class="lab" style="width:44px;font-size:11px">{n}</span>'
                     f'<span style="width:18px;height:18px;background:{df["pal"][k]["r"]}"></span><span style="width:18px;height:18px;background:{df["pal"][k]["f"]};'
                     f'box-shadow:inset 0 0 0 1px #434C89"></span><span style="font-size:14px;color:#C9CDEA">{df["pairs"][k][0]} over {df["pairs"][k][1]}</span></div>'
                     for k, _, n in DOMS) + '</div></div>'
           '<div class="abs" style="left:36px;right:36px;bottom:-26px;display:flex;gap:14px;justify-content:center">'
           f'<a class="btn cta" href="R{REV}Loading.dc.html">{kc("A")}Use it now</a><button class="btn gho" type="button">{kc("B")}Later</button></div>'
           f'<div class="abs" style="left:36px;bottom:40px;font-size:13px;color:{SOFT}">Later: it waits in Profile - Colour sets.</div></section>')
    body = ground() + topnav() + behind + win
    return board(title_of("Unlock"), body)


def loading_board():
    logic = core_js().replace("this.props.start ?? 'nocturne'", "this.props.start ?? 'dartfrog'")
    body = loading_markup("6s linear infinite")
    props = {"start": {"editor": "enum", "options": ["photic", "dartfrog", "nocturne"], "default": "dartfrog", "section": "State"},
             "equipped": {"editor": "enum", "options": ["nocturne", "photic", "dartfrog"], "default": "nocturne", "section": "State"}}
    return board(title_of("Loading"), body, logic, props=props)


def notes_board():
    cols = [
        ("The three options", [
            "A (recommended). The Profile gets a third tab: Record | Colour sets | Audio Studio, on LB / RB like the Voyage tabs. Four set cards and a big preview of the cell in the picked set. One more tab, nothing else on the screen moves; the preview is large enough to judge a palette.",
            "B. The set you wear sits on the pilot card with CHANGE (X); the record stays as built. The picker is a window over the Profile. Smallest change to the built screen; the preview is the biggest; one press deeper.",
            "C. A shelf of large cards under the pilot card, each its own preview. Most visible, but it pushes the record and Audio Studio below the fold, and the shelf grows with every set.",
            "Every option ends the same way: APPLY (lime: the player can change it back) closes on the cell-change loading screen, then the toast."]),
        ("Unlocked for now; quests later", [
            "For now every colour set is unlocked for every pilot (Shombith, 2026-11-05: the quests are not planned yet). Boards A, B, C, Loading and Applied show that.",
            "Later, a set is a quest reward, beside the mode it already unlocks: the quest asset gains an optional colour-set reward, and the owned sets save with the quest progress (QuestProgressCloudData).",
            "The Later boards use Quest 4, Maelstrom, for Dart Frog only as an example. Locked shows grey, the quest's name, its goals done / to do, and OPEN VOYAGE (Y). The preview still shows the set at full colour, so the player sees what they are playing for.",
            "The claim in the Voyage window opens a reward window: USE IT NOW repaints at once, LATER leaves it in Profile - Colour sets.",
            "Clear Sight (Protan, Deutan, Tritan) is never behind a quest: accessibility is free for everyone."]),
        ("What the repaint does in Unity", [
            "Today ThemeManager builds the four domain material sets once, at Awake, from ThemeManagerDataContainer.ColorSet, and hands the set to GameToastAPI and PrismLit. A switch rebuilds those sets and hands them over again.",
            "Prisms take their material when they spawn, so the honest repaint is a cell reload under the existing veil (EnvironmentLoadVeil). Its four steps get these words: mixing the palette, repainting prisms, recolouring vessels and trails, opening the cell.",
            "Not repainted today, because they read Photic (Docs/PALETTE.md 1): the hull materials, the card art, the studios and Amoebius. They need a per-set path before a set is a full repaint.",
            "Local only: a set changes how YOU see the game. Another pilot sees their own set; domains and squads are unchanged."]),
        ("Decisions for you", [
            "1. Which option: A, B or C.",
            "2. The word: 'Colour sets' (Garrett's, in colour_sets.json). 'Theme' is taken by the style guide's Deep Sea and Shore.",
            "3. Later: which quests give which sets (the Locked and Unlock boards are the shape, not the plan).",
            "4. Dart Frog: colour_sets.json has it on the paid track (Garrett). A quest reward, paid, or both? Garrett's call.",
            "5. Do the UI's domain colours follow the set? The style guide (D2) says a domain is the live palette, so the HUD's Jade / Ruby / Gold would change with it."]),
    ]
    html = []
    for t, items in cols:
        html.append(f'<div style="flex:1;min-width:0"><span class="lab" style="font-size:14px">{t}</span>'
                    + "".join(f'<p style="font-size:15px;line-height:1.5;color:#C9CDEA;margin:12px 0 0">{i}</p>' for i in items) + '</div>')
    body = (f'<div style="padding:40px 56px"><span class="lab">Rev {REV} - notes</span><div style="font-size:34px;margin:6px 0 4px">Colour sets in the Profile</div>'
            f'<div style="font-size:15px;color:{SOFT};margin-bottom:22px">Asked (Shombith, 2026-11-05): change the colour sets from inside the Profile; the player '
            'unlocks them through quests; once picked, the cell-change loading screen says the colours are changing, then the set is applied.</div>'
            '<div style="display:flex;gap:40px">' + "".join(html) + '</div></div>')
    return board(title_of("Notes"), body, w=1920, h=900)


def main():
    out = {
        "Compare": compare_board(),
        "A": profile_tabs_board("A"),
        "B": chip_board(),
        "C": shelf_board(),
        "Locked": profile_tabs_board("Locked", start="dartfrog", unlocked=False),
        "Unlock": unlock_board(),
        "Loading": loading_board(),
        "Applied": profile_tabs_board("Applied", start="dartfrog", equipped="dartfrog", phase="done", unlocked=True),
        "Notes": notes_board(),
    }
    for k, html in out.items():
        p = ROOT / f"R{REV}{k}.dc.html"
        p.write_text(html)
        print(f"wrote {p.name} ({len(html) // 1024} KB)")


if __name__ == "__main__":
    main()
