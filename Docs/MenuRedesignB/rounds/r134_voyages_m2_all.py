#!/usr/bin/env python3
"""Rev 134 boards: the picked Milestone 2 Voyages UI on every screen and state, and how the world reads in play
(Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-24, on Rev 133: "For the board I like B / Seats can be mixture of A and B / Pause A / Vote B /
Awards A / I think we are missing more screens", and earlier the same day: "the in game UI and world everything should
be very well defined for these to work".

So: Board B (the quay), Seats A+B (the hangar line-up, each hull with its pilot speaking under it), Pause A (the chart on
your lane), Vote B (the pilot who saw it asks), Awards A (the run on the chart, one card per seat), drawn on every screen
a run meets, in order, with the states each one has, and two world sheets. Every screen is its own board.

  Shell    Board, BoardRefuse, Seats, SeatsRefuse, Pilots, Party
  Run      Intro, Crossing, Event, Vote, VoteTie, VoteSolo, Detour, Rival, Hostile, Joins, Pause, Dock, Awards, Lost
  World    WorldEvents (the five events next door, as they read through a membrane), WorldSea (zones, cells, danger)

    python3 Docs/MenuRedesignB/rounds/r134_voyages_m2_all.py <scratch root> [rev]
"""
import importlib.util
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "134"
_spec = importlib.util.spec_from_file_location("r133", HERE / "r133_voyages_m2.py")
r133 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r133)
r120, r116, r114, r113, r100 = r133.r120, r133.r116, r133.r114, r133.r113, r133.r100

REV = int(_rev)
ROOT = r133.ROOT
W, H = 1920, 1080
INK, MUTE, SOFT, LINE, LIME, RED, RED2, SEL = r133.INK, r133.MUTE, r133.SOFT, r133.LINE, r133.LIME, r133.RED, r133.RED2, r133.SEL
HULL, ARK, G, CREW, JOB, SEATS = r133.HULL, r133.ARK, r133.G, r133.CREW, r133.JOB, r133.SEATS
VCLASS, MOOD_SAY, MOOD_WHY, DANGER, RIVAL, DOCK, ZONE = r133.VCLASS, r133.MOOD_SAY, r133.MOOD_WHY, r133.DANGER, r133.RIVAL, r133.DOCK, r133.ZONE
FIT, BONUS, PAY = r133.FIT, r133.BONUS, r133.PAY
glyph, cls_tag, mood_mark, mood_tag, rank_mark, rank_tag = r133.glyph, r133.cls_tag, r133.mood_mark, r133.mood_tag, r133.rank_mark, r133.rank_tag
danger, fit_meter, face, gems, window, page = r133.danger, r133.fit_meter, r133.face, r133.gems, r133.window, r133.page
plate, chip, toast, kc, stack, flower = r133.plate, r133.chip, r133.toast, r133.kc, r133.stack, r133.flower
i_gem, i_ark, i_flag, i_warn, i_clock = r133.i_gem, r133.i_ark, r133.i_flag, r133.i_warn, r133.i_clock
world, VESSEL, ark, gem, fauna = r133.world, r133.VESSEL, r133.ark, r133.gem, r133.fauna
VOYAGE = JOB["salvage"][1]                         # The Glassmere Shatter
FRIEND = "Mara"


def tag(word, col=INK):
    return f'<span class="cap2" style="color:{col}">{word}</span>'


# ── the shell ───────────────────────────────────────────────────────────────────────────────────────────────────────
def s_board():
    return r133.b_board()


