#!/usr/bin/env python3
"""Rev 137 boards: rival Arks and runaway crews, and how you beat each (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-24: "Lets make rival ark thats cooler and makes sense there can be runaway crews as well no problem
both can be there and how do i defeat theses players?"

Two kinds of rival, both AI crews in Milestone 2 (people in a rival squad wait for DEPTH.md D3), PvP petals only:
  A rival Ark   three escorts sailing their own Ark and its hold. Beat them by breaking their Ark (its hull is mass, like
                ours: the hold bursts and its crystal drifts loose, the crew turns back), by silencing the escorts so the
                Ark has no one guarding it, or by finishing the job first.
  A runaway crew  three vessels with no Ark and nothing to protect; they raid your drifts and your Ark. Silence all three
                at once and they break and run, dropping what they carried as loose crystal.

  Chart        the pause chart: your lane, a rival Ark on its own lane, a runaway crew loose in the next cell
  ArkMeet      a rival Ark arrives: their Ark in the world, lit, with its plate (hold, hull, escorts)
  ArkOpen      two escorts silenced: nobody guards her hull
  ArkBreak     her hull breaks: the hold bursts, the crystal drifts loose, the crew turns back
  Runaway      a runaway crew raids your drift: the crew strip shows who is silenced
  Rout         all three silenced at once: they break and run, dropping their haul
  Fall         the end board's rival line: what each crew lost, its grudge, the Rival Breaker award

    python3 Docs/MenuRedesignB/rounds/r137_voyages_rivals.py <scratch root> [rev]
"""
import importlib.util
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "137"
_spec = importlib.util.spec_from_file_location("r134", HERE / "r134_voyages_m2_all.py")
r134 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r134)
r133, r120, r116, r114 = r134.r133, r134.r120, r134.r116, r134.r114

REV = int(_rev)
ROOT = r134.ROOT
INK, MUTE, SOFT, LIME, RED, RED2, SEL = r134.INK, r134.MUTE, r134.SOFT, r134.LIME, r134.RED, r134.RED2, r134.SEL
HULL, ARK, G, CREW, SEATS = r134.HULL, r134.ARK, r134.G, r134.CREW, r134.SEATS
page, toast, face, gems, stack, kc = r134.page, r134.toast, r134.face, r134.gems, r134.stack, r134.kc
hud, gem, fauna, VESSEL = r134.hud, r116.gem, r116.fauna, r116.VESSEL
sea_map, cell, place, zone, lane_svg, ark_dot, event_mark = (r133.sea_map, r133.cell, r133.place, r133.zone, r133.lane_svg,
                                                             r133.ark_dot, r133.event_mark)
DOCK = r134.DOCK
i_ark, i_warn = r116.i_ark, r116.i_warn

ARKCREW = ("The Rusted Grin", "Pirate", "Greedy", ["Dolphin", "Serpent", "Rhino"], "The Grin's Ark, Saltjaw")
RUNAWAY = ("The Gutter Tide", "Thief", "Bold", ["Manta", "Sparrow", "Rhino"])


# ── pieces ──────────────────────────────────────────────────────────────────────────────────────────────────────────
def rival_ark(x, y, w, broken=False):
    """Their Ark in the world: the same hull class as ours, facing us, lit from inside in the threat red (a light, not a ring)."""
    glow = (f'<span aria-hidden="true" class="abs" style="left:{x - w * .15:.0f}px;top:{y - w * .12:.0f}px;width:{w * 1.3:.0f}px;height:{w * .7:.0f}px;'
            f'border-radius:50%;background:radial-gradient(ellipse,rgba(255,75,58,{.18 if broken else .30}),rgba(255,75,58,0) 70%)"></span>')
    img = (f'<img src="{ARK}" alt="Their Ark" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{w * .47:.0f}px;object-fit:contain;'
           f'transform:scaleX(-1) rotate({8 if broken else -3}deg);opacity:{.55 if broken else .95};filter:saturate(.6)">')
    return glow + img


def escort(v, state):
    col = {"Guarding": INK, "Silenced": MUTE, "Turning back": SOFT, "Running": SOFT}[state]
    return (f'<span class="row" style="gap:10px;opacity:{.5 if state == "Silenced" else 1}"><img class="hull" src="{HULL[v]}" alt="{v}" '
            f'style="width:64px;height:32px;object-fit:contain;transform:scaleX(-1)"><span style="display:flex;flex-direction:column;gap:2px">'
            f'<span style="font-size:14px;color:{INK}">{v}</span><span style="font-size:14px;color:{col}">{state}</span></span></span>')


