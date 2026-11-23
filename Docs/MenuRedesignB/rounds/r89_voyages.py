#!/usr/bin/env python3
"""Rev 89 boards: the Voyages screen with the HyperSea as the show (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md
section 170).

Shombith, 2026-11-12, on Rev 87/88: "Delete B and C but still for the voyages UI, I do not know I dont like it ...
give me some cool ass designed UI for the voyage".

Rev 87 and 88 muted the map (saturate .45) and boxed it into a 660 column between two columns of plates, so the one
thing that was to stay was the dullest thing on screen. Here the map and its edge-on view are the screen, in the
map's own colours:

  A  Sea table  the map lies tilted in perspective and the edge-on view is its front face: one slab cut out of the
                HyperSea, the course across its top and down its side. The voyage's name stands over it.
  B  Flat chart the same screen without the tilt: the whole map, uncropped and in its own colours, over its edge view.
  Notes

    python3 Docs/MenuRedesignB/rounds/r89_voyages.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "89"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r88", HERE / "r88_voyages.py")
r88 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r88)
r87 = r88.r87

REV, ROOT, page, nav, GROUND = int(_rev), r88.ROOT, r88.page, r88.nav, r88.GROUND
JADE, INK, DIM, LINE = r88.JADE, r88.INK, r88.DIM, r88.LINE
GLASS = "rgba(0,4,31,.78)"
MAP_ASPECT, EDGE_ASPECT = 2196 / 1358, 2196 / 438


def logic():
    lg = r88.voyage_logic(1)
    old = "  return { voyage, vlist, tabs, presets, seatList, tray };"
    assert old in lg
    return lg.replace(old, """  const hero = { kicker: v.esc ? 'The Ark' : 'Voyage 0' + v.n + ' of 0' + V.length + ' - brought by ' + v.crew,
    stats: v.ly + '   \\u00B7   ' + v.min + '   \\u00B7   ' + (v.esc ? 'Difficulty ' + st.d : 'Intensity ' + v.i),
    esc: !!v.esc, diff: String(st.d), down: () => this.setState({ d: Math.max(1, st.d - 1) }), up: () => this.setState({ d: Math.min(4, st.d + 1) }) };
  vlist.forEach((r, i) => { r.ink = i === st.v ? '#E6E9FF' : '#747BAD'; r.plate = i === st.v ? 'rgba(0,4,31,.94)' : 'rgba(0,4,31,.62)';
    r.edge = i === st.v ? 'inset 3px 0 0 #00D4FF' : 'inset 0 0 0 1px rgba(67,76,137,.7)'; r.thumb = i === st.v ? 1 : 0.5; });
  return { voyage, vlist, tabs, presets, seatList, tray, hero };""")


# Where each tab goes once A was picked: the Voyages screen is this round's A, the other three are Rev 90's.
TAB_FILES = {"Voyages": f"R{REV}A", "Saga": "R90Saga", "Quests": "R90Quests", "Episodes": "R90Episodes"}


def tabs(lit):
    out = []
    for name, target in TAB_FILES.items():
        on = name == lit
        soon = '<span style="font-size:14px;margin-left:8px;letter-spacing:.12em">Soon</span>' if name == "Episodes" else ""
        out.append(f'<a class="cap" href="{target}.dc.html" aria-current="{"page" if on else "false"}" '
                   f'style="font-size:15px;text-decoration:none;color:{INK if on else DIM};padding-bottom:8px;'
                   f'border-bottom:2px solid {INK if on else "rgba(0,0,0,0)"}">{name}{soon}</a>')
    return f'<nav aria-label="Voyage window tabs" class="abs" style="left:64px;top:186px;display:flex;gap:34px;align-items:flex-end">{"".join(out)}</nav>'


def window(body, title, ground=GROUND, lit="Voyages", lg=None):
    return page(title, ground + nav(crystals=False) + r88.header() + tabs(lit) + body, lg or logic())


def difficulty(style):
    """The Ark escort's one setting, where it is picked."""
    arrow = lambda fn, lab, svg: (f'<button class="pick" type="button" aria-label="{lab}" onClick="{{{{hero.{fn}}}}}" '
                                  f'style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:{INK}">{svg}</button>')
    return (f'<sc-if value="{{{{hero.esc}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="{style}"><span class="lab">Difficulty</span>'
            f'<div class="sls" style="width:180px;height:44px;display:flex;align-items:center;justify-content:space-between;background:#00010A;box-shadow:inset 0 0 0 1px {LINE}">'
            + arrow("down", "Easier", r87.CHEV_L) + '<span class="num" style="font-size:22px">{{hero.diff}}</span>' + arrow("up", "Harder", r87.CHEV_R)
            + '</div></div></sc-if>')


