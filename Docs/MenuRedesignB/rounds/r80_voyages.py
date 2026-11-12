#!/usr/bin/env python3
"""Rev 80 boards: the Voyage window, B Logbook taken further (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md section 161).

Shombith, 2026-11-09, on Rev 79: "B looks good make it better more mockus inspired from B". Every option keeps B's
order - the voyages as a list on the left, the picked voyage's course in the middle, the squad on the right - and
answers what B left plain: rows with nothing to recognise a voyage by, a loud course map, dead space under the
squad, seats with no pilot.

  B1  Refined     each row a course thumbnail and who brought it; seats with their pilot
  B2  Wide sea    a narrow list, the course map wide with the voyage over it, the squad a bar along the foot
  B3  Crew        the squad as three tall seat cards, the hull and its pilot large
  B4  Quiet       no pictures in the list, the maps muted so only the course is bright; the most air
  Compare         what each one changes from B, piece by piece
  Notes

    python3 Docs/MenuRedesignB/rounds/r80_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "80"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r79", HERE / "r79_voyages.py")
r79 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r79)
r71 = r79.r71

REV, ROOT = int(_rev), r79.ROOT
blob, page, nav, GROUND = r71.blob, r71.page, r71.nav, r71.GROUND
HULL = r71.HULL
PILOT = {"Shrapnel": blob("109d50c83786e87bc77b272531aadd87"), "Feist": blob("4f08e5dc0445758132618029646325ef"),
         "Longshift": blob("a31623aedd55f09eb0f08c21b3f141da"), "Flux": blob("858b88d97e9cf77b716845a5c67979fa")}

# The squads with pilots whose portraits are on the canvas (the content is the board's own, as in Rev 70).
SQUADS = [
    {"name": "Skim Wing", "dom": "Jade", "mine": True, "owner": "",
     "seats": [{"role": "Leader", "who": "You", "v": "Sparrow", "p": "Shrapnel", "e": "Charge", "a": "Extra Bay"},
               {"role": "Rogue One", "who": "Halcyon", "v": "Dolphin", "p": "Feist", "e": "Space", "a": "Seed Pod"},
               {"role": "Rogue Two", "who": "AI wingmate", "v": "Squirrel", "p": "Longshift", "e": "Time", "a": "No artifact"}]},
    {"name": "Breakers", "dom": "Ruby", "mine": False, "owner": "Kestrel",
     "seats": [{"role": "Leader", "who": "Kestrel", "v": "Rhino", "p": "Flux", "e": "Mass", "a": "Long Edge"},
               {"role": "Rogue One", "who": "You", "v": "Manta", "p": "Shrapnel", "e": "Charge", "a": "Tailwind"},
               {"role": "Rogue Two", "who": "AI wingmate", "v": "Serpent", "p": "Feist", "e": "Space", "a": "No artifact"}]},
]
DATA = (r79.DATA.replace("const SQ = " + json.dumps(r71.SQUADS), "const SQ = " + json.dumps(SQUADS))
        + "const PILOT = " + json.dumps(PILOT) + ";\n")
assert "const SQ = " + json.dumps(SQUADS) in DATA

VALS = r79.VALS + """
  seatNames.forEach((s, i) => { s.face = PILOT[seats[i].p]; s.short = seats[i].who === 'AI wingmate' ? 'AI' : seats[i].who; });
  voyage.i = String(v.i);
  vlist.forEach((r, i) => { r.map = V[i].map; r.pos = V[i].pos; r.crew = 'Brought by ' + V[i].crew; });
