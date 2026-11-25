#!/usr/bin/env python3
"""Rev 127 boards: avatar borders that sit on the domain halo, six animated rings (Shombith, 2026-11-23).

Asked: "The avatar backdrop have the glowing circle but the actual borders of the avatars do not align with it.
Give me 6 variations on the design board for avatars with their animated glowing borders."

Today the halo (PartySlotDomainGlow, ConnectingPlayerRoster, RematchVoteRoster) is a round radial glow 1.22x the
avatar rect, drawn behind a SQUARE avatar: the art's corners reach 0.71 of its width from the centre, past the
halo's 0.61, so the square pokes out of the circle and no border follows it. Every option here shares one geometry:
the avatar is cut to a circle, its ring sits on that circle's edge, and the halo is centred on the same point at
1.3x the diameter, so the glow starts where the ring ends. The options differ only in the ring and its motion.

    python3 Docs/MenuRedesignB/rounds/r127_avatar_borders.py <root>     writes <root>/project/R127*.dc.html
"""
import html
import json
import os
import sys

REV = 127
CELL = "/_blob/423125b7f05629e899e1ec1f3e076f05"   # the menu cell, as the menu boards use it
AV = {
    "Anvil":  "/_blob/2bec37a53b5e64db656e2599e2f69167",
    "Bandit": "/_blob/353a02a7cb4c3379cf34db8f76715690",
    "Arilia": "/_blob/7377e4bf0f92a484e20bc497154a8baf",
    "Athara": "/_blob/7204fc34071ac04460d1d4dfc256266a",
    "Belko":  "/_blob/f23c96a9033304537db93810f99406ef",
    "Aane":   "/_blob/941eaa0748101230a083b807e5512af7",
}
# Domain colours from the style tokens (Docs/UI/froglet-tokens.json, deep-sea).
JADE, RUBY, GOLD = "#21C0BD", "#BD1BC8", "#FF9B11"
HALO = 1.3          # halo diameter / avatar diameter (the game uses 1.22 today)

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
.abs{{position:absolute}}
.lab{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.16em;text-transform:uppercase;color:#747BAD}}
.cap{{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}}
.body{{font-size:18px;line-height:1.5;color:#B8C0F0}}
.sl{{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}}
.glass{{background:rgba(0,4,31,.78);backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px)}}
.edge{{box-shadow:inset 0 0 0 1px #434C89}}
.seg{{box-sizing:border-box;height:44px;padding:0 18px;border:0;cursor:pointer;background:rgba(0,4,31,.78);color:#747BAD;box-shadow:inset 0 0 0 1px #434C89;font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:14px;letter-spacing:.14em;text-transform:uppercase;display:inline-flex;align-items:center;gap:10px}}
.seg[aria-pressed=true]{{color:#E6E9FF;box-shadow:inset 0 0 0 1px #747BAD,inset 0 -2px 0 #E6E9FF}}
.seg:focus-visible{{outline:2px solid #00D4FF;outline-offset:3px}}
.dot{{width:10px;height:10px;border-radius:50%;flex:none}}
.ell{{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}}
/* one geometry for every option: a round avatar, its ring on the edge, the halo centred at {halo}x */
.av{{position:relative;width:var(--d);height:var(--d);flex:none}}
.av>*{{position:absolute;box-sizing:border-box;border-radius:50%;pointer-events:none}}
.face{{inset:0;width:100%;height:100%;object-fit:cover}}
.halo{{width:calc(var(--d) * {halo});height:calc(var(--d) * {halo});left:calc(var(--d) * {hoff});top:calc(var(--d) * {hoff});background:radial-gradient(closest-side,var(--c) 0,var(--c) {hcore}%,color-mix(in srgb,var(--c) 38%,transparent) {hmid}%,transparent 100%);opacity:.8}}
.base{{inset:0}}
.guide{{display:none}}
.guides .guide{{display:block;border:1px dashed rgba(230,233,255,.7)}}
.g1{{inset:0}}
.g2{{width:calc(var(--d) * {halo});height:calc(var(--d) * {halo});left:calc(var(--d) * {hoff});top:calc(var(--d) * {hoff})}}
.unk .halo,.unk .fx{{display:none}}
.unk .base{{box-shadow:inset 0 0 0 2px #434C89!important;animation:none!important;filter:none!important;background:none!important;opacity:1!important;-webkit-mask:none!important;mask:none!important}}
.still *{{animation-play-state:paused!important}}
.band{{-webkit-mask:radial-gradient(farthest-side,transparent calc(100% - var(--w)),#000 calc(100% - var(--w) + 1px));mask:radial-gradient(farthest-side,transparent calc(100% - var(--w)),#000 calc(100% - var(--w) + 1px))}}
@keyframes spin{{to{{transform:rotate(360deg)}}}}
@keyframes spinr{{to{{transform:rotate(-360deg)}}}}
@keyframes breath0{{0%,100%{{opacity:.45}}50%{{opacity:.9}}}}
{css}
</style>
</helmet>
"""

TAIL = """<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
{logic}
</script>
</body>
</html>
"""

# ---- the six rings ------------------------------------------------------------------------------------------------
# below: elements drawn under the face (glow that spills outward); above: elements over the face's edge.
OPTIONS = {
    "A": dict(
        name="Breath", short="one ring that breathes with the halo",
        line="A 3 px ring on the avatar's edge. It and the halo breathe together at the game's 0.7 Hz, so the glow reads as coming from the ring.",
        motion="Alpha only, 0.7 Hz, ring and halo in phase. Nothing moves or turns.",
        css="""@keyframes breath{0%,100%{opacity:.5}50%{opacity:1}}
.optA .halo{animation:breath 1.43s ease-in-out infinite}
.optA .base{box-shadow:inset 0 0 0 calc(var(--d) * .016 + 1.6px) var(--c),0 0 calc(var(--d) * .05) 0 var(--c);animation:breath 1.43s ease-in-out infinite}""",
        below="", above=""),
    "B": dict(
        name="Orbit", short="a hairline ring with one bright arc circling it",
        line="A hairline ring in the domain colour; one bright arc runs round it every 4 s. The halo holds still.",
        motion="One arc, one turn every 4 s, linear.",
        css=""".optB .base{box-shadow:inset 0 0 0 1.5px color-mix(in srgb,var(--c) 65%,transparent)}
.optB .arc{inset:calc(var(--d) * -.02);--w:calc(var(--d) * .03 + 2px);background:conic-gradient(from 0deg,transparent 0 62%,color-mix(in srgb,var(--c) 40%,transparent) 78%,var(--c) 94%,#FFFFFF 100%);animation:spin 4s linear infinite;filter:drop-shadow(0 0 calc(var(--d) * .03) var(--c))}""",
        below="", above='<div class="fx arc band"></div>'),
    "C": dict(
        name="Dial", short="two counter-turning segmented rings",
        line="A solid 2 px ring on the edge, and two segmented rings between it and the halo's rim that turn slowly in opposite directions.",
        motion="Outer segments one turn in 18 s, inner in 26 s the other way.",
        css=""".optC .base{box-shadow:inset 0 0 0 2px var(--c),0 0 calc(var(--d) * .04) 0 var(--c)}
.optC .d1{inset:calc(var(--d) * -.075);--w:calc(var(--d) * .02 + 1.5px);background:repeating-conic-gradient(var(--c) 0 7deg,transparent 7deg 15deg);animation:spin 18s linear infinite}
.optC .d2{inset:calc(var(--d) * -.035);--w:calc(var(--d) * .01 + 1px);background:repeating-conic-gradient(color-mix(in srgb,var(--c) 70%,#FFFFFF) 0 2deg,transparent 2deg 6deg);opacity:.7;animation:spinr 26s linear infinite}
.optC .halo{opacity:.55}""",
        below='<div class="fx d1 band"></div><div class="fx d2 band"></div>', above=""),
    "D": dict(
        name="Ripple", short="a ring that sends a wave out to the halo's edge",
        line="A 3 px ring on the edge sends a thin wave outward that fades exactly at the halo's rim, two waves 1.2 s apart.",
        motion="Each wave 2.4 s, eased out, scale 1.0 to 1.3 (the halo's size).",
        css="""@keyframes ripple{0%{transform:scale(1);opacity:.9}100%{transform:scale(1.3);opacity:0}}
.optD .base{box-shadow:inset 0 0 0 calc(var(--d) * .016 + 1.6px) var(--c)}
.optD .halo{opacity:.55}
.optD .rp{inset:0;border:calc(var(--d) * .008 + 1px) solid var(--c);animation:ripple 2.4s cubic-bezier(.22,1,.36,1) infinite}
.optD .rp2{animation-delay:-1.2s}""",
        below='<div class="fx rp"></div><div class="fx rp rp2"></div>', above=""),
    "E": dict(
        name="Current", short="a turning gradient ring with its own glow",
        line="A 4 px ring filled with a gradient of the domain colour and white that turns, with its own soft glow over the halo.",
        motion="The gradient turns once every 3 s; the glow travels with its bright part.",
        css=""".optE .base{inset:calc(var(--d) * -.01);--w:calc(var(--d) * .025 + 2px);background:conic-gradient(from 0deg,var(--c),#FFFFFF 18%,var(--c) 36%,color-mix(in srgb,var(--c) 35%,transparent) 60%,var(--c) 100%);animation:spin 3s linear infinite;filter:drop-shadow(0 0 calc(var(--d) * .035) var(--c));-webkit-mask:radial-gradient(farthest-side,transparent calc(100% - var(--w)),#000 calc(100% - var(--w) + 1px));mask:radial-gradient(farthest-side,transparent calc(100% - var(--w)),#000 calc(100% - var(--w) + 1px))}""",
        below="", above=""),
    "F": dict(
        name="Corona", short="an eclipse rim, a crescent of light that slowly turns",
        line="A dim hairline ring with a crescent of light on one side, as if lit from behind; the crescent and the halo's bright side turn together.",
        motion="One turn every 12 s, linear; the halo is brighter on the crescent's side.",
        css=""".optF .base{box-shadow:inset 0 0 0 1px color-mix(in srgb,var(--c) 45%,transparent)}
.optF .halo{opacity:.45}
.optF .cr{inset:calc(var(--d) * -.03);--w:calc(var(--d) * .06 + 2px);background:conic-gradient(from 0deg,transparent 0 50%,color-mix(in srgb,var(--c) 60%,transparent) 66%,#FFFFFF 75%,var(--c) 84%,transparent 100%);filter:blur(calc(var(--d) * .006 + .4px));animation:spin 12s linear infinite}
.optF .cg{width:calc(var(--d) * 1.3);height:calc(var(--d) * 1.3);left:calc(var(--d) * -.15);top:calc(var(--d) * -.15);background:conic-gradient(from 0deg,transparent 0 50%,var(--c) 75%,transparent 100%);-webkit-mask:radial-gradient(closest-side,transparent 70%,#000 77%,transparent 100%);mask:radial-gradient(closest-side,transparent 70%,#000 77%,transparent 100%);opacity:.7;animation:spin 12s linear infinite}""",
        below='<div class="fx cg"></div>', above='<div class="fx cr band"></div>'),
}
ORDER = "ABCDEF"


def css_all():
    hoff = -(HALO - 1) / 2
    core = round(100 / HALO, 1)               # where the avatar's edge falls inside the halo
    mid = round(core + (100 - core) * .45, 1)
    common = dict(halo=HALO, hoff=round(hoff, 3), hcore=core, hmid=mid)
    return common


def avatar(opt, src, alt, c, d, cls="", extra=""):
    """One avatar: a dotted lookup for every value that changes (src, colour, class), sizes fixed per call site."""
    o = OPTIONS[opt]
    return (f'<div class="av opt{opt} {cls}" style="--c:{c};--d:{d}px{extra}">'
            f'<div class="halo"></div>{o["below"]}'
            f'<img class="face" src="{src}" alt="{alt}">'
            f'<div class="base"></div>{o["above"]}'
            f'<div class="guide g1"></div><div class="guide g2"></div></div>')


def head(title, opts):
    c = css_all()
    css = "\n".join(OPTIONS[k]["css"] for k in opts)
    return HEAD.format(title=html.escape(title, quote=False), css=css, **c)


def backdrop(h=1080):
    return (f'<img src="{CELL}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover">'
            f'<div class="abs" style="inset:0;background:linear-gradient(180deg,rgba(0,4,31,.82),rgba(0,4,31,.66) 40%,rgba(0,1,10,.9))"></div>')


# ---- an option board -----------------------------------------------------------------------------------------------
OPTION_LOGIC = """const DOM = { jade: '%s', ruby: '%s', gold: '%s' };
const AV = %s;
const PARTY = [
  { n: 'Anvil', role: 'You \\u00b7 Host', d: 'jade' },
  { n: 'Bandit', role: 'Pilot', d: 'jade' },
  { n: 'Arilia', role: 'Pilot', d: 'ruby' },
  { n: 'Athara', role: 'Joining', d: '' },
];
const LOBBY = [
  { n: 'Anvil', d: 'jade' }, { n: 'Bandit', d: 'jade' }, { n: 'Belko', d: 'jade' },
  { n: 'Arilia', d: 'ruby' }, { n: 'Aane', d: 'ruby' }, { n: 'Athara', d: 'gold' },
];
const NAMES = { jade: 'Jade', ruby: 'Ruby', gold: 'Gold', '': 'Not known yet' };
function seat(s) { return { img: AV[s.n], name: s.n, role: s.role || NAMES[s.d], c: DOM[s.d] || '#434C89', cls: s.d ? '' : 'unk' }; }
class Component extends DCLogic {
constructor(p) { super(p); this.state = { dom: 'jade', guides: false, motion: true }; }
renderVals() {
  const st = this.state;
  const pick = (d) => ({ label: NAMES[d], dot: DOM[d] || '#434C89', pressed: st.dom === d ? 'true' : 'false', go: () => this.setState({ dom: d }) });
  return {
    root: (st.guides ? 'guides ' : '') + (st.motion ? '' : 'still'),
    hero: { c: DOM[st.dom] || '#434C89', cls: st.dom ? '' : 'unk', img: AV.Anvil, name: NAMES[st.dom] },
    doms: [pick('jade'), pick('ruby'), pick('gold'), pick('')],
    guidesPressed: st.guides ? 'true' : 'false',
    motionPressed: st.motion ? 'true' : 'false',
    motionLabel: st.motion ? 'Motion on' : 'Motion off',
    toggleGuides: () => this.setState({ guides: !st.guides }),
    toggleMotion: () => this.setState({ motion: !st.motion }),
    party: PARTY.map(seat),
    lobby: LOBBY.map(seat),
  };
}
}"""


def option_board(k, title, recommended):
    o = OPTIONS[k]
    rec = ' <span class="cap" style="font-size:14px;color:#99FF80;margin-left:14px">Recommended</span>' if recommended else ""
    hero = avatar(k, "{{hero.img}}", "Anvil, your avatar", "{{hero.c}}", 340, "{{hero.cls}}")
    p_av = avatar(k, "{{s.img}}", "{{s.name}}", "{{s.c}}", 72, "{{s.cls}}")
    l_av = avatar(k, "{{s.img}}", "{{s.name}}", "{{s.c}}", 96, "{{s.cls}}")
    body = f"""<div class="{{{{root}}}}" style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
{backdrop()}
<div class="abs" style="left:96px;top:72px;width:1728px">
<div class="lab">Rev {REV} {k} &middot; Avatar border</div>
<div style="display:flex;align-items:baseline;margin-top:10px"><span class="cap" style="font-size:48px;font-weight:700;letter-spacing:.08em">{o['name']}</span>{rec}</div>
<p class="body" style="margin:10px 0 0;max-width:1180px">{html.escape(o['line'])}</p>
</div>
<div class="abs" style="left:96px;top:300px;width:760px;height:700px">
<div class="abs" style="left:210px;top:40px">{hero}</div>
<div class="abs" style="left:0;top:470px;width:760px;display:flex;flex-direction:column;align-items:center;gap:18px">
<div class="lab">Domain</div>
<div style="display:flex;gap:10px"><sc-for list="{{{{doms}}}}" as="d" hint-placeholder-count="4"><button class="seg" type="button" aria-pressed="{{{{d.pressed}}}}" onClick="{{{{d.go}}}}"><span class="dot" style="background:{{{{d.dot}}}}"></span>{{{{d.label}}}}</button></sc-for></div>
<div style="display:flex;gap:10px"><button class="seg" type="button" aria-pressed="{{{{guidesPressed}}}}" onClick="{{{{toggleGuides}}}}">Show the circles</button><button class="seg" type="button" aria-pressed="{{{{motionPressed}}}}" onClick="{{{{toggleMotion}}}}">{{{{motionLabel}}}}</button></div>
<div class="lab" style="text-transform:none;letter-spacing:.04em;font-family:Aldrich,sans-serif;font-size:15px">{html.escape(o['motion'])}</div>
</div>
</div>
<div class="abs glass sl edge" style="left:984px;top:300px;width:840px;height:330px">
<div class="lab abs" style="left:32px;top:24px">Party</div>
<div class="abs" style="left:32px;top:72px;width:776px;display:grid;grid-template-columns:1fr 1fr;row-gap:36px;column-gap:24px">
<sc-for list="{{{{party}}}}" as="s" hint-placeholder-count="4"><div style="display:flex;align-items:center;gap:26px;padding-left:12px">{p_av}<div style="min-width:0"><div class="cap ell" style="font-size:20px;color:#E6E9FF">{{{{s.name}}}}</div><div class="ell" style="font-size:15px;color:#747BAD;margin-top:4px">{{{{s.role}}}}</div></div></div></sc-for>
</div>
</div>
<div class="abs glass sl edge" style="left:984px;top:660px;width:840px;height:340px">
<div class="lab abs" style="left:32px;top:24px">Match lobby &middot; 3 v 2 v 1</div>
<div class="abs" style="left:32px;top:84px;width:776px;display:flex;justify-content:space-between">
<sc-for list="{{{{lobby}}}}" as="s" hint-placeholder-count="6"><div style="width:120px;display:flex;flex-direction:column;align-items:center;gap:22px;padding-top:14px">{l_av}<div style="text-align:center"><div class="cap" style="font-size:16px">{{{{s.name}}}}</div><div style="font-size:14px;color:#747BAD;margin-top:2px">{{{{s.role}}}}</div></div></div></sc-for>
</div>
</div>
</div>
</x-dc>
"""
    logic = OPTION_LOGIC % (JADE, RUBY, GOLD, json.dumps(AV))
    return head(title, [k]) + body + TAIL.format(w=1920, h=1080, logic=logic)


# ---- the compare board ---------------------------------------------------------------------------------------------
def compare_board(title):
    cols = []
    for i, k in enumerate(ORDER):
        o = OPTIONS[k]
        x = 600 + i * 210
        big = avatar(k, AV["Anvil"], "Anvil", JADE, 150)
        s1 = avatar(k, AV["Arilia"], "Arilia", RUBY, 64)
        s2 = avatar(k, AV["Athara"], "Athara", GOLD, 64)
        cols.append(f"""<div class="abs" style="left:{x}px;top:250px;width:190px;display:flex;flex-direction:column;align-items:center">
<div class="cap" style="font-size:22px">{k} &middot; {o['name']}</div>
<div class="body" style="font-size:14px;line-height:1.4;text-align:center;height:60px;margin-top:6px;color:#747BAD">{html.escape(o['short'])}</div>
<div style="margin-top:40px">{big}</div>
<div style="display:flex;gap:40px;margin-top:70px">{s1}{s2}</div>
<div style="margin-top:56px">{avatar(k, AV['Belko'], 'Belko, domain not known yet', JADE, 64, 'unk')}</div>
</div>""")
    today = f"""<div class="abs" style="left:96px;top:250px;width:420px;display:flex;flex-direction:column;align-items:center">
<div class="cap" style="font-size:22px">Today</div>
<div class="body" style="font-size:14px;line-height:1.4;text-align:center;height:60px;margin-top:6px;color:#747BAD">the square art over the round halo at 1.22x</div>
<div style="position:relative;width:150px;height:150px;margin-top:40px">
<div class="abs" style="width:183px;height:183px;left:-16.5px;top:-16.5px;border-radius:50%;background:radial-gradient(closest-side,{JADE},color-mix(in srgb,{JADE} 30%,transparent) 70%,transparent);animation:breath0 1.43s ease-in-out infinite"></div>
<img src="{AV['Anvil']}" alt="Anvil, today" class="abs" style="inset:0;width:150px;height:150px;object-fit:cover;box-shadow:inset 0 0 0 2px #E6E9FF">
<div class="abs" style="inset:0;box-shadow:inset 0 0 0 2px rgba(230,233,255,.8)"></div>
<div class="abs" style="width:183px;height:183px;left:-16.5px;top:-16.5px;border-radius:50%;border:1px dashed rgba(230,233,255,.7)"></div>
</div>
<p class="body" style="font-size:15px;margin:70px 0 0;text-align:center;max-width:360px">The corners reach 0.71 of the width from the centre; the halo ends at 0.61. The square's border crosses the glow at eight points and follows none of it.</p>
</div>
<div class="abs" style="left:552px;top:240px;width:1px;height:740px;background:#434C89"></div>"""
    rows = """<div class="lab abs" style="left:566px;top:590px;writing-mode:vertical-rl;transform:rotate(180deg)">In a lobby</div>
<div class="lab abs" style="left:566px;top:700px;writing-mode:vertical-rl;transform:rotate(180deg)">Not known</div>"""
    body = f"""<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
{backdrop()}
<div class="abs" style="left:96px;top:72px;width:1728px">
<div class="lab">Rev {REV} &middot; Compare</div>
<div class="cap" style="font-size:40px;font-weight:700;letter-spacing:.08em;margin-top:10px">Avatar borders on the domain halo</div>
<p class="body" style="margin:10px 0 0;max-width:1500px">Every option cuts the avatar to a circle, puts its ring on that circle and centres the halo on it at 1.3x, so the glow starts where the ring ends. Jade large, Ruby and Gold at party size, and a pilot whose domain is not known yet (no halo, a quiet ring).</p>
</div>
{today}
{rows}
{''.join(cols)}
</div>
</x-dc>
"""
    logic = "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}"
    return head(title, ORDER) + body + TAIL.format(w=1920, h=1080, logic=logic)


# ---- the notes board -----------------------------------------------------------------------------------------------
def notes_board(title):
    items = "".join(
        f'<li style="margin-bottom:8px"><span class="cap" style="color:#E6E9FF">{k} {OPTIONS[k]["name"]}</span>: {html.escape(OPTIONS[k]["motion"])}</li>'
        for k in ORDER)
    body = f"""<div style="width: 1920px; height: 900px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
<div class="abs" style="left:96px;top:72px;width:1728px">
<div class="lab">Rev {REV} &middot; Notes</div>
<div class="cap" style="font-size:36px;font-weight:700;letter-spacing:.08em;margin-top:10px">What the round decides</div>
</div>
<div class="abs body" style="left:96px;top:190px;width:820px">
<div class="lab" style="margin-bottom:10px">The problem</div>
<p style="margin:0 0 22px">The domain halo is a round glow 1.22x the avatar's rect (PartySlotDomainGlow in the party panel, ConnectingPlayerRoster and RematchVoteRoster in matches). The avatar art is square, so its corners stand outside the circle and its border is a square: nothing follows the glow.</p>
<div class="lab" style="margin-bottom:10px">What every option shares</div>
<p style="margin:0 0 22px">The avatar is masked to a circle. The ring sits on that circle's edge. The halo is centred on the same point at 1.3x (up from 1.22, so the glow has room outside the ring) and starts at full colour exactly at the ring. A pilot whose domain is not known yet gets no halo and a quiet 2 px line ring, as today's "draws only what it knows" rule.</p>
<div class="lab" style="margin-bottom:10px">Recommended: A Breath</div>
<p style="margin:0">It keeps the motion the game already has (alpha only, 0.7 Hz, the halo's own breath) and makes the ring part of it, so it is the quietest answer and costs one ring sprite. B and D are the next choices if the party should feel more alive; C, E and F are louder and read best large, at 150 px and up.</p>
</div>
<div class="abs body" style="left:1004px;top:190px;width:820px">
<div class="lab" style="margin-bottom:10px">Motion per option</div>
<ul style="margin:0 0 22px;padding-left:20px">{items}</ul>
<div class="lab" style="margin-bottom:10px">What the pick changes in Unity</div>
<p style="margin:0">One round mask on the avatar Image, one ring Image over it, the halo resized to 1.3x and its sprite starting at the ring's radius. The three halo call sites become one component (Docs/PartySystem/TODOS.md TODO-12), so the party panel, the connecting roster and the rematch vote all get the same border. Colours are the style tokens' Jade, Ruby and Gold.</p>
</div>
</div>
</x-dc>
"""
    logic = "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}"
    return head(title, []) + body + TAIL.format(w=1920, h=900, logic=logic)


def main():
    root = sys.argv[1]
    proj = os.path.join(root, "project")
    idx = json.load(open(os.path.join(proj, "canvas.json")))
    boards = idx["boards"]

    def write(name, text):
        with open(os.path.join(proj, name), "w") as f:
            f.write(text)

    write(f"R{REV}Compare.dc.html", compare_board(boards[f"R{REV}Compare.dc.html"]["title"]))
    for k in ORDER:
        write(f"R{REV}{k}.dc.html", option_board(k, boards[f"R{REV}{k}.dc.html"]["title"], k == "A"))
    write(f"R{REV}Notes.dc.html", notes_board(boards[f"R{REV}Notes.dc.html"]["title"]))
    print("wrote", 8, "boards to", proj)


if __name__ == "__main__":
    main()
