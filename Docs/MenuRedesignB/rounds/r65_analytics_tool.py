#!/usr/bin/env python3
"""Rev 65 boards: Froglet Analytics, the full tool (Shombith, 2026-11-06).

Asked: "need filters and sorting for sure"; the player profile "is not at all good there should different analytical
tools like pie charts, ganntt charts etc for business development"; "need features to filter solo vs ai, multiplayer";
"make it the best analytical tool ever ... 5-6 mockups there for players details new features etc. We should also give
control to admins more like creating new tables queries".

Six screens of the console (Tools/Analytics/console, Rev 50 B as built) in its default theme, Nocturne (the game's
colour set: Tools/Analytics/console/themes.css), with the menu's sea lines behind, plus a notes board. Every number is
invented sample data, drawn from one seeded set so the boards agree with each other.

    python3 Docs/MenuRedesignB/rounds/r65_analytics_tool.py <root>      # writes <root>/project/R65*.dc.html
"""
import json
import math
import random
import sys
from pathlib import Path

ROOT = Path(sys.argv[1]) / "project"
REV = 65

# ---------------------------------------------------------------- the Nocturne theme (themes.css, make_themes.py)
GROUND, DEEP, SURFACE, RAISED, LINE = "#13010C", "#050003", "#3C1D31", "#70405F", "#70405F"
TEXT, SOFT, OFF = "#F5E6EE", "#A8869A", "#6C5A65"
CTA, DANGER = "#C4F637", "#FF8169"
JADE, RUBY, GOLD, BLUE = "#65E1DF", "#DF5CE5", "#FFC549", "#7BA0FF"
SWATCH = {"nocturne": [("#65E1DF", "#480135"), ("#DF5CE5", "#00384E"), ("#FFC549", "#3F205F"), ("#7BA0FF", "#023B1B")],
          "photic": [("#42E1DC", "#006F9E"), ("#E500E3", "#611497"), ("#FFD400", "#866300"), ("#AABCFF", "#2A2A9F")],
          "dartfrog": [("#96E1BF", "#774325"), ("#C865E5", "#0D606C"), ("#FFC819", "#57288F"), ("#6EB5FF", "#06541F")]}
SERIES = [JADE, RUBY, GOLD, BLUE]
PLAY = [("solo_vs_ai", "Solo vs AI", JADE), ("solo", "Solo", GOLD), ("multi", "Multiplayer", RUBY)]
PLAY_COLOUR = {k: c for k, _, c in PLAY}

HELMET = f"""<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{{margin:0;background:{DEEP}}}
.lab{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.14em;text-transform:uppercase;color:{SOFT}}}
.h1{{font-family:'Aldrich',sans-serif;font-size:36px;color:{TEXT};margin:0;font-weight:400}}
.h2{{font-family:'Aldrich',sans-serif;font-size:28px;color:{TEXT};margin:0;font-weight:400}}
.h3{{font-family:'Aldrich',sans-serif;font-size:20px;color:{TEXT};margin:0;font-weight:400}}
.body{{font-family:'Aldrich',sans-serif;font-size:17px;line-height:1.45;color:{TEXT};margin:0}}
.small{{font-family:'Aldrich',sans-serif;font-size:14px;line-height:1.4;color:{SOFT};margin:0}}
.num{{font-variant-numeric:tabular-nums}}
.sl{{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}}
.sls{{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}}
.panel{{background:linear-gradient(160deg,rgba(112,64,95,.34) 0%,rgba(112,64,95,0) 38%),linear-gradient(180deg,rgba(60,29,49,.92),rgba(52,25,43,.9));box-shadow:inset 0 0 0 1px #7A5068,inset 0 1px 0 rgba(101,225,223,.45)}}
.nav{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:15px;letter-spacing:.1em;text-transform:uppercase;color:{SOFT};text-decoration:none;display:flex;align-items:center;gap:12px;height:42px;padding:0 16px}}
.nav svg{{width:20px;height:20px;fill:none;stroke:currentColor;stroke-width:1.6;flex:none}}
.nav.on{{color:{TEXT};background:{SURFACE};box-shadow:inset 2px 0 0 {JADE}}}
.seg{{display:flex;padding:0 4px;background:{SURFACE};box-shadow:inset 0 0 0 1px {LINE}}}
.seg button{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:12px;letter-spacing:.08em;text-transform:uppercase;border:0;background:transparent;color:{SOFT};height:38px;padding:0 9px;white-space:nowrap}}
.seg button.on{{color:{TEXT};box-shadow:inset 0 -2px 0 {JADE}}}
.chip{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.08em;text-transform:uppercase;display:inline-flex;align-items:center;gap:8px;height:36px;padding:0 14px;border:0;background:{SURFACE};color:{TEXT};box-shadow:inset 0 0 0 1px {LINE}}}
.chip.on{{box-shadow:inset 0 0 0 1px {JADE},inset 0 -2px 0 {JADE}}}
.chip i{{font-style:normal;color:{SOFT}}}
.btn{{font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:14px;letter-spacing:.12em;text-transform:uppercase;border:0;height:42px;padding:0 20px;display:inline-flex;align-items:center;gap:10px}}
.btn.cta{{background:{CTA};color:{GROUND}}}
.btn.def{{background:{TEXT};color:{GROUND}}}
.btn.ghost{{background:transparent;color:{TEXT};box-shadow:inset 0 0 0 1px {LINE}}}
.th{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:12px;letter-spacing:.12em;text-transform:uppercase;color:{SOFT};text-align:left;padding:8px 10px;border-bottom:1px solid {LINE};white-space:nowrap}}
.th.sort{{color:{TEXT}}}
.td{{font-family:'Aldrich',sans-serif;font-size:14px;color:{TEXT};padding:8px 10px;border-bottom:1px solid rgba(112,64,95,.6);font-variant-numeric:tabular-nums;white-space:nowrap}}
.field{{background:{DEEP};box-shadow:inset 0 0 0 1px {LINE};height:40px;display:flex;align-items:center;padding:0 14px;color:{SOFT};font-family:'Aldrich',sans-serif;font-size:15px}}
.code{{background:{DEEP};box-shadow:inset 0 0 0 1px {LINE};padding:10px 14px;font-family:'Aldrich',monospace;font-size:13px;line-height:1.45;color:{BLUE};white-space:pre;margin:0}}
.tag{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:11px;letter-spacing:.12em;text-transform:uppercase;padding:3px 8px;box-shadow:inset 0 0 0 1px {LINE};color:{SOFT}}}
</style>
</helmet>"""

ICONS = {
    "overview": '<path d="M3 3h6v6H3zM11 3h6v6h-6zM3 11h6v6H3zM11 11h6v6h-6z"/>',
    "players": '<circle cx="7" cy="7" r="3"/><path d="M2 17c0-3 2.2-5 5-5s5 2 5 5"/><circle cx="14" cy="6" r="2.5"/><path d="M13 11.5c2.8 0 5 1.8 5 5"/>',
    "cohorts": '<path d="M3 5h9M3 10h13M3 15h6"/>',
    "eras": '<path d="M3 4h14v13H3zM3 8h14M7 2v4M13 2v4"/>',
    "playtypes": '<circle cx="10" cy="10" r="7"/><path d="M10 3v7l6 3"/>',
    "retention": '<path d="M4 10a6 6 0 1 0 2-4.5"/><path d="M3 3v4h4"/>',
    "matches": '<path d="M3 17V9M8 17V4M13 17v-6M18 17V7"/>',
    "ftue": '<path d="M3 17V3l12 4-12 4"/>',
    "economy": '<path d="M10 2l6 8-6 8-6-8z"/>',
    "social": '<circle cx="6" cy="10" r="3"/><circle cx="14" cy="10" r="3"/>',
    "health": '<path d="M2 10h4l2-5 4 10 2-5h4"/>',
    "ask": '<path d="M3 4h14v9H8l-4 3v-3H3z"/>',
    "query": '<path d="M4 4h12v3H4zM4 9h12v3H4zM4 14h7v3H4z"/>',
    "dash": '<path d="M3 3h8v5H3zM13 3h4v9h-4zM3 10h8v7H3zM13 14h4v3h-4z"/>',
    "erasure": '<path d="M4 6h12M8 6V4h4v2M6 6l1 11h6l1-11"/>',
}
NAV = [("overview", "Overview"), ("players", "Players"), ("cohorts", "Cohorts"), ("eras", "Eras"), ("playtypes", "Play types"),
       ("retention", "Retention"), ("matches", "Matches"), ("ftue", "FTUE"), ("economy", "Economy"),
       ("social", "Social"), ("health", "Data health"), ("ask", "Ask Claude")]
ADMIN = [("query", "Query Studio"), ("dash", "Dashboards"), ("erasure", "Erasure")]


