#!/usr/bin/env python3
"""Rev 90 boards: Saga, Quests and Episodes as the Sea table (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md
section 171).

Shombith, 2026-11-12, on Rev 89: "Go with A, redo saga quests and episodes to match".

Every tab is the same slab: a picture lying tilted in perspective, and under its near edge a front face that shows
the same thing as a line. On Voyages the front face is the course seen edge on. Here:

  Saga       the part's arena on the table; the front face is the fight's forms in order
  Quests     the HyperSea on the table (Episode 1 is "The Hypersea"); the front face is the episode's five quests as
             a track; the picked quest's own card stands in the right column with Claim or Play quest
  Episodes   the galaxy, dimmed; the front face says Episode 2 is coming

The chips, the name over the table, the right column and the one Lime button are Rev 89 A's. The tabs are links;
Voyages goes to R89A.

    python3 Docs/MenuRedesignB/rounds/r90_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "90"
sys.argv = [sys.argv[0], _root, "89"]          # Rev 89's own files keep their names; this round's are R90*
_spec = importlib.util.spec_from_file_location("r89", HERE / "r89_voyages.py")
r89 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r89)
r88, r87 = r89.r88, r89.r87

REV, ROOT, page = int(_rev), r89.ROOT, r89.page
JADE, INK, DIM, LINE, GLASS = r89.JADE, r89.INK, r89.DIM, r89.LINE, r89.GLASS
TANDAVA, GALAXY, LOCK, QUESTS = r88.TANDAVA, r88.GALAXY, r88.LOCK, r88.QUESTS
FORMS = r88.TANDAVA_FORMS.split(" - ")
W, X = 1020, 380
TOP_H, FACE_H = round(W / r89.MAP_ASPECT), round(W / r89.EDGE_ASPECT)


def slab(src, alt, face, pos="50% 50%", filt="none", x=X, zoom=100):
    """The table: a picture tilted back about its near edge, and a front face under that edge."""
    off = (zoom - 100) / 2
    return f"""<div class="abs" aria-hidden="true" style="left:{x - 110}px;top:890px;width:1240px;height:190px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(230,233,255,.10),rgba(230,233,255,0))"></div>
<div class="abs" style="left:{x}px;top:214px;width:{W}px;height:800px;perspective:1500px;perspective-origin:50% 0%">
<figure style="position:absolute;left:0;bottom:{FACE_H}px;width:{W}px;height:{TOP_H}px;margin:0;transform-origin:50% 100%;transform:rotateX(56deg);overflow:hidden;-webkit-mask-image:linear-gradient(0deg,#000 52%,transparent 96%);mask-image:linear-gradient(0deg,#000 52%,transparent 96%)">
<img src="{src}" alt="{alt}" style="position:absolute;left:-{off}%;top:-{off}%;width:{zoom}%;height:{zoom}%;object-fit:cover;object-position:{pos};filter:{filt}"></figure>
<div style="position:absolute;left:0;bottom:0;width:{W}px;height:{FACE_H}px;box-sizing:border-box;background:linear-gradient(180deg,#00041F 0,#00010A 100%);box-shadow:0 -1px 0 {INK}">{face}</div>
</div>"""


def name_over(kicker, name, line, extra="", x=X):
    return f"""<header class="abs" style="left:{x}px;top:238px;width:{W}px;display:flex;flex-direction:column;align-items:center;gap:8px;z-index:3">
