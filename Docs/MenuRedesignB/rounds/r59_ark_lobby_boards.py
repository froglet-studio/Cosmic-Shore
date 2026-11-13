#!/usr/bin/env python3
"""The Ark's lobby boards: the Squads tab as the inside of the Ark (the lobby renders of r59_ark_hangar_render.py), with the
squad's seat cards on top - text only, no pilots, no vessels. Six layouts A-F, plus Compare (the real game's
screenshots beside the lobby, the three domains) and Notes. Writes into <root>/project/.

    python3 r59_ark_lobby_boards.py <root> <rev> <blobs.json>

blobs.json maps names to /_blob/<id>: lobby_<cam>_<dom> (cam: hero, board, mouth; dom: Jade, Ruby, Gold) and
ref_<name> (the Amoebius screenshots). Colours follow Docs/UI_STYLE_GUIDE.md: the squad's domain is a team thing in the
live palette (Nocturne Jade #21C0BD, Ruby #BD1BC8, Gold #FF8F11); the selected seat is the Jade selection ring #00D4FF;
TAKE INTO is Ruby #A600FF (no way back); INVITE, the kept seat's call to action, is Lime #99FF80; elements have no colour.
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
    "A": ("Seat placards", "hero", "Each seat's card hangs over its launch pad's prism, a hairline down to it"),
    "B": ("Rail", "hero", "The room left clear; the three seats in one glass rail along the bottom"),
    "C": ("The board wall", "board", "Turned to the HyperSea board between the arches; the seats stacked on the right"),
    "D": ("From the cell", "mouth", "Outside, before the prow: the real Ark, its mouth open on the lit hangar; the seats in a column"),
    "E": ("Slate", "hero", "One glass slate on the right lists the squad; the hangar and its pads fill the left"),
    "F": ("One seat at a time", "hero", "The chosen seat large, the other two as tabs; LB / RB step between them"),
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
  const layers = ['Jade', 'Ruby', 'Gold'].map((d) => ({ src: BG[d], op: d === dom ? 1 : 0, alt: d === dom ? 'Inside the Ark, lit ' + d : '' }));
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

def screen_A():
    # Seat placards: each card hangs over its launch pad's prism; a hairline drops to it
    xs = [1650, 1150, 650]                    # Leader, Rogue One, Rogue Two: the near pad is on the right
    seat_pts = [(1644, 742), (1110, 622), (860, 612)]
    import math

    def hair(x1, y1, x2, y2, dash):
        # a hairline as a turned div (the canvas draws divs everywhere; an inline <svg> line is not guaranteed)
        ln, ang = math.hypot(x2 - x1, y2 - y1), math.degrees(math.atan2(y2 - y1, x2 - x1))
        stroke = ("background:repeating-linear-gradient(90deg,rgba(230,233,255,.75) 0 6px,transparent 6px 11px)" if dash
                  else "background:rgba(230,233,255,.7)")
        return (f'<div class="abs" style="left:{x1}px;top:{y1}px;width:{ln:.0f}px;height:1.5px;{stroke};transform-origin:0 50%;'
                f'transform:rotate({ang:.2f}deg);box-shadow:0 0 6px rgba(0,1,10,.8);pointer-events:none"></div>'
                f'<div class="abs" style="left:{x2 - 4}px;top:{y2 - 4}px;width:8px;height:8px;border-radius:50%;'
                f'{"border:1.5px dashed #E6E9FF;box-sizing:border-box" if dash else "background:#E6E9FF"};'
                f'box-shadow:0 0 10px rgba(230,233,255,.8);pointer-events:none"></div>')
    tops = [512, 512, 488]                    # each card's bottom edge (the kept card is shorter)
    lines = "".join(hair(x, t, sx, sy, i == 2) for i, (x, t, (sx, sy)) in enumerate(zip(xs, tops, seat_pts)))
    cards = ('<div class="abs" style="left:445px;top:300px;width:1410px;display:flex;flex-direction:row-reverse;justify-content:space-between;align-items:flex-start">'
             '<sc-for list="{{seats}}" as="s" hint-placeholder-count="3">' + seat_card(410) + '</sc-for></div>')
    body = (f'<div aria-hidden="true">{lines}</div>'
            f'<div class="abs" style="left:40px;top:172px">{head()}</div>'
            f'<div class="abs" style="right:40px;top:176px;display:flex;flex-direction:column;align-items:flex-end;gap:10px">{domain_chips()}{squads_row()}</div>'
            f'<div class="abs" style="left:40px;right:40px;top:1000px;display:flex;justify-content:space-between;align-items:center">{actions()}</div>')
    return frame(body, cards)


def screen_B():
    # Rail: the room clear, three seats along the bottom in one glass rail
    cards = ('<div class="abs glass sl" style="left:40px;right:40px;top:752px;height:228px;box-sizing:border-box;padding:16px;display:flex;gap:16px">'
             '<sc-for list="{{seats}}" as="s" hint-placeholder-count="3">' + seat_card(600, 16, 22, 16).replace('class="sl glass"', 'class="sl"') + '</sc-for></div>')
    body = (f'<div class="abs" style="left:40px;top:172px">{head(44)}</div>'
            f'<div class="abs" style="right:40px;top:176px;display:flex;flex-direction:column;align-items:flex-end;gap:10px">{domain_chips()}{squads_row()}</div>'
            f'<div class="abs" style="left:40px;right:40px;top:1004px;display:flex;justify-content:space-between;align-items:center">{actions()}</div>')
    return frame(body, cards)


def screen_C():
    # The board wall: turned to the HyperSea board; the seats stacked on the right
    cards = ('<div class="abs" style="right:40px;top:300px;width:470px;display:flex;flex-direction:column;gap:12px">'
             '<sc-for list="{{seats}}" as="s" hint-placeholder-count="3">' + seat_card(470, 16, 21, 15) + '</sc-for></div>')
    body = (f'<div class="abs" style="right:40px;top:172px;width:470px;display:flex;flex-direction:column;gap:12px">{head(34)}</div>'
            f'<div class="abs" style="left:40px;top:172px;display:flex;flex-direction:column;gap:10px">{squads_row()}{domain_chips()}</div>'
            f'<div class="abs glass sls" style="left:40px;top:930px;padding:8px 14px;display:flex;align-items:center;gap:10px">'
            f'<span class="lab" style="font-size:11px">On the board</span><span style="font-size:15px">The Ark at Tumult - Sector chart, the galaxy beside it</span></div>'
            f'<div class="abs" style="left:40px;right:40px;top:1004px;display:flex;justify-content:space-between;align-items:center">{actions()}</div>')
    return frame(body, cards)


def screen_D():
    # From the cell: outside before the prow, the Ark's mouth open on the lit hangar; the seats in a column on the left
    cards = ('<div class="abs" style="left:40px;top:300px;width:470px;display:flex;flex-direction:column;gap:12px">'
             '<sc-for list="{{seats}}" as="s" hint-placeholder-count="3">' + seat_card(470, 16, 21, 15) + '</sc-for></div>')
    body = (f'<div class="abs" style="left:40px;top:172px">{head(34)}</div>'
            f'<div class="abs" style="right:40px;top:176px;display:flex;flex-direction:column;align-items:flex-end;gap:10px">{domain_chips()}{squads_row()}</div>'
            f'<div class="abs glass sls" style="right:40px;top:930px;padding:8px 14px;display:flex;align-items:center;gap:10px">'
            f'<span class="lab" style="font-size:11px">The Ark</span><span style="font-size:15px">Its hangar open: three pads, one per seat</span></div>'
            f'<div class="abs" style="left:40px;right:40px;top:1004px;display:flex;justify-content:space-between;align-items:center">{actions()}</div>')
    return frame(body, cards)


def screen_E():
    # Slate: one glass slate on the right lists the squad; the room and the seat prisms fill the left
    row = (f'<div role="group" aria-label="{{{{s.aria}}}}" style="position:relative;display:grid;'
           f'grid-template-columns:150px 1fr;gap:16px;padding:14px 16px 12px 20px;box-shadow:inset 0 0 0 {{{{s.ringW}}}} {{{{s.ring}}}}">'
           f'<button class="pick" type="button" onClick="{{{{s.pick}}}}" style="display:flex;flex-direction:column;gap:4px"><span class="lab num" style="font-size:11px">{{{{s.num}}}} {{{{s.seat}}}}</span>'
           f'<span class="ell" style="font-size:20px;max-width:150px">{{{{s.who}}}}</span></button>'
           f'<sc-if value="{{{{s.filled}}}}" hint-placeholder-val="{{{{ true }}}}"><span style="display:grid;grid-template-columns:1fr 1fr;column-gap:16px">{rows(15, 4)}</span></sc-if>'
           f'<sc-if value="{{{{s.kept}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="display:flex;align-items:center;gap:8px">'
           f'<span style="font-size:14px;color:{SUB};flex:1">Kept until they come aboard</span>'
           f'<button class="btn sm cta sls" type="button" onClick="{{{{s.invite}}}}">{kc("Y")}<span>Invite</span></button></span></sc-if></div>')
    slate = (f'<div class="abs glass sl" style="right:40px;top:172px;width:720px;box-sizing:border-box;padding:24px;'
             f'display:flex;flex-direction:column;gap:18px"><span class="abs" style="left:0;top:20px;bottom:20px;width:3px;background:{{{{dc}}}}"></span>'
             f'{head(36)}{squads_row()}{domain_chips()}<span style="height:1px;background:rgba(116,123,173,.35)"></span>'
             f'<div style="display:flex;flex-direction:column;gap:10px"><sc-for list="{{{{seats}}}}" as="s" hint-placeholder-count="3">{row}</sc-for></div></div>')
    body = f'<div class="abs" style="left:40px;right:40px;top:1004px;display:flex;justify-content:space-between;align-items:center">{actions()}</div>'
    return frame(body, slate)


def screen_F():
    # One seat at a time: the chosen seat large over the prisms, the others as tabs; LB / RB step
    tabs = ('<div class="abs glass sls" style="left:660px;top:276px;width:600px;height:52px;box-sizing:border-box;padding:0 14px;display:flex;align-items:center;justify-content:space-between;gap:12px">'
            f'<button class="pick" type="button" onClick="{{{{prev}}}}" aria-label="Previous seat">{kc("LB")}</button>'
            '<sc-for list="{{seats}}" as="s" hint-placeholder-count="3"><button class="pick" type="button" onClick="{{s.pick}}" '
            'style="display:flex;flex-direction:column;align-items:center;gap:6px;padding:0 8px">'
            '<span class="cap num" style="font-size:14px;color:{{s.tabCol}}">{{s.num}} {{s.seat}}</span>'
            '<span style="width:100%;height:2px;background:{{s.mark}}"></span></button></sc-for>'
            f'<button class="pick" type="button" onClick="{{{{next}}}}" aria-label="Next seat">{kc("RB")}</button></div>')
    big = ('<div class="abs" style="left:660px;top:344px;width:600px">'
           '<sc-for list="{{seats}}" as="s" hint-placeholder-count="3"><sc-if value="{{s.isSel}}" hint-placeholder-val="{{ true }}">'
           + seat_card(600, 22, 32, 19) + '</sc-if></sc-for></div>')
    body = (f'<div class="abs" style="left:40px;top:172px">{head()}</div>'
            f'<div class="abs" style="right:40px;top:176px;display:flex;flex-direction:column;align-items:flex-end;gap:10px">{domain_chips()}{squads_row()}</div>'
            f'<div class="abs" style="left:40px;right:40px;top:1004px;display:flex;justify-content:space-between;align-items:center">{actions()}</div>')
    return frame(body, tabs + big)


SCREENS = {"A": screen_A, "B": screen_B, "C": screen_C, "D": screen_D, "E": screen_E, "F": screen_F}


# ------------------------------------------------------------------------------------------------ compare and notes

def compare():
    refs = [("ref_view_wide", "Prisms up close: black faces, the rim in the domain"), ("ref_cell_clean_0", "The menu's cell, HUD hidden"),
            ("ref_menu_home", "Home: the live cell behind the menu"), ("ref_Rampage_lobby", "Rampage: the arena cell from far off")]
    ref = "".join(f'<figure style="margin:0;display:flex;flex-direction:column;gap:6px"><img src="{BL[k]}" alt="{e(t)}" '
                  f'style="width:420px;height:236px;object-fit:cover;box-shadow:inset 0 0 0 1px {NEUT_D}">'
                  f'<figcaption style="font-size:14px;color:{SUB}">{e(t)}</figcaption></figure>' for k, t in refs)
    lob = "".join(f'<figure style="margin:0;display:flex;flex-direction:column;gap:6px"><img src="{BL["lobby_hero_" + d]}" alt="The lobby lit {d}" '
                  f'style="width:560px;height:315px;object-fit:cover"><figcaption class="cap" style="font-size:13px;color:{DOMC[d]}">{d}</figcaption></figure>'
                  for d in DOMS)
    cams = "".join(f'<figure style="margin:0;display:flex;flex-direction:column;gap:6px"><img src="{BL["lobby_" + c + "_Jade"]}" alt="{t}" '
                   f'style="width:560px;height:315px;object-fit:cover"><figcaption style="font-size:14px;color:{SUB}">{t}</figcaption></figure>'
                   for c, t in (("hero", "In the hangar: the three pads, the open mouth, the cell (A, B, E, F)"),
                                ("board", "The HyperSea board between the arches (C)"), ("mouth", "From the cell: the real Ark, its mouth open (D)")))
    return (f'<div class="abs" style="left:60px;top:40px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - compare</span>'
            f'<span style="font-size:30px">The game it is built from, the three domains, the three cameras</span></div>'
            f'<div class="abs" style="left:60px;top:120px;display:flex;flex-direction:column;gap:10px"><span class="lab" style="color:{LIGHT}">'
            f'1 - Real screenshots, taken in Amoebius from the game itself</span><div style="display:flex;gap:16px">{ref}</div></div>'
            f'<div class="abs" style="left:60px;top:430px;display:flex;flex-direction:column;gap:10px"><span class="lab" style="color:{LIGHT}">'
            f'2 - The squad\'s domain lights the hangar: press a chip on any board</span><div style="display:flex;gap:16px">{lob}</div></div>'
            f'<div class="abs" style="left:60px;top:790px;display:flex;flex-direction:column;gap:10px"><span class="lab" style="color:{LIGHT}">'
            f'3 - The cameras</span><div style="display:flex;gap:16px">{cams}</div></div>')


def notes():
    def para(t):
        return f'<p style="margin:0;font-size:17px;line-height:1.5;color:#C9CDEA">{t}</p>'
    left = "".join([
        '<span class="lab">Asked (Shombith, 2026-11-05)</span>',
        para('"Remove this completely. We want the inside of the Ark in more of a 3D view, with maybe a couch and prisms, but no characters or '
             'vessels, and cards can be on top of that. A new modernized AAA screen ... a seat saved for later when a player comes." Then: '
             '"see how our game looks, make it detailed, raw and scary, use in-game prisms, environments", "a noisy lobby with a lot of stuff", '
             '"take real game screenshots", "a board showing the HyperSea galaxy or sector location, and a window outside which we can see the cell", "maybe a zoomed in more aesthetic view, do not show a real couch", "check the actual model of the Ark: it should look like the inside".'),
        '<span class="lab" style="margin-top:8px">How the room is made</span>',
        para("It is the real Ark (ArkReverse.fbx, Garrett's model): its hangar in the prow, between the jaws - the bay of arched frames, "
             "the three launch pads for the three escorts (one per seat), the floor lights, the glowing back wall and its tunnel, the "
             "clam-jaw doors opened with the model's own hangaropen take - in the game's ship materials, the trim in the squad's domain. "
             "The hull's prisms sit in the model's own sockets at their real sizes. Amoebius was fixed so it runs the current game again, "
             "and the game was screenshotted in it (Compare, row 1): the prisms are the game's read (black faces, a fresnel rim), in "
             "Nocturne. Dressed raw and noisy: the squad's prisms hover over the pads (the kept seat an outline), the HyperSea board "
             "between the arches (the Sector chart, the Ark at Tumult; the galaxy opposite), heaps of debris, danger blades at the mouth, "
             "hanging chains, junk in the haze. Out of the open mouth: the cell."),
    ])
    right = "".join([
        '<span class="lab">The six</span>',
        para("A Seat placards: each card over its pad's prism, a hairline down to it. B Rail: the hangar clear, the seats in one rail at the "
             "bottom. C The board wall: turned to the HyperSea board, the seats stacked right. D From the cell: outside, the real Ark with its "
             "mouth open, the seats in a column. E Slate: one glass slate lists the squad. F One seat at a time: the chosen seat large, LB / RB."),
        para("Every board is interactive: the domain chips relight the room (Jade, Ruby, Gold), the seat cards select (the Jade ring), the kept "
             "seat invites its friend (Lime) or seats an AI until then, Breakers is Kestrel's so its chips refuse, TAKE INTO ARENA is Ruby."),
        '<span class="lab" style="margin-top:8px">Recommendation: A</span>',
        para("The three pads are the Ark's own seats for the squad, and A is the only one where each card visibly belongs to its pad, so the "
             "kept seat reads as the empty pad waiting for someone; the open mouth and the cell stay in view above the cards."),
        '<span class="lab" style="margin-top:8px">Differs from the build</span>',
        para("Today the tab is the Rev 31 deck on the menu's sea. This puts the Ark's hangar behind it: either a baked backdrop per domain (these "
             "renders) or the ArkReverse model's hangar as a small scene with the game's real prisms (it would breathe; the doors, the pads and "
             "the domain trim would react). Seat data, Make, Join, Edit and Take "
             "into are unchanged. The build waits on a pick."),
    ])
    return (f'<div class="abs" style="left:60px;top:44px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:30px">The Ark\'s lobby: the Squads tab in the Ark\'s own hangar, six ways</span></div>'
            f'<div class="abs" style="left:60px;top:130px;width:860px;display:flex;flex-direction:column;gap:12px">{left}</div>'
            f'<div class="abs" style="left:1000px;top:130px;width:860px;display:flex;flex-direction:column;gap:12px">{right}</div>')


def main():
    files = {f"R{REV}Compare.dc.html": board(f"Rev {REV} - compare", compare())}
    for k, (name, cam, line) in OPTS.items():
        files[f"R{REV}{k}.dc.html"] = board(f"Rev {REV} {k} - {name}", SCREENS[k](), cam)
    files[f"R{REV}Notes.dc.html"] = board(f"Rev {REV} - notes", notes(), h=900)
    for n, s in files.items():
        (ROOT / n).write_text(s, encoding="utf-8")
        print(f"{n:24} {len(s):>7}")


if __name__ == "__main__":
    main()