def icon(name, size=20, colour="currentColor"):
    return (f'<svg viewBox="0 0 20 20" width="{size}" height="{size}" style="fill:none;stroke:{colour};stroke-width:1.6;'
            f'flex:none" aria-hidden="true">{ICONS[name]}</svg>')


# ---------------------------------------------------------------- the sea lines (UISeaLines.cginc SeaLine maths)

def sea(w=1640, h=1080):
    """The three layers as static polylines (a board never moves), the set's Jade -> Blue -> Ruby across."""
    spacing, loop, amp = 46, 480, 9
    layers = [(0.2, 0.9), (0.3, 1.1), (0.42, 1.5)]
    k1 = 2 * math.pi / loop
    k2 = 3 * k1
    lines = []
    for i in range(0, h // spacing + 2):
        layer = i % 3
        a1 = amp * (1 + 0.45 * ((i * 7) % 3))
        a2 = amp * (0.45 + 0.22 * ((i * 5) % 3))
        pts = " ".join(f"{x},{i * spacing + a1 * math.sin(k1 * x + i * 0.9) + a2 * math.sin(k2 * x + i * 1.7):.1f}"
                       for x in range(0, w + 1, 12))
        alpha, width = layers[layer]
        lines.append(f'<polyline points="{pts}" fill="none" stroke="url(#r65sea)" stroke-opacity="{alpha}" '
                     f'stroke-width="{width}"></polyline>')
    return (f'<div style="position:absolute;inset:0;background:radial-gradient(ellipse 70% 55% at 50% 22%,'
            f'rgba(101,225,223,.10),rgba(101,225,223,0) 70%),linear-gradient(180deg,#210118 0%,#1A0112 50%,{GROUND} 100%)">'
            f'</div><div style="position:absolute;inset:0;-webkit-mask-image:linear-gradient(180deg,transparent 0%,#000 14%,'
            f'#000 86%,transparent 100%);mask-image:linear-gradient(180deg,transparent 0%,#000 14%,#000 86%,transparent 100%)">'
            f'<svg aria-hidden="true" width="{w}" height="{h}" style="position:absolute;left:0;top:0"><defs>'
            f'<linearGradient id="r65sea" x1="0" y1="0" x2="{w}" y2="0" gradientUnits="userSpaceOnUse">'
            f'<stop offset="0" stop-color="{JADE}"></stop><stop offset="0.5" stop-color="{BLUE}"></stop>'
            f'<stop offset="1" stop-color="{RUBY}"></stop></linearGradient></defs>{"".join(lines)}</svg></div>')


# ---------------------------------------------------------------- the frame every screen shares

def rail(active, admin=True):
    items = "".join(f'<a class="nav{" on" if k == active else ""}" href="#{k}">{icon(k)}{label}</a>' for k, label in NAV)
    adm = "".join(f'<a class="nav{" on" if k == active else ""}" href="#{k}">{icon(k)}{label}</a>' for k, label in ADMIN)
    themes = "".join(
        f'<div style="display:flex;align-items:center;gap:10px;height:34px;padding:0 8px;'
        f'{"background:" + SURFACE + ";box-shadow:inset 2px 0 0 " + JADE if t == "nocturne" else ""}">'
        f'<span style="display:flex;gap:2px">'
        + "".join(f'<i style="display:block;width:12px;height:18px;background:{f};box-shadow:inset 0 3px 0 {r}"></i>' for r, f in SWATCH[t])
        + f'</span><span style="font-size:14px;color:{TEXT if t == "nocturne" else SOFT}">{n}</span></div>'
        for t, n in (("nocturne", "Nocturne"), ("photic", "Photic"), ("dartfrog", "Dart Frog")))
    return (f'<div style="position:relative;background:rgba(5,0,3,.82);box-shadow:inset -1px 0 0 {LINE};padding:30px 14px 22px;'
            f'display:flex;flex-direction:column;gap:2px">'
            f'<div style="padding:0 16px 20px;display:flex;flex-direction:column;gap:4px"><span class="lab">Froglet</span>'
            f'<span class="h3">Analytics</span><span style="display:flex;gap:3px;margin-top:6px">'
            + "".join(f'<i style="display:block;width:26px;height:3px;background:{c}"></i>' for c in SERIES)
            + f'</span></div>{items}'
            f'<span class="lab" style="padding:16px 16px 6px;font-size:11px">Admin</span>{adm}'
            f'<div style="margin-top:auto;padding:12px 8px 0;display:flex;flex-direction:column;gap:2px">'
            f'<span class="lab" style="padding:0 8px 6px">Theme</span>{themes}</div>'
            f'<div style="padding:14px 16px 0"><span class="lab" style="font-size:11px">Signed in</span>'
            f'<div class="small" style="color:{TEXT}">shombith · admin</div></div></div>')


def segs(options, on):
    return '<div class="seg sls">' + "".join(
        f'<button type="button" class="{"on" if o == on else ""}">{o}</button>' for o in options) + "</div>"


def header(eyebrow, title, play="All", extra="", era="All time"):
    extra = f'<button type="button" class="chip sls" style="height:38px;white-space:nowrap">Era <i>{era}</i></button>' + extra
    return (f'<div style="display:flex;align-items:flex-end;justify-content:space-between;gap:20px">'
            f'<div style="display:flex;flex-direction:column;gap:6px;min-width:0"><span class="lab">{eyebrow}</span>'
            f'<h1 class="h1" style="white-space:nowrap">{title}</h1></div><div style="display:flex;gap:8px;align-items:center;flex:none">{extra}'
            + segs(["Release", "Editor", "Port", "Dev"], "Release") + segs(["7d", "30d", "90d", "365d"], "90d")
            + segs(["All", "Solo vs AI", "Solo", "Multiplayer"], play) + "</div></div>")


def screen(active, body, admin=True):
    return (f'<div style="width:1920px;height:1080px;box-sizing:border-box;position:relative;overflow:hidden;'
            f'background:{GROUND};color:{TEXT};font-family:\'Aldrich\',sans-serif;display:grid;'
            f'grid-template-columns:260px minmax(0,1fr)">{rail(active, admin)}'
            f'<div style="position:relative;overflow:hidden">{sea()}'
            f'<div style="position:relative;padding:28px 36px 28px;display:flex;flex-direction:column;gap:16px;height:100%;'
            f'box-sizing:border-box">{body}</div></div></div>')


def panel(title, body, note="", style=""):
    return (f'<div class="panel sl" style="padding:18px 20px;display:flex;flex-direction:column;gap:12px;min-width:0;{style}">'
            f'<div style="display:flex;justify-content:space-between;align-items:baseline;gap:12px">'
            f'<span class="lab" style="color:{TEXT}">{title}</span><span class="small">{note}</span></div>{body}</div>')


def tile(label, value, sub, colour=TEXT):
    return (f'<div class="panel sl" style="padding:16px 18px;display:flex;flex-direction:column;gap:4px;min-width:0">'
            f'<span class="lab">{label}</span><span class="num" style="font-size:34px;color:{colour}">{value}</span>'
            f'<span class="small">{sub}</span></div>')


def legend(items):
    return '<div style="display:flex;gap:16px;flex-wrap:wrap">' + "".join(
        f'<span class="small" style="display:inline-flex;align-items:center;gap:8px;color:{TEXT}">'
        f'<i style="display:block;width:12px;height:12px;background:{c}"></i>{t}</span>' for t, c in items) + "</div>"


# ---------------------------------------------------------------- charts (SVG, drawn here; marks per the dataviz spec)

def donut(parts, size=220, hole=0.62, label="", sub=""):
    total = sum(v for _, v, _ in parts) or 1
    r, cx = size / 2, size / 2
    out, a0 = [], -math.pi / 2
    for name, v, colour in parts:
        a1 = a0 + 2 * math.pi * v / total
        gap = 0.012                                 # a surface gap between slices
        s, e = a0 + gap, a1 - gap
        large = 1 if e - s > math.pi else 0
        ro, ri = r, r * hole
        p = (f"M{cx + ro * math.cos(s):.1f},{cx + ro * math.sin(s):.1f} A{ro},{ro} 0 {large} 1 "
             f"{cx + ro * math.cos(e):.1f},{cx + ro * math.sin(e):.1f} L{cx + ri * math.cos(e):.1f},{cx + ri * math.sin(e):.1f} "
             f"A{ri},{ri} 0 {large} 0 {cx + ri * math.cos(s):.1f},{cx + ri * math.sin(s):.1f} Z")
        out.append(f'<path d="{p}" fill="{colour}"></path>')
        a0 = a1
    return (f'<svg width="{size}" height="{size}" viewBox="0 0 {size} {size}" role="img" aria-label="{label}">{"".join(out)}'
            f'<text x="{cx}" y="{cx - 2}" text-anchor="middle" fill="{TEXT}" style="font:32px Aldrich">{label}</text>'
            f'<text x="{cx}" y="{cx + 24}" text-anchor="middle" fill="{SOFT}" style="font:13px Aldrich">{sub}</text></svg>')


def ranked_bars(rows, width=420, colour=BLUE, unit="", top=None):
    top = top or max(v for _, v in rows) or 1
    out = []
    for name, v in rows:
        w = max(2, (width - 190) * v / top)
        out.append(f'<div style="display:grid;grid-template-columns:130px {width - 190}px 50px;align-items:center;gap:10px;height:24px">'
                   f'<span class="small" style="color:{TEXT};overflow:hidden;text-overflow:ellipsis;white-space:nowrap">{name}</span>'
                   f'<span style="display:block;height:12px;width:{w:.0f}px;background:{colour}"></span>'
                   f'<span class="small num" style="color:{TEXT};text-align:right">{v}{unit}</span></div>')
    return '<div style="display:flex;flex-direction:column;gap:4px">' + "".join(out) + "</div>"


def mix_bar(parts, width=120, height=10):
    total = sum(v for v, _ in parts) or 1
    x, out = 0.0, []
    for v, c in parts:
        w = width * v / total
        if w >= 1:
            out.append(f'<rect x="{x:.1f}" y="0" width="{max(w - 2, 1):.1f}" height="{height}" fill="{c}"></rect>')
        x += w
    return f'<svg width="{width}" height="{height}" aria-hidden="true">{"".join(out)}</svg>'


# ---------------------------------------------------------------- the sample data (seeded, shared by every board)

R = random.Random(65)
NAMES = ["Dragon", "db1993", "Kes", "Mirelle", "orbweaver", "Tamsin", "nova_k", "Halcyon", "Bram", "pip.exe", "Juno",
         "Saffron", "Wren", "Talon", "Ilse", "Corvid", "Moss", "Quill", "Rook", "Sable", "Fen", "Lumen", "Vesper"]
COUNTRIES = [("India", "IN"), ("United States", "US"), ("United Kingdom", "GB"), ("Brazil", "BR"), ("Germany", "DE"),
             ("Philippines", "PH")]
PLATFORMS = ["WindowsPlayer", "Android", "IPhonePlayer", "WebGL"]
TODAY = 90
PLAYERS = []
for i, name in enumerate(NAMES):
    first = R.randint(0, 84)
    last = min(TODAY, first + int(R.expovariate(1 / 22)) + R.randint(0, 6))
    days = sorted({first, last} | {R.randint(first, last) for _ in range(R.randint(1, 14))})
    matches = max(1, int(len(days) * R.uniform(1.2, 4.5)))
    mv = R.random()
    multi = int(matches * (0.55 if mv > 0.7 else R.uniform(0, .25)))
    sva = int((matches - multi) * R.uniform(.45, .85))
    PLAYERS.append({"name": name, "id": "".join(R.choice("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789")
                                                for _ in range(22)),
                    "country": R.choices(COUNTRIES, weights=[5, 4, 2, 2, 1, 1])[0], "platform": R.choices(PLATFORMS, weights=[5, 3, 2, 1])[0],
                    "first": first, "last": last, "days": days, "matches": matches, "multi": multi, "solo_vs_ai": sva,
                    "solo": matches - multi - sva, "finished": int(matches * R.uniform(.45, .85)),
                    "won": int(matches * R.uniform(.05, .3)), "crystals": R.choice([0, 200, 850, 1600, 2500, 4200, 10200])})
PLAYERS[0].update({"country": ("India", "IN"), "platform": "WindowsPlayer", "matches": 53, "multi": 19, "solo_vs_ai": 12,
                   "solo": 22 - 0, "finished": 33, "won": 10, "crystals": 10200, "first": 66, "last": 90})
PLAYERS[0]["solo"] = 53 - 19 - 12
PLAYERS[0]["days"] = sorted({66, 90} | set(random.Random(66).sample(range(66, 91), 15)))


def date(day):
    """Sample day number (0 = 2026-08-08, 90 = today, 2026-11-06) as a date."""
    import datetime
    return (datetime.date(2026, 8, 8) + datetime.timedelta(days=day)).isoformat()


# ---------------------------------------------------------------- the boards

def board(title, body, w=1920, h=1080):
    logic = "class Component extends DCLogic {\n  renderVals() {\n    return {};\n  }\n}"
    return (f'<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>{title}</title>\n'
            f'<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n{HELMET}\n{body}\n</x-dc>\n'
            f"<script type=\"text/x-dc\" data-dc-script data-props='{json.dumps({'$preview': {'width': w, 'height': h}})}'>\n"
            f"{logic}\n</script>\n</body>\n</html>\n")


def lifespan(p, width=150, span=TODAY, h=12):
    x0, x1 = width * p["first"] / span, width * p["last"] / span
    churned = TODAY - p["last"] > 14
    dots = "".join(f'<rect x="{width * d / span - 1:.1f}" y="1" width="2" height="{h - 2}" fill="{TEXT}" opacity=".9"></rect>'
                   for d in p["days"])
    return (f'<svg width="{width}" height="{h}" aria-hidden="true"><rect x="0" y="{h / 2 - .5}" width="{width}" height="1" fill="{LINE}"></rect>'
            f'<rect x="{x0:.1f}" y="2" width="{max(x1 - x0, 3):.1f}" height="{h - 4}" fill="{BLUE}" opacity="{.35 if churned else .75}"></rect>{dots}</svg>')


def players_board():
    rows = sorted(PLAYERS, key=lambda p: -p["last"])[:13]
    head = "".join(f'<th class="th{" sort" if s else ""}">{t}{" " + s if s else ""}</th>' for t, s in (
        ("Player", ""), ("Country", ""), ("Platform", ""), ("Lifespan · 90 d", ""), ("Play mix", ""), ("Matches", ""),
        ("Finished", ""), ("Won", ""), ("Crystals", ""), ("Last seen", "&#9660;")))
    body = "".join(
        f'<tr><td class="td"><span style="text-decoration:underline">{p["name"]}</span><div class="small" style="font-size:12px">{p["id"]}</div></td>'
        f'<td class="td">{p["country"][0]} ({p["country"][1]})</td><td class="td">{p["platform"]}</td>'
        f'<td class="td">{lifespan(p)}</td>'
        f'<td class="td">{mix_bar([(p["solo_vs_ai"], JADE), (p["solo"], GOLD), (p["multi"], RUBY)])}</td>'
        f'<td class="td">{p["matches"]}</td><td class="td">{round(100 * p["finished"] / p["matches"])}%</td>'
        f'<td class="td">{p["won"]}</td><td class="td">{p["crystals"]:,}</td><td class="td">{date(p["last"])}</td></tr>'
        for p in rows)
    tot = {k: sum(p[k] for p in PLAYERS) for k in ("solo_vs_ai", "solo", "multi", "matches")}
    filters = (
        f'<div style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">'
        f'<div class="field sls" style="width:340px">Search name or player id</div>'
        f'<button type="button" class="chip on sls">Country <i>India, US +2</i></button>'
        f'<button type="button" class="chip sls">Platform <i>any</i></button>'
        f'<button type="button" class="chip on sls">Active <i>last 30 d</i></button>'
        f'<button type="button" class="chip sls">Matches <i>&#8805; 5</i></button>'
        f'<button type="button" class="chip sls">Has played <i>multiplayer</i></button>'
        f'<button type="button" class="chip sls">Crystals <i>any</i></button>'
        f'<span style="flex:1"></span>'
        f'<button type="button" class="chip sls">Segment <i>Lapsed 14 d</i></button>'
        f'<button type="button" class="btn ghost sls">Save segment</button><button type="button" class="btn def sls">Export CSV</button></div>')
    summary = (
        f'<div style="display:grid;grid-template-columns:repeat(4,minmax(0,1fr)) 2fr;gap:12px">'
        + tile("Players", "23", "match the filters, of 45") + tile("Matches", f'{tot["matches"]}', "by these players")
        + tile("Median lifespan", "16 d", "first to last seen") + tile("Lapsed", "6", "no play in 14 days", DANGER)
        + panel("Play mix of these players", mix_bar([(tot["solo_vs_ai"], JADE), (tot["solo"], GOLD), (tot["multi"], RUBY)], 440, 16)
                + legend([(f"Solo vs AI {tot['solo_vs_ai']}", JADE), (f"Solo {tot['solo']}", GOLD), (f"Multiplayer {tot['multi']}", RUBY)]))
        + "</div>")
    table = panel("Every player", f'<table style="border-collapse:collapse;width:100%"><thead><tr>{head}</tr></thead><tbody>{body}</tbody></table>',
                  "23 of 45 · sorted by last seen, newest first · click any column to sort", "flex:1;overflow:hidden")
    return screen("players", header("Players · release builds · last 90 days", "Players", "All") + filters + summary + table)


def profile_board():
    p = PLAYERS[0]
    modes = [("Tandava", 6), ("Scurry", 5), ("Switchback", 4), ("Redline", 3), ("Skein", 3), ("Bends", 2), ("Headlong", 2),
             ("Regatta", 2), ("Other (9)", 11)]
    vessels = [("Squirrel", 11), ("Dolphin", 6), ("Urchin", 5), ("Sparrow", 5), ("Scarab", 4), ("Manta", 4), ("Rhino", 3)]
    ident = (f'<div style="display:flex;align-items:flex-end;justify-content:space-between;gap:20px">'
             f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Players / profile</span>'
             f'<div style="display:flex;align-items:baseline;gap:16px"><h1 class="h1">Dragon</h1>'
             f'<span class="small">{p["id"]}</span></div>'
             f'<div style="display:flex;gap:8px"><span class="tag">India (IN)</span><span class="tag">WindowsPlayer</span>'
             f'<span class="tag">First seen 2026-10-15</span><span class="tag">Last seen today 15:53</span>'
             f'<span class="tag" style="color:{RUBY};box-shadow:inset 0 0 0 1px {RUBY}">Multiplayer regular</span></div></div>'
             f'<div style="display:flex;gap:10px"><button type="button" class="btn ghost sls">All players</button>'
             f'{segs(["All", "Solo vs AI", "Solo", "Multiplayer"], "All")}<button type="button" class="btn def sls">Export</button></div></div>')
    tiles = ('<div style="display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:12px">'
             + tile("Matches", "53", "17 active days") + tile("Finished", "62%", "33 of 53") + tile("Won", "19%", "10 matches")
             + tile("At the controls", "61 min", "1.2 min a match") + tile("Rematch", "48%", "within 10 min") + tile("Crystals", "10,200", "4,800 earned · 0 spent") + "</div>")
    play = panel("How they play", '<div style="display:flex;gap:22px;align-items:center">'
                 + donut([("Multiplayer", 19, RUBY), ("Solo vs AI", 12, JADE), ("Solo", 22, GOLD)], 200, label="53", sub="matches")
                 + '<div style="display:flex;flex-direction:column;gap:12px">'
                 + "".join(f'<div style="display:flex;flex-direction:column"><span class="small" style="display:flex;gap:8px;align-items:center;color:{TEXT}">'
                           f'<i style="display:block;width:12px;height:12px;background:{c}"></i>{n}</span>'
                           f'<span class="num" style="font-size:22px;padding-left:20px">{v} <span class="small">· {round(100 * v / 53)}%</span></span></div>'
                           for n, v, c in (("Solo", 22, GOLD), ("Multiplayer", 19, RUBY), ("Solo vs AI", 12, JADE)))
                 + '</div></div>', "share of matches")
    # the match timeline: a Gantt, one lane per mode, each match a bar from its start for its length
    lanes = ["Tandava", "Scurry", "Switchback", "Redline", "Skein", "Other"]
    W, X0 = 1080, 120
    span = 23
    bars = []
    rng = random.Random(7)
    for li, lane in enumerate(lanes):
        for _ in range([6, 5, 4, 3, 3, 9][li] + (3 if lane == "Other" else 0)):
            d = rng.uniform(0, span - .3)
            kind = rng.choices(["multi", "solo_vs_ai", "solo"], weights=[19, 12, 22])[0]
            fin = rng.random() < .62
            x = X0 + (W - X0) * d / span
            w = max(6, (W - X0) * rng.uniform(.04, .16) / span)
            y = 22 + li * 30
            c = PLAY_COLOUR[kind]
            bars.append(f'<rect x="{x:.1f}" y="{y}" width="{w:.1f}" height="16" fill="{c if fin else "none"}" '
                        f'stroke="{c}" stroke-width="{0 if fin else 1.5}"></rect>')
            if fin and rng.random() < .3:
                bars.append(f'<rect x="{x + w / 2 - 2:.1f}" y="{y - 6}" width="4" height="4" fill="{TEXT}"></rect>')
    axis = "".join(f'<text x="{X0 + (W - X0) * d / span:.0f}" y="{22 + len(lanes) * 30 + 14}" fill="{SOFT}" style="font:12px Aldrich">'
                   f'{["Oct 15", "Oct 20", "Oct 25", "Oct 30", "Nov 4"][i]}</text>'
                   f'<rect x="{X0 + (W - X0) * d / span:.0f}" y="14" width="1" height="{len(lanes) * 30}" fill="{LINE}" opacity=".6"></rect>'
                   for i, d in enumerate((0, 5, 10, 15, 20)))
    names = "".join(f'<text x="0" y="{34 + i * 30}" fill="{TEXT}" style="font:13px Aldrich">{n}</text>' for i, n in enumerate(lanes))
    gantt = panel("Match timeline", f'<svg width="{W}" height="{40 + len(lanes) * 30}" role="img" aria-label="Every match on a time axis, one lane per mode">'
                  f'{axis}{names}{"".join(bars)}</svg>'
                  + legend([("Solo vs AI", JADE), ("Solo", GOLD), ("Multiplayer", RUBY)])
                  + f'<span class="small">A filled bar finished; an outline did not. A square above it is a win. Bar length is the match\'s length.</span>',
                  "Oct 15 to today · one lane per mode · hover a bar for the match")
    # crystals: balance over time, earned by source
    pts = [(0, 5400), (2, 6000), (5, 6800), (8, 7200), (11, 8300), (14, 9000), (16, 9400), (19, 9800), (21, 10200), (23, 10200)]
    cw, ch = 520, 150
    poly = " ".join(f"{cw * d / 23:.0f},{ch - ch * (v - 5000) / 5600:.0f}" for d, v in pts)
    crystals = panel("Crystals", f'<svg width="{cw}" height="{ch + 18}" role="img" aria-label="Crystal balance over time">'
                     f'<rect x="0" y="{ch}" width="{cw}" height="1" fill="{LINE}"></rect>'
                     f'<polyline points="{poly}" fill="none" stroke="{GOLD}" stroke-width="2"></polyline>'
                     + "".join(f'<rect x="{cw * d / 23 - 4:.0f}" y="{ch - ch * (v - 5000) / 5600 - 4:.0f}" width="8" height="8" fill="{GOLD}"></rect>' for d, v in pts[-1:])
                     + f'<text x="{cw - 4}" y="{ch - ch * (10200 - 5000) / 5600 - 12:.0f}" text-anchor="end" fill="{TEXT}" style="font:14px Aldrich">10,200</text>'
                     f'<text x="0" y="{ch + 16}" fill="{SOFT}" style="font:12px Aldrich">Oct 15</text>'
                     f'<text x="{cw}" y="{ch + 16}" text-anchor="end" fill="{SOFT}" style="font:12px Aldrich">today</text></svg>'
                     + ranked_bars([("game_placement", 3600), ("tournament_placement", 900), ("quests", 300)], 520, GOLD)
                     + '<span class="small" style="text-decoration:underline">All 48 transactions</span>', "balance, and earned by source")
    grid = ('<div style="display:grid;grid-template-columns:400px 420px 420px minmax(0,1fr);gap:12px">'
            + play + panel("Modes", ranked_bars(modes, 380), "matches, most first")
            + panel("Vessels", ranked_bars(vessels, 380), "matches, most first")
            + panel("When they play", heat_week(), "hour of day x weekday") + "</div>")
    bottom = '<div style="display:grid;grid-template-columns:minmax(0,1fr) 560px;gap:12px">' + gantt + crystals + "</div>"
    return screen("players", ident + tiles + grid + bottom)


def heat_week(cell=10):
    rng = random.Random(3)
    rows = []
    days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]
    for i, d in enumerate(days):
        rows.append(f'<text x="0" y="{i * (cell + 2) + cell - 2}" fill="{SOFT}" style="font:11px Aldrich">{d}</text>')
        for h in range(24):
            v = max(0.0, math.sin((h - 13) / 24 * math.pi * 2) * .6 + (.5 if i >= 5 else 0) + rng.uniform(-.3, .3))
            op = min(1, v) * .95
            rows.append(f'<rect x="{34 + h * (cell + 2)}" y="{i * (cell + 2)}" width="{cell}" height="{cell}" fill="{BLUE}" '
                        f'opacity="{max(op, .06):.2f}"></rect>')
    return (f'<svg width="{34 + 24 * (cell + 2)}" height="{7 * (cell + 2) + 16}" role="img" aria-label="Matches by hour and weekday">'
            + "".join(rows) + f'<text x="34" y="{7 * (cell + 2) + 12}" fill="{SOFT}" style="font:11px Aldrich">00</text>'
            f'<text x="{34 + 12 * (cell + 2)}" y="{7 * (cell + 2) + 12}" fill="{SOFT}" style="font:11px Aldrich">12</text>'
            f'<text x="{34 + 23 * (cell + 2)}" y="{7 * (cell + 2) + 12}" fill="{SOFT}" style="font:11px Aldrich">23</text></svg>'
            f'<span class="small">Busiest: weekends, 14:00 to 22:00 (their local time)</span>')