def voyage_chips(direction, w, h):
    """The voyages on offer: its number and place, its minutes, a sliver of its own water; the picked one lit."""
    return ('<sc-for list="{{vlist}}" as="r" hint-placeholder-count="5"><button class="pick sls" type="button" aria-pressed="{{r.pressed}}" '
            f'onClick="{{{{r.pick}}}}" style="position:relative;{w};height:{h}px;overflow:hidden;background:{{{{r.plate}}}};box-shadow:{{{{r.edge}}}}">'
            '<img src="{{r.map}}" alt="" style="position:absolute;right:0;top:0;width:44%;height:100%;object-fit:cover;object-position:{{r.pos}};opacity:{{r.thumb}};'
            '-webkit-mask-image:linear-gradient(90deg,transparent,#000 70%);mask-image:linear-gradient(90deg,transparent,#000 70%)">'
            '<span style="position:absolute;left:20px;top:14px;font-size:18px;color:{{r.ink}}">{{r.head}}</span>'
            f'<span class="num" style="position:absolute;left:20px;top:44px;font-size:14px;color:{DIM}">{{{{r.meta}}}}</span></button></sc-for>')


def seats(card_w):
    """The squad: a card per seat - pilot, vessel, who flies it, hull. A pressed card opens its four steppers under it."""
    hull = 150 if card_w >= 420 else 112
    return f"""<div style="display:flex;flex-direction:column;gap:10px">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><div class="sls" style="opacity:{{{{s.op}}}};background:{GLASS};box-shadow:{{{{s.edge}}}}">
<button class="pick" type="button" aria-pressed="{{{{s.pressed}}}}" aria-label="{{{{s.alt}}}}" onClick="{{{{s.pick}}}}" style="width:100%;height:128px;display:grid;grid-template-columns:84px 1fr {hull}px;gap:12px;align-items:center;padding:0 14px;box-sizing:border-box">
<img src="{{{{s.face}}}}" alt="" style="width:84px;height:120px;align-self:end;object-fit:contain;object-position:50% 100%">
<span style="display:flex;flex-direction:column;gap:5px"><span style="font-size:20px">{{{{s.name}}}}</span><span style="font-size:14px;color:{INK}">{{{{s.line1}}}}</span><span style="font-size:14px;color:{DIM}">{{{{s.line2}}}}</span></span>
<img class="hull" src="{{{{s.hull}}}}" alt="" style="width:{hull}px;height:84px"></button>
<sc-if value="{{{{s.open}}}}" hint-placeholder-val="{{{{ false }}}}">{r87.steppers().replace("font-size:18px", "font-size:15px").replace("padding:4px 18px 16px", "padding:2px 14px 14px")}</sc-if>
</div></sc-for></div>"""


# ── A: Sea table ──────────────────────────────────────────────────────────────────────────────────────────────────────

def a_board():
    w, x = 1020, 380
    mh, eh = round(w / MAP_ASPECT), round(w / EDGE_ASPECT)
    # One slab: the map tilted back about its near edge, the edge-on view standing under that edge as its front face.
    slab = f"""<div class="abs" aria-hidden="true" style="left:270px;top:890px;width:1240px;height:190px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(230,233,255,.10),rgba(230,233,255,0))"></div>
<div class="abs" style="left:{x}px;top:214px;width:{w}px;height:800px;perspective:1500px;perspective-origin:50% 0%">
<figure style="position:absolute;left:0;bottom:{eh}px;width:{w}px;height:{mh}px;margin:0;transform-origin:50% 100%;transform:rotateX(56deg);-webkit-mask-image:linear-gradient(0deg,#000 52%,transparent 96%);mask-image:linear-gradient(0deg,#000 52%,transparent 96%)">
<img src="{{{{voyage.map}}}}" alt="The picked voyage's course on the HyperSea" style="display:block;width:100%;height:100%"></figure>
<figure style="position:absolute;left:0;bottom:0;width:{w}px;height:{eh}px;margin:0;box-shadow:0 -1px 0 {INK}">
<img src="{{{{voyage.edge}}}}" alt="The course seen edge on, through the disk" style="display:block;width:100%;height:100%"></figure>
</div>"""
    name = f"""<header class="abs" style="left:{x}px;top:238px;width:{w}px;display:flex;flex-direction:column;align-items:center;gap:8px;z-index:3">
<span class="lab">{{{{hero.kicker}}}}</span>
<span style="font-size:48px;line-height:1.05">{{{{voyage.place}}}}</span>
<span style="font-size:18px;color:{INK}">{{{{voyage.title}}}}</span>
<span class="num" style="font-size:22px;margin-top:6px;white-space:pre">{{{{hero.stats}}}}</span>
{difficulty("display:flex;align-items:center;gap:14px;margin-top:4px")}
</header>"""
    left = f"""<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:310px;display:flex;flex-direction:column;gap:10px">
{voyage_chips("column", "width:100%", 76)}</nav>"""
    right = f"""<section aria-label="The squad" class="abs" style="left:1430px;top:244px;width:450px;height:796px;display:flex;flex-direction:column">
{r87.squads("margin-bottom:16px")}
{seats(450)}
{r87.sail("margin-top:auto;width:100%")}
</section>"""
    return window(slab + name + left + right,
                  f"Rev {REV} A - Sea table: the map tilted in perspective, the edge-on view its front face; the voyage's name over it (recommended) - interactive")