<span class="lab">{kicker}</span>
<span style="font-size:48px;line-height:1.05">{name}</span>
<span style="font-size:18px;color:{INK}">{line}</span>{extra}
</header>"""


def chips(listname, label, count):
    return (f'<nav aria-label="{label}" class="abs" style="left:40px;top:244px;width:310px;display:flex;flex-direction:column;gap:10px">'
            + r89.voyage_chips("column", "width:100%", 76).replace('list="{{vlist}}"', f'list="{{{{{listname}}}}}"')
            .replace('hint-placeholder-count="5"', f'hint-placeholder-count="{count}"') + '</nav>')


def track(listname, count):
    """The front face as a line: one station per item in order, the picked one under a Jade line."""
    return (f'<div style="position:absolute;left:40px;right:40px;top:78px;height:1px;background:{LINE}"></div>'
            f'<div style="position:absolute;left:40px;right:40px;top:30px;display:grid;grid-template-columns:repeat({count},1fr)">'
            f'<sc-for list="{{{{{listname}}}}}" as="n" hint-placeholder-count="{count}"><div style="display:flex;flex-direction:column;align-items:center;gap:8px">'
            '<span class="lab">{{n.k}}</span>'
            '<span style="width:14px;height:14px;box-sizing:border-box;transform:rotate(45deg);background:{{n.fill}};border:2px solid {{n.ring}};margin:14px 0 8px"></span>'
            '<span style="font-size:18px;color:{{n.ink}};padding-bottom:6px;border-bottom:2px solid {{n.under}}">{{n.name}}</span>'
            f'<span style="font-size:14px;color:{DIM}">{{{{n.note}}}}</span></div></sc-for></div>')


CHIP_JS = """ink: on ? '#E6E9FF' : '#747BAD', plate: on ? 'rgba(0,4,31,.94)' : 'rgba(0,4,31,.62)',
      edge: on ? 'inset 3px 0 0 #00D4FF' : 'inset 0 0 0 1px rgba(67,76,137,.7)', pressed: on ? 'true' : 'false'"""


# ── Saga ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

def saga_board():
    lock = LOCK.replace('width="14" height="16"', 'width="16" height="18"')
    lg = r89.logic().replace("  return { voyage, vlist, tabs, presets, seatList, tray, hero };", """  const k = st.k || 0;
  const PARTS = [{ name: 'Tandava', line: 'Two parts. The second opens when the first is done.', pos: '50% 40%' },
    { name: 'The Severing', line: 'The Severing opens when Tandava is done.', pos: '50% 80%' }];
  const parts = PARTS.map((p, i) => { const on = i === k; return { head: '0' + (i + 1) + '  ' + p.name, meta: i === 0 ? 'Part 1 of 2' : 'After Tandava',
      map: """ + json.dumps(TANDAVA) + """, pos: p.pos, thumb: i === 1 ? 0.3 : on ? 1 : 0.5, pick: () => this.setState({ k: i }),
      """ + CHIP_JS + """ }; });
  const saga = { open: k === 0, locked: k === 1, pos: PARTS[k].pos, name: PARTS[k].name, line: PARTS[k].line, kicker: 'Saga - part ' + (k + 1) + ' of 2',
    look: k === 0 ? 'none' : 'saturate(.2) brightness(.5)' };
  const forms = """ + json.dumps(FORMS) + """.map((f, i) => ({ k: 'Form ' + (i + 1), name: f, note: '', fill: 'rgba(0,0,0,0)', ring: '#E6E9FF', ink: '#E6E9FF', under: 'rgba(0,0,0,0)' }));
  return { voyage, vlist, tabs, presets, seatList, tray, hero, parts, saga, forms };""")
    assert "parts, saga, forms" in lg
    face = (f'<sc-if value="{{{{saga.open}}}}" hint-placeholder-val="{{{{ true }}}}">{track("forms", 4)}</sc-if>'
            f'<sc-if value="{{{{saga.locked}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="position:absolute;inset:0;display:flex;align-items:center;'
            f'justify-content:center;gap:14px;font-size:22px;color:{INK}">{lock}Opens when Tandava is done</div></sc-if>')
    locked = (f'<span class="btn" style="margin-top:auto;width:100%;background:rgba(0,4,31,.6);color:{DIM};box-shadow:inset 0 0 0 1px {LINE}">'
              f'{lock}After Tandava</span>')
    right = f"""<section aria-label="The squad" class="abs" style="left:1430px;top:244px;width:450px;height:796px;display:flex;flex-direction:column">
{r87.squads("margin-bottom:16px")}
{r89.seats(450)}
<sc-if value="{{{{saga.open}}}}" hint-placeholder-val="{{{{ true }}}}">{r87.sail("margin-top:auto;width:100%")}</sc-if>
<sc-if value="{{{{saga.locked}}}}" hint-placeholder-val="{{{{ false }}}}">{locked}</sc-if>
</section>"""
    body = (slab(TANDAVA, "{{saga.name}}", face, pos="{{saga.pos}}", filt="{{saga.look}}")
            + name_over("{{saga.kicker}}", "{{saga.name}}", "{{saga.line}}") + chips("parts", "The saga's parts", 2) + right)
    return r89.window(body, f"Rev {REV} Saga - the part's arena on the table, its forms in order on the front face; The Severing locked - interactive",
                      lit="Saga", lg=lg)


# ── Quests ────────────────────────────────────────────────────────────────────────────────────────────────────────────

def quests_board():
    lg = ("const Q = " + json.dumps(QUESTS) + """;