def lifespans_board():
    rows = sorted(PLAYERS, key=lambda p: p["first"])
    W, X0, H = 920, 110, 26
    span = TODAY
    out = []
    for i, p in enumerate(rows):
        y = 10 + i * H
        x0, x1 = X0 + (W - X0) * p["first"] / span, X0 + (W - X0) * p["last"] / span
        dom = max(("solo_vs_ai", "solo", "multi"), key=lambda k: p[k])
        churned = TODAY - p["last"] > 14
        out.append(f'<text x="0" y="{y + 12}" fill="{TEXT if not churned else SOFT}" style="font:13px Aldrich">{p["name"]}</text>'
                   f'<rect x="{x0:.1f}" y="{y}" width="{max(x1 - x0, 4):.1f}" height="14" fill="{PLAY_COLOUR[dom]}" opacity="{.35 if churned else .85}"></rect>'
                   + "".join(f'<rect x="{X0 + (W - X0) * d / span - 1:.1f}" y="{y + 15}" width="2" height="4" fill="{TEXT}" opacity=".8"></rect>' for d in p["days"]))
    axis = "".join(f'<rect x="{X0 + (W - X0) * d / span:.0f}" y="4" width="1" height="{len(rows) * H + 6}" fill="{LINE}" opacity=".5"></rect>'
                   f'<text x="{X0 + (W - X0) * d / span:.0f}" y="{len(rows) * H + 26}" fill="{SOFT}" style="font:12px Aldrich">{t}</text>'
                   for d, t in ((0, "Aug 8"), (24, "Sep 1"), (54, "Oct 1"), (85, "Nov 1")))
    gantt = panel("Every player's lifespan", f'<svg width="{W + 20}" height="{len(rows) * H + 32}" role="img" aria-label="Each player from first to last seen">'
                  f'{axis}{"".join(out)}</svg>'
                  + legend([("Mostly solo vs AI", JADE), ("Mostly solo", GOLD), ("Mostly multiplayer", RUBY)])
                  + '<span class="small">A bar runs from first to last seen, coloured by how they mostly play; ticks under it are the days they played. Faded: no play in 14 days.</span>',
                  "sorted by first seen · click a name for the profile", "grid-row:span 2")
    # cohort heatmap: weekly cohorts x weeks since first play (one hue, light to dark)
    cohorts = ["Aug 4", "Aug 11", "Aug 18", "Aug 25", "Sep 1", "Sep 8", "Sep 15", "Sep 22", "Sep 29", "Oct 6", "Oct 13", "Oct 20", "Oct 27"]
    rng = random.Random(11)
    cell = 44
    cells = []
    for i, c in enumerate(cohorts):
        n = rng.randint(1, 6)
        cells.append(f'<text x="0" y="{i * (cell - 14) + 20}" fill="{TEXT}" style="font:13px Aldrich">{c}</text>'
                     f'<text x="70" y="{i * (cell - 14) + 20}" fill="{SOFT}" style="font:13px Aldrich">{n}</text>')
        val = 100.0
        for wk in range(0, 9):
            if wk > len(cohorts) - 1 - i:
                break
            if wk:
                val = max(0, val * rng.uniform(.45, .85))
            x = 100 + wk * (cell + 2)
            cells.append(f'<rect x="{x}" y="{i * (cell - 14) + 4}" width="{cell}" height="{cell - 16}" fill="{BLUE}" opacity="{max(.08, val / 100):.2f}"></rect>'
                         f'<text x="{x + cell / 2}" y="{i * (cell - 14) + 22}" text-anchor="middle" fill="{GROUND if val > 55 else TEXT}" style="font:12px Aldrich">{round(val)}%</text>')
    head = "".join(f'<text x="{100 + w * (cell + 2) + cell / 2}" y="-6" text-anchor="middle" fill="{SOFT}" style="font:12px Aldrich">W{w}</text>' for w in range(9))
    heat = panel("Return by weekly cohort", f'<svg width="520" height="{len(cohorts) * (cell - 14) + 24}" viewBox="0 -18 520 {len(cohorts) * (cell - 14) + 24}" role="img" aria-label="Share of each week\'s new players who played again in each later week">'
                 f'{head}<text x="0" y="-6" fill="{SOFT}" style="font:12px Aldrich">COHORT</text><text x="70" y="-6" fill="{SOFT}" style="font:12px Aldrich">N</text>{"".join(cells)}</svg>'
                 + '<span class="small">Share of each week\'s new players who played again in each later week.</span>',
                 "the darker, the more came back")
    kpis = ('<div style="display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px">'
            + tile("Median lifespan", "16 d", "first to last seen") + tile("D1 return", "41%", "of new players")
            + tile("D7 return", "22%", "cohorts 7+ days old") + tile("Lapsed", "9", "of 23: no play in 14 days", DANGER) + "</div>")
    body = (header("Cohorts · release builds · last 90 days", "Cohorts and lifespans")
            + '<div style="display:grid;grid-template-columns:1000px minmax(0,1fr);grid-template-rows:auto 1fr;gap:12px;flex:1;min-height:0">'
            + gantt + f'<div style="display:flex;flex-direction:column;gap:12px">{kpis}{heat}</div></div>')
    return screen("cohorts", body)