def s_board_refuse():
    """Black Tide Rising picked: one of your pilots refuses it, said in the crew's reply and on the sail line."""
    j = JOB["bounty"]
    moods = [("TALON", "Keen", "Hunting a crew? I know where they run."), ("VOLT", "Reluctant", "Bounties pay late."),
             ("OPAL", "Refuses", "I will not hunt a crew for coin.")]
    givers = "".join(r133.b_giver(i, k, k == "bounty") for i, k in enumerate(("salvage", "escort", "bounty", "core")))
    fit = sum(1 for s in SEATS if VCLASS[s[2]] in j[7])
    mini = r133.sea_map(1260, 190, 620, 420, "40% 55%", .3,
                        r133.lane_svg([(60, 330), (240, 250), (420, 170)], w=620, h=420)
                        + r133.place(60, 330, DOCK, True, 14) + r133.place(420, 170, j[4], False, 14)
                        + r133.event_mark(300, 300, "rival", "Black Tide", "Two cells out, running")
                        + f'<span class="abs lab" style="left:20px;top:18px">On the chart</span>'
                        + f'<span class="abs" style="left:20px;bottom:18px;font-size:14px;color:{SOFT}">{j[5]} cells  ·  3-5 min  ·  {DANGER[j[6]]} sea</span>')
    ask = (f'<div class="abs" style="left:40px;top:640px;width:1180px;display:flex;flex-direction:column;gap:16px">'
           f'<span class="lab">{j[11]}  ·  {j[12]}</span><span style="font-size:24px;line-height:1.45;max-width:1100px">"{j[9]}"</span>'
           f'<div class="row" style="gap:28px;margin-top:6px"><span class="lab">Bring me</span>{"".join(cls_tag(c, 16) for c in j[7])}</div></div>')
    reply = (f'<div class="abs" style="left:1260px;top:640px;width:620px;display:flex;flex-direction:column;gap:12px">'
             f'<span class="lab">Your crew says</span>'
             + "".join(f'<div class="row" style="gap:12px">{face(cs, 36, 44)}<span style="font-size:15px;flex:1;color:{RED2 if m == "Refuses" else INK}">"{w}"</span>{mood_mark(m)}</div>'
                       for cs, m, w in moods) + '</div>')
    foot = (f'<div class="abs row" style="left:40px;right:40px;top:990px;justify-content:space-between">'
            f'{fit_meter(fit)}<span class="row" style="gap:24px"><span class="row" style="gap:8px;font-size:15px;color:{RED2}">{mood_mark("Refuses")}'
            f'{CREW["OPAL"][0]} refuses. Swap them on Your Ark.</span>{gems(j[8], 20)}'
            f'<button class="btn soft-cta" type="button" style="width:300px">{kc("A")}Take the job</button></span></div>')
    head = f'<span class="abs" style="left:40px;top:164px;font-size:14px;color:{SOFT}">Docked at {DOCK}, {ZONE}. Four givers on the quay.</span>'
    return window(1, head + givers + mini + ask + reply + foot)


def mix_seat(i, x, mood=None, quote=None, state=None):
    """Seats A+B: A's hull standing on the floor, B's pilot under it with their own words, mood and rank."""
    role, who, v, el, cs, m, verb, n = SEATS[i]
    m = mood or m
    quote = quote or MOOD_SAY[m]
    fits = VCLASS[v] in JOB["salvage"][7]
    st = state or {"You": "You", "Rook": "Ready", "AI wingmate": "AI"}[who]
    stc = {"You": SEL, "Ready": INK, "AI": MUTE, "Choosing": SOFT, "Refuses": RED2}[st]
    foot = (f'<button class="btn sm soft" type="button" style="width:100%">{kc("X")}Change your seat</button>' if who == "You" else
            f'<span style="font-size:14px;color:{MUTE}">Rook picks their own seat.</span>' if who == "Rook" else
            f'<button class="btn sm soft" type="button" style="width:100%">{kc("X")}Swap pilot</button>' if m == "Refuses" else
            '<span class="row" style="gap:10px"><button class="btn sm soft chipq" type="button" style="flex:1">Invite a friend</button>'
            '<button class="btn sm soft chipq" type="button" style="flex:1">Change wingmate</button></span>')
    return (f'<div class="abs" style="left:{x}px;top:250px;width:520px;height:700px">'
            f'<span aria-hidden="true" class="abs" style="left:40px;right:40px;top:230px;height:60px;border-radius:50%;'
            f'background:radial-gradient(ellipse,rgba(150,172,252,.22),rgba(150,172,252,0) 70%)"></span>'
            f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:40px;width:440px;top:40px;height:230px;object-fit:contain">'
            f'<div class="abs" style="left:0;right:0;top:0;display:flex;justify-content:space-between"><span class="cap2">{role}  ·  {who}</span>'
            f'<span class="cap2" style="color:{stc}">{st}</span></div>'
            f'<div class="abs row" style="left:0;right:0;top:300px;gap:14px"><span style="font-size:24px">{v}</span>'
            f'{cls_tag(VCLASS[v], 15, INK if fits else MUTE)}<span style="margin-left:auto">{flower(el, 30)}</span></div>'
            f'<div class="abs" style="left:0;right:0;top:350px;height:1px;background:{SEL if who == "You" else "rgba(67,76,137,.6)"}"></div>'
            f'<div class="abs row" style="left:0;right:0;top:370px;gap:16px;align-items:flex-start">{face(cs, 84, 100, .45 if m == "Refuses" else 1)}'
            f'<span style="display:flex;flex-direction:column;gap:8px;flex:1"><span style="font-size:18px">{CREW[cs][0]}</span>'
            f'<span style="font-size:16px;line-height:1.4;color:{RED2 if m == "Refuses" else INK}">"{quote}"</span></span></div>'
            f'<div class="abs row" style="left:0;right:0;top:490px;gap:28px">{mood_tag(m)}{rank_tag(verb, n)}</div>'
            f'<span class="abs" style="left:0;top:524px;font-size:14px;color:{SOFT}">{MOOD_WHY[m]}</span>'
            f'<div class="abs" style="left:0;right:0;top:580px">{foot}</div></div>')