class Component extends DCLogic {
constructor(p) { super(p); this.state = { states: Q.map((q) => q.state), sel: 2 }; }
renderVals() {
  const st = this.state, s = st.states;
  const line = (x, i) => x === 'claimed' ? 'Claimed' : x === 'done' ? 'Done - claim it' : x === 'open' ? 'Next to play' : 'After ' + Q[i - 1].name;
  const quests = Q.map((q, i) => { const on = i === st.sel; return { head: q.n + '  ' + q.name, meta: line(s[i], i), map: q.art, pos: '50% 50%',
      thumb: s[i] === 'locked' ? 0.3 : on ? 1 : 0.6, pick: () => this.setState({ sel: i }),
      """ + CHIP_JS + """ }; });
  const nodes = Q.map((q, i) => ({ k: q.n, name: q.name, note: line(s[i], i),
    fill: s[i] === 'claimed' ? '#E6E9FF' : 'rgba(0,0,0,0)', ring: s[i] === 'claimed' ? '#E6E9FF' : s[i] === 'done' ? '#00D4FF' : s[i] === 'open' ? '#E6E9FF' : '#434C89',
    ink: s[i] === 'locked' ? '#747BAD' : '#E6E9FF', under: i === st.sel ? '#00D4FF' : 'rgba(0,0,0,0)' }));
  const k = s[st.sel], cur = Q[st.sel], claimed = s.filter((x) => x === 'claimed').length;
  const act = () => { if (k === 'done') { const n = s.slice(); n[st.sel] = 'claimed'; if (n[st.sel + 1] === 'locked') n[st.sel + 1] = 'open'; this.setState({ states: n }); } };
  const focus = { kicker: 'Quest ' + cur.n + ' of 0' + Q.length, name: cur.name, goal: cur.goal, img: cur.art, state: line(k, st.sel),
    claim: k === 'done', play: k === 'open', wait: k === 'locked' || k === 'claimed', act };
  return { quests, nodes, focus, progress: claimed + ' of ' + Q.length + ' claimed' };
}
}""")
    foot = "margin-top:auto;width:100%"
    right = f"""<section aria-label="The picked quest" class="abs" style="left:1430px;top:244px;width:450px;height:796px;display:flex;flex-direction:column">