ERAS = [("Old PostHog data", "2026-08-08", "2026-11-05", BLUE, "imported", "PostHog import: every event that arrived with its own old timestamp"),
        ("Pre-beta editor testing", "2026-11-06", "2026-12-15", JADE, "live", "Froglet Analytics, editor and test builds, until the beta starts"),
        ("Closed beta, mobile", "2027-01-11", "2027-02-26", GOLD, "planned", "starts when activation is ready (READINESS.md); no date yet"),
        ("Steam with keys", "2027-03-01", "2027-03-31", RUBY, "planned", "by March 2027, before GDC (READINESS.md)")]


def eras_board():
    import datetime as dt
    d0, d1 = dt.date(2026, 8, 1), dt.date(2027, 4, 30)
    W, X0 = 1060, 200
    span = (d1 - d0).days
    x = lambda iso: X0 + (W - X0) * (dt.date.fromisoformat(iso) - d0).days / span
    today = x("2026-11-06")
    months = []
    m = d0
    while m <= d1:
        months.append(m)
        m = dt.date(m.year + (m.month == 12), m.month % 12 + 1, 1)
    grid = "".join(f'<rect x="{x(mm.isoformat()):.0f}" y="0" width="1" height="362" fill="{LINE}" opacity=".45"></rect>'
                   f'<text x="{x(mm.isoformat()) + 6:.0f}" y="16" fill="{SOFT}" style="font:12px Aldrich">{mm.strftime("%b %Y" if mm.month == 1 or mm == d0 else "%b")}</text>'
                   for mm in months)
    lanes = []
    for i, (name, a, b, colour, kind, why) in enumerate(ERAS):
        y = 36 + i * 44
        xa, xb = x(a), x(b)
        lanes.append(f'<text x="0" y="{y + 17}" fill="{TEXT}" style="font:15px Aldrich">{name}</text>'
                     f'<text x="0" y="{y + 35}" fill="{SOFT}" style="font:11px \'Chakra Petch\';letter-spacing:.12em">{kind.upper()} · {a[5:]} TO {b[5:]}</text>')
        if kind == "planned":
            lanes.append(f'<rect x="{xa:.1f}" y="{y + 4}" width="{xb - xa:.1f}" height="22" fill="none" stroke="{colour}" stroke-width="1.5" stroke-dasharray="6 4"></rect>')
        elif kind == "live":
            lanes.append(f'<rect x="{xa:.1f}" y="{y + 4}" width="{today - xa + 4:.1f}" height="22" fill="{colour}"></rect>'
                         f'<rect x="{today + 4:.1f}" y="{y + 4}" width="{xb - today - 4:.1f}" height="22" fill="none" stroke="{colour}" stroke-width="1.5"></rect>'
                         f'<rect x="{xb - 4:.1f}" y="{y}" width="8" height="30" fill="{TEXT}"></rect>')
        else:
            lanes.append(f'<rect x="{xa:.1f}" y="{y + 4}" width="{xb - xa:.1f}" height="22" fill="{colour}"></rect>')
    # milestones
    ms = [("Server live, PostHog off", "2026-11-06"), ("Activation ready", "2027-01-11"), ("GDC", "2027-03-16")]
    marks = "".join(f'<path d="M{x(d):.1f},{214} l7,7 l-7,7 l-7,-7z" fill="{TEXT}"></path>'
                    f'<text x="{x(d) + 10:.1f}" y="{226}" fill="{TEXT}" style="font:12px Aldrich">{n}</text>' for n, d in ms)
    # events per week underneath, coloured by era
    rng = random.Random(21)
    weeks = []
    w = dt.date(2026, 8, 3)
    while w <= dt.date(2026, 11, 2):
        weeks.append(w)
        w += dt.timedelta(days=7)
    bars = []
    for i, wk in enumerate(weeks):
        v = int(2600 + 2400 * math.sin(i / 3) ** 2 + rng.randint(0, 1800)) if wk < dt.date(2026, 11, 6) else 0
        bars.append((wk, v, BLUE))
    bars.append((dt.date(2026, 11, 2), 3900, JADE))
    top = max(v for _, v, _ in bars)
    bar_svg = "".join(f'<rect x="{x(wk.isoformat()) + 1:.1f}" y="{360 - 100 * v / top:.1f}" width="{(W - X0) * 7 / span - 3:.1f}" height="{100 * v / top:.1f}" fill="{c}"></rect>'
                      for wk, v, c in bars)
    chart = (f'<svg width="{W + 10}" height="382" role="img" aria-label="The project\'s eras on a calendar, with events per week underneath">'
             f'{grid}{"".join(lanes)}{marks}'
             f'<text x="0" y="290" fill="{TEXT}" style="font:15px Aldrich">Events a week</text>'
             f'<text x="0" y="308" fill="{SOFT}" style="font:11px \'Chakra Petch\';letter-spacing:.12em">COLOURED BY ERA</text>'
             f'<rect x="{X0}" y="360" width="{W - X0}" height="1" fill="{LINE}"></rect>{bar_svg}'
             f'<rect x="{today:.1f}" y="24" width="2" height="340" fill="{CTA}"></rect>'
             f'<text x="{today + 6:.1f}" y="376" fill="{CTA}" style="font:12px Aldrich">today</text></svg>')
    plan = panel("The plan", chart + legend([("Old PostHog data", BLUE), ("Pre-beta editor testing", JADE), ("Closed beta, mobile (planned)", GOLD), ("Steam with keys (planned)", RUBY)])
                 + '<span class="small">A filled bar holds data; an outline is still ahead; dashed has no data yet and moves with the plan. The white bar is where an era ends; drag it, or set the date on the right.</span>',
                 "Aug 2026 to Apr 2027 · today 6 Nov", "grid-column:span 2")
    editor = panel("Eras", "".join(
        f'<div style="display:flex;flex-direction:column;gap:6px;padding:10px 12px;box-shadow:inset 3px 0 0 {c};background:rgba(5,0,3,.35)">'
        f'<div style="display:flex;justify-content:space-between;align-items:center"><span style="font-size:15px;color:{TEXT}">{n}</span>'
        f'<span class="tag" style="{"color:" + c + ";box-shadow:inset 0 0 0 1px " + c if k != "planned" else ""}">{k}</span></div>'
        f'<div style="display:flex;gap:8px"><span class="field sls" style="height:32px;font-size:13px;color:{TEXT}">{a}</span>'
        f'<span class="small" style="align-self:center">to</span><span class="field sls" style="height:32px;font-size:13px;color:{TEXT}">{b if k != "live" else b + " (set)"}</span></div>'
        f'<span class="small" style="font-size:12px">{why}</span></div>' for n, a, b, c, k, why in ERAS)
        + '<button type="button" class="btn ghost sls">New era</button>'
        + '<span class="small">An era is a named date range, optionally limited to builds or to imported data. Pick one in the Era filter on any page; compare any two here. Admins edit; readers see.</span>',
        "admins edit", "grid-row:span 2")
    rows = [("Players", "45", "12", ""), ("Matches", "1,402", "118", ""),
            ("Editor share", "91%", "84%", ""), ("Finished", "58%", "66%", "+8 pts"), ("Rematch within 10 min", "47%", "53%", "+6 pts"),
            ("Back next day", "39%", "41%", "+2 pts"), ("Rows per play", "1.81", "1.00", "the #1639 fix")]
    table = ('<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">Measure</th>'
             f'<th class="th" style="color:{BLUE}">Old PostHog</th><th class="th" style="color:{JADE}">Pre-beta</th><th class="th">Change</th></tr></thead><tbody>'
             + "".join(f'<tr><td class="td">{a}</td><td class="td">{b}</td><td class="td">{c}</td><td class="td" style="color:{CTA if d else SOFT}">{d or "-"}</td></tr>' for a, b, c, d in rows)
             + "</tbody></table>")
    compare = panel("The eras compared", table + '<span class="small">The new era is a day old; it fills in as testing goes on.</span>',
                    "any two eras")
    shares = panel("Where the data comes from", ranked_bars([("Old PostHog data", 66746), ("Pre-beta testing", 3912)], 520, BLUE)
                   + '<span class="small">Imported events keep their own time and say so: nothing from PostHog is counted twice or mixed into the new era.</span>',
                   "events, all builds")
    body = (header("Eras · release builds · every era", "Eras", era="All eras")
            + '<div style="display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr) 430px;grid-template-rows:auto 1fr;gap:12px;flex:1;min-height:0">'
            + plan + editor + compare + shares + "</div>")
    return screen("eras", body)


