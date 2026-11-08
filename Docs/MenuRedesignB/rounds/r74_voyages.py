#!/usr/bin/env python3
"""Rev 74 boards: Rev 71 C (Coverflow, picked) taken forward - the whole-galaxy HyperSea map on the left with the
edge-on view under it, the coverflow on the right, and four card faces that are not blue (Frogma tab "Voyages").

Shombith, 2026-11-08, on Rev 71: "Only one I like is C but still we should have the old whole galaxy view on the left
along with the bottom edge point view that we liked. Also the background of the cards tooo blue", then "delete all
keep C and make more".

Every board: the menu's ground (Rev 71), the HyperSea map column of the built window (section 119) opening on the whole
galaxy with its zoom ladder (Galaxy to System, Rev 46's seven renders; Region shows the picked voyage's course) and the
edge-on view at its foot, the coverflow of voyages on the right, the voyage's numbers, the squad (a hull opens the
vessel strip, one press picks; another squad never changes the voyage) and Set sail. What differs is the card face:
  C1 Graphite  the course in greys on a near-black card
  C2 Line      no picture: the route drawn as a line with its waypoints, the destination's name large
  C3 Place     a muted close-up of the destination, its name over it
  C4 Glass     clear glass over the ground, a small round course thumbnail and the words
plus a Compare board of the four faces side by side and the notes.

    python3 Docs/MenuRedesignB/rounds/r74_voyages.py <root> 74

Rev 72 and 73 went to other tabs while these were drawn; the round takes the next free number.
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "74"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r71", HERE / "r71_voyages.py")
r71 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r71)

REV, ROOT = int(_rev), r71.ROOT
blob, page, nav, tabrow, GROUND, squad, SAIL, TABS = r71.blob, r71.page, r71.nav, r71.tabrow, r71.GROUND, r71.squad, r71.SAIL, r71.TABS
VOYAGES = r71.VOYAGES

TIERS = [["Galaxy", "10,000 ly", blob("7559d7b030c6e949f7cde2d25ce5d9e7")],
         ["Arm", "1,000 ly", blob("fced8d3ce00c8f4c54ce4f49f52448c0")],
         ["Sector", "200 ly", blob("6138a0a6b01214f86b8a8496fe0140ba")],
         ["Region", "50 ly", ""],                       # the picked voyage's own course map
         ["Neighbourhood", "20 ly", blob("aa78b73bc805bdf9f8bc992bd5f12d74")],
         ["Cell", "2 ly", blob("a67a28db2832830827c20435ff87b2c6")],
         ["System", "1 ly", blob("2756a1ad4aee8ba111763fec387e64df")]]

# Waypoint glyphs for C2: the route as a line, its turns in the card's own space (0..1), from the course renders.
ROUTES = [[(0.12, 0.80), (0.40, 0.62), (0.88, 0.22)],
          [(0.10, 0.84), (0.26, 0.70), (0.38, 0.72), (0.52, 0.56), (0.62, 0.40), (0.74, 0.30), (0.90, 0.16)],
          [(0.14, 0.82), (0.50, 0.50), (0.86, 0.20)],
          [(0.12, 0.80), (0.46, 0.44), (0.88, 0.24)]]

DATA = (r71.DATA + "const TIERS = " + json.dumps(TIERS) + ";\nconst ROUTES = " + json.dumps(ROUTES) + ";\n")

MAP_VALS = """
  const t = TIERS[st.t];
  const mx = { img: t[2] || v.map, pos: t[2] ? '50% 50%' : v.pos, label: 'HyperSea map - ' + t[0] + ' - ' + t[1],
    alt: 'The HyperSea map at the ' + t[0] + ' view', edge: v.edge,
    zoomIn: () => this.setState({ t: Math.min(TIERS.length - 1, st.t + 1) }), zoomOut: () => this.setState({ t: Math.max(0, st.t - 1) }) };
  TIERS.forEach((x, i) => { mx['f' + i] = i === st.t ? '#E6E9FF' : '#434C89'; mx['w' + i] = i === st.t ? 26 : 12; });
  const flow = V.map((x, i) => { const d = i - st.v, a = Math.abs(d), r = ROUTES[i];
    return { title: x.title, place: x.place, sub: x.place + ' - ' + x.min, map: x.map, pos: x.pos, pick: pickV(i),
      pressed: d === 0 ? 'true' : 'false', z: 10 - a, op: a === 0 ? 1 : a === 1 ? 0.7 : 0.35,
      tf: 'translateX(' + (d * 250) + 'px) rotateY(' + (-d * 40) + 'deg) scale(' + (1 - Math.min(a, 2) * 0.18) + ')',
      edge: d === 0 ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89',
      line: r.map((p) => (p[0] * 300).toFixed(0) + ',' + (p[1] * 170).toFixed(0)).join(' '),
      dots: r.map((p) => 'M' + (p[0] * 300 - 4).toFixed(0) + ' ' + (p[1] * 170).toFixed(0) + 'l4 -4l4 4l-4 4z').join('') }; });