"""


def logic(extra_vals="", extra_ret=""):
    return DATA + ("class Component extends DCLogic {\nconstructor(p) { super(p); " + r71.START + " }\nrenderVals() {"
                   + VALS + extra_vals + "  return { " + r79.RET + (", " + extra_ret if extra_ret else "") + " };\n}\n}")


def window(body, title, extra_vals="", extra_ret=""):
    return page(title, GROUND + nav(crystals=False) + r79.header() + body, logic(extra_vals, extra_ret))


tab_text, hull_grid = r79.tab_text, r79.hull_grid
CTA = '<a class="btn cta" href="#" style="{style}"><span class="kc">A</span>Set sail</a>'
MUTED = "filter:saturate(.45) brightness(.82)"


def course(x, y, w, h, extra="", muted=False, alt="The picked voyage's course on the HyperSea", plain=False):
    f = MUTED if muted else ""
    plate = "" if plain else "background:#00010A;box-shadow:inset 0 0 0 1px #434C89"
    return (f'<figure class="abs sl" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;margin:0;overflow:hidden;{plate}"><img src="{{{{voyage.map}}}}" alt="{alt}" style="position:absolute;inset:0;'
            f'width:100%;height:100%;object-fit:cover;object-position:{{{{voyage.pos}}}};{f}">{extra}</figure>')


def edge(x, y, w, h, muted=False, plain=False):
    f = MUTED if muted else ""
    plate = "" if plain else "background:#00010A;box-shadow:inset 0 0 0 1px #434C89"
    return (f'<figure class="abs sl" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;margin:0;overflow:hidden;{plate}"><img src="{{{{voyage.edge}}}}" alt="The course seen edge on, through the disk" '
            f'style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:40% 82%;{f}"></figure>')


def stats(labelled=True, size=28):
    if not labelled:
        return (f'<span class="num" style="font-size:{size}px">{{{{voyage.ly}}}}  ·  {{{{voyage.min}}}}  ·  '
                '{{voyage.intensity}}</span>')
    cell = lambda lab, val: (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">{lab}</span>'
                             f'<span class="num" style="font-size:{size}px">{val}</span></div>')
    return ('<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:16px">'
            + cell("Distance", "{{voyage.ly}}") + cell("Sail", "{{voyage.min}}") + cell("Intensity", "{{voyage.intensity}}")
            .replace("{{voyage.intensity}}", "{{voyage.i}}") + '</div>')


def squad_head(style=""):
    return (f'<button class="pick lab" type="button" onClick="{{{{squad.next}}}}" style="color:#E6E9FF;text-align:left;{style}">'
            'Squad - {{squad.name}}  &gt;</button>')


# ── B1: Refined ───────────────────────────────────────────────────────────────────────────────────────────────────────

def b1_board():
    rows = ('<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:540px;display:flex;flex-direction:column;gap:8px">'
            '<sc-for list="{{vlist}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{r.pressed}}" '
            'onClick="{{r.pick}}" style="height:190px;box-sizing:border-box;padding:0 22px;display:grid;grid-template-columns:150px 1fr;'
            'gap:22px;align-items:center;background:{{r.bg}};box-shadow:inset 3px 0 0 {{r.bar}}">'
            '<img class="sls" src="{{r.map}}" alt="" style="width:150px;height:110px;object-fit:cover;object-position:{{r.pos}}">'
            '<span style="display:flex;flex-direction:column;gap:8px"><span style="font-size:22px;color:{{r.col}}">{{r.n}}  {{r.place}}</span>'
            '<span class="num" style="font-size:15px;line-height:1.6;color:#747BAD">{{r.crew}}<br>{{r.meta}}</span></span>'
            '</button></sc-for></nav>')
    seats = ('<div style="display:flex;flex-direction:column;gap:10px;margin-top:14px"><sc-for list="{{seatNames}}" as="s" hint-placeholder-count="3">'
             '<button class="pick sls" type="button" aria-pressed="{{s.pressed}}" onClick="{{s.pick}}" style="height:96px;display:grid;'
             'grid-template-columns:130px 64px 1fr;gap:16px;align-items:center;padding:0 16px;box-shadow:{{s.edge}}">'
             '<img class="hull" src="{{s.hull}}" alt="{{s.role}}, {{s.who}}: {{s.name}}, press to change" style="width:130px;height:74px">'
             '<img src="{{s.face}}" alt="" style="width:64px;height:80px;object-fit:cover;object-position:50% 20%">'
             '<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{{s.name}}<br>{{s.pilot}} - {{s.el}}</span>'
             '</button></sc-for></div>')
    body = f"""{tab_text(64, 178)}
{rows}
{course(620, 244, 740, 470, muted=True, plain=True)}
{edge(620, 734, 740, 306, muted=True, plain=True)}
<section class="abs" style="left:1400px;top:244px;width:480px;height:796px;display:flex;flex-direction:column">
<span style="font-size:28px;line-height:1.25">{{{{voyage.title}}}}</span>
<span style="font-size:18px;line-height:1.5;color:#E6E9FF;margin-top:14px">{{{{voyage.line}}}}</span>
<div style="margin-top:24px">{stats(labelled=False, size=22)}</div>
<span style="font-size:15px;line-height:1.5;color:#747BAD;margin-top:22px">{{{{voyage.placeLine}}}}</span>
{squad_head("margin-top:auto")}
{seats}
{CTA.format(style="margin-top:20px;width:100%")}
</section>
{hull_grid(620, 244, 740, 3, 176)}"""
    return window(body, f"Rev {REV} B1 - Refined: each row a course thumbnail and who brought it, seats with their pilot (recommended) - interactive")


# ── B2: Wide sea ──────────────────────────────────────────────────────────────────────────────────────────────────────

def b2_board():
    rows = ('<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:420px;display:flex;flex-direction:column;gap:6px">'
            '<sc-for list="{{vlist}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{r.pressed}}" '
            'onClick="{{r.pick}}" style="height:150px;box-sizing:border-box;padding:0 24px;display:flex;flex-direction:column;justify-content:center;'
            'gap:10px;background:{{r.bg}};box-shadow:inset 3px 0 0 {{r.bar}}"><span style="font-size:22px;color:{{r.col}}">{{r.n}}  {{r.place}}</span>'
            '<span class="num" style="font-size:15px;color:#747BAD">{{r.meta}}</span></button></sc-for></nav>')
    over = ('<div class="abs sl" style="left:28px;bottom:28px;width:640px;box-sizing:border-box;padding:26px 30px;background:rgba(0,1,10,.88);'
            'display:flex;flex-direction:column;gap:12px"><span class="lab">{{voyage.kicker}}</span>'
            '<span style="font-size:28px;line-height:1.25">{{voyage.title}}</span>'
            '<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{{voyage.line}}</span>' + stats(labelled=False, size=22) + '</div>')
    bar = ('<div class="abs" style="left:500px;top:928px;width:960px;height:112px;display:grid;grid-template-columns:repeat(3,1fr);gap:12px">'
           '<sc-for list="{{seatNames}}" as="s" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{s.pressed}}" '
           'onClick="{{s.pick}}" style="display:grid;grid-template-columns:140px 1fr;align-items:center;gap:12px;padding:0 16px;box-shadow:{{s.edge}}">'
           '<img class="hull" src="{{s.hull}}" alt="{{s.role}}, {{s.who}}: {{s.name}}, press to change" style="width:140px;height:80px">'
           '<span style="font-size:18px;color:#E6E9FF">{{s.name}}<br><span style="font-size:15px;color:#747BAD">{{s.short}}</span></span></button>'
           '</sc-for></div>')
    body = f"""{tab_text(64, 178)}
{rows}
{course(500, 244, 1380, 520, extra=over)}
{edge(500, 780, 1380, 120)}
{squad_head("position:absolute;left:500px;top:906px;z-index:2")}
{bar}
{CTA.format(style="position:absolute;left:1480px;top:984px;width:400px")}
{hull_grid(500, 400, 960, 6, 200)}"""
    return window(body, f"Rev {REV} B2 - Wide sea: a narrow list, the course map wide with the voyage over it, the squad a bar along the foot - interactive")


# ── B3: Crew ──────────────────────────────────────────────────────────────────────────────────────────────────────────

def b3_board():
    rows = ('<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:440px;display:flex;flex-direction:column;gap:6px">'
            '<sc-for list="{{vlist}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{r.pressed}}" '
            'onClick="{{r.pick}}" style="height:150px;box-sizing:border-box;padding:0 24px;display:flex;flex-direction:column;justify-content:center;'
            'gap:10px;background:{{r.bg}};box-shadow:inset 3px 0 0 {{r.bar}}"><span style="font-size:22px;color:{{r.col}}">{{r.n}}  {{r.place}}</span>'
            '<span class="num" style="font-size:15px;color:#747BAD">{{r.meta}}</span></button></sc-for></nav>')
    cards = ('<div class="abs" style="left:1140px;top:282px;width:740px;height:640px;display:grid;grid-template-columns:repeat(3,1fr);gap:14px">'
             '<sc-for list="{{seatNames}}" as="s" hint-placeholder-count="3"><button class="pick sl" type="button" aria-pressed="{{s.pressed}}" '
             'onClick="{{s.pick}}" style="position:relative;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:8px;'
             'padding:0 12px 22px;background:linear-gradient(180deg,rgba(0,4,31,.3),rgba(0,4,31,.92));box-shadow:{{s.edge}}">'
             '<img src="{{s.face}}" alt="" style="width:210px;height:268px;object-fit:contain;object-position:50% 100%">'
             '<img class="hull" src="{{s.hull}}" alt="{{s.role}}, {{s.who}}: {{s.name}}, press to change" style="width:100%;height:150px">'
             '<span style="font-size:22px">{{s.name}}</span><span style="font-size:15px;line-height:1.5;color:#E6E9FF;text-align:center">'
             '{{s.short}} - {{s.pilot}}<br>{{s.el}}</span></button></sc-for></div>')
    body = f"""{tab_text(64, 178)}
{rows}
<div class="abs" style="left:520px;top:244px;width:580px;display:flex;flex-direction:column;gap:12px"><span class="lab">{{{{voyage.kicker}}}}</span><span style="font-size:28px;line-height:1.25">{{{{voyage.title}}}}</span>{stats(labelled=False, size=22)}</div>
{course(520, 380, 580, 420, plain=True)}
{edge(520, 820, 580, 220, plain=True)}
{squad_head("position:absolute;left:1140px;top:244px")}
{cards}
{CTA.format(style="position:absolute;left:1140px;top:984px;width:740px")}
{hull_grid(520, 380, 580, 3, 150)}"""
    return window(body, f"Rev {REV} B3 - Crew: the squad as three tall seat cards, the hull and its pilot large - interactive")


# ── B4: Quiet ─────────────────────────────────────────────────────────────────────────────────────────────────────────

def b4_board():
    rows = ('<nav aria-label="Voyages on offer" class="abs" style="left:64px;top:252px;width:470px;display:flex;flex-direction:column">'
            '<sc-for list="{{vlist}}" as="r" hint-placeholder-count="4"><button class="pick" type="button" aria-pressed="{{r.pressed}}" '
            'onClick="{{r.pick}}" style="height:186px;box-sizing:border-box;display:flex;flex-direction:column;justify-content:center;gap:12px;'
            'border-bottom:1px solid #434C89"><span style="font-size:28px;color:{{r.col}}">{{r.place}}</span>'
            '<span class="num" style="font-size:15px;color:#747BAD">{{r.meta}}</span></button></sc-for></nav>')
    body = f"""{tab_text(64, 178)}
{rows}
{course(600, 252, 780, 520, muted=True)}
{edge(600, 792, 780, 248, muted=True)}
<section class="abs" style="left:1440px;top:252px;width:440px;height:788px;display:flex;flex-direction:column">
<span style="font-size:28px;line-height:1.3">{{{{voyage.title}}}}</span>
<span style="font-size:18px;line-height:1.6;color:#E6E9FF;margin-top:18px">{{{{voyage.line}}}}</span>
<div style="margin-top:28px">{stats(labelled=False, size=22)}</div>
{squad_head("margin-top:auto")}
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-top:14px"><sc-for list="{{{{seatNames}}}}" as="s" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{{{s.pressed}}}}" onClick="{{{{s.pick}}}}" style="height:120px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:6px;padding-bottom:10px;box-shadow:{{{{s.edge}}}}"><img class="hull" src="{{{{s.hull}}}}" alt="{{{{s.role}}}}, {{{{s.who}}}}: {{{{s.name}}}}, press to change" style="width:90%;height:70px"><span style="font-size:15px;color:#E6E9FF">{{{{s.name}}}}</span></button></sc-for></div>
{CTA.format(style="margin-top:20px;width:100%")}
</section>
{hull_grid(600, 252, 780, 3, 176)}"""
    return window(body, f"Rev {REV} B4 - Quiet: no pictures in the list, the maps muted so only the course is bright, the most air - interactive")


# ── Compare: what each changes from B ─────────────────────────────────────────────────────────────────────────────────

def compare_board():
    v = r71.VOYAGES[1]
    s0 = SQUADS[0]["seats"]

    def label(x, y, words):
        return f'<span class="abs" style="left:{x}px;top:{y}px;width:860px;font-size:18px;color:#E6E9FF">{words}</span>'

    b1 = (label(60, 70, "B1  A voyage row: its course, who brought it, its numbers")
          + f'<div class="abs sls" style="left:60px;top:110px;width:600px;height:190px;box-sizing:border-box;padding:0 22px;display:grid;'
            f'grid-template-columns:150px 1fr;gap:22px;align-items:center;background:rgba(0,4,31,.86);box-shadow:inset 3px 0 0 #00D4FF">'
            f'<img class="sls" src="{v["map"]}" alt="" style="width:150px;height:110px;object-fit:cover;object-position:{v["pos"]}">'
            f'<span style="display:flex;flex-direction:column;gap:8px"><span style="font-size:22px">02  {v["place"]}</span>'
            f'<span style="font-size:15px;color:#747BAD">Brought by {v["crew"]}</span>'
            f'<span class="num" style="font-size:15px;color:#747BAD">{v["ly"]} · {v["min"]} · Intensity {v["i"]}</span></span></div>')
    b2 = (label(1000, 70, "B2  The squad as a bar along the foot, beside Set sail")
          + '<div class="abs" style="left:1000px;top:110px;width:860px;height:112px;display:grid;grid-template-columns:repeat(3,1fr);gap:12px">'
          + "".join(f'<div class="sls" style="display:grid;grid-template-columns:120px 1fr;align-items:center;gap:10px;padding:0 14px;'
                    f'box-shadow:inset 0 0 0 1px #434C89"><img class="hull" src="{HULL[s["v"]]}" alt="" style="width:120px;height:70px">'
                    f'<span style="font-size:18px">{s["v"]}</span></div>' for s in s0) + '</div>')
    b3 = (label(60, 380, "B3  A seat card: the pilot and the hull, large")
          + '<div class="abs" style="left:60px;top:420px;width:640px;height:520px;display:grid;grid-template-columns:repeat(3,1fr);gap:12px">'
          + "".join(f'<div class="sl" style="display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:8px;padding-bottom:16px;'
                    f'background:linear-gradient(180deg,rgba(0,4,31,.3),rgba(0,4,31,.92));box-shadow:inset 0 0 0 1px #434C89">'
                    f'<img src="{PILOT[s["p"]]}" alt="" style="width:120px;height:150px;object-fit:cover;object-position:50% 18%">'
                    f'<img class="hull" src="{HULL[s["v"]]}" alt="" style="width:100%;height:110px"><span style="font-size:18px">{s["v"]}</span></div>'
                    for s in s0) + '</div>')
    b4 = (label(1000, 380, "B4  A quiet row, and the course with its colour turned down")
          + f'<div class="abs" style="left:1000px;top:420px;width:420px;height:150px;display:flex;flex-direction:column;justify-content:center;gap:12px;'
            f'border-bottom:1px solid #434C89"><span style="font-size:28px">{v["place"]}</span>'
            f'<span class="num" style="font-size:15px;color:#747BAD">{v["ly"]} · {v["min"]} · Intensity {v["i"]}</span></div>'
            f'<img class="abs sls" src="{v["map"]}" alt="" style="left:1460px;top:420px;width:400px;height:300px;object-fit:cover;'
            f'object-position:{v["pos"]};{MUTED}">')
    body = (GROUND.replace('top:84px;width:1920px;height:996px', 'top:0;width:1920px;height:1080px')
            + '<span class="abs lab" style="left:60px;top:28px">What each option changes from B</span>' + b1 + b2 + b3 + b4)
    return page(f"Rev {REV} - What each option changes from B: the row, the squad bar, the seat card, the quiet row", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}")


def notes_board():
    rows = [
        ("Asked", "\"B looks good make it better more mockus inspired from B\" (Shombith, 2026-11-09, on Rev 79)."),
        ("Kept from B", "The order: the voyages as a list on the left, the picked voyage's course in the middle with the edge view "
                        "under it, the squad on the right, one Set sail. The tabs as large words. The vessel grid: one press picks."),
        ("B1 Refined (recommended)", "Each row shows its course, who brought it and its numbers, so a voyage is recognised at a glance. "
                        "Each seat shows its pilot beside the hull. The maps' colour is turned down so the "
                        "course reads first."),
        ("B2 Wide sea", "The course map takes most of the window with the voyage written over it; the squad is a bar along the foot."),
        ("B3 Crew", "The squad leads: three tall seat cards, the pilot and the hull large; the course is smaller."),
        ("B4 Quiet", "The calmest: the list is words only, the maps muted, the most space between things."),
        ("Pilots", "The seats use the four pilot portraits on the canvas (Shrapnel, Feist, Longshift, Flux)."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:340px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:26px">'
            f'<span style="font-size:36px">Rev {REV} - B Logbook, taken further</span>{items}</div>')
    return page(f"Rev {REV} - notes: what B keeps, what each option changes, the recommendation", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}Compare.dc.html": compare_board, f"R{REV}B1.dc.html": b1_board, f"R{REV}B2.dc.html": b2_board,
          f"R{REV}B3.dc.html": b3_board, f"R{REV}B4.dc.html": b4_board, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
