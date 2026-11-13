#!/usr/bin/env python3
"""Rev 88 boards: Rev 87 A "Slide" taken forward - smaller top texts, and every screen of the Voyage window in it
(Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md section 169).

Shombith, 2026-11-12, on Rev 87: "I like As direction but the top texts are too big, can you show all the screens
inside the voyage tab".

The top texts step down the type scale: the window's title 36 to 22, the hub row 18 to 15, the tabs 22 to 15. Every
screen keeps A's order - small cards on the left, the picked one slid open; the picture in the middle; one column on
the right that ends in the screen's one Lime button:

  Voyages    A as picked, with the Ark escort as the fifth card
  Escort     the Ark escort card open: its difficulty stepper where a voyage has its numbers
  Saga       the two parts as cards (Tandava open, The Severing locked), the part's picture, the squad, Set sail
  Quests     the five quests as cards with their state, the quest's own card art, Claim or Play quest
  Episodes   Episode 2, coming soon, over the galaxy
  Notes

The tabs are links: each opens its board.

    python3 Docs/MenuRedesignB/rounds/r88_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "88"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r87", HERE / "r87_voyages.py")
r87 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r87)
r70 = r87.r82.r71.r70 if hasattr(r87.r82, "r71") else None
if r70 is None:
    r70 = r87.r79.r71.r70

REV, ROOT, page, nav, GROUND = int(_rev), r87.ROOT, r87.page, r87.nav, r87.GROUND
JADE, INK, DIM, LINE, PLATE = r87.JADE, r87.INK, r87.DIM, r87.LINE, r87.PLATE
blob, LOCK, QUESTS = r70.blob, r70.LOCK, r70.QUESTS
TANDAVA = blob("ea5244253b797934bf2988bdc1260ec7")
GALAXY = blob("7559d7b030c6e949f7cde2d25ce5d9e7")
TANDAVA_FORMS = "Great Serpent - Many-Headed Serpent - Lord of the Dance - Antlion"
FILES = {"Voyages": "Voyages", "Saga": "Saga", "Quests": "Quests", "Episodes": "Episodes"}

# ── The top of the window, one step down the type scale ───────────────────────────────────────────────────────────────

HUB = ("TOY BOX", "ARCADE", "ARENA", "VOYAGE")


def header():
    items = "".join(
        f'<span class="cap" style="font-size:15px;color:{INK if h == "VOYAGE" else DIM};padding-bottom:6px;'
        f'border-bottom:2px solid {INK if h == "VOYAGE" else "rgba(0,0,0,0)"}">{h}</span>' for h in HUB)
    return ('<div class="abs" style="left:64px;right:64px;top:100px;height:48px;display:flex;align-items:center;'
            'justify-content:space-between;z-index:4"><span style="font-size:22px;width:360px">Voyage</span>'
            f'<div style="display:flex;align-items:center;gap:26px"><span class="kc">LB</span>{items}<span class="kc">RB</span></div>'
            '<div style="width:360px;display:flex;justify-content:flex-end"><button class="btn sm def" type="button">'
            '<span class="kc">B</span>Back</button></div></div>')


def tabs(lit):
    """The window's four tabs, each a link to its board; the shown one light and underlined; Episodes says Soon."""
    out = []
    for name in FILES:
        on = name == lit
        soon = '<span style="font-size:14px;margin-left:8px;letter-spacing:.12em">Soon</span>' if name == "Episodes" else ""
        out.append(f'<a class="cap" href="R{REV}{FILES[name]}.dc.html" aria-current="{"page" if on else "false"}" '
                   f'style="font-size:15px;text-decoration:none;color:{INK if on else DIM};padding-bottom:8px;'
                   f'border-bottom:2px solid {INK if on else "rgba(0,0,0,0)"}">{name}{soon}</a>')
    return f'<nav aria-label="Voyage window tabs" class="abs" style="left:64px;top:186px;display:flex;gap:34px;align-items:flex-end">{"".join(out)}</nav>'


def window(lit, body, title, logic):
    return page(title, GROUND + nav(crystals=False) + header() + tabs(lit) + body, logic)


# ── The squad column (Voyages, Escort, Saga): a row per seat, a pressed row opens its four steppers ───────────────────