def playtypes_board():
    def spark(seed, colour, w=470, h=90):
        rng = random.Random(seed)
        vals = [max(0, 3 + 3 * math.sin(i / 5) + rng.uniform(-2, 3)) for i in range(30)]
        top = max(vals)
        pts = " ".join(f"{w * i / 29:.0f},{h - h * v / top:.0f}" for i, v in enumerate(vals))
        return (f'<svg width="{w}" height="{h + 2}" aria-hidden="true"><rect x="0" y="{h}" width="{w}" height="1" fill="{LINE}"></rect>'
                f'<polyline points="{pts}" fill="none" stroke="{colour}" stroke-width="2"></polyline></svg>')

    def meter(label, pct, colour):
        return (f'<div style="display:grid;grid-template-columns:150px 1fr 60px;gap:10px;align-items:center">'
                f'<span class="small" style="color:{TEXT}">{label}</span><span style="display:block;height:10px;background:{GROUND}">'
                f'<span style="display:block;height:10px;width:{pct}%;background:{colour}"></span></span>'
                f'<span class="small num" style="color:{TEXT};text-align:right">{pct}%</span></div>')
    stats = {"solo_vs_ai": (148, 71, 52, 31, 18, 2.1, [("Tandava", 31), ("Scurry", 24), ("Switchback", 19), ("Redline", 14)]),
             "solo": (96, 58, 41, 24, 11, 1.6, [("Skein", 22), ("Bends", 17), ("Headlong", 15), ("Regatta", 12)]),
             "multi": (61, 49, 22, 18, 9, 2.8, [("Joust", 21), ("DogFight", 16), ("WreckingBall", 11), ("PeelTheCage", 8)])}
    cols = []
    for key, name, colour in PLAY:
        m, fin, rem, d1, d7, mins, top = stats[key]
        cols.append(panel(name, f'<div style="display:flex;align-items:baseline;gap:12px"><span class="num" style="font-size:40px;color:{TEXT}">{m}</span>'
                          f'<span class="small">matches · 90 days</span></div>{spark(hash(key) % 97, colour)}'
                          + f'<span class="small">Matches per day</span>'
                          + meter("Finished", fin, colour) + meter("Rematch < 10 min", rem, colour) + meter("Back next day", d1, colour)
                          + meter("Back in 7 days", d7, colour)
                          + f'<div style="display:flex;justify-content:space-between"><span class="small">At the controls per match</span><span class="small num" style="color:{TEXT}">{mins} min</span></div>'
                          + '<span class="lab" style="margin-top:4px">Top modes</span>' + ranked_bars(top, 470, colour),
                          "", f"box-shadow:inset 0 0 0 1px #7A5068,inset 0 3px 0 {colour}"))
    funnel = [("Party invites sent", 74), ("Accepted", 51), ("Started a match", 38), ("Finished it", 27), ("Played again", 12)]
    fun = panel("Multiplayer, from invite to playing again", ranked_bars(funnel, 760, RUBY, top=74)
                + '<span class="small">Each step as a count of parties; the drop between steps is where multiplayer loses people.</span>',
                "last 90 days · release builds")
    share = panel("Share of matches", '<div style="display:flex;gap:24px;align-items:center">'
                  + donut([("Solo vs AI", 148, JADE), ("Solo", 96, GOLD), ("Multiplayer", 61, RUBY)], 190, label="305", sub="matches")
                  + legend([("Solo vs AI 49%", JADE), ("Solo 31%", GOLD), ("Multiplayer 20%", RUBY)]).replace("flex-wrap:wrap", "flex-direction:column")
                  + "</div>", "")
    body = (header("Play types · release builds · last 90 days", "Solo vs AI, Solo, Multiplayer", "All")
            + '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px">' + "".join(cols) + "</div>"
            + '<div style="display:grid;grid-template-columns:minmax(0,1fr) 560px;gap:12px">' + fun + share + "</div>"
            + f'<span class="small">The Play filter above sits on every page (Overview, Players, Cohorts, Retention, Matches): pick Multiplayer and every number reads multiplayer only. Numbers the server cannot split by play type say so in their corner.</span>')
    return screen("playtypes", body)


