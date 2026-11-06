#!/usr/bin/env python3
"""The Ark's lobby boards, Rev 61: the Squads tab with the REAL Ark and the squad's REAL vessels, filmed (the renders of
r61_ark_real_render.py) rather than drawn: Rev 59 read as a cartoon. The seat cards on top are text only. Six layouts
A-F built on what was liked in Rev 59 (D, the Ark from the cell with the seats in a column; C, the HyperSea map wall),
plus Compare (Rev 59 against Rev 61, the three domains, the cameras) and Notes. Writes into <root>/project/.

    python3 r61_ark_lobby_boards.py <root> <rev> <blobs.json> [--all]

blobs.json maps names to /_blob/<id>: lobby_<cam>_<dom> (cam: beside, flank, pads, cross, board; dom: Jade, Ruby, Gold),
map_sector (the Voyage Board's Sector chart) and r59_<name> (Rev 59's renders, for the comparison). Colours follow
Docs/UI_STYLE_GUIDE.md: the squad's domain is a team thing in the live palette (Nocturne Jade #21C0BD, Ruby #BD1BC8,
Gold #FF8F11); the selected seat is the Jade selection ring #00D4FF; TAKE INTO is Ruby #A600FF (no way back); INVITE,
the kept seat's call to action, is Lime #99FF80; elements have no colour.
"""
import html
import json
import sys
from pathlib import Path

ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2])
BL = json.loads(Path(sys.argv[3]).read_text())
e = html.escape
W, H = 1920, 1080

NAV = [("CODEX", "/_blob/ec690d6b90d6fcdbc430b9bbaacf28a9"), ("HANGAR", "/_blob/34f5a50f1275fef42808fe9df3bf6af5"),
       ("HOME", "/_blob/a5bd4d8d1de7b494567fc3de75525e1e"), ("ARK", "/_blob/e5b050c5a676193c650e94d873ad40e2"),
       ("PORT", "/_blob/d0e850bc927559f1a499868d26f2a3de")]
LIGHT, SOFT, INACT, NEUT, NEUT_D, VDARK, BLACK = "#E6E9FF", "#747BAD", "#5C5F70", "#434C89", "#222645", "#00041F", "#00010A"
SUB = "#9AA0CC"
SEL, COMMIT, CTA, PURCHASE = "#00D4FF", "#A600FF", "#99FF80", "#FFAE00"
DOMC = {"Jade": "#21C0BD", "Ruby": "#BD1BC8", "Gold": "#FF8F11"}
DOMS = ["Jade", "Ruby", "Gold"]

BOOK = [
    {"name": "Skim Wing", "dom": "Jade", "mine": True, "line": "Yours - one seat kept for a friend",
     "seats": [{"seat": "Leader", "who": "You", "v": "Sparrow", "e": "Charge", "p": "Shrapnel", "a": "Extra Bay"},
               {"seat": "Rogue One", "who": "Halcyon", "v": "Dolphin", "e": "Space", "p": "Riptide", "a": "Seed Pod"},
               {"seat": "Rogue Two", "kept": True, "for": "Mothwing"}]},
    {"name": "Breakers", "dom": "Ruby", "mine": False, "owner": "Kestrel", "line": "Kestrel's squad - joined",
     "seats": [{"seat": "Leader", "who": "Kestrel", "v": "Rhino", "e": "Mass", "p": "Tomahawk", "a": "Long Edge"},
               {"seat": "Rogue One", "who": "You", "v": "Manta", "e": "Charge", "p": "Reaper", "a": "Tailwind"},
               {"seat": "Rogue Two", "who": "AI wingmate", "v": "Serpent", "e": "Space", "p": "Scales", "a": "No artifact"}]},
]