def rplate(x, y, crew, hull, hold, states, line, ark=True):
    """The rival's plate, hung in the world over their Ark (or over the crew): name, what they carry, hull, each escort."""
    hullbar = (f'<div class="row" style="gap:10px"><span class="lab" style="width:70px">Hull</span><span style="width:200px;height:8px;'
               f'background:rgba(67,76,137,.6)"><span style="display:block;height:100%;width:{hull}%;background:{RED2}"></span></span>'
               f'<span class="num" style="font-size:14px">{hull}%</span></div>') if ark else ""
    return (f'<div class="abs tin" style="left:{x}px;top:{y}px;width:380px;display:flex;flex-direction:column;gap:10px;padding:14px 18px;'
            f'background:rgba(0,4,31,.82);box-shadow:inset 0 2px 0 {RED2}">'
            f'<div class="row" style="gap:10px">{i_ark(20, RED2) if ark else i_warn(18, RED2)}<span class="hd" style="font-size:18px;color:{RED2}">{crew[0]}</span>'
            f'<span style="font-size:14px;color:{SOFT};margin-left:auto">{crew[1]}  ·  {crew[2]}</span></div>'
            f'<div class="row" style="gap:10px"><span class="lab" style="width:70px">{"Hold" if ark else "Carrying"}</span>{gems(hold, 15)}</div>'
            + hullbar
            + '<div style="display:flex;flex-direction:column;gap:8px">' + "".join(escort(v, s) for v, s in zip(crew[3], states)) + '</div>'
            + f'<span style="font-size:14px;color:{SOFT};line-height:1.4">{line}</span></div>')


def spill(cx, cy, n=10, r=170):
    pts = [(cx + int(r * ((i * 37) % 100 - 50) / 50), cy + int(r * .6 * ((i * 61) % 100 - 50) / 50)) for i in range(n)]
    return "".join(gem(x, y, .7 + (i % 3) * .1) for i, (x, y) in enumerate(pts))


def runaway_hulls(x, y, states):
    out = []
    for k, (v, s) in enumerate(zip(RUNAWAY[3], states)):
        lit = s == "Guarding"
        out.append(f'<span aria-hidden="true" class="abs" style="left:{x + k * 170 - 20}px;top:{y + (k % 2) * 70 - 30}px;width:220px;height:150px;'
                   f'border-radius:50%;background:radial-gradient(ellipse,rgba(255,75,58,{.26 if lit else .06}),rgba(255,75,58,0) 70%)"></span>'
                   f'<img class="hull" src="{HULL[v]}" alt="" style="position:absolute;left:{x + k * 170}px;top:{y + (k % 2) * 70}px;width:180px;height:90px;'
                   f'object-fit:contain;transform:scaleX(-1);opacity:{1 if lit else .45}">')
    return "".join(out)


# ── boards ──────────────────────────────────────────────────────────────────────────────────────────────────────────
def b_chart():
    pts = [(300, 760), (720, 600), (1120, 470), (1560, 330)]
    theirs = [(1700, 820), (1420, 640), (1120, 470)]
    inner = (lane_svg(pts, [(300, 760), (720, 600), (870, 552)])
             + '<svg class="abs" style="left:0;top:0" width="1920" height="1080" aria-hidden="true">'
             + f'<path d="M{" L".join(f"{a},{b}" for a, b in theirs)}" fill="none" stroke="{RED2}" stroke-width="2" stroke-dasharray="8 8"></path></svg>'
             + cell(520, 690, 120, "behind", "Ember Shelf") + cell(920, 535, 130, "now", "Ashwater") + cell(1340, 400, 120, "ahead", "Glassmere")
             + cell(760, 330, 100, "event")
             + place(300, 760, DOCK, True) + place(1560, 330, "Glassmere dock", False)
             + ark_dot(870, 552, 40)
             + f'<span class="abs" style="left:892px;top:578px;font-size:14px;color:{INK};white-space:nowrap">You  ·  0:52 to the next cell</span>'
             + f'<span class="abs" style="left:1403px;top:623px;width:34px;height:34px;border-radius:50%;background:rgba(0,4,31,.9);'
               f'box-shadow:inset 0 0 0 2px {RED2};display:flex;align-items:center;justify-content:center">{i_ark(24, RED2)}</span>'
             + f'<span class="abs" style="left:1450px;top:620px;display:flex;flex-direction:column;gap:2px;text-shadow:0 0 6px #00010A">'
               f'<span style="font-size:15px;color:{RED2};white-space:nowrap">{ARKCREW[0]}  ·  rival Ark</span>'
               f'<span class="num" style="font-size:14px;color:{SOFT};white-space:nowrap">Also bound for Glassmere  ·  hold 22</span></span>'
             + event_mark(700, 300, "rival", f"{RUNAWAY[0]}  ·  runaway crew", "Loose in the next cell  ·  no Ark"))
    legend = (f'<div class="glass" style="position:absolute;left:40px;top:40px;width:560px;box-sizing:border-box;padding:24px;display:flex;flex-direction:column;gap:16px">'
              f'<span class="hd" style="font-size:20px;color:{SOFT}">Paused  ·  two kinds of rival</span>'
              f'<div class="row" style="gap:14px;align-items:flex-start"><span style="width:30px;flex:none">{i_ark(26, RED2)}</span>'
              f'<span style="display:flex;flex-direction:column;gap:4px"><span style="font-size:18px">Rival Ark</span>'
              f'<span style="font-size:15px;color:{SOFT};line-height:1.4">Three escorts sail their own Ark on their own lane. Her hold is the prize: '
              f'break her hull and it spills.</span></span></div>'
              f'<div class="row" style="gap:14px;align-items:flex-start"><span style="width:30px;flex:none">{i_warn(24, RED2)}</span>'
              f'<span style="display:flex;flex-direction:column;gap:4px"><span style="font-size:18px">Runaway crew</span>'
              f'<span style="font-size:15px;color:{SOFT};line-height:1.4">Three vessels, no Ark, nothing to lose. They take from your drifts and your '
              f'Ark. Silence all three and they run.</span></span></div>'
              f'<div style="display:flex;flex-direction:column;gap:12px;margin-top:6px">{r133.buttons_pause(300)}</div></div>')
    return sea_map(0, 0, 1920, 1080, "50% 45%", .25, inner) + legend