def seats_top():
    j = JOB["salvage"]
    return (f'<div class="abs row" style="left:40px;right:40px;top:176px;justify-content:space-between">'
            f'<span class="row" style="gap:16px">{face(j[3], 44, 52)}<span style="display:flex;flex-direction:column;gap:6px">'
            f'<span style="font-size:18px">{j[1]}</span><span class="row" style="gap:16px;font-size:14px;color:{SOFT}">{danger(j[6])}'
            f'<span>{j[5]} cells  ·  for {j[11]}</span></span></span></span>'
            f'<span class="row" style="gap:20px"><span class="lab">Wanted</span>{"".join(cls_tag(c, 15) for c in j[7])}</span></div>')


FLOOR = ('<div class="abs" style="left:0;right:0;top:500px;height:340px;background:linear-gradient(180deg,rgba(117,143,251,0),rgba(117,143,251,.06));'
         'box-shadow:inset 0 1px 0 rgba(67,76,137,.4)"></div>')


def sail_bar(blocked=""):
    right = (f'<span class="row" style="gap:10px;font-size:15px;color:{RED2}">{mood_mark("Refuses")}{blocked}</span>'
             f'<button class="btn def" type="button" disabled style="width:300px;opacity:.5">{kc("A")}Set sail</button>' if blocked else
             f'<span style="font-size:16px;color:#B8C0F0">Pay {PAY}</span>'
             f'<button class="btn soft-cta" type="button" style="width:300px">{kc("A")}Set sail</button>')
    return (f'<div class="abs row" style="left:40px;right:40px;top:990px;justify-content:space-between">{fit_meter()}'
            f'<span class="row" style="gap:24px">{right}</span></div>')


def s_seats():
    return window(3, seats_top() + FLOOR + "".join(mix_seat(i, 120 + i * 580) for i in range(3)) + sail_bar())


def s_seats_refuse():
    body = (mix_seat(0, 120) + mix_seat(1, 700)
            + mix_seat(2, 1280, "Refuses", "I will not pick over the dead.", "Refuses").replace(f'src="{CREW["OPAL"][2]}"', f'src="{CREW["NEEDLE"][2]}"')
            .replace(CREW["OPAL"][0], CREW["NEEDLE"][0]))
    return window(3, seats_top() + FLOOR + body + sail_bar(f"{CREW['NEEDLE'][0]} will not sail this one"))


def s_pilots():
    """The pilot picker over Your Ark: each pilot's mood for THIS job and rank in its verb; who refuses is listed, dimmed."""
    rows = [("OPAL", "Steady", 1, "Pay is pay"), ("TALON", "Keen", 3, "Raiders love salvage"), ("HORNET", "Keen", 2, "Raiders love salvage"),
            ("FLUME", "Steady", 1, "Pay is pay"), ("IVY", "Reluctant", 0, "Not their kind of work"), ("NEEDLE", "Refuses", 1, "Against their ethos")]
    lst = "".join(
        f'<button class="pick row" type="button" aria-pressed="{str(i == 0).lower()}" style="width:640px;height:84px;gap:16px;padding:0 16px;'
        f'{"box-shadow:inset 2px 0 0 " + SEL + ";background:rgba(0,212,255,.06)" if i == 0 else "background:rgba(0,4,31,.55)"}">'
        f'{face(cs, 52, 62, .45 if m == "Refuses" else 1)}<span style="display:flex;flex-direction:column;gap:5px;flex:1">'
        f'<span style="font-size:17px;color:{MUTE if m == "Refuses" else INK}">{CREW[cs][0]}</span><span style="font-size:13px;color:{MUTE}">{CREW[cs][1]}  ·  {why}</span></span>'
        f'<span style="width:130px">{mood_tag(m, 14)}</span><span style="width:130px">{rank_tag("Stow", max(r, 1), 14) if r else tag("New", MUTE)}</span></button>'
        for i, (cs, m, r, why) in enumerate(rows))
    cs = "OPAL"
    preview = (f'<div class="abs" style="left:1100px;top:180px;width:640px;height:760px;overflow:hidden;background:#00010A">'
               f'<img src="{CREW[cs][3]}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:50% 20%">'
               f'<div class="abs" style="left:0;right:0;bottom:0;height:360px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.96) 45%)"></div>'
               f'<div class="abs" style="left:30px;right:30px;bottom:30px;display:flex;flex-direction:column;gap:12px">'
               f'<span style="font-size:24px">{CREW[cs][0]}</span><span style="font-size:15px;color:{SOFT}">{CREW[cs][1]}</span>'
               f'<span style="font-size:17px;line-height:1.45">"{MOOD_SAY["Steady"]}"</span>'
               f'<div class="row" style="gap:24px">{mood_tag("Steady")}{rank_tag("Stow", 1)}</div>'
               f'<span style="font-size:14px;color:{SOFT};line-height:1.5">Flown by the AI: a Steady pilot plays the seat at its own rung. '
               f'Keen or Veteran plays one rung up, Reluctant one down.</span></div></div>')
    panel = (f'<div class="abs" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,1,10,.78)"></div>'
             f'<div class="abs" style="left:120px;top:150px;display:flex;flex-direction:column;gap:12px">'
             f'<span class="lab">Seat 3  ·  pilot for {VOYAGE}</span><span style="font-size:24px;margin-bottom:8px">Who flies the Dolphin</span>{lst}'
             f'<div class="row" style="gap:16px;margin-top:14px"><button class="btn soft-cta" type="button" style="width:260px">{kc("A")}Take the seat</button>'
             f'<button class="btn sm soft" type="button" style="width:180px">{kc("B")}Back</button></div></div>')
    return window(3, panel + preview)