def squad_rows(foot):
    return f"""<section aria-label="The squad" class="abs" style="left:1320px;top:244px;width:560px;height:796px;display:flex;flex-direction:column">
{r87.squads()}
<div style="display:flex;flex-direction:column;gap:12px;margin-top:20px">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><div class="sl" style="opacity:{{{{s.op}}}};background:{PLATE};box-shadow:{{{{s.edge}}}}">
<button class="pick" type="button" aria-pressed="{{{{s.pressed}}}}" aria-label="{{{{s.alt}}}}" onClick="{{{{s.pick}}}}" style="width:100%;height:158px;display:grid;grid-template-columns:120px 1fr 200px;gap:16px;align-items:center;padding:0 18px;box-sizing:border-box">
<img src="{{{{s.face}}}}" alt="" style="width:120px;height:150px;align-self:end;object-fit:contain;object-position:50% 100%">
<span style="display:flex;flex-direction:column;gap:6px"><span style="font-size:22px">{{{{s.name}}}}</span><span style="font-size:15px;color:{INK}">{{{{s.line1}}}}</span><span style="font-size:15px;color:{DIM}">{{{{s.line2}}}}</span></span>
<img class="hull" src="{{{{s.hull}}}}" alt="" style="width:200px;height:110px"></button>
<sc-if value="{{{{s.open}}}}" hint-placeholder-val="{{{{ false }}}}">{r87.steppers()}</sc-if>
</div></sc-for></div>
{foot}
</section>"""


SAIL = r87.sail("margin-top:auto;width:100%")


def closed_card(img, pos, head, meta, extra=""):
    """A small card: its picture on the right, its name and one line; a press slides it open."""
    return (f'<button class="pick sls" type="button" aria-pressed="false" onClick="{{{{r.pick}}}}" style="position:relative;height:84px;overflow:hidden;'
            f'background:#00010A;box-shadow:inset 0 0 0 1px {LINE}"><img src="{img}" alt="" style="position:absolute;right:0;top:0;width:300px;'
            f'height:100%;object-fit:cover;object-position:{pos};opacity:{{{{r.art}}}}">'
            '<span style="position:absolute;inset:0;background:linear-gradient(90deg,#00010A 40%,rgba(0,1,10,0))"></span>'
            f'<span style="position:absolute;left:22px;top:16px;font-size:22px;color:{INK}">{head}</span>'
            f'<span class="num" style="position:absolute;left:22px;top:50px;font-size:15px;color:{DIM}">{meta}</span>{extra}</button>')


def card_list(label, listname, closed, opened, count):
    return (f'<nav aria-label="{label}" class="abs" style="left:40px;top:244px;width:540px;height:796px;display:flex;flex-direction:column;gap:10px">'
            f'<sc-for list="{{{{{listname}}}}}" as="r" hint-placeholder-count="{count}"><div style="flex:{{{{r.grow}}}};display:flex;flex-direction:column;min-height:0">'
            f'<sc-if value="{{{{r.off}}}}" hint-placeholder-val="{{{{ true }}}}">{closed}</sc-if>'
            f'<sc-if value="{{{{r.on}}}}" hint-placeholder-val="{{{{ false }}}}"><section class="sl" aria-label="{{{{r.aria}}}}" style="flex:1;box-sizing:border-box;'
            f'padding:26px 30px 28px 33px;display:flex;flex-direction:column;background:{PLATE};box-shadow:inset 3px 0 0 {JADE}">{opened}</section></sc-if>'
            '</div></sc-for></nav>')


def picture(src, pos, alt, x=620, w=660, inner="", filt="saturate(.45) brightness(.82)"):
    return (f'<figure class="abs sl" style="left:{x}px;top:244px;width:{w}px;height:796px;margin:0;overflow:hidden;background:#00010A">'
            f'<img src="{src}" alt="{alt}" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{pos};filter:{filt}">{inner}</figure>')


# ── Voyages, and the Ark escort ───────────────────────────────────────────────────────────────────────────────────────

# The escort is the fifth card: the Ark's own run, with a difficulty where a voyage has its intensity.
ESCORT = ("const ESC = { n: 5, esc: true, title: 'Escort the Ark from Tumult to Tollmoor', place: 'Ark escort', min: V[0].min, ly: V[0].ly, i: 0, "
          "crew: '', line: 'Fly beside the Ark and keep it standing.', map: V[0].map, pos: V[0].pos, edge: V[0].edge };\n"
          "const ALL = V.concat([ESC]);\n")