"""


def ladder():
    rects = "".join(f'<rect x="{{{{mx.x{i}}}}}" y="{i * 22}" width="{{{{mx.w{i}}}}}" height="3" fill="{{{{mx.f{i}}}}}"></rect>'
                    .replace("{{mx.x" + str(i) + "}}", "0") for i in range(len(TIERS)))
    return (f'<div class="abs" style="right:16px;top:16px;display:flex;flex-direction:column;align-items:flex-end;gap:10px">'
            f'<button class="pick sls" type="button" aria-label="Zoom in" onClick="{{{{mx.zoomIn}}}}" style="width:44px;height:44px;background:rgba(0,1,10,.84);box-shadow:inset 0 0 0 1px #434C89;font-family:\'Chakra Petch\';font-size:22px;color:#E6E9FF">+</button>'
            f'<svg aria-hidden="true" width="26" height="{(len(TIERS) - 1) * 22 + 3}" style="transform:scaleX(-1)">{rects}</svg>'
            f'<button class="pick sls" type="button" aria-label="Zoom out" onClick="{{{{mx.zoomOut}}}}" style="width:44px;height:44px;background:rgba(0,1,10,.84);box-shadow:inset 0 0 0 1px #434C89;font-family:\'Chakra Petch\';font-size:22px;color:#E6E9FF">-</button></div>')


def map_column():
    return f"""<figure class="abs sl" style="left:40px;top:172px;width:1060px;height:628px;margin:0;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89">