def s_party():
    body = (mix_seat(0, 120) + mix_seat(1, 700, state="Choosing").replace("Rook picks their own seat.", "Rook is choosing a seat.")
            + mix_seat(2, 1280))
    bar = (f'<div class="abs row" style="left:40px;right:40px;top:990px;justify-content:space-between">{fit_meter()}'
           f'<span class="row" style="gap:24px"><span style="font-size:16px;color:{SOFT}">Waiting for Rook  ·  you sail when every friend is ready</span>'
           f'<button class="btn def" type="button" disabled style="width:300px;opacity:.5">{kc("A")}Set sail</button></span></div>')
    return window(3, seats_top() + FLOOR + body + bar)


# ── the run ─────────────────────────────────────────────────────────────────────────────────────────────────────────
def hud(count=14, hull=82, line="+5 to stow", mood="calm", clock="2:10", extra="", kind="raid"):
    return (world(kind) + plate(*r120.RAID_ARK, count, hull, mood=mood, line=line, goal="/ 30")
            + chip(count, 5 if count else 0, hull, mood=mood, clock=clock, extra=extra).replace(f"/{r120.GOAL}", "/30") + VESSEL)


def s_intro():
    j = JOB["salvage"]
    squad = "".join(f'<span class="row" style="gap:10px">{face(cs, 40, 48)}<span style="display:flex;flex-direction:column;gap:3px">'
                    f'<span style="font-size:16px">{v}</span><span style="font-size:13px;color:{SOFT}">{CREW[cs][0]}</span></span></span>'
                    for _, _, v, _, cs, *_ in SEATS)
    return (world("raid") + r120.letterbox()
            + stack(0, 330, (f'<span class="hd" style="font-size:20px;color:{SOFT}">{ZONE}  ·  {j[2]} for {j[11]}</span>'
                             f'<span class="hd" style="font-size:56px;line-height:1">{VOYAGE}</span>'
                             f'<span style="font-size:20px;color:{INK}">Gather 30 loose crystals and bank them in the Ark.</span>'
                             f'<span style="font-size:16px;color:{SOFT}">{DOCK} to {j[4]}  ·  {j[5]} cells  ·  {DANGER[j[6]]} sea</span>'
                             f'<div class="row" style="gap:40px;margin-top:22px">{squad}</div>'), W, 14, "center")
            + f'<div class="abs row" style="right:60px;bottom:150px;gap:10px;font-size:16px;color:{SOFT}">{kc("A")}Hold to skip</div>')


def s_crossing():
    """Through the membrane into the next cell: the membrane is lit where the Ark passes; the feed names the cell."""
    membrane = ('<span class="abs" style="left:-200px;top:-300px;width:2400px;height:1700px;border-radius:50%;'
                'box-shadow:inset 0 0 0 3px rgba(230,233,255,.35),inset 0 0 80px rgba(230,233,255,.18)"></span>')
    return (r114.ground(G["through"], "50% 50%", .12) + membrane + ark(700, 360, 520)
            + plate(960, 330, 9, 90, line="Cell 2 of 3", goal="/ 30") + chip(9, 0, 90, clock="").replace(f"/{r120.GOAL}", "/30")
            + toast("Ashwater", f"Cell 2 of 3  ·  {DANGER[2]} sea  ·  {ZONE}", "event") + VESSEL)