def voyage_logic(start_v):
    lg = r87.LOGIC
    a = lg.index("class Component extends DCLogic")
    lg = lg[:a] + ESCORT + lg[a:]
    rep = [
        ("this.state = { v: 1, sq: 0,", "this.state = { v: %d, d: 2, sq: 0," % start_v),
        ("v = V[st.v],", "v = ALL[st.v],"),
        ("const vlist = V.map((x, i) => ({ n: '0' + x.n,", "const vlist = ALL.map((x, i) => ({ esc: !!x.esc, voy: !x.esc, art: 0.85, aria: x.title,\n"
         "    lab: x.esc ? 'The Ark' : '0' + x.n + ' - Brought by ' + x.crew, head: '0' + x.n + '  ' + x.place,\n"
         "    diff: String(st.d), down: () => this.setState({ d: Math.max(1, st.d - 1) }), up: () => this.setState({ d: Math.min(4, st.d + 1) }),\n"
         "    n: '0' + x.n,"),
        ("meta: x.min + ' - Intensity ' + x.i,", "meta: x.esc ? x.min + ' - Difficulty ' + st.d : x.min + ' - Intensity ' + x.i,"),
    ]
    for old, new in rep:
        assert old in lg, old
        lg = lg.replace(old, new, 1)
    return lg


def voyage_cards():
    cell = lambda lab, val: (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">{lab}</span>'
                             f'<span class="num" style="font-size:28px">{val}</span></div>')
    arrow = lambda fn, lab, svg: (f'<button class="pick" type="button" aria-label="{lab}" onClick="{{{{r.{fn}}}}}" '
                                  f'style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:{INK}">{svg}</button>')
    opened = f"""<span class="lab">{{{{r.lab}}}}</span>
<span style="font-size:36px;line-height:1.15;margin-top:12px">{{{{r.place}}}}</span>
<span style="font-size:22px;line-height:1.3;margin-top:8px">{{{{r.title}}}}</span>
<span style="font-size:18px;line-height:1.5;color:{INK};margin-top:12px">{{{{r.line}}}}</span>
<div style="margin-top:auto;display:grid;grid-template-columns:repeat(3,1fr);gap:16px;align-items:end">{cell("Distance", "{{r.ly}}")}{cell("Sail", "{{r.min}}")}
<div><sc-if value="{{{{r.voy}}}}" hint-placeholder-val="{{{{ true }}}}">{cell("Intensity", "{{r.i}}")}</sc-if>
<sc-if value="{{{{r.esc}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="display:flex;flex-direction:column;gap:2px"><span class="lab">Difficulty</span>
<div class="sls" style="height:44px;display:flex;align-items:center;justify-content:space-between;background:#00010A;box-shadow:inset 0 0 0 1px {LINE}">{arrow("down", "Easier", r87.CHEV_L)}<span class="num" style="font-size:22px">{{{{r.diff}}}}</span>{arrow("up", "Harder", r87.CHEV_R)}</div></div></sc-if></div></div>"""
    return card_list("Voyages on offer", "vlist", closed_card("{{r.map}}", "{{r.pos}}", "{{r.head}}", "{{r.meta}}"), opened, 5)


def voyages_board():
    return window("Voyages", voyage_cards() + r87.sea() + squad_rows(SAIL),
                  f"Rev {REV} Voyages - A with smaller top texts: the voyages and the Ark escort as cards, the picked one open; the squad as rows - interactive",
                  voyage_logic(1))


def escort_board():
    return window("Voyages", voyage_cards() + r87.sea() + squad_rows(SAIL),
                  f"Rev {REV} Escort - the Ark escort card open: its difficulty stepper where a voyage has its intensity - interactive",
                  voyage_logic(4))


# ── Saga ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