def b_meet():
    return (hud() + rival_ark(1260, 380, 460)
            + r116.r114.hull("Dolphin", 1180, 640, 150) + r116.r114.hull("Serpent", 1400, 680, 140) + r116.r114.hull("Rhino", 1620, 630, 150)
            + rplate(1464, 96, ARKCREW, 100, 22, ["Guarding"] * 3, "Her escorts wall her hull. Silence them, then break her.")
            + toast(f"{ARKCREW[0]} sail their own Ark", f"{ARKCREW[4]} carries 22 crystals. Break her hull and the hold spills.", "threat"))


def b_open():
    return (hud(17, 82, "+3 to stow") + rival_ark(1260, 380, 460)
            + r116.r114.hull("Rhino", 1620, 630, 150)
            + rplate(1464, 96, ARKCREW, 64, 22, ["Silenced", "Silenced", "Guarding"],
                     "Two escorts silenced: her hull is open. Destroyers, now.")
            + toast("Saltjaw is unguarded", "Dolphin and Serpent are silenced for 0:06. Break her hull before they recover.", "event"))


def b_break():
    return (hud(17, 82, "+3 to stow") + rival_ark(1260, 400, 460, broken=True) + spill(1480, 680, 12, 220)
            + rplate(1464, 96, ARKCREW, 0, 0, ["Turning back"] * 3, "Her hold is gone. The crew turns back to save her.")
            + toast("Saltjaw's hold bursts", "22 crystals drift loose. Take what you can reach before the fauna do.", "good"))


def b_runaway():
    return (hud(19, 74, "Being robbed", "hot") + runaway_hulls(1020, 440, ["Silenced", "Guarding", "Guarding"])
            + rplate(1464, 96, RUNAWAY, 0, 6, ["Silenced", "Guarding", "Guarding"],
                     "Silence all three at once and they run. One is down.", ark=False)
            + toast(f"{RUNAWAY[0]} are in your drift", "A runaway crew: no Ark, nothing to lose. They take from your hold.", "threat"))


def b_rout():
    return (hud(19, 74, "+6 loose") + runaway_hulls(1120, 460, ["Running"] * 3) + spill(1180, 560, 6, 160)
            + rplate(1464, 96, RUNAWAY, 0, 0, ["Running", "Running", "Running"],
                     "All three silenced at once. They drop their haul and run.", ark=False)
            + toast(f"{RUNAWAY[0]} break and run", "They dropped 6 crystals in the drift. Grudge raised: they will remember you.", "good"))