# ── B: Open sea ───────────────────────────────────────────────────────────────────────────────────────────────────────

def b_board():
    w, x, y = 1000, 460, 232
    mh, eh = round(w / MAP_ASPECT), round(w / EDGE_ASPECT)
    sea = f"""<figure class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{mh}px;margin:0"><img src="{{{{voyage.map}}}}" alt="The picked voyage's course on the HyperSea" style="display:block;width:100%;height:100%"></figure>
<figure class="abs" style="left:{x}px;top:{y + mh}px;width:{w}px;height:{eh}px;margin:0;box-shadow:0 -1px 0 {INK}"><img src="{{{{voyage.edge}}}}" alt="The course seen edge on, through the disk" style="display:block;width:100%;height:100%"></figure>"""
    left = f"""<section aria-label="The open voyage" class="abs" style="left:40px;top:244px;width:390px;height:796px;display:flex;flex-direction:column">
<span class="lab">{{{{hero.kicker}}}}</span>
<span style="font-size:36px;line-height:1.1;margin-top:10px">{{{{voyage.place}}}}</span>
<span style="font-size:18px;line-height:1.4;color:{INK};margin-top:8px">{{{{voyage.title}}}}</span>
<span class="num" style="font-size:20px;margin-top:14px;white-space:pre">{{{{hero.stats}}}}</span>
{difficulty("display:flex;align-items:center;gap:14px;margin-top:12px")}
<nav aria-label="Voyages on offer" style="margin-top:auto;display:flex;flex-direction:column;gap:10px">
{voyage_chips("column", "width:100%", 76)}</nav>
</section>"""
    right = f"""<section aria-label="The squad" class="abs" style="left:1490px;top:244px;width:390px;height:796px;display:flex;flex-direction:column">
{r87.squads("margin-bottom:16px")}
{seats(390)}
{r87.sail("margin-top:auto;width:100%")}
</section>"""
    return window(sea + left + right,
                  f"Rev {REV} B - Flat chart: the same screen without the tilt - the whole map in its own colours over its edge-on view - interactive")


# ── Notes ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

def notes_board():
    rows = [
        ("Asked", "\"Delete B and C but still for the voyages UI, I do not know I dont like it ... give me some cool ass designed UI "
                  "for the voyage\" (Shombith, 2026-11-12, on Rev 87 and 88)."),
        ("What was dull", "Rev 87 and 88 muted the map and boxed it into a 660 column between two columns of plates. The one thing "
                          "that was to stay was the dullest thing on the screen."),
        ("Both options", "The map and its edge-on view are the screen, in the map's own colours. The voyage's name stands on it at "
                         "48 in A. The voyages are small chips with a sliver of their own water. The squad keeps Rev 88's cards and steppers. "
                         "One Lime: Set sail."),
        ("A Sea table (recommended)", "The map lies tilted in perspective and the edge-on view is its front face: one slab cut out of "
                                      "the HyperSea, with the course across its top and down its side. It says what the edge-on view is "
                                      "without a label."),
        ("B Flat chart", "The same screen without the tilt: the whole map, uncropped, over its edge-on view at the same width. "
                         "The name and numbers head the left column over the voyages. The safer build."),
        ("For the build", "A's tilt is one rotation of the map's rect on a perspective canvas, or the map drawn to a texture on a "
                          "tilted quad; the map itself is the game's own. B needs no tilt."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:340px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:26px">'
            f'<span style="font-size:36px">Rev {REV} - the Voyages screen with the HyperSea as the show</span>{items}</div>')
    return page(f"Rev {REV} - notes: what was dull, what both options do, the two options, the recommendation", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}A.dc.html": a_board, f"R{REV}B.dc.html": b_board, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