OPTS = {
    "A": ("Beside the Ark", "beside", "Outside, before the prow: the Ark with its mouth open, the Sparrow and the Dolphin flying beside it; the seats in a column"),
    "B": ("The leader, close", "flank", "Outside, on the leader's wing: the Sparrow close, the Ark and the Dolphin beyond; the seats on the right"),
    "C": ("On the pads", "pads", "Inside, from the door: the two vessels on their pads, the kept pad empty, the back wall's tunnel; the seats on the right"),
    "D": ("Across the bay", "cross", "Inside, from the starboard corner: the vessels before the HyperSea board; the seats stacked on the right"),
    "E": ("The map wall", "board", "Inside, at the HyperSea board (Rev 59 C's map): the Ark at Tumult; the seats stacked on the right"),
    "F": ("Beside the Ark, with the map", "beside", "A's view with the Sector chart as a glass panel: where the Ark is, under the squad"),
}

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
.lab{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.16em;text-transform:uppercase;color:#8C93C7;text-shadow:0 0 8px rgba(0,1,10,.95),0 1px 2px rgba(0,1,10,.95)}}
.cap{{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}}
.num{{font-variant-numeric:tabular-nums}}
.abs{{position:absolute}}
.sl{{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}}
.sls{{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}}
.kc{{display:inline-flex;align-items:center;justify-content:center;min-width:26px;height:22px;padding:0 6px;box-sizing:border-box;border:1px solid #434C89;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:12px;color:#C9CDEA}}
.btn{{box-sizing:border-box;display:flex;align-items:center;justify-content:center;gap:10px;height:48px;padding:0 20px;white-space:nowrap;border:0;cursor:pointer;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:18px;letter-spacing:.1em;text-transform:uppercase}}
.btn.sm{{height:40px;padding:0 14px;font-size:14px}}
.commit{{background:#A600FF;color:#E6E9FF}}
.cta{{background:#99FF80;color:#00010A}}
.def{{background:rgba(0,4,31,.55);color:#E6E9FF;box-shadow:inset 0 0 0 1px #747BAD}}
.off{{background:rgba(0,4,31,.4);color:#5C5F70;box-shadow:inset 0 0 0 1px #222645}}
.btn .kc{{border-color:currentColor;color:inherit;opacity:.85}}
.btn:focus-visible,.pick:focus-visible{{outline:2px solid #00D4FF;outline-offset:2px}}
.pick{{background:none;border:0;padding:0;cursor:pointer;color:inherit;font:inherit;text-align:left}}
.glass{{background:rgba(0,4,31,.62);backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px)}}
.bg{{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;transition:opacity 900ms ease-in-out}}
.ell{{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}}
</style>
</helmet>
"""
TAIL = """</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
{logic}</script>
</body>
</html>
"""
LOCK = ('<svg width="12" height="14" viewBox="0 0 14 16" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true">'
        '<path d="M3 7V5a4 4 0 018 0v2"></path><path d="M1.5 7h11v8h-11z"></path></svg>')
PLUS = ('<svg width="14" height="14" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">'
        '<path d="M7 1v12M1 7h12"></path></svg>')


def kc(t):
    return f'<span class="kc">{e(t)}</span>'


def logic(cam, extra=""):
    bg = {d: BL[f"lobby_{cam}_{d}"] for d in DOMS}
    return (f"const BG = {json.dumps(bg)};\nconst DOMC = {json.dumps(DOMC)};\nconst BOOK = {json.dumps(BOOK)};\n" + r"""
class Component extends DCLogic {
 constructor(props) { super(props); this.state = { sq: 0, doms: BOOK.map((b) => b.dom), sel: 0, toast: '' }; this.t = null; }
 componentWillUnmount() { if (this.t) clearTimeout(this.t); }
 say(x) { if (this.t) clearTimeout(this.t); this.setState({ toast: x }); this.t = setTimeout(() => this.setState({ toast: '' }), 2600); }
 renderVals() {
  const st = this.state, q = BOOK[st.sq], dom = st.doms[st.sq], dc = DOMC[dom];
  const layers = ['Jade', 'Ruby', 'Gold'].map((d) => ({ src: BG[d], op: d === dom ? 1 : 0, alt: d === dom ? 'The Ark and its squad, lit ' + d : '' }));
  const seats = q.seats.map((s, i) => {
   const on = i === st.sel, kept = !!s.kept;
   return { num: '0' + (i + 1), seat: s.seat, who: kept ? 'Kept for ' + s.for : s.who, kept, filled: !kept,
    v: s.v || '', e: s.e || '', p: s.p || '', a: s.a || '', aCol: s.a === 'No artifact' ? '#5C5F70' : '#E6E9FF',
    ring: on ? '#00D4FF' : 'rgba(116,123,173,.45)', ringW: on ? '2px' : '1px', mark: on ? '#00D4FF' : 'transparent',
    tabCol: on ? '#E6E9FF' : '#747BAD', isSel: on, notSel: !on,
    aria: s.seat + ': ' + (kept ? 'kept for ' + s.for : s.who),
    pick: () => this.setState({ sel: i }),
    invite: () => this.say('Invite sent to ' + s.for + '. The seat stays theirs until they come aboard.'),
    ai: () => this.say('An AI wingmate sits here until ' + s.for + ' comes aboard.') };
  });
  const cur = seats[st.sel];
  const squads = BOOK.map((b, i) => ({ name: b.name, dot: DOMC[st.doms[i]], on: i === st.sq,
   edge: i === st.sq ? '#00D4FF' : 'rgba(116,123,173,.45)', col: i === st.sq ? '#E6E9FF' : '#9AA0CC',
   pick: () => this.setState({ sq: i, sel: 0, toast: '' }) }));
  const domains = ['Jade', 'Ruby', 'Gold'].map((k) => { const on = k === dom; return { name: k, dot: DOMC[k],
   edge: on ? '#00D4FF' : 'rgba(116,123,173,.45)', col: on ? '#E6E9FF' : '#9AA0CC', pressed: on ? 'true' : 'false',
   pick: () => { if (on) return; if (!q.mine) { this.say('Only ' + q.owner + ' changes the squad\'s domain.'); return; }
    const d = st.doms.slice(); d[st.sq] = k; this.setState({ doms: d }); } }; });
  return { layers, seats, cur, squads, domains, dom, dc, name: q.name, line: q.line,
   kicker: q.mine ? 'Your squad' : q.owner + '\'s squad', toast: st.toast, hasToast: st.toast !== '',
   prev: () => this.setState({ sel: (st.sel + 2) % 3 }), next: () => this.setState({ sel: (st.sel + 1) % 3 }),
   edit: () => this.say('Edit opens the seat picker.'), make: () => this.say('Make squad starts a new one.'),
   join: () => this.say('Join a friend\'s squad.'), mission: () => this.say('Mission is locked: finish Episode 1 first.'),
   arena: () => this.say('Taking ' + q.name + ' into the Arena, flying ' + dom + '.'), tabDecor: () => this.say('Not open yet.') """
            + extra + r""" };
 }
}
""")


def board(title, body, cam=None, w=W, h=H, static_logic=None):
    root = (f'<div style="width: {w}px; height: {h}px; position: relative; overflow: hidden; background: {VDARK}; color: {LIGHT}; '
            f"font-family: 'Aldrich', 'Chakra Petch', sans-serif\">\n{body}\n</div>\n")
    lg = logic(cam) if cam else (static_logic or "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n")
    return HEAD.format(title=e(title)) + root + TAIL.format(w=w, h=h, logic=lg)


# ------------------------------------------------------------------------------------------------ pieces

def backdrop(shade="linear-gradient(180deg,rgba(0,1,10,.75) 0%,rgba(0,1,10,0) 22%,rgba(0,1,10,0) 70%,rgba(0,1,10,.7) 100%)"):
    return ('<sc-for list="{{layers}}" as="l" hint-placeholder-count="3"><img class="bg" src="{{l.src}}" alt="{{l.alt}}" '
            'style="opacity:{{l.op}}"></sc-for>'
            f'<div class="abs" style="inset:0;background:{shade};pointer-events:none"></div>')


def nav():
    items = []
    for name, icon in NAV:
        on = name == "ARK"
        mark = '<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:#E6E9FF"></span>' if on else ""
        items.append(f'<span style="position:relative;display:flex;align-items:center;gap:10px;font-family:\'Chakra Petch\';font-weight:600;'
                     f'font-size:17px;letter-spacing:.12em;color:{LIGHT if on else SOFT}"><img src="{icon}" alt="" '
                     f'style="width:26px;height:26px;object-fit:contain;opacity:{1 if on else .7}">{name}{mark}</span>')
    return (f'<nav aria-label="Menu screens" class="abs" style="left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.78);'
            f'border-bottom:1px solid {NEUT_D};display:flex;align-items:center;justify-content:center;gap:48px;z-index:5">'
            f'{kc("LT")}{"".join(items)}{kc("RT")}</nav>'
            f'<div class="abs num" style="right:40px;top:30px;z-index:6;display:flex;align-items:center;gap:8px;font-size:20px;color:{PURCHASE}">'
            f'<span style="width:14px;height:18px;background:{PURCHASE};clip-path:polygon(50% 0,100% 35%,50% 100%,0 35%)"></span>1,240</div>')


def tabrow():
    def lab(t, on):
        return f'<span class="cap" style="font-size:16px;color:{LIGHT if on else SOFT}">{t}</span>'
    tabs = (f'<span>{lab("Converter", False)}</span>'
            f'<span style="position:relative">{lab("Squads", True)}<span class="abs" style="left:0;right:0;bottom:-15px;height:2px;background:{LIGHT}"></span></span>'
            f'<span>{lab("Artifacts", False)}</span>'
            f'<button class="pick" type="button" onClick="{{{{tabDecor}}}}" style="display:flex;align-items:center;gap:8px;color:{INACT}">{LOCK}'
            f'<span class="cap" style="font-size:16px;color:{INACT}">Decor</span></button>')
    return (f'<div class="abs" style="left:40px;right:40px;top:96px;height:52px;display:flex;align-items:center;gap:30px;'
            f'border-bottom:1px solid rgba(116,123,173,.35);z-index:4">{kc("LB")}{tabs}{kc("RB")}</div>')


def toast():
    return ('<sc-if value="{{hasToast}}" hint-placeholder-val="{{ false }}"><div class="abs glass sls" role="status" '
            'style="right:40px;top:172px;width:440px;padding:14px 18px 14px 22px;z-index:9;display:flex;flex-direction:column;gap:4px">'
            f'<span class="abs" style="left:0;top:12px;bottom:12px;width:3px;background:{LIGHT}"></span>'
            '<span class="lab" style="font-size:12px">Squads</span><span style="font-size:16px;line-height:1.4">{{toast}}</span></div></sc-if>')


def head(size=40):
    return (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">{{{{kicker}}}}</span>'
            f'<span style="display:flex;align-items:center;gap:14px"><span style="width:12px;height:12px;transform:rotate(45deg);background:{{{{dc}}}}"></span>'
            f'<span style="font-size:{size}px;line-height:1.05">{{{{name}}}}</span></span>'
            f'<span style="font-size:15px;color:{SUB}">{{{{line}}}}</span></div>')


def squads_row():
    return ('<div style="display:flex;align-items:center;gap:8px"><span class="lab" style="font-size:12px;margin-right:6px">Squads</span>'
            '<sc-for list="{{squads}}" as="s" hint-placeholder-count="2"><button class="pick sls glass" type="button" onClick="{{s.pick}}" '
            'style="display:flex;align-items:center;gap:8px;padding:8px 14px;box-shadow:inset 0 0 0 1px {{s.edge}}">'
            '<span style="width:8px;height:8px;transform:rotate(45deg);background:{{s.dot}}"></span>'
            '<span class="cap" style="font-size:14px;color:{{s.col}}">{{s.name}}</span></button></sc-for>'
            f'<button class="pick sls glass" type="button" onClick="{{{{make}}}}" style="display:flex;align-items:center;gap:6px;padding:8px 14px;'
            f'box-shadow:inset 0 0 0 1px rgba(116,123,173,.45);color:{SUB}">{PLUS}<span class="cap" style="font-size:14px">Make</span></button>'
            f'<button class="pick sls glass" type="button" onClick="{{{{join}}}}" style="padding:8px 14px;box-shadow:inset 0 0 0 1px rgba(116,123,173,.45)">'
            f'<span class="cap" style="font-size:14px;color:{SUB}">Join a friend</span></button></div>')


def domain_chips():
    return ('<div style="display:flex;align-items:center;gap:8px"><span class="lab" style="font-size:12px;margin-right:6px">Domain</span>'
            '<sc-for list="{{domains}}" as="d" hint-placeholder-count="3"><button class="pick sls glass" type="button" aria-pressed="{{d.pressed}}" '
            'onClick="{{d.pick}}" style="display:flex;align-items:center;gap:8px;padding:8px 14px;box-shadow:inset 0 0 0 1px {{d.edge}}">'
            '<span style="width:9px;height:9px;transform:rotate(45deg);background:{{d.dot}}"></span>'
            '<span class="cap" style="font-size:14px;color:{{d.col}}">{{d.name}}</span></button></sc-for></div>')


def actions():
    return (f'<div style="display:flex;align-items:center;gap:10px">'
            f'<button class="btn sm def sls" type="button" onClick="{{{{edit}}}}">{kc("X")}<span>Edit squad</span></button></div>'
            f'<div style="display:flex;align-items:center;gap:10px"><span class="lab" style="font-size:12px">Take this squad into</span>'
            f'<button class="btn off sl" type="button" onClick="{{{{mission}}}}">{LOCK}<span>Mission</span></button>'
            f'<button class="btn commit sl" type="button" onClick="{{{{arena}}}}">{kc("A")}<span>Arena</span></button></div>')


def rows(v_size=17, gap=6):
    def r(k, v, col=LIGHT):
        return (f'<span style="display:flex;justify-content:space-between;align-items:baseline;gap:12px;padding:{gap}px 0;'
                f'border-top:1px solid rgba(116,123,173,.25)"><span class="lab" style="font-size:11px">{k}</span>'
                f'<span class="ell" style="font-size:{v_size}px;color:{col}">{v}</span></span>')
    return r("Vessel", "{{s.v}}") + r("Element", "{{s.e}}") + r("Pilot", "{{s.p}}") + r("Artifact", "{{s.a}}", "{{s.aCol}}")


def seat_card(width, pad=18, who=24, v_size=17):
    """One seat as a glass card: the seat and number, who sits there, its four rows; a kept seat waits for its friend.
    The card is a group: its header is the select button, Invite / AI are buttons of their own."""
    return (f'<div role="group" aria-label="{{{{s.aria}}}}" class="sl glass" '
            f'style="position:relative;width:{width}px;box-sizing:border-box;padding:{pad}px {pad}px {pad - 2}px {pad + 4}px;display:flex;flex-direction:column;gap:6px;'
            f'box-shadow:inset 0 0 0 {{{{s.ringW}}}} {{{{s.ring}}}}">'
            f'<span class="abs" style="left:0;top:14px;bottom:14px;width:3px;background:{{{{dc}}}}"></span>'
            f'<button class="pick" type="button" onClick="{{{{s.pick}}}}" style="display:flex;flex-direction:column;gap:6px;width:100%">'
            f'<span style="display:flex;justify-content:space-between;width:100%"><span class="lab num" style="font-size:12px">{{{{s.num}}}} {{{{s.seat}}}}</span>'
            f'<span style="width:8px;height:8px;transform:rotate(45deg);background:{{{{s.mark}}}}"></span></span>'
            f'<span class="ell" style="font-size:{who}px;max-width:100%">{{{{s.who}}}}</span></button>'
            f'<sc-if value="{{{{s.filled}}}}" hint-placeholder-val="{{{{ true }}}}"><span style="display:flex;flex-direction:column;margin-top:4px">{rows(v_size)}</span></sc-if>'
            f'<sc-if value="{{{{s.kept}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="display:flex;flex-direction:column;gap:10px;margin-top:6px">'
            f'<span style="font-size:15px;line-height:1.45;color:{SUB}">The seat is theirs. It stays kept until they come aboard.</span>'
            f'<span style="display:flex;gap:8px"><button class="btn sm cta sls" type="button" onClick="{{{{s.invite}}}}">{kc("Y")}<span>Invite</span></button>'
            f'<button class="btn sm def sls" type="button" onClick="{{{{s.ai}}}}"><span>AI until then</span></button></span></span></sc-if>'
            f'</div>')


def frame(body, seats_markup):
    return backdrop() + nav() + tabrow() + body + seats_markup + toast()


# ------------------------------------------------------------------------------------------------ the six layouts

def column(side, top=300, width=470):
    return (f'<div class="abs" style="{side}:40px;top:{top}px;width:{width}px;display:flex;flex-direction:column;gap:12px">'
            '<sc-for list="{{seats}}" as="s" hint-placeholder-count="3">' + seat_card(width, 16, 21, 15) + '</sc-for></div>')


def caption(side, label, text, top=930):
    return (f'<div class="abs glass sls" style="{side}:40px;top:{top}px;padding:8px 14px;display:flex;align-items:center;gap:10px">'
            f'<span class="lab" style="font-size:11px">{label}</span><span style="font-size:15px">{text}</span></div>')


def bottom():
    return f'<div class="abs" style="left:40px;right:40px;top:1004px;display:flex;justify-content:space-between;align-items:center">{actions()}</div>'


def chips_right():
    return f'<div class="abs" style="right:40px;top:176px;display:flex;flex-direction:column;align-items:flex-end;gap:10px">{domain_chips()}{squads_row()}</div>'


def chips_left():
    return f'<div class="abs" style="left:40px;top:172px;display:flex;flex-direction:column;gap:10px">{squads_row()}{domain_chips()}</div>'


def screen_A():
    # Rev 59 D, filmed: the seats in a column on the left, the Ark and its escorts on the right
    body = (f'<div class="abs" style="left:40px;top:172px">{head(34)}</div>{chips_right()}'
            + caption("right", "The Ark", "Its hangar open; the Sparrow and the Dolphin beside it") + bottom())
    return frame(body, column("left"))


def screen_B():
    # on the leader's wing: the Sparrow fills the left, the seats on the right
    body = (f'<div class="abs" style="right:40px;top:172px;width:470px">{head(34)}</div>{chips_left()}'
            + caption("left", "Leader", "Your Sparrow, on the Ark's starboard bow") + bottom())
    return frame(body, column("right"))


def screen_C():
    # inside, from the door: the vessels on their pads; the seats on the right
    body = (f'<div class="abs" style="right:40px;top:172px;width:470px">{head(34)}</div>{chips_left()}'
            + caption("left", "The hangar", "Two pads taken, the third kept for Mothwing") + bottom())
    return frame(body, column("right"))


def screen_D():
    # across the bay: the vessels before the HyperSea board; the seats stacked on the right (Rev 59 C's layout)
    body = (f'<div class="abs" style="right:40px;top:172px;width:470px">{head(34)}</div>{chips_left()}'
            + caption("left", "On the board", "The Ark at Tumult, the Sector chart") + bottom())
    return frame(body, column("right"))


def screen_E():
    # the map wall (Rev 59 C, filmed): the board fills the left, the seats stacked on the right
    body = (f'<div class="abs" style="right:40px;top:172px;width:470px">{head(34)}</div>{chips_left()}'
            + caption("left", "On the board", "The Ark at Tumult - Sector chart, the galaxy beside it") + bottom())
    return frame(body, column("right"))


def screen_F():
    # A with the map: the Sector chart as a glass panel bottom right, the Ark's place marked
    mp = (f'<figure class="abs glass sl" style="right:40px;top:590px;width:560px;margin:0;box-sizing:border-box;padding:12px;'
          f'display:flex;flex-direction:column;gap:8px"><span style="display:flex;justify-content:space-between;align-items:baseline">'
          f'<span class="lab" style="font-size:11px">HyperSea - Sector</span><span style="font-size:14px;color:{SUB}">The Ark at Tumult</span></span>'
          f'<img src="{BL["map_sector"]}" alt="The Voyage Board\'s Sector chart with the Ark at Tumult" '
          f'style="width:536px;height:290px;object-fit:cover;display:block"></figure>')
    body = (f'<div class="abs" style="left:40px;top:172px">{head(34)}</div>{chips_right()}{mp}' + bottom())
    return frame(body, column("left"))


SCREENS = {"A": screen_A, "B": screen_B, "C": screen_C, "D": screen_D, "E": screen_E, "F": screen_F}


# ------------------------------------------------------------------------------------------------ compare and notes

def compare():
    def fig(src, alt, w, h, cap, col=SUB):
        return (f'<figure style="margin:0;display:flex;flex-direction:column;gap:6px"><img src="{src}" alt="{e(alt)}" '
                f'style="width:{w}px;height:{h}px;object-fit:cover"><figcaption style="font-size:14px;color:{col}">{e(cap)}</figcaption></figure>')
    before = "".join([
        fig(BL["r59_hero"], "Rev 59 inside", 420, 236, "Rev 59, inside: flat self-lit colour, a blue fill everywhere"),
        fig(BL["r59_mouth"], "Rev 59 outside", 420, 236, "Rev 59, outside: the Ark as black with neon outlines"),
        fig(BL["lobby_pads_Jade"], "Rev 61 inside", 420, 236, "Rev 61, inside: lit plating, dead prisms, the vessels on their pads"),
        fig(BL["lobby_beside_Jade"], "Rev 61 outside", 420, 236, "Rev 61, outside: a lit hull, its escorts beside it")])
    doms = "".join(fig(BL["lobby_beside_" + d], "Beside the Ark, " + d, 560, 315, d, DOMC[d]) for d in DOMS)
    cams = "".join(fig(BL["lobby_" + c + "_Jade"], t, 560, 315, t) for c, t in (
        ("flank", "The leader, close (B)"), ("cross", "Across the bay (D)"), ("board", "The map wall (E)")))
    return (f'<div class="abs" style="left:60px;top:40px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - compare</span>'
            f'<span style="font-size:30px">No cartoon: Rev 59 against Rev 61, the three domains, the cameras</span></div>'
            f'<div class="abs" style="left:60px;top:120px;display:flex;flex-direction:column;gap:10px"><span class="lab" style="color:{LIGHT}">'
            f'1 - Before and after: the same Ark and prisms, drawn flat (59) and lit (61)</span><div style="display:flex;gap:16px">{before}</div></div>'
            f'<div class="abs" style="left:60px;top:430px;display:flex;flex-direction:column;gap:10px"><span class="lab" style="color:{LIGHT}">'
            f'2 - The squad\'s domain: its trim, its running lights, its trail (press a chip on any board)</span><div style="display:flex;gap:16px">{doms}</div></div>'
            f'<div class="abs" style="left:60px;top:790px;display:flex;flex-direction:column;gap:10px"><span class="lab" style="color:{LIGHT}">'
            f'3 - The other cameras</span><div style="display:flex;gap:16px">{cams}</div></div>')


def notes():
    def para(t):
        return f'<p style="margin:0;font-size:17px;line-height:1.5;color:#C9CDEA">{t}</p>'
    left = "".join([
        '<span class="lab">Asked (Shombith, 2026-11-06, on Rev 59)</span>',
        para('"I liked D and in C the map ... can you also show the vessels beside the Ark and then another inside ... the current one '
             'looks like they are from a cartoon, please we need to fix that first. Cannot be looking like a cartoon."'),
        '<span class="lab" style="margin-top:8px">Why 59 read as a cartoon, and what changed</span>',
        para("Everything glowed by itself under one strong blue fill light, and the Standard view transform clipped each glow to a flat colour; "
             "the hull was black with neon outlines; the debris was candy-coloured. Rev 61 films the same pieces: AgX tone mapping (light rolls "
             "off instead of clipping), a near-black world with the cell as the only sky, every light motivated (the cell through the mouth, "
             "strip lights in the arches, the pads, the back wall's ember); the hull lit, in panels with seams, wear and varying gloss; the "
             "prisms the game's read but lit - glossy black faces, the rim caught on their edges; the spent prisms in the heaps dead and dark; "
             "haze, depth of field, bloom and grain."),
        '<span class="lab" style="margin-top:8px">The vessels</span>',
        para("The squad's own models at their real sizes: the leader's Sparrow (12.5 u) and Rogue One's Dolphin (5.3 u), in the squad's domain. "
             "Inside they stand on their launch pads (the Ark's hangar.slot bones: head on the pad, tail toward the door); the third pad is "
             "kept for Mothwing, empty but for a dark prism outline. Outside they fly beside the Ark, each laying the game's block trail."),
    ])
    right = "".join([
        '<span class="lab">The six</span>',
        para("A Beside the Ark: Rev 59 D, filmed, with the escorts. B The leader, close: on the Sparrow's wing, the Ark beyond. C On the pads: "
             "inside from the door, the vessels on their pads. D Across the bay: the vessels before the HyperSea board. E The map wall: Rev 59 "
             "C's map, filmed. F Beside the Ark, with the map: A plus the Sector chart as a glass panel."),
        para("Every board is interactive: the domain chips relight the Ark, the vessels' trim and the trails (Jade, Ruby, Gold); the seat cards "
             "select (the Jade ring); the kept seat invites its friend (Lime) or seats an AI until then; Breakers is Kestrel's so its chips "
             "refuse; TAKE INTO ARENA is Ruby."),
        '<span class="lab" style="margin-top:8px">Recommendation: A</span>',
        para("It is the view that was liked (Rev 59 D), now with the squad flying beside the Ark: the squad is what the tab is about, and the "
             "open hangar says where they live. C is the inside to pair with it if the tab should open inside."),
        '<span class="lab" style="margin-top:8px">Differs from the build</span>',
        para("Today the tab is the Rev 31 deck on the menu's sea. This puts the Ark and its escorts behind it: baked backdrops per domain (these "
             "renders) or a small live scene with ArkReverse and the seats' real hulls (it would breathe: the doors, the trails, the trim). "
             "The vessels shown would be the seats' own. Seat data, Make, Join, Edit and Take into are unchanged. The build waits on a pick."),
    ])
    return (f'<div class="abs" style="left:60px;top:44px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:30px">The Ark\'s lobby, filmed: the real Ark, the squad\'s real vessels, no cartoon</span></div>'
            f'<div class="abs" style="left:60px;top:130px;width:860px;display:flex;flex-direction:column;gap:12px">{left}</div>'
            f'<div class="abs" style="left:1000px;top:130px;width:860px;display:flex;flex-direction:column;gap:12px">{right}</div>')


def main():
    # the canvas is at its 512-file cap: the round is the six option boards; Compare and Notes are written only with
    # --all (the rationale lives in Docs/MENU_REDESIGN_B.md)
    files = {}
    if "--all" in sys.argv:
        files[f"R{REV}Compare.dc.html"] = board(f"Rev {REV} - compare", compare())
    for k, (name, cam, line) in OPTS.items():
        files[f"R{REV}{k}.dc.html"] = board(f"Rev {REV} {k} - {name}", SCREENS[k](), cam)
    if "--all" in sys.argv:
        files[f"R{REV}Notes.dc.html"] = board(f"Rev {REV} - notes", notes(), h=900)
    for n, s in files.items():
        (ROOT / n).write_text(s, encoding="utf-8")
        print(f"{n:24} {len(s):>7}")


if __name__ == "__main__":
    main()