def query_board():
    tables = [("plays", ["match_id", "day", "build_kind", "game_mode", "intensity", "vessel_class", "play_type", "humans",
                         "completed", "rematched", "won", "flight_time_seconds"]),
              ("daily_players", []), ("returns", []), ("economy", []), ("funnel", []), ("ftue_steps", []), ("mode_signals", []),
              ("my tables", [])]
    schema = "".join(
        f'<div style="display:flex;flex-direction:column;gap:2px"><div style="display:flex;justify-content:space-between;align-items:center;'
        f'height:30px;padding:0 10px;{"background:" + SURFACE + ";box-shadow:inset 2px 0 0 " + JADE if t == "plays" else ""}">'
        f'<span style="font-size:14px;color:{TEXT if t in ("plays", "my tables") else SOFT}">{t}</span>'
        f'<span class="small" style="font-size:12px">{"12 cols" if cols else ("3" if t == "my tables" else "")}</span></div>'
        + "".join(f'<span class="small" style="padding:0 10px 0 22px;font-size:13px">{c}</span>' for c in cols) + "</div>"
        for t, cols in tables)
    mine = "".join(f'<span class="small" style="padding:0 10px 0 22px;font-size:13px;color:{TEXT}">{t}</span>'
                   for t in ("solo_ai_modes_30d", "whales_weekly", "launch_week_ftue"))
    left = panel("Tables", schema + mine + f'<button type="button" class="btn ghost sls" style="margin-top:8px">New table from a query</button>'
                 + '<span class="small">Totals only: no player ids. Player-level tables need an admin key and stay admin-only.</span>',
                 "", "width:300px;flex:none")

    def block(word, content, colour=JADE):
        return (f'<div style="display:grid;grid-template-columns:110px 1fr;align-items:center;gap:12px;min-height:36px">'
                f'<span class="lab" style="color:{colour}">{word}</span><div style="display:flex;gap:8px;flex-wrap:wrap">{content}</div></div>')
    pill = lambda t: f'<span class="chip sls" style="height:32px">{t}</span>'
    builder = (block("From", pill("plays"))
               + block("Where", pill("build_kind = release") + pill("day &#8805; 30 days ago") + pill("play_type = solo vs AI") + pill("+ condition"))
               + block("Group by", pill("game_mode") + pill("+ group"))
               + block("Measure", pill("matches = count(match_id)") + pill("finished % = avg(completed)") + pill("avg flight = avg(flight_time_seconds)") + pill("+ measure"))
               + block("Order", pill("matches, high to low") + pill("limit 12")))
    sql = ("SELECT game_mode,\n       COUNT(DISTINCT match_id)      AS matches,\n       ROUND(100.0 * AVG(completed), 1) AS finished_pct,\n"
           "       ROUND(AVG(flight_time_seconds), 1) AS avg_flight\nFROM plays\nWHERE build_kind = 'release' AND day >= date('now', '-30 days')\n"
           "  AND play_type = 'solo_vs_ai'\nGROUP BY game_mode ORDER BY matches DESC LIMIT 12;")
    res_rows = [("Tandava", 31, 74.2, 96.0), ("Scurry", 24, 70.8, 88.5), ("Switchback", 19, 63.1, 102.4), ("Redline", 14, 78.5, 71.2)]
    table = ('<table style="border-collapse:collapse;width:100%"><thead><tr>' + "".join(f'<th class="th">{h}</th>' for h in ("game_mode", "matches", "finished_pct", "avg_flight"))
             + "</tr></thead><tbody>" + "".join("<tr>" + "".join(f'<td class="td">{v}</td>' for v in r) + "</tr>" for r in res_rows) + "</tbody></table>")
    chart_kinds = segs(["Bars", "Line", "Donut", "Gantt", "Table", "Number"], "Bars")
    centre = (panel("Query", f'<div style="display:flex;gap:10px;align-items:center">{segs(["Builder", "SQL", "Ask Claude"], "Builder")}'
                    f'<span class="small">Read-only, 2 s and 200 rows at most, like the Ask box</span></div>'
                    + builder + f'<pre class="code">{sql}</pre>'
                    + '<div style="display:flex;gap:10px"><button type="button" class="btn cta sls">Run</button>'
                    + '<span class="small" style="align-self:center">Ctrl + Enter · ran in 41 ms · 4 rows</span></div>', "solo vs AI modes, last 30 days")
              + panel("Result", f'<div style="display:flex;justify-content:space-between;align-items:center">{chart_kinds}'
                      f'<span class="small">x game_mode · y matches</span></div>'
                      + '<div style="display:grid;grid-template-columns:400px minmax(0,1fr);gap:20px">' + ranked_bars([(r[0], r[1]) for r in res_rows], 400, BLUE) + table + "</div>"))
    save = panel("Save", '<div class="field sls" style="color:' + TEXT + '">solo_ai_modes_30d</div>'
                 + f'<span class="lab">Keep it as</span>{segs(["A saved query", "A table, rebuilt nightly"], "A table, rebuilt nightly")}'
                 + f'<span class="lab">Who sees it</span>{segs(["Only me", "Readers", "Admins"], "Readers")}'
                 + '<div style="display:flex;flex-direction:column;gap:8px"><button type="button" class="btn def sls">Save</button>'
                 + '<button type="button" class="btn ghost sls">Pin to a dashboard</button><button type="button" class="btn ghost sls">Alert me when it changes</button></div>'
                 + '<span class="lab" style="margin-top:6px">History</span>'
                 + "".join(f'<div style="display:flex;justify-content:space-between"><span class="small" style="color:{TEXT}">{a}</span><span class="small">{b}</span></div>'
                           for a, b in (("whales_weekly", "Garrett · Mon"), ("launch_week_ftue", "you · Nov 4"), ("multi_funnel", "Ian · Nov 2"))),
                 "", "width:330px;flex:none")
    body = (header("Admin · build your own", "Query Studio", "Solo vs AI")
            + f'<div style="display:flex;gap:12px;flex:1;min-height:0">{left}<div style="display:flex;flex-direction:column;gap:12px;flex:1;min-width:0">{centre}</div>{save}</div>')
    return screen("query", body)