<img src="{{{{mx.img}}}}" alt="{{{{mx.alt}}}}" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{{{mx.pos}}}}">
<span class="abs glass sls cap" style="left:16px;top:16px;padding:10px 16px;font-size:14px;color:#E6E9FF">{{{{mx.label}}}}</span>
{ladder()}
</figure>
<img class="abs" src="{{{{mx.edge}}}}" alt="The course seen edge on: how it rises and falls through the disk" style="left:40px;top:816px;width:1060px;height:224px;object-fit:cover;object-position:40% 60%;background:#00010A">"""


# The four card faces. Each is the button's background, its picture (or drawing) and its words.
FACES = {
    "C1": ("Graphite", "the course in greys on a near-black card",
           '<img src="{{c.map}}" alt="" style="position:absolute;left:0;top:0;width:100%;height:220px;object-fit:cover;object-position:{{c.pos}};'
           'filter:grayscale(1) brightness(.72) contrast(1.15);-webkit-mask-image:linear-gradient(180deg,#000 55%,transparent);mask-image:linear-gradient(180deg,#000 55%,transparent)">',
           "#00010A"),
    "C2": ("Line", "no picture: the route drawn as a line with its waypoints, the destination large",
           '<svg aria-hidden="true" width="300" height="170" viewBox="0 0 300 170" style="position:absolute;left:30px;top:26px">'
           '<polyline points="{{c.line}}" fill="none" stroke="#E6E9FF" stroke-width="2.5" stroke-linejoin="round"></polyline>'
           '<path d="{{c.dots}}" fill="#E6E9FF"></path></svg>',
           "#00010A"),
    "C3": ("Place", "a muted close-up of the destination, its name over it",
           '<img src="{{c.map}}" alt="" style="position:absolute;left:-90%;top:-90%;width:280%;height:280%;object-fit:cover;object-position:{{c.pos}};'
           'filter:saturate(.25) brightness(.55);-webkit-mask-image:linear-gradient(180deg,#000 50%,transparent 92%);mask-image:linear-gradient(180deg,#000 50%,transparent 92%)">',
           "#00010A"),
    "C4": ("Glass", "clear glass over the ground, a small round course thumbnail and the words",
           '<img src="{{c.map}}" alt="" style="position:absolute;left:28px;top:28px;width:120px;height:120px;border-radius:50%;object-fit:cover;'
           'object-position:{{c.pos}};filter:grayscale(.6) brightness(.85)">',
           "rgba(0,1,10,.42);backdrop-filter:blur(16px);-webkit-backdrop-filter:blur(16px)"),
}


def card(face, w=360, h=320, big=False):
    _, _, art, bg = FACES[face]
    name = "{{c.place}}" if face in ("C2", "C3") else "{{c.title}}"
    size = 28 if face in ("C2", "C3") else 20
    if big:
        size += 4
    return (f'<button class="pick sl" type="button" aria-pressed="{{{{c.pressed}}}}" aria-label="{{{{c.title}}}}" onClick="{{{{c.pick}}}}" '
            f'style="position:absolute;left:{{{{c.x}}}};top:{{{{c.y}}}};width:{w}px;height:{h}px;overflow:hidden;box-sizing:border-box;'
            f'padding:0 24px 24px;display:flex;flex-direction:column;justify-content:flex-end;background:{bg};box-shadow:{{{{c.edge}}}};'
            f'transform:{{{{c.tf}}}};opacity:{{{{c.op}}}};z-index:{{{{c.z}}}};transition:transform 320ms cubic-bezier(.22,1,.36,1),opacity 320ms;'
            f'font-size:{size}px;line-height:1.2;color:#E6E9FF;text-align:left">{art}<span style="position:relative">{name}</span></button>')


def option(face):
    label, line, _, _ = FACES[face]
    stage = (f'<div class="abs" style="left:1120px;top:172px;width:760px;height:400px;perspective:1400px;overflow:hidden">'
             f'<sc-for list="{{{{flow}}}}" as="c" hint-placeholder-count="4">{card(face)}</sc-for></div>'
             '<span class="kc abs" style="left:1132px;top:340px">LB</span><span class="kc abs" style="left:1840px;top:340px">RB</span>')
    words = """<div class="abs" style="left:1120px;top:588px;width:760px;display:flex;flex-direction:column;gap:10px">