def event_light(side="right"):
    x = 1500 if side == "right" else -140
    return (f'<span class="abs" style="left:{x}px;top:180px;width:560px;height:640px;border-radius:50%;'
            'background:radial-gradient(ellipse,rgba(230,233,255,.24),rgba(230,233,255,0) 65%)"></span>')


def s_event():
    return hud() + event_light("left") + toast("Wreck surfacing to port", "Next door, 0:42 before it sinks. Your squad will vote.", "event")


def vote_box(votes, head, sub, foot, timer="0:03", solo=False):
    cs = SEATS[1][4] if not solo else SEATS[0][4]
    who = CREW[cs][0] + ("  ·  Rook's pilot" if not solo else "  ·  your pilot")
    faces = "" if solo else ('<div class="row" style="gap:16px">' + "".join(
        f'<span class="row" style="gap:8px;font-size:14px;color:{INK if p else MUTE}">{face(c, 26, 32, 1 if p else .5)}{w}</span>' for c, w, p in votes) + "</div>")
    return (f'<div class="abs slide-in" style="left:40px;top:640px;width:640px;display:flex;gap:18px;padding:18px;box-sizing:border-box;'
            f'background:linear-gradient(90deg,rgba(0,4,31,.9),rgba(0,4,31,.6));box-shadow:inset 2px 0 0 {INK}">'
            f'{face(cs, 84, 100)}<div style="display:flex;flex-direction:column;gap:10px;flex:1">'
            f'<span class="lab" style="color:{INK}">{who}</span>'
            f'<span style="font-size:18px;line-height:1.4">"{head}"</span><span style="font-size:15px;color:{SOFT}">{sub}</span>'
            f'<div class="row" style="gap:12px"><button class="btn sm def" type="button" style="box-shadow:inset 0 0 0 2px {INK}">{kc("◀")}Detour</button>'
            f'<button class="btn sm def" type="button">Press on{kc("▶")}</button><span class="big" style="font-size:22px;margin-left:auto">{timer}</span></div>'
            f'{faces}<span style="font-size:13px;color:{MUTE}">{foot}</span></div></div>')


VOTERS = [(SEATS[0][4], "Detour", True), (SEATS[1][4], "Detour", True), (SEATS[2][4], "Press on", False)]


def s_vote():
    return hud() + event_light("left") + vote_box(VOTERS, "Wreck coming up to port. We strip it, we sink nothing. Detour?",
                                                  "Detour one cell: 0:42 on the clock, the wreck's crystals into the hold.",
                                                  "A tie goes to the host. The AI votes with its pilot's mood.")


def s_vote_tie():
    tie = [(SEATS[0][4], "Detour  ·  host", True), (SEATS[1][4], "Press on", True), ("WINGMAN", "No vote", False)]
    box = vote_box(tie, "Wreck coming up to port. Detour?", f"One each, and {FRIEND} did not press.",
                   "Tie: the host's vote wins. Detour.", "0:00")
    return hud() + event_light("left") + box


def s_vote_solo():
    return hud() + event_light("left") + vote_box([], "Wreck to port. Your call, captain.", "Detour one cell: 0:42 on the clock.",
                                                  "Solo: one press decides. No press in 5 s: press on.", solo=True)


def s_detour():
    ext = f'<span style="width:1px;height:26px;background:{LINE}"></span>{i_flag(16, INK)}<span style="font-size:18px">Detour  0:42</span>'
    hulk = ('<span class="abs" style="left:1180px;top:520px;width:420px;height:140px;transform:rotate(-12deg);'
            'background:linear-gradient(90deg,rgba(184,192,240,.15),rgba(184,192,240,.45),rgba(184,192,240,.1));'
            'clip-path:polygon(0 40%,20% 0,85% 10%,100% 55%,80% 100%,10% 85%)"></span>')
    loose = "".join(gem(x, y, .8) for x, y in ((1240, 640), (1320, 600), (1420, 680), (1500, 620), (1380, 720)))
    return (r114.ground(G["cell"], "50% 50%", .14) + hulk + loose + ark(520, 380, 460)
            + plate(750, 370, 14, 82, line="Detour  ·  0:42", goal="/ 30") + chip(14, 0, 82, clock="2:10", extra=ext).replace(f"/{r120.GOAL}", "/30")
            + toast("Detour: the wreck", "Strip it before it sinks. The lane waits one cell over.", "event") + VESSEL)


