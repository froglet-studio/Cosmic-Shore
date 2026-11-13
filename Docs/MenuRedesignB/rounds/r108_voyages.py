#!/usr/bin/env python3
"""Rev 108 boards: Rev 100's table top again, and P3A taken further (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-14, on Rev 100: "P1A : I dont like how the edge on view looks we can barely see it, tilt the table top
more flat I guess, make the edges softer and have some animation I would say. P2A is good. I like p3A's direction. Can
you go more towards it? Make the cards look cooler and have hover animation or glowing borders and show me also what
happens when I select fly a squad instead and when I want to swap my vessel or pilot, there would be a lot so clicking 22
times shouldnt be an option as I am saying, make it interesting and easy for the user maybe a popup"

  Table    the table flatter (34 deg, was 50), its corners round and its edges feathered into the sea, the edge-on view
           standing upright under it as the table's lit front (flat to the eye, so it reads), the table floating slowly,
           a light sweeping across it, the picked beacon breathing
  Lobby    P3A further: seat cards of glass with a lit rim; hover lifts the card, lights its rim and raises the vessel;
           your own card has Vessel / Pilot / Element / Artifact as four quick buttons that open the picker on that tab
  Squad    the same page after "Fly a squad instead": your squads as a row of formations, the picked one's seats below,
           your lobby told it waits; "Back to your lobby" returns
  Picker   the picker as a popup over the page: the four tabs with the current pick under each; quick kits (all four in
           one press); the voyage's own pick first ("For this bounty hunt"); filters by what a pilot sees; a grid where one
           press takes the pick and a preview that says what it is

    python3 Docs/MenuRedesignB/rounds/r108_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "108"
_spec = importlib.util.spec_from_file_location("r100", HERE / "r100_voyages.py")
r100 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, "100"]
_spec.loader.exec_module(r100)

REV = int(_rev)
ROOT = r100.ROOT
blob = r100.blob
INK, DIM, LINE, SEL = r100.INK, r100.DIM, r100.LINE, r100.SEL

# The eighteen pilots (Rev 99's Pilots tab): callsign, name, house, what they see, close-up, tall scene.
PILOTS = [
    ["ENSIGN", "Veliane Crestward", "Wardens of the Calm", "Your domain's mass: what is yours to defend, and the gaps in it.", "d0722706843bc319df8813f42deca2a5", "7e101ab02e8bc3970229bfa38ce27dc5", "Prisms"],
    ["MANGER", "Tamako Deephook", "Order of the Last Lure", "The mass cluster the fauna are heading for.", "7ddc268d4a4cf61656bf54291784f083", "5a38c3df5c4230b71c50c0b6d0171107", "Life"],
    ["ANVIL", "Brikon Copperwright", "Tidewright Exchange", "Every Mass crystal.", "f4c3e0f26d1f48e7d121156e212e4136", "5b307944f40dd4ab3329bca57b8121a2", "Crystals"],
    ["VOLT", "Karenth Amberweaver", "Brass Lantern Company", "Every Charge crystal.", "32a9eed26dda493c7334d13cddaf435e", "6760fa59723a74b7eda70ba160d62c26", "Crystals"],
    ["ZODIAC", "Ivelia Starreach", "Saltmark Consortium", "Every Space crystal.", "30c5723ab68af43282bfca424757ff31", "706c426e007e3987d06ffcffbe76fdde", "Crystals"],
    ["SUNDIAL", "Delmira Stillwater", "House Verdance", "Every Time crystal.", "7c6ca6ecc0a83ebe77abab96aac4cc29", "e06ea08442ebbf923f09e4d604cf4a8f", "Crystals"],
    ["OPAL", "Quivera Pearlspark", "House of Seven Scales", "Every Omni crystal.", "02126cf45624881e04e68ab3443b261b", "fa1bc075bdbd0350e5c4cd992752af3e", "Crystals"],
    ["RAMPART", "Obelo Stormstone", "Quayside Mutual", "Every shielded prism.", "eeb9791e88d775390515d374cec71f6b", "db7cf92873c24f549dc9a7bbe03841d2", "Prisms"],
    ["CITADEL", "Nedira Shellward", "House Albrine", "Every super-shielded prism: what a payload is for.", "d0df7ab8ebac626cdfb038866c8db9f0", "e40ad9b86a157dd28aefbe49dd4f0d64", "Prisms"],
    ["HORNET", "Zeko Mirethorn", "Deepreed Stalkers", "Every danger prism: the mass that burns your petals.", "5bb7e9dc77ccbb917e79991a86168b17", "43d5cc7af8dfe39f8e03cffcb121dc86", "Prisms"],
    ["IVY", "Mathalo Kelpbloom", "House Verdance", "Every plant.", "d750680bcbd6bed6958c9fa98632d348", "6a4df7481e5e23e2d83d4358d6fcc9e4", "Life"],
    ["TALON", "Wyvendo Waveline", "Hollowfen Trackers", "Every predator.", "77e722ebf367c3b4093b2ae2778762e4", "3531d7cc65a34ce447f77a23a2d4e226", "Life"],
    ["DOE", "Fenlia Moonbell", "Longwake Lodge", "Every herbivore.", "b214d22d4da869609f3bcdf9bdb404e0", "3b10815ffd8b9882fa114bd21210a4a5", "Life"],
    ["WINGMAN", "Jorin Gullwing", "Gullbone Crew", "Every vessel on your side, through mass.", "e907f06a7e1221c5fbd2265e0738b5ae", "cf6bf4b56f8ddf24154e2823a86aaf35", "Vessels"],
    ["BANDIT", "Rosh Rusthook", "Black Tide", "Every opposing vessel, through mass.", "283a48123225eca8a9a42c41e606d174", "3c64ff1cf957e4b1f877d1d78aff0521", "Vessels"],
    ["NEEDLE", "Ilia Silvergate", "The Jade Accord", "Every ring that activates something: gates, Scarab switches, goals.", "17190fc5544dfc08266eab625c0aa6a2", "b340492105d57db32af6f6f7ad3f8a79", "Paths"],
    ["GALLEON", "Haradek Anchorkeel", "The Unmoored", "The Arks, and the payload aboard.", "45b8dac249d55356c4c5c193130fd1b0", "4ce66274d87e522133d5f06da5faaf1c", "Vessels"],
    ["FLUME", "Ekora Hollowbrook", "Saltjaw Brethren", "Every soliton, whoever made it: where it is and where it leads.", "78ad334460657bdc5cf529a6cb76d1cd", "cfe5c00123b194bc5845945fd6be8fcd", "Paths"],
]
PILOTS = [[cs, n, h, sees, blob(c), blob(t), grp] for cs, n, h, sees, c, t, grp in PILOTS]
SEES = ["All", "Crystals", "Prisms", "Life", "Vessels", "Paths"]
VESSELS = [["Sparrow", "Charge", "Gunship: cannons and a turret stance"], ["Dolphin", "Space", "Skimmer: rides the trail, jumps it"],
           ["Squirrel", "Time", "Racer: drifts and boosts through the trail"], ["Rhino", "Mass", "Ram: breaks shields, lays heavy walls"],
           ["Manta", "Space", "Glider: wide wings, long skims"], ["Serpent", "Charge", "Hunter: coils, strikes from range"]]
ARTIFACTS = [["No artifact", "Fly light"], ["Extra Bay", "One more find aboard"], ["Seed Pod", "Plants a bloom where it lands"],
             ["Tailwind", "A burst of speed after a skim"], ["Long Edge", "Your trail lasts longer"]]
KITS = [["Bounty kit", "Rhino", "BANDIT", "Charge", "Long Edge"], ["Skimmer", "Dolphin", "ZODIAC", "Space", "Tailwind"],
        ["Hauler", "Manta", "GALLEON", "Mass", "Extra Bay"], ["Last flown", "Sparrow", "NEEDLE", "Charge", "Extra Bay"]]
FOR_VOYAGE = {"v": ["Rhino", "Serpent"], "p": ["BANDIT", "WINGMAN", "GALLEON"]}

CSS = """
.glass{position:relative;background:linear-gradient(170deg,rgba(67,76,137,.36),rgba(0,4,31,.88) 55%);transition:transform .35s cubic-bezier(.2,.7,.2,1),box-shadow .35s ease}
.glass::before{content:'';position:absolute;inset:0;padding:1px;pointer-events:none;background:linear-gradient(155deg,rgba(185,201,250,.75),rgba(67,76,137,.15) 38%,rgba(67,76,137,.15) 62%,rgba(0,212,255,.55));-webkit-mask:linear-gradient(#000 0 0) content-box,linear-gradient(#000 0 0);-webkit-mask-composite:xor;mask:linear-gradient(#000 0 0) content-box exclude,linear-gradient(#000 0 0)}
.glass:hover{transform:translateY(-8px);box-shadow:0 0 0 1px rgba(0,212,255,.9),0 18px 60px rgba(0,212,255,.22)}
.glass .ship{transition:transform .5s cubic-bezier(.2,.7,.2,1)}
.glass:hover .ship{transform:translateY(-10px) scale(1.05)}
.glass.on{box-shadow:0 0 0 1px #00D4FF,0 0 48px rgba(0,212,255,.28)}
.chipq{transition:background .25s,color .25s,box-shadow .25s}
.chipq:hover{box-shadow:inset 0 0 0 1px #00D4FF}
.tile{transition:transform .25s cubic-bezier(.2,.7,.2,1),box-shadow .25s,opacity .25s}
.tile:hover{transform:translateY(-4px);box-shadow:0 0 0 1px rgba(0,212,255,.8),0 10px 30px rgba(0,212,255,.18)}
.float{animation:float 9s ease-in-out infinite alternate}
.drift{animation:drift 90s linear infinite}.drift2{animation:drift 150s linear infinite reverse}
@keyframes drift{from{transform:translateX(0)}to{transform:translateX(-1920px)}}
.sheen::after{content:'';position:absolute;top:0;bottom:0;width:40%;pointer-events:none;background:linear-gradient(100deg,rgba(230,233,255,0),rgba(230,233,255,.10),rgba(230,233,255,0));animation:sheen 11s ease-in-out infinite}
.breathe{animation:breathe 2.8s ease-in-out infinite}
.modal{animation:modal .45s cubic-bezier(.2,.7,.2,1) both}
.fade{animation:fade .35s ease both}
@keyframes float{from{transform:translateY(0)}to{transform:translateY(-10px)}}
@keyframes sheen{0%{transform:translateX(-120%)}55%,100%{transform:translateX(160%)}}
@keyframes breathe{0%,100%{box-shadow:0 0 0 4px rgba(0,212,255,.25)}50%{box-shadow:0 0 0 12px rgba(0,212,255,0)}}
@keyframes modal{from{opacity:0;transform:translateY(24px) scale(.97)}to{opacity:1;transform:none}}
@keyframes fade{from{opacity:0}to{opacity:1}}
"""
r100.HELMET = r100.HELMET.replace("</style>", CSS + "</style>")

# ── The sea ground (Shombith on Rev 108: "The blue background is too dull man. make it interesting with sea lines") ──

def _waves(n, y0, gap, amp, cycles, cols, op, w=1.4):
    """One SVG of n wave lines, tileable across 1920 px, as a CSS background (one element, no noise)."""
    paths = []
    for i in range(n):
        y = y0 + i * gap
        k = i * 0.9
        pts = " ".join(f"{x},{round(y + amp * math.sin(x / 1920 * 6.2832 * cycles + k) + amp * 0.35 * math.sin(x / 1920 * 6.2832 * cycles * 3 + k * 2), 1)}"
                       for x in range(0, 1921, 24))
        c = cols[i % len(cols)]
        paths.append(f"<polyline points='{pts}' fill='none' stroke='{c}' stroke-opacity='{op[i % len(op)]}' stroke-width='{w}'/>")
    svg = f"<svg xmlns='http://www.w3.org/2000/svg' width='1920' height='996'>{''.join(paths)}</svg>"
    return svg


# The two wave layers are canvas assets (Frogma takes no data: URIs); `--waves <dir>` writes the SVGs to upload.
WAVE_SVGS = {"sea_waves_a.svg": lambda: _waves(9, 120, 104, 22, 2, ("#758FFB", "#96ACFC", "#B9C9FA"), (".30", ".18", ".24")),
             "sea_waves_b.svg": lambda: _waves(6, 170, 150, 34, 1, ("#B9C9FA", "#758FFB"), (".12", ".16"), 2.4)}
WAVES_A = r100.blob("54569e7d13034451941746e7057f92b0")
WAVES_B = r100.blob("cb1328da13e08b834c8ef0150bf33b83")

# The seat cards and the seat popup carry the sea: wave lines drifting through them, no glow. Shombith: "For background
# I was talking about this card background", then "I like the background but not the glow remove that and give the
# same treatment picker tab".
r100.HELMET = r100.HELMET.replace("</style>", (
    ".glass{background:"
    "url(" + r100.blob("54569e7d13034451941746e7057f92b0") + ") 0 -40px/1100px 570px repeat-x,"
    "url(" + r100.blob("cb1328da13e08b834c8ef0150bf33b83") + ") 0 20px/1400px 726px repeat-x,"
    "linear-gradient(170deg,rgba(34,38,69,.96),rgba(0,4,31,.94) 55%,rgba(0,1,10,.97));"
    "animation:cardsea 40s linear infinite}\n"
    ".glass:not(.modal):hover{animation-duration:14s}\n"
    ".glass.modal{background-color:#00041F}\n"
    "@keyframes cardsea{from{background-position:0 -40px,0 20px,0 0}to{background-position:-1100px -40px,1400px 20px,0 0}}\n"
    "</style>"))

SEA = ('<img src="' + r100.blob("391619dde523174b4a9ead64e169b67a") + '" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover">'
       '<div class="abs" style="left:0;top:84px;width:1920px;height:996px;background:'
       'radial-gradient(ellipse 55% 45% at 28% 30%,rgba(117,143,251,.26),rgba(117,143,251,0) 70%),'
       'radial-gradient(ellipse 45% 40% at 82% 78%,rgba(150,172,252,.16),rgba(150,172,252,0) 70%),'
       'linear-gradient(180deg,rgba(34,38,69,.92) 0%,rgba(0,4,31,.94) 48%,rgba(0,1,10,.97) 100%)"></div>'
       '<div aria-hidden="true" class="abs drift" style="left:0;top:84px;width:3840px;height:996px;background:'
       + "url(" + WAVES_A + ") 0 0/1920px 996px repeat-x\"></div>"
       '<div aria-hidden="true" class="abs drift2" style="left:0;top:84px;width:3840px;height:996px;background:'
       + "url(" + WAVES_B + ") 0 0/1920px 996px repeat-x\"></div>")



def link(name):
    return f"R{REV}{name}.dc.html"


# ── The table, flatter ───────────────────────────────────────────────────────────────────────────────────────────────

PAN = r"""
  // Pan: the map sits in a 160% sheet the drag slides (at most 22% each way, so no edge shows); pins and home follow it.
  const K = 1.6, ease = 'left .8s cubic-bezier(.2,.7,.2,1),top .8s cubic-bezier(.2,.7,.2,1)', lim = 22;
  const clamp = (x) => Math.max(-lim, Math.min(lim, x));
  const panAt = (q) => { const L = 50 + (parseFloat(q.left) - 50) * K + st.px, T = 50 + (parseFloat(q.top) - 50) * K + st.py;
    q.left = L.toFixed(2) + '%'; q.top = T.toFixed(2) + '%'; return L > 2 && L < 98 && T > 2 && T < 98; };
  pins.forEach((q) => { if (!panAt(q)) q.vis = 'none'; q.tr = st.drag ? 'none' : ease; });
  panAt(home); home.tr = st.drag ? 'none' : ease;
  map.panL = (-30 + st.px).toFixed(2) + '%'; map.panT = (-30 + st.py).toFixed(2) + '%';
  map.ptr = st.drag ? 'none' : ease; map.grab = st.drag ? 'grabbing' : 'grab';
  map.down = (e) => this.setState({ drag: { x: e.clientX, y: e.clientY, px: st.px, py: st.py } });
  map.move = (e) => { const g = st.drag; if (!g) return;
    this.setState({ px: clamp(g.px + (e.clientX - g.x) / 9.2), py: clamp(g.py + (e.clientY - g.y) / 5.6 * 1.55) }); };
  map.up = () => { if (st.drag) this.setState({ drag: null }); };
"""


def zoom_row(x, y):
    """The zoom ladder laid in a row above the table (Shombith: it was clipping over the map)."""
    return (r100.zoom_controls(x, y)
            .replace("display:flex;flex-direction:column;gap:12px;z-index:3", "display:flex;align-items:center;gap:16px;z-index:3", 1)
            .replace("display:flex;flex-direction:column;gap:4px;padding:12px 0;", "display:flex;gap:4px;padding:0 6px;", 1)
            .replace("box-shadow:inset 2px 0 0 {{t.bar}}", "box-shadow:inset 0 -2px 0 {{t.bar}};align-items:center", 1))


def table_board():
    # Shombith on the soft version: "Nahhh too soft corners I liked the old 100p1A more as well and also in this map I
    # cannot move around just zoom". So: Rev 100 P1A's table (50 deg, square corners, its rim and shadow) on the new sea,
    # the side view upright under it, and the map pans by dragging (the wheel still zooms).
    up = "transform:rotateX(-50deg)"
    move = "transition:left .8s cubic-bezier(.2,.7,.2,1),top .8s cubic-bezier(.2,.7,.2,1)"
    beacons = r100.beacon(up).replace(
        "border:2px solid {{p.ring}};", "border:2px solid {{p.ring}};animation:{{p.anim}};").replace(move, "transition:{{p.tr}}")
    home = r100.home_mark().replace(move, "transition:{{home.tr}}")
    table = f"""<div class="abs float" onWheel="{{{{map.wheel}}}}" onMouseDown="{{{{map.down}}}}" onMouseMove="{{{{map.move}}}}" onMouseUp="{{{{map.up}}}}" onMouseLeave="{{{{map.up}}}}" style="left:150px;top:256px;width:1080px;height:680px;perspective:1600px;perspective-origin:50% 10%;cursor:{{{{map.grab}}}}">
<div style="position:absolute;left:90px;top:0;width:900px;height:540px;transform-style:preserve-3d;transform:rotateX(50deg);transform-origin:50% 0%">
<div class="sheen" style="position:absolute;inset:0;overflow:hidden;background:#00010A;box-shadow:0 0 0 2px rgba(116,123,173,.5),0 40px 120px rgba(0,0,0,.7)">
<div style="position:absolute;left:{{{{map.panL}}}};top:{{{{map.panT}}}};width:160%;height:160%;transition:{{{{map.ptr}}}}">{r100.layers()}</div></div>
<div style="position:absolute;inset:0;transform-style:preserve-3d">{home}{beacons}</div>
<figure style="position:absolute;left:0;top:100%;width:100%;height:70px;margin:0;transform-origin:50% 0;transform:rotateX(-50deg);overflow:hidden;background:#00010A;box-shadow:0 0 0 2px rgba(116,123,173,.5),0 40px 80px rgba(0,0,0,.6)">
<div style="position:absolute;top:0;bottom:0;left:{{{{map.panL}}}};width:160%;transition:{{{{map.ptr}}}}">
<img src="{{{{voyage.edge}}}}" alt="The course seen edge on: the table's front, sliding with the map" style="width:100%;height:100%;object-fit:cover;object-position:40% 78%"></div></figure>
</div>

</div>"""
    html = r100.window(1, table + zoom_row(40, 172) + full_link(888, 172, "Full", "Full screen") + r100.rec_list(1400, 176, 480) + r100.open_cta(1400, 852, 480),
                       f"Rev {REV} Table - Rev 100 P1A's table (50 deg, square corners) on the moving sea; drag to pan, wheel to zoom; the side view under it - interactive", ground=SEA)
    html = html.replace("ring: i === st.sel ? '#00D4FF'", "anim: i === st.sel ? 'breathe 2.8s ease-in-out infinite' : 'none', ring: i === st.sel ? '#00D4FF'")
    html = html.replace("t: 0 }; }", "t: 0, px: 0, py: 0, drag: null }; }", 1)
    anchor = "  const pips = (n) =>"
    assert anchor in html and "t: 0, px: 0" in html
    return html.replace(anchor, PAN.strip("\n") + "\n" + anchor, 1)


# ── The Ark page, further ────────────────────────────────────────────────────────────────────────────────────────────
DATA = ("const PILOTS = " + json.dumps(PILOTS) + ";\nconst SEES = " + json.dumps(SEES) + ";\nconst VESSELS = " + json.dumps(VESSELS)
        + ";\nconst ARTS = " + json.dumps(ARTIFACTS) + ";\nconst KITS = " + json.dumps(KITS) + ";\nconst FORV = " + json.dumps(FOR_VOYAGE) + ";\n")

ARK_VALS = """
  // The Ark page: lobby or squad, and the picker over it.
  const PBY = {}; PILOTS.forEach((p) => { PBY[p[0]] = p; });
  const mode = st.mode || 'lobby', pk = st.pk === undefined ? -1 : st.pk, me = st.me;
  const setMe = (patch) => this.setState({ me: Object.assign({}, me, patch) });
  const open = (k) => () => this.setState({ pk: k, see: 'All', hov: null });
  const seat = (s, i, mine) => { const p = PBY[s.cs];
    return { who: s.who, role: s.role || '', hull: HULL[s.v], v: s.v, pilot: p[1], face: p[4], el: s.e, flower: FLOWER[s.e], art: s.a,
      mine: mine, theirs: !mine, ready: s.st === 'Ready', choosing: s.st === 'Choosing', ai: s.who === 'AI wingmate',
      cls: 'glass' + (mine ? ' on' : ''), cv: open(0), cp: open(1), ce: open(2), ca: open(3) }; };
  const meSeat = Object.assign({ who: 'You', st: 'You' }, me);
  const lobbySeats = [seat(meSeat, 0, true), seat(LOBBY[1], 1, false), seat(LOBBY[2], 2, false)];
  const SQ = SQUADS.slice(0, 2);
  const sqi = st.sqi || 0;
  const sqSeats = SQ[sqi].seats.map((s, i) => seat(s.who === 'You' ? Object.assign({}, s, me, { who: 'You', role: s.st }) : Object.assign({}, s, { role: s.st }), i, s.who === 'You'));
  const squadsRow = SQ.map((q, i) => ({ name: q.name, who: q.seats.map((x) => x.who === 'AI wingmate' ? 'AI' : x.who).join('  \\u00B7  '),
    h0: HULL[q.seats[0].v], h1: HULL[q.seats[1].v], h2: HULL[q.seats[2].v], cls: 'glass' + (i === sqi ? ' on' : ''), pressed: on(i === sqi),
    pick: () => this.setState({ sqi: i }) }));
  const ark = { lobby: mode === 'lobby', squad: mode === 'squad', seats: mode === 'lobby' ? lobbySeats : sqSeats,
    toSquad: () => this.setState({ mode: 'squad', pk: -1 }), toLobby: () => this.setState({ mode: 'lobby', pk: -1 }),
    head: mode === 'lobby' ? 'Your lobby' : 'Fly a squad', sub: mode === 'lobby' ? 'Halcyon and Mirel sail with you' : 'Halcyon and Mirel wait in your lobby; AI wingmates fly the other seats' };
  // The picker: four tabs, the current pick under each; kits; the voyage's own picks first; filters; one press takes.
  const tabsP = ['Vessel', 'Pilot', 'Element', 'Artifact'].map((t, k) => ({ name: t, now: [me.v, PBY[me.cs][1], me.e, me.a][k],
    pressed: on(k === pk), col: k === pk ? '#E6E9FF' : '#747BAD', line: k === pk ? '#00D4FF' : 'rgba(0,0,0,0)', pick: () => this.setState({ pk: k, see: 'All', hov: null }) }));
  const kits = KITS.map((k) => ({ name: k[0], line: k[1] + '  \\u00B7  ' + PBY[k[2]][1].split(' ')[0] + '  \\u00B7  ' + k[3], hull: HULL[k[1]], face: PBY[k[2]][4],
    on: me.v === k[1] && me.cs === k[2] && me.e === k[3] && me.a === k[4], edge: me.v === k[1] && me.cs === k[2] && me.e === k[3] && me.a === k[4] ? 'inset 0 -2px 0 #00D4FF' : 'inset 0 0 0 1px rgba(67,76,137,.6)',
    pick: () => setMe({ v: k[1], cs: k[2], e: k[3], a: k[4] }) }));
  const see = st.see || 'All';
  const filters = (pk === 1 ? SEES : []).map((f) => ({ name: f, pressed: on(f === see), col: f === see ? '#00041F' : '#E6E9FF', bg: f === see ? '#E6E9FF' : 'rgba(0,4,31,.7)', pick: () => this.setState({ see: f }) }));
  const tile = (key, name, sub, img, onNow, take, big, tag) => ({ key: key, name: name, sub: sub, img: img, tag: tag || '', hasTag: !!tag,
    cls: 'tile pick', op: onNow ? 1 : 0.86, edge: onNow ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(67,76,137,.55)', pressed: on(onNow),
    pick: () => { take(); this.setState({ hov: key }); } });
  let grid = [];
  if (pk === 0) grid = VESSELS.map((x) => tile('v' + x[0], x[0], x[2], HULL[x[0]], me.v === x[0], () => setMe({ v: x[0] }), true, FORV.v.indexOf(x[0]) >= 0 ? 'For this hunt' : ''));
  if (pk === 1) grid = PILOTS.filter((p) => see === 'All' || p[6] === see).sort((a, b) => (FORV.p.indexOf(b[0]) >= 0) - (FORV.p.indexOf(a[0]) >= 0))
    .map((p) => tile('p' + p[0], p[1], p[2], p[4], me.cs === p[0], () => setMe({ cs: p[0] }), false, FORV.p.indexOf(p[0]) >= 0 ? 'For this hunt' : ''));
  if (pk === 2) grid = Object.keys(FLOWER).map((e) => tile('e' + e, e, '', FLOWER[e], me.e === e, () => setMe({ e: e }), true, ''));
  if (pk === 3) grid = ARTS.map((a) => tile('a' + a[0], a[0], a[1], '', me.a === a[0], () => setMe({ a: a[0] }), true, ''));
  const cur = pk === 0 ? VESSELS.find((x) => x[0] === me.v) : null, pc = PBY[me.cs];
  const prev = { vessel: pk === 0, pilot: pk === 1, other: pk >= 2,
    img: pk === 0 ? HULL[me.v] : (pk === 1 ? pc[5] : (pk === 2 ? FLOWER[me.e] : '')),
    title: pk === 0 ? me.v : (pk === 1 ? pc[1] : (pk === 2 ? me.e : me.a)),
    kicker: pk === 0 ? 'Vessel' : (pk === 1 ? pc[2] : (pk === 2 ? 'Element' : 'Artifact')),
    line: pk === 0 && cur ? cur[2] : (pk === 1 ? 'Sees: ' + pc[3] : (pk === 3 ? (ARTS.find((a) => a[0] === me.a) || ['', ''])[1] : 'Your vessel\\'s abilities grow with ' + me.e + '.')) };
  const picker = { open: pk >= 0, close: () => this.setState({ pk: -1 }), tabs: tabsP, kits: kits, filters: filters, hasFilters: pk === 1,
    grid: grid, isPilot: pk === 1, isWide: pk !== 1, count: pk === 1 ? grid.length + ' of ' + PILOTS.length + ' pilots' : '', prev: prev };
"""


def ark_logic(start):
    lg = r100.LOGIC
    a = "constructor(p) { super(p); this.state = { z: 2, sel: 0, d: -1, stop: -1, card: 0, seat: -1, sq: 0, lob: LOBBY.map((s) => Object.assign({}, s)), t: 0 }; }"
    assert a in lg
    lg = lg.replace(a, "constructor(p) { super(p); this.state = Object.assign({ z: 2, sel: 0, d: -1, stop: -1, card: 0, seat: -1, sq: 0, lob: LOBBY.map((s) => Object.assign({}, s)), t: 0, "
                    "mode: 'lobby', pk: -1, see: 'All', sqi: 0, me: { v: 'Sparrow', cs: 'NEEDLE', e: 'Charge', a: 'Extra Bay' } }, " + json.dumps(start) + "); }")
    b = "  return { map, layers,"
    assert b in lg
    lg = lg.replace(b, ARK_VALS + "  return { ark, picker, squadsRow, map, layers,")
    return DATA + lg


def seat_cards():
    btn = lambda fn, word: (f'<button class="btn sm soft chipq" type="button" onClick="{{{{s.{fn}}}}}" style="flex:1;height:40px;padding:0 8px;font-size:14px">{word}</button>')
    return ('<div class="abs" style="left:40px;top:300px;width:1840px;height:540px;display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:34px;z-index:2">'
            '<sc-for list="{{ark.seats}}" as="s" hint-placeholder-count="3"><div class="{{s.cls}}" style="height:540px">'
            '<div style="position:absolute;left:28px;right:28px;top:26px;display:flex;align-items:baseline;justify-content:space-between">'
            '<span style="font-size:22px">{{s.who}}</span>'
            '<sc-if value="{{s.ready}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:14px;color:#E6E9FF;display:flex;align-items:center;gap:8px">'
            '<span style="width:8px;height:8px;border-radius:50%;background:#E6E9FF"></span>Ready</span></sc-if>'
            '<sc-if value="{{s.choosing}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:14px;color:#747BAD">Choosing</span></sc-if>'
            '<span style="font-size:14px;color:#747BAD">{{s.role}}</span></div>'
            '<span aria-hidden="true" style="position:absolute;left:6%;right:6%;top:120px;height:210px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(150,172,252,.22),rgba(150,172,252,0))"></span>'
            '<img class="hull ship" src="{{s.hull}}" alt="{{s.v}}" style="position:absolute;left:8%;width:84%;top:96px;height:230px">'
            '<div style="position:absolute;left:28px;right:28px;top:350px;display:flex;align-items:center;gap:18px">'
            '<img src="{{s.face}}" alt="" style="width:72px;height:86px;object-fit:cover;object-position:50% 28%;border-radius:4px">'
            '<span style="display:flex;flex-direction:column;gap:8px;min-width:0;flex:1"><span style="font-size:22px">{{s.v}}</span>'
            '<span class="ell" style="font-size:15px;color:#747BAD">{{s.pilot}}</span></span>'
            '<span style="display:flex;flex-direction:column;align-items:center;gap:6px"><img src="{{s.flower}}" alt="" style="width:32px;height:32px"><span style="font-size:14px;color:#747BAD">{{s.el}}</span></span></div>'
            '<div style="position:absolute;left:28px;right:28px;bottom:26px;height:48px;display:flex;align-items:center">'
            '<sc-if value="{{s.mine}}" hint-placeholder-val="{{ true }}"><div style="display:flex;gap:8px;width:100%">'
            + btn("cv", "Vessel") + btn("cp", "Pilot") + btn("ce", "Element") + btn("ca", "Artifact") + '</div></sc-if>'
            '<sc-if value="{{s.theirs}}" hint-placeholder-val="{{ false }}"><span style="font-size:15px;color:#747BAD">Artifact  ·  {{s.art}}</span></sc-if>'
            '</div></div></sc-for></div>')


def squads_row():
    return ('<sc-if value="{{ark.squad}}" hint-placeholder-val="{{ false }}"><div class="abs fade" style="left:640px;top:176px;width:520px;height:104px;display:flex;gap:16px;z-index:3">'
            '<sc-for list="{{squadsRow}}" as="q" hint-placeholder-count="2"><button class="{{q.cls}} pick" type="button" aria-pressed="{{q.pressed}}" onClick="{{q.pick}}" style="flex:1;height:104px">'
            '<span style="position:absolute;left:18px;top:16px;display:flex;flex-direction:column;gap:8px"><span style="font-size:18px">{{q.name}}</span>'
            '<span style="font-size:14px;color:#747BAD">{{q.who}}</span></span>'
            '<span style="position:absolute;right:12px;bottom:10px;width:96px;height:44px"><img class="hull" src="{{q.h1}}" alt="" style="position:absolute;left:0;top:0;width:44px;height:16px;opacity:.8">'
            '<img class="hull" src="{{q.h2}}" alt="" style="position:absolute;right:0;top:0;width:44px;height:16px;opacity:.8">'
            '<img class="hull" src="{{q.h0}}" alt="" style="position:absolute;left:22px;bottom:0;width:52px;height:20px"></span></button></sc-for></div></sc-if>')


def picker():
    tabs = ('<div role="tablist" style="display:flex;gap:40px"><sc-for list="{{picker.tabs}}" as="t" hint-placeholder-count="4">'
            '<button class="pick" role="tab" type="button" aria-selected="{{t.pressed}}" onClick="{{t.pick}}" style="display:flex;flex-direction:column;gap:8px;padding-bottom:10px;box-shadow:inset 0 -2px 0 {{t.line}}">'
            '<span class="cap" style="font-size:15px;color:{{t.col}}">{{t.name}}</span><span style="font-size:14px;color:#747BAD">{{t.now}}</span></button></sc-for></div>')
    kits = ('<div style="display:flex;flex-direction:column;gap:12px"><span class="lab">Quick kits  ·  all four in one press</span>'
            '<div style="display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px"><sc-for list="{{picker.kits}}" as="k" hint-placeholder-count="4">'
            '<button class="tile pick" type="button" onClick="{{k.pick}}" style="position:relative;height:84px;display:flex;align-items:center;gap:14px;padding:0 14px;background:rgba(0,4,31,.7);box-shadow:{{k.edge}}">'
            '<img src="{{k.face}}" alt="" style="width:46px;height:56px;object-fit:cover;object-position:50% 28%"><span style="display:flex;flex-direction:column;gap:6px;min-width:0">'
            '<span style="font-size:15px">{{k.name}}</span><span class="ell" style="font-size:14px;color:#747BAD">{{k.line}}</span></span></button></sc-for></div></div>')
    filters = ('<sc-if value="{{picker.hasFilters}}" hint-placeholder-val="{{ true }}"><div style="display:flex;align-items:center;gap:10px">'
               '<span class="lab" style="margin-right:8px">Sees</span><sc-for list="{{picker.filters}}" as="f" hint-placeholder-count="6">'
               '<button class="chipq pick" type="button" aria-pressed="{{f.pressed}}" onClick="{{f.pick}}" style="height:36px;padding:0 16px;font-size:14px;color:{{f.col}};background:{{f.bg}};box-shadow:inset 0 0 0 1px rgba(67,76,137,.8)">{{f.name}}</button></sc-for>'
               '<span style="margin-left:auto;font-size:14px;color:#747BAD">{{picker.count}}</span></div></sc-if>')
    tag = ('<sc-if value="{{g.hasTag}}" hint-placeholder-val="{{ false }}"><span class="cap" style="position:absolute;left:8px;top:8px;font-size:14px;padding:2px 8px;'
           'background:rgba(0,1,10,.85);color:#E6E9FF;box-shadow:inset 0 -2px 0 #00D4FF">{{g.tag}}</span></sc-if>')
    grid_p = ('<sc-if value="{{picker.isPilot}}" hint-placeholder-val="{{ true }}"><div style="display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:12px">'
              '<sc-for list="{{picker.grid}}" as="g" hint-placeholder-count="18"><button class="{{g.cls}}" type="button" aria-pressed="{{g.pressed}}" onClick="{{g.pick}}" '
              'style="position:relative;height:116px;overflow:hidden;background:#00010A;box-shadow:{{g.edge}};opacity:{{g.op}}">'
              '<img src="{{g.img}}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:50% 25%">'
              '<span style="position:absolute;left:0;right:0;bottom:0;padding:24px 10px 8px;background:linear-gradient(180deg,rgba(0,1,10,0),rgba(0,1,10,.92))">'
              f'<span class="ell" style="display:block;font-size:14px">{{{{g.name}}}}</span></span>{tag}</button></sc-for></div></sc-if>')
    grid_w = ('<sc-if value="{{picker.isWide}}" hint-placeholder-val="{{ false }}"><div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px">'
              '<sc-for list="{{picker.grid}}" as="g" hint-placeholder-count="6"><button class="{{g.cls}}" type="button" aria-pressed="{{g.pressed}}" onClick="{{g.pick}}" '
              'style="position:relative;height:150px;background:rgba(0,4,31,.7);box-shadow:{{g.edge}};opacity:{{g.op}};display:flex;flex-direction:column;align-items:center;justify-content:center;gap:10px">'
              '<img class="hull" src="{{g.img}}" alt="" style="width:70%;height:76px">'
              f'<span style="font-size:18px">{{{{g.name}}}}</span>{tag}</button></sc-for></div></sc-if>')
    prev = ('<aside aria-label="Your pick" style="width:400px;flex:none;position:relative;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)">'
            '<sc-if value="{{picker.prev.pilot}}" hint-placeholder-val="{{ true }}"><img class="fade" src="{{picker.prev.img}}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:50% 20%"></sc-if>'
            '<sc-if value="{{picker.prev.vessel}}" hint-placeholder-val="{{ false }}"><img class="hull ship fade" src="{{picker.prev.img}}" alt="" style="position:absolute;left:6%;width:88%;top:70px;height:220px"></sc-if>'
            '<div style="position:absolute;inset:0;background:linear-gradient(180deg,rgba(0,1,10,0) 40%,rgba(0,1,10,.95) 75%)"></div>'
            '<div style="position:absolute;left:28px;right:28px;bottom:28px;display:flex;flex-direction:column;gap:12px">'
            '<span class="lab" style="color:#E6E9FF">{{picker.prev.kicker}}</span><span style="font-size:28px;line-height:1.2">{{picker.prev.title}}</span>'
            '<span style="font-size:15px;line-height:1.5;color:#B8C0F0">{{picker.prev.line}}</span></div></aside>')
    close = ('<button class="pick" type="button" aria-label="Close" onClick="{{picker.close}}" style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:#E6E9FF;box-shadow:inset 0 0 0 1px #434C89">'
             '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M2 2l12 12M14 2L2 14"></path></svg></button>')
    return ('<sc-if value="{{picker.open}}" hint-placeholder-val="{{ false }}"><div class="abs fade" style="left:0;top:84px;width:1920px;height:996px;z-index:8;background:rgba(0,1,10,.72);backdrop-filter:blur(6px);-webkit-backdrop-filter:blur(6px)">'
            '<div role="dialog" aria-label="Change your seat" class="glass modal" style="position:absolute;left:200px;top:70px;width:1520px;height:830px">'
            '<div style="position:absolute;left:40px;right:40px;top:30px;display:flex;align-items:flex-start;justify-content:space-between">'
            f'<div style="display:flex;flex-direction:column;gap:18px"><span style="font-size:28px">Change your seat</span>{tabs}</div>{close}</div>'
            f'<div style="position:absolute;left:40px;right:40px;top:180px;bottom:100px;display:flex;gap:32px">'
            f'<div style="flex:1;min-width:0;display:flex;flex-direction:column;gap:22px">{kits}{filters}{grid_p}{grid_w}</div>{prev}</div>'
            '<div style="position:absolute;left:40px;right:40px;bottom:30px;display:flex;align-items:center;justify-content:space-between">'
            '<span style="font-size:15px;color:#747BAD">One press takes it. Your seat behind updates as you pick.</span>'
            '<button class="btn soft-cta" type="button" onClick="{{picker.close}}" style="width:300px"><span class="kc">A</span>Done</button></div>'
            '</div></div></sc-if>')


def ark_board(title, start):
    head = ('<div class="abs" style="left:40px;top:186px;display:flex;flex-direction:column;gap:12px;z-index:3">'
            '<span class="lab" style="color:#E6E9FF">{{ark.head}}</span><span style="font-size:15px;color:#747BAD">{{ark.sub}}</span></div>')
    foot = ('<div class="abs" style="left:40px;top:878px;z-index:3">'
            '<sc-if value="{{ark.lobby}}" hint-placeholder-val="{{ true }}"><button class="btn soft chipq" type="button" onClick="{{ark.toSquad}}" style="width:360px"><span class="kc">X</span>Fly a squad instead</button></sc-if>'
            '<sc-if value="{{ark.squad}}" hint-placeholder-val="{{ false }}"><button class="btn soft chipq" type="button" onClick="{{ark.toLobby}}" style="width:360px"><span class="kc">X</span>Back to your lobby</button></sc-if></div>'
            f'<div class="abs" style="left:1240px;top:878px;display:flex;gap:20px;z-index:3"><a class="btn soft" href="{r100.link("P2A")}" style="width:200px"><span class="kc">B</span>Back</a>'
            '<a class="btn soft-cta" href="#" style="width:420px"><span class="kc">A</span>Set sail</a></div>')
    body = head + squads_row() + r100.voyage_chip(1180, 176, 700) + seat_cards() + foot + picker()
    return r100.page(title, SEA + r100.r70.nav(crystals=False) + r100.header(3, r100.link("P2A")) + body, ark_logic(start))


# ── Full screen: the map across the window, two tiers deeper (Shombith, 2026-11-14: "We cannot zoom further than the
# course there should be maybe a fullscreen mode where we select and the whole map widens and the crew goes to the sides
# like in preview full screen and we can do the full zoom till the cell view as we created the foam view") ───────────
# The two deeper tiers are frames of the Voyage Board's foam (cells.js round 3, the voyages branch's cell work): the
# Neighbourhood (20 ly, the packed cells) and the Cell (5 ly: membrane, cytoplasm, nucleus), around Tumult.
DEEP = [["Neighbourhood", "20 ly", r100.blob("1837bd8d6221b1584ff604aecf5d8ae1"), "50% 50%"],
        ["Cell", "5 ly", r100.blob("7c2d7a8d37ee3f10e337b64c6f3a40a7"), "40% 40%"]]

EXPAND = '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M1 6V1h5M10 1h5v5M15 10v5h-5M6 15H1v-5"></path></svg>'
SHRINK = '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 1v5H1M15 6h-5V1M10 15v-5h5M1 10h5v5"></path></svg>'


def full_link(x, y, name, label):
    icon = EXPAND if name == "Full" else SHRINK
    return (f'<a class="btn sm soft abs" href="{link(name)}" style="left:{x}px;top:{y}px;width:220px;gap:12px;z-index:3">'
            f'{icon}{label}</a>')


def crew_strip(x, y):
    """The crew, gone to the side: one portrait per voyage; the picked one is lit and its card opens beside the map."""
    return (f'<section aria-label="Your crew recommends" class="abs" style="left:{x}px;top:{y}px;width:100px;display:flex;flex-direction:column;gap:14px;z-index:3">'
            '<sc-for list="{{recs}}" as="c" hint-placeholder-count="4"><button class="pick" type="button" aria-pressed="{{c.pressed}}" aria-label="{{c.title}}" onClick="{{c.pick}}" '
            'style="position:relative;width:100px;height:150px;padding:0;overflow:hidden;box-shadow:{{c.edge}};opacity:{{c.op}};transition:opacity .3s">'
            '<img src="{{c.face}}" alt="{{c.pilot}}" style="width:100%;height:100%;object-fit:cover;object-position:50% 28%">'
            '<span class="num" style="position:absolute;left:8px;bottom:6px;font-size:14px;text-shadow:0 1px 3px #00010A">{{c.diff}}</span>'
            '</button></sc-for></section>')


def picked_card(x, y, w):
    """The picked voyage, over the foot of the map: who offers it, the job, difficulty, pay, and Open voyage."""
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;align-items:center;gap:28px;padding:18px 24px;box-sizing:border-box;'
            'background:rgba(0,4,31,.86);box-shadow:inset 0 0 0 1px rgba(67,76,137,.7);z-index:3">'
            '<span style="display:flex;flex-direction:column;gap:8px;flex:1;min-width:0">'
            '<span class="lab" style="color:#E6E9FF">{{pick.kind}}  ·  {{pick.pilot}}</span>'
            '<span style="font-size:20px">{{pick.title}}</span>'
            '<span style="display:flex;align-items:center;gap:14px;font-size:14px;color:#747BAD">'
            '<span class="pips"><span style="width:{{pick.pips}}"></span></span><span class="num">{{pick.diff}}</span>'
            '<img src="{{pick.flower}}" alt="" style="width:18px;height:18px;margin-left:8px"><span class="num">{{pick.reward}}</span></span></span>'
            f'<a class="btn soft-cta" href="{r100.link("P2A")}" style="width:300px"><span class="kc">A</span>Open voyage{r100.ARROW}</a></div>')


def full_board():
    move = "transition:left .8s cubic-bezier(.2,.7,.2,1),top .8s cubic-bezier(.2,.7,.2,1)"
    beacons = r100.beacon().replace(
        "border:2px solid {{p.ring}};", "border:2px solid {{p.ring}};animation:{{p.anim}};").replace(move, "transition:{{p.tr}}")
    home = r100.home_mark().replace(move, "transition:{{home.tr}}")
    sea = f"""<div class="abs" onWheel="{{{{map.wheel}}}}" onMouseDown="{{{{map.down}}}}" onMouseMove="{{{{map.move}}}}" onMouseUp="{{{{map.up}}}}" onMouseLeave="{{{{map.up}}}}" style="left:260px;top:170px;width:1490px;height:780px;overflow:hidden;background:#00010A;box-shadow:0 0 0 2px rgba(116,123,173,.5),0 30px 90px rgba(0,0,0,.6);cursor:{{{{map.grab}}}}">
<div style="position:absolute;left:{{{{map.panL}}}};top:{{{{map.panT}}}};width:130%;height:167.3%;transition:{{{{map.ptr}}}}">{r100.layers()}</div>
<div style="position:absolute;inset:0">{home}{beacons}</div>
<span class="lab" style="position:absolute;right:20px;top:18px;color:#E6E9FF;text-shadow:0 1px 3px #00010A">{{{{map.name}}}}  ·  {{{{map.scale}}}}</span>
</div>"""
    body = (sea + r100.zoom_controls(40, 196).replace("width:70px", "width:124px") + full_link(40, 560, "Table", "Exit full screen")
            + crew_strip(1780, 176) + picked_card(290, 820, 1120))
    html = r100.window(1, body, f"Rev {REV} Full - full screen: the map across the window, the crew at the side, zoom from the Galaxy down to one Cell (the foam) - interactive",
                       ground=SEA, foot=False)
    html = html.replace("ring: i === st.sel ? '#00D4FF'", "anim: i === st.sel ? 'breathe 2.8s ease-in-out infinite' : 'none', ring: i === st.sel ? '#00D4FF'")
    html = html.replace("t: 0 }; }", "t: 0, px: 0, py: 0, drag: null }; }", 1)
    deep = json.dumps(DEEP)[1:-1]
    for a, b in (('["Course", "50 ly", "", ""]]', '["Course", "50 ly", "", ""], ' + deep + ']'),
                 ("Math.min(3, z))", "Math.min(5, z))"),
                 ("Math.min(3, z + (e.deltaY", "Math.min(5, z + (e.deltaY"),
                 ("if (z === 3) return null;", "if (z >= 3) return null;"),
                 ("vis: (z === 3 && i !== st.sel)", "vis: (z > 3 || (z === 3 && i !== st.sel))"),
                 ("z: z === 3 ? 3 : 2 })", "z: z >= 3 ? z : 2 })")):
        assert a in html, a
        html = html.replace(a, b)
    # The sheet keeps the table's shape (920 x 620), 130% of the window wide, so the pins land where the table puts them.
    pan = r"""
  // Pan: a 130% x 167% sheet (the table's shape) the drag slides; pins and Tumult follow it.
  const KX = 1.3, KY = 1.673, OX = -15, OY = -33.65, ease = 'left .8s cubic-bezier(.2,.7,.2,1),top .8s cubic-bezier(.2,.7,.2,1)';
  const cx = (x) => Math.max(-15, Math.min(15, x)), cy = (y) => Math.max(-33, Math.min(33, y));
  const panAt = (q) => { const L = OX + st.px + parseFloat(q.left) * KX, T = OY + st.py + parseFloat(q.top) * KY;
    q.left = L.toFixed(2) + '%'; q.top = T.toFixed(2) + '%'; return L > 1 && L < 99 && T > 2 && T < 98; };
  pins.forEach((q) => { if (!panAt(q)) q.vis = 'none'; q.tr = st.drag ? 'none' : ease; });
  panAt(home); home.tr = st.drag ? 'none' : ease;
  if (z > 3) { home.left = '-50%'; home.top = '-50%'; }
  map.panL = (OX + st.px).toFixed(2) + '%'; map.panT = (OY + st.py).toFixed(2) + '%';
  map.ptr = st.drag ? 'none' : ease; map.grab = st.drag ? 'grabbing' : 'grab';
  map.down = (e) => this.setState({ drag: { x: e.clientX, y: e.clientY, px: st.px, py: st.py } });
  map.move = (e) => { const g = st.drag; if (!g) return;
    this.setState({ px: cx(g.px + (e.clientX - g.x) / 14.9), py: cy(g.py + (e.clientY - g.y) / 7.8) }); };
  map.up = () => { if (st.drag) this.setState({ drag: null }); };
"""
    anchor = "  const pips = (n) =>"
    html = html.replace(anchor, pan.strip("\n") + "\n" + anchor, 1)
    html = html.replace("  return { map, layers,", "  const pick = recs[st.sel];\n  return { pick, map, layers,", 1)
    return html


BOARDS = {
    f"R{REV}Table.dc.html": table_board,
    f"R{REV}Lobby.dc.html": lambda: ark_board(f"Rev {REV} Lobby - P3A further: glass seat cards that lift and light on hover; four quick buttons on your own seat - interactive", {}),
    f"R{REV}Squad.dc.html": lambda: ark_board(f"Rev {REV} Squad - after Fly a squad instead: your squads as formations, the picked one's seats below - interactive", {"mode": "squad"}),
    f"R{REV}Full.dc.html": full_board,
    f"R{REV}Picker.dc.html": lambda: ark_board(f"Rev {REV} Picker - the seat popup on Pilot: quick kits, the hunt's picks first, filters by what a pilot sees, one press - interactive", {"pk": 1}),
}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
