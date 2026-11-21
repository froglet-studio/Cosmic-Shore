#!/usr/bin/env python3
"""Rev 43 boards: the Voyage window redrawn - map and edge view on the left (opaque), the voyage card sliding in on the
right with three levels (the voyage, its Ark crew, its story), and Saga | Voyages | Episodes on LB / RB.
Six options A-F, five screens each, plus Compare and Notes. Writes the board files into <root>/project/.

    python3 Docs/MenuRedesignB/rounds/r43_voyage_window.py <root> 43     (root: the scratch folder `frogma.py plan` wrote)

The boards it wrote are on the Frogma's Menu Redesign C tab (section 113 of Docs/MENU_REDESIGN_B.md); kept
so a follow-up round on the picked option starts from the same pieces. MAP and EDGE are the Voyage Board
(Docs/Voyages/VoyageBoard/voyage-board.html) rendered at 2x with Playwright and uploaded to the canvas's asset store."""
import html
import json
import sys
from pathlib import Path

ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2])
W, H = 1920, 1080

MAP = "/_blob/b96bc9cef23de987e4ed42294befc28a"     # Voyage Board render, 2x: Tumult -> Lantern Reach, cells, stars
EDGE = "/_blob/25c96499cc959b94655e2a5a3362228e"    # Voyage Board course profile, 2x
TANDAVA = "/_blob/ea5244253b797934bf2988bdc1260ec7"
QUEST_ART = ["/_blob/e991a3576a32930173b452e9806c08d3", "/_blob/870fa2d022d70e4eadd5963ce81b5328",
             "/_blob/f73f095638ef9ad060a2a8bb097ea2af", "/_blob/360243baa0fb047a2c955d06f7f663a0",
             "/_blob/7d5ddbe91b12c281ac5d6a4b4ad38239"]
NAV = [("CODEX", "/_blob/ec690d6b90d6fcdbc430b9bbaacf28a9"), ("HANGAR", "/_blob/34f5a50f1275fef42808fe9df3bf6af5"),
       ("HOME", "/_blob/a5bd4d8d1de7b494567fc3de75525e1e"), ("ARK", "/_blob/e5b050c5a676193c650e94d873ad40e2"),
       ("PORT", "/_blob/d0e850bc927559f1a499868d26f2a3de")]
HULL = {"Squirrel": "/_blob/694ae84ec6db347c4f034c8e03b56991", "Sparrow": "/_blob/a2a4139d8e2822885778740d4f2fb92e",
        "Dolphin": "/_blob/e4ebf079ba441cd7141a2c8bc092d8ea", "Manta": "/_blob/ab51cbb598b5a1e912416244829d134c",
        "Serpent": "/_blob/215e6ad15de9f075e7a45e7640a12f09", "Rhino": "/_blob/48e972fa9d369a737bacf39f37baf59f"}
PILOT_ART = {"Shrapnel": "/_blob/70fab52e055a8baf2eca47881dc75789", "Riptide": "/_blob/10e39715b356ad76fe64dcc5234b824f",
             "Trace": "/_blob/b2c216ffe8fce1e71dc01d6af16ef5bf"}

# Style foundation v0.3.3
LIGHT, INACT, CTA, NEUT_L, NEUT, NEUT_D, VDARK, BLACK = "#E6E9FF", "#5C5F70", "#99FF80", "#747BAD", "#434C89", "#222645", "#00041F", "#00010A"
SUB = "#9AA0CC"
DOM = {"Jade": ("#00D4FF", "#80EAFF"), "Ruby": ("#A600FF", "#D280FF"), "Gold": ("#FFAE00", "#FFD780")}
SEA = [("Spray", "#8e9cb4"), ("Froth", "#2e8a9e"), ("Water", "#2869b8"), ("Thick", "#6a44b0"), ("Reef", "#e0644a")]

# The voyage: the Voyage Board's voyage 2 and the slice's job.hunt.sighting, word for word where they exist.
V = {
    "title": "Open the vaults at Lantern Reach",
    "summary": "Vaults are lit at Lantern Reach. Crack one with a payload, carry its artifact back to the Ark and stow it.",
    "kind": "Hunt", "place": "Lantern Reach", "placeKind": "Vault field",
    "crew": "Oriel Vance", "callsign": "CITADEL", "faction": "Hunter",
    "crewLine": "Reads a vault's shell like a face. Has opened more than she has kept.",
    "course": "580 LY THROUGH WATER - LIGHT TAKES 580 YEARS",
}
STATS = [("Course", "580 ly"), ("Sail", "29 min"), ("Intensity", "I2"), ("Pays", "8"), ("Finds", "1"), ("Odds", "78%")]
SEA_MIX = [("Spray", 9, "#8e9cb4"), ("Froth", 1, "#2e8a9e"), ("Water", 19, "#2869b8")]
WAYPOINTS = [("Tumult Rim harbours", "Moored"), ("Elyel Light", ""), ("Brinon", ""), ("Rosum", ""),
             ("Belira Reach", ""), ("Duneth Gate", ""), ("Lantern Reach", "Vault field")]
OTHERS = [("1", "Carry sealed news to Tollmoor", "VOLT", "27 min", "I1"), ("2", V["title"], "CITADEL", "29 min", "I2"),
          ("3", "Raid the thieves' cache at Gallowglass", "GALLEON", "47 min", "I2"), ("4", "Hunt the creature in The Maw", "TALON", "49 min", "I3")]
PRESETS = [("Skim Wing", "Jade", "Yours - every seat filled", ["Sparrow", "Dolphin", "Squirrel"], True),
           ("Breakers", "Ruby", "Kestrel's squad - joined", ["Rhino", "Manta", "Serpent"], False)]
SEATS = [("Leader", "You", "Sparrow", "Shrapnel", "Charge", "Extra Bay"),
         ("Rogue One", "Halcyon", "Dolphin", "Riptide", "Space", "Seed Pod"),
         ("Rogue Two", "AI wingmate", "Squirrel", "Trace", "Time", "No artifact")]
QUESTS = [("01", "Scurry", "In progress", "Intensity 1 of 4", True), ("02", "Hex Race", "Locked", "Unlocks after Scurry", False),
          ("03", "Joust", "Locked", "Unlocks after Hex Race", False), ("04", "Maelstrom", "Locked", "Unlocks after Joust", False),
          ("05", "Vessel Hangar", "Locked", "Unlocks after Maelstrom", False)]
STORY = [("Who brought it", "Oriel Vance, callsign CITADEL, Hunter", "Reads a vault's shell like a face. Has opened more than she has kept."),
         ("The place", "Lantern Reach, a vault field", "Vault shells drift here, lit from inside. Something sealed them, and something wants them back."),
         ("The news", "Heard at Celadon Harbour", "A trader swears the shells at Lantern Reach are lit again."),
         ("The water", "Coreward lane, 70 ly below the midplane", "Light from here takes 580 years to reach Lantern Reach. You take 29 minutes. The thickest water is 2.7 g/cm3, as heavy as iron.")]

e = html.escape