def dashboards_board():
    lst = "".join(f'<div style="display:flex;flex-direction:column;gap:2px;padding:8px 10px;{"background:" + SURFACE + ";box-shadow:inset 2px 0 0 " + JADE if i == 0 else ""}">'
                  f'<span style="font-size:15px;color:{TEXT if i == 0 else SOFT}">{n}</span><span class="small" style="font-size:12px">{o}</span></div>'
                  for i, (n, o) in enumerate((("Launch week", "you · shared with readers"), ("Monetisation", "Garrett · admins"),
                                              ("FTUE health", "Caleb · readers"), ("Multiplayer", "Ian · readers"))))
    left = panel("Dashboards", lst + '<button type="button" class="btn ghost sls" style="margin-top:8px">New dashboard</button>', "", "width:260px;flex:none")
    handle = f'<svg width="14" height="14" viewBox="0 0 14 14" aria-hidden="true" style="fill:{SOFT}"><circle cx="4" cy="3" r="1.3"/><circle cx="10" cy="3" r="1.3"/><circle cx="4" cy="7" r="1.3"/><circle cx="10" cy="7" r="1.3"/><circle cx="4" cy="11" r="1.3"/><circle cx="10" cy="11" r="1.3"/></svg>'

    def t(title, inner, span="", src=""):
        return (f'<div class="panel sl" style="padding:14px 16px;display:flex;flex-direction:column;gap:10px;min-width:0;{span};'
                f'box-shadow:inset 0 0 0 1px {JADE}55,inset 0 1px 0 {JADE}88">'
                f'<div style="display:flex;justify-content:space-between;align-items:center"><span class="lab" style="color:{TEXT}">{title}</span>'
                f'<span style="display:flex;gap:10px;align-items:center"><span class="small" style="font-size:12px">{src}</span>{handle}</span></div>{inner}</div>')
    rng = random.Random(5)
    vals = [rng.randint(2, 14) for _ in range(30)]
    bw = 600 / 30
    bars = "".join(f'<rect x="{i * bw + 1:.1f}" y="{130 - 9 * v}" width="{bw - 2:.1f}" height="{9 * v}" fill="{BLUE}"></rect>' for i, v in enumerate(vals))
    grid = ('<div style="display:grid;grid-template-columns:repeat(4,minmax(0,1fr));grid-auto-rows:minmax(0,auto);gap:12px">'
            + t("New players", '<span class="num" style="font-size:40px">38</span><span class="small">this week · +12 on last week</span>', "", "daily_players")
            + t("D1 return", '<span class="num" style="font-size:40px">41%</span><span class="small">target 40% · met</span>', "", "returns")
            + t("FTUE finished", '<span class="num" style="font-size:40px">63%</span><span class="small">of new players</span>', "", "launch_week_ftue")
            + t("Rows per play", f'<span class="num" style="font-size:40px;color:{CTA}">1.00</span><span class="small">healthy</span>', "", "daily_plays")
            + t("Matches per day", f'<svg width="600" height="132" role="img" aria-label="Matches per day">{bars}</svg>', "grid-column:span 2", "plays")
            + t("Play mix", '<div style="display:flex;gap:16px;align-items:center">' + donut([("Solo vs AI", 148, JADE), ("Solo", 96, GOLD), ("Multiplayer", 61, RUBY)], 130, label="305", sub="")
                + legend([("Solo vs AI", JADE), ("Solo", GOLD), ("Multiplayer", RUBY)]).replace("flex-wrap:wrap", "flex-direction:column") + "</div>", "", "plays")
            + t("Solo vs AI modes", ranked_bars([("Tandava", 31), ("Scurry", 24), ("Switchback", 19), ("Redline", 14)], 330, BLUE), "", "solo_ai_modes_30d")
            + t("Whales this week", '<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">Player</th><th class="th">Crystals</th><th class="th">Matches</th></tr></thead><tbody>'
                + "".join(f'<tr><td class="td">{a}</td><td class="td">{b}</td><td class="td">{c}</td></tr>' for a, b, c in (("Dragon", "10,200", 53), ("Kes", "4,200", 31), ("Halcyon", "2,500", 22)))
                + "</tbody></table>", "grid-column:span 2", "whales_weekly · admins")
            + f'<div class="sl" style="grid-column:span 2;display:flex;align-items:center;justify-content:center;gap:12px;min-height:120px;'
              f'box-shadow:inset 0 0 0 1px {LINE};background:repeating-linear-gradient(135deg,transparent 0 10px,rgba(112,64,95,.18) 10px 11px)">'
              f'<span class="btn ghost sls">+ Add a tile</span><span class="small">from a saved query, or any chart on any page</span></div>'
            + "</div>")
    bar = ('<div style="display:flex;justify-content:space-between;align-items:center;gap:12px">'
           f'<div style="display:flex;gap:10px;align-items:center"><span class="tag" style="color:{JADE};box-shadow:inset 0 0 0 1px {JADE}">Editing</span>'
           '<span class="small">Drag a tile by its handle; drag its corner to resize.</span></div>'
           '<div style="display:flex;gap:10px"><button type="button" class="btn ghost sls">Share: readers</button>'
           '<button type="button" class="btn ghost sls">Email it Mondays 09:00</button><button type="button" class="btn def sls">Done</button></div></div>')
    body = (header("Admin · your boards", "Launch week")
            + bar + f'<div style="display:flex;gap:12px;flex:1;min-height:0">{left}<div style="flex:1;min-width:0">{grid}</div></div>')
    return screen("dash", body)


