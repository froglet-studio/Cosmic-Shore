#!/usr/bin/env python3
"""Rev 70 boards: the Voyage window redrawn in the Ark's look, a vessel in one press, a course that stays put, and the
quest tracker as its own window (Frogma tab "Voyages").

Shombith, 2026-11-08: "Make new mockups for the Voyages, its not cool none of the designs are good and match other
better parts of the game. For selecting vessels also what do you mean in order to select a vessel I have to click 22
times also more when I change domain or team as well the course is changing. Make this better. Create a new page in
/frogma Voyages or all voyage UI. Move the quest tracker out of the voyages, make it into its seperate window like
profile and show that mockup as well in the redesign board maybe."

The kit is the style guide's (Docs/UI/froglet-tokens.json): token colours only, the type scale (48 44 36 28 22 20 18
15 14), one Lime call to action per screen, Set sail in Ruby (it launches, like Play), the selection in #00D4FF, a
domain colour only on a team thing, the Codex's element shapes, and at most 60 visual elements a screen
(Tools/Build/check_mockup.py). The art is the canvas's own: the Ark rooms of Rev 61 / 67 (the Ark tab), the voyage
maps of Rev 46, the hull renders, the pilot portraits and the quest art of Rev 14 / 43.

    python3 Docs/MenuRedesignB/rounds/r70_voyages.py <root> 70

Rev 68 and 69 went to the Fleet Codex while these were drawn; the round takes the next free number.
"""
import json
import sys
from pathlib import Path

ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2]) if len(sys.argv) > 2 else 70
ROOT.mkdir(parents=True, exist_ok=True)


def blob(h):
    return "/_blob/" + h


# ── The canvas's own art ────────────────────────────────────────────────────────────────────────────────────────────
ROOM = {"Jade": blob("c2a9b2634cf0ccae357e84f5a3ff5917"), "Ruby": blob("cd2a783f5d168b8a448e948e3d67122a"),
        "Gold": blob("48075ef29368f88768c579631ceb3f3d")}          # the Ark's squads room, Nocturne, per domain (Rev 67)
NAV = [("CODEX", "ec690d6b90d6fcdbc430b9bbaacf28a9"), ("HANGAR", "34f5a50f1275fef42808fe9df3bf6af5"),
       ("HOME", "a5bd4d8d1de7b494567fc3de75525e1e"), ("ARK", "e5b050c5a676193c650e94d873ad40e2"),
       ("PORT", "d0e850bc927559f1a499868d26f2a3de")]
HULL = {"Sparrow": blob("a2a4139d8e2822885778740d4f2fb92e"), "Dolphin": blob("e4ebf079ba441cd7141a2c8bc092d8ea"),
        "Squirrel": blob("694ae84ec6db347c4f034c8e03b56991"), "Rhino": blob("48e972fa9d369a737bacf39f37baf59f"),
        "Manta": blob("ab51cbb598b5a1e912416244829d134c"), "Serpent": blob("215e6ad15de9f075e7a45e7640a12f09")}
PILOT = {"Shrapnel": blob("109d50c83786e87bc77b272531aadd87"), "Feist": blob("4f08e5dc0445758132618029646325ef"),
         "Longshift": blob("a31623aedd55f09eb0f08c21b3f141da"), "Flux": blob("858b88d97e9cf77b716845a5c67979fa")}
ELEMENT = {"Charge": blob("1c70115a6fb1a56b2e125564d0300b98"), "Mass": blob("10bf4fb3c4924a10f0e5d5a1f71924a5"),
           "Space": blob("eea67d233893eb4fe28dfeb466d29b04"), "Time": blob("6af82be95fa71d9b3add315514cfd089")}
QUEST_ART = {"Scurry": blob("e991a3576a32930173b452e9806c08d3"), "Hex Race": blob("870fa2d022d70e4eadd5963ce81b5328"),
             "Joust": blob("f73f095638ef9ad060a2a8bb097ea2af"), "Maelstrom": blob("360243baa0fb047a2c955d06f7f663a0"),
             "Vessel Hangar": blob("7d5ddbe91b12c281ac5d6a4b4ad38239")}

# The voyages of Rev 46 (the board's own content: the crew who found them, the real course on the HyperSea map).
VOYAGES = [
    {"n": 1, "title": "Carry sealed news to Tollmoor", "place": "Tollmoor", "min": "27 min", "ly": "540 ly", "i": 1,
     "crew": "Merrin Tal", "line": "Reach Tollmoor with the Ark standing. The news aboard will change what Tollmoor knows.",
     "placeLine": "A toll port on the edge of Tumult's bubble. The dock crews hear everything first.",
     "wps": 2, "map": blob("9778fd69fcae1f8cb003a9bbd8508a56"), "pos": "62% 58%"},
    {"n": 2, "title": "Open the vaults at Lantern Reach", "place": "Lantern Reach", "min": "29 min", "ly": "580 ly", "i": 2,
     "crew": "Oriel Vance", "line": "Crack a vault with a payload, carry its artifact back to the Ark and stow it.",
     "placeLine": "Super-shielded shells drift in a lit shoal, each with an artifact inside.",
     "wps": 7, "map": blob("b96bc9cef23de987e4ed42294befc28a"), "pos": "38% 46%"},
    {"n": 3, "title": "Raid the thieves' cache at Gallowglass", "place": "Gallowglass", "min": "47 min", "ly": "940 ly", "i": 2,
     "crew": "Cass Morrow", "line": "Take what the thieves took from their hold in the reefs, and stow it before they notice.",
     "placeLine": "The thieves' hold in the reefs toward the centre.",
     "wps": 2, "map": blob("037d66eeef857313705b96448c91f4a0"), "pos": "50% 40%"},
    {"n": 4, "title": "Hunt the creature in The Maw", "place": "The Maw", "min": "49 min", "ly": "980 ly", "i": 3,
     "crew": "Brisk Amadou", "line": "Something big feeds in The Maw. Find it, bring it down, bring back what it carried.",
     "placeLine": "A dark gulf where the sea runs thick and the big feeders hunt.",
     "wps": 2, "map": blob("da47aa2e4c65d63e53711ebfe6fc4ac2"), "pos": "48% 38%"},
]
for v in VOYAGES:
    v["thumbPos"] = v["pos"]