def saga_board():
    lock = LOCK.replace('width="14" height="16"', 'width="16" height="18"')
    closed = closed_card(TANDAVA, "{{r.pos}}", "{{r.head}}", "{{r.meta}}")
    opened = f"""<span class="lab">{{{{r.lab}}}}</span>
<span style="font-size:36px;line-height:1.15;margin-top:12px">{{{{r.name}}}}</span>
<span style="font-size:18px;line-height:1.5;color:{INK};margin-top:12px">{{{{r.line}}}}</span>
<div style="margin-top:auto;display:flex;flex-direction:column;gap:8px"><span class="lab">{{{{r.formsLab}}}}</span>
<sc-for list="{{{{r.forms}}}}" as="f" hint-placeholder-count="4"><span style="font-size:18px">{{{{f.name}}}}</span></sc-for></div>"""
    locked = (f'<span class="btn" style="margin-top:auto;width:100%;background:rgba(0,4,31,.6);color:{DIM};box-shadow:inset 0 0 0 1px {LINE}">'
              f'{lock}After Tandava</span>')
    foot = (f'<sc-if value="{{{{saga.open}}}}" hint-placeholder-val="{{{{ true }}}}">{SAIL}</sc-if>'
            f'<sc-if value="{{{{saga.locked}}}}" hint-placeholder-val="{{{{ false }}}}">{locked}</sc-if>')
    lg = r87.LOGIC.replace("  return { voyage, vlist, tabs, presets, seatList, tray };", """  const k = st.k || 0;
  const PARTS = [{ name: 'Tandava', line: 'Two parts. The second opens when the first is done.', forms: """ + json.dumps(TANDAVA_FORMS.split(" - ")) + """, pos: '50% 40%' },
    { name: 'The Severing', line: 'The Severing opens when Tandava is done.', forms: [], pos: '50% 80%' }];
  const parts = PARTS.map((p, i) => ({ name: p.name, line: p.line, pos: p.pos, aria: p.name, head: '0' + (i + 1) + '  ' + p.name,
    meta: i === 0 ? 'Part 1 of 2' : 'After Tandava', lab: 'Saga - part ' + (i + 1) + ' of 2', art: i === 0 ? 0.85 : 0.4,
    formsLab: p.forms.length ? 'Forms' : '', forms: p.forms.map((f) => ({ name: f })),
    on: i === k, off: i !== k, grow: i === k ? '1 1 0' : '0 0 auto', pick: () => this.setState({ k: i }) }));
  const saga = { open: k === 0, locked: k === 1, pos: PARTS[k].pos, name: PARTS[k].name };
  return { voyage, vlist, tabs, presets, seatList, tray, parts, saga };""")
    assert "parts, saga" in lg
    art = picture(TANDAVA, "{{saga.pos}}", "{{saga.name}}")
    return window("Saga", card_list("The saga's parts", "parts", closed, opened, 2) + art + squad_rows(foot),
                  f"Rev {REV} Saga - the two parts as cards, Tandava open with its forms, The Severing locked; the squad and Set sail - interactive", lg)


# ── Quests ────────────────────────────────────────────────────────────────────────────────────────────────────────────

def quests_board():
    closed = closed_card("{{r.img}}", "50% 50%", "{{r.head}}", "{{r.meta}}")
    opened = f"""<span class="lab">{{{{r.lab}}}}</span>
<span style="font-size:36px;line-height:1.15;margin-top:12px">{{{{r.name}}}}</span>
<span style="font-size:18px;line-height:1.5;color:{INK};margin-top:12px">{{{{r.goal}}}}</span>
<span class="cap" style="margin-top:auto;font-size:15px;color:{INK}">{{{{r.meta}}}}</span>"""
    art = picture("{{focus.img}}", "50% 50%", "{{focus.name}}", w=1260, filt="saturate(.7) brightness(.85)")
    # The episode and the one button stand on a plate at the picture's foot; the sliver is on a layer of its own.
    right = f"""<section aria-label="Episode 1" class="abs" style="left:1290px;top:756px;width:560px;height:254px">
<div class="abs sl" style="inset:0;background:{PLATE}"></div>
<div class="abs" style="left:28px;right:28px;top:24px;bottom:0;display:flex;flex-direction:column">
<span class="lab">Episode 1</span>
<span style="font-size:28px;line-height:1.15;margin-top:8px">The Hypersea</span>
<div style="margin-top:16px;display:flex;flex-direction:column;gap:8px"><span class="num" style="font-size:15px;color:{INK}">{{{{progress}}}}</span>
<div style="height:8px;background:#222645"><div style="height:8px;width:{{{{fill}}}};background:{INK}"></div></div></div>
</div>
<sc-if value="{{{{focus.claim}}}}" hint-placeholder-val="{{{{ false }}}}"><button class="btn cta abs" type="button" onClick="{{{{focus.act}}}}" style="left:0;bottom:0;width:100%"><span class="kc">A</span>Claim</button></sc-if>
<sc-if value="{{{{focus.play}}}}" hint-placeholder-val="{{{{ true }}}}"><button class="btn cta abs" type="button" style="left:0;bottom:0;width:100%"><span class="kc">A</span>Play quest</button></sc-if>
<sc-if value="{{{{focus.wait}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="btn abs" style="left:0;bottom:0;width:100%;background:#00010A;color:{DIM};box-shadow:inset 0 0 0 1px {LINE}">{{{{focus.waitLine}}}}</span></sc-if>
</section>"""
    lg = ("const Q = " + json.dumps(QUESTS) + """;
class Component extends DCLogic {
constructor(p) { super(p); this.state = { states: Q.map((q) => q.state), sel: 2 }; }
renderVals() {
  const st = this.state, s = st.states;
  const line = (x, i) => x === 'claimed' ? 'Claimed' : x === 'done' ? 'Done - claim it' : x === 'open' ? 'Next to play' : 'After ' + Q[i - 1].name;
  const quests = Q.map((q, i) => ({ name: q.name, goal: q.goal, img: q.art, aria: q.name, head: q.n + '  ' + q.name, meta: line(s[i], i),
    lab: 'Quest ' + q.n + ' of 0' + Q.length, art: s[i] === 'locked' ? 0.35 : 0.85,
    on: i === st.sel, off: i !== st.sel, grow: i === st.sel ? '1 1 0' : '0 0 auto', pick: () => this.setState({ sel: i }) }));
  const k = s[st.sel], cur = Q[st.sel], claimed = s.filter((x) => x === 'claimed').length;
  const act = () => { if (k === 'done') { const n = s.slice(); n[st.sel] = 'claimed'; if (n[st.sel + 1] === 'locked') n[st.sel + 1] = 'open'; this.setState({ states: n }); } };
  const focus = { name: cur.name, img: cur.art, claim: k === 'done', play: k === 'open', wait: k === 'locked' || k === 'claimed',
    waitLine: k === 'claimed' ? 'Claimed' : line(k, st.sel), act };
  return { quests, focus, progress: claimed + ' of ' + Q.length + ' claimed', fill: Math.round(100 * claimed / Q.length) + '%' };
}
}""")
    return window("Quests", card_list("Episode 1's quests", "quests", closed, opened, 5) + art + right,
                  f"Rev {REV} Quests - the five quests as cards with their state, the picked one open over its game's art; Claim or Play quest - interactive", lg)