def rival_hulls(x, y, s=1.0, red=False):
    out = []
    for k, v in enumerate(RIVAL[3]):
        out.append(f'<img class="hull" src="{HULL[v]}" alt="" style="position:absolute;left:{x + k * 150 * s:.0f}px;top:{y + (k % 2) * 60 * s:.0f}px;'
                   f'width:{180 * s:.0f}px;height:{90 * s:.0f}px;object-fit:contain;transform:scaleX(-1)">')
        if red:
            out.append(f'<span class="abs" style="left:{x + k * 150 * s - 20:.0f}px;top:{y + (k % 2) * 60 * s - 30:.0f}px;width:{220 * s:.0f}px;'
                       f'height:{150 * s:.0f}px;border-radius:50%;background:radial-gradient(ellipse,rgba(255,75,58,.28),rgba(255,75,58,0) 70%)"></span>')
    return "".join(out)


def s_rival():
    name = (f'<span class="abs hd" style="left:1270px;top:560px;font-size:16px;color:{RED2};text-shadow:0 0 6px #00010A">{RIVAL[0]}</span>'
            f'<span class="abs" style="left:1270px;top:584px;font-size:14px;color:{SOFT}">{RIVAL[1]}  ·  {RIVAL[2]}  ·  racing you to Glassmere</span>')
    return (hud() + rival_hulls(1260, 400, .9) + name
            + toast("The Rusted Grin are here", "Same drift, same crystals. Whatever they stow is theirs.", "threat"))


def s_hostile():
    return (hud(11, 46, "Under attack", "hot") + rival_hulls(980, 260, 1.0, True)
            + toast("The Rusted Grin want your hold", "They are breaking the Ark's mass. Protectors, hold them off.", "threat"))


def s_joins():
    return (hud() + toast(f"{FRIEND} joins at the next cell", "They take the AI's seat as the Ark crosses into Glassmere.", "good")
            + f'<div class="abs row" style="left:40px;top:180px;gap:12px;font-size:15px;color:{SOFT}">{face("WINGMAN", 30, 36)}'
              f'{FRIEND}  ·  Dolphin  ·  {CREW["OPAL"][0]}  ·  0:14 to the membrane</div>')


def s_pause():
    return r133.a_pause()


def s_dock():
    dockglow = ('<span class="abs" style="left:900px;top:120px;width:900px;height:700px;border-radius:50%;'
                'background:radial-gradient(ellipse,rgba(230,233,255,.18),rgba(230,233,255,0) 65%)"></span>')
    return (r114.ground(G["heart"], "50% 50%", .2) + dockglow + ark(760, 420, 520)
            + plate(1020, 410, 30, 76, mood="good", line="Banking at Glassmere", goal="/ 30")
            + chip(30, 0, 76, mood="good", clock="").replace(f"/{r120.GOAL}", "/30")
            + toast("Docked at Glassmere", "30 crystals banked. The voyage ends here.", "good") + VESSEL)


def s_awards():
    return r133.a_awards()


def s_lost():
    head = stack(0, 300, (f'<span class="hd" style="font-size:20px;color:{RED2}">The Ark is lost</span>'
                          f'<span class="hd" style="font-size:48px;line-height:1">{VOYAGE} ends at Ashwater</span>'
                          f'<span style="font-size:17px;color:{SOFT}">Cell 2 of 3  ·  3:10  ·  22 crystals went down with her</span>'
                          f'<div class="row" style="gap:28px;margin-top:16px;font-size:15px;color:{SOFT}">'
                          + "".join(f'<span class="row" style="gap:10px">{face(cs, 36, 44)}{mood_mark(m)}{t}</span>'
                                    for cs, m, t in (("TALON", "Steady", "Keen to Steady"), ("VOLT", "Reluctant", "Steady to Reluctant"),
                                                     ("OPAL", "Reluctant", "Reluctant")))
                          + "</div>"), W, 14, "center")
    btn = (f'<div class="abs row" style="left:0;right:0;top:640px;justify-content:center">'
           + r120.choice("A", "Back to the quay", f"Return to {DOCK}", True) + "</div>")
    return world("pause") + head + btn


# ── the world ───────────────────────────────────────────────────────────────────────────────────────────────────────
EVENTS = [
    ("Wreck surfacing", "A fallen Ark's hull glows through the membrane, cold white, slowly sinking.", "cell", "wreck"),
    ("Bloom", "The next cell lights from inside: crystals and flora thick enough to tint the membrane.", "crystal", "bloom"),
    ("Rival crew", "Three hulls in their domain's colour, flying together, named when they enter your cell.", "approach", "rival"),
    ("Distress call", "A flare rises and falls on a slow beat beside a still derelict.", "trail", "distress"),
    ("Predator pack", "Red-lit bodies gathering at the membrane, then heading for your lane.", "through", "pack"),
]