# The squads (Rev 61 / 62's book): yours, and one you joined.
SQUADS = [
    {"name": "Skim Wing", "dom": "Jade", "mine": True, "owner": "",
     "seats": [{"role": "Leader", "who": "You", "v": "Sparrow", "p": "Shrapnel", "e": "Charge", "a": "Extra Bay"},
               {"role": "Rogue One", "who": "Halcyon", "v": "Dolphin", "p": "Riptide", "e": "Space", "a": "Seed Pod"},
               {"role": "Rogue Two", "who": "AI wingmate", "v": "Squirrel", "p": "Trace", "e": "Time", "a": "No artifact"}]},
    {"name": "Breakers", "dom": "Ruby", "mine": False, "owner": "Kestrel",
     "seats": [{"role": "Leader", "who": "Kestrel", "v": "Rhino", "p": "Tomahawk", "e": "Mass", "a": "Long Edge"},
               {"role": "Rogue One", "who": "You", "v": "Manta", "p": "Reaper", "e": "Charge", "a": "Tailwind"},
               {"role": "Rogue Two", "who": "AI wingmate", "v": "Serpent", "p": "Scales", "e": "Space", "a": "No artifact"}]},
]
DOMC = {"Jade": "#21C0BD", "Ruby": "#BD1BC8", "Gold": "#FF8F11"}
ARTIFACTS = ["No artifact", "Extra Bay", "Seed Pod", "Tailwind", "Long Edge"]

QUESTS = [
    {"n": "01", "name": "Scurry", "goal": "Finish a Scurry at intensity 1.", "state": "claimed"},
    {"n": "02", "name": "Hex Race", "goal": "Finish a Hex Race at intensity 1.", "state": "done"},
    {"n": "03", "name": "Joust", "goal": "Win a Joust at intensity 1.", "state": "open"},
    {"n": "04", "name": "Maelstrom", "goal": "Place in a Maelstrom.", "state": "locked"},
    {"n": "05", "name": "Vessel Hangar", "goal": "Upgrade a vessel in the Hangar.", "state": "locked"},
]
for q in QUESTS:
    q["art"] = QUEST_ART[q["name"]]

# ── The kit (tokens only) ────────────────────────────────────────────────────────────────────────────────────────────
HELMET = """<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#00010A}
.abs{position:absolute}
.lab{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.16em;text-transform:uppercase;color:#747BAD}
.cap{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}
.num{font-variant-numeric:tabular-nums}
.sl{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}
.sls{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}
.kc{display:inline-flex;align-items:center;justify-content:center;min-width:28px;height:24px;padding:0 6px;box-sizing:border-box;border:1px solid #434C89;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;color:#E6E9FF}
.btn{box-sizing:border-box;display:inline-flex;align-items:center;justify-content:center;gap:12px;height:56px;padding:0 26px;white-space:nowrap;border:0;cursor:pointer;text-decoration:none;font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:18px;letter-spacing:.14em;text-transform:uppercase;clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}
.btn.sm{height:44px;padding:0 18px;font-size:14px}
.commit{background:#A600FF;color:#FFFFFF}
.cta{background:#99FF80;color:#00041F}
.def{background:rgba(0,4,31,.78);color:#E6E9FF;box-shadow:inset 0 0 0 1px #747BAD}
.btn .kc{border-color:currentColor;color:inherit}
.pick{background:none;border:0;padding:0;margin:0;cursor:pointer;color:inherit;font:inherit;text-align:left}
.btn:focus-visible,.pick:focus-visible{outline:2px solid #00D4FF;outline-offset:3px}
.glass{background:rgba(0,4,31,.78);backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px)}
.ell{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.hull{mix-blend-mode:screen;object-fit:contain}
.set::before{content:attr(data-l);display:block;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.16em;text-transform:uppercase;color:#747BAD;margin-bottom:4px}
</style>
</helmet>"""

LOCK = ('<svg width="14" height="16" viewBox="0 0 14 16" fill="none" stroke="currentColor" stroke-width="1.6" '
        'aria-hidden="true"><path d="M3 7V5a4 4 0 018 0v2"></path><path d="M1.5 7h11v8h-11z"></path></svg>')