# ── Episodes ──────────────────────────────────────────────────────────────────────────────────────────────────────────

def episodes_board():
    lock = LOCK.replace('width="14" height="16"', 'width="24" height="28"')
    left = f"""<section class="abs sl" aria-label="Episode 2" style="left:40px;top:244px;width:540px;height:796px;box-sizing:border-box;padding:26px 30px 28px 33px;display:flex;flex-direction:column;background:{PLATE};box-shadow:inset 3px 0 0 {LINE}">
<span class="lab">Episodes</span>
<span style="display:flex;align-items:center;gap:14px;font-size:36px;line-height:1.15;margin-top:12px">{lock}Episode 2</span>
<span class="cap" style="font-size:18px;color:{INK};margin-top:12px">Coming soon</span>
<span style="font-size:18px;line-height:1.5;color:{INK};margin-top:12px">New episodes arrive here. Voyages and the Saga are open now.</span>
<a class="btn def" href="R{REV}Voyages.dc.html" style="margin-top:auto;width:100%"><span class="kc">LB</span>Back to Voyages</a>
</section>"""
    art = picture(GALAXY, "50% 50%", "The galaxy", x=620, w=1260, filt="saturate(.3) brightness(.85)")
    return window("Episodes", left + art, f"Rev {REV} Episodes - Episode 2, coming soon, over the galaxy; one way back",
                  "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}")


# ── Notes ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

def notes_board():
    rows = [
        ("Asked", "\"I like As direction but the top texts are too big, can you show all the screens inside the voyage tab\" "
                  "(Shombith, 2026-11-12, on Rev 87)."),
        ("The top texts", "One step down the type scale: the window's title 36 to 22, the hub row 18 to 15, the tabs 22 to 15."),
        ("One order on every screen", "Small cards on the left, the picked one slid open. The picture in the middle. One column on "
                                      "the right that ends in the screen's one Lime button."),
        ("Voyages, Escort", "A as picked. The Ark escort is the fifth card; open, it has a difficulty stepper where a voyage has its intensity."),
        ("Saga", "Tandava and The Severing as cards. Tandava open shows its forms; The Severing is locked and Set sail says After Tandava."),
        ("Quests", "The five quests as cards with their state. The picked one is open over its game's art; the button is Claim or Play quest."),
        ("Episodes", "Episode 2, coming soon, over the galaxy, with one way back."),
        ("Not drawn", "A voyage that is under way, and the story beats. Say if you want them in this look."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:340px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:26px">'
            f'<span style="font-size:36px">Rev {REV} - A taken forward: smaller top texts, every screen of the Voyage window</span>{items}</div>')
    return page(f"Rev {REV} - notes: the top texts, the one order, each screen, what is not drawn", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}Voyages.dc.html": voyages_board, f"R{REV}Escort.dc.html": escort_board, f"R{REV}Saga.dc.html": saga_board,
          f"R{REV}Quests.dc.html": quests_board, f"R{REV}Episodes.dc.html": episodes_board, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