def ev_overlay(kind):
    if kind == "wreck":
        return ('<span class="abs" style="left:150px;top:120px;width:300px;height:90px;transform:rotate(-12deg);background:linear-gradient(90deg,'
                'rgba(230,233,255,.12),rgba(230,233,255,.5),rgba(230,233,255,.1));clip-path:polygon(0 40%,20% 0,85% 10%,100% 55%,80% 100%,10% 85%)"></span>')
    if kind == "bloom":
        return ('<span class="abs" style="left:60px;top:30px;width:460px;height:260px;border-radius:50%;background:radial-gradient(ellipse,'
                'rgba(230,233,255,.3),rgba(230,233,255,0) 65%)"></span>' + "".join(gem(x, y, .7) for x, y in ((200, 120), (260, 160), (320, 110), (380, 170))))
    if kind == "rival":
        return rival_hulls(120, 90, .6)
    if kind == "distress":
        return ('<span class="abs" style="left:280px;top:20px;width:6px;height:160px;background:linear-gradient(180deg,rgba(255,255,255,0),#E6E9FF)"></span>'
                '<span class="abs" style="left:250px;top:10px;width:66px;height:66px;border-radius:50%;background:radial-gradient(circle,rgba(230,233,255,.8),rgba(230,233,255,0) 70%)"></span>')
    return "".join(fauna(x, y, .6) for x, y in ((180, 140), (240, 180), (300, 130), (360, 190)))