def page(title, body, logic, w=1920, h=1080):
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{title}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
{HELMET}
<div style="width: {w}px; height: {h}px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
{body}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
{logic}
</script>
</body>
</html>
"""


def nav(lit="HOME", crystals=True):
    items = []
    for name, icon in NAV:
        on = name == lit
        under = '<span class="abs" style="left:0;right:0;bottom:-12px;height:2px;background:#E6E9FF"></span>' if on else ""
        items.append(f'<span style="position:relative;display:flex;align-items:center;gap:10px;font-family:\'Chakra Petch\';'
                     f'font-weight:600;font-size:18px;letter-spacing:.12em;color:{"#E6E9FF" if on else "#747BAD"}">'
                     f'<img src="{blob(icon)}" alt="" style="width:26px;height:26px;object-fit:contain;opacity:{1 if on else .7}">'
                     f'{name}{under}</span>')
    bar = ('<nav aria-label="Menu screens" class="abs" style="left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.82);'
           'display:flex;align-items:center;justify-content:center;gap:48px;z-index:5">'
           '<span class="kc">LT</span>' + "".join(items) + '<span class="kc">RT</span></nav>')
    if crystals:
        bar += ('<div class="abs num" style="right:40px;top:28px;z-index:6;display:flex;align-items:center;gap:8px;'
                'font-size:20px;color:#FFAE00"><span style="width:14px;height:18px;background:#FFAE00;'
                'clip-path:polygon(50% 0,100% 35%,50% 100%,0 35%)"></span>1,240</div>')
    return bar


def tabrow(tabs, lit, back_href=None, back_label="Back"):
    """LB / RB tabs under the nav (the Ark's row), and Back on the right. A locked tab carries the lock."""
    parts = ['<span class="kc">LB</span>']
    for name, locked in tabs:
        on = name == lit
        under = ('<span class="abs" style="left:0;right:0;bottom:-16px;height:2px;background:#E6E9FF"></span>' if on else "")
        lock = LOCK if locked else ""
        parts.append(f'<span class="cap" style="position:relative;display:flex;align-items:center;gap:8px;font-size:18px;'
                     f'color:{"#E6E9FF" if on else "#747BAD"}">{lock}{name}{under}</span>')
    parts.append('<span class="kc">RB</span>')
    href = f' href="{back_href}"' if back_href else ""
    tag = "a" if back_href else "button"
    typ = "" if back_href else ' type="button"'
    back = f'<{tag} class="btn sm def"{href}{typ}><span class="kc">B</span>{back_label}</{tag}>'
    return ('<div class="abs" style="left:40px;right:40px;top:100px;height:52px;display:flex;align-items:center;'
            'justify-content:space-between;z-index:4"><div style="display:flex;align-items:center;gap:32px">'
            + "".join(parts) + f'</div>{back}</div>')


VOYAGE_TABS = [("Voyages", False), ("Saga", False), ("Episodes", True)]

# ── Shared logic pieces ──────────────────────────────────────────────────────────────────────────────────────────────
DATA = ("const V = " + json.dumps(VOYAGES) + ";\nconst SQ = " + json.dumps(SQUADS) + ";\nconst HULL = " + json.dumps(HULL)
        + ";\nconst ROOM = " + json.dumps(ROOM) + ";\nconst DOMC = " + json.dumps(DOMC) + ";\n")

VOYAGE_VALS = """
  const v = V[st.v], q = SQ[st.sq];
  const voyage = { kicker: 'Voyage ' + v.n + ' of ' + V.length + ' - found by ' + v.crew, title: v.title, line: v.line,
    ly: v.ly, min: v.min, intensity: 'Intensity ' + v.i, map: v.map, pos: v.pos, place: v.place, placeLine: v.placeLine,
    course: 'Tumult to ' + v.place + ' - ' + v.wps + ' waypoints' };
  const rail = V.map((x, i) => ({ title: x.title, sub: x.place + ' - ' + x.min, map: x.map, pos: x.thumbPos,
    edge: i === st.v ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(116,123,173,.45)',
    pressed: i === st.v ? 'true' : 'false', pick: () => this.setState({ v: i }) }));
  const presets = SQ.map((s, i) => ({ name: s.name, pressed: i === st.sq ? 'true' : 'false',
    edge: i === st.sq ? 'inset 3px 0 0 ' + DOMC[s.dom] + ', inset 0 0 0 1px #00D4FF' : 'inset 3px 0 0 ' + DOMC[s.dom] + ', inset 0 0 0 1px rgba(116,123,173,.45)',
    col: i === st.sq ? '#E6E9FF' : '#747BAD', pick: () => this.setState({ sq: i }) }));
  const formation = q.seats.map((s) => ({ hull: HULL[s.v], alt: s.v, who: (s.who === 'You' ? 'You' : s.who) + ' - ' + s.v }));
  const squad = { name: q.name, room: ROOM[q.dom], dc: DOMC[q.dom] };
"""


def a_board():
    rail = """<div class="abs" style="left:40px;top:852px;width:1180px;display:grid;grid-template-columns:repeat(4,1fr);gap:20px">
<sc-for list="{{rail}}" as="r" hint-placeholder-count="4"><button class="pick glass sl" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" style="height:176px;box-sizing:border-box;padding:22px 22px 0;display:flex;flex-direction:column;justify-content:space-between;box-shadow:{{r.edge}};font-size:20px;line-height:1.3;color:#E6E9FF">{{r.title}}<span style="display:block;padding-bottom:20px;font-size:15px;color:#747BAD">{{r.sub}}</span></button></sc-for>
</div>"""
    chart = """<div class="abs sl" style="left:40px;top:180px;width:1180px;height:640px;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89"><img src="{{voyage.map}}" alt="The course on the HyperSea map" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{voyage.pos}};filter:brightness(.82) saturate(.85)"><span class="abs glass sls" style="left:24px;bottom:24px;padding:12px 18px;font-size:18px">{{voyage.course}}</span></div>"""
    right = """<div class="abs" style="left:1268px;top:180px;width:612px;display:flex;flex-direction:column;gap:14px">
<span class="lab">{{voyage.kicker}}</span>
<span style="font-size:44px;line-height:1.08">{{voyage.title}}</span>
<span style="font-size:18px;line-height:1.45;color:#747BAD">{{voyage.line}}</span>
<div class="num" style="display:flex;gap:34px;margin-top:6px;font-size:28px"><span>{{voyage.ly}}</span><span>{{voyage.min}}</span><span>{{voyage.intensity}}</span></div>
</div>
<div class="abs glass sl" style="left:1268px;top:490px;width:612px;height:430px;box-sizing:border-box;padding:24px">
<div style="display:flex;gap:12px"><sc-for list="{{presets}}" as="p" hint-placeholder-count="2"><button class="pick sls" type="button" aria-pressed="{{p.pressed}}" onClick="{{p.pick}}" style="padding:10px 18px;background:rgba(0,4,31,.6);box-shadow:{{p.edge}};font-family:'Chakra Petch';font-weight:600;font-size:15px;letter-spacing:.12em;text-transform:uppercase;color:{{p.col}}">{{p.name}}</button></sc-for></div>
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:26px"><sc-for list="{{formation}}" as="f" hint-placeholder-count="3"><div style="display:flex;flex-direction:column;align-items:center;gap:10px"><img class="hull" src="{{f.hull}}" alt="{{f.alt}}" style="width:186px;height:170px"><span class="ell" style="max-width:176px;font-size:15px;color:#E6E9FF">{{f.who}}</span></div></sc-for></div>
<a class="btn sm def abs" href="R{REV}Crew.dc.html" style="left:24px;bottom:24px"><span class="kc">X</span>Ark crew</a>
</div>
<a class="btn commit abs" href="#" style="left:1268px;top:964px;width:612px"><span class="kc">A</span>Set sail</a>"""
    body = ('<img src="{{squad.room}}" alt="Inside the Ark, your squad\'s room" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover">'
            '<div class="abs" style="inset:0;background:linear-gradient(90deg,rgba(0,1,10,.3) 0%,rgba(0,1,10,.22) 55%,rgba(0,1,10,.8) 66%,rgba(0,1,10,.88) 100%)"></div>'
            + nav() + tabrow(VOYAGE_TABS, "Voyages") + chart + right + rail)
    logic = DATA + """class Component extends DCLogic {
constructor(p) { super(p); this.state = { v: 1, sq: 0 }; }
renderVals() {
  const st = this.state;""" + VOYAGE_VALS + """  return { voyage, rail, presets, formation, squad };
}
}"""
    return page(f"Rev {REV} A - Bridge: the HyperSea map is the room, the voyages a rail along its foot, your squad beside the lit route (recommended) - interactive", body, logic)


def b_board():
    rail = """<div class="abs" style="left:40px;top:888px;width:1220px;display:grid;grid-template-columns:repeat(4,1fr);gap:16px">
<sc-for list="{{rail}}" as="r" hint-placeholder-count="4"><button class="pick glass sls" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" style="height:150px;box-sizing:border-box;padding:18px 18px 0;display:flex;flex-direction:column;justify-content:space-between;box-shadow:{{r.edge}};font-size:18px;line-height:1.3;color:#E6E9FF">{{r.title}}<span style="display:block;padding-bottom:16px;font-size:15px;color:#747BAD">{{r.sub}}</span></button></sc-for>
</div>"""
    card = """<sc-if value="{{open}}" hint-placeholder-val="{{ true }}"><div class="abs glass sl" style="left:1300px;top:172px;width:580px;height:868px;box-sizing:border-box;padding:30px 30px 0">
<button class="pick abs" type="button" onClick="{{close}}" aria-label="Close the voyage" style="right:24px;top:24px;font-size:28px;color:#747BAD">&#215;</button>
<span class="lab" style="display:block;max-width:470px">{{voyage.kicker}}</span>
<span style="display:block;margin-top:12px;font-size:36px;line-height:1.1">{{voyage.title}}</span>
<div class="num" style="display:flex;gap:26px;margin-top:22px;font-size:22px"><span>{{voyage.ly}}</span><span>{{voyage.min}}</span><span>{{voyage.intensity}}</span></div>
<span style="display:block;margin-top:18px;font-size:18px;line-height:1.45;color:#747BAD">{{voyage.line}}</span>
<div style="display:flex;gap:10px;margin-top:34px"><sc-for list="{{presets}}" as="p" hint-placeholder-count="2"><button class="pick sls" type="button" aria-pressed="{{p.pressed}}" onClick="{{p.pick}}" style="padding:10px 16px;background:rgba(0,4,31,.6);box-shadow:{{p.edge}};font-family:'Chakra Petch';font-weight:600;font-size:14px;letter-spacing:.12em;text-transform:uppercase;color:{{p.col}}">{{p.name}}</button></sc-for></div>
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin-top:22px"><sc-for list="{{formation}}" as="f" hint-placeholder-count="3"><div style="display:flex;flex-direction:column;align-items:center;gap:8px"><img class="hull" src="{{f.hull}}" alt="{{f.alt}}" style="width:166px;height:150px"><span class="ell" style="max-width:170px;font-size:14px">{{f.who}}</span></div></sc-for></div>
<div class="abs" style="left:30px;right:30px;bottom:30px;display:flex;flex-direction:column;gap:12px">
<div style="display:flex;gap:12px"><a class="btn sm def" href="R{REV}Crew.dc.html" style="flex:1">Ark crew</a><a class="btn sm def" href="#" style="flex:1">Story</a></div>
<a class="btn commit" href="#"><span class="kc">A</span>Set sail</a></div>
</div></sc-if>
<sc-if value="{{closed}}" hint-placeholder-val="{{ false }}"><button class="btn def abs" type="button" onClick="{{reopen}}" style="right:40px;top:180px"><span class="kc">A</span>Open the voyage</button></sc-if>"""
    body = ('<img src="{{voyage.map}}" alt="The course on the HyperSea map" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{voyage.pos}};filter:brightness(.62) saturate(.8)">'
            '<div class="abs" style="inset:0;background:linear-gradient(180deg,rgba(0,1,10,.94) 0%,rgba(0,1,10,.7) 16%,rgba(0,1,10,0) 30%,rgba(0,1,10,0) 70%,rgba(0,1,10,.82) 100%)"></div>'
            + nav() + tabrow(VOYAGE_TABS, "Voyages")
            + '<span class="abs glass sls" style="left:40px;top:180px;padding:12px 18px;font-size:18px">{{voyage.course}}</span>'
            + card + rail)
    logic = DATA + """class Component extends DCLogic {
constructor(p) { super(p); this.state = { v: 1, sq: 0, open: true }; }
renderVals() {
  const st = this.state;""" + VOYAGE_VALS.replace("pick: () => this.setState({ v: i })", "pick: () => this.setState({ v: i, open: true })") + """  return { voyage, rail, presets, formation, squad, open: st.open, closed: !st.open,
    close: () => this.setState({ open: false }), reopen: () => this.setState({ open: true }) };
}
}"""
    return page(f"Rev {REV} B - The sea: the route poster at window size, the voyages along its foot, the open voyage on the right - interactive", body, logic)


SEA_LINES = ('<svg aria-hidden="true" width="1920" height="996" style="position:absolute;left:0;top:84px">'
             + "".join(
                 f'<polyline points="{" ".join(f"{x},{round(y0 + 18 * __import__("math").sin((x / 1920) * 6.283 * 2.4 + k), 1)}" for x in range(0, 1921, 32))}" '
                 f'fill="none" stroke="{c}" stroke-opacity="0.22" stroke-width="1.5"></polyline>'
                 for y0, k, c in ((338, 0.0, "#758FFB"), (578, 1.7, "#96ACFC"), (796, 3.1, "#B9C9FA")))
             + '</svg>')
PROFILE_GROUND = ('<div class="abs" style="left:0;top:84px;width:1920px;height:996px;background:linear-gradient(180deg,'
                  'rgba(34,38,69,.96) 0%,rgba(0,4,31,.97) 52%,rgba(0,1,10,.98) 100%)"></div>' + SEA_LINES)


def c_board():
    world = """<div class="abs sl" style="left:40px;top:180px;width:1140px;height:560px;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89"><img src="{{voyage.map}}" alt="{{voyage.place}}, close" style="position:absolute;left:-60%;top:-60%;width:220%;height:220%;object-fit:cover;object-position:{{voyage.pos}};filter:brightness(.7) saturate(.85)"><div class="abs" style="left:0;right:0;bottom:0;height:220px;padding:0 36px 32px;box-sizing:border-box;display:flex;flex-direction:column;justify-content:flex-end;gap:10px;background:linear-gradient(180deg,rgba(0,4,31,0) 0%,rgba(0,4,31,.88) 70%)"><span style="font-size:48px;line-height:1">{{voyage.place}}</span><span style="font-size:18px;color:#E6E9FF;max-width:760px">{{voyage.placeLine}}</span></div></div>"""
    rail = """<div class="abs" style="left:40px;top:770px;width:1140px;display:grid;grid-template-columns:repeat(4,1fr);gap:18px">
<sc-for list="{{rail}}" as="r" hint-placeholder-count="4"><button class="pick sl" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" style="height:236px;box-sizing:border-box;display:flex;flex-direction:column;background:rgba(0,4,31,.9);box-shadow:{{r.edge}};overflow:hidden;font-size:18px;line-height:1.3;color:#E6E9FF"><img src="{{r.map}}" alt="" style="width:100%;height:120px;object-fit:cover;object-position:{{r.pos}};filter:brightness(.8)"><span style="display:block;padding:16px 18px 0">{{r.title}}</span></button></sc-for>
</div>"""
    right = """<div class="abs" style="left:1230px;top:180px;width:650px;display:flex;flex-direction:column;gap:14px">
<span class="lab">{{voyage.kicker}}</span>
<span style="font-size:36px;line-height:1.1">{{voyage.title}}</span>
<span style="font-size:18px;line-height:1.45;color:#747BAD">{{voyage.line}}</span>
<div class="num" style="display:flex;gap:30px;margin-top:8px;font-size:28px"><span>{{voyage.ly}}</span><span>{{voyage.min}}</span><span>{{voyage.intensity}}</span></div>
</div>
<div class="abs sl" style="left:1230px;top:470px;width:650px;height:430px;box-sizing:border-box;padding:24px;background:rgba(0,4,31,.86);box-shadow:inset 0 0 0 1px #434C89">
<div style="display:flex;gap:12px"><sc-for list="{{presets}}" as="p" hint-placeholder-count="2"><button class="pick sls" type="button" aria-pressed="{{p.pressed}}" onClick="{{p.pick}}" style="padding:10px 18px;background:rgba(34,38,69,.9);box-shadow:{{p.edge}};font-family:'Chakra Petch';font-weight:600;font-size:15px;letter-spacing:.12em;text-transform:uppercase;color:{{p.col}}">{{p.name}}</button></sc-for></div>
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:30px"><sc-for list="{{formation}}" as="f" hint-placeholder-count="3"><div style="display:flex;flex-direction:column;align-items:center;gap:10px"><img class="hull" src="{{f.hull}}" alt="{{f.alt}}" style="width:186px;height:160px"><span class="ell" style="max-width:186px;font-size:15px">{{f.who}}</span></div></sc-for></div>
<a class="btn sm def abs" href="R{REV}Crew.dc.html" style="left:24px;bottom:24px"><span class="kc">X</span>Ark crew</a>
</div>
<a class="btn commit abs" href="#" style="left:1230px;top:948px;width:650px"><span class="kc">A</span>Set sail</a>"""
    body = PROFILE_GROUND + nav() + tabrow(VOYAGE_TABS, "Voyages") + world + rail + right
    logic = DATA + """class Component extends DCLogic {
constructor(p) { super(p); this.state = { v: 1, sq: 0 }; }
renderVals() {
  const st = this.state;""" + VOYAGE_VALS + """  return { voyage, rail, presets, formation, squad };
}
}"""
    return page(f"Rev {REV} C - Showroom: the destination fills the page under its name, as the Profile's Showroom does; the voyages as art cards under it - interactive", body, logic)


def crew_board():
    body = ('<img src="{{room}}" alt="Inside the Ark, your squad\'s room" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover">'
            '<div class="abs" style="inset:0;background:linear-gradient(180deg,rgba(0,1,10,.75) 0%,rgba(0,1,10,.55) 30%,rgba(0,1,10,.6) 70%,rgba(0,1,10,.88) 100%)"></div>'
            + nav(crystals=False) + tabrow(VOYAGE_TABS, "Voyages", back_href=f"R{REV}A.dc.html", back_label="Voyage")
            + """<div class="abs" style="left:40px;top:180px;right:40px;display:flex;align-items:center;justify-content:space-between">
<div style="display:flex;align-items:center;gap:22px"><span style="font-size:36px">{{name}}</span>
<sc-for list="{{domains}}" as="d" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{d.pressed}}" onClick="{{d.pick}}" style="padding:10px 18px;background:rgba(0,4,31,.7);box-shadow:{{d.edge}};font-family:'Chakra Petch';font-weight:600;font-size:15px;letter-spacing:.12em;text-transform:uppercase;color:{{d.col}}">{{d.name}}</button></sc-for></div>
<span class="glass sls" style="padding:12px 18px;font-size:18px">{{pinned}}</span>
</div>
<div class="abs" style="left:40px;top:262px;width:1840px;display:grid;grid-template-columns:repeat(3,1fr);gap:20px">
<sc-for list="{{seats}}" as="s" hint-placeholder-count="3"><div class="glass sl" style="height:440px;box-sizing:border-box;padding:22px 24px;box-shadow:{{s.edge}};font-family:'Chakra Petch';font-weight:600;font-size:15px;letter-spacing:.14em;text-transform:uppercase;color:#747BAD">{{s.head}}<img class="hull" src="{{s.hull}}" alt="{{s.v}}" style="display:block;width:100%;height:170px;margin:14px 0 8px">
<div style="display:grid;grid-template-columns:1fr 1fr;gap:10px"><sc-for list="{{s.settings}}" as="g" hint-placeholder-count="4"><button class="pick set sls" type="button" data-l="{{g.label}}" onClick="{{g.press}}" style="padding:14px 16px;background:rgba(0,4,31,.62);box-shadow:{{g.edge}};font-family:'Aldrich';font-size:18px;letter-spacing:0;text-transform:none;color:{{g.col}}">{{g.value}}</button></sc-for></div>
</div></sc-for>
</div>
<sc-if value="{{strip.open}}" hint-placeholder-val="{{ true }}"><div class="abs glass sl" style="left:40px;top:730px;width:1840px;height:220px;box-sizing:border-box;padding:18px 24px;box-shadow:inset 0 0 0 1px #00D4FF;font-family:'Chakra Petch';font-weight:600;font-size:15px;letter-spacing:.14em;text-transform:uppercase;color:#E6E9FF">{{strip.caption}}<div style="display:flex;gap:16px;margin-top:14px"><sc-for list="{{strip.items}}" as="t" hint-placeholder-count="6"><button class="pick sls" type="button" onClick="{{t.pick}}" aria-label="{{t.name}}" aria-pressed="{{t.pressed}}" style="flex:1;height:140px;display:flex;align-items:center;justify-content:center;box-shadow:{{t.edge}}"><img class="hull" src="{{t.img}}" alt="{{t.name}}" style="width:{{t.w}};height:{{t.h}}"></button></sc-for></div></div></sc-if>
<sc-if value="{{strip.shut}}" hint-placeholder-val="{{ false }}"><a class="btn cta abs" href="#" style="right:40px;top:980px"><span class="kc">Y</span>Save squad</a></sc-if>""")
    logic = DATA + "const PILOT = " + json.dumps(PILOT) + ";\nconst ELEMENT = " + json.dumps(ELEMENT) + ";\nconst ARTIFACTS = " + json.dumps(ARTIFACTS) + """;
const HULLS = ['Sparrow', 'Dolphin', 'Squirrel', 'Rhino', 'Manta', 'Serpent'];
const LABELS = ['Vessel', 'Pilot', 'Element', 'Artifact'];
const KEYS = ['v', 'p', 'e', 'a'];
class Component extends DCLogic {
constructor(p) { super(p); this.state = { seats: SQ[0].seats.map((s) => Object.assign({}, s)), dom: 'Jade', open: { seat: 1, set: 0 } }; }
renderVals() {
  const st = this.state, o = st.open;
  const setSeat = (i, k, val) => { const seats = st.seats.map((s, j) => j === i ? Object.assign({}, s, { [k]: val }) : s); this.setState({ seats, open: null }); };
  const seats = st.seats.map((s, i) => ({ v: s.v, hull: HULL[s.v],
    head: '0' + (i + 1) + ' ' + s.role + ' - ' + s.who,
    edge: o && o.seat === i ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(116,123,173,.45)',
    settings: KEYS.map((k, j) => { const on = o && o.seat === i && o.set === j; return { label: LABELS[j], value: s[k],
      col: s[k] === 'No artifact' ? '#747BAD' : '#E6E9FF',
      edge: on ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(116,123,173,.35)',
      press: () => this.setState({ open: on ? null : { seat: i, set: j } }) }; }) }));
  let strip = { open: false, shut: true, caption: '', items: [] };
  if (o) {
    const s = st.seats[o.seat], k = KEYS[o.set];
    let items;
    if (o.set === 0) items = HULLS.map((h) => ({ name: h, img: HULL[h], w: '200px', h: '120px' }));
    else if (o.set === 1) items = Object.keys(PILOT).map((n) => ({ name: n, img: PILOT[n], w: '96px', h: '122px' }));
    else if (o.set === 2) items = Object.keys(ELEMENT).map((n) => ({ name: n, img: ELEMENT[n], w: '72px', h: '72px' }));
    else items = ARTIFACTS.map((n) => ({ name: n, img: '', w: '0px', h: '0px' }));
    items = items.map((t) => Object.assign(t, { pressed: s[k] === t.name ? 'true' : 'false',
      edge: s[k] === t.name ? 'inset 0 0 0 2px #00D4FF' : 'none', pick: () => setSeat(o.seat, k, t.name) }));
    strip = { open: true, shut: false, caption: s.role + ' - ' + LABELS[o.set] + ': one press picks', items };
  }
  const domains = ['Jade', 'Ruby', 'Gold'].map((d) => ({ name: d, pressed: d === st.dom ? 'true' : 'false', col: d === st.dom ? '#E6E9FF' : '#747BAD',
    edge: 'inset 3px 0 0 ' + DOMC[d] + (d === st.dom ? ', inset 0 0 0 1px #00D4FF' : ', inset 0 0 0 1px rgba(116,123,173,.45)'),
    pick: () => this.setState({ dom: d }) }));
  const v = V[1];
  return { name: SQ[0].name, room: ROOM[st.dom], domains, seats, strip,
    pinned: 'Voyage ' + v.n + ' - ' + v.title + ' - ' + v.ly };
}
}"""
    return page(f"Rev {REV} Crew - A's Ark crew: press a seat's vessel and the Hangar's vessel strip opens on that seat; one press picks the hull - interactive", body, logic)


def quests_board():
    body = (PROFILE_GROUND + nav() + tabrow([("Episode 1", False), ("Episode 2", True)], "Episode 1")
            + """<div class="abs" style="left:40px;top:186px;right:40px;display:flex;align-items:flex-end;justify-content:space-between">
<span style="font-size:48px;line-height:1">The Hypersea</span>
<div style="display:flex;align-items:center;gap:18px"><span class="num" style="font-size:20px">{{progress}}</span><div style="width:320px;height:8px;background:#222645"><div style="height:8px;width:{{fill}};background:#E6E9FF"></div></div></div>
</div>
<svg aria-hidden="true" width="1840" height="4" style="position:absolute;left:40px;top:298px"><line x1="0" y1="2" x2="1840" y2="2" stroke="#434C89" stroke-width="2"></line></svg>
<div class="abs" style="left:40px;top:330px;width:1840px;display:grid;grid-template-columns:repeat(5,1fr);gap:20px">
<sc-for list="{{quests}}" as="q" hint-placeholder-count="5"><button class="pick sl" type="button" aria-pressed="{{q.pressed}}" onClick="{{q.pick}}" style="height:380px;box-sizing:border-box;display:flex;flex-direction:column;overflow:hidden;background:rgba(0,4,31,.9);box-shadow:{{q.edge}};font-size:22px;color:{{q.col}}"><img src="{{q.art}}" alt="" style="width:100%;height:250px;object-fit:cover;opacity:{{q.op}}"><span style="display:block;padding:20px 20px 0">{{q.title}}</span><span class="cap" style="display:block;padding:10px 20px 0;font-size:14px;color:{{q.stateCol}}">{{q.stateLine}}</span></button></sc-for>
</div>
<div class="abs glass sl" style="left:40px;top:760px;width:1840px;height:200px;box-sizing:border-box;padding:30px 36px;display:flex;align-items:center;justify-content:space-between">
<div style="display:flex;flex-direction:column;gap:10px"><span style="font-size:28px">{{focus.title}}</span><span style="font-size:18px;color:#747BAD">{{focus.goal}}</span></div>
<sc-if value="{{focus.claim}}" hint-placeholder-val="{{ false }}"><button class="btn cta" type="button" onClick="{{focus.act}}"><span class="kc">A</span>Claim</button></sc-if>
<sc-if value="{{focus.play}}" hint-placeholder-val="{{ true }}"><button class="btn cta" type="button" onClick="{{focus.act}}"><span class="kc">A</span>Play quest</button></sc-if>
<sc-if value="{{focus.wait}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:18px;color:#747BAD">{{focus.waitLine}}</span></sc-if>
</div>""")
    logic = "const Q = " + json.dumps(QUESTS) + """;
class Component extends DCLogic {
constructor(p) { super(p); this.state = { states: Q.map((q) => q.state), sel: 2 }; }
renderVals() {
  const st = this.state, s = st.states;
  const claimed = s.filter((x) => x === 'claimed').length;
  const line = (x, i) => x === 'claimed' ? 'Claimed' : x === 'done' ? 'Done - claim it' : x === 'open' ? 'Next to play' : 'Unlocks after ' + Q[i - 1].name;
  const quests = Q.map((q, i) => ({ art: q.art, title: q.n + '  ' + q.name, stateLine: line(s[i], i),
    stateCol: s[i] === 'done' || s[i] === 'open' ? '#E6E9FF' : '#747BAD', col: s[i] === 'locked' ? '#747BAD' : '#E6E9FF',
    op: s[i] === 'locked' ? 0.35 : 1, pressed: i === st.sel ? 'true' : 'false',
    edge: i === st.sel ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(116,123,173,.45)',
    pick: () => this.setState({ sel: i }) }));
  const k = s[st.sel], q = Q[st.sel];
  const act = () => {
    if (k === 'done') { const n = s.slice(); n[st.sel] = 'claimed'; this.setState({ states: n }); }
  };
  const focus = { title: q.n + '  ' + q.name, goal: q.goal, claim: k === 'done', play: k === 'open', wait: k === 'locked' || k === 'claimed',
    waitLine: k === 'claimed' ? 'Claimed' : line(k, st.sel), act };
  return { quests, focus, progress: claimed + ' of ' + Q.length, fill: Math.round(100 * claimed / Q.length) + '%' };
}
}"""
    return page(f"Rev {REV} Quests - Quests, its own window like the Profile: Episode 1's five quests on one track, the next one to play lit - interactive", body, logic)


def compare_board():
    old_setting = ('<div class="sls" style="width:460px;padding:16px 20px;background:rgba(0,4,31,.9);box-shadow:inset 0 0 0 1px #434C89;'
                   'font-size:22px">Vessel: Sparrow, press for the next</div>')
    strip = ('<div class="sl" style="display:flex;gap:10px;padding:14px;background:rgba(0,4,31,.9);box-shadow:inset 0 0 0 1px #00D4FF">'
             + "".join(f'<img class="hull" src="{HULL[h]}" alt="{h}" style="width:92px;height:80px">' for h in HULL)
             + '</div>')

    def course(title, thumb, pos):
        return (f'<div style="display:flex;flex-direction:column;gap:10px;width:200px"><img src="{thumb}" alt="" '
                f'style="width:200px;height:120px;object-fit:cover;object-position:{pos};filter:brightness(.8)">'
                f'<span style="font-size:18px;line-height:1.3">{title}</span></div>')

    lantern, toll = VOYAGES[1], VOYAGES[0]
    chip = ('<span class="sls" style="padding:10px 16px;background:rgba(0,4,31,.9);box-shadow:inset 3px 0 0 #BD1BC8, inset 0 0 0 1px #00D4FF;'
            'font-family:\'Chakra Petch\';font-weight:600;font-size:15px;letter-spacing:.12em;text-transform:uppercase;white-space:nowrap">To Ruby</span>')
    rows = [
        ("Picking a vessel", "Ark crew cycled a seat's vessel one hull per press: up to 22 presses for the one you want.",
         old_setting, strip,
         "The seat opens the vessel strip the Arena and Voyage configure card already has (P-ui-21); one press picks."),
        ("The course while the crew changes", "The board is dealt from the seated pilots and the home domain: change either and the voyage on screen is replaced.",
         '<div style="display:flex;align-items:center;gap:20px">' + course(lantern["title"], lantern["map"], lantern["pos"]) + chip
         + course(toll["title"], toll["map"], toll["pos"]) + '</div>',
         '<div style="display:flex;align-items:center;gap:20px">' + course(lantern["title"], lantern["map"], lantern["pos"]) + chip
         + course(lantern["title"], lantern["map"], lantern["pos"]) + '</div>',
         "The voyage you are looking at stays; a new pilot's voyage joins the rail instead of replacing one (Garrett's rule to change)."),
        ("Where the quest tracker lives", "A tab inside the Voyage window: Voyages, Saga, Quests, Episodes.",
         '<span class="cap" style="font-size:18px">Voyages - Saga - Quests - Episodes</span>',
         '<div style="display:flex;align-items:center;gap:24px"><span class="cap" style="font-size:18px">Voyages - Saga - Episodes</span>'
         '<span class="sls" style="padding:12px 18px;background:rgba(0,4,31,.9);box-shadow:inset 0 0 0 1px #747BAD;font-size:18px">Home: Quests 1 of 5</span></div>',
         "Its own window, opened from Home like the Profile; the Voyage window keeps three tabs."),
    ]
    out = ['<span class="abs" style="left:56px;top:40px;font-size:36px">Rev ' + str(REV) + ' - the three fixes</span>',
           '<span class="abs lab" style="left:600px;top:110px">Now</span>', '<span class="abs lab" style="left:1220px;top:110px">Rev ' + str(REV) + '</span>']
    y = 150
    for head, now_line, now, new, new_line in rows:
        out.append(f'<div class="abs" style="left:56px;top:{y}px;width:500px;display:flex;flex-direction:column;gap:10px">'
                   f'<span style="font-size:28px">{head}</span><span style="font-size:15px;line-height:1.45;color:#747BAD">{now_line}</span>'
                   f'<span style="font-size:15px;line-height:1.45;color:#E6E9FF">{new_line}</span></div>')
        out.append(f'<div class="abs" style="left:600px;top:{y}px;width:580px">{now}</div>')
        out.append(f'<div class="abs" style="left:1220px;top:{y}px;width:660px">{new}</div>')
        y += 300
    logic = "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}"
    return page(f"Rev {REV} - the three fixes side by side: picking a vessel (22 presses, now 2), the course while the squad or domain changes (re-dealt, now pinned), and where the quest tracker lives (a Voyage tab, now its own window)",
                "\n".join(out), logic)


def notes_board():
    blocks = [
        ("Asked", "Shombith, 2026-11-08: none of the Voyage designs are good or match the better parts of the game; picking a vessel takes up to 22 presses; changing domain or team changes the course; move the quest tracker into its own window like the Profile."),
        ("The look", "A stands the window inside the Ark (the room of Rev 61 C and Rev 67, lit by the squad's domain) with the HyperSea chart as its screen. B is Rev 49 C's route poster at window size. C is the Profile's Showroom. All three keep Rev 46's small cards and the three levels."),
        ("Recommended: A", "It is the newest look the team picked (the Ark tab), it keeps the map as the hero, and the squad stands in its own room. B is the safest step from what is built; C reads quietest."),
        ("Picking a vessel", "A seat's vessel, pilot, element or artifact opens a strip on that seat, and one press picks (P-ui-21: the configure card's vessel strip, Yash's ArenaLaunchPanel). The cycling buttons go. Elements use the Codex's shapes (P-ui-29)."),
        ("The course stays put", "Today the board is dealt from the seated pilots (DEPTH.md section 4, VoyageHarbour.Board) and a first galaxy is seeded from the home domain (VoyageRun.LoadOrBegin). Pinning the voyage on screen is Garrett's rule to change: a proposal to him before any build."),
        ("Quests, its own window", "Opened from Home like the Profile, Episode 1 and Episode 2 (locked) on LB / RB, the five quests on one track, one Lime button: Claim or Play quest. P-ui-28 put the tracker in the Voyage window; this round moves it, on Shombith's word. FTUE is Shombith's and Caleb's."),
    ]
    cells = "".join(f'<div style="display:flex;flex-direction:column;gap:10px"><span class="lab">{h}</span>'
                    f'<span style="font-size:18px;line-height:1.5">{t}</span></div>' for h, t in blocks)
    body = (f'<span class="abs" style="left:56px;top:44px;font-size:36px">Rev {REV} - notes</span>'
            f'<div class="abs" style="left:56px;top:120px;width:1808px;display:grid;grid-template-columns:1fr 1fr;gap:34px 64px">{cells}</div>')
    logic = "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}"
    return page(f"Rev {REV} - notes: what each option changes in the built window, the two rules the fixes need (the vessel strip, P-ui-21; the pinned board, Garrett's DEPTH.md 4), and the Quests window",
                body, logic, h=900)


BOARDS = {
    f"R{REV}Compare.dc.html": compare_board,
    f"R{REV}A.dc.html": a_board,
    f"R{REV}B.dc.html": b_board,
    f"R{REV}C.dc.html": c_board,
    f"R{REV}Crew.dc.html": crew_board,
    f"R{REV}Quests.dc.html": quests_board,
    f"R{REV}Notes.dc.html": notes_board,
}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn().replace("R{REV}", f"R{REV}"), encoding="utf-8")
        print("wrote", ROOT / name)