<span class="lab">{{{{focus.kicker}}}}</span>
<figure class="sls" style="margin:14px 0 0;width:450px;height:299px;overflow:hidden;background:#00010A"><img src="{{{{focus.img}}}}" alt="{{{{focus.name}}}}" style="display:block;width:100%;height:100%;object-fit:cover"></figure>
<span style="font-size:36px;line-height:1.15;margin-top:20px">{{{{focus.name}}}}</span>
<span style="font-size:18px;line-height:1.5;color:{INK};margin-top:10px">{{{{focus.goal}}}}</span>
<span class="cap" style="font-size:15px;color:{INK};margin-top:14px">{{{{focus.state}}}}</span>
<sc-if value="{{{{focus.claim}}}}" hint-placeholder-val="{{{{ false }}}}"><button class="btn cta" type="button" onClick="{{{{focus.act}}}}" style="{foot}"><span class="kc">A</span>Claim</button></sc-if>
<sc-if value="{{{{focus.play}}}}" hint-placeholder-val="{{{{ true }}}}"><button class="btn cta" type="button" style="{foot}"><span class="kc">A</span>Play quest</button></sc-if>
<sc-if value="{{{{focus.wait}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="btn" style="{foot};background:rgba(0,4,31,.6);color:{DIM};box-shadow:inset 0 0 0 1px {LINE}">{{{{focus.state}}}}</span></sc-if>
</section>"""
    body = (slab(GALAXY, "The HyperSea", track("nodes", 5), pos="50% 56%", filt="saturate(1.3) brightness(1.5)", zoom=190)
            + name_over("Episode 1", "The Hypersea", "{{progress}}") + chips("quests", "Episode 1's quests", 5) + right)
    return r89.window(body, f"Rev {REV} Quests - the HyperSea on the table, the episode's five quests as a track on the front face; the picked quest's card, Claim or Play quest - interactive",
                      lit="Quests", lg=lg)


# ── Episodes ──────────────────────────────────────────────────────────────────────────────────────────────────────────

def episodes_board():
    lock = LOCK.replace('width="14" height="16"', 'width="18" height="20"')
    x = 450
    face = (f'<div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;gap:14px;font-size:22px;color:{INK}">'
            f'{lock}New episodes arrive here</div>')
    back = (f'<a class="btn def abs" href="{r89.TAB_FILES["Voyages"]}.dc.html" style="left:1540px;top:984px;width:340px">'
            '<span class="kc">LB</span>Back to Voyages</a>')
    body = (slab(GALAXY, "The galaxy", face, pos="50% 56%", filt="saturate(.25) brightness(.9)", x=x, zoom=190)
            + name_over("Episodes", "Episode 2", "Voyages and the Saga are open now.",
                        f'<span class="cap" style="font-size:22px;color:{INK};margin-top:6px">Coming soon</span>', x=x) + back)
    return r89.window(body, f"Rev {REV} Episodes - the galaxy on the table, dimmed; Episode 2 is coming; one way back", lit="Episodes",
                      lg="class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}")


# ── Notes ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

def notes_board():
    rows = [
        ("Asked", "\"Go with A, redo saga quests and episodes to match\" (Shombith, 2026-11-12, on Rev 89)."),
        ("One slab on every tab", "A picture lying tilted in perspective, and under its near edge a front face that shows the same "
                                  "thing as a line. On Voyages that line is the course seen edge on."),
        ("Saga", "The part's arena on the table. The front face is the fight's four forms in order. The Severing is dimmed and "
                 "locked; the button says After Tandava. The squad and Set sail are Voyages' own."),
        ("Quests", "The HyperSea on the table, because Episode 1 is The Hypersea. The front face is the five quests as a track: "
                   "claimed ones filled, the one to claim ringed in Jade, the picked one underlined. The picked quest's own card "
                   "stands on the right with Claim or Play quest."),
        ("Episodes", "The galaxy, dimmed. Episode 2, coming soon, and one way back."),
        ("The tabs", "Links between the boards. Voyages opens Rev 89 A; its tabs now open these (only the link targets of R89A and "
                     "R89B changed)."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:340px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:26px">'
            f'<span style="font-size:36px">Rev {REV} - Saga, Quests and Episodes as the Sea table</span>{items}</div>')
    return page(f"Rev {REV} - notes: one slab on every tab, each tab, the tabs as links", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}Saga.dc.html": saga_board, f"R{REV}Quests.dc.html": quests_board, f"R{REV}Episodes.dc.html": episodes_board,
          f"R{REV}Notes.dc.html": notes_board, "R89A.dc.html": r89.a_board, "R89B.dc.html": r89.b_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