def b_fall():
    rows = [(ARKCREW, True, "Hold broken  ·  22 crystals spilled, 14 taken", "Grudge 1", "OPAL  ·  Rival Breaker"),
            (RUNAWAY, False, "Routed  ·  6 crystals dropped, all taken", "Grudge 2  ·  they come after you next voyage", "TALON")]
    lines = "".join(
        f'<div class="row" style="gap:24px;height:96px;padding:0 24px;background:rgba(0,4,31,.6);box-shadow:inset 2px 0 0 {RED2}">'
        f'<span style="width:30px">{i_ark(26, RED2) if ark else i_warn(24, RED2)}</span>'
        f'<span style="display:flex;flex-direction:column;gap:4px;width:360px"><span style="font-size:20px">{c[0]}</span>'
        f'<span style="font-size:14px;color:{SOFT}">{"Rival Ark" if ark else "Runaway crew"}  ·  {c[1]}  ·  {c[2]}</span></span>'
        f'<span style="font-size:15px;width:420px">{what}</span><span style="font-size:15px;color:{SOFT};width:420px">{g}</span>'
        f'<span class="row" style="gap:10px;font-size:15px">{face(who.split()[0], 36, 44)}{who}</span></div>'
        for c, ark, what, g, who in rows)
    award = (f'<div class="row" style="gap:20px;margin-top:20px">{face("OPAL", 72, 86)}<span style="display:flex;flex-direction:column;gap:6px">'
             f'<span class="lab">Rival Breaker</span><span style="font-size:22px">{CREW["OPAL"][0]}  ·  Dolphin</span>'
             f'<span style="font-size:15px;color:{SOFT}">Broke Saltjaw\'s hull: the last 40% of it</span></span></div>')
    return (r116.world("pause") + stack(120, 160, (f'<span class="hd" style="font-size:20px;color:{SOFT}">The end board  ·  rivals</span>'
                                                  f'<span class="hd" style="font-size:36px">Who you beat on The Glassmere Shatter</span>'
                                                  f'<div style="display:flex;flex-direction:column;gap:12px;width:1680px;margin-top:20px">{lines}</div>{award}'), 1680, 14))


def notes():
    rows = [
        ("Asked", "Shombith, 24 Nov: \"Lets make rival ark thats cooler and makes sense there can be runaway crews as well no problem both can be "
                  "there and how do i defeat theses players?\" All rivals are AI crews in Milestone 2; rivals with people in them are DEPTH.md D3."),
        ("Rival Ark", "Three escorts sail their own Ark and hold on their own lane, often to the same place. Their Ark is the same hull class as "
                      "ours, facing you, lit from inside in the threat red (a light in the world, never a ring). Its plate hangs over it: hold, hull, "
                      "each escort's state."),
        ("Beat a rival Ark", "1 Break her hull: it is mass like ours, so Destroyers break it; the hold bursts and drifts loose (Salvage's loose "
                             "crystal), the crew turns back. 2 Silence the escorts with petal hits: silenced escorts cannot wall her hull. 3 Finish the "
                             "job first: they sail home empty. They attack your Ark for your hold the same way."),
        ("Runaway crew", "Three vessels, no Ark, nothing to protect. They raid your drifts and your Ark's hold. Beat them by silencing all three "
                         "AT ONCE (a silence lasts what the hit was worth, so it is a team play): they drop what they carried as loose crystal and run. "
                         "Or out-gather them: with nothing left to take they leave."),
        ("Rules to pin (build waits on these)", "A per-crew flag: Ark or runaway. Rout = all three silenced at the same moment (runaway crews); a "
                                                "rival Ark crew does not rout, it turns back when its Ark breaks. Rival Arks sail in The Crossing and "
                                                "Aurum Banks, runaway crews in Carmine Drift and The Deep."),
        ("After the run", "The end board names each crew you met and what they lost. Beating a crew raises its grudge; at 2 it comes after you on a "
                          "later voyage. The seat that dealt the decisive blow gets Rival Breaker."),
        ("Kept", "PvP is petals only (silence). Uses what the build has: RivalCrew's Temper and fall lines, Grudge, VoyageDriftCrystal for the "
                 "spill. Rev 120 B's plate, chip and feed, at the game's text sizes."),
    ]
    html = r133.notes_page("notes: rival Arks and runaway crews, and how you beat them", rows)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


SCREENS = {
    "Chart": ("Pause: a rival Ark on its lane, a runaway crew next door", b_chart),
    "ArkMeet": ("A rival Ark arrives: their Ark, its hold and escorts", b_meet),
    "ArkOpen": ("Two escorts silenced: her hull is open", b_open),
    "ArkBreak": ("Her hull breaks: the hold bursts, the crew turns back", b_break),
    "Runaway": ("A runaway crew raids your drift", b_runaway),
    "Rout": ("All three silenced: they drop their haul and run", b_rout),
    "Fall": ("The end board: who you beat and their grudge", b_fall),
}

if __name__ == "__main__":
    for key, (title, fn) in SCREENS.items():
        (ROOT / f"R{REV}{key}.dc.html").write_text(page(f"Rev {REV} {key} - {title}", fn()), encoding="utf-8")
    (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", len(SCREENS) + 1, "boards to", ROOT)