def s_world_events():
    tiles = []
    for i, (name, how, bg, kind) in enumerate(EVENTS):
        x, y = 60 + (i % 3) * 610, 150 + (i // 3) * 450
        tiles.append(f'<div class="abs" style="left:{x}px;top:{y}px;width:580px;height:420px">'
                     f'<div style="position:relative;width:580px;height:300px;overflow:hidden;background:#00010A">'
                     f'<img src="{G[bg]}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover">'
                     f'<div class="abs" style="inset:0;background:rgba(0,1,10,.3)"></div>{ev_overlay(kind)}</div>'
                     f'<div style="display:flex;flex-direction:column;gap:8px;margin-top:14px"><span class="hd" style="font-size:18px">{name}</span>'
                     f'<span style="font-size:15px;color:{SOFT};line-height:1.45">{how}</span></div></div>')
    key = (f'<div class="abs" style="left:1280px;top:600px;width:580px;display:flex;flex-direction:column;gap:14px">'
           f'<span class="hd" style="font-size:18px">Every event, three ways at once</span>'
           f'<span style="font-size:15px;color:{SOFT};line-height:1.5">In the world, lit through the neighbouring membrane (never a ring on the '
           f'screen). In the feed under the chip, one line when it lights. On the pause chart, a mark with its timer. A rival is the only one in the '
           f'threat red; the rest are the sea\'s own white light.</span></div>')
    return (f'<span class="abs hd" style="left:60px;top:60px;font-size:32px">How the events next door read in the world</span>'
            + "".join(tiles) + key)


def s_world_sea():
    zones = [("Celadon Reach", "Jade home  ·  Wardens", 1, "Calm, wide cells, open water"), ("Aurum Banks", "Gold home  ·  Traders", 1, "Shallow, crystal-rich, busy lanes"),
             ("Carmine Drift", "Ruby home  ·  Raiders", 2, "Tight cells, predators, wrecks"), ("The Crossing", "Contested  ·  everyone", 3, "Rival crews often, bounties"),
             ("The Deep", "Uncharted  ·  explorers", 4, "Dark cells, the kaiju's wake")]
    rows = "".join(f'<div class="row" style="gap:24px;height:72px;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)">'
                   f'<span style="width:220px;font-size:18px">{n}</span><span style="width:260px;font-size:15px;color:{SOFT}">{who}</span>'
                   f'<span style="width:220px">{danger(d, 70)}</span><span style="font-size:15px;color:{SOFT}">{feel}</span></div>' for n, who, d, feel in zones)
    left = (f'<div class="abs" style="left:60px;top:150px;width:1060px;display:flex;flex-direction:column">'
            f'<span class="lab" style="margin-bottom:10px">Zones: who you meet and how dangerous the sea is</span>{rows}</div>')
    cells = (r133.cell(1300, 330, 70, "behind", "Behind") + r133.cell(1500, 330, 80, "now", "Now") + r133.cell(1710, 330, 70, "ahead", "Ahead")
             + r133.cell(1500, 520, 60, "event", "Next door") + r133.ark_dot(1500, 330, 30))
    right = (f'<div class="abs" style="left:1220px;top:150px;width:640px;display:flex;flex-direction:column;gap:8px">'
             f'<span class="lab">A lane, as the chart draws it</span></div>' + cells
             + f'<div class="abs" style="left:1220px;top:640px;width:640px;display:flex;flex-direction:column;gap:12px;font-size:15px;color:{SOFT};line-height:1.5">'
               f'<span>A run crosses one to six real cells of the map, in order. Danger is the sea\'s, set by the zone and the rung (I1-I4), never the clock.</span>'
               f'<span>Inside a cell the danger shows as what lives there: more and redder fauna, raiders, a darker sea. Crossing a membrane names the cell in the feed.</span>'
               f'<span>Arriving docks the Ark at a real place, which is where the next run starts.</span></div>')
    return f'<span class="abs hd" style="left:60px;top:60px;font-size:32px">How the sea reads: zones, cells, danger</span>' + left + right


# ── notes ───────────────────────────────────────────────────────────────────────────────────────────────────────────
def notes():
    rows = [
        ("Picked on Rev 133", "Board B, Seats A + B, Pause A, Vote B, Awards A (Shombith, 24 Nov). This round draws that mix on every screen a run "
                              "meets, in order, with its states, and adds what was missing."),
        ("Added screens", "A pilot who refuses the job (on the board and on Your Ark, Set sail held until you swap), the pilot picker with each "
                          "pilot's mood for this job and rank, a friend still choosing, the intro card, crossing a membrane, an event lighting next door, "
                          "the vote in party, tie and solo, the detour, a rival racing and a rival attacking, a friend joining at the next cell, docking "
                          "and the Ark lost."),
        ("The world", "Two sheets: the five events next door as they read through a membrane, and the zones, cells and danger. Each event shows "
                      "in the world, in the feed and on the pause chart; only a rival wears the threat red."),
        ("Seats A + B", "A's hull standing on the floor, B's pilot under it with their own words, mood and rank, so the vessel and the person read together."),
        ("Vote rules drawn", "Five seconds on the D-pad; a tie goes to the host; the AI votes with its pilot's mood; solo, one press decides and no press presses on."),
        ("Kept", "Rev 120 B (plate over the Ark, chip, feed) at the game's text sizes, one layout for every device; the Codex's voyage names; "
                 "Lime only on the call to action."),
    ]
    html = r133.notes_page("notes: the picked mix on every screen", rows)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


SCREENS = {
    "Board": ("The quay: the givers ask, your crew answers", s_board),
    "BoardRefuse": ("A pilot refuses the job", s_board_refuse),
    "Seats": ("Your Ark: A's line-up, B's pilots speaking", s_seats),
    "SeatsRefuse": ("A refusing pilot holds Set sail", s_seats_refuse),
    "Pilots": ("The pilot picker: mood for this job, rank", s_pilots),
    "Party": ("A friend still choosing", s_party),
    "Intro": ("The intro card: the voyage's name", s_intro),
    "Crossing": ("Through a membrane: the cell named", s_crossing),
    "Event": ("An event lights next door", s_event),
    "Vote": ("The vote: the pilot who saw it asks", s_vote),
    "VoteTie": ("A tie: the host's vote wins", s_vote_tie),
    "VoteSolo": ("Solo: your call", s_vote_solo),
    "Detour": ("The detour: one cell over", s_detour),
    "Rival": ("A rival crew arrives, racing", s_rival),
    "Hostile": ("A rival attacks the Ark", s_hostile),
    "Joins": ("A friend joins at the next cell", s_joins),
    "Pause": ("Pause: the chart on your lane", s_pause),
    "Dock": ("Docking: the hold banks", s_dock),
    "Awards": ("The end board: one award per seat", s_awards),
    "Lost": ("The Ark lost", s_lost),
    "WorldEvents": ("The world: the five events next door", s_world_events),
    "WorldSea": ("The world: zones, cells, danger", s_world_sea),
}

if __name__ == "__main__":
    for key, (title, fn) in SCREENS.items():
        body = fn()
        if body.startswith("<!doctype"):    # a Rev 133 board taken as picked: retitle it
            html = re.sub(r"<title>.*?</title>", f"<title>Rev {REV} {key} - {title}</title>", body, count=1)
        else:
            html = page(f"Rev {REV} {key} - {title}", body)
        (ROOT / f"R{REV}{key}.dc.html").write_text(html, encoding="utf-8")
    (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", len(SCREENS) + 1, "boards to", ROOT)
