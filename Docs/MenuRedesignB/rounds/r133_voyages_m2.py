#!/usr/bin/env python3
"""Rev 133 boards: the Milestone 2 Voyages UI, three directions (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-24, after the Milestone 2 design (https://claude.ai/artifact/41YTUVhkuX1wPDwJQhub3g): "For this we
have to create a well engaging UI and the world and the 4 missions very interesting ... How do we start? First the app
shell and in game UI and in parallel making the four voyages". His answers to that design: the four missions, Salvage is
collecting LOOSE crystals, runs average 3-5 minutes over 1-6 cells and difficulty is how dangerous the sea is (not the
time), the seven class names, mood with refusals, the detour is a vote.

Five screens, each drawn three ways, every screen of every option its own board (his rule from Rev 116):
  Board   the Voyages tab: the HyperSea and the job board (giver, recommended classes, squad fit, pay bonus)
  Seats   your Ark's seats: vessel, its voyage class, the pilot's mood (Keen / Steady / Reluctant / Refuses) and rank
  Pause   pause in a voyage: the map with the Ark's live position, the events next door, the rival crew
  Vote    the detour vote: five seconds on the D-pad, the host breaks a tie (solo: your call)
  Awards  the end board: one role award per seat, pay with the fit bonus, the pilots' mood and rank after the run

  A  The Chart (recommended): the map is the voyage. Jobs are pins on the HyperSea, a pin slides open its giver's card;
     pause is the same chart framed on your lane; the vote is a mark in the world toward the event.
  B  The Quay: people first. You are docked, the givers stand on the quay and ask; seats are pilot cards that speak
     their mood; pause is the lane as a column beside the paused world; the pilot who saw the event asks the vote.
  C  The Ledger: quiet and application-like. The board is a list with a detail pane; seats are rows; pause is a course
     strip; the vote lives in the HUD chip; the end board is a table.

In-game boards keep Rev 120 B (the plate over the Ark, the chip top left, the feed under it) at the game's text sizes,
one layout for every device. Uses the art already on the canvas (hull renders, pilot close-ups and scenes, the Ark,
the sector map, real game frames); no new uploads.

    python3 Docs/MenuRedesignB/rounds/r133_voyages_m2.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "133"
_spec = importlib.util.spec_from_file_location("r120", HERE / "r120_voyage_hud_ark.py")
r120 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r120)
r116, r114, r113, r100 = r120.r116, r120.r114, r120.r113, r120.r100
r108, r70 = r113.r108, r100.r70

REV = int(_rev)
ROOT = r120.ROOT
W, H = 1920, 1080
INK, MUTE, SOFT, LINE, PANEL = r120.INK, r120.MUTE, r120.SOFT, r120.LINE, r120.PANEL
LIME, RED, RED2, SEL = r120.LIME, r120.RED, r120.RED2, "#00D4FF"
HULL, FLOWER, ARK, G = r114.HULL, r114.FLOWER, r114.ARK, r114.G
SECTOR = r100.TIERS[2][2]
CREW = {p[0]: [p[1], p[2], p[4], p[5]] for p in r108.PILOTS}   # callsign -> [name, house, close-up, tall scene]
plate, chip, toast, kc, stack, flower = r120.plate, r120.chip, r120.toast, r120.kc, r120.stack, r113.flower
i_gem, i_ark, i_clock, i_flag, i_warn = r116.i_gem, r116.i_ark, r116.i_clock, r116.i_flag, r116.i_warn
world, VESSEL, ark, gem, fauna = r116.world, r116.VESSEL, r116.ark, r116.gem, r116.fauna
LOGIC = r113.LOGIC

r100.HELMET = r100.HELMET.replace("</style>", (
    ".slide-in{animation:slidein .5s cubic-bezier(.2,.7,.2,1) both}\n"
    "@keyframes slidein{from{opacity:0;transform:translateX(24px)}to{opacity:1;transform:none}}\n"
    ".vote-ring{transform:rotate(-90deg)}\n"
    "</style>"))

# ── the data every option shows (the same content three ways) ───────────────────────────────────────────────────────
CLASS = {  # the Milestone 2 design's seven voyage classes and their glyphs (labels only; a kit never changes)
    "Guardian": "M15 3 L27 8 L25 20 L15 28 L5 20 L3 8 Z",
    "Protector": "M15 3 A12 12 0 1 1 14.9 3 Z M15 9 A6 6 0 1 0 15.1 9 Z",
    "Destroyer": "M15 2 L19 11 L28 15 L19 19 L15 28 L11 19 L2 15 L11 11 Z",
    "Cultivator": "M15 28 L15 12 M15 16 C9 16 6 11 6 6 C11 6 15 9 15 14 M15 14 C15 9 19 5 25 5 C25 11 21 15 15 15",
    "Hunter": "M15 3 L15 27 M3 15 L27 15 M15 8 A7 7 0 1 1 14.9 8 Z",
    "Shepherd": "M6 22 C6 10 24 10 24 22 M10 22 L10 26 M20 22 L20 26 M15 6 L15 12",
    "Pathfinder": "M3 22 C9 22 9 8 15 8 C21 8 21 22 27 22 M23 18 L27 22 L23 26",
}
VCLASS = {"Sparrow": "Destroyer", "Dolphin": "Destroyer", "Rhino": "Destroyer", "Squirrel": "Cultivator",
          "Manta": "Hunter", "Serpent": "Guardian"}
DOCK, ZONE = "Gullbone Landing", "The Crossing"
# The Voyage Codex (https://claude.ai/artifact/S4aH7rcMZhd6xrSen9oZSD): each giver's name, callsign (their portrait), squad
# and lore. key: title, kind, giver callsign, place, cells, danger 1-4, recommended classes, base pay, the giver's ask, pin (x, y), giver
JOBS = [
    ("salvage", "The Glassmere Shatter", "Salvage", "GALLEON", "Glassmere", 3, 2, ["Cultivator", "Pathfinder", "Protector"], 30,
     "When an Ark goes down, her hold bursts and her crystal drifts loose. Take every loose crystal you can reach and bring it to the Ark "
     "before something with teeth takes it from you.", (1060, 430), "Cass Morrow", "Pirate"),
    ("escort", "The Tollmoor Crossing", "Escort the Ark", "WINGMAN", "Tollmoor", 4, 3, ["Protector", "Guardian", "Destroyer"], 40,
     "Settlers, wounded crews, a whole port moving house. Nothing aboard is worth anything if she arrives in pieces.", (900, 870), "Captain Theo Lark", "First responder"),
    ("bounty", "Black Tide Rising", "Bounty", "BANDIT", "Magpie Hollow", 3, 3, ["Hunter", "Pathfinder", "Destroyer"], 45,
     "They strip wrecks that are not theirs and leave the crews inside. Serve the warrant at sea.", (1180, 680), "Juno Rask", "Bounty hunter"),
    ("core", "Ironmouth Holds", "Hold the Core", "RAMPART", "Ironmouth", 2, 2, ["Guardian", "Cultivator", "Protector"], 35,
     "Ironmouth's core is wavering. Anchor the Ark under the nucleus and do not let it turn.", (700, 330), "Marshal Edda Kroll", "Aristocrat"),
]
JOB = {j[0]: j for j in JOBS}
DANGER = ["", "Calm", "Rough", "Hostile", "Deadly"]
# The squad (Shombith's default, Sparrow / Squirrel / Dolphin): seat, who, vessel, element, pilot callsign, mood, verb, rank
SEATS = [("Leader", "You", "Sparrow", "Charge", "TALON", "Keen", "Stow", 3),
         ("Seat 2", "Rook", "Squirrel", "Mass", "VOLT", "Steady", "Stow", 2),
         ("Seat 3", "AI wingmate", "Dolphin", "Space", "OPAL", "Reluctant", "Stow", 1)]
MOOD_WHY = {"Keen": "Raiders love salvage", "Steady": "Pay is pay", "Reluctant": "Not their kind of work",
            "Refuses": "Against their ethos"}
MOOD_SAY = {"Keen": "Loose crystals? Say no more.", "Steady": "Thirty crystals is thirty crystals.",
            "Reluctant": "Scavenging. If we must.", "Refuses": "I will not pick over the dead."}
RANK = ["", "Green", "Seasoned", "Veteran"]
FIT = sum(1 for s in SEATS if VCLASS[s[2]] in JOB["salvage"][7])          # 1 of 3 (Squirrel)
BONUS = {0: 0, 1: 10, 2: 20, 3: 35}[FIT]
PAY = 30 + 30 * BONUS // 100                                                 # 36
RIVAL = ("The Rusted Grin", "Pirate", "Greedy", ["Dolphin", "Serpent", "Rhino"])
LANE = [DOCK, "Ember Shelf", "Ashwater", "Glassmere"]          # dock, three cells, dock
NOW = 1                                                                       # in cell 2 of 3 (Ashwater)
AWARDS = [("Top Salvager", "TALON", "Sparrow", "You", "19 crystals stowed", "Keen", "Stow Veteran"),
          ("Wall of the Voyage", "VOLT", "Squirrel", "Rook", "41% of the Ark's damage stopped", "Steady  to  Keen", "Stow Seasoned"),
          ("Rival Breaker", "OPAL", "Dolphin", "AI wingmate", "Two Rusted Grin hulls broken", "Reluctant  to  Steady", "Stow Green, 2 of 3")]


# ── small pieces ────────────────────────────────────────────────────────────────────────────────────────────────────
def glyph(cls, s=22, col=INK, w=1.8):
    return (f'<svg width="{s}" height="{s}" viewBox="0 0 30 30" aria-hidden="true" style="flex:none"><path d="{CLASS[cls]}" fill="none" '
            f'stroke="{col}" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round"></path></svg>')


def cls_tag(cls, size=15, col=INK, s=20):
    return f'<span class="row" style="gap:8px;font-size:{size}px;color:{col};white-space:nowrap">{glyph(cls, s, col)}{cls}</span>'


def mood_mark(mood, s=18):
    """Four steps, read left to right: three bars lit for Keen, two Steady, one Reluctant, a struck circle for Refuses."""
    if mood == "Refuses":
        return (f'<svg width="{s}" height="{s}" viewBox="0 0 20 20" aria-hidden="true" style="flex:none"><circle cx="10" cy="10" r="8" fill="none" '
                f'stroke="{RED2}" stroke-width="2"></circle><path d="M4.5 15.5L15.5 4.5" stroke="{RED2}" stroke-width="2"></path></svg>')
    n = {"Keen": 3, "Steady": 2, "Reluctant": 1}[mood]
    bars = "".join(f'<rect x="{2 + i * 6}" y="{14 - i * 4}" width="4" height="{4 + i * 4}" fill="{INK if i < n else "rgba(67,76,137,.8)"}"></rect>'
                   for i in range(3))
    return f'<svg width="{s}" height="{s}" viewBox="0 0 20 20" aria-hidden="true" style="flex:none">{bars}</svg>'


def mood_tag(mood, size=15):
    col = RED2 if mood == "Refuses" else INK
    return f'<span class="row" style="gap:8px;font-size:{size}px;color:{col};white-space:nowrap">{mood_mark(mood)}{mood}</span>'


def rank_mark(n, s=14):
    """Rank in one work verb: one to three chevrons."""
    out = "".join(f'<path d="M2 {4 + i * 5}l5 -3 5 3" fill="none" stroke="{INK if i < n else "rgba(67,76,137,.8)"}" stroke-width="1.8"></path>'
                  for i in range(3))
    return f'<svg width="{s}" height="{s + 4}" viewBox="0 0 14 18" aria-hidden="true" style="flex:none;transform:scaleY(-1)">{out}</svg>'


def rank_tag(verb, n, size=15):
    return f'<span class="row" style="gap:8px;font-size:{size}px;color:{INK};white-space:nowrap">{rank_mark(n)}{verb} {RANK[n]}</span>'


def danger(n, w=86):
    segs = "".join(f'<span style="flex:1;height:8px;background:{INK if i < n else "rgba(67,76,137,.6)"}"></span>' for i in range(4))
    return (f'<span class="row" style="gap:10px;font-size:14px;color:{SOFT}"><span class="row" style="gap:3px;width:{w}px">{segs}</span>'
            f'{DANGER[n]} sea</span>')


def fit_meter(fit=FIT, w=180, label=True):
    segs = "".join(f'<span style="flex:1;height:10px;background:{INK if i < fit else "rgba(67,76,137,.6)"}"></span>' for i in range(3))
    lab = (f'<span style="font-size:15px;color:{INK};white-space:nowrap">Squad fit {fit} of 3</span>'
           f'<span style="font-size:15px;color:{SOFT};white-space:nowrap">Pay +{BONUS}%</span>') if label else ""
    return f'<span class="row" style="gap:14px"><span class="row" style="gap:4px;width:{w}px">{segs}</span>{lab}</span>'


def face(cs, w=56, h=66, op=1.0):
    return (f'<img src="{CREW[cs][2]}" alt="" style="width:{w}px;height:{h}px;object-fit:cover;object-position:50% 28%;'
            f'border-radius:4px;flex:none;opacity:{op}">')


def gems(n, s=18):
    return f'<span class="row" style="gap:8px">{i_gem(s, INK)}<span class="big" style="font-size:{s + 4}px">{n}</span></span>'


def window(step, body):
    return r113.menu(step, body)


def page(title, body):
    return r100.page(title, body, LOGIC)


def sea_map(x, y, w, h, pos="50% 45%", dim=.25, inner=""):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;overflow:hidden;background:#00010A">'
            f'<img src="{SECTOR}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{pos}">'
            f'<div class="abs" style="inset:0;background:rgba(0,1,10,{dim})"></div>{inner}</div>')


def cell(x, y, r, state="ahead", label=""):
    """A cell of the lane as foam: a ring. behind = filled faint, now = bright ring, ahead = dim ring."""
    st = {"behind": f"background:rgba(230,233,255,.10);box-shadow:inset 0 0 0 1px rgba(230,233,255,.35)",
          "now": f"background:rgba(230,233,255,.08);box-shadow:inset 0 0 0 2px {INK},0 0 24px rgba(230,233,255,.25)",
          "ahead": "background:rgba(0,1,10,.35);box-shadow:inset 0 0 0 1px rgba(184,192,240,.55)",
          "event": "background:rgba(255,255,255,.05);box-shadow:inset 0 0 0 1px rgba(230,233,255,.5)"}[state]
    lab = (f'<span class="abs" style="left:50%;top:{2 * r + 8}px;transform:translateX(-50%);font-size:14px;white-space:nowrap;'
           f'color:{INK if state == "now" else SOFT}">{label}</span>') if label else ""
    return f'<div class="abs" style="left:{x - r}px;top:{y - r}px;width:{2 * r}px;height:{2 * r}px;border-radius:50%;{st}">{lab}</div>'


def place(x, y, name, docked=False, size=15):
    mark = (f'<span class="abs" style="left:{x - 9}px;top:{y - 9}px;width:18px;height:18px;transform:rotate(45deg);'
            f'background:{INK if docked else "#00041F"};box-shadow:inset 0 0 0 2px {INK}"></span>')
    return mark + (f'<span class="abs" style="left:{x + 18}px;top:{y - 11}px;font-size:{size}px;white-space:nowrap;'
                   f'text-shadow:0 0 6px #00010A">{name}</span>')


def zone(x, y, name):
    return (f'<span class="abs cap" style="left:{x}px;top:{y}px;font-size:16px;letter-spacing:.3em;color:rgba(184,192,240,.55);'
            f'white-space:nowrap">{name}</span>')


def event_mark(x, y, kind, words, t, s=1.0):
    """An event next door on a map: a small lit diamond, its name and its timer. Rivals get three hull ticks."""
    ic = {"wreck": i_flag(18, INK), "bloom": i_gem(16, INK), "rival": i_warn(18, RED2)}[kind]
    col = RED2 if kind == "rival" else INK
    return (f'<div class="abs row" style="left:{x}px;top:{y}px;gap:10px;transform:scale({s});transform-origin:0 50%">'
            f'<span style="width:30px;height:30px;display:flex;align-items:center;justify-content:center;background:rgba(0,4,31,.85);'
            f'box-shadow:inset 0 0 0 1px {col}">{ic}</span>'
            f'<span style="display:flex;flex-direction:column;gap:2px;text-shadow:0 0 6px #00010A"><span style="font-size:15px;color:{col};white-space:nowrap">{words}</span>'
            f'<span class="num" style="font-size:13px;color:{SOFT}">{t}</span></span></div>')


def lane_svg(pts, done_to=None, w=1920, h=1080, x=0, y=0):
    d = "M" + " L".join(f"{a},{b}" for a, b in pts)
    done = ""
    if done_to:
        dd = "M" + " L".join(f"{a},{b}" for a, b in done_to)
        done = f'<path d="{dd}" fill="none" stroke="{INK}" stroke-width="3"></path>'
    return (f'<svg class="abs" style="left:{x}px;top:{y}px" width="{w}" height="{h}" aria-hidden="true">'
            f'<path d="{d}" fill="none" stroke="rgba(230,233,255,.55)" stroke-width="2" stroke-dasharray="8 8"></path>{done}</svg>')


def ark_dot(x, y, s=34):
    return (f'<span class="abs" style="left:{x - s / 2:.0f}px;top:{y - s / 2:.0f}px;width:{s}px;height:{s}px;border-radius:50%;'
            f'background:rgba(0,4,31,.9);box-shadow:inset 0 0 0 2px {INK},0 0 18px rgba(230,233,255,.45);display:flex;align-items:center;'
            f'justify-content:center">{i_ark(s * .7, INK)}</span>')


def buttons_pause(w=360):
    return (f'<div style="display:flex;flex-direction:column;gap:12px;width:{w}px">'
            f'<button class="btn def" type="button" style="justify-content:flex-start;width:100%">{kc("B")}Resume</button>'
            f'<button class="btn def" type="button" style="justify-content:flex-start;width:100%">Settings</button>'
            f'<button class="btn def" type="button" style="justify-content:flex-start;width:100%;color:{SOFT}">Leave voyage</button></div>')


def party_note(words="The party keeps sailing while you look"):
    return f'<span style="font-size:15px;color:{SOFT}">{words}</span>'


def salvage_hud(count=14, line="+5 to stow"):
    return world("raid") + plate(*r120.RAID_ARK, count, 82, line=line, goal="/ 30") + chip(count, 5, 82, clock="2:10").replace(f"/{r120.GOAL}", "/30")


def notes_page(title, rows):
    html = page(f"Rev {REV} {title}",
                f'<span class="abs hd" style="left:60px;top:50px;font-size:36px">{title}</span>'
                + stack(60, 130, "".join(
                    f'<div style="display:flex;gap:30px;width:1800px"><span class="hd" style="font-size:20px;width:380px;flex:none;color:{INK}">{a}</span>'
                    f'<span style="font-size:19px;color:{SOFT};line-height:1.45">{b}</span></div>' for a, b in rows), 1800, 22))
    return html


# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# A  The Chart: the map is the voyage
# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
A_PLACES = [(420, 640, DOCK, True), (1060, 430, "Glassmere", False), (900, 870, "Tollmoor", False),
            (1180, 680, "Magpie Hollow", False), (700, 330, "Ironmouth", False)]


def a_pin(key, on=False):
    j = JOB[key]
    x, y = j[10]
    edge = f"inset 0 0 0 2px {SEL}" if on else "inset 0 0 0 1px rgba(184,192,240,.7)"
    return (f'<button class="pick abs row" type="button" aria-pressed="{str(on).lower()}" style="left:{x - 30}px;top:{y - 96}px;gap:10px;'
            f'padding:6px 14px 6px 6px;background:rgba(0,4,31,.9);box-shadow:{edge}">{face(j[3], 38, 46)}'
            f'<span style="display:flex;flex-direction:column;gap:3px"><span class="cap" style="font-size:13px;color:{SOFT}">{j[2]}</span>'
            f'<span class="row" style="gap:6px">{"".join(glyph(c, 16, INK) for c in j[7])}</span></span></button>'
            f'<span class="abs" style="left:{x - 1}px;top:{y - 40}px;width:2px;height:30px;background:{SEL if on else "rgba(184,192,240,.7)"}"></span>')


def a_card(key, x=1360, y=184, w=520):
    j = JOB[key]
    name, house = j[11], j[12]
    seats = "".join(
        f'<div class="row" style="gap:12px;height:46px;padding:0 12px;background:rgba(0,4,31,.5)">'
        f'<img class="hull" src="{HULL[v]}" alt="{v}" style="width:64px;height:34px;object-fit:contain">'
        f'<span style="font-size:15px;flex:1">{v}</span>{cls_tag(VCLASS[v], 14, INK if VCLASS[v] in j[7] else MUTE, 18)}</div>'
        for _, _, v, *_ in SEATS)
    return (f'<div class="glass slide-in" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{1060 - y}px;box-sizing:border-box;padding:26px 28px;'
            f'display:flex;flex-direction:column;gap:18px;box-shadow:inset 1px 0 0 rgba(67,76,137,.8)">'
            f'<div class="row" style="gap:16px">{face(j[3], 72, 86)}<span style="display:flex;flex-direction:column;gap:6px">'
            f'<span class="lab">{j[2]}  ·  {house}</span><span style="font-size:22px;line-height:1.3">{j[1]}</span>'
            f'<span style="font-size:14px;color:{SOFT}">{name}</span></span></div>'
            f'<span style="font-size:16px;line-height:1.5;color:{SOFT}">"{j[9]}"</span>'
            f'<div class="row" style="gap:22px;font-size:14px;color:{SOFT}">{danger(j[6])}<span>{j[5]} cells  ·  3-5 min</span></div>'
            f'<div style="display:flex;flex-direction:column;gap:10px"><span class="lab">{name.split()[0]} wants</span>'
            f'<div class="row" style="gap:20px">{"".join(cls_tag(c, 15) for c in j[7])}</div></div>'
            f'<div style="display:flex;flex-direction:column;gap:8px"><span class="lab">Your squad</span>{seats}</div>'
            f'{fit_meter()}'
            f'<div class="row" style="margin-top:auto;justify-content:space-between">{gems(PAY, 20)}'
            f'<button class="btn soft-cta" type="button" style="width:280px">{kc("A")}Take the job</button></div></div>')


def a_board():
    inner = (zone(200, 520, ZONE) + zone(820, 180, "Celadon Reach") + zone(1000, 960, "Carmine Drift")
             + lane_svg([(420, 640), (620, 560), (840, 490), (1060, 430)], w=1920, h=1080)
             + "".join(place(x, y, n, d) for x, y, n, d in A_PLACES)
             + "".join(a_pin(k, k == "salvage") for k in ("salvage", "escort", "bounty", "core"))
             + event_mark(560, 740, "rival", "The Rusted Grin", "Heading for Glassmere")
             + event_mark(880, 560, "wreck", "Wreck surfacing", "Next door"))
    left = (f'<div class="abs" style="left:40px;top:184px;display:flex;flex-direction:column;gap:8px">'
            f'<span class="lab">Docked at</span><span style="font-size:24px">{DOCK}</span>'
            f'<span style="font-size:14px;color:{SOFT}">{ZONE}  ·  four givers have work</span></div>')
    return page(f"Rev {REV} A Board - The Chart: jobs as giver pins on the HyperSea; the pin slides open its card",
                window(1, sea_map(0, 84, 1920, 996, "50% 45%", .2, inner) + left + a_card("salvage")))


def a_seat(i, x, picker=False):
    role, who, v, el, cs, mood, verb, n = SEATS[i]
    fits = VCLASS[v] in JOB["salvage"][7]
    on = i == 0
    return (f'<div class="abs" style="left:{x}px;top:270px;width:520px;height:660px">'
            f'<span aria-hidden="true" class="abs" style="left:40px;right:40px;top:290px;height:60px;border-radius:50%;'
            f'background:radial-gradient(ellipse,rgba(150,172,252,.22),rgba(150,172,252,0) 70%)"></span>'
            f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:20px;width:480px;top:60px;height:260px;object-fit:contain">'
            f'<div class="abs" style="left:0;right:0;top:0;display:flex;justify-content:space-between"><span class="cap2">{role}  ·  {who}</span>'
            f'{cls_tag(VCLASS[v], 15, INK if fits else MUTE)}</div>'
            f'<div class="abs" style="left:0;right:0;top:380px;height:1px;background:{SEL if on else "rgba(67,76,137,.6)"}"></div>'
            f'<div class="abs row" style="left:0;right:0;top:400px;gap:16px">{face(cs, 60, 72)}<span style="display:flex;flex-direction:column;gap:6px;flex:1">'
            f'<span style="font-size:24px">{v}</span><span style="font-size:14px;color:{MUTE}">{CREW[cs][0]}</span></span>'
            f'<span style="display:flex;flex-direction:column;align-items:center;gap:4px">{flower(el, 34)}<span style="font-size:13px;color:{MUTE}">{el}</span></span></div>'
            f'<div class="abs row" style="left:0;right:0;top:500px;gap:28px">{mood_tag(mood)}{rank_tag(verb, n)}</div>'
            f'<span class="abs" style="left:0;top:534px;font-size:14px;color:{SOFT}">{MOOD_WHY[mood]}</span>'
            f'<div class="abs" style="left:0;right:0;top:580px">'
            + (f'<button class="btn sm soft" type="button" style="width:100%">{kc("X")}Change your seat</button>' if on else
               f'<span style="font-size:14px;color:{MUTE}">Rook picks their own seat.</span>' if who == "Rook" else
               '<span class="row" style="gap:10px"><button class="btn sm soft chipq" type="button" style="flex:1">Invite a friend</button>'
               '<button class="btn sm soft chipq" type="button" style="flex:1">Change wingmate</button></span>')
            + '</div></div>')


def a_seats():
    j = JOB["salvage"]
    top = (f'<div class="abs row" style="left:40px;right:40px;top:180px;justify-content:space-between">'
           f'<span class="row" style="gap:16px">{face(j[3], 44, 52)}<span style="display:flex;flex-direction:column;gap:6px">'
           f'<span style="font-size:18px">{j[1]}</span><span class="row" style="gap:16px;font-size:14px;color:{SOFT}">{danger(j[6])}'
           f'<span>{j[5]} cells</span></span></span></span>'
           f'<span class="row" style="gap:20px"><span class="lab">Wanted</span>{"".join(cls_tag(c, 15) for c in j[7])}</span></div>')
    floor = ('<div class="abs" style="left:0;right:0;top:620px;height:300px;background:linear-gradient(180deg,rgba(117,143,251,0),rgba(117,143,251,.06));'
             'box-shadow:inset 0 1px 0 rgba(67,76,137,.4)"></div>')
    bar = (f'<div class="abs row" style="left:40px;right:40px;top:990px;justify-content:space-between">'
           f'{fit_meter()}<span class="row" style="gap:24px"><span style="font-size:16px;color:#B8C0F0">Pay {PAY}</span>'
           f'<button class="btn soft-cta" type="button" style="width:300px">{kc("A")}Set sail</button></span></div>')
    return page(f"Rev {REV} A Seats - The Chart: Rev 115 A's line-up, each seat with its class, its pilot's mood and rank",
                window(3, top + floor + "".join(a_seat(i, 120 + i * 580) for i in range(3)) + bar))


def a_pause_map(x0=0, y0=0):
    pts = [(300, 760), (720, 600), (1120, 470), (1560, 330)]
    cells = (cell(520, 690, 120, "behind", "Ember Shelf") + cell(920, 535, 130, "now", "Ashwater") + cell(1340, 400, 120, "ahead", "Glassmere")
             + cell(1000, 820, 110, "event") + cell(760, 330, 100, "event"))
    return (lane_svg(pts, [(300, 760), (720, 600), (870, 552)])
            + cells + place(300, 760, DOCK, True) + place(1560, 330, "Glassmere dock", False)
            + ark_dot(870, 552, 40)
            + f'<span class="abs" style="left:892px;top:578px;font-size:14px;color:{INK};white-space:nowrap">You  ·  0:52 to the next cell</span>'
            + event_mark(960, 800, "wreck", "Wreck surfacing", "0:42 left  ·  Stow")
            + event_mark(700, 300, "bloom", "Bloom", "0:31 left  ·  Gather")
            + event_mark(1240, 560, "rival", RIVAL[0], "Racing you to Glassmere")
            + '<svg class="abs" style="left:0;top:0" width="1920" height="1080" aria-hidden="true">'
            + f'<path d="M1240 575 L1180 545" stroke="{RED2}" stroke-width="2" stroke-dasharray="4 6"></path></svg>')


def a_pause():
    side = stack(60, 300, (f'<span class="hd" style="font-size:20px;color:{SOFT}">Paused</span>'
                           f'<span style="font-size:24px">The Glassmere Shatter</span>'
                           f'<span class="row" style="gap:16px">{gems(14)}<span style="font-size:18px;color:{SOFT}">/ 30 aboard</span></span>'
                           f'<span style="font-size:15px;color:{SOFT}">Cell 2 of 3  ·  {DANGER[2]} sea</span>'
                           f'<div style="height:16px"></div>{buttons_pause()}<div style="height:8px"></div>{party_note()}'), 380, 14)
    legend = (f'<div class="abs row" style="right:60px;bottom:50px;gap:28px;font-size:14px;color:{SOFT}">'
              f'<span class="row" style="gap:8px">{i_ark(20, INK)}The Ark, live</span><span class="row" style="gap:8px">{i_flag(16, INK)}Event next door</span>'
              f'<span class="row" style="gap:8px">{i_warn(16, RED2)}Rival crew</span></div>')
    shade = ('<div class="abs" style="left:0;top:0;width:620px;height:1080px;background:linear-gradient(90deg,rgba(0,1,10,.92),rgba(0,1,10,.8) 70%,rgba(0,1,10,0))"></div>')
    return page(f"Rev {REV} A Pause - The Chart: pause is the chart framed on your lane, the Ark live in its cell",
                sea_map(0, 0, 1920, 1080, "62% 50%", .45, a_pause_map()) + shade + side + legend)


def a_vote():
    light = ('<span class="abs" style="left:1500px;top:180px;width:520px;height:620px;border-radius:50%;'
             'background:radial-gradient(ellipse,rgba(230,233,255,.22),rgba(230,233,255,0) 65%)"></span>'
             '<span class="abs" style="left:1700px;top:330px;width:120px;height:240px;transform:rotate(18deg);'
             'background:linear-gradient(180deg,rgba(230,233,255,.0),rgba(230,233,255,.35),rgba(230,233,255,0));filter:blur(6px)"></span>')
    seat_dot = lambda who, pick: (f'<span class="row" style="gap:8px;font-size:15px;color:{INK if pick else MUTE}">'
                                  f'<span style="width:10px;height:10px;border-radius:50%;background:{INK if pick else "rgba(67,76,137,.9)"}"></span>{who}</span>')
    ring = (f'<svg class="vote-ring" width="64" height="64" viewBox="0 0 64 64" aria-hidden="true"><circle cx="32" cy="32" r="28" fill="none" '
            f'stroke="rgba(67,76,137,.6)" stroke-width="4"></circle><circle cx="32" cy="32" r="28" fill="none" stroke="{INK}" stroke-width="4" '
            f'stroke-dasharray="176" stroke-dashoffset="70"></circle></svg>')
    vote = (f'<div class="abs" style="left:610px;top:760px;width:700px;display:flex;flex-direction:column;align-items:center;gap:14px">'
            f'<span class="hd" style="font-size:20px">Wreck surfacing to starboard</span>'
            f'<span style="font-size:16px;color:{SOFT}">Detour one cell: strip it before it sinks, 0:42</span>'
            f'<div class="row" style="gap:22px"><button class="btn def" type="button" style="width:240px;box-shadow:inset 0 0 0 2px {INK}">{kc("◀")}Detour  2</button>'
            f'<span style="position:relative;width:64px;height:64px">{ring}<span class="abs big" style="inset:0;display:flex;align-items:center;justify-content:center;font-size:22px">3</span></span>'
            f'<button class="btn def" type="button" style="width:240px">Press on  1{kc("▶")}</button></div>'
            f'<div class="row" style="gap:26px">{seat_dot("You  ·  Detour", True)}{seat_dot("Rook  ·  Detour", True)}{seat_dot("AI  ·  Press on", False)}</div>'
            f'<span style="font-size:14px;color:{MUTE}">A tie goes to the host. Solo, it is your call.</span></div>')
    arrow = ('<svg class="abs" style="left:1790px;top:420px" width="70" height="70" viewBox="0 0 80 80" aria-hidden="true">'
             f'<path d="M10 28h34V12l30 28-30 28V52H10z" fill="none" stroke="{INK}" stroke-width="3"></path></svg>'
             f'<span class="abs" style="left:1700px;top:500px;font-size:14px;color:{INK};width:180px;text-align:right">Wreck  ·  1 cell</span>')
    return page(f"Rev {REV} A Vote - The Chart: the event lights the membrane; the vote sits low and centre over the world",
                salvage_hud() + light + arrow + vote + VESSEL.replace("bottom:40px", "bottom:40px;opacity:.6"))


def a_awards():
    route = (f'<div class="abs" style="left:240px;top:190px;width:1440px;height:110px">'
             + lane_svg([(20, 60), (480, 40), (700, 90), (960, 40), (1420, 60)], [(20, 60), (480, 40), (700, 90), (960, 40), (1420, 60)], 1440, 110)
             + "".join(f'<span class="abs" style="left:{x - 7}px;top:{y - 7}px;width:14px;height:14px;border-radius:50%;background:{INK}"></span>'
                       for x, y in ((20, 60), (480, 40), (960, 40), (1420, 60)))
             + f'<span class="abs" style="left:640px;top:96px;font-size:14px;color:{SOFT};white-space:nowrap">Detour: the wreck</span>'
             + f'<span class="abs" style="left:0;top:80px;font-size:14px;color:{SOFT}">{DOCK}</span>'
             + f'<span class="abs" style="left:1340px;top:80px;font-size:14px;color:{SOFT}">Glassmere</span></div>')
    head = stack(0, 70, (f'<span class="cap2">The Glassmere Shatter  ·  3 cells and one detour  ·  4:12</span>'
                         f'<span class="hd" style="font-size:44px">Voyage complete</span>'), W, 8, "center")
    cards = []
    for i, (award, cs, v, who, line, mood, rank) in enumerate(AWARDS):
        x = 160 + i * 540
        cards.append(f'<div class="glass" style="position:absolute;left:{x}px;top:340px;width:500px;height:400px;box-sizing:border-box;padding:24px;'
                     f'display:flex;flex-direction:column;gap:14px;box-shadow:inset 0 2px 0 {INK if i == 0 else LINE}">'
                     f'<span class="hd" style="font-size:22px">{award}</span><span style="font-size:16px;color:{SOFT}">{line}</span>'
                     f'<img class="hull" src="{HULL[v]}" alt="{v}" style="width:100%;height:120px;object-fit:contain">'
                     f'<div class="row" style="gap:14px">{face(cs, 48, 58)}<span style="display:flex;flex-direction:column;gap:4px">'
                     f'<span style="font-size:17px">{who}  ·  {v}</span><span style="font-size:14px;color:{MUTE}">{CREW[cs][0]}</span></span></div>'
                     f'<div class="row" style="gap:20px;margin-top:auto;font-size:14px;color:{SOFT}">{mood_mark(mood.split()[-1])}{mood}'
                     f'{rank_mark(3 if "Veteran" in rank else 2 if "Seasoned" in rank else 1)}{rank}</div></div>')
    pay = (f'<div class="abs row" style="left:0;right:0;top:780px;justify-content:center;gap:40px;font-size:18px">'
           f'<span class="row" style="gap:10px">{gems(30, 22)}<span style="color:{SOFT}">Pay</span></span>'
           f'<span style="color:{SOFT}">+ squad fit {BONUS}%  {PAY - 30}</span><span style="color:{SOFT}">+ wreck  8</span>'
           f'<span class="row" style="gap:10px">{gems(PAY + 8, 26)}<span>Banked</span></span></div>')
    buttons = (f'<div class="abs row" style="left:0;right:0;top:880px;justify-content:center;gap:24px;align-items:flex-start">'
               + r120.choice("X", "Finish here", "Bank it all and end the voyage")
               + r120.choice("A", "Venture further", "Three jobs wait at Glassmere", True) + "</div>")
    return page(f"Rev {REV} A Awards - The Chart: the run as a line on the chart, one award card per seat",
                r116.dock_world() + head + route + "".join(cards) + pay + buttons)


# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# B  The Quay: people first
# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def b_giver(i, key, on):
    j = JOB[key]
    x, w = 40 + i * 300, 280
    if on:
        w = 0
    edge = f"inset 0 -2px 0 {SEL}" if on else "inset 0 0 0 1px rgba(67,76,137,.6)"
    return (f'<button class="pick abs" type="button" aria-pressed="{str(on).lower()}" style="left:{x}px;top:190px;width:280px;height:420px;overflow:hidden;'
            f'background:#00010A;box-shadow:{edge}">'
            f'<img src="{CREW[j[3]][3]}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:50% 20%;opacity:{1 if on else .55}">'
            f'<div class="abs" style="left:0;right:0;bottom:0;height:190px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.95) 50%)"></div>'
            f'<div class="abs" style="left:18px;right:18px;bottom:18px;display:flex;flex-direction:column;gap:8px;pointer-events:none">'
            f'<span class="lab" style="color:{INK if on else SOFT}">{j[2]}</span><span style="font-size:18px;line-height:1.3">{j[1]}</span>'
            f'<span class="row" style="gap:8px">{"".join(glyph(c, 18, INK if on else SOFT) for c in j[7])}</span></div></button>')


def b_board():
    j = JOB["salvage"]
    givers = "".join(b_giver(i, k, k == "salvage") for i, k in enumerate(("salvage", "escort", "bounty", "core")))
    mini = sea_map(1260, 190, 620, 420, "60% 45%", .3,
                   lane_svg([(60, 330), (220, 260), (380, 200), (540, 140)], w=620, h=420)
                   + place(60, 330, DOCK, True, 14) + place(540, 140, "Glassmere", False, 14)
                   + cell(220, 260, 46, "ahead") + cell(380, 200, 46, "ahead")
                   + f'<span class="abs lab" style="left:20px;top:18px">On the chart</span>'
                   + f'<span class="abs" style="left:20px;bottom:18px;font-size:14px;color:{SOFT}">{j[5]} cells  ·  3-5 min  ·  {DANGER[j[6]]} sea</span>')
    ask = (f'<div class="abs" style="left:40px;top:640px;width:1180px;display:flex;flex-direction:column;gap:16px">'
           f'<span class="lab">{j[11]}  ·  {j[12]}</span>'
           f'<span style="font-size:24px;line-height:1.45;max-width:1100px">"{j[9]}"</span>'
           f'<div class="row" style="gap:28px;margin-top:6px"><span class="lab">Bring me</span>{"".join(cls_tag(c, 16) for c in j[7])}</div></div>')
    reply = (f'<div class="abs" style="left:1260px;top:640px;width:620px;display:flex;flex-direction:column;gap:12px">'
             f'<span class="lab">Your crew says</span>'
             + "".join(f'<div class="row" style="gap:12px">{face(cs, 36, 44)}<span style="font-size:15px;flex:1;color:{INK}">"{MOOD_SAY[m]}"</span>{mood_mark(m)}</div>'
                       for _, _, _, _, cs, m, _, _ in SEATS) + '</div>')
    foot = (f'<div class="abs row" style="left:40px;right:40px;top:990px;justify-content:space-between">'
            f'{fit_meter()}<span class="row" style="gap:24px">{gems(PAY, 20)}'
            f'<button class="btn soft-cta" type="button" style="width:300px">{kc("A")}Take the job</button></span></div>')
    head = (f'<span class="abs" style="left:40px;top:164px;font-size:14px;color:{SOFT}">Docked at {DOCK}, {ZONE}. Four givers on the quay.</span>')
    return page(f"Rev {REV} B Board - The Quay: the givers stand on the quay; the one you pick asks, your crew answers",
                window(1, head + givers + mini + ask + reply + foot))


def b_seat(i):
    role, who, v, el, cs, mood, verb, n = SEATS[i]
    x = 40 + i * 470
    fits = VCLASS[v] in JOB["salvage"][7]
    return (f'<div class="abs" style="left:{x}px;top:190px;width:440px;height:760px;overflow:hidden;background:#00010A;'
            f'box-shadow:inset 0 0 0 1px {SEL if i == 0 else "rgba(67,76,137,.6)"}">'
            f'<img src="{CREW[cs][3]}" alt="" style="position:absolute;left:0;top:0;width:100%;height:470px;object-fit:cover;object-position:50% 22%">'
            f'<div class="abs" style="left:0;right:0;top:300px;height:170px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,1))"></div>'
            f'<div class="abs" style="left:22px;right:22px;top:20px;display:flex;justify-content:space-between"><span class="cap2" style="color:{INK}">{role}</span>'
            f'<span class="cap2" style="color:{INK}">{who}</span></div>'
            f'<div class="abs" style="left:22px;right:22px;top:360px;display:flex;flex-direction:column;gap:6px">'
            f'<span style="font-size:22px">{CREW[cs][0]}</span><span style="font-size:14px;color:{SOFT}">{CREW[cs][1]}</span></div>'
            f'<div class="abs" style="left:0;right:0;top:470px;bottom:0;background:#00041F;padding:20px 22px;box-sizing:border-box;display:flex;flex-direction:column;gap:14px">'
            f'<span style="font-size:17px;line-height:1.4;color:{RED2 if mood == "Refuses" else INK}">"{MOOD_SAY[mood]}"</span>'
            f'<div class="row" style="gap:24px">{mood_tag(mood)}{rank_tag(verb, n)}</div>'
            f'<div class="row" style="gap:14px;margin-top:6px"><img class="hull" src="{HULL[v]}" alt="{v}" style="width:110px;height:56px;object-fit:contain">'
            f'<span style="display:flex;flex-direction:column;gap:6px;flex:1"><span style="font-size:18px">{v}</span>{cls_tag(VCLASS[v], 14, INK if fits else MUTE, 18)}</span>'
            f'{flower(el, 30)}</div>'
            + (f'<button class="btn sm soft" type="button" style="width:100%;margin-top:auto">{kc("X")}Change pilot or vessel</button>' if i != 1 else
               f'<span style="font-size:14px;color:{MUTE};margin-top:auto">Rook picks their own seat.</span>')
            + '</div></div>')


def b_seats():
    j = JOB["salvage"]
    bench = [("HORNET", "Keen", 2), ("FLUME", "Steady", 1), ("NEEDLE", "Refuses", 1)]
    side = (f'<div class="abs" style="left:1460px;top:190px;width:420px;display:flex;flex-direction:column;gap:14px">'
            f'<span class="lab">{j[1]}</span><div class="row" style="gap:16px">{"".join(glyph(c, 22) for c in j[7])}'
            f'<span style="font-size:14px;color:{SOFT}">{j[11].split()[-1]} wants these</span></div>'
            f'{fit_meter(w=150)}<div style="height:10px"></div><span class="lab">Others at the harbour</span>'
            + "".join(f'<div class="row" style="gap:12px;height:72px;padding:0 12px;background:rgba(0,4,31,.6)">{face(cs, 44, 54, .5 if m == "Refuses" else 1)}'
                      f'<span style="display:flex;flex-direction:column;gap:5px;flex:1"><span style="font-size:15px;color:{MUTE if m == "Refuses" else INK}">{CREW[cs][0]}</span>'
                      f'{mood_tag(m, 13)}</span>{rank_mark(r)}</div>' for cs, m, r in bench)
            + f'<span style="font-size:14px;color:{SOFT};line-height:1.5">A pilot who refuses will not take the seat. Swap them or pick another job.</span></div>')
    bar = (f'<div class="abs row" style="left:40px;right:40px;top:990px;justify-content:flex-end;gap:24px">'
           f'<span style="font-size:16px;color:#B8C0F0">Pay {PAY}</span>'
           f'<button class="btn soft-cta" type="button" style="width:300px">{kc("A")}Set sail</button></div>')
    return page(f"Rev {REV} B Seats - The Quay: pilot cards that say how they feel; the vessel and its class under them",
                window(3, "".join(b_seat(i) for i in range(3)) + side + bar))


def b_lane_column(x, y, h):
    """The lane as a column: dock at the foot, three cells, dock at the top; events branch to the side."""
    ys = [y + h, y + h * .72, y + h * .44, y + h * .16, y - 10]
    parts = [f'<span class="abs" style="left:{x - 1}px;top:{y - 10}px;width:2px;height:{h + 10}px;background:rgba(184,192,240,.45)"></span>',
             f'<span class="abs" style="left:{x - 1}px;top:{ys[2]:.0f}px;width:2px;height:{ys[0] - ys[2]:.0f}px;background:{INK}"></span>']
    names = [DOCK, "Ember Shelf", "Ashwater", "Glassmere", "Glassmere dock"]
    for k, (yy, nm) in enumerate(zip(ys, names)):
        if k in (0, 4):
            parts.append(place(x, yy, nm, k == 0, 15))
        else:
            parts.append(cell(x, yy, 34, "behind" if k == 1 else "now" if k == 2 else "ahead"))
            parts.append(f'<span class="abs" style="left:{x + 48}px;top:{yy - 10:.0f}px;font-size:15px;color:{INK if k == 2 else SOFT}">{nm}</span>')
    parts.append(ark_dot(x, ys[2] + 30, 34))
    for (dy, side, kind, words, t) in ((ys[2], -1, "wreck", "Wreck surfacing", "0:42  ·  1 cell to port"),
                                        (ys[3] + 40, -1, "bloom", "Bloom", "0:31"),
                                        (ys[3] - 10, 1, "rival", RIVAL[0], "Racing you there")):
        ex = x - 330 if side < 0 else x + 110
        parts.append(f'<span class="abs" style="left:{min(x, ex + 260) if side < 0 else x + 40}px;top:{dy:.0f}px;width:{(x - ex - 260 - 40) if side < 0 else 60}px;'
                     f'height:0;border-top:1px dashed rgba(184,192,240,.5)"></span>')
        parts.append(event_mark(ex, dy - 15, kind, words, t))
    return "".join(parts)


def b_pause():
    blur = r116.world("pause")
    panel = (f'<div class="glass" style="position:absolute;left:1080px;top:0;width:840px;height:1080px;box-shadow:inset 1px 0 0 rgba(67,76,137,.8)">'
             + stack(60, 60, (f'<span class="hd" style="font-size:20px;color:{SOFT}">Paused</span>'
                              f'<span style="font-size:24px">The Glassmere Shatter</span>'
                              f'<span class="row" style="gap:16px">{gems(14)}<span style="font-size:16px;color:{SOFT}">/ 30 aboard  ·  cell 2 of 3</span></span>'), 700, 10)
             + b_lane_column(480, 260, 600)
             + f'<div class="abs" style="left:60px;bottom:40px">{party_note()}</div></div>')
    left = stack(80, 700, buttons_pause(360), 360)
    return page(f"Rev {REV} B Pause - The Quay: the world stays behind; the lane is a column with the events beside it",
                blur + panel + left)


def b_vote():
    cs = SEATS[1][4]
    comm = (f'<div class="abs slide-in" style="left:40px;top:640px;width:620px;display:flex;gap:18px;padding:18px;box-sizing:border-box;'
            f'background:linear-gradient(90deg,rgba(0,4,31,.9),rgba(0,4,31,.6));box-shadow:inset 2px 0 0 {INK}">'
            f'{face(cs, 84, 100)}<div style="display:flex;flex-direction:column;gap:10px;flex:1">'
            f'<span class="lab" style="color:{INK}">{CREW[cs][0]}  ·  Rook\'s pilot</span>'
            f'<span style="font-size:18px;line-height:1.4">"Wreck coming up to port. We strip it, we sink nothing. Detour?"</span>'
            f'<div class="row" style="gap:12px"><button class="btn sm def" type="button" style="box-shadow:inset 0 0 0 2px {INK}">{kc("◀")}Detour</button>'
            f'<button class="btn sm def" type="button">Press on{kc("▶")}</button><span class="big" style="font-size:22px;margin-left:auto">0:03</span></div>'
            f'<div class="row" style="gap:16px">'
            + "".join(f'<span class="row" style="gap:8px;font-size:14px;color:{INK if p else MUTE}">{face(c, 26, 32, 1 if p else .5)}{w}</span>'
                      for c, w, p in ((SEATS[0][4], "Detour", True), (SEATS[1][4], "Detour", True), (SEATS[2][4], "Press on", False)))
            + f'</div><span style="font-size:13px;color:{MUTE}">A tie goes to the host. Solo, it is your call.</span></div></div>')
    light = ('<span class="abs" style="left:-140px;top:260px;width:520px;height:560px;border-radius:50%;'
             'background:radial-gradient(ellipse,rgba(230,233,255,.2),rgba(230,233,255,0) 65%)"></span>')
    return page(f"Rev {REV} B Vote - The Quay: the pilot who saw it asks, the three faces are the votes",
                salvage_hud() + light + comm + VESSEL)


def b_awards():
    head = stack(0, 60, (f'<span class="cap2">The Glassmere Shatter  ·  4:12</span>'
                         f'<span class="hd" style="font-size:44px">Voyage complete</span>'
                         f'<span class="row" style="gap:16px;font-size:18px;color:{SOFT}">{gems(PAY + 8, 24)}banked, with +{BONUS}% for squad fit</span>'), W, 10, "center")
    cols = []
    for i, (award, cs, v, who, line, mood, rank) in enumerate(AWARDS):
        x = 120 + i * 570
        cols.append(f'<div class="abs" style="left:{x}px;top:250px;width:540px;height:560px;overflow:hidden;background:#00010A;'
                    f'box-shadow:inset 0 0 0 1px {INK if i == 0 else "rgba(67,76,137,.6)"}">'
                    f'<img src="{CREW[cs][3]}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:50% 20%">'
                    f'<div class="abs" style="left:0;right:0;bottom:0;height:300px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.96) 40%)"></div>'
                    f'<div class="abs" style="left:26px;right:26px;bottom:24px;display:flex;flex-direction:column;gap:10px">'
                    f'<span class="hd" style="font-size:24px">{award}</span><span style="font-size:16px;color:{SOFT}">{line}</span>'
                    f'<span style="font-size:16px">{CREW[cs][0]}  ·  {who}  ·  {v}</span>'
                    f'<div class="row" style="gap:20px;font-size:14px;color:{SOFT}">{mood_mark(mood.split()[-1])}{mood}'
                    f'{rank_mark(3 if "Veteran" in rank else 2 if "Seasoned" in rank else 1)}{rank}</div></div></div>')
    buttons = (f'<div class="abs row" style="left:0;right:0;top:860px;justify-content:center;gap:24px;align-items:flex-start">'
               + r120.choice("X", "Finish here", "Bank it all and end the voyage")
               + r120.choice("A", "Venture further", "Three jobs wait at Glassmere", True) + "</div>")
    return page(f"Rev {REV} B Awards - The Quay: the three pilots stand up for their award, mood and rank under each",
                r116.dock_world() + head + "".join(cols) + buttons)


# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# C  The Ledger: quiet, application-like
# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def c_row(key, on, y):
    j = JOB[key]
    fit = sum(1 for s in SEATS if VCLASS[s[2]] in j[7])
    bonus = {0: 0, 1: 10, 2: 20, 3: 35}[fit]
    edge = f"box-shadow:inset 2px 0 0 {SEL};background:rgba(0,212,255,.06)" if on else "background:rgba(0,4,31,.55)"
    segs = "".join(f'<span style="flex:1;height:8px;background:{INK if i < fit else "rgba(67,76,137,.6)"}"></span>' for i in range(3))
    return (f'<button class="pick abs row" type="button" aria-pressed="{str(on).lower()}" style="left:40px;top:{y}px;width:1100px;height:100px;'
            f'gap:0;padding:0 20px;box-sizing:border-box;{edge}">'
            f'<span class="row" style="gap:14px;width:330px">{face(j[3], 50, 60)}<span style="display:flex;flex-direction:column;gap:5px">'
            f'<span style="font-size:17px">{j[1]}</span><span style="font-size:13px;color:{MUTE}">{j[11]}  ·  {j[12]}</span></span></span>'
            f'<span style="width:190px">{danger(j[6], 70)}</span>'
            f'<span class="num" style="width:110px;font-size:15px;color:{SOFT}">{j[5]} cells</span>'
            f'<span class="row" style="gap:8px;width:150px">{"".join(glyph(c, 20) for c in j[7])}</span>'
            f'<span class="row" style="gap:4px;width:110px;margin-right:20px">{segs}</span>'
            f'<span class="num" style="width:60px;font-size:15px;color:{SOFT}">+{bonus}%</span>'
            f'<span class="row" style="gap:8px;margin-left:auto">{i_gem(14, INK)}<span class="big" style="font-size:20px">{j[8] + j[8] * bonus // 100}</span></span></button>')


def c_heads():
    cols = [("Job and giver", 330), ("Sea", 190), ("Length", 110), ("Wants", 150), ("Your fit", 130), ("Bonus", 60)]
    return (f'<div class="abs row" style="left:60px;top:236px;gap:0">'
            + "".join(f'<span class="lab" style="width:{w}px">{n}</span>' for n, w in cols)
            + f'<span class="lab" style="margin-left:90px">Pay</span></div>')


def c_detail(x=1180, y=236, w=700):
    j = JOB["salvage"]
    strip = r116.route_h  # the in-game strip, reused so the run and the board read the same
    lane = (f'<div style="position:relative;width:{w - 56}px;height:60px">'
            + "".join(f'<span class="abs" style="left:{k * (w - 76) / 3:.0f}px;top:0;width:18px;height:18px;transform:rotate(45deg);'
                      f'background:{INK if k == 0 else "#00041F"};box-shadow:inset 0 0 0 2px {INK}"></span>' if k in (0, 3) else
                      f'<span class="abs" style="left:{k * (w - 76) / 3 - 12:.0f}px;top:-12px;width:42px;height:42px;border-radius:50%;'
                      f'box-shadow:inset 0 0 0 1px rgba(184,192,240,.6)"></span>' for k in range(4))
            + f'<span class="abs" style="left:18px;right:18px;top:8px;height:0;border-top:2px dashed rgba(184,192,240,.5)"></span>'
            + f'<span class="abs" style="left:0;top:34px;font-size:13px;color:{SOFT}">{DOCK}</span>'
            + f'<span class="abs" style="right:0;top:34px;font-size:13px;color:{SOFT}">Glassmere</span></div>')
    del strip
    return (f'<div class="glass" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{960 - y}px;box-sizing:border-box;padding:26px 28px;'
            f'display:flex;flex-direction:column;gap:18px;box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)">'
            f'<span class="lab">{j[2]}</span><span style="font-size:24px">{j[1]}</span>'
            f'<span style="font-size:16px;line-height:1.5;color:{SOFT}">"{j[9]}"  {j[11]}</span>{lane}'
            f'<div class="row" style="gap:20px">{"".join(cls_tag(c, 15) for c in j[7])}</div>'
            f'<div style="display:flex;flex-direction:column;gap:0">'
            + "".join(f'<div class="row" style="gap:14px;height:52px;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)">'
                      f'<span style="width:110px;font-size:15px">{v}</span>{cls_tag(VCLASS[v], 14, INK if VCLASS[v] in j[7] else MUTE, 18)}'
                      f'<span style="margin-left:auto">{mood_tag(m, 14)}</span></div>' for _, _, v, _, _, m, _, _ in SEATS)
            + f'</div>{fit_meter(w=150)}'
            f'<div class="row" style="margin-top:auto;justify-content:space-between">{gems(PAY, 20)}'
            f'<button class="btn soft-cta" type="button" style="width:300px">{kc("A")}Take the job</button></div></div>')


def c_board():
    filt = (f'<div class="abs row" style="left:40px;top:180px;gap:10px">'
            + "".join(f'<button class="btn sm {"def" if n == "All" else "soft"}" type="button" style="{"box-shadow:inset 0 0 0 1px " + INK if n == "All" else ""}">{n}</button>'
                      for n in ("All", "Fits my squad", "Calm sea", "Best pay"))
            + f'<span style="font-size:14px;color:{SOFT};margin-left:20px">Docked at {DOCK}  ·  {ZONE}</span></div>')
    rows = "".join(c_row(k, k == "salvage", 280 + i * 112) for i, k in enumerate(("salvage", "escort", "bounty", "core")))
    lock = (f'<div class="abs row" style="left:40px;top:740px;gap:10px;font-size:14px;color:{MUTE}">{r100.LOCK}'
            f'Lair Hunt, Liberate the Pen and Courier Race open after Milestone 2</div>')
    return page(f"Rev {REV} C Board - The Ledger: one quiet list with fit and pay as columns, the detail pane on the right",
                window(1, filt + c_heads() + rows + lock + c_detail()))


def c_seats():
    j = JOB["salvage"]
    cols = [("Seat", 170), ("Vessel", 280), ("Class", 200), ("Pilot", 360), ("Mood", 260), ("Rank", 230), ("Element", 140)]
    head = (f'<div class="abs row" style="left:60px;top:300px">' + "".join(f'<span class="lab" style="width:{w}px">{n}</span>' for n, w in cols) + '</div>')
    rows = []
    for i, (role, who, v, el, cs, mood, verb, n) in enumerate(SEATS):
        fits = VCLASS[v] in j[7]
        edge = f"box-shadow:inset 2px 0 0 {SEL};background:rgba(0,212,255,.06)" if i == 0 else "background:rgba(0,4,31,.55)"
        rows.append(f'<button class="pick abs row" type="button" style="left:40px;top:{340 + i * 120}px;width:1840px;height:108px;padding:0 20px;box-sizing:border-box;{edge}">'
                    f'<span style="width:170px;display:flex;flex-direction:column;gap:4px"><span style="font-size:16px">{role}</span><span style="font-size:13px;color:{MUTE}">{who}</span></span>'
                    f'<span class="row" style="width:280px;gap:14px"><img class="hull" src="{HULL[v]}" alt="" style="width:120px;height:60px;object-fit:contain"><span style="font-size:18px">{v}</span></span>'
                    f'<span style="width:200px">{cls_tag(VCLASS[v], 15, INK if fits else MUTE)}</span>'
                    f'<span class="row" style="width:360px;gap:12px">{face(cs, 46, 56)}<span style="font-size:16px">{CREW[cs][0]}</span></span>'
                    f'<span style="width:260px;display:flex;flex-direction:column;gap:4px">{mood_tag(mood)}<span style="font-size:13px;color:{MUTE}">{MOOD_WHY[mood]}</span></span>'
                    f'<span style="width:230px">{rank_tag(verb, n)}</span>'
                    f'<span style="width:140px">{flower(el, 28)}</span></button>')
    refuse = (f'<div class="abs row" style="left:40px;top:720px;width:1840px;height:72px;padding:0 20px;box-sizing:border-box;gap:14px;'
              f'background:rgba(255,75,58,.06);box-shadow:inset 2px 0 0 {RED2}">{face("NEEDLE", 36, 44, .6)}'
              f'<span style="font-size:15px">{CREW["NEEDLE"][0]} was in seat 3.</span>{mood_tag("Refuses", 15)}'
              f'<span style="font-size:15px;color:{SOFT}">"I will not pick over the dead." Swapped for {CREW["OPAL"][0]}.</span>'
              f'<button class="btn sm soft" type="button" style="margin-left:auto">Undo</button></div>')
    top = (f'<div class="abs row" style="left:40px;top:190px;gap:24px">{face(j[3], 44, 52)}<span style="display:flex;flex-direction:column;gap:6px">'
           f'<span style="font-size:20px">{j[1]}</span><span style="font-size:14px;color:{SOFT}">Wants {", ".join(j[7])}</span></span></div>')
    bar = (f'<div class="abs row" style="left:40px;right:40px;top:990px;justify-content:space-between">{fit_meter()}'
           f'<span class="row" style="gap:24px"><span style="font-size:16px;color:#B8C0F0">Pay {PAY}</span>'
           f'<button class="btn soft-cta" type="button" style="width:300px">{kc("A")}Set sail</button></span></div>')
    return page(f"Rev {REV} C Seats - The Ledger: the seats as rows; a refusal says who left and who took the seat",
                window(3, top + head + "".join(rows) + refuse + bar))


def c_strip(w=1500):
    n = 5
    xs = [i * w / (n - 1) for i in range(n)]
    names = [DOCK, "Ember Shelf", "Ashwater", "Glassmere", "Dock"]
    out = [f'<div style="position:relative;width:{w}px;height:280px">',
           f'<span class="abs" style="left:0;right:0;top:139px;height:2px;background:rgba(184,192,240,.45)"></span>',
           f'<span class="abs" style="left:0;top:139px;width:{xs[2] - 40:.0f}px;height:2px;background:{INK}"></span>']
    for k, (x, nm) in enumerate(zip(xs, names)):
        if k in (0, 4):
            out.append(f'<span class="abs" style="left:{x - 9:.0f}px;top:131px;width:18px;height:18px;transform:rotate(45deg);'
                       f'background:{INK if k == 0 else "#00041F"};box-shadow:inset 0 0 0 2px {INK}"></span>')
        else:
            out.append(cell(x, 140, 44, "behind" if k == 1 else "now" if k == 2 else "ahead"))
        out.append(f'<span class="abs" style="left:{x:.0f}px;top:196px;transform:translateX(-50%);font-size:15px;white-space:nowrap;'
                   f'color:{INK if k == 2 else SOFT}">{nm}</span>')
    out.append(f'<span class="abs" style="left:{xs[2] - 58:.0f}px;top:122px">{ark_dot(0, 0, 34).replace("left:-17px;top:-17px", "left:0;top:0")}</span>')
    for (x, up, kind, words, t) in ((xs[2], True, "wreck", "Wreck surfacing", "0:42  ·  port"), (xs[3] - 120, False, "bloom", "Bloom", "0:31  ·  starboard"),
                                     (xs[3] + 60, True, "rival", RIVAL[0], "Racing you there")):
        y = 20 if up else 236
        out.append(f'<span class="abs" style="left:{x:.0f}px;top:{56 if up else 164}px;width:0;height:{60 if up else 60}px;border-left:1px dashed rgba(184,192,240,.5)"></span>')
        out.append(event_mark(x - 15, y, kind, words, t))
    out.append("</div>")
    return "".join(out)


def c_pause():
    head = stack(0, 140, (f'<span class="hd" style="font-size:20px;color:{SOFT}">Paused</span>'
                          f'<span style="font-size:24px">The Glassmere Shatter  ·  cell 2 of 3</span>'), W, 8, "center")
    strip = f'<div class="abs" style="left:210px;top:290px">{c_strip()}</div>'
    stats = (f'<div class="abs row" style="left:0;right:0;top:640px;justify-content:center;gap:60px;font-size:16px;color:{SOFT}">'
             f'<span class="row" style="gap:10px">{gems(14)}/ 30 aboard</span><span>Hull 82%</span><span>{DANGER[2]} sea</span><span>0:52 to the next cell</span></div>')
    btns = (f'<div class="abs row" style="left:0;right:0;top:740px;justify-content:center;gap:16px">'
            f'<button class="btn def" type="button" style="width:260px">{kc("B")}Resume</button>'
            f'<button class="btn def" type="button" style="width:260px">Settings</button>'
            f'<button class="btn def" type="button" style="width:260px;color:{SOFT}">Leave voyage</button></div>'
            f'<div class="abs" style="left:0;right:0;top:830px;text-align:center">{party_note()}</div>')
    return page(f"Rev {REV} C Pause - The Ledger: the lane as one strip, the events above and below it, the buttons in a row",
                r116.world("pause") + '<div class="abs" style="inset:0;background:rgba(0,1,10,.35)"></div>' + head + strip + stats + btns)


def c_vote():
    ext = (f'<span style="width:1px;height:26px;background:{LINE}"></span>{i_flag(16, INK)}'
           f'<span style="font-size:18px">Wreck to port</span>'
           f'<span class="row" style="gap:6px;font-size:18px"><span class="kc" style="box-shadow:inset 0 0 0 1px {INK}">◀</span>Detour 2</span>'
           f'<span class="row" style="gap:6px;font-size:18px;color:{SOFT}">Press on 1<span class="kc">▶</span></span>'
           f'<span class="big" style="font-size:20px">0:03</span>')
    hud = world("raid") + plate(*r120.RAID_ARK, 14, 82, line="+5 to stow", goal="/ 30") + chip(14, 5, 82, clock="2:10", extra=ext).replace(f"/{r120.GOAL}", "/30")
    feed = toast("Wreck surfacing next door", "Vote on the D-pad. A tie goes to the host.", "event")
    edge = ('<span class="abs" style="left:0;top:200px;width:180px;height:680px;'
            'background:radial-gradient(ellipse at 0 50%,rgba(230,233,255,.2),rgba(230,233,255,0) 70%)"></span>')
    return page(f"Rev {REV} C Vote - The Ledger: the vote rides in the chip, the feed says how; the screen stays clear",
                hud + edge + feed + VESSEL)


def c_awards():
    head = stack(80, 80, (f'<span class="cap2">The Glassmere Shatter  ·  3 cells and one detour  ·  4:12</span>'
                          f'<span class="hd" style="font-size:44px">Voyage complete</span>'), 1200, 8)
    cols = [("Seat", 300), ("Award", 300), ("What they did", 440), ("Mood", 300), ("Rank", 260)]
    th = (f'<div class="abs row" style="left:100px;top:250px">' + "".join(f'<span class="lab" style="width:{w}px">{n}</span>' for n, w in cols) + '</div>')
    rows = "".join(
        f'<div class="abs row" style="left:80px;top:{290 + i * 104}px;width:1760px;height:92px;padding:0 20px;box-sizing:border-box;'
        f'background:rgba(0,4,31,.6);box-shadow:inset 2px 0 0 {INK if i == 0 else LINE}">'
        f'<span class="row" style="width:300px;gap:12px">{face(cs, 42, 50)}<span style="display:flex;flex-direction:column;gap:4px">'
        f'<span style="font-size:16px">{who}</span><span style="font-size:13px;color:{MUTE}">{v}  ·  {CREW[cs][0]}</span></span></span>'
        f'<span class="hd" style="width:300px;font-size:18px">{award}</span>'
        f'<span style="width:440px;font-size:16px;color:{SOFT}">{line}</span>'
        f'<span class="row" style="width:300px;gap:10px;font-size:15px">{mood_mark(mood.split()[-1])}{mood}</span>'
        f'<span class="row" style="width:260px;gap:10px;font-size:15px">{rank_mark(3 if "Veteran" in rank else 2 if "Seasoned" in rank else 1)}{rank}</span></div>'
        for i, (award, cs, v, who, line, mood, rank) in enumerate(AWARDS))
    pay = (f'<div class="abs" style="left:80px;top:640px;width:640px;display:flex;flex-direction:column;gap:0">'
           + "".join(f'<div class="row" style="height:46px;justify-content:space-between;font-size:17px;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)">'
                     f'<span style="color:{SOFT}">{a}</span><span class="num">{b}</span></div>'
                     for a, b in (("Job pay", "30"), (f"Squad fit {FIT} of 3, +{BONUS}%", str(PAY - 30)), ("Wreck, on the detour", "8")))
           + f'<div class="row" style="height:56px;justify-content:space-between"><span style="font-size:18px">Banked</span>{gems(PAY + 8, 24)}</div></div>')
    rival = (f'<div class="abs" style="left:800px;top:640px;width:560px;display:flex;flex-direction:column;gap:10px">'
             f'<span class="lab">{RIVAL[0]}</span><span style="font-size:16px;color:{SOFT};line-height:1.5">Lost the race to Glassmere and two hulls. '
             f'They will remember you: once more and they come looking.</span></div>')
    buttons = (f'<div class="abs row" style="left:80px;right:80px;top:940px;justify-content:flex-end;gap:16px">'
               f'<button class="btn def" type="button" style="width:280px">{kc("X")}Finish here</button>'
               f'<button class="btn soft-cta" type="button" style="width:320px">{kc("A")}Venture further</button></div>')
    return page(f"Rev {REV} C Awards - The Ledger: the end board as a table, pay itemised, the rival's grudge in one line",
                r116.dock_world() + head + th + rows + pay + rival + buttons)


# ── notes ───────────────────────────────────────────────────────────────────────────────────────────────────────────
def notes():
    rows = [
        ("Asked", "Shombith, 24 Nov: \"we have to create a well engaging UI ... First the app shell and in game UI\". "
                  "Five screens from the Milestone 2 design, each drawn three ways, each screen its own board."),
        ("Same content in all three", f"Docked at {DOCK}; four givers (Salvage, Escort, Bounty, Hold the Core); the default squad "
                                      "Sparrow, Squirrel, Dolphin; Cass Morrow's Salvage wants Cultivator, Pathfinder, Protector, so the squad fits 1 of 3 "
                                      f"(pay +{BONUS}%, 30 to {PAY}). The Rusted Grin race you; a wreck surfaces next door. Givers, squads and "
                                      "lore come from the Voyage Codex."),
        ("A The Chart (recommended)", "Pillar 1 says the sea is real, so the map is the voyage: jobs are giver pins, pause is the chart framed on "
                                      "your lane with the Ark live, the vote points at a lit membrane. One picture from the board to the run."),
        ("B The Quay", "People first: the givers ask in their own words and your pilots answer, so mood reads before any number. "
                       "Uses the pilot scenes. The pause map is a column beside the paused world."),
        ("C The Ledger", "Quietest and closest to an application: fit, pay and danger as columns you can sort, the vote inside the HUD chip."),
        ("Marks", "Class: the seven glyphs from the design (outline only, no colour). Mood: three bars (Keen 3, Steady 2, Reluctant 1), "
                  "Refuses is a struck circle in the threat red. Rank: one to three chevrons per work verb. Fit: three segments. "
                  "No domain colour, no element mark except the element petals."),
        ("Kept", "Rev 115 A's line-up (A Seats), Rev 120 B's plate, chip and feed in every in-game board, the game's text sizes, one layout "
                 "for every device. Lime only on Take the job, Set sail and Venture further."),
        ("Names", "Every voyage carries its Codex name (The Glassmere Shatter, The Tollmoor Crossing, Black Tide Rising, Ironmouth Holds); "
                  "the type (Salvage, Escort the Ark, Bounty, Hold the Core) is only the small label above it, never the title."),
        ("Givers", "The four givers are the Codex's (Cass Morrow, Captain Theo Lark, Juno Rask, Marshal Edda Kroll); each shows the portrait of their callsign until they get their own."),
    ]
    html = notes_page("notes: the Milestone 2 Voyages UI, three ways", rows)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


BOARDS = {
    "ABoard": a_board, "ASeats": a_seats, "APause": a_pause, "AVote": a_vote, "AAwards": a_awards,
    "BBoard": b_board, "BSeats": b_seats, "BPause": b_pause, "BVote": b_vote, "BAwards": b_awards,
    "CBoard": c_board, "CSeats": c_seats, "CPause": c_pause, "CVote": c_vote, "CAwards": c_awards,
}

if __name__ == "__main__":
    for key, fn in BOARDS.items():
        (ROOT / f"R{REV}{key}.dc.html").write_text(fn(), encoding="utf-8")
    (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", len(BOARDS) + 1, "boards to", ROOT)
