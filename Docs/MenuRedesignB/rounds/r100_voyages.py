#!/usr/bin/env python3
"""Rev 100 boards: the Voyages tab drawn new, in three pages (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-13: "Delete everything from the voyages tab, I dont like any of these designs. We will start on a new
design set. So the voyages need new modern UI with the hyper sea theme ofcourse. Main Preview (Show me some interesting
option along with the table top version but we should be able to zoom like how we used for the whole hypersea map). For
Voyages, they dont use intensity rather they use the difficulty level 1-9. Each of these voyages are recommended by a
certain pilot. Look at the pilots the user has and they can offer bounty hunts or piracy task etc and reccoment to the
user in the voyages tab, each possesses different routes, storylines and rewards. The configure your ark shoud not
include the squads but your lobby first if the lobby is not there then they can select the squads and also the squad
selection screen can be in a different page. So we can divide in 3 pages. First the hypersea map, maybe as a table top
in the middle and on the left or right just reccomendation once they are opened up we show the story the rewards we are
getting the route etc in a very cool modern interactive animated way and once user decides to set sail they get the
squad selection and the set sail."

Quests (own screen) and Saga / Episodes (one locked strip, Rev 103, drawn by another session into the band this round
leaves free at y 940-1060 of page 1) are not drawn here.

  Page 1 - the HyperSea, and the voyages your crew recommends (every map zooms: ladder, + / -, the wheel)
    P1A  Table top   the map lies on a chart table, its edge-on view the table's front; pins stand up as beacons
    P1B  Orrery      the galaxy as a disc floating in the sea, a slow ring of bearings round it
    P1C  Bridge      the map flat and full-bleed, the recommendations along its foot like the bridge's screens
    P1D  Dive        Galaxy, Sector and Course as three glass sheets, one under the other; zoom is a dive
  Page 2 - the voyage opened: who offers it, its story, its route, its rewards, difficulty 1-9
    P2A  Briefing    the pilot's portrait and story, the route drawing itself, the rewards arriving
    P2B  Journey     one lane across the window, the Ark travelling it; each stop opens its part of the story
    P2C  Dossier     the map stays; Story, Route and Rewards as cards that slide open
  Page 3 - your Ark: the lobby first; with no lobby, a squad; then Set sail
    P3A  Lobby       the lobby's three seats as they stand, your own seat changed in place
    P3B  No lobby    the same page with nobody in the lobby: a squad picked from the list, Invite beside it
    P3C  Formation   the three vessels in formation over the course; a press on one opens its tray
  Notes

    python3 Docs/MenuRedesignB/rounds/r100_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "100"
sys.argv = [sys.argv[0], _root, "70"]
_spec = importlib.util.spec_from_file_location("r70", HERE / "r70_voyages.py")
r70 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r70)

REV = int(_rev)
ROOT = Path(_root) / "project"
ROOT.mkdir(parents=True, exist_ok=True)
blob = r70.blob
INK, DIM, LINE, SEL, LIME, GROUND_INK = "#E6E9FF", "#747BAD", "#434C89", "#00D4FF", "#99FF80", "#00041F"

# ── The art already on the canvas ────────────────────────────────────────────────────────────────────────────────────
TIERS = [["Galaxy", "10,000 ly", blob("7559d7b030c6e949f7cde2d25ce5d9e7"), "50% 50%"],
         ["Arm", "1,000 ly", blob("fced8d3ce00c8f4c54ce4f49f52448c0"), "50% 50%"],
         ["Sector", "200 ly", blob("6138a0a6b01214f86b8a8496fe0140ba"), "50% 45%"],
         ["Course", "50 ly", "", ""]]
TUMULT = [[50.6, 49.8, 0.035], [50.0, 52.0, 0.2], [49.0, 61.0, 1.0]]   # where Tumult stands per tier, and the scale
HULL = {"Sparrow": blob("a2a4139d8e2822885778740d4f2fb92e"), "Dolphin": blob("e4ebf079ba441cd7141a2c8bc092d8ea"),
        "Squirrel": blob("694ae84ec6db347c4f034c8e03b56991"), "Rhino": blob("48e972fa9d369a737bacf39f37baf59f"),
        "Manta": blob("ab51cbb598b5a1e912416244829d134c"), "Serpent": blob("215e6ad15de9f075e7a45e7640a12f09")}
FLOWER = {"Charge": blob("324c1b515d7475bc5bd2b27fc2fcf468"), "Mass": blob("d439975f02e4371bd42b1fa6fb776075"),
          "Space": blob("5ed3904cc47e72a42c91e94413216ede"), "Time": blob("0ca36a64a90d1f07d05093972c19c130")}
# The eighteen pilots as the Ark's Pilots tab draws them (Rev 99): name, house, the close-up and the tall scene.
CREW = {
    "BANDIT": ["Rosh Rusthook", "Black Tide", "283a48123225eca8a9a42c41e606d174", "3c64ff1cf957e4b1f877d1d78aff0521"],
    "GALLEON": ["Haradek Anchorkeel", "The Unmoored", "45b8dac249d55356c4c5c193130fd1b0", "4ce66274d87e522133d5f06da5faaf1c"],
    "TALON": ["Wyvendo Waveline", "Hollowfen Trackers", "77e722ebf367c3b4093b2ae2778762e4", "3531d7cc65a34ce447f77a23a2d4e226"],
    "VOLT": ["Karenth Amberweaver", "Brass Lantern Company", "32a9eed26dda493c7334d13cddaf435e", "6760fa59723a74b7eda70ba160d62c26"],
    "NEEDLE": ["Ilia Silvergate", "The Jade Accord", "17190fc5544dfc08266eab625c0aa6a2", "b340492105d57db32af6f6f7ad3f8a79"],
    "OPAL": ["Quivera Pearlspark", "House of Seven Scales", "02126cf45624881e04e68ab3443b261b", "fa1bc075bdbd0350e5c4cd992752af3e"],
    "HORNET": ["Zeko Mirethorn", "Deepreed Stalkers", "5bb7e9dc77ccbb917e79991a86168b17", "43d5cc7af8dfe39f8e03cffcb121dc86"],
    "FLUME": ["Ekora Hollowbrook", "Saltjaw Brethren", "78ad334460657bdc5cf529a6cb76d1cd", "cfe5c00123b194bc5845945fd6be8fcd"],
    "ANVIL": ["Brikon Copperwright", "Tidewright Exchange", "f4c3e0f26d1f48e7d121156e212e4136", "5b307944f40dd4ab3329bca57b8121a2"],
}
CREW = {k: [n, h, blob(c), blob(t)] for k, (n, h, c, t) in CREW.items()}

# ── The voyages your pilots offer: the jobs of voyage_slice.json, each with its own route, story and rewards ─────────
# stops are points on a 1000 x 600 chart of the course; the pin is where the place stands on the Sector tier (%).
VOYAGES = [
    {"cs": "BANDIT", "kind": "Bounty hunt", "el": "Charge", "base": 12, "diff": 6,
     "title": "Bring in The Rusted Grin at Lantern Reach", "place": "Lantern Reach", "ly": "580 ly", "min": "29 min",
     "map": blob("b96bc9cef23de987e4ed42294befc28a"), "pos": "38% 46%", "edge": blob("25c96499cc959b94655e2a5a3362228e"), "pin": [45, 46],
     "story": ["The Rusted Grin took a courier's Ark at Gullbone and sold its hold twice over.",
               "There is a price on them now. They were seen running dark toward Lantern Reach.",
               "Arrive while they are there and take their Ark down. Writ and Hook pays on proof."],
     "stops": [["Tumult", "The Ark casts off from its mooring.", 110, 470], ["Saltmarch", "A core on the trade lane, ringed with old wreckage.", 360, 400],
               ["Magpie Hollow", "Dock talk: the Grin took on fuel here two days ago.", 610, 250], ["Lantern Reach", "Vault shells lit from inside. The Grin hides among them.", 880, 130]],
     "extra": [["The Rusted Grin's bounty", "Paid by Writ and Hook"], ["Standing with Black Tide", "They trust you more"], ["A lawful hunt", "No notoriety"]]},
    {"cs": "GALLEON", "kind": "Piracy", "el": "Mass", "base": 7, "diff": 4,
     "title": "Raid the thieves' hoard at Gallowglass", "place": "Gallowglass", "ly": "940 ly", "min": "47 min",
     "map": blob("037d66eeef857313705b96448c91f4a0"), "pos": "50% 40%", "edge": blob("58696b3b52c011054a8b024f94aff2e1"), "pin": [60, 32],
     "story": ["The thieves of Gallowglass sealed what they stole in shells, and sleep beside them.",
               "Crack one, stow what is inside, and be gone before the reef wakes.",
               "They will not forgive it. Every pirate port will know your Ark's silhouette."],
     "stops": [["Tumult", "The Ark casts off from its mooring.", 120, 480], ["Verdigris", "A contested core. Keep wide of its nucleus.", 330, 300],
               ["The Hollow", "A quiet cell with deep vaults. Quiet for a reason.", 560, 360], ["Gallowglass", "The thieves' hold in the reefs toward the centre.", 860, 170]],
     "extra": [["A sealed shell", "Whatever the thieves took"], ["Standing with The Unmoored", "They trust you more"], ["Notoriety", "Pirates will hear of it"]]},
    {"cs": "TALON", "kind": "Leviathan hunt", "el": "Charge", "base": 20, "diff": 8,
     "title": "Fell the Leviathan at The Maw", "place": "The Maw", "ly": "980 ly", "min": "49 min",
     "map": blob("da47aa2e4c65d63e53711ebfe6fc4ac2"), "pos": "48% 38%", "edge": blob("e8de257ad245f412b429c6d58363dc57"), "pin": [44, 30],
     "story": ["The Leviathan lairs at The Maw. Every port knows it, and no crew has come back with a scale.",
               "Fell its brood close to the Ark until it rises, then bring the beast down.",
               "The water is thick there. Keep the Ark under you or it will not keep you."],
     "stops": [["Tumult", "The Ark casts off from its mooring.", 100, 500], ["Cinder Shoal", "Rough water. Off every chart until last week.", 300, 330],
               ["Gnawreach", "Whatever eats here eats everything.", 560, 420], ["The Maw", "A dark gulf where the big feeders hunt.", 860, 150]],
     "extra": [["The Leviathan's scale", "A find, once"], ["Standing with Hollowfen Trackers", "They trust you more"], ["Danger", "The brood hunts the Ark"]]},
    {"cs": "VOLT", "kind": "Courier", "el": "Time", "base": 6, "diff": 2,
     "title": "Carry a sealed dispatch to Tollmoor", "place": "Tollmoor", "ly": "540 ly", "min": "27 min",
     "map": blob("9778fd69fcae1f8cb003a9bbd8508a56"), "pos": "62% 58%", "edge": blob("cd62f3bbf1d64b1e7f1f4739209aaa74"), "pin": [58, 68],
     "story": ["News is priced by its voltage, and this news sparks.",
               "Reach Tollmoor with the Ark standing. What is aboard will change what Tollmoor knows.",
               "Nobody aboard reads it. That is what the Company pays for."],
     "stops": [["Tumult", "The Ark casts off from its mooring.", 120, 420], ["Spindrift Shallows", "Easy water, and crystals for the taking.", 420, 300],
               ["Tollmoor", "A neutral port where every lane crosses. News lands here first.", 860, 200]],
     "extra": [["Standing with Brass Lantern Company", "They trust you more"], ["Tollmoor's ear", "The dock crews will talk to you"]]},
]
for v in VOYAGES:
    n, h, close, tall = CREW[v["cs"]]
    v.update(pilot=n, house=h, face=close, tall=tall, flower=FLOWER[v["el"]])

LOBBY = [{"who": "You", "st": "You", "v": "Sparrow", "cs": "NEEDLE", "e": "Charge", "a": "Extra Bay"},
         {"who": "Halcyon", "st": "Ready", "v": "Dolphin", "cs": "OPAL", "e": "Space", "a": "Seed Pod"},
         {"who": "Mirel", "st": "Choosing", "v": "Manta", "cs": "HORNET", "e": "Time", "a": "Tailwind"}]
SQUADS = [{"name": "Skim Wing", "owner": "Yours", "seats": [
              {"who": "You", "st": "Leader", "v": "Sparrow", "cs": "NEEDLE", "e": "Charge", "a": "Extra Bay"},
              {"who": "AI wingmate", "st": "Rogue One", "v": "Squirrel", "cs": "FLUME", "e": "Time", "a": "No artifact"},
              {"who": "AI wingmate", "st": "Rogue Two", "v": "Rhino", "cs": "ANVIL", "e": "Mass", "a": "Long Edge"}]},
          {"name": "Long Haul", "owner": "Yours", "seats": [
              {"who": "You", "st": "Leader", "v": "Dolphin", "cs": "OPAL", "e": "Space", "a": "Seed Pod"},
              {"who": "AI wingmate", "st": "Rogue One", "v": "Manta", "cs": "HORNET", "e": "Time", "a": "Tailwind"},
              {"who": "AI wingmate", "st": "Rogue Two", "v": "Serpent", "cs": "NEEDLE", "e": "Charge", "a": "No artifact"}]},
          {"name": "Breakers", "owner": "Kestrel's", "seats": [
              {"who": "Kestrel", "st": "Leader", "v": "Rhino", "cs": "ANVIL", "e": "Mass", "a": "Long Edge"},
              {"who": "You", "st": "Rogue One", "v": "Manta", "cs": "HORNET", "e": "Charge", "a": "Tailwind"},
              {"who": "AI wingmate", "st": "Rogue Two", "v": "Serpent", "cs": "FLUME", "e": "Space", "a": "No artifact"}]}]
ARTIFACTS = ["No artifact", "Extra Bay", "Seed Pod", "Tailwind", "Long Edge"]

# ── The kit: the menu's (Rev 70) plus this round's soft buttons and its few one-shot motions ─────────────────────────
BAND = "linear-gradient(90deg,rgba(0,4,31,0) 0,rgba(0,4,31,.86) 14%,rgba(0,4,31,.86) 86%,rgba(0,4,31,0) 100%)"
CSS = """
.soft-cta{clip-path:none;background:linear-gradient(90deg,rgba(153,255,128,0) 0,#99FF80 18%,#99FF80 82%,rgba(153,255,128,0) 100%);color:#00041F}
.soft{clip-path:none;color:#E6E9FF;background:linear-gradient(90deg,rgba(230,233,255,0) 0,rgba(230,233,255,.7) 20%,rgba(230,233,255,.7) 80%,rgba(230,233,255,0) 100%) top/100% 1px no-repeat,linear-gradient(90deg,rgba(230,233,255,0) 0,rgba(230,233,255,.7) 20%,rgba(230,233,255,.7) 80%,rgba(230,233,255,0) 100%) bottom/100% 1px no-repeat,linear-gradient(90deg,rgba(0,4,31,0) 0,rgba(0,4,31,.86) 20%,rgba(0,4,31,.86) 80%,rgba(0,4,31,0) 100%)}
.tier{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;transition:opacity .55s ease,transform .8s cubic-bezier(.2,.7,.2,1)}
.pips{display:inline-block;width:124px;height:8px;background:repeating-linear-gradient(90deg,#434C89 0 12px,rgba(0,0,0,0) 12px 14px)}
.pips>span{display:block;height:100%;background:repeating-linear-gradient(90deg,#E6E9FF 0 12px,rgba(0,0,0,0) 12px 14px);transition:width .4s ease}
.rise{animation:rise .7s cubic-bezier(.2,.7,.2,1) both}
.pop{animation:pop .45s cubic-bezier(.3,1.4,.5,1) both}
.draw{stroke-dasharray:1400;stroke-dashoffset:1400;animation:draw 2.4s .3s cubic-bezier(.4,0,.2,1) forwards}
.slide{transition:max-height .5s cubic-bezier(.2,.7,.2,1),opacity .4s ease;overflow:hidden}
.spin{animation:spin 240s linear infinite}
.sailer{offset-rotate:auto;animation:sail 3.2s .4s cubic-bezier(.4,0,.2,1) both}
@keyframes rise{from{opacity:0;transform:translateY(16px)}to{opacity:1;transform:none}}
@keyframes pop{from{opacity:0;transform:scale(.3)}to{opacity:1;transform:scale(1)}}
@keyframes draw{to{stroke-dashoffset:0}}
@keyframes spin{to{transform:rotate(360deg)}}
@keyframes sail{from{offset-distance:0%}to{offset-distance:100%}}
"""
HELMET = r70.HELMET.replace("</style>", CSS + "</style>")
LOCK = r70.LOCK
assert HELMET != r70.HELMET


def page(title, body, logic):
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
<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
{body}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":1920,"height":1080}}}}'>
{logic}
</script>
</body>
</html>
"""


GROUND = ('<img src="' + blob("391619dde523174b4a9ead64e169b67a") + '" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover">'
          '<div class="abs" style="left:0;top:84px;width:1920px;height:996px;background:radial-gradient(ellipse 70% 60% at 50% 40%,'
          'rgba(117,143,251,.10),rgba(117,143,251,0) 70%),linear-gradient(180deg,rgba(0,4,31,.94) 0%,rgba(0,1,10,.97) 100%)"></div>'
          + r70.SEA_LINES)

STEPS = ["The HyperSea", "The voyage", "Your Ark"]


def header(step, back=None):
    """One quiet line: where you are (Voyages), the three pages as a path, Back."""
    parts = []
    for i, name in enumerate(STEPS, 1):
        on = i == step
        col = INK if on else DIM
        line = INK if on else "rgba(0,0,0,0)"
        parts.append(f'<span class="cap" style="font-size:14px;color:{col};padding-bottom:6px;border-bottom:2px solid {line};white-space:nowrap">'
                     f'<span class="num" style="margin-right:10px">0{i}</span>{name}</span>')
    rail = f'<span aria-hidden="true" style="width:56px;height:1px;background:{LINE}"></span>'.join(parts)
    back_el = (f'<a class="btn sm soft" href="{back}" style="width:180px"><span class="kc">B</span>Back</a>' if back else
               '<button class="btn sm soft" type="button" style="width:180px"><span class="kc">B</span>Back</button>')
    return ('<header class="abs" style="left:40px;right:40px;top:104px;height:48px;display:flex;align-items:center;justify-content:space-between;z-index:4">'
            '<span style="font-size:28px;width:340px">Voyages</span>'
            f'<nav aria-label="Voyage steps" style="display:flex;align-items:center;gap:22px">{rail}</nav>'
            f'<div style="width:340px;display:flex;justify-content:flex-end">{back_el}</div></header>')


def band():
    """Rev 103 A as Shombith picked it (narrowed): Saga and Episodes as two locked cards, 520 x 120, centred."""
    card = lambda name: (f'<div style="width:520px;height:120px;display:flex;align-items:center;gap:16px;padding:0 30px;box-sizing:border-box;'
                         f'color:{DIM};background:rgba(0,4,31,.7);box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)">{LOCK}'
                         f'<span class="cap" style="font-size:15px">{name}</span><span style="margin-left:auto;font-size:14px">Coming soon</span></div>')
    return f'<div class="abs" style="left:428px;top:940px;width:1064px;display:flex;gap:24px">{card("Saga")}{card("Episodes")}</div>'


def window(step, body, title, back=None, ground=GROUND, foot=True):
    return page(title, ground + r70.nav(crystals=False) + header(step, back) + body + (band() if step == 1 and foot else ""), LOGIC)


def link(name):
    return f"R{REV}{name}.dc.html"


# ── The logic every board shares ─────────────────────────────────────────────────────────────────────────────────────
DATA = ("const V = " + json.dumps(VOYAGES) + ";\nconst TIERS = " + json.dumps(TIERS) + ";\nconst TUMULT = " + json.dumps(TUMULT)
        + ";\nconst HULL = " + json.dumps(HULL) + ";\nconst FLOWER = " + json.dumps(FLOWER) + ";\nconst CREW = " + json.dumps(CREW)
        + ";\nconst LOBBY = " + json.dumps(LOBBY) + ";\nconst SQUADS = " + json.dumps(SQUADS) + ";\nconst ARTIFACTS = " + json.dumps(ARTIFACTS) + ";\n")

LOGIC = DATA + """class Component extends DCLogic {
constructor(p) { super(p); this.state = { z: 2, sel: 0, d: -1, stop: -1, card: 0, seat: -1, sq: 0, lob: LOBBY.map((s) => Object.assign({}, s)), t: 0 }; }
go(z) { this.setState({ z: Math.max(0, Math.min(3, z)) }); }
renderVals() {
  const st = this.state, z = st.z, v = V[st.sel], d = st.d < 0 ? v.diff : st.d;
  const on = (b) => b ? 'true' : 'false';
  const wheel = (e) => { const now = Date.now(); if (now - st.t < 380) return; this.setState({ t: now, z: Math.max(0, Math.min(3, z + (e.deltaY < 0 ? 1 : -1))) }); };
  const map = { zin: () => this.go(z + 1), zout: () => this.go(z - 1), wheel: wheel, name: TIERS[z][0], scale: TIERS[z][1],
    readout: z === 3 ? v.place + ',  ' + v.ly : TIERS[z][1] + ' across', course: z === 3, wide: z < 3, near: z === 2 };
  const layers = TIERS.map((x, i) => ({ src: i === 3 ? v.map : x[2], pos: i === 3 ? v.pos : x[3], alt: 'The HyperSea, ' + x[0] + ' tier',
    op: i === z ? 1 : 0, sc: i < z ? 2.2 : (i > z ? 0.5 : 1) }));
  const ladder = TIERS.map((x, i) => ({ name: x[0], scale: x[1], pressed: on(i === z), col: i === z ? '#E6E9FF' : '#747BAD',
    bar: i === z ? '#00D4FF' : 'rgba(0,0,0,0)', pick: () => this.go(i) })).reverse();
  // A place on the map: where it stands at this tier, from where it stands on the Sector tier.
  const at = (p) => { if (z === 3) return null; const T = TUMULT[z], S = TUMULT[2];
    return [T[0] + (p[0] - S[0]) * T[2], T[1] + (p[1] - S[1]) * T[2]]; };
  const pins = V.map((x, i) => { const p = at(x.pin) || (i === st.sel ? [86, 22] : [-50, -50]);
    return { left: p[0] + '%', top: p[1] + '%', place: x.place, label: z >= 2 || (z === 3 && i === st.sel), pressed: on(i === st.sel),
      ring: i === st.sel ? '#00D4FF' : 'rgba(230,233,255,.9)', glow: i === st.sel ? '0 0 0 6px rgba(0,212,255,.22)' : 'none',
      stalk: (40 + x.diff * 14) + 'px', alt: x.place + ', ' + x.kind + ' offered by ' + x.pilot + ', difficulty ' + x.diff,
      vis: (z === 3 && i !== st.sel) ? 'none' : 'block', pick: () => this.setState({ sel: i, d: -1, stop: -1 }) }; });
  const home = (() => { const p = at([TUMULT[2][0], TUMULT[2][1]]) || [12, 78]; return { left: p[0] + '%', top: p[1] + '%' }; })();
  const pips = (n) => (n / 9 * 100).toFixed(2) + '%';
  const scaled = (x, dd) => Math.round(x.base * (0.55 + dd * 0.1));
  const recs = V.map((x, i) => ({ face: x.face, pilot: x.pilot, house: x.house, kind: x.kind, title: x.title, place: x.place,
    diff: String(x.diff), pips: pips(x.diff), meta: x.ly + '  \\u00B7  ' + x.min, flower: x.flower, reward: scaled(x, x.diff) + ' ' + x.el,
    pressed: on(i === st.sel), op: i === st.sel ? 1 : 0.72, edge: i === st.sel ? 'inset 0 -2px 0 #00D4FF' : 'inset 0 -1px 0 rgba(67,76,137,.6)',
    bg: i === st.sel ? 'rgba(0,4,31,.9)' : 'rgba(0,4,31,.55)', pick: () => this.setState({ sel: i, d: -1, stop: -1, z: z === 3 ? 3 : 2 }) }));
  // The opened voyage: the story, the route, the rewards; difficulty 1-9 changes what it pays.
  const pts = v.stops.map((s) => s[2] + ',' + s[3]).join(' ');
  const lane = 'M ' + v.stops.map((s, k) => (120 + k * (1560 / (v.stops.length - 1))) + ' ' + (k % 2 ? 150 : 250)).join(' L ');
  const stops = v.stops.map((s, k) => ({ name: s[0], note: s[1], x: (s[2] / 10) + '%', y: (s[3] / 6) + '%', n: '0' + (k + 1),
    side: s[2] > 700 ? 'right:30px' : 'left:30px', lx: (120 + k * (1560 / (v.stops.length - 1))) + 'px', ly: (k % 2 ? 150 : 250) + 'px',
    delay: (0.3 + 2.4 * k / (v.stops.length - 1)).toFixed(2) + 's', pressed: on(k === st.stop),
    end: k === v.stops.length - 1, ring: k === st.stop ? '#00D4FF' : '#E6E9FF', col: k === st.stop ? '#E6E9FF' : '#747BAD',
    pick: () => this.setState({ stop: st.stop === k ? -1 : k }) }));
  const shown = st.stop >= 0 ? v.stops[st.stop] : v.stops[v.stops.length - 1];
  const voyage = { title: v.title, place: v.place, pilot: v.pilot, house: v.house, kind: v.kind, face: v.face, tall: v.tall,
    map: v.map, pos: v.pos, edge: v.edge, ly: v.ly, min: v.min, meta: v.ly + '  \\u00B7  ' + v.min + '  \\u00B7  ' + v.stops.length + ' stops',
    kicker: v.kind + '  \\u00B7  offered by ' + v.pilot, diff: String(d), pips: pips(d), pts: pts, lane: lane,
    lanePath: "path('" + lane + "')", rec: 'Recommended by ' + v.pilot.split(' ')[0] + ': ' + v.diff,
    stopName: shown[0], stopNote: shown[1], stopN: 'Stop ' + (st.stop >= 0 ? st.stop + 1 : v.stops.length) + ' of ' + v.stops.length,
    story: v.story.map((t, k) => ({ text: t, delay: (0.5 + k * 0.7).toFixed(1) + 's' })) };
  const rewards = [{ big: true, icon: v.flower, title: scaled(v, d) + ' ' + v.el + ' crystals', sub: 'More at a higher difficulty', delay: '1.2s' }]
    .concat(v.extra.map((x, k) => ({ big: false, icon: '', title: x[0], sub: x[1], delay: (1.6 + k * 0.35).toFixed(2) + 's' })));
  const diffs = [1, 2, 3, 4, 5, 6, 7, 8, 9].map((n) => ({ n: String(n), pressed: on(n === d), col: n === d ? '#00041F' : (n <= d ? '#E6E9FF' : '#747BAD'),
    bg: n === d ? '#E6E9FF' : 'rgba(0,4,31,.7)', edge: n === v.diff && n !== d ? 'inset 0 -2px 0 #00D4FF' : 'inset 0 0 0 1px rgba(67,76,137,.8)',
    pick: () => this.setState({ d: n }) }));
  const cards = ['Story', 'Route', 'Rewards'].map((name, k) => ({ name: name, n: '0' + (k + 1), open: k === st.card, shut: k !== st.card,
    pressed: on(k === st.card), h: k === st.card ? '270px' : '0px', op: k === st.card ? 1 : 0, col: k === st.card ? '#E6E9FF' : '#747BAD',
    sum: [v.story[0], v.stops.length + ' stops  \\u00B7  ' + v.ly, scaled(v, d) + ' ' + v.el + ' crystals and ' + v.extra.length + ' more'][k],
    pick: () => this.setState({ card: k }) }));
  // Your Ark: the lobby's seats; with no lobby, the squad's.
  const seatOf = (s, i, mine) => { const c = CREW[s.cs];
    return { who: s.who === 'AI wingmate' ? 'AI wingmate' : s.who, st: s.st, role: s.st === 'Ready' || s.st === 'Choosing' || s.st === 'You' ? '' : s.st, v: s.v, hull: HULL[s.v], pilot: c[0], face: c[2], el: s.e, flower: FLOWER[s.e], art: s.a,
      mine: mine(s), theirs: !mine(s), ready: s.st === 'Ready', choosing: s.st === 'Choosing', pressed: on(i === st.seat),
      edge: i === st.seat ? 'inset 0 -2px 0 #00D4FF' : 'inset 0 -1px 0 rgba(67,76,137,.5)', op: mine(s) || i === st.seat ? 1 : 0.86,
      pick: mine(s) ? () => this.setState({ seat: st.seat === i ? -1 : i }) : () => {} }; };
  const lobby = st.lob.map((s, i) => seatOf(s, i, (x) => x.who === 'You'));
  const sq = SQUADS[st.sq];
  const squadSeats = sq.seats.map((s, i) => seatOf(s, i, (x) => x.who === 'You' || (sq.owner === 'Yours' && x.who === 'AI wingmate')));
  const squads = SQUADS.map((q, i) => ({ name: q.name, owner: q.owner, pressed: on(i === st.sq), col: i === st.sq ? '#E6E9FF' : '#747BAD',
    edge: i === st.sq ? 'inset 0 -2px 0 #00D4FF' : 'inset 0 -1px 0 rgba(67,76,137,.5)', h0: HULL[q.seats[0].v], h1: HULL[q.seats[1].v], h2: HULL[q.seats[2].v],
    who: q.seats.map((x) => x.who === 'AI wingmate' ? 'AI' : x.who).join('  \\u00B7  '), pick: () => this.setState({ sq: i, seat: -1 }) }));
  const editing = st.seat >= 0 && st.lob[st.seat] && st.lob[st.seat].who === 'You';
  const mineSeat = st.lob[0];
  const tray = { open: editing, h: editing ? '200px' : '0px', op: editing ? 1 : 0,
    hulls: Object.keys(HULL).map((h) => ({ name: h, img: HULL[h], pressed: on(mineSeat.v === h), op: mineSeat.v === h ? 1 : 0.55,
      under: mineSeat.v === h ? '#00D4FF' : 'rgba(0,0,0,0)', pick: () => { const lob = st.lob.map((s) => Object.assign({}, s)); lob[0].v = h; this.setState({ lob }); } })),
    els: Object.keys(FLOWER).map((e) => ({ name: e, img: FLOWER[e], pressed: on(mineSeat.e === e), op: mineSeat.e === e ? 1 : 0.5,
      under: mineSeat.e === e ? '#00D4FF' : 'rgba(0,0,0,0)', pick: () => { const lob = st.lob.map((s) => Object.assign({}, s)); lob[0].e = e; this.setState({ lob }); } })),
    close: () => this.setState({ seat: -1 }) };
  return { map, layers, ladder, pins, home, recs, voyage, stops, rewards, diffs, cards, lobby, squads, squadSeats, squad: { name: sq.name, owner: sq.owner }, tray };
}
}"""

# ── Shared pieces ────────────────────────────────────────────────────────────────────────────────────────────────────
PLUS = ('<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">'
        '<path d="M8 2v12M2 8h12"></path></svg>')
MINUS = ('<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">'
         '<path d="M2 8h12"></path></svg>')
ARROW = ('<svg width="18" height="14" viewBox="0 0 18 14" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">'
         '<path d="M1 7h15M10 1l6 6-6 6"></path></svg>')


def layers(extra_style=""):
    return ('<sc-for list="{{layers}}" as="l" hint-placeholder-count="4"><img class="tier" src="{{l.src}}" alt="{{l.alt}}" '
            f'style="object-position:{{{{l.pos}}}};opacity:{{{{l.op}}}};transform:scale({{{{l.sc}}}});{extra_style}"></sc-for>')


def zoom_controls(x, y, vertical=True):
    """The zoom ladder (Galaxy to Course) and + / -; the wheel on the map does the same."""
    rows = ('<sc-for list="{{ladder}}" as="t" hint-placeholder-count="4"><button class="pick" type="button" aria-pressed="{{t.pressed}}" '
            'onClick="{{t.pick}}" style="display:flex;align-items:baseline;gap:16px;height:36px;padding:0 20px 0 16px;box-shadow:inset 2px 0 0 {{t.bar}}">'
            '<span class="cap" style="font-size:14px;width:70px;color:{{t.col}}">{{t.name}}</span>'
            '<span class="num" style="font-size:14px;color:#747BAD">{{t.scale}}</span></button></sc-for>')
    btn = lambda fn, lab, svg: (f'<button class="pick" type="button" aria-label="{lab}" onClick="{{{{map.{fn}}}}}" '
                                f'style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:{INK};'
                                f'background:rgba(0,1,10,.86);box-shadow:inset 0 0 0 1px {LINE}">{svg}</button>')
    return (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;flex-direction:column;gap:12px;z-index:3">'
            f'<div style="display:flex;flex-direction:column;gap:4px;padding:12px 0;background:rgba(0,1,10,.8)">{rows}</div>'
            f'<div style="display:flex;gap:8px">{btn("zin", "Zoom in", PLUS)}{btn("zout", "Zoom out", MINUS)}</div></div>')


def beacon(upright=""):
    """A place on the map: a ring that a press sets the course to, its name, and (on a lying map) a beacon standing up."""
    stalk = ('<span aria-hidden="true" style="position:absolute;left:12px;bottom:14px;width:3px;height:{{p.stalk}};'
             'background:linear-gradient(0deg,#E6E9FF,rgba(230,233,255,0))"></span>') if upright else ""
    return ('<sc-for list="{{pins}}" as="p" hint-placeholder-count="4"><button class="pick abs" type="button" aria-label="{{p.alt}}" aria-pressed="{{p.pressed}}" '
            f'onClick="{{{{p.pick}}}}" style="left:{{{{p.left}}}};top:{{{{p.top}}}};width:28px;height:28px;margin:-28px 0 0 -14px;display:{{{{p.vis}}}};'
            f'transition:left .8s cubic-bezier(.2,.7,.2,1),top .8s cubic-bezier(.2,.7,.2,1);transform-origin:50% 100%;{upright}">'
            f'{stalk}<span style="position:absolute;left:4px;bottom:0;width:20px;height:20px;border-radius:50%;box-sizing:border-box;'
            'border:2px solid {{p.ring}};box-shadow:{{p.glow}};background:rgba(0,1,10,.6)"></span>'
            '<sc-if value="{{p.label}}" hint-placeholder-val="{{ true }}"><span class="cap" style="position:absolute;left:34px;bottom:0;'
            'font-size:14px;white-space:nowrap;color:#E6E9FF;text-shadow:0 1px 3px #00010A">{{p.place}}</span></sc-if></button></sc-for>')


def home_mark(upright=""):
    return ('<span class="abs" aria-label="Tumult, where the Ark is moored" role="img" style="left:{{home.left}};top:{{home.top}};width:18px;height:18px;'
            f'margin:-9px 0 0 -9px;transform:rotate(45deg) {upright};border:2px solid #FFAE00;box-sizing:border-box;'
            'transition:left .8s cubic-bezier(.2,.7,.2,1),top .8s cubic-bezier(.2,.7,.2,1)"></span>')


def readout(x, y, align="left"):
    return (f'<div class="abs" style="{align}:{x}px;top:{y}px;display:flex;flex-direction:column;gap:6px;z-index:3;text-align:{align}">'
            '<span class="cap" style="font-size:14px">{{map.name}}</span><span class="num" style="font-size:14px;color:#747BAD">{{map.readout}}</span></div>')


def rec_list(x, y, w, card_h=140, gap=14, label="Your crew recommends"):
    """The voyages your pilots offer, each a card: who offers it, what kind of job, the title, difficulty, the pay."""
    return (f'<section aria-label="{label}" class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:{gap}px;z-index:3">'
            f'<span class="lab">{label}</span>'
            '<sc-for list="{{recs}}" as="c" hint-placeholder-count="4"><button class="pick" type="button" aria-pressed="{{c.pressed}}" onClick="{{c.pick}}" '
            f'style="position:relative;height:{card_h}px;display:flex;gap:20px;align-items:center;padding:0 18px;background:{{{{c.bg}}}};'
            'box-shadow:{{c.edge}};opacity:{{c.op}};transition:background .3s,opacity .3s">'
            f'<img src="{{{{c.face}}}}" alt="{{{{c.pilot}}}}" style="width:96px;height:{card_h - 28}px;object-fit:cover;object-position:50% 28%">'
            '<span style="display:flex;flex-direction:column;gap:10px;min-width:0;flex:1">'
            '<span class="lab" style="color:#E6E9FF">{{c.kind}}</span>'
            '<span style="font-size:18px;line-height:1.35">{{c.title}}</span>'
            '<span style="display:flex;align-items:center;gap:14px;font-size:14px;color:#747BAD">'
            '<span class="pips"><span style="width:{{c.pips}}"></span></span><span class="num">{{c.diff}}</span>'
            '<img src="{{c.flower}}" alt="" style="width:18px;height:18px;margin-left:8px"><span class="num">{{c.reward}}</span></span>'
            '</span></button></sc-for></section>')


def open_cta(x, y, w):
    return (f'<a class="btn soft-cta abs" href="{link("P2A")}" style="left:{x}px;top:{y}px;width:{w}px">'
            f'<span class="kc">A</span>Open voyage{ARROW}</a>')


# ── Page 1 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

def p1a():
    """Table top: the chart lies on a table; the edge-on view is its front; places stand up as beacons."""
    up = "transform:rotateX(-50deg)"
    table = f"""<div class="abs" onWheel="{{{{map.wheel}}}}" style="left:200px;top:170px;width:1080px;height:760px;perspective:1600px;perspective-origin:50% 10%">
<div style="position:absolute;left:80px;top:40px;width:920px;height:620px;transform-style:preserve-3d;transform:rotateX(50deg);transform-origin:50% 0%">
<div style="position:absolute;inset:0;overflow:hidden;background:#00010A;box-shadow:0 0 0 2px rgba(116,123,173,.5),0 40px 120px rgba(0,0,0,.7)">{layers()}</div>
<div style="position:absolute;inset:0;transform-style:preserve-3d">{home_mark()}{beacon(up)}</div>
<figure style="position:absolute;left:0;top:100%;width:100%;height:150px;margin:0;transform-origin:50% 0;transform:rotateX(-90deg);overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 2px rgba(116,123,173,.5)">
<img src="{{{{voyage.edge}}}}" alt="The course seen edge on, the table's front" style="width:100%;height:100%;object-fit:cover;object-position:40% 82%;opacity:.9"></figure>
</div></div>"""
    return window(1, table + zoom_controls(40, 196) + rec_list(1400, 176, 480) + open_cta(1400, 852, 480),
                  f"Rev {REV} P1A - Table top: the HyperSea on a chart table, its edge view the front; the crew's voyages on the right (recommended) - interactive")


def p1b():
    """Orrery: the galaxy as a disc in the sea; a slow ring of bearings round it; the voyages on the left."""
    up = "transform:rotateX(-62deg)"
    ring = ('<div aria-hidden="true" class="spin" style="position:absolute;inset:-46px;border-radius:50%;'
            'background:repeating-conic-gradient(rgba(230,233,255,.5) 0 .25deg,rgba(0,0,0,0) .25deg 5deg);'
            '-webkit-mask:radial-gradient(circle,transparent 69.4%,#000 69.6%,#000 71%,transparent 71.2%);'
            'mask:radial-gradient(circle,transparent 69.4%,#000 69.6%,#000 71%,transparent 71.2%)"></div>'
            '<div aria-hidden="true" style="position:absolute;inset:-14px;border-radius:50%;box-shadow:inset 0 0 0 1px rgba(116,123,173,.55)"></div>')
    disc = f"""<div class="abs" onWheel="{{{{map.wheel}}}}" style="left:560px;top:160px;width:1360px;height:780px;perspective:1600px;perspective-origin:50% 10%">
<div style="position:absolute;left:250px;top:-40px;width:860px;height:860px;transform-style:preserve-3d;transform:rotateX(62deg)">
{ring}<div style="position:absolute;inset:0;border-radius:50%;overflow:hidden;background:#00010A;box-shadow:0 0 120px rgba(117,143,251,.25)">{layers()}</div>
<div style="position:absolute;inset:0;transform-style:preserve-3d">{home_mark()}{beacon(up)}</div>
</div></div>"""
    recs = rec_list(40, 176, 500, card_h=140)
    return window(1, disc + recs + zoom_controls(1700, 196) + open_cta(40, 852, 500),
                  f"Rev {REV} P1B - Orrery: the galaxy as a disc floating in the sea, a slow ring of bearings; the voyages on the left - interactive")


def p1c():
    """Bridge: the map flat and full-bleed; the crew's voyages along its foot like the bridge's screens."""
    sea = f"""<div class="abs" onWheel="{{{{map.wheel}}}}" style="left:0;top:84px;width:1920px;height:856px;overflow:hidden;background:#00010A">{layers()}
<div aria-hidden="true" style="position:absolute;inset:0;background:linear-gradient(180deg,rgba(0,1,10,.85) 0,rgba(0,1,10,0) 22%,rgba(0,1,10,0) 50%,rgba(0,1,10,.92) 100%)"></div>
{home_mark()}{beacon()}</div>"""
    row = ('<section aria-label="Your crew recommends" class="abs" style="left:40px;top:700px;width:1840px;z-index:3;display:flex;flex-direction:column;gap:14px">'
           '<div style="display:flex;align-items:center;justify-content:space-between"><span class="lab">Your crew recommends</span></div>'
           '<div style="display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:20px">'
           '<sc-for list="{{recs}}" as="c" hint-placeholder-count="4"><button class="pick" type="button" aria-pressed="{{c.pressed}}" onClick="{{c.pick}}" '
           'style="position:relative;height:150px;display:flex;gap:18px;align-items:center;padding:0 16px;background:{{c.bg}};box-shadow:{{c.edge}};opacity:{{c.op}};transition:background .3s,opacity .3s">'
           '<img src="{{c.face}}" alt="{{c.pilot}}" style="width:92px;height:122px;object-fit:cover;object-position:50% 28%">'
           '<span style="display:flex;flex-direction:column;gap:10px;min-width:0;flex:1"><span class="lab" style="color:#E6E9FF">{{c.kind}}</span>'
           '<span style="font-size:18px;line-height:1.35">{{c.title}}</span>'
           '<span style="display:flex;align-items:center;gap:12px;font-size:14px;color:#747BAD"><span class="pips"><span style="width:{{c.pips}}"></span></span>'
           '<span class="num">{{c.diff}}</span></span></span></button></sc-for></div></section>')
    return window(1, sea + zoom_controls(40, 176) + readout(40, 176, "right") + row + open_cta(1500, 630, 380),
                  f"Rev {REV} P1C - Bridge: the map flat and full-bleed; the crew's voyages along its foot - interactive")


def p1d():
    """Dive: Galaxy, Sector and Course as three glass sheets, one under the other; zoom is a dive between them."""
    sheets = []
    rows = [(0, "Galaxy", TIERS[0][2], "50% 50%", -70), (2, "Sector", TIERS[2][2], "50% 45%", 140), (3, "Course", "{{voyage.map}}", "{{voyage.pos}}", 350)]
    for z, name, src, pos, top in rows:
        lit = {0: "{{sheet.g}}", 2: "{{sheet.s}}", 3: "{{sheet.c}}"}[z]
        pins = (f'<div style="position:absolute;inset:0">{home_mark()}{beacon()}</div>' if z == 2 else "")
        route = ('<svg aria-hidden="true" viewBox="0 0 1000 600" preserveAspectRatio="none" style="position:absolute;inset:0;width:100%;height:100%">'
                 '<polyline class="draw" points="{{voyage.pts}}" fill="none" stroke="#E6E9FF" stroke-width="4" stroke-linejoin="round"></polyline></svg>') if z == 3 else ""
        # A sheet is a div: its own press is a clear button under the places, so no button sits inside another.
        sheets.append(
            f'<div style="position:absolute;left:0;top:{top}px;width:960px;height:440px;transform:rotateX(60deg);transform-origin:50% 50%;'
            f'opacity:{lit};transition:opacity .5s ease">'
            f'<div style="position:absolute;inset:0;overflow:hidden;background:#00010A;box-shadow:0 0 0 2px rgba(116,123,173,.55),0 30px 90px rgba(0,0,0,.6)">'
            f'<img src="{src}" alt="" style="width:100%;height:100%;object-fit:cover;object-position:{pos}">{route}</div>'
            f'<button class="pick" type="button" aria-label="Dive to the {name} tier" onClick="{{{{dive.{name.lower()}}}}}" style="position:absolute;inset:0"></button>'
            f'{pins}</div>'
            f'<span class="cap" style="position:absolute;left:-150px;top:{top + 206}px;font-size:15px;opacity:{lit}">{name}</span>')
    stack = (f'<div class="abs" onWheel="{{{{map.wheel}}}}" style="left:300px;top:170px;width:960px;height:740px;perspective:1700px;perspective-origin:50% 40%">'
             + '<span aria-hidden="true" style="position:absolute;left:469px;top:150px;width:2px;height:420px;background:linear-gradient(180deg,rgba(255,174,0,.8),rgba(255,174,0,.12))"></span>'
             + "".join(sheets) + '</div>')
    body = stack + rec_list(1400, 176, 480) + open_cta(1400, 852, 480)
    html = window(1, body, f"Rev {REV} P1D - Dive: Galaxy, Sector and Course as three glass sheets; a zoom is a dive between them - interactive")
    # The dive's own values: which sheet is lit (the map's zoom tier), and a press on a sheet dives to it.
    return html.replace("  return { map, layers,", "  const lit = (k) => z === k || (k === 2 && z === 1) ? 1 : 0.4;\n"
                        "  const dive = { galaxy: () => this.go(0), sector: () => this.go(2), course: () => this.go(3) };\n"
                        "  const sheet = { g: lit(0), s: lit(2), c: lit(3) };\n  return { dive, sheet, map, layers,")


# ── Page 2 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

def diff_row(x, y, w):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:14px;z-index:3">'
            '<div style="display:flex;align-items:baseline;justify-content:space-between"><span class="lab">Difficulty</span>'
            '<span style="font-size:14px;color:#747BAD">{{voyage.rec}}</span></div>'
            '<div role="group" aria-label="Difficulty, 1 to 9" style="display:grid;grid-template-columns:repeat(9,minmax(0,1fr));gap:6px">'
            '<sc-for list="{{diffs}}" as="n" hint-placeholder-count="9"><button class="pick num" type="button" aria-pressed="{{n.pressed}}" onClick="{{n.pick}}" '
            'style="height:44px;display:flex;align-items:center;justify-content:center;font-size:18px;color:{{n.col}};background:{{n.bg}};box-shadow:{{n.edge}};transition:background .25s,color .25s">{{n.n}}</button></sc-for>'
            '</div></div>')


def reward_list(x, y, w, gap=16):
    return (f'<section aria-label="Rewards" class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:{gap}px;z-index:3">'
            '<span class="lab">Rewards</span>'
            '<sc-for list="{{rewards}}" as="r" hint-placeholder-count="4"><div class="rise" style="display:flex;align-items:center;gap:18px;animation-delay:{{r.delay}};'
            'padding-bottom:14px;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)">'
            '<sc-if value="{{r.big}}" hint-placeholder-val="{{ false }}"><img class="pop" src="{{r.icon}}" alt="" style="width:44px;height:44px;animation-delay:{{r.delay}}"></sc-if>'
            '<span style="display:flex;flex-direction:column;gap:6px"><span style="font-size:18px;line-height:1.35">{{r.title}}</span>'
            '<span style="font-size:14px;color:#747BAD">{{r.sub}}</span></span></div></sc-for></section>')


def route_map(x, y, w, h, labels=True):
    """The course: the voyage's own water, the route drawing itself, each stop arriving as the line reaches it."""
    lab = ('<span class="cap" style="position:absolute;{{s.side}};top:-4px;font-size:14px;white-space:nowrap;color:{{s.col}};text-shadow:0 1px 3px #00010A">{{s.name}}</span>'
           if labels else "")
    return (f'<figure class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;margin:0;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px {LINE};z-index:2">'
            '<img src="{{voyage.map}}" alt="The voyage\'s water on the HyperSea" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{voyage.pos}}">'
            '<div aria-hidden="true" style="position:absolute;inset:0;background:radial-gradient(ellipse 80% 80% at 50% 50%,rgba(0,1,10,.2),rgba(0,1,10,.7))"></div>'
            '<svg aria-hidden="true" viewBox="0 0 1000 600" preserveAspectRatio="none" style="position:absolute;inset:0;width:100%;height:100%">'
            '<polyline points="{{voyage.pts}}" fill="none" stroke="rgba(230,233,255,.18)" stroke-width="10" stroke-linejoin="round"></polyline>'
            '<polyline class="draw" points="{{voyage.pts}}" fill="none" stroke="#E6E9FF" stroke-width="3" stroke-linejoin="round"></polyline></svg>'
            '<sc-for list="{{stops}}" as="s" hint-placeholder-count="4"><button class="pick pop abs" type="button" aria-label="{{s.name}}" aria-pressed="{{s.pressed}}" '
            'onClick="{{s.pick}}" style="left:{{s.x}};top:{{s.y}};width:24px;height:24px;margin:-12px 0 0 -12px;animation-delay:{{s.delay}}">'
            '<span style="position:absolute;inset:0;border-radius:50%;box-sizing:border-box;border:2px solid {{s.ring}};background:rgba(0,1,10,.75)"></span>'
            f'{lab}</button></sc-for></figure>')


def foot(x, y, back_href, cta_words, cta_href, cta_w=420, back_w=220):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;gap:20px;z-index:3">'
            f'<a class="btn soft" href="{back_href}" style="width:{back_w}px"><span class="kc">B</span>Back</a>'
            f'<a class="btn soft-cta" href="{cta_href}" style="width:{cta_w}px"><span class="kc">A</span>{cta_words}{ARROW}</a></div>')


def p2a():
    """Briefing: the pilot's portrait and story; the route drawing itself; the rewards arriving one by one."""
    left = ('<section aria-label="Who offers it" class="abs" style="left:40px;top:176px;width:520px;height:748px;overflow:hidden;z-index:2">'
            '<img src="{{voyage.tall}}" alt="{{voyage.pilot}}" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:50% 20%">'
            '<div aria-hidden="true" style="position:absolute;inset:0;background:linear-gradient(180deg,rgba(0,1,10,0) 25%,rgba(0,1,10,.92) 62%,#00010A 100%)"></div>'
            '<div style="position:absolute;left:32px;right:32px;bottom:32px;display:flex;flex-direction:column;gap:12px">'
            '<span class="lab" style="color:#E6E9FF">{{voyage.house}}</span><span style="font-size:28px;line-height:1.2">{{voyage.pilot}}</span>'
            '<div style="display:flex;flex-direction:column;gap:14px;margin-top:14px">'
            '<sc-for list="{{voyage.story}}" as="l" hint-placeholder-count="3"><p class="rise" style="margin:0;font-size:18px;line-height:1.55;color:#E6E9FF;animation-delay:{{l.delay}}">{{l.text}}</p></sc-for>'
            '</div></div></section>')
    title = ('<div class="abs" style="left:600px;top:176px;width:780px;display:flex;flex-direction:column;gap:12px;z-index:2">'
             '<span class="lab">{{voyage.kicker}}</span><span style="font-size:36px;line-height:1.25">{{voyage.title}}</span>'
             '<span class="num" style="font-size:15px;color:#747BAD">{{voyage.meta}}</span></div>')
    caption = ('<div class="abs" style="left:600px;top:808px;width:780px;height:116px;display:flex;flex-direction:column;justify-content:center;gap:8px;z-index:2">'
               '<span class="lab">{{voyage.stopN}}</span><span style="font-size:20px">{{voyage.stopName}}</span>'
               '<span style="font-size:15px;line-height:1.5;color:#747BAD">{{voyage.stopNote}}</span></div>')
    body = (left + title + route_map(600, 316, 780, 476) + caption + diff_row(1420, 176, 460) + reward_list(1420, 316, 460)
            + foot(1420, 868, link("P1A"), "Ready your Ark", link("P3A"), cta_w=280, back_w=160))
    return window(2, body, f"Rev {REV} P2A - Briefing: the pilot's story, the route drawing itself, the rewards arriving; difficulty 1-9 (recommended) - interactive",
                  back=link("P1A"))


def p2b():
    """Journey: one lane across the window, the Ark sailing it; each stop opens its part of the story."""
    bg = ('<img src="{{voyage.map}}" alt="" style="position:absolute;left:0;top:84px;width:1920px;height:996px;object-fit:cover;object-position:{{voyage.pos}};opacity:.55">'
          '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:linear-gradient(180deg,rgba(0,1,10,.94) 0,rgba(0,1,10,.55) 35%,rgba(0,1,10,.55) 60%,rgba(0,1,10,.96) 100%)"></div>')
    top = ('<div class="abs" style="left:40px;top:176px;width:1840px;display:flex;gap:28px;align-items:center;z-index:3">'
           '<img src="{{voyage.face}}" alt="{{voyage.pilot}}" style="width:112px;height:132px;object-fit:cover;object-position:50% 28%">'
           '<div style="display:flex;flex-direction:column;gap:12px;flex:1"><span class="lab">{{voyage.kicker}}</span>'
           '<span style="font-size:36px;line-height:1.25">{{voyage.title}}</span>'
           '<span class="num" style="font-size:15px;color:#747BAD">{{voyage.meta}}</span></div></div>')
    lane = ('<div class="abs" style="left:0;top:356px;width:1920px;height:400px;z-index:2">'
            '<svg aria-hidden="true" width="1920" height="400" style="position:absolute;left:0;top:0">'
            '<path d="{{voyage.lane}}" fill="none" stroke="rgba(230,233,255,.16)" stroke-width="12" stroke-linejoin="round"></path>'
            '<path class="draw" d="{{voyage.lane}}" fill="none" stroke="#E6E9FF" stroke-width="3" stroke-linejoin="round" style="stroke-dasharray:2400;stroke-dashoffset:2400"></path></svg>'
            '<span class="sailer" aria-hidden="true" style="position:absolute;left:0;top:0;width:26px;height:16px;offset-path:{{voyage.lanePath}};'
            'background:#FFAE00;clip-path:polygon(0 0,100% 50%,0 100%,22% 50%)"></span>'
            '<sc-for list="{{stops}}" as="s" hint-placeholder-count="4"><div class="abs" style="left:{{s.lx}};top:{{s.ly}};width:0;height:0">'
            '<button class="pick pop" type="button" aria-label="{{s.name}}" aria-pressed="{{s.pressed}}" onClick="{{s.pick}}" '
            'style="position:absolute;left:-16px;top:-16px;width:32px;height:32px;animation-delay:{{s.delay}}">'
            '<span style="position:absolute;inset:0;border-radius:50%;box-sizing:border-box;border:2px solid {{s.ring}};background:rgba(0,1,10,.8)"></span></button>'
            '<span class="cap rise" style="position:absolute;left:-120px;top:30px;width:240px;text-align:center;font-size:14px;color:{{s.col}};animation-delay:{{s.delay}}">'
            '<span class="num" style="margin-right:10px">{{s.n}}</span>{{s.name}}</span></div></sc-for></div>')
    story = ('<div class="abs" style="left:40px;top:330px;width:1840px;height:36px;display:flex;justify-content:center;z-index:3">'
             '<p class="rise" style="margin:0;font-size:18px;line-height:1.5;color:#E6E9FF;text-align:center;max-width:1100px;animation-delay:.4s">'
             '{{voyage.stopName}}  ·  {{voyage.stopNote}}</p></div>')
    haul = ('<section aria-label="Rewards" class="abs" style="left:40px;top:790px;width:1180px;display:flex;flex-direction:column;gap:16px;z-index:3">'
            '<span class="lab">What it pays</span><div style="display:flex;gap:36px">'
            '<sc-for list="{{rewards}}" as="r" hint-placeholder-count="4"><div class="rise" style="display:flex;align-items:center;gap:14px;animation-delay:{{r.delay}}">'
            '<sc-if value="{{r.big}}" hint-placeholder-val="{{ false }}"><img class="pop" src="{{r.icon}}" alt="" style="width:40px;height:40px;animation-delay:{{r.delay}}"></sc-if>'
            '<span style="display:flex;flex-direction:column;gap:6px"><span style="font-size:18px;white-space:nowrap">{{r.title}}</span>'
            '<span style="font-size:14px;color:#747BAD;white-space:nowrap">{{r.sub}}</span></span></div></sc-for></div></section>')
    body = bg + top + story + lane + haul + diff_row(1420, 690, 460) + foot(1420, 868, link("P1A"), "Ready your Ark", link("P3A"), cta_w=280, back_w=160)
    return window(2, body, f"Rev {REV} P2B - Journey: one lane across the window, the Ark sailing it; each stop opens its part of the story - interactive",
                  back=link("P1A"), ground=GROUND + bg)


def p2c():
    """Dossier: the map stays where page 1 left it; Story, Route and Rewards are cards that slide open."""
    head = ('<div class="abs" style="left:1220px;top:172px;width:660px;display:flex;gap:22px;align-items:center;z-index:3">'
            '<img src="{{voyage.face}}" alt="{{voyage.pilot}}" style="width:76px;height:90px;object-fit:cover;object-position:50% 28%">'
            '<div style="display:flex;flex-direction:column;gap:10px"><span class="lab">{{voyage.kicker}}</span>'
            '<span style="font-size:22px;line-height:1.25">{{voyage.title}}</span></div></div>')
    stack = ('<section aria-label="The voyage" class="abs" style="left:1220px;top:392px;width:660px;display:flex;flex-direction:column;gap:10px;z-index:3">'
             '<sc-for list="{{cards}}" as="k" hint-placeholder-count="3"><div style="background:rgba(0,4,31,.82);box-shadow:inset 0 -1px 0 rgba(67,76,137,.6)">'
             '<button class="pick" type="button" aria-expanded="{{k.pressed}}" onClick="{{k.pick}}" style="width:100%;height:64px;display:flex;align-items:center;gap:18px;padding:0 22px;box-sizing:border-box">'
             '<span class="num" style="font-size:14px;color:#747BAD">{{k.n}}</span><span class="cap" style="font-size:15px;color:{{k.col}}">{{k.name}}</span>'
             '<sc-if value="{{k.shut}}" hint-placeholder-val="{{ true }}"><span class="ell" style="margin-left:auto;max-width:380px;font-size:14px;color:#747BAD">{{k.sum}}</span></sc-if></button>'
             '<div class="slide" style="max-height:{{k.h}};opacity:{{k.op}}"><div style="padding:6px 22px 26px">'
             '<sc-if value="{{k.open}}" hint-placeholder-val="{{ false }}">{{CARD}}</sc-if></div></div></div></sc-for></section>')
    # One template for the open card: the three bodies sit side by side and each shows only on its own card.
    bodies = ('<div style="display:flex;flex-direction:column;gap:14px">'
              '<sc-if value="{{cardIs.story}}" hint-placeholder-val="{{ true }}"><sc-for list="{{voyage.story}}" as="l" hint-placeholder-count="3">'
              '<p class="rise" style="margin:0;font-size:18px;line-height:1.55;animation-delay:{{l.delay}}">{{l.text}}</p></sc-for></sc-if>'
              '<sc-if value="{{cardIs.route}}" hint-placeholder-val="{{ false }}"><sc-for list="{{stops}}" as="s" hint-placeholder-count="4">'
              '<button class="pick rise" type="button" aria-pressed="{{s.pressed}}" onClick="{{s.pick}}" style="display:flex;gap:16px;align-items:baseline;animation-delay:{{s.delay}}">'
              '<span class="num" style="font-size:14px;color:#747BAD;width:28px">{{s.n}}</span><span style="display:flex;flex-direction:column;gap:6px">'
              '<span style="font-size:18px;color:{{s.col}}">{{s.name}}</span><span style="font-size:14px;line-height:1.5;color:#747BAD">{{s.note}}</span></span></button></sc-for></sc-if>'
              '<sc-if value="{{cardIs.rewards}}" hint-placeholder-val="{{ false }}"><sc-for list="{{rewards}}" as="r" hint-placeholder-count="4">'
              '<div class="rise" style="display:flex;align-items:center;gap:16px;animation-delay:{{r.delay}}"><sc-if value="{{r.big}}" hint-placeholder-val="{{ false }}">'
              '<img src="{{r.icon}}" alt="" style="width:40px;height:40px"></sc-if><span style="display:flex;flex-direction:column;gap:6px">'
              '<span style="font-size:18px">{{r.title}}</span><span style="font-size:14px;color:#747BAD">{{r.sub}}</span></span></div></sc-for></sc-if></div>')
    stack = stack.replace("{{CARD}}", bodies)
    edge = ('<figure class="abs" style="left:40px;top:796px;width:1140px;height:128px;margin:0;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89;z-index:2">'
            '<img src="{{voyage.edge}}" alt="The course seen edge on" style="width:100%;height:100%;object-fit:cover;object-position:40% 82%"></figure>')
    body = (route_map(40, 176, 1140, 604) + edge + head + stack + diff_row(1220, 288, 660)
            + foot(1220, 884, link("P1A"), "Ready your Ark", link("P3A"), cta_w=420))
    html = window(2, body, f"Rev {REV} P2C - Dossier: the map stays; Story, Route and Rewards as cards that slide open - interactive", back=link("P1A"))
    return html.replace("  return { map, layers,", "  const cardIs = { story: st.card === 0, route: st.card === 1, rewards: st.card === 2 };\n  return { cardIs, map, layers,")


# ── Page 3 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

def voyage_chip(x, y, w):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:72px;display:flex;align-items:center;gap:18px;z-index:3">'
            '<img src="{{voyage.face}}" alt="{{voyage.pilot}}" style="width:60px;height:72px;object-fit:cover;object-position:50% 28%">'
            '<span style="display:flex;flex-direction:column;gap:8px;min-width:0"><span class="ell" style="font-size:18px">{{voyage.title}}</span>'
            '<span style="display:flex;align-items:center;gap:12px;font-size:14px;color:#747BAD"><span>{{voyage.kind}}</span>'
            '<span class="pips"><span style="width:{{voyage.pips}}"></span></span><span class="num">{{voyage.diff}}</span></span></span></div>')


def seat_cols(list_name, x, y, w, h, gap=34, change=True):
    """The three seats: whose seat, the vessel large over its light, the pilot, the element's flower, the artifact."""
    chg = ('<sc-if value="{{s.mine}}" hint-placeholder-val="{{ true }}"><span class="cap" style="position:absolute;right:24px;top:24px;font-size:14px;color:#E6E9FF">Change</span></sc-if>'
           if change else "")
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:{gap}px;z-index:2">'
            f'<sc-for list="{{{{{list_name}}}}}" as="s" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{{{s.pressed}}}}" onClick="{{{{s.pick}}}}" '
            f'style="position:relative;height:{h}px;background:rgba(0,4,31,.6);box-shadow:{{{{s.edge}}}};opacity:{{{{s.op}}}};transition:opacity .3s">'
            '<span style="position:absolute;left:24px;top:24px;display:flex;flex-direction:column;gap:8px"><span style="font-size:22px">{{s.who}}</span>'
            '<sc-if value="{{s.ready}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:14px;color:#E6E9FF">Ready</span></sc-if>'
            '<sc-if value="{{s.choosing}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:14px;color:#747BAD">Choosing</span></sc-if>'
            '<span style="font-size:14px;color:#747BAD">{{s.role}}</span></span>'
            f'{chg}'
            f'<span aria-hidden="true" style="position:absolute;left:8%;right:8%;top:{round(h * .26)}px;height:{round(h * .36)}px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(230,233,255,.16),rgba(230,233,255,0))"></span>'
            f'<img class="hull" src="{{{{s.hull}}}}" alt="{{{{s.v}}}}" style="position:absolute;left:10%;width:80%;top:{round(h * .2)}px;height:{round(h * .42)}px">'
            '<span style="position:absolute;left:24px;right:24px;bottom:24px;display:flex;align-items:center;gap:16px">'
            '<img src="{{s.face}}" alt="" style="width:64px;height:76px;object-fit:cover;object-position:50% 28%">'
            '<span style="display:flex;flex-direction:column;gap:8px;min-width:0;flex:1"><span style="font-size:20px">{{s.v}}</span>'
            '<span class="ell" style="font-size:14px;color:#747BAD">{{s.pilot}}</span></span>'
            '<span style="display:flex;flex-direction:column;align-items:center;gap:6px"><img src="{{s.flower}}" alt="" style="width:30px;height:30px">'
            '<span style="font-size:14px;color:#747BAD">{{s.el}}</span></span>'
            '<span style="font-size:14px;color:#E6E9FF;width:96px;text-align:right">{{s.art}}</span></span>'
            '</button></sc-for></div>')


def tray(x, y, w):
    """Your seat, changed in place: one press on a vessel or an element."""
    return (f'<div class="slide abs" style="left:{x}px;top:{y}px;width:{w}px;max-height:{{{{tray.h}}}};opacity:{{{{tray.op}}}};z-index:4;background:rgba(0,1,10,.94);box-shadow:inset 0 1px 0 #434C89">'
            '<div style="display:flex;gap:56px;align-items:center;padding:26px 34px">'
            '<div style="display:flex;flex-direction:column;gap:14px"><span class="lab">Vessel</span><div style="display:flex;gap:14px">'
            '<sc-for list="{{tray.hulls}}" as="o" hint-placeholder-count="6"><button class="pick" type="button" aria-pressed="{{o.pressed}}" onClick="{{o.pick}}" '
            'style="width:150px;display:flex;flex-direction:column;align-items:center;gap:8px;opacity:{{o.op}}">'
            '<img class="hull" src="{{o.img}}" alt="" style="width:140px;height:56px"><span style="font-size:14px;padding-bottom:6px;border-bottom:2px solid {{o.under}}">{{o.name}}</span></button></sc-for></div></div>'
            '<div style="display:flex;flex-direction:column;gap:14px"><span class="lab">Element</span><div style="display:flex;gap:18px">'
            '<sc-for list="{{tray.els}}" as="o" hint-placeholder-count="4"><button class="pick" type="button" aria-pressed="{{o.pressed}}" onClick="{{o.pick}}" '
            'style="width:76px;display:flex;flex-direction:column;align-items:center;gap:8px;opacity:{{o.op}}">'
            '<img src="{{o.img}}" alt="" style="width:46px;height:46px"><span style="font-size:14px;padding-bottom:6px;border-bottom:2px solid {{o.under}}">{{o.name}}</span></button></sc-for></div></div>'
            '<button class="btn sm soft" type="button" onClick="{{tray.close}}" style="margin-left:auto;width:160px"><span class="kc">B</span>Done</button>'
            '</div></div>')


def p3a():
    """Lobby: who is in your lobby flies this voyage with you; your own seat changes in place."""
    body = ('<div class="abs" style="left:40px;top:200px;z-index:3"><span class="lab">Your lobby</span></div>'
            + voyage_chip(1180, 176, 700) + seat_cols("lobby", 40, 280, 1840, 560) + tray(40, 640, 1840)
            + '<div class="abs" style="left:40px;top:868px;display:flex;gap:20px;z-index:3">'
            + f'<a class="btn soft" href="{link("P3B")}" style="width:340px"><span class="kc">X</span>Fly a squad instead</a></div>'
            + f'<div class="abs" style="left:1240px;top:868px;display:flex;gap:20px;z-index:3"><a class="btn soft" href="{link("P2A")}" style="width:200px"><span class="kc">B</span>Back</a>'
            + '<a class="btn soft-cta" href="#" style="width:420px"><span class="kc">A</span>Set sail</a></div>')
    return window(3, body, f"Rev {REV} P3A - Lobby: your lobby's three seats as they stand; your seat changes in place; Set sail (recommended) - interactive",
                  back=link("P2A"))


def p3b():
    """No lobby: the same page with nobody in the lobby; a squad is picked from the list, and friends can be invited."""
    squads = ('<section aria-label="Your squads" class="abs" style="left:40px;top:280px;width:420px;display:flex;flex-direction:column;gap:12px;z-index:3">'
              '<sc-for list="{{squads}}" as="q" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{q.pressed}}" onClick="{{q.pick}}" '
              'style="position:relative;height:150px;background:rgba(0,4,31,.6);box-shadow:{{q.edge}}">'
              '<span style="position:absolute;left:22px;top:22px;display:flex;flex-direction:column;gap:8px"><span style="font-size:20px;color:{{q.col}}">{{q.name}}</span>'
              '<span style="font-size:14px;color:#747BAD">{{q.owner}}</span></span>'
              '<span style="position:absolute;left:22px;right:22px;bottom:18px;display:flex;align-items:flex-end;justify-content:space-between">'
              '<span style="font-size:14px;color:#747BAD">{{q.who}}</span>'
              '<span style="position:relative;width:150px;height:60px"><img class="hull" src="{{q.h1}}" alt="" style="position:absolute;left:0;top:0;width:70px;height:24px;opacity:.8">'
              '<img class="hull" src="{{q.h2}}" alt="" style="position:absolute;right:0;top:0;width:70px;height:24px;opacity:.8">'
              '<img class="hull" src="{{q.h0}}" alt="" style="position:absolute;left:30px;bottom:0;width:90px;height:30px"></span></span></button></sc-for>'
              '<a class="btn soft" href="#" style="width:100%;margin-top:8px"><span class="kc">Y</span>Invite friends</a></section>')
    body = ('<div class="abs" style="left:40px;top:186px;display:flex;flex-direction:column;gap:10px;z-index:3"><span class="lab">Nobody in your lobby</span>'
            '<span style="font-size:15px;color:#747BAD">Pick a squad, or invite friends to fill its seats</span></div>'
            + voyage_chip(1180, 176, 700) + squads + seat_cols("squadSeats", 500, 280, 1380, 560, gap=24, change=False)
            + f'<div class="abs" style="left:1240px;top:868px;display:flex;gap:20px;z-index:3"><a class="btn soft" href="{link("P2A")}" style="width:200px"><span class="kc">B</span>Back</a>'
            + '<a class="btn soft-cta" href="#" style="width:420px"><span class="kc">A</span>Set sail</a></div>')
    return window(3, body, f"Rev {REV} P3B - No lobby: pick a squad from the list, or invite friends; Set sail - interactive", back=link("P2A"))


def p3c():
    """Formation: the three vessels fly in formation over the course; a press on one opens its tray."""
    bg = ('<img src="{{voyage.map}}" alt="" style="position:absolute;left:0;top:84px;width:1920px;height:996px;object-fit:cover;object-position:{{voyage.pos}};opacity:.5;filter:blur(6px)">'
          '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:radial-gradient(ellipse 60% 50% at 50% 55%,rgba(0,1,10,.2),rgba(0,1,10,.92))"></div>')
    slots = [(680, 290, 560, "0"), (160, 430, 500, "1"), (1260, 430, 500, "2")]
    ships = []
    for x, y, w, i in slots:
        ships.append(
            f'<button class="pick abs" type="button" aria-pressed="{{{{lobby.{i}.pressed}}}}" onClick="{{{{lobby.{i}.pick}}}}" style="left:{x}px;top:{y}px;width:{w}px;height:{round(w * .62)}px;z-index:2">'
            f'<span aria-hidden="true" style="position:absolute;left:0;right:0;top:20%;height:60%;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(230,233,255,.14),rgba(230,233,255,0))"></span>'
            f'<img class="hull" src="{{{{lobby.{i}.hull}}}}" alt="{{{{lobby.{i}.v}}}}" style="position:absolute;left:0;top:14%;width:100%;height:46%">'
            f'<span style="position:absolute;left:0;right:0;bottom:0;display:flex;flex-direction:column;align-items:center;gap:8px">'
            f'<span style="font-size:20px;padding-bottom:6px;box-shadow:{{{{lobby.{i}.edge}}}}">{{{{lobby.{i}.who}}}}</span>'
            f'<span style="display:flex;align-items:center;gap:12px;font-size:14px;color:#747BAD"><span>{{{{lobby.{i}.v}}}}</span>'
            f'<img src="{{{{lobby.{i}.flower}}}}" alt="" style="width:20px;height:20px"><span>{{{{lobby.{i}.pilot}}}}</span></span></span></button>')
    rail = ('<section aria-label="Your lobby" class="abs" style="left:40px;top:186px;display:flex;align-items:center;gap:30px;z-index:3"><span class="lab">Your lobby</span>'
            '<sc-for list="{{lobby}}" as="s" hint-placeholder-count="3"><span style="display:flex;align-items:center;gap:10px">'
            '<img src="{{s.face}}" alt="" style="width:36px;height:42px;object-fit:cover;object-position:50% 28%"><span style="font-size:15px">{{s.who}}</span>'
            '<sc-if value="{{s.ready}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:14px">Ready</span></sc-if>'
            '<sc-if value="{{s.choosing}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:14px;color:#747BAD">Choosing</span></sc-if></span></sc-for></section>')
    body = (rail + voyage_chip(1180, 176, 700) + "".join(ships) + tray(40, 640, 1840)
            + '<div class="abs" style="left:40px;top:868px;z-index:3">'
            + f'<a class="btn soft" href="{link("P3B")}" style="width:340px"><span class="kc">X</span>Fly a squad instead</a></div>'
            + f'<div class="abs" style="left:1240px;top:868px;display:flex;gap:20px;z-index:3"><a class="btn soft" href="{link("P2A")}" style="width:200px"><span class="kc">B</span>Back</a>'
            + '<a class="btn soft-cta" href="#" style="width:420px"><span class="kc">A</span>Set sail</a></div>')
    return window(3, body, f"Rev {REV} P3C - Formation: your lobby's three vessels in formation over the course; a press opens its tray - interactive",
                  back=link("P2A"), ground=GROUND + bg)


# ── Notes ────────────────────────────────────────────────────────────────────────────────────────────────────────────
NOTES = [
    ("What was asked", "Shombith, 2026-11-13: everything on the Voyages tab is gone; a new design set in three pages. "
     "1 the HyperSea map (a table top and other options, all zoomable) with the voyages your pilots recommend; "
     "2 the voyage opened - its story, route and rewards, animated; 3 your Ark - your lobby first, a squad only when there is no lobby, then Set sail. "
     "Voyages use difficulty 1-9, not intensity. Quests and Saga / Episodes are drawn in other rounds."),
    ("Page 1 - the HyperSea", "Every map zooms the same way: the ladder (Galaxy, Arm, Sector, Course), + and -, and the wheel. "
     "A press on a place, or on a pilot's card, picks the voyage; Open voyage goes on. "
     "A Table top (recommended): the chart on a table, the edge-on view as its front, the places as beacons whose height is the difficulty. "
     "B Orrery: the galaxy as a disc with a slow ring of bearings. C Bridge: flat and full-bleed, the cards along the foot. "
     "D Dive: three glass sheets; a zoom is a dive. The foot of every page 1 is left for Saga and Episodes (Rev 103)."),
    ("Page 2 - the voyage", "Who offers it, what job it is, its story in the pilot's words, its stops, what it pays. "
     "Difficulty 1 to 9: the pilot's recommendation is marked, any other can be picked, and the crystals it pays follow it. "
     "A Briefing (recommended): the portrait and story, the route drawing itself, the rewards arriving. "
     "B Journey: one lane across the window, the Ark sailing it, each stop telling its part. C Dossier: the map stays; Story, Route, Rewards slide open."),
    ("Page 3 - your Ark", "A Lobby (recommended): the lobby's three seats as they stand; a friend's seat shows Ready or Choosing; your own seat changes in place "
     "(a vessel, an element: one press). B No lobby: the same page with a squad picked from the list, and Invite friends. "
     "C Formation: the three vessels in formation over the course. Set sail is the one Lime on every page."),
    ("New rules to confirm", "A voyage is a job a pilot of your crew offers (the slice's 31 jobs), one card per pilot. "
     "Difficulty 1-9 replaces intensity 1-4 on voyages, and scales the crystal reward (drawn as base x (0.55 + 0.1 x difficulty)). "
     "With a lobby, the lobby is the squad; squads appear only without one."),
]


def notes_board():
    cols = "".join(f'<section style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">{h}</span>'
                   f'<p style="margin:0;font-size:18px;line-height:1.6;color:#C9CDEB">{t}</p></section>' for h, t in NOTES)
    body = ('<div style="position:absolute;inset:0;padding:80px 96px;box-sizing:border-box;display:flex;flex-direction:column;gap:40px">'
            f'<span style="font-size:40px">Rev {REV} - the Voyages tab, new, in three pages</span>'
            f'<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:48px 64px">{cols}</div></div>')
    return page(f"Rev {REV} - notes", body, "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}")


BOARDS = {f"R{REV}P1A.dc.html": p1a, f"R{REV}P1B.dc.html": p1b, f"R{REV}P1C.dc.html": p1c, f"R{REV}P1D.dc.html": p1d,
          f"R{REV}P2A.dc.html": p2a, f"R{REV}P2B.dc.html": p2b, f"R{REV}P2C.dc.html": p2c,
          f"R{REV}P3A.dc.html": p3a, f"R{REV}P3B.dc.html": p3b, f"R{REV}P3C.dc.html": p3c, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