HEAD = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{title}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{{margin:0;background:#00010A}}
.lab{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.18em;text-transform:uppercase;color:#8C93C7}}
.cap{{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}}
.num{{font-variant-numeric:tabular-nums}}
.abs{{position:absolute}}
.sl{{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}}
.sls{{clip-path:polygon(8px 0,100% 0,100% calc(100% - 8px),calc(100% - 8px) 100%,0 100%,0 8px)}}
.ell{{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}}
.kc{{display:inline-flex;align-items:center;justify-content:center;min-width:26px;height:22px;padding:0 6px;box-sizing:border-box;border:1px solid #434C89;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:12px;color:#C9CDEA}}
.btn{{box-sizing:border-box;display:flex;align-items:center;justify-content:center;gap:10px;height:48px;padding:0 14px;white-space:nowrap;border:0;cursor:pointer;font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:15px;letter-spacing:.14em;text-transform:uppercase}}
.cta{{background:#99FF80;color:#00010A}}
.sec{{background:#2B3266;color:#E6E9FF}}
.gho{{background:transparent;color:#C9CDEA;box-shadow:inset 0 0 0 1px #434C89}}
.btn .kc{{border-color:currentColor;color:inherit;opacity:.8}}
.btn:focus-visible,.tab:focus-visible,.pick:focus-visible{{outline:2px solid #3DB2FF;outline-offset:2px}}
.tab,.pick{{background:none;border:0;padding:0;cursor:pointer;color:inherit;font:inherit;text-align:left}}
.panel{{background:#080C28;box-shadow:inset 0 0 0 1px #2B3266}}
.rule{{border-top:1px solid #222645}}
</style>
</helmet>
"""
TAIL = """</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
class Component extends DCLogic {{
renderVals() {{
return {{}};
}}
}}
</script>
</body>
</html>
"""

LOCK = '<svg width="12" height="14" viewBox="0 0 14 16" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M3 7V5a4 4 0 018 0v2"></path><path d="M1.5 7h11v8h-11z"></path></svg>'
CHEV = '<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M2 3.5l3 3 3-3"></path></svg>'
ARROW = '<svg width="14" height="10" viewBox="0 0 14 10" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M1 5h11M8 1l4 4-4 4"></path></svg>'


def kc(t):
    return f'<span class="kc">{e(t)}</span>'


def board(title, body, w=W, h=H, bg=VDARK):
    root = (f'<div style="width: {w}px; height: {h}px; position: relative; overflow: hidden; background: {bg}; color: {LIGHT}; '
            f"font-family: 'Aldrich', 'Chakra Petch', sans-serif\">\n{body}\n</div>\n")
    return HEAD.format(title=e(title)) + root + TAIL.format(w=w, h=h)


def nav():
    items = []
    for name, icon in NAV:
        on = name == "HOME"
        mark = '<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:#E6E9FF"></span>' if on else ""
        items.append(f'<span style="position:relative;display:flex;align-items:center;gap:10px;font-family:\'Chakra Petch\';font-weight:600;'
                     f'font-size:17px;letter-spacing:.12em;color:{LIGHT if on else NEUT_L}"><img src="{icon}" alt="" '
                     f'style="width:26px;height:26px;object-fit:contain;opacity:{1 if on else .7}">{name}{mark}</span>')
    return (f'<nav aria-label="Menu screens" class="abs" style="left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.85);'
            f'border-bottom:1px solid {NEUT_D};display:flex;align-items:center;justify-content:center;gap:48px;z-index:5">'
            f'{kc("LT")}{"".join(items)}{kc("RT")}</nav>'
            f'<div class="abs num" style="right:40px;top:30px;z-index:6;display:flex;align-items:center;gap:8px;font-size:20px;color:#FFAE00">'
            f'<span style="width:14px;height:18px;background:#FFAE00;clip-path:polygon(50% 0,100% 35%,50% 100%,0 35%)"></span>1,240</div>')


# ------------------------------------------------------------------------------------------------ window tab rows

TABS = ["Saga", "Voyages", "Episodes"]


def back():
    return (f'<button class="pick" type="button" style="display:flex;align-items:center;gap:10px">{kc("B")}'
            f'<span class="cap" style="font-size:14px;color:{NEUT_L}">Back</span></button>')


def tabrow(style, active):
    """The window's own tabs under the nav: Saga | Voyages | Episodes, LB / RB to step. One look per option."""
    def lab(t, on, size=17):
        return f'<span class="cap" style="font-size:{size}px;color:{LIGHT if on else NEUT_L}">{t}</span>'
    if style == "A":   # left-aligned underline row, the Ark's tab row
        tabs = "".join(f'<button class="tab" type="button" style="position:relative">{lab(t, t == active)}'
                       + (f'<span class="abs" style="left:0;right:0;bottom:-15px;height:2px;background:{LIGHT}"></span>' if t == active else "")
                       + '</button>' for t in TABS)
        return (f'<div class="abs" style="left:40px;right:40px;top:96px;height:56px;display:flex;align-items:center;gap:34px;'
                f'border-bottom:1px solid {NEUT_D};z-index:4">{back()}<span style="width:1px;height:22px;background:{NEUT_D}"></span>'
                f'<span style="font-size:24px">Voyage</span><span style="width:12px"></span>{kc("LB")}{tabs}{kc("RB")}</div>')
    if style == "B":   # centred, the active tab in a thin box
        tabs = "".join(f'<button class="tab sls" type="button" style="padding:8px 22px;'
                       + (f'box-shadow:inset 0 0 0 1px {LIGHT};background:rgba(230,233,255,.06)' if t == active else "")
                       + f'">{lab(t, t == active)}</button>' for t in TABS)
        return (f'<div class="abs" style="left:40px;top:108px;z-index:4">{back()}</div>'
                f'<div class="abs" style="left:0;right:0;top:100px;height:48px;display:flex;align-items:center;justify-content:center;gap:14px;z-index:4">'
                f'{kc("LB")}{tabs}{kc("RB")}</div>')
    if style == "C":   # a segmented bar, right of the title
        tabs = "".join(f'<button class="tab" type="button" style="flex:1;height:100%;display:flex;align-items:center;justify-content:center;'
                       + (f'background:#2B3266' if t == active else "") + f'">{lab(t, t == active, 15)}</button>' for t in TABS)
        return (f'<div class="abs" style="left:40px;right:40px;top:100px;height:48px;display:flex;align-items:center;gap:20px;z-index:4">'
                f'{back()}<span style="font-size:26px">Voyage</span><span style="flex:1"></span>{kc("LB")}'
                f'<div class="sls" style="width:520px;height:40px;display:flex;background:#0E1646;box-shadow:inset 0 0 0 1px {NEUT}">{tabs}</div>{kc("RB")}</div>')
    if style == "D":   # tabs with counts
        cnt = {"Saga": "1 of 2", "Voyages": "4 found", "Episodes": "0 of 5"}
        tabs = "".join(f'<button class="tab" type="button" style="display:flex;flex-direction:column;gap:2px;padding:0 6px;'
                       + (f'box-shadow:inset 0 -2px 0 {LIGHT}' if t == active else "") + f';height:52px;justify-content:center">'
                       f'{lab(t, t == active)}<span class="lab num" style="font-size:11px;color:{SUB if t == active else INACT}">{cnt[t]}</span></button>'
                       for t in TABS)
        return (f'<div class="abs" style="left:40px;right:40px;top:96px;height:60px;display:flex;align-items:center;gap:30px;'
                f'border-bottom:1px solid {NEUT_D};z-index:4">{back()}<span style="flex:1"></span>{kc("LB")}{tabs}{kc("RB")}<span style="flex:1"></span>'
                f'<span style="width:110px"></span></div>')
    if style == "E":   # step dots under centred tabs
        tabs = "".join(f'<button class="tab" type="button">{lab(t, t == active, 18 if t == active else 16)}</button>' for t in TABS)
        dots = "".join(f'<span style="width:{22 if t == active else 6}px;height:3px;background:{LIGHT if t == active else NEUT}"></span>' for t in TABS)
        return (f'<div class="abs" style="left:40px;top:108px;z-index:4">{back()}</div>'
                f'<div class="abs" style="left:0;right:0;top:98px;display:flex;flex-direction:column;align-items:center;gap:8px;z-index:4">'
                f'<div style="display:flex;align-items:center;gap:30px">{kc("LB")}{tabs}{kc("RB")}</div><div style="display:flex;gap:6px">{dots}</div></div>')
    # F: headline tabs, the active one as the screen title
    tabs = "".join(f'<button class="tab" type="button" style="font-size:{34 if t == active else 20}px;color:{LIGHT if t == active else NEUT_L};'
                   f'font-family:Aldrich;letter-spacing:.04em">{t}</button>' for t in TABS)
    return (f'<div class="abs" style="left:40px;right:40px;top:96px;height:60px;display:flex;align-items:flex-end;gap:28px;z-index:4">'
            f'{back()}<span style="width:8px"></span>{kc("LB")}{tabs}{kc("RB")}</div>')


# ------------------------------------------------------------------------------------------------ map and edge

def mapview(x, y, w, hm, he, pos="50% 40%", gap=12, ladder=True, list_strip=False, glow=False):
    """The HyperSea map (opaque, the Voyage Board's own render) with the edge view under it at the same width.
    At level 1 (glow) it is the interactive HyperSea map explorer instead."""
    if glow:
        return map_explorer(x, y, w, hm, he, pos, gap)
    tiers = ["Galaxy", "Arm", "Sector", "Region", "Neighbourhood"]
    lad = ""
    if ladder:
        lad = ('<div class="abs" style="right:14px;top:14px;display:flex;flex-direction:column;gap:6px;align-items:flex-end">'
               + "".join(f'<span class="cap" style="font-size:11px;padding:3px 8px;background:rgba(0,1,10,.78);color:{LIGHT if t == "Neighbourhood" else NEUT_L};'
                         + (f'box-shadow:inset 0 0 0 1px {LIGHT}' if t == "Neighbourhood" else "") + f'">{t}</span>' for t in tiers)
               + '</div>')
    key = ('<div class="abs" style="left:14px;bottom:14px;padding:8px 10px;background:rgba(0,1,10,.82);display:flex;flex-direction:column;gap:5px">'
           '<span class="lab" style="font-size:10px">Sea</span><div style="display:flex;gap:2px">'
           + "".join(f'<span style="width:40px;height:6px;background:{c}"></span>' for _, c in SEA)
           + '</div><div style="display:flex;gap:2px">'
           + "".join(f'<span class="cap" style="width:40px;font-size:9px;color:{SUB}">{n}</span>' for n, _ in SEA) + '</div></div>')
    strip = ""
    if list_strip:
        strip = ('<div class="abs" style="left:14px;top:14px;display:flex;gap:6px">'
                 + "".join(f'<button class="pick sls" type="button" style="display:flex;align-items:center;gap:8px;padding:7px 12px;background:rgba(0,1,10,.82);'
                           + (f'box-shadow:inset 0 0 0 1px {LIGHT}' if n == "2" else "") + f'"><span class="num" style="font-size:15px;color:{LIGHT if n == "2" else NEUT_L}">{n}</span>'
                           f'<span class="cap ell" style="max-width:150px;font-size:11px;color:{LIGHT if n == "2" else NEUT_L}">{e(t)}</span></button>'
                           for n, t, *_ in OTHERS) + '</div>')
    cap = "" if list_strip else (f'<div class="abs" style="left:14px;top:14px;padding:8px 12px;background:rgba(0,1,10,.82);display:flex;flex-direction:column;gap:3px">'
                                 f'<span class="lab" style="font-size:10px">HyperSea map</span><span style="font-size:15px">Tumult to Lantern Reach</span></div>')
    return (f'<figure style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{hm}px;margin:0;overflow:hidden;background:{BLACK};box-shadow:inset 0 0 0 1px {NEUT_D}">'
            f'<img src="{MAP}" alt="HyperSea map at the Region tier: the course from Tumult to Lantern Reach across the 20 ly cells" '
            f'style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{pos}">{cap}{strip}{lad}{key}{map_glow(w, hm, pos) if glow else ""}</figure>'
            f'<figure style="position:absolute;left:{x}px;top:{y + hm + gap}px;width:{w}px;height:{he}px;margin:0;overflow:hidden;background:{BLACK};box-shadow:inset 0 0 0 1px {NEUT_D}">'
            f'<img src="{EDGE}" alt="Edge-on view at true scale: the course from Tumult, 30 ly above the midplane, down the core lane to Lantern Reach, 34 ly below" '
            f'style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{pos.split()[0]} 60%">'
            f'<span class="abs cap" style="left:12px;top:8px;font-size:11px;padding:3px 8px;background:rgba(0,1,10,.82);color:{SUB}">'
            f'Edge on, true scale - disk 1,000 ly - Lantern Reach 34 ly below</span></figure>')


# ------------------------------------------------------------------------------------------------ the route, lit (edit, 2026-11-02)
# Shombith: "show the waypoints in a better animated glowy way, and how do we navigate between voyage cards". The level-1
# Voyage boards of Rev 43 and Rev 44 were edited in place for it: the route sweeps from the Ark to the destination,
# each waypoint lights as the sweep passes, the destination pulses, the same waypoints light on the map in step, and
# a pager of the four voyages heads the card (D-pad, click or swipe).

ROUTE = "#C8FF5A"          # the map's own course colour, so card and map read as one route
ARK_AMBER = "#FFC740"      # the Voyage Board's Ark marker
CYCLE = 4.2                # one sweep, seconds
SWEEP = 2.9                # the part of it the sweep spends crossing
GLOW_CSS = ("@keyframes wpLit{0%{background:#0B1038;box-shadow:inset 0 0 0 1.5px rgba(200,255,90,.55),0 0 0 0 rgba(200,255,90,0)}"
            "5%{background:#C8FF5A;box-shadow:inset 0 0 0 1.5px #C8FF5A,0 0 16px 5px rgba(200,255,90,.8)}"
            "45%{background:#C8FF5A;box-shadow:inset 0 0 0 1.5px #C8FF5A,0 0 7px 2px rgba(200,255,90,.35)}"
            "100%{background:rgba(200,255,90,.35);box-shadow:inset 0 0 0 1.5px #C8FF5A,0 0 0 0 rgba(200,255,90,0)}}\n"
            "@keyframes wpRing{0%{transform:scale(1);opacity:.85}100%{transform:scale(2.8);opacity:0}}\n"
            "@keyframes wpBeam{0%{left:0;opacity:0}4%{opacity:1}69%{opacity:1}72%{left:100%;opacity:0}100%{left:100%;opacity:0}}\n"
            "@keyframes wpBeamV{0%{top:0;opacity:0}4%{opacity:1}69%{opacity:1}72%{top:100%;opacity:0}100%{top:100%;opacity:0}}\n"
            ".wpn{display:block;transform:rotate(45deg);animation:wpLit 4.2s ease-out infinite}\n"
            ".wpr{position:absolute;border:1.5px solid #C8FF5A;border-radius:50%;animation:wpRing 2.1s ease-out infinite}\n"
            ".wpb{position:absolute;width:110px;height:10px;margin-left:-55px;border-radius:5px;"
            "background:radial-gradient(ellipse at center,rgba(200,255,90,.95),rgba(200,255,90,.35) 45%,rgba(200,255,90,0) 72%);animation:wpBeam 4.2s linear infinite}\n"
            ".wpbv{position:absolute;width:10px;height:90px;margin-top:-45px;border-radius:5px;"
            "background:radial-gradient(ellipse at center,rgba(200,255,90,.95),rgba(200,255,90,.35) 45%,rgba(200,255,90,0) 72%);animation:wpBeamV 4.2s linear infinite}\n")


def with_glow(page):
    """A board that draws the lit route carries its keyframes in its helmet."""
    return page.replace("</style>", GLOW_CSS + "</style>", 1)


def _node(i, n, size, end_lit=False):
    delay = SWEEP * i / (n - 1)
    if end_lit:
        return (f'<span class="wpn" style="width:{size}px;height:{size}px;background:{ROUTE};animation:none;'
                f'box-shadow:0 0 12px 3px rgba(200,255,90,.6)"></span>')
    return f'<span class="wpn" style="width:{size}px;height:{size}px;animation-delay:{delay:.2f}s"></span>'


def route_glow(compact=False, vertical=False):
    """The waypoints as a lit route: Tumult (lit, the Ark is there) to Lantern Reach (lit, pulsing), the sweep between."""
    pts = WAYPOINTS if not compact else [WAYPOINTS[0], WAYPOINTS[2], WAYPOINTS[3], WAYPOINTS[5], WAYPOINTS[-1]]
    n = len(pts)
    head = (f'<div style="display:flex;justify-content:space-between;align-items:baseline"><span class="lab" style="font-size:11px">Waypoints</span>'
            f'<span class="cap num" style="font-size:11px;color:{SUB}">{len(WAYPOINTS)} on the course - 29 min</span></div>')
    if vertical:
        rows = []
        for i, (name, tag) in enumerate(pts):
            end = i in (0, n - 1)
            node = _node(i, n, 12 if end else 9, end_lit=(i == 0))
            ring = (f'<span class="wpr" style="left:-4px;top:-4px;width:20px;height:20px"></span>') if i == n - 1 else ""
            rows.append(f'<div style="display:flex;align-items:center;gap:14px;height:28px"><span style="position:relative;width:12px;display:grid;place-items:center">{node}{ring}</span>'
                        f'<span style="font-size:{16 if end else 14}px;color:{LIGHT if end else "#C9CDEA"}">{name}</span>'
                        + (f'<span class="cap" style="font-size:11px;color:{NEUT_L}">{tag}</span>' if tag else "") + '</div>')
        return (f'<div style="display:flex;flex-direction:column;gap:6px">{head}<div style="position:relative;display:flex;flex-direction:column;gap:4px">'
                f'<span class="abs" style="left:5px;top:14px;bottom:14px;width:2px;background:linear-gradient(180deg,{ROUTE},rgba(200,255,90,.25))"></span>'
                f'<span class="abs" style="left:1px;top:14px;bottom:14px;width:10px;overflow:visible"><span class="wpbv" style="left:0"></span></span>'
                f'{"".join(rows)}</div></div>')
    items = []
    for i, (name, tag) in enumerate(pts):
        end = i in (0, n - 1)
        name = "Tumult" if i == 0 else name
        node = _node(i, n, 13 if end else 9, end_lit=(i == 0))
        ring = (f'<span class="wpr" style="left:50%;top:6px;width:22px;height:22px;margin:-11px 0 0 -11px"></span>') if i == n - 1 else ""
        items.append(f'<div style="position:relative;display:flex;flex-direction:column;align-items:center;gap:9px;flex:1;min-width:0">{ring}'
                     f'<span style="height:13px;display:grid;place-items:center">{node}</span>'
                     f'<span class="ell" style="max-width:100%;font-size:12px;color:{LIGHT if end else SUB}">{name}</span>'
                     + (f'<span class="cap" style="font-size:10px;color:{ROUTE if i == n - 1 else ARK_AMBER}">{"Destination" if i == n - 1 else "Your Ark"}</span>' if end else "")
                     + '</div>')
    lo, hi = 50 / n, 100 - 50 / n
    return (f'<div style="display:flex;flex-direction:column;gap:10px">{head}<div style="position:relative;display:flex">'
            f'<span class="abs" style="left:{lo:.1f}%;right:{100 - hi:.1f}%;top:5px;height:2px;background:linear-gradient(90deg,{ROUTE},rgba(200,255,90,.22))"></span>'
            f'<span class="abs" style="left:{lo:.1f}%;right:{100 - hi:.1f}%;top:1px;height:10px"><span class="wpb" style="top:0"></span></span>'
            f'{"".join(items)}</div></div>')


# The course on the map render (map1.jpg, 2196 x 1358 px): the Ark at Tumult, the waypoints, Lantern Reach.
MAP_PX = (2196, 1358)
MAP_ROUTE = [(1510, 1180), (1511, 1125), (1312, 1038), (1085, 909), (933, 650), (711, 264), (660, 157)]


def map_glow(w, h, pos):
    """The waypoints lit on the map itself, in step with the card: the same delays, the Ark ringed in amber at Tumult."""
    fx, fy = (float(v.strip("%")) / 100 for v in pos.split())
    s = max(w / MAP_PX[0], h / MAP_PX[1])
    ox, oy = (w - MAP_PX[0] * s) * fx, (h - MAP_PX[1] * s) * fy
    n = len(MAP_ROUTE)
    out = []
    for i, (px, py) in enumerate(MAP_ROUTE):
        x, y = ox + px * s, oy + py * s
        if not (0 <= x <= w and 0 <= y <= h):
            continue
        if i == 0:
            out.append(f'<span class="wpr" style="left:{x - 14:.0f}px;top:{y - 14:.0f}px;width:28px;height:28px;border-color:{ARK_AMBER}"></span>')
            continue
        size = 14 if i == n - 1 else 10
        out.append(f'<span class="abs" style="left:{x - size / 2:.0f}px;top:{y - size / 2:.0f}px">{_node(i, n, size)}</span>')
        if i == n - 1:
            out.append(f'<span class="wpr" style="left:{x - 16:.0f}px;top:{y - 16:.0f}px;width:32px;height:32px"></span>')
    return "".join(out)


# ------------------------------------------------------------------------------------------------ the HyperSea map, zoomable (edit, 2026-11-02)
# Shombith: "Garrett built the new zoomed in and zoomed out feature of the HyperSea map. It is almost like a google map.
# Take that pull in and propose the new mockup along with that. Just re-edit the old mockups." / "This is called the
# HyperSea map; that should be called it in the UI as well." Garrett's build (a1922d78, #1717): seven tiers from the
# whole galaxy to one star system (VoyageLines.MapTier*), the SOL and TUMULT dives, the readout, a card for a tapped
# star / cluster / landmark / cell. Each tier below is the Voyage Board itself rendered at that tier.

MAP_TIERS = [  # (tier, what - the authored MapTier*What line, scale bar as the board draws it, render, position, minimap box %)
    ("Galaxy", "arms, bulge, the halo's globular clusters", "10,000 ly", "/_blob/7559d7b030c6e949f7cde2d25ce5d9e7", "50% 50%", None),
    ("Arm", "giant nurseries, the brightest supergiants", "1,000 ly", "/_blob/fced8d3ce00c8f4c54ce4f49f52448c0", "50% 50%", 22),
    ("Sector", "star clusters and the brightest stars", "200 ly", "/_blob/6138a0a6b01214f86b8a8496fe0140ba", "50% 45%", 6),
    ("Region", "clusters, clouds and the stars you see by eye", "50 ly", MAP, None, 2.6),
    ("Neighbourhood", "a slab of nearby stars around one height", "20 ly", "/_blob/aa78b73bc805bdf9f8bc992bd5f12d74", "50% 50%", 1.4),
    ("Cell", "one 20 ly cell and the stars inside it", "2 ly", "/_blob/a67a28db2832830827c20435ff87b2c6", "50% 50%", 1.4),
    ("System", "single star systems a few ly apart", "1 ly", "/_blob/2756a1ad4aee8ba111763fec387e64df", "50% 50%", 1.4),
]
TUMULT_IN_GALAXY = (50.6, 49.8)   # where Tumult sits in the Galaxy render, % of its width / height

MAP_LOGIC = """const TIERS = %s;
const TUMULT = %s;
class Component extends DCLogic {
constructor(p){ super(p); this.state = { t: 3 }; }
go(t) { this.setState({ t: Math.max(0, Math.min(TIERS.length - 1, t)) }); }
renderVals() {
const t = this.state.t, T = TIERS[t], box = T[5];
const layers = TIERS.map((x, i) => ({ src: x[3], alt: 'HyperSea map, ' + x[0] + ' tier', pos: x[4], op: i === t ? 1 : 0, sc: i < t ? 1.8 : (i > t ? 0.55 : 1) }));
const ladder = TIERS.map((x, i) => ({ name: x[0], on: i === t, off: i !== t, pick: () => this.go(i) })).reverse();
const mmBox = box ? 'left:' + (TUMULT[0] - box / 2) + '%%;top:' + (TUMULT[1] - box * 0.8) + '%%;width:' + box + '%%;height:' + (box * 1.6) + '%%' : 'display:none';
return { mx: {
  layers, ladder, tier: T[0].toUpperCase(), what: T[1], scale: T[2],
  course: t === 3, inspect: t >= 4, sector: t === 2, mm: t > 0, mmBox,
  zoomIn: () => this.go(t + 1), zoomOut: () => this.go(t - 1),
  diveArk: () => this.go(6), diveCourse: () => this.go(3), diveAll: () => this.go(2)
} };
}
}"""


def map_logic(pos):
    import json as _json
    tiers = [[a, b, c, d, (e or pos), f] for a, b, c, d, e, f in MAP_TIERS]
    return MAP_LOGIC % (_json.dumps(tiers), _json.dumps(list(TUMULT_IN_GALAXY)))


EMPTY_LOGIC = "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}"


def with_map(page, pos):
    """A level-1 board: the explorer's logic in place of the empty one."""
    assert EMPTY_LOGIC in page
    return page.replace(EMPTY_LOGIC, map_logic(pos), 1)


def map_explorer(x, y, w, hm, he, pos, gap=12):
    """The HyperSea map as Garrett built it, zoomable: seven tiers from the galaxy to one star system."""
    plate = "background:rgba(0,1,10,.84)"
    btn = (f'display:grid;place-items:center;width:44px;height:44px;{plate};box-shadow:inset 0 0 0 1px {NEUT};color:{LIGHT};'
           f'font-family:\'Chakra Petch\';font-size:24px')
    ladder = ('<sc-for list="{{mx.ladder}}" as="l" hint-placeholder-count="7">'
              f'<button class="pick" type="button" onClick="{{{{l.pick}}}}" style="display:flex;align-items:center;justify-content:flex-end;gap:8px;height:24px">'
              f'<sc-if value="{{{{l.on}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="font-size:11px;padding:3px 8px;{plate};color:{LIGHT};box-shadow:inset 0 0 0 1px {LIGHT}">{{{{l.name}}}}</span>'
              f'<span style="width:14px;height:2px;background:{LIGHT}"></span></sc-if>'
              f'<sc-if value="{{{{l.off}}}}" hint-placeholder-val="{{{{ true }}}}"><span class="cap" style="font-size:10px;color:{NEUT_L};text-shadow:0 0 4px #00010A">{{{{l.name}}}}</span>'
              f'<span style="width:8px;height:2px;background:{NEUT}"></span></sc-if></button></sc-for>')
    zoom = (f'<div class="abs" style="right:14px;top:14px;display:flex;flex-direction:column;align-items:flex-end;gap:6px">'
            f'<button class="pick sls" type="button" aria-label="Zoom in" onClick="{{{{mx.zoomIn}}}}" style="{btn}">+</button>'
            f'<div style="display:flex;flex-direction:column;gap:2px;padding:4px 0">{ladder}</div>'
            f'<button class="pick sls" type="button" aria-label="Zoom out" onClick="{{{{mx.zoomOut}}}}" style="{btn}">-</button></div>')
    readout = (f'<div class="abs" style="left:14px;top:14px;padding:9px 12px;{plate};display:flex;flex-direction:column;gap:4px;max-width:420px">'
               f'<span class="lab" style="font-size:10px">HyperSea map</span>'
               f'<span style="font-size:17px"><span class="cap" style="font-size:15px">{{{{mx.tier}}}}</span>: {{{{mx.what}}}}</span>'
               f'<span style="display:flex;align-items:center;gap:8px"><span style="width:90px;height:4px;box-shadow:inset 0 -1px 0 {LIGHT},inset 1px 0 0 {LIGHT},inset -1px 0 0 {LIGHT}"></span>'
               f'<span class="cap num" style="font-size:11px;color:{SUB}">{{{{mx.scale}}}}</span></span></div>')
    dives = (f'<div class="abs" style="left:14px;top:110px;display:flex;gap:6px">'
             + "".join(f'<button class="pick sls" type="button" onClick="{{{{mx.{h}}}}}" style="padding:6px 10px;{plate};box-shadow:inset 0 0 0 1px {NEUT}">'
                       f'<span class="cap" style="font-size:11px;color:{c}">{t}</span></button>'
                       for h, t, c in (("diveArk", "Your Ark", ARK_AMBER), ("diveCourse", "This course", ROUTE), ("diveAll", "All voyages", LIGHT)))
             + '</div>')
    mini = ('<sc-if value="{{mx.mm}}" hint-placeholder-val="{{ true }}">'
            f'<div class="abs" style="right:14px;bottom:14px;width:190px;height:118px;overflow:hidden;{plate};box-shadow:inset 0 0 0 1px {NEUT}">'
            f'<img src="{MAP_TIERS[0][3]}" alt="Where you are looking, in the galaxy" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:.85">'
            f'<span class="abs" style="{{{{mx.mmBox}}}};box-shadow:0 0 0 1.5px {LIGHT}"></span></div></sc-if>')
    hint = (f'<div class="abs" style="left:50%;bottom:14px;transform:translateX(-50%);display:flex;align-items:center;gap:8px;padding:6px 10px;{plate}">'
            f'{kc("R3")}<span class="cap" style="font-size:11px;color:#C9CDEA">Explore</span><span style="width:6px"></span>'
            f'{kc("RS")}<span class="cap" style="font-size:11px;color:{SUB}">Pan</span>{kc("LT")}{kc("RT")}<span class="cap" style="font-size:11px;color:{SUB}">Zoom</span>'
            f'{kc("A")}<span class="cap" style="font-size:11px;color:{SUB}">Inspect</span></div>')
    layers = ('<sc-for list="{{mx.layers}}" as="m" hint-placeholder-count="7">'
              '<img src="{{m.src}}" alt="{{m.alt}}" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{m.pos}};'
              'opacity:{{m.op}};transform:scale({{m.sc}});transition:opacity .32s cubic-bezier(.22,1,.36,1),transform .32s cubic-bezier(.22,1,.36,1)"></sc-for>')
    course = f'<sc-if value="{{{{mx.course}}}}" hint-placeholder-val="{{{{ true }}}}">{map_glow(w, hm, pos)}</sc-if>'
    card = ('<sc-if value="{{mx.inspect}}" hint-placeholder-val="{{ false }}">'
            f'<div class="abs sls" style="left:{w // 2 + 30}px;top:{hm // 2 - 70}px;width:330px;padding:14px 16px;background:rgba(6,10,38,.94);box-shadow:inset 0 0 0 1px {NEUT};'
            f'display:flex;flex-direction:column;gap:6px"><span class="lab" style="font-size:10px">Harbour</span><span style="font-size:20px">Tumult</span>'
            f'<span style="font-size:13px;line-height:1.45;color:#C9CDEA">Home. Celadon, Carmine and Aurum harbours ride the Rim of a lens-shaped world that spins once an hour. '
            f'<span style="color:{NEUT_L}">[A]</span></span><span class="cap" style="font-size:10px;color:{ARK_AMBER}">30 ly above the midplane - Your Ark is here</span></div></sc-if>')
    sector = ('<sc-if value="{{mx.sector}}" hint-placeholder-val="{{ false }}">'
              f'<div class="abs sls" style="left:14px;top:156px;padding:8px 12px;{plate};box-shadow:inset 0 0 0 1px {NEUT}">'
              f'<span class="cap" style="font-size:11px;color:#C9CDEA">All four voyages from Tumult - tap a pin to open its card</span></div></sc-if>')
    key = ('<div class="abs" style="left:14px;bottom:14px;padding:8px 10px;background:rgba(0,1,10,.82);display:flex;flex-direction:column;gap:5px">'
           '<span class="lab" style="font-size:10px">Sea</span><div style="display:flex;gap:2px">'
           + "".join(f'<span style="width:32px;height:6px;background:{c}"></span>' for _, c in SEA)
           + '</div><div style="display:flex;gap:2px">'
           + "".join(f'<span class="cap" style="width:32px;font-size:8px;color:{SUB}">{n}</span>' for n, _ in SEA) + '</div></div>')
    return (f'<figure style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{hm}px;margin:0;overflow:hidden;background:{BLACK};box-shadow:inset 0 0 0 1px {NEUT_D}">'
            f'{layers}{course}{readout}{dives}{sector}{card}{zoom}{key}{hint}{mini}</figure>'
            f'<figure style="position:absolute;left:{x}px;top:{y + hm + gap}px;width:{w}px;height:{he}px;margin:0;overflow:hidden;background:{BLACK};box-shadow:inset 0 0 0 1px {NEUT_D}">'
            f'<img src="{EDGE}" alt="Edge-on view at true scale: the course from Tumult, 30 ly above the midplane, down the core lane to Lantern Reach, 34 ly below" '
            f'style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{pos.split()[0]} 60%">'
            f'<span class="abs cap" style="left:12px;top:8px;font-size:11px;padding:3px 8px;background:rgba(0,1,10,.82);color:{SUB}">'
            f'Edge on, true scale - disk 1,000 ly - Lantern Reach 34 ly below</span></figure>')


DPAD_L = '<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="M6.5 1.5L3 5l3.5 3.5"></path></svg>'
DPAD_R = '<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="M3.5 1.5L7 5 3.5 8.5"></path></svg>'
PAGER = [("1", "Tollmoor", "27 min - I1", "VOLT"), ("2", "Lantern Reach", "29 min - I2", "CITADEL"),
         ("3", "Gallowglass", "47 min - I2", "GALLEON"), ("4", "The Maw", "49 min - I3", "TALON")]


def voyage_pager(sel="2"):
    """How you move between the voyages your crew found: a quiet tab strip of the four, D-pad left / right at its ends
    (or a click, or a swipe on the card); the open one is underlined in the route colour and the map follows it.
    Edited 2026-11-02 after Shombith's "I don't like these parts": no boxes, no repeated meta, nothing cut off."""
    key = lambda svg, lab: (f'<button class="pick" type="button" aria-label="{lab}" style="display:grid;place-items:center">'
                            f'<span class="kc" style="height:26px;min-width:26px">{svg}</span></button>')
    tabs = "".join(
        f'<button class="pick" type="button" aria-pressed="{"true" if n == sel else "false"}" style="position:relative;display:flex;align-items:baseline;gap:6px;'
        f'white-space:nowrap;padding:6px 0">'
        f'<span class="num" style="font-size:12px;color:{LIGHT if n == sel else INACT}">{n}</span>'
        f'<span style="font-size:15px;color:{LIGHT if n == sel else NEUT_L}">{place}</span>'
        + (f'<span class="abs" style="left:0;right:0;bottom:-7px;height:2px;background:{ROUTE}"></span>' if n == sel else "")
        + '</button>'
        for n, place, meta, cs in PAGER)
    return (f'<div style="display:flex;align-items:center;gap:12px;padding-bottom:8px;border-bottom:1px solid {NEUT_D}">'
            f'{key(DPAD_L, "Previous voyage")}<div style="flex:1;display:flex;justify-content:space-between;gap:12px">{tabs}</div>'
            f'{key(DPAD_R, "Next voyage")}</div>')


# ------------------------------------------------------------------------------------------------ voyage card pieces

def head_block(size=34, kicker=True):
    k = (f'<div style="display:flex;align-items:center;gap:10px"><span class="lab">Voyage 2 of 4</span><span class="lab" style="color:{INACT}">/</span>'
         f'<span class="lab">{V["kind"]} - {V["placeKind"]}</span></div>') if kicker else ""
    return (f'<div style="display:flex;flex-direction:column;gap:10px">{k}<div style="font-size:{size}px;line-height:1.1">{V["title"]}</div>'
            f'<div style="font-size:17px;line-height:1.45;color:#C9CDEA">{V["summary"]}</div></div>')


def crew_chip():
    """Who handed you the voyage, as one line (it was a monogram badge)."""
    return (f'<div style="display:flex;align-items:baseline;gap:8px;flex-wrap:wrap"><span class="lab" style="font-size:11px">Found by</span>'
            f'<span style="font-size:16px">{V["crew"]}</span><span class="cap" style="font-size:12px;color:{NEUT_L}">{V["callsign"]} - {V["faction"]}</span></div>')


def stats_row(cols=6):
    """The voyage's numbers without a grid of boxes: the three that decide it large, the rest as one line."""
    big = "".join(f'<div style="display:flex;flex-direction:column;gap:2px"><span class="num" style="font-size:26px;line-height:1">{v}</span>'
                  f'<span class="lab" style="font-size:10px">{k}</span></div>' for k, v in STATS[:3])
    small = " - ".join(f"{k} {v}" for k, v in STATS[3:])
    return (f'<div style="display:flex;align-items:flex-end;gap:{30 if cols > 3 else 22}px;flex-wrap:wrap">{big}'
            f'<span class="cap num" style="font-size:12px;color:{SUB};padding-bottom:2px">{small}</span></div>')


def sea_bar():
    total = sum(n for _, n, _ in SEA_MIX)
    segs = "".join(f'<span style="flex:{n};height:8px;background:{c}"></span>' for _, n, c in SEA_MIX)
    labs = " - ".join(f"{n} {k.lower()}" for k, n, _ in SEA_MIX)
    return (f'<div style="display:flex;flex-direction:column;gap:6px"><div style="display:flex;justify-content:space-between">'
            f'<span class="lab" style="font-size:11px">Course, cell by cell</span><span class="cap num" style="font-size:11px;color:{SUB}">{labs} - {total} cells</span></div>'
            f'<div style="display:flex;gap:2px">{segs}</div><span class="cap" style="font-size:11px;color:{NEUT_L}">{V["course"]}</span></div>')


def waypoints(vertical=False, compact=False):
    if vertical:
        rows = []
        for i, (n, t) in enumerate(WAYPOINTS):
            end = i in (0, len(WAYPOINTS) - 1)
            dot = (f'<span style="width:{12 if end else 8}px;height:{12 if end else 8}px;transform:rotate(45deg);'
                   f'background:{LIGHT if end else "transparent"};box-shadow:inset 0 0 0 1.5px {LIGHT if end else NEUT_L}"></span>')
            line = f'<span class="abs" style="left:5px;top:16px;width:2px;height:22px;background:{NEUT}"></span>' if i < len(WAYPOINTS) - 1 else ""
            rows.append(f'<div style="position:relative;display:flex;align-items:center;gap:14px;height:30px"><span style="width:12px;display:grid;place-items:center">{dot}</span>{line}'
                        f'<span style="font-size:{16 if end else 14}px;color:{LIGHT if end else "#C9CDEA"}">{n}</span>'
                        + (f'<span class="cap" style="font-size:11px;color:{NEUT_L}">{t}</span>' if t else "") + '</div>')
        return f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab" style="font-size:11px">Waypoints</span>{"".join(rows)}</div>'
    pts = WAYPOINTS if not compact else [WAYPOINTS[0], WAYPOINTS[2], WAYPOINTS[4], WAYPOINTS[-1]]
    items = []
    for i, (n, t) in enumerate(pts):
        end = i in (0, len(pts) - 1)
        items.append(f'<div style="display:flex;flex-direction:column;align-items:center;gap:6px;flex:1;min-width:0">'
                     f'<span style="width:{11 if end else 7}px;height:{11 if end else 7}px;transform:rotate(45deg);background:{LIGHT if end else BLACK};'
                     f'box-shadow:inset 0 0 0 1.5px {LIGHT if end else NEUT_L}"></span>'
                     f'<span class="ell" style="max-width:100%;font-size:12px;color:{LIGHT if end else SUB}">{n}</span></div>')
    return (f'<div style="display:flex;flex-direction:column;gap:8px"><span class="lab" style="font-size:11px">Waypoints</span>'
            f'<div style="position:relative;display:flex"><span class="abs" style="left:7%;right:7%;top:5px;height:1px;background:{NEUT}"></span>{"".join(items)}</div></div>')


def preset_rows(dense=False, show_config=True):
    """Your Ark: the Arks you configured, each with its three hulls large enough to read, the picked one marked by one
    thin line in its domain colour; Configure under them."""
    rows = []
    for name, dom, line, hulls, on in PRESETS:
        c, cl = DOM[dom]
        thumbs = "".join(f'<img src="{HULL[h]}" alt="{h}" style="width:{80 if dense else 92}px;height:{30 if dense else 34}px;object-fit:contain">' for h in hulls)
        rows.append(f'<button class="pick sls" type="button" aria-pressed="{"true" if on else "false"}" style="position:relative;display:flex;align-items:center;gap:14px;'
                    f'padding:{8 if dense else 10}px 14px {8 if dense else 10}px 18px;background:{"rgba(14,22,70,.9)" if on else "rgba(8,12,40,.6)"};box-shadow:inset 0 0 0 1px {c if on else NEUT_D}">'
                    + (f'<span class="abs" style="left:0;top:10px;bottom:10px;width:3px;background:{c}"></span>' if on else "")
                    + f'<span style="display:flex;flex-direction:column;gap:3px;min-width:140px"><span style="font-size:17px">{name}</span>'
                    f'<span style="display:flex;align-items:center;gap:6px"><span style="width:7px;height:7px;transform:rotate(45deg);background:{c}"></span>'
                    f'<span class="cap" style="font-size:11px;color:{cl}">{dom}</span><span style="font-size:12px;color:{SUB}">{line}</span></span></span>'
                    f'<span style="flex:1"></span><span style="display:flex;gap:6px">{thumbs}</span></button>')
    cfg = (f'<button class="btn gho sls" type="button" style="height:40px;width:100%">{kc("X")}<span>Configure</span></button>') if show_config else ""
    return (f'<div style="display:flex;flex-direction:column;gap:8px"><div style="display:flex;justify-content:space-between;align-items:baseline">'
            f'<span class="lab" style="font-size:11px;color:{LIGHT}">Your Ark</span><span class="lab" style="font-size:11px">Choose one, or configure</span></div>'
            f'{"".join(rows)}{cfg}</div>')


def actions(level=1, wide=False):
    if level == 1:
        return (f'<div style="display:flex;gap:10px">'
                f'<button class="btn cta sl" type="button" style="flex:1.4">{kc("A")}<span>Set sail</span></button>'
                f'<button class="btn sec sl" type="button" style="flex:1">{kc("X")}<span>Ark crew</span></button>'
                f'<button class="btn gho sl" type="button" style="flex:1">{kc("Y")}<span>Story</span></button></div>')
    if level == 2:
        return (f'<div style="display:flex;gap:10px">'
                f'<button class="btn cta sl" type="button" style="flex:1.4">{kc("A")}<span>Set sail</span></button>'
                f'<button class="btn sec sl" type="button" style="flex:1">{kc("X")}<span>Save as Ark</span></button>'
                f'<button class="btn gho sl" type="button" style="flex:1">{kc("B")}<span>Voyage</span></button></div>')
    return (f'<div style="display:flex;gap:10px">'
            f'<button class="btn cta sl" type="button" style="flex:1.4">{kc("A")}<span>Set sail</span></button>'
            f'<button class="btn gho sl" type="button" style="flex:1">{kc("B")}<span>Voyage</span></button></div>')


def setting(label, value, art=None, dense=False, stacked=False):
    img = f'<img src="{art}" alt="" style="width:46px;height:20px;object-fit:contain">' if art else ""
    if stacked:
        return (f'<button class="pick" type="button" style="display:flex;align-items:center;gap:8px;height:52px;padding:0 12px;background:#0B1038;'
                f'box-shadow:inset 0 0 0 1px {NEUT_D}"><span style="display:flex;flex-direction:column;gap:3px;flex:1;min-width:0">'
                f'<span class="lab" style="font-size:10px">{label}</span><span class="ell" style="font-size:16px;color:{INACT if value == "No artifact" else LIGHT}">{value}</span></span>'
                f'<span style="color:{NEUT_L}">{CHEV}</span></button>')
    return (f'<button class="pick" type="button" style="display:flex;align-items:center;justify-content:space-between;gap:10px;height:{36 if dense else 42}px;'
            f'padding:0 12px;background:#0B1038;box-shadow:inset 0 0 0 1px {NEUT_D}"><span class="lab" style="font-size:10px;width:62px">{label}</span>'
            f'<span style="display:flex;align-items:center;gap:8px;flex:1;min-width:0">{img}<span class="ell" style="font-size:{14 if dense else 15}px;'
            f'color:{INACT if value == "No artifact" else LIGHT}">{value}</span></span><span style="color:{NEUT_L}">{CHEV}</span></button>')


def seat_card(i, w, dense=False, pilot_fig=True):
    role, who, vessel, pilot, element, art = SEATS[i]
    edit = i == 0
    fig = (f'<img src="{PILOT_ART[pilot]}" alt="{pilot}" style="position:absolute;right:6px;top:6px;width:{70 if dense else 86}px;height:{104 if dense else 128}px;'
           f'object-fit:contain;object-position:center bottom">') if pilot_fig else ""
    return (f'<div class="sl" style="position:relative;width:{w}px;box-sizing:border-box;padding:14px;background:#080C28;'
            f'box-shadow:inset 0 0 0 1px {DOM["Jade"][0] if edit else "#2B3266"};display:flex;flex-direction:column;gap:{6 if dense else 8}px">{fig}'
            f'<div style="display:flex;flex-direction:column;gap:2px;min-height:{40 if dense else 50}px"><span class="lab" style="font-size:11px;color:{LIGHT if edit else NEUT_L}">'
            f'0{i + 1} {role}</span><span style="font-size:{18 if dense else 20}px">{who}</span></div>'
            f'<img src="{HULL[vessel]}" alt="{vessel}" style="width:{int(w * .62)}px;height:{44 if dense else 60}px;object-fit:contain;object-position:left center">'
            f'{setting("Vessel", vessel, None, dense, w < 400)}{setting("Pilot", pilot, None, dense, w < 400)}{setting("Element", element, None, dense, w < 400)}{setting("Artifact", art, None, dense, w < 400)}</div>')


def crew_head():
    c, cl = DOM["Jade"]
    return (f'<div style="display:flex;justify-content:space-between;align-items:flex-end"><div style="display:flex;flex-direction:column;gap:6px">'
            f'<span class="lab">Level 2 - Ark crew</span><span style="font-size:28px">Skim Wing</span><span style="font-size:14px;color:{SUB}">'
            f'Three seats, four settings each. Loaded from your Ark; changes here are for this voyage until you save them.</span></div>'
            f'<span style="display:flex;align-items:center;gap:8px;padding:6px 12px;box-shadow:inset 0 0 0 1px {c}"><span style="width:9px;height:9px;transform:rotate(45deg);background:{c}"></span>'
            f'<span class="cap" style="font-size:13px;color:{cl}">Jade</span></span></div>')


def preset_tabs():
    out = []
    for name, dom, _, _, on in PRESETS:
        c = DOM[dom][0]
        out.append(f'<button class="pick" type="button" style="display:flex;align-items:center;gap:8px;padding:6px 12px;'
                   f'box-shadow:inset 0 0 0 1px {c if on else NEUT_D};background:{"#0E1646" if on else "transparent"}">'
                   f'<span style="width:8px;height:8px;transform:rotate(45deg);background:{c}"></span><span class="cap" style="font-size:12px;color:{LIGHT if on else NEUT_L}">{name}</span></button>')
    out.append(f'<button class="pick" type="button" style="padding:6px 12px;box-shadow:inset 0 0 0 1px {NEUT_D}"><span class="cap" style="font-size:12px;color:{NEUT_L}">+ New Ark</span></button>')
    return f'<div style="display:flex;gap:8px;align-items:center"><span class="lab" style="font-size:11px;margin-right:4px">Preset</span>{"".join(out)}</div>'


def story_blocks(cols=1, size=16):
    items = "".join(f'<div style="display:flex;flex-direction:column;gap:6px;padding:14px 0" class="rule"><span class="lab" style="font-size:11px">{k}</span>'
                    f'<span style="font-size:{size + 2}px">{a}</span><span style="font-size:{size}px;line-height:1.5;color:#C9CDEA">{b}</span></div>' for k, a, b in STORY)
    return f'<div style="display:grid;grid-template-columns:repeat({cols}, minmax(0, 1fr));column-gap:36px">{items}</div>'


def story_head():
    return (f'<div style="display:flex;flex-direction:column;gap:6px"><div style="display:flex;gap:10px;align-items:center"><span class="lab">Level 3 - Story</span>'
            f'<span class="cap" style="font-size:11px;padding:2px 8px;box-shadow:inset 0 0 0 1px {NEUT};color:{NEUT_L}">Optional</span></div>'
            f'<span style="font-size:28px">{V["title"]}</span></div>')


def level_crumbs(level, style="line"):
    names = ["Voyage", "Ark crew", "Story"]
    parts = []
    for i, n in enumerate(names, 1):
        on = i == level
        parts.append(f'<span class="cap" style="font-size:12px;color:{LIGHT if on else NEUT_L}">{i} {n}</span>')
    return f'<div style="display:flex;gap:16px;align-items:center">{"<span style=\"color:#434C89\">/</span>".join(parts)}</div>'


# ------------------------------------------------------------------------------------------------ the six options

def screen_A(level):
    """A - Drawer: the card slides in from the right; deeper levels slide over it and leave its edge as a spine."""
    body = [nav(), tabrow("A", "Voyages"), mapview(40, 172, 1080, 640, 216, "38% 45%", list_strip=level > 1, glow=level == 1)]
    x0, w0 = 1140, 740
    spines = []
    if level >= 2:
        spines.append((x0, "1 Voyage"))
    if level >= 3:
        spines.append((x0 + 56, "2 Ark crew"))
    for sx, t in spines:
        body.append(f'<button class="pick" type="button" style="position:absolute;left:{sx}px;top:172px;width:52px;height:868px;background:#060A24;'
                    f'box-shadow:inset 0 0 0 1px #2B3266"><span class="cap" style="position:absolute;left:50%;top:24px;transform:translateX(-50%) rotate(180deg);'
                    f'writing-mode:vertical-rl;font-size:13px;color:{NEUT_L}">{t}</span></button>')
    x = x0 + 56 * len(spines)
    w = w0 - 56 * len(spines)
    if level == 1:
        inner = (f'{voyage_pager()}{head_block(30)}{crew_chip()}{stats_row(6)}{sea_bar()}{route_glow()}'
                 f'{preset_rows(dense=True)}<div style="flex:1"></div>{actions(1)}')
    elif level == 2:
        inner = (f'{level_crumbs(2)}{crew_head()}{preset_tabs()}<div style="display:flex;gap:10px">'
                 + "".join(seat_card(i, (w - 56 - 20) // 3, dense=True) for i in range(3)) + f'</div><div style="flex:1"></div>{actions(2)}')
    else:
        inner = f'{level_crumbs(3)}{story_head()}{story_blocks(1, 15)}<div style="flex:1"></div>{actions(3)}'
    body.append(f'<section aria-label="Voyage card" class="sl" style="position:absolute;left:{x}px;top:172px;width:{w}px;height:868px;box-sizing:border-box;'
                f'padding:26px 28px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266,-24px 0 40px rgba(0,1,10,.6);display:flex;flex-direction:column;gap:18px">{inner}</section>')
    return "\n".join(body)


def screen_B(level):
    """B - Tray: the arena preview's tray. Level 1 is a narrow card; deeper levels widen it over the map, which stays."""
    body = [nav(), tabrow("B", "Voyages")]
    if level == 1:
        body.append(mapview(40, 168, 1220, 660, 212, "45% 45%", glow=True))
        inner = (f'{voyage_pager()}{head_block(28)}{stats_row(3)}{route_glow(compact=True)}{preset_rows(dense=True)}<div style="flex:1"></div>{actions(1)}')
        x, w = 1280, 600
    else:
        body.append(mapview(40, 168, 540, 660, 212, "30% 45%"))
        x, w = 600, 1280
        if level == 2:
            inner = (f'{level_crumbs(2)}{crew_head()}{preset_tabs()}<div style="display:flex;gap:16px">'
                     + "".join(seat_card(i, 392) for i in range(3)) + f'</div><div style="flex:1"></div>{actions(2)}')
        else:
            inner = f'{level_crumbs(3)}{story_head()}{story_blocks(2, 16)}<div style="flex:1"></div>{actions(3)}'
    body.append(f'<section aria-label="Voyage card" class="sl" style="position:absolute;left:{x}px;top:168px;width:{w}px;height:872px;box-sizing:border-box;'
                f'padding:26px 28px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:16px">{inner}</section>')
    return "\n".join(body)


def screen_C(level):
    """C - Level tabs: one card; its three levels are tabs at its top (D-pad), the window's tabs stay on LB / RB."""
    body = [nav(), tabrow("C", "Voyages"), mapview(40, 168, 1100, 660, 212, "40% 45%", glow=level == 1)]
    names = ["Voyage", "Ark crew", "Story"]
    tabs = "".join(f'<button class="tab" type="button" style="flex:1;height:52px;display:flex;align-items:center;justify-content:center;gap:8px;'
                   + (f'box-shadow:inset 0 -2px 0 {LIGHT};background:#0E1646' if i == level else f'box-shadow:inset 0 -1px 0 {NEUT_D}')
                   + f'"><span class="num" style="font-size:15px;color:{LIGHT if i == level else INACT}">0{i}</span><span class="cap" style="font-size:14px;color:{LIGHT if i == level else NEUT_L}">{n}</span></button>'
                   for i, n in enumerate(names, 1))
    list_ = ('<div style="display:flex;flex-direction:column;gap:4px">' + "".join(
        f'<button class="pick" type="button" style="display:flex;align-items:center;gap:12px;height:34px;padding:0 10px;'
        + (f'box-shadow:inset 2px 0 0 {LIGHT};background:#0E1646' if n == "2" else "") + f'"><span class="num" style="width:14px;color:{LIGHT if n == "2" else INACT}">{n}</span>'
        f'<span class="ell" style="flex:1;font-size:14px;color:{LIGHT if n == "2" else SUB}">{e(t)}</span><span class="cap num" style="font-size:11px;color:{NEUT_L}">{m} - {i}</span></button>'
        for n, t, c, m, i in OTHERS) + '</div>')
    if level == 1:
        inner = f'{voyage_pager()}{head_block(28, False)}{crew_chip()}{stats_row(6)}{route_glow(compact=True)}{preset_rows(dense=True)}<div style="flex:1"></div>{actions(1)}'
    elif level == 2:
        inner = (f'{crew_head()}{preset_tabs()}<div style="display:flex;flex-direction:column;gap:8px">'
                 + "".join(seat_row(i) for i in range(3)) + f'</div><div style="flex:1"></div>{actions(2)}')
    else:
        inner = f'{story_head()}{story_blocks(1, 15)}<div style="flex:1"></div>{actions(3)}'
    body.append(f'<section aria-label="Voyage card" class="sl" style="position:absolute;left:1160px;top:168px;width:720px;height:872px;box-sizing:border-box;'
                f'background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column">'
                f'<div style="display:flex;align-items:center;gap:6px;padding:0 10px">{kc("D-pad")}{tabs}</div>'
                f'<div style="flex:1;padding:20px 26px 24px;display:flex;flex-direction:column;gap:14px">{inner}</div></section>')
    return "\n".join(body)


def seat_row(i):
    role, who, vessel, pilot, element, art = SEATS[i]
    edit = i == 0
    return (f'<div class="sls" style="display:grid;grid-template-columns:136px repeat(4, minmax(0, 1fr));gap:6px;align-items:center;padding:10px;'
            f'background:#080C28;box-shadow:inset 0 0 0 1px {DOM["Jade"][0] if edit else "#2B3266"}">'
            f'<div style="display:flex;align-items:center;gap:8px"><img src="{PILOT_ART[pilot]}" alt="{pilot}" style="width:40px;height:58px;object-fit:contain">'
            f'<span style="display:flex;flex-direction:column;gap:2px"><span class="lab" style="font-size:10px">{role}</span><span style="font-size:15px">{who}</span></span></div>'
            f'{setting("Vessel", vessel, None, True, True)}{setting("Pilot", pilot, None, True, True)}{setting("Element", element, None, True, True)}{setting("Artifact", art, None, True, True)}</div>')


def screen_D(level):
    """D - Rising sheet: level 1 is a card at the right; deeper levels rise from the bottom over the edge view."""
    body = [nav(), tabrow("D", "Voyages"), mapview(40, 172, 1160, 640, 216, "40% 45%", glow=level == 1)]
    if level == 1:
        inner = f'{voyage_pager()}{head_block(26)}{stats_row(3)}{route_glow(vertical=True)}{preset_rows(dense=True)}<div style="flex:1"></div>{actions(1)}'
        body.append(f'<section aria-label="Voyage card" class="sl" style="position:absolute;left:1220px;top:172px;width:660px;height:868px;box-sizing:border-box;'
                    f'padding:20px 26px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:11px">{inner}</section>')
        return "\n".join(body)
    # A summary of level 1 stays at the top right; the sheet rises over the edge view and the map's lower third.
    body.append(f'<section aria-label="Voyage" class="sl" style="position:absolute;left:1220px;top:172px;width:660px;height:290px;box-sizing:border-box;'
                f'padding:22px 26px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:12px">'
                f'{head_block(24)}</section>')
    if level == 2:
        inner = (f'<div style="display:flex;justify-content:space-between;align-items:center">{level_crumbs(2)}{preset_tabs()}</div>{crew_head()}'
                 f'<div style="display:flex;gap:16px">' + "".join(seat_card(i, 590, dense=True) for i in range(3)) + f'</div>'
                 f'<div style="display:flex;justify-content:flex-end"><div style="width:720px">{actions(2)}</div></div>')
        top = 480
    else:
        inner = (f'<div style="display:flex;justify-content:space-between;align-items:center">{level_crumbs(3)}</div>{story_head()}{story_blocks(4, 14)}'
                 f'<div style="display:flex;justify-content:flex-end"><div style="width:520px">{actions(3)}</div></div>')
        top = 560
    body.append(f'<section aria-label="Level {level}" class="sl" style="position:absolute;left:40px;top:{top}px;width:1840px;height:{1040 - top}px;box-sizing:border-box;'
                f'padding:22px 28px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266,0 -30px 50px rgba(0,1,10,.7);display:flex;flex-direction:column;gap:12px">'
                f'<span class="abs" style="left:50%;top:8px;width:60px;height:3px;margin-left:-30px;background:{NEUT}"></span>{inner}</section>')
    return "\n".join(body)


def screen_E(level):
    """E - Dossier: one card with the three levels as numbered steps down a spine; the open step fills the card."""
    body = [nav(), tabrow("E", "Voyages"), mapview(40, 172, 1100, 640, 216, "40% 45%", list_strip=level > 1, glow=level == 1)]
    names = ["Voyage", "Ark crew", "Story"]
    summary = {1: "Lantern Reach - 580 ly - 29 min - I2", 2: "Skim Wing - Sparrow, Dolphin, Squirrel", 3: "Who brought it, the place, the news, the water"}
    parts = []
    for i, n in enumerate(names, 1):
        on = i == level
        mark = (f'<span class="sls" style="width:34px;height:34px;display:grid;place-items:center;font-size:15px;'
                f'background:{LIGHT if on else "#0E1646"};color:{VDARK if on else NEUT_L};box-shadow:inset 0 0 0 1px {NEUT}">{i}</span>')
        key = "" if on else kc({1: "B", 2: "X", 3: "Y"}[i])
        parts.append(f'<button class="pick" type="button" style="display:flex;align-items:center;gap:14px;width:100%">{mark}'
                     f'<span class="cap" style="font-size:15px;color:{LIGHT if on else NEUT_L}">{n}</span><span style="flex:1"></span>'
                     + ("" if on else f'<span class="ell" style="max-width:380px;font-size:13px;color:{SUB}">{summary[i]}</span>')
                     + f'{key}</button>')
        if on:
            if i == 1:
                content = f'{voyage_pager()}{head_block(26, False)}{stats_row(6)}{route_glow(compact=True)}{preset_rows(dense=True)}'
            elif i == 2:
                content = f'{preset_tabs()}<div style="display:flex;flex-direction:column;gap:8px">' + "".join(seat_row(k) for k in range(3)) + '</div>'
            else:
                content = story_blocks(1, 15)
            parts.append(f'<div style="flex:1;display:flex;flex-direction:column;gap:14px;padding-left:30px;border-left:1px solid {NEUT};margin-left:16px">{content}</div>')
    inner = "".join(parts)
    body.append(f'<section aria-label="Voyage dossier" class="sl" style="position:absolute;left:1160px;top:172px;width:720px;height:868px;box-sizing:border-box;'
                f'padding:22px 26px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:14px">'
                f'{inner}{actions(level)}</section>')
    return "\n".join(body)


def screen_F(level):
    """F - Two cards: deeper levels slide in as a second card between the map and the voyage card; the map narrows."""
    body = [nav(), tabrow("F", "Voyages")]
    if level == 1:
        body.append(mapview(40, 172, 1100, 640, 216, "40% 45%", glow=True))
        inner = f'{voyage_pager()}{head_block(28)}{crew_chip()}{stats_row(3)}{route_glow(compact=True)}{preset_rows(dense=True)}<div style="flex:1"></div>{actions(1)}'
        body.append(f'<section aria-label="Voyage card" class="sl" style="position:absolute;left:1160px;top:172px;width:720px;height:868px;box-sizing:border-box;'
                    f'padding:24px 26px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:16px">{inner}</section>')
        return "\n".join(body)
    body.append(mapview(40, 172, 520, 640, 216, "28% 45%", ladder=False))
    compact = (f'{head_block(24)}{stats_row(3)}{waypoints(vertical=True)}<div style="flex:1"></div>'
               f'<button class="btn cta sl" type="button">{kc("A")}<span>Set sail</span></button>')
    body.append(f'<section aria-label="Voyage card" class="sl" style="position:absolute;left:1440px;top:172px;width:440px;height:868px;box-sizing:border-box;'
                f'padding:22px 22px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:14px">{compact}</section>')
    if level == 2:
        inner = (f'{crew_head()}{preset_tabs()}<div style="display:flex;flex-direction:column;gap:8px">' + "".join(seat_row(i) for i in range(3))
                 + f'</div><div style="flex:1"></div><div style="display:flex;gap:10px"><button class="btn sec sl" type="button" style="flex:1">{kc("X")}<span>Save as Ark</span></button>'
                 f'<button class="btn gho sl" type="button" style="flex:1">{kc("B")}<span>Close</span></button></div>')
    else:
        inner = (f'{story_head()}{story_blocks(1, 15)}<div style="flex:1"></div><button class="btn gho sl" type="button">{kc("B")}<span>Close</span></button>')
    body.append(f'<section aria-label="Level {level}" class="sl" style="position:absolute;left:580px;top:172px;width:840px;height:868px;box-sizing:border-box;'
                f'padding:24px 26px;background:#0A0F30;box-shadow:inset 0 0 0 1px {NEUT};display:flex;flex-direction:column;gap:14px">{inner}</section>')
    return "\n".join(body)


MAP_POS = {"A": "38% 45%", "B": "45% 45%", "C": "40% 45%", "D": "40% 45%", "E": "40% 45%", "F": "40% 45%"}
SCREENS = {"A": screen_A, "B": screen_B, "C": screen_C, "D": screen_D, "E": screen_E, "F": screen_F}


# ------------------------------------------------------------------------------------------------ saga and episodes

def tandava_hero(w, h, set_sail=True, title=64):
    chips = "".join(f'<span class="cap" style="font-size:12px;padding:5px 10px;box-shadow:inset 0 0 0 1px {NEUT};color:#C9CDEA">{t}</span>'
                    for t in ["Great Serpent", "Many-Headed Serpent", "Lord of the Dance", "Antlion"])
    btn = (f'<div style="display:flex;width:360px"><button class="btn cta sl" type="button" style="flex:1">{kc("A")}<span>Set sail</span></button></div>') if set_sail else ""
    return (f'<div class="sl" style="position:relative;width:{w}px;height:{h}px;overflow:hidden;background:#04103A;flex:none">'
            f'<img src="{TANDAVA}" alt="Tandava" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover">'
            f'<div class="abs" style="left:0;right:0;bottom:0;height:62%;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.94))"></div>'
            f'<div class="abs" style="left:32px;right:32px;bottom:28px;display:flex;flex-direction:column;gap:12px">'
            f'<span class="lab">Saga - Part 1</span><span style="font-size:{title}px;letter-spacing:.06em">TANDAVA</span>'
            f'<div style="display:flex;gap:8px;flex-wrap:wrap">{chips}</div>{btn}</div></div>')


def severing(w, h):
    return (f'<div class="sl" style="width:{w}px;height:{h}px;box-sizing:border-box;padding:24px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;'
            f'display:flex;flex-direction:column;gap:10px;flex:none"><span class="lab">Saga - Part 2</span>'
            f'<span style="display:flex;align-items:center;gap:10px;font-size:30px;color:{NEUT_L}">{LOCK}The Severing</span>'
            f'<span style="font-size:15px;color:{SUB}">After Tandava</span></div>')


def part_rows(open_first=True):
    rows = []
    for i, (name, line, on) in enumerate([("Tandava", "Part 1 - open", True), ("The Severing", "Part 2 - after Tandava", False)]):
        rows.append(f'<button class="pick sls" type="button" style="position:relative;display:flex;align-items:center;gap:14px;height:64px;padding:0 18px;'
                    f'background:{"#0E1646" if on else "#060A24"};box-shadow:inset 0 0 0 1px {LIGHT if on else NEUT_D}">'
                    f'<span class="num" style="font-size:18px;color:{LIGHT if on else INACT}">0{i + 1}</span>'
                    f'<span style="display:flex;align-items:center;gap:8px;font-size:20px;color:{LIGHT if on else NEUT_L}">{"" if on else LOCK}{name}</span>'
                    f'<span style="flex:1"></span><span class="cap" style="font-size:12px;color:{CTA if on else INACT}">{line}</span></button>')
    return "".join(rows)


def saga_content(opt):
    x, y, w, h = 40, 184, 1840, 856
    if opt == "A":   # the Voyage window's own frame: art left, the drawer right
        return (f'<div class="abs" style="left:{x}px;top:{y - 12}px;display:flex;gap:20px">{tandava_hero(1080, 868, False)}'
                f'<section aria-label="Saga" class="sl" style="width:740px;height:868px;box-sizing:border-box;padding:26px 28px;background:#080C28;'
                f'box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:12px"><span class="lab">Saga</span>'
                f'<span style="font-size:34px">Tandava</span>{part_rows()}<div style="flex:1"></div>'
                f'<button class="btn cta sl" type="button">{kc("A")}<span>Set sail</span></button></section></div>')
    if opt == "B":   # full-width hero, the next part as a strip
        return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:16px">'
                f'{tandava_hero(w, h - 136)}{severing(w, 120)}</div>')
    if opt == "C":   # the two parts as equal cards
        return (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;gap:20px">{tandava_hero(910, h, title=56)}'
                f'<div class="sl" style="position:relative;width:910px;height:{h}px;overflow:hidden;background:#060A24;box-shadow:inset 0 0 0 1px {NEUT_D}">'
                f'<img src="{TANDAVA}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:.12;filter:grayscale(1)">'
                f'<div class="abs" style="left:32px;bottom:28px;display:flex;flex-direction:column;gap:12px"><span class="lab">Saga - Part 2</span>'
                f'<span style="display:flex;align-items:center;gap:12px;font-size:56px;letter-spacing:.06em;color:{NEUT_L}">{LOCK}THE SEVERING</span>'
                f'<span style="font-size:16px;color:{SUB}">After Tandava</span></div></div></div>')
    if opt == "D":   # hero, and the parts on a sheet risen from the bottom
        return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px">{tandava_hero(w, h, False)}'
                f'<section aria-label="Saga parts" class="sl" style="position:absolute;left:0;right:0;bottom:0;height:210px;box-sizing:border-box;padding:22px 28px;'
                f'background:#080C28;box-shadow:inset 0 0 0 1px #2B3266,0 -30px 50px rgba(0,1,10,.7);display:grid;grid-template-columns:repeat(2, minmax(0, 1fr)) 360px;gap:16px;align-items:end">'
                f'<span class="abs" style="left:50%;top:8px;width:60px;height:3px;margin-left:-30px;background:{NEUT}"></span>'
                f'<div style="display:flex;flex-direction:column;gap:8px"><span class="lab">Part 1</span>{part_rows().split("</button>")[0]}</button></div>'
                f'<div style="display:flex;flex-direction:column;gap:8px"><span class="lab">Part 2</span>{part_rows().split("</button>")[1]}</button></div>'
                f'<button class="btn cta sl" type="button" style="height:64px">{kc("A")}<span>Set sail</span></button></section></div>')
    if opt == "E":   # the parts as numbered steps, the open one large
        return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:14px">'
                f'<div style="display:flex;align-items:center;gap:14px"><span class="sls" style="width:34px;height:34px;display:grid;place-items:center;background:{LIGHT};color:{VDARK}">1</span>'
                f'<span class="cap" style="font-size:15px">Tandava</span></div><div style="margin-left:16px;padding-left:30px;border-left:1px solid {NEUT}">{tandava_hero(w - 46, h - 130)}</div>'
                f'<div style="display:flex;align-items:center;gap:14px;color:{NEUT_L}"><span class="sls" style="width:34px;height:34px;display:grid;place-items:center;background:#0E1646;box-shadow:inset 0 0 0 1px {NEUT}">2</span>'
                f'<span class="cap" style="display:flex;align-items:center;gap:8px;font-size:15px">{LOCK}The Severing</span><span style="flex:1"></span><span style="font-size:13px;color:{INACT}">After Tandava</span></div></div>')
    # F: two cards, the open part wide
    return (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;gap:20px">{tandava_hero(1340, h)}{severing(480, h)}</div>')


def quest_card(k, w, h, art_h=.58):
    num, name, state, line, open_ = QUESTS[k]
    return (f'<div class="sl" style="position:relative;width:{w}px;height:{h}px;overflow:hidden;background:#080C28;flex:none;'
            f'box-shadow:inset 0 0 0 1px {LIGHT if open_ else "#2B3266"}">'
            f'<img src="{QUEST_ART[k]}" alt="" style="position:absolute;left:0;top:0;width:100%;height:{int(art_h * 100)}%;object-fit:cover;opacity:{1 if open_ else .4}">'
            f'<div class="abs" style="left:18px;right:18px;bottom:18px;display:flex;flex-direction:column;gap:8px">'
            f'<span class="lab num" style="color:{LIGHT if open_ else NEUT_L}">{num}</span>'
            f'<span style="display:flex;align-items:center;gap:8px;font-size:22px;color:{LIGHT if open_ else NEUT_L}">{"" if open_ else LOCK}{name.upper()}</span>'
            f'<span class="cap" style="font-size:12px;color:{CTA if open_ else INACT}">{state}</span><span style="font-size:14px;color:{SUB}">{line}</span>'
            + (f'<button class="btn cta sl" type="button" style="height:42px;margin-top:6px">{kc("A")}<span>Play quest</span></button>' if open_ else "")
            + '</div></div>')


def quest_row(k, w):
    num, name, state, line, open_ = QUESTS[k]
    return (f'<button class="pick sls" type="button" style="position:relative;display:flex;align-items:center;gap:16px;width:{w}px;height:112px;padding:0 18px 0 0;overflow:hidden;'
            f'background:{"#0E1646" if open_ else "#060A24"};box-shadow:inset 0 0 0 1px {LIGHT if open_ else NEUT_D}">'
            f'<img src="{QUEST_ART[k]}" alt="" style="width:190px;height:112px;object-fit:cover;opacity:{1 if open_ else .4}">'
            f'<span class="num" style="font-size:18px;color:{LIGHT if open_ else INACT}">{num}</span>'
            f'<span style="display:flex;flex-direction:column;gap:4px"><span style="display:flex;align-items:center;gap:8px;font-size:22px;color:{LIGHT if open_ else NEUT_L}">{"" if open_ else LOCK}{name.upper()}</span>'
            f'<span style="font-size:14px;color:{SUB}">{line}</span></span><span style="flex:1"></span>'
            f'<span class="cap" style="font-size:12px;color:{CTA if open_ else INACT}">{state}</span></button>')


def ep_head(w=None):
    return (f'<div style="display:flex;justify-content:space-between;align-items:flex-end;{"width:" + str(w) + "px" if w else ""}"><div style="display:flex;flex-direction:column;gap:6px">'
            f'<span class="lab">Episode 1</span><span style="font-size:34px">The Hypersea</span></div><div style="display:flex;align-items:center;gap:16px">'
            f'<span class="num" style="font-size:20px">0 / 5</span><span style="width:220px;height:4px;background:{NEUT_D}"></span></div></div>')


def ep2(w=None, h=56):
    return (f'<div class="sls" style="{"width:" + str(w) + "px;" if w else ""}height:{h}px;box-sizing:border-box;display:flex;align-items:center;gap:12px;padding:0 20px;'
            f'background:#060A24;box-shadow:inset 0 0 0 1px {NEUT_D};color:{NEUT_L}">{LOCK}<span class="cap" style="font-size:15px">Episode 2</span>'
            f'<span style="font-size:14px;color:{INACT}">After Episode 1</span></div>')


def episodes_content(opt):
    x, y, w, h = 40, 184, 1840, 856
    if opt == "A":   # the list left where the map sits, the open quest in the drawer
        rows = "".join(quest_row(k, 1080) for k in range(5))
        return (f'<div class="abs" style="left:{x}px;top:{y - 12}px;width:1080px;display:flex;flex-direction:column;gap:12px">{ep_head(1080)}{rows}{ep2(1080, 48)}</div>'
                f'<section aria-label="Quest" class="sl" style="position:absolute;left:1140px;top:172px;width:740px;height:868px;box-sizing:border-box;overflow:hidden;'
                f'background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column">'
                f'<img src="{QUEST_ART[0]}" alt="Scurry" style="width:100%;height:420px;object-fit:cover">'
                f'<div style="padding:24px 28px;display:flex;flex-direction:column;gap:12px;flex:1"><span class="lab">Quest 01 - In progress</span>'
                f'<span style="font-size:40px">SCURRY</span><span style="font-size:16px;color:{SUB}">Intensity 1 of 4</span><div style="flex:1"></div>'
                f'<button class="btn cta sl" type="button">{kc("A")}<span>Play quest</span></button></div></section>')
    if opt == "B":   # five tall cards across, Episode 2 as a bar
        cw = (w - 14 * 4) // 5
        return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:18px">{ep_head()}'
                f'<div style="display:flex;gap:14px">{"".join(quest_card(k, cw, h - 150) for k in range(5))}</div>{ep2()}</div>')
    if opt == "C":   # the old quest tracker: a track of five, the open one large under it
        nodes = "".join(f'<div style="display:flex;flex-direction:column;align-items:center;gap:8px;flex:1"><span class="sls" style="width:44px;height:44px;display:grid;place-items:center;'
                        f'background:{LIGHT if q[4] else "#0E1646"};color:{VDARK if q[4] else NEUT_L};box-shadow:inset 0 0 0 1px {NEUT}">{q[0]}</span>'
                        f'<span class="cap" style="font-size:13px;color:{LIGHT if q[4] else NEUT_L}">{q[1]}</span></div>' for q in QUESTS)
        return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:22px">{ep_head()}'
                f'<div style="position:relative;display:flex"><span class="abs" style="left:10%;right:10%;top:21px;height:2px;background:{NEUT}"></span>{nodes}</div>'
                f'<div style="display:flex;gap:20px">{quest_card(0, 900, 520, .62)}<div style="display:flex;flex-direction:column;gap:14px;width:920px">'
                + "".join(quest_row(k, 920).replace("height:112px", "height:90px").replace("height:112px", "height:90px") for k in range(1, 5))
                + f'</div></div>{ep2()}</div>')
    if opt == "D":   # two bands: Episode 1 open, Episode 2 risen as a locked sheet
        cw = (w - 14 * 4) // 5
        return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;display:flex;flex-direction:column;gap:18px">{ep_head()}'
                f'<div style="display:flex;gap:14px">{"".join(quest_card(k, cw, 520) for k in range(5))}</div>'
                f'<section aria-label="Episode 2" class="sl" style="flex:1;box-sizing:border-box;padding:22px 28px;background:#060A24;box-shadow:inset 0 0 0 1px {NEUT_D};'
                f'display:flex;align-items:center;gap:16px;color:{NEUT_L}">{LOCK}<span class="cap" style="font-size:20px">Episode 2</span>'
                f'<span style="font-size:15px;color:{INACT}">After Episode 1 - five more quests</span></section></div>')
    if opt == "E":   # numbered steps, the quest in progress open
        out = [ep_head()]
        for k, q in enumerate(QUESTS):
            on = q[4]
            out.append(f'<div style="display:flex;align-items:center;gap:14px;color:{LIGHT if on else NEUT_L}"><span class="sls" style="width:34px;height:34px;display:grid;place-items:center;'
                       f'background:{LIGHT if on else "#0E1646"};color:{VDARK if on else NEUT_L};box-shadow:inset 0 0 0 1px {NEUT}">{k + 1}</span>'
                       f'<span class="cap" style="display:flex;align-items:center;gap:8px;font-size:15px">{"" if on else LOCK}{q[1]}</span><span style="flex:1"></span>'
                       f'<span style="font-size:13px;color:{CTA if on else INACT}">{q[2] if on else q[3]}</span></div>')
            if on:
                out.append(f'<div style="margin-left:16px;padding-left:30px;border-left:1px solid {NEUT}">{quest_card(0, w - 46, 420, .7)}</div>')
        out.append(ep2())
        return f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:14px">{"".join(out)}</div>'
    # F: two cards, Episode 1 wide, Episode 2 narrow and locked
    return (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;gap:20px"><section aria-label="Episode 1" class="sl" style="width:1340px;height:{h}px;box-sizing:border-box;'
            f'padding:24px 26px;background:#080C28;box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:12px">{ep_head()}'
            + "".join(quest_row(k, 1288) for k in range(5)) + f'</section>'
            f'<section aria-label="Episode 2" class="sl" style="width:480px;height:{h}px;box-sizing:border-box;padding:24px 26px;background:#060A24;box-shadow:inset 0 0 0 1px {NEUT_D};'
            f'display:flex;flex-direction:column;gap:10px;color:{NEUT_L}"><span class="lab">Episode 2</span><span style="display:flex;align-items:center;gap:10px;font-size:30px">{LOCK}Locked</span>'
            f'<span style="font-size:15px;color:{INACT}">After Episode 1</span></section></div>')


def screen_tabs(opt, which):
    body = [nav(), tabrow(opt, which)]
    body.append(saga_content(opt) if which == "Saga" else episodes_content(opt))
    return "\n".join(body)


# ------------------------------------------------------------------------------------------------ compare and notes

OPTS = {
    "A": ("Drawer", "The card slides in from the right; Ark crew and Story slide over it and leave a spine you press to go back. Map and edge never move."),
    "B": ("Tray", "The arena preview's tray: a narrow card at level 1; Ark crew and Story widen it over the map, which keeps its left third."),
    "C": ("Level tabs", "One card with Voyage, Ark crew and Story as tabs on its top (D-pad); the voyage list sits in the card."),
    "D": ("Rising sheet", "Level 1 is a card at the right; Ark crew and Story rise from the bottom across the width, over the edge view."),
    "E": ("Dossier", "The three levels are numbered steps down one card; the open step fills it, the others fold to one line."),
    "F": ("Two cards", "Ark crew or Story slides in as a second card between the map and the voyage card; the map narrows, the brief stays."),
}
LAYOUT = {  # mini diagrams: (map box, card boxes) in a 300x169 frame per level
    "A": [[(4, 18, 166, 147)], [(4, 18, 166, 147)], [(4, 18, 166, 147)]],
    "B": [[(4, 18, 198, 147)], [(4, 18, 86, 147)], [(4, 18, 86, 147)]],
    "C": [[(4, 18, 172, 147)], [(4, 18, 172, 147)], [(4, 18, 172, 147)]],
    "D": [[(4, 18, 180, 147)], [(4, 18, 180, 70)], [(4, 18, 180, 80)]],
    "E": [[(4, 18, 172, 147)], [(4, 18, 172, 147)], [(4, 18, 172, 147)]],
    "F": [[(4, 18, 172, 147)], [(4, 18, 82, 147)], [(4, 18, 82, 147)]],
}
CARDS = {
    "A": [[(176, 18, 120, 147, "1")], [(176, 18, 9, 147, ""), (186, 18, 110, 147, "2")], [(176, 18, 9, 147, ""), (186, 18, 9, 147, ""), (196, 18, 100, 147, "3")]],
    "B": [[(206, 18, 90, 147, "1")], [(94, 18, 202, 147, "2")], [(94, 18, 202, 147, "3")]],
    "C": [[(180, 18, 116, 147, "1 . .")], [(180, 18, 116, 147, ". 2 .")], [(180, 18, 116, 147, ". . 3")]],
    "D": [[(188, 18, 108, 147, "1")], [(188, 18, 108, 46, "1"), (4, 92, 292, 73, "2")], [(188, 18, 108, 46, "1"), (4, 104, 292, 61, "3")]],
    "E": [[(180, 18, 116, 147, "1 open")], [(180, 18, 116, 147, "2 open")], [(180, 18, 116, 147, "3 open")]],
    "F": [[(180, 18, 116, 147, "1")], [(90, 18, 130, 147, "2"), (224, 18, 72, 147, "1")], [(90, 18, 130, 147, "3"), (224, 18, 72, 147, "1")]],
}


def compare():
    rows = []
    for k, (name, line) in OPTS.items():
        frames = []
        for lv in range(3):
            mx, my, mw, mh = LAYOUT[k][lv][0]
            m = (f'<span class="abs" style="left:{mx}px;top:{my}px;width:{mw}px;height:{mh}px;overflow:hidden">'
                 f'<img src="{MAP}" alt="" style="width:100%;height:100%;object-fit:cover;object-position:40% 45%"></span>')
            cs = "".join(f'<span class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;background:#0E1646;box-shadow:inset 0 0 0 1px {LIGHT if t.strip() else NEUT};'
                         f'display:grid;place-items:center;font-family:\'Chakra Petch\';font-size:12px;color:{LIGHT}">{t}</span>' for x, y, w, h, t in CARDS[k][lv])
            frames.append(f'<div style="display:flex;flex-direction:column;gap:6px"><div style="position:relative;width:240px;height:135px;overflow:hidden">'
                          f'<div style="position:absolute;left:0;top:0;width:300px;height:169px;transform:scale(.8);transform-origin:0 0;background:{VDARK};'
                          f'box-shadow:inset 0 0 0 1px {NEUT_D};overflow:hidden"><span class="abs" style="left:4px;right:4px;top:4px;height:9px;background:{NEUT_D}"></span>{m}{cs}</div></div>'
                          f'<span class="cap" style="font-size:11px;color:{NEUT_L}">Level {lv + 1} - {["Voyage", "Ark crew", "Story"][lv]}</span></div>')
        rows.append(f'<div style="display:flex;flex-direction:column;gap:10px;width:820px"><div style="display:flex;flex-direction:column;gap:4px">'
                    f'<span style="font-size:24px">{k} - {name}{" (recommended)" if k == "A" else ""}</span><span style="font-size:15px;line-height:1.4;color:{SUB}">{line}</span></div>'
                    f'<div style="display:flex;gap:18px">{"".join(frames)}</div></div>')
    body = (f'<div class="abs" style="left:60px;top:44px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - compare</span>'
            f'<span style="font-size:30px">Where the voyage card goes at each level, in all six</span></div>'
            f'<div class="abs" style="left:60px;top:132px;width:1800px;display:grid;grid-template-columns:repeat(2, minmax(0, 1fr));column-gap:120px;row-gap:34px">{"".join(rows)}</div>')
    return body


def notes():
    def para(t):
        return f'<p style="margin:0;font-size:17px;line-height:1.5;color:#C9CDEA">{t}</p>'
    left = "".join([
        f'<span class="lab">Asked (Shombith)</span>',
        para('"Design the new voyage page after taking in Garrett\'s new PR. On the left the whole map preview and the edge on view below, opaque, high res. '
             'Each voyage is given to you by your crew; the card has 3 levels: the voyage (title, description, waypoints, the preset), '
             'the configuration (the 12 settings, a separate level), and the story (optional). Saga and Episodes keep their content, with LB and RB."'),
        f'<span class="lab" style="margin-top:10px">Shared by all six</span>',
        para("The map and the edge view are the Voyage Board's own render at 2x (Neighbourhood tier, the 20 ly cells, Tumult to Lantern Reach), drawn opaque. "
             "Level 1: title, the crew's summary, who found it, course / sail / intensity / pays / finds / odds, the course cell by cell, the waypoints, "
             "and Your Ark: the Arks you configured (Skim Wing, Breakers) to choose from, or Configure. Level 2: three seats x vessel, pilot, element, artifact = the 12 settings, "
             "a preset switcher and Save as Ark. Level 3: who brought it, the place, the news, the water - optional. A Set sail, X Ark crew, Y Story, B back."),
        para("Every word is the slice's or the Voyage Board's (voyage_slice.json job.hunt.sighting, the crew's line, the place's blurb, the seed news)."),
    ])
    right = "".join([
        f'<span class="lab">Recommendation: A, the drawer</span>',
        para("The map and edge view never move or shrink at any level; the card slides in like the arena preview's tray, each level is one press deeper, and the spines "
             "show where you are and take you back. B keeps the tray literally but hides two thirds of the map at levels 2 and 3. "
             "C and E are the quietest; E reads best with a pad. D gives the 12 settings the most room but covers the edge view. "
             "F shows brief and crew together at the cost of a narrow map."),
        f'<span class="lab" style="margin-top:10px">Differs from the build</span>',
        para("Built (Garrett, 94c1050b / a1922d78, #1717 open): SAGA and EPISODES as buttons opening modals; the map and edge view above a three-across grid of voyage cards "
             "that open over each other. This round: Saga | Voyages | Episodes are the window's tabs on LB / RB; the map and edge view take the left side, opaque; "
             "the grid becomes the voyage list (a strip on the map, or a list in the card); the open card gains the preset (an Ark), a level for the 12 settings and a story level."),
        para("Garrett's map (VoyageMapGraphic, VoyageEdgeGraphic) stays as it is; only where it sits changes. The build waits on a pick."),
    ])
    return (f'<div class="abs" style="left:60px;top:44px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:30px">The Voyage window: map left, the card in three levels, six ways</span></div>'
            f'<div class="abs" style="left:60px;top:140px;width:860px;display:flex;flex-direction:column;gap:12px">{left}</div>'
            f'<div class="abs" style="left:1000px;top:140px;width:860px;display:flex;flex-direction:column;gap:12px">{right}</div>')


def main():
    files = {}
    files[f"R{REV}Compare.dc.html"] = board(f"Rev {REV} - compare", compare())
    for k in OPTS:
        for lv, key in ((1, "Voyage"), (2, "Crew"), (3, "Story")):
            page = board(f"Rev {REV} {k} - {OPTS[k][0]} - level {lv}", SCREENS[k](lv))
            files[f"R{REV}{k}{key}.dc.html"] = with_map(with_glow(page), MAP_POS[k]) if lv == 1 else page
        files[f"R{REV}{k}Saga.dc.html"] = board(f"Rev {REV} {k} - {OPTS[k][0]} - Saga", screen_tabs(k, "Saga"))
        files[f"R{REV}{k}Episodes.dc.html"] = board(f"Rev {REV} {k} - {OPTS[k][0]} - Episodes", screen_tabs(k, "Episodes"))
    files[f"R{REV}Notes.dc.html"] = board(f"Rev {REV} - notes", notes())
    for n, s in files.items():
        (ROOT / n).write_text(s, encoding="utf-8")
        print(f"{n:32} {len(s):>7}")


if __name__ == "__main__":
    main()