def notes_board():
    rows = [
        ("Players", "Filters (country, platform, active, matches, has played multiplayer, crystals), click-to-sort columns, saved segments, CSV export; each row gets a lifespan bar and a play-mix bar.",
         "Server ready: /v1/people now carries each player's play mix and wins (4a729d91d6). Segments are new: saved filters."),
        ("Profile", "Six tiles, a play-type donut, ranked modes and vessels, an hour x weekday heatmap, a Gantt match timeline (lane per mode, filled = finished, square = win), the crystal balance line and earnings by source; the transaction table moves behind a link.",
         "Server ready: /v1/person already returns every match with its time, mode, vessel, play type, finish and win, and every crystal change with its balance."),
        ("Cohorts", "Every player's lifespan as a Gantt (first to last seen, coloured by how they mostly play, ticks on play days, faded when lapsed) and a weekly cohort return heatmap.",
         "New view: lifespans from the people list; the heatmap from a weekly returns table (new aggregate)."),
        ("Eras", "Named date ranges an admin sets (Old PostHog data, Pre-beta editor testing, then the betas from the launch plan) on a plan chart over the weeks' events; an Era filter on every page; any two eras compared.",
         "Server: eras in the config, applied like the build filter; imported PostHog events are told apart exactly (they arrive with their own old timestamp). New."),
        ("Play types", "Solo vs AI, Solo and Multiplayer side by side (matches per day, finished, rematch, D1, D7, minutes, top modes), the multiplayer invite-to-replay funnel, the share donut.",
         "Server ready: the play filter (play=solo_vs_ai|solo|multi) on Overview, Retention and Matches (4a729d91d6). The funnel needs party events counted per party (new aggregate)."),
        ("Query Studio", "Admins build a query (From / Where / Group by / Measure / Order), or write SQL, or ask Claude; see it as bars, line, donut, Gantt, table or a number; save it as a query or as a table rebuilt nightly; share it; pin it; get an alert.",
         "New: saved queries and tables in the store, rebuilt by the nightly job; the same read-only guard the Ask box has (authorizer, 2 s, 200 rows). Player-level tables admin-only."),
        ("Dashboards", "Anyone's own boards of tiles from saved queries or any chart in the console, drag and resize, shared with readers or admins, emailed on a schedule.",
         "New: dashboards in the store; email needs a mail sender on the VM (a decision)."),
    ]
    body = "".join(f'<tr><td class="td" style="vertical-align:top;color:{JADE};white-space:nowrap">{a}</td>'
                   f'<td class="td" style="vertical-align:top;white-space:normal;line-height:1.45">{b}</td>'
                   f'<td class="td" style="vertical-align:top;white-space:normal;line-height:1.45;color:{SOFT}">{c}</td></tr>' for a, b, c in rows)
    return (f'<div style="width:1920px;height:900px;box-sizing:border-box;position:relative;overflow:hidden;background:{GROUND};'
            f'color:{TEXT};font-family:\'Aldrich\',sans-serif;padding:44px 56px;display:flex;flex-direction:column;gap:18px">'
            f'<span class="lab">Rev 65 · notes</span><h1 class="h1">Froglet Analytics, the full tool</h1>'
            f'<p class="body" style="color:{SOFT};max-width:1500px">Asked (Shombith, 2026-11-06): filters and sorting on Players; '
            f'"different analytical tools like pie charts, gantt charts etc for business development" instead of lists; the data in named eras '
            f'("Old Posthog data", "Pre Beta Editor testing results", on "a plan chart"); a solo vs AI / '
            f'multiplayer filter; "the best analytical tool ever"; "give control to admins more like creating new tables queries". '
            f'Seven screens of one tool, not seven options: pick the ones to build, or change any. Drawn in the console\'s default theme, '
            f'Nocturne, with the sea lines; every number is sample data.</p>'
            f'<table style="border-collapse:collapse;width:100%"><thead><tr><th class="th">Screen</th><th class="th">What it adds</th>'
            f'<th class="th">What the server has, and what is new</th></tr></thead><tbody>{body}</tbody></table>'
            f'<p class="small" style="max-width:1600px">Charts: categories wear the colour set\'s four rims in a fixed order '
            f'(Jade solo vs AI, Gold solo, Ruby multiplayer; Blue for plain data), each with a legend and labels, so colour is never '
            f'the only cue; the game\'s rims are brighter than the usual chart band, so labels stay on. A donut is used only for one '
            f'whole in three parts; anything with more parts is ranked bars. Lifespans and the match timeline are Gantt charts on a '
            f'time axis. A sequential heatmap is one hue, light to dark.</p></div>')


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    out = {
        "R65Players": ("Rev 65 Players - filters, sorting, play mix and lifespan", players_board()),
        "R65Profile": ("Rev 65 Profile - a player in charts", profile_board()),
        "R65Lifespans": ("Rev 65 Lifespans - cohorts and lifespans", lifespans_board()),
        "R65Eras": ("Rev 65 Eras - the plan chart and the eras compared", eras_board()),
        "R65PlayTypes": ("Rev 65 PlayTypes - solo vs AI, solo, multiplayer", playtypes_board()),
        "R65Query": ("Rev 65 Query - Query Studio for admins", query_board()),
        "R65Dashboards": ("Rev 65 Dashboards - your own boards", dashboards_board()),
    }
    for key, (title, body) in out.items():
        (ROOT / f"{key}.dc.html").write_text(board(title, body), encoding="utf-8")
    (ROOT / "R65Notes.dc.html").write_text(board("Rev 65 - notes", notes_board(), 1920, 900), encoding="utf-8")
    print("wrote", ", ".join(sorted(out)) + ", R65Notes")


if __name__ == "__main__":
    main()