<span class="lab">{{voyage.kicker}}</span>
""" + r71.stats(28, 34) + "</div>"
    body = map_column() + stage + words + squad(1120, 700, 760, 92, 1120, 468, 760) + SAIL.format(x=1120, y=990, w=760)
    ev = MAP_VALS + "  flow.forEach((c) => { c.x = '200px'; c.y = '30px'; });\n"
    lg = (DATA + "class Component extends DCLogic {\nconstructor(p) { super(p); " + r71.START.replace("open: -1 };", "open: -1, t: 0 };")
          + " }\nrenderVals() {" + r71.VALS + ev + "  return { " + r71.RET + ", mx, flow };\n}\n}")
    rec = " (recommended)" if face == "C2" else ""
    title = f"Rev {REV} {face} - {label}: C with the whole-galaxy map and the edge view on the left; the cards {line}{rec} - interactive"
    return page(title, GROUND + nav(crystals=False) + tabrow(TABS, "Voyages") + body, lg)


def compare_board():
    cols = []
    for k, face in enumerate(FACES):
        label, line, _, _ = FACES[face]
        cols.append(f'<div class="abs" style="left:{56 + k * 460}px;top:170px;width:420px;display:flex;flex-direction:column;gap:12px">'
                    f'<span style="font-size:28px">{face} {label}</span><span style="font-size:15px;line-height:1.45;color:#747BAD">{line}</span></div>'
                    f'<div class="abs" style="left:{56 + k * 460}px;top:300px;width:400px;height:360px">'
                    f'<sc-for list="{{{{one}}}}" as="c" hint-placeholder-count="1">{card(face, 400, 360, True)}</sc-for></div>')
    body = (GROUND + f'<span class="abs" style="left:56px;top:110px;font-size:36px">Rev {REV} - the four card faces, the same voyage</span>'
            + "".join(cols)
            + '<span class="abs" style="left:56px;top:720px;width:1800px;font-size:18px;line-height:1.5;color:#E6E9FF">'
              'Every face sits on near-black or clear glass, so the card no longer reads blue; the course picture is greyed, '
              'drawn as a line, muted to the place, or kept small.</span>')
    lg = (DATA + "class Component extends DCLogic {\nconstructor(p) { super(p); " + r71.START.replace("open: -1 };", "open: -1, t: 0 };")
          + " }\nrenderVals() {" + r71.VALS + MAP_VALS
          + "  const one = [Object.assign({}, flow[1], { x: '0px', y: '0px', tf: 'none', op: 1, edge: 'inset 0 0 0 1px #434C89', pick: () => {} })];\n"
          + "  return { one };\n}\n}")
    return page(f"Rev {REV} - the four card faces side by side on the same voyage: Graphite, Line, Place, Glass", body, lg)


def notes_board():
    blocks = [
        ("Picked", "Rev 71 C, the Coverflow (Shombith, 2026-11-08): \"Only one I like is C but still we should have the old whole galaxy view on the left along with the bottom edge point view that we liked. Also the background of the cards tooo blue\", then \"delete all keep C and make more\"."),
        ("What every option keeps", "The menu's ground. On the left the HyperSea map column of the built window: it opens on the whole galaxy, + and - (and the ladder) zoom from Galaxy to System, Region shows the picked voyage's course, and the edge-on view sits at its foot. On the right C's coverflow, the voyage's numbers, the squad and Set sail."),
        ("The four card faces", "C1 Graphite: the course in greys. C2 Line: no picture, the route as a drawn line with its waypoints and the destination large. C3 Place: a muted close-up of the destination. C4 Glass: clear glass, a small round course thumbnail. Every card's ground is near-black or glass, never a blue panel."),
        ("Recommended: C2", "The map column already shows the course in colour; the card only has to say which voyage it is, and a drawn line plus the place reads at a glance from across the room, with nothing blue in it. C1 is the closest to C as picked."),
        ("The two fixes", "A squad hull opens the vessel strip on its seat, one press picks. Another squad never changes the voyage on screen (Garrett's voyage rules to change, section 147)."),
        ("Off the canvas", "Rev 71's A, B, D, E, F, its Quests window and its notes (copies in Docs/MenuRedesignB/board). The Quests window comes back in this look once a card face is picked."),
    ]
    cells = "".join(f'<div style="display:flex;flex-direction:column;gap:10px"><span class="lab">{h}</span>'
                    f'<span style="font-size:18px;line-height:1.5">{t}</span></div>' for h, t in blocks)
    body = (f'<span class="abs" style="left:56px;top:44px;font-size:36px">Rev {REV} - notes</span>'
            f'<div class="abs" style="left:56px;top:120px;width:1808px;display:grid;grid-template-columns:1fr 1fr;gap:34px 64px">{cells}</div>')
    return page(f"Rev {REV} - notes: C taken forward, what every option keeps, the four card faces, the recommendation",
                body, "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}Compare.dc.html": compare_board, f"R{REV}C1.dc.html": lambda: option("C1"),
          f"R{REV}C2.dc.html": lambda: option("C2"), f"R{REV}C3.dc.html": lambda: option("C3"),
          f"R{REV}C4.dc.html": lambda: option("C4"), f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
