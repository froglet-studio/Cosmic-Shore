#!/usr/bin/env python3
"""Offline Regatta race for the Manta AI: the Soar drive (boost by how straight the stick is, MantaAnalogTurnBoostExecutor)
at its stick bands, and a corner-aware variant that also drops Soar ahead of a corner (sized by the corner after the
objective, as the Rhino's brake). Result 2026-10-17: the corner-aware variant is SLOWER on every course (the Manta's turn
rate grows with speed, 0.2 v + 30 deg/s, so its radius barely shrinks when it slows: 237 u at 720 u/s, 156 u at 180) and
it was not shipped; wider stick bands are faster (--bands): Regatta flies 0.8 / 0.85 (MantaSoarAITuning_Regatta).

A MODEL, not the game. A point mass on the Regatta course generator's rings flies the gate steering as Regatta does
(approach point, then through the ring's CENTRE; see rhino_drive_sim.py) with the Manta's numbers (Manta.prefab):
cruise 180 u/s, boost multiplier 4 (Soar 720 u/s), speed lerp 1.5/s toward the target, stick turn rate
(0.2 v + 30) deg/s (RotationThrottleScaler 0.2, the smaller of Pitch 50 / Yaw 30), Yawstery trigger yaw 60 deg/s at a full
net pull. The stick is proportional to the angle error, as rhino_drive_sim.py. No break-off and no rotation follow lag:
the editor is the measurement.

triggers() MIRRORS MantaAnalogTurnBoostExecutor.AutopilotTriggers: change both together. cap_for() exists only here.

    python3 Tools/Build/manta_drive_sim.py            # default bands vs Regatta's, and the corner-aware variant
    python3 Tools/Build/manta_drive_sim.py --bands    # the boost / pivot band grid
    python3 Tools/Build/manta_drive_sim.py --sweep    # the corner-aware variant's corner speed / margin grid
    python3 Tools/Build/manta_drive_sim.py --self-test
    python3 Tools/Build/manta_drive_sim.py --nose-rate 1.5      # + the rows with the game's nose lag (1.5/s), lead off
    python3 Tools/Build/manta_drive_sim.py --nose-lead 2.5      # + the lag rows and the AI nose lead 2.5 on top of the lag
    python3 Tools/Build/manta_drive_sim.py --bands --nose-lead 2.5   # either grid under the lag (and the lead)

NOSE LAG AND THE AI NOSE LEAD (2026-11-07; Tools/Build/ai_nose_lead.py is the one shared piece): race() takes two knobs,
both OFF by default, so every number above and every caller (race_models.py's "manta_drive", drive_fit, manta_line_sim's
reuse of the constants and triggers()) races byte for byte what it raced before.
- nose_rate=None is today's model: ONE heading is the nose, the stick's command and the velocity at once, turned straight
  at the aim by rotate_toward. That lag-free model is the one the measurements were calibrated on, and it is known to be
  optimistic: Tools/Compute/evaluators/redline_fit.py records that on Regatta's own courses this no-lag model is about 3x
  faster than the AI Mantas in the editor's recorded races. A number is the nose follow rate (1.5 = the fleet lag,
  VesselTransformer.LERP_AMOUNT, every AI hull; scarab_nose_lead_sim --fleet gives the Manta a 9/s nose, `--nose-rate 9`
  races that): the stick turns a COMMAND direction (the same rate-limited rotate_toward at (0.2 v + 30) x stick + 60 x
  net deg/s, the same err/20 stick, judged from the NOSE as the game judges it today), the nose follows the command
  first-order each frame, fwd = rotate_toward(fwd, cmd, ang(fwd, cmd) x min(1, nose_rate x dt)), and the hull flies along
  the NOSE. That lagged form is the game's steering defect made visible: judged from the nose, the pilot keeps winding the
  command on while the nose is still catching up, the command overshoots, then the nose does, and the hull crosses the
  ring's plane beside the mouth. 0, inf or a rate that closes the whole gap in one step (nose_rate x dt >= 1) is no lag:
  today's model. LERP above stays the SPEED lerp; it is not the nose rate.
- nose_lead=None is off; a gain, a {"gain", "max_extra_deg"} dict or a NoseLead (ai_nose_lead.NoseLead.from_config) is
  AIPilot.NoseLeadGain on top of the lag. The effective gain is computed ONCE a race from this race's own nose rate
  (AIPilot.EffectiveNoseLeadGain: max(1, gain x 1.5 / nose_rate), and 0 with no lag, so a lag-free race is the same race
  with the lead on or off), and each frame the pilot judges its error from the COMMAND and wants the nose turned toward
  the aim by that gain times its error, past it (at most max_extra_deg, never past 170 deg: ai_nose_lead.slerp_beyond =
  AIPilot.NoseLeadDirection); the err/20 stick, triggers() and the command's turn rate are the lines above, untouched.
  Soar is a speed multiplier here, not a steering mode, and the model has no drift, rail, shell or orbit break-off, so
  the lead is on at every step of a lagged race.
  MEASURED 2026-11-07 (`--nose-lead 2.5`; default bands / Regatta bands / corner-aware): the lag alone takes I1-I3 from
  26.5 / 32.8 / 37.5 s to 66.4 / 78.2 / 87.5 s (Regatta's bands 25.0 / 31.0 / 35.6 -> 62.1 / 72.4 / 80.4 s, corner-aware
  27.6 / 34.6 / 40.5 -> 67.6 / 78.2 / 87.6 s), 2.2-2.5x slower with no missed mouth, most of the way to redline_fit's 3x;
  at I4 (54 u mouths; the Soar's turning radius is 236 u at 709 u/s) it strands every column, 10 / 2 / 10 rings in 300 s
  with 184 / 203 / 183 crossings beside the mouth (`--sweep --nose-rate 1.5`: a 260 u/s corner speed finishes I4 in
  84.4 s, so the strand is speed at the mouth, not the course). The 2.5 lead on top of the lag recovers I1-I3 in every
  column: 45.7 / 55.5 / 64.4 s at the default bands, 42.6 / 50.7 / 64.8 s at Regatta's (6 misses at I3), 46.7 / 56.0 /
  64.9 s corner-aware, no other missed mouth. At I4 it does NOT recover the shipped drive: stranded at 1 ring with 207
  crossings at either band pair (this model has no orbit break-off, so a pilot that once misses a 54 u mouth at the Soar
  circles it for the rest of the race, and judged from the command it misses earlier than judged from the nose); the
  unshipped corner-aware variant with the lead finishes I4 in 61.0 s with 2 misses. Gain 1 (judged from the command, no
  pull past the aim) strands every intensity at the default bands (4 / 1 / 1 / 1 rings), so the pull past the aim is
  what the lagged Manta needs; max_extra_deg 0 is gain 1 (the same wanted direction); on a 9/s nose the 2.5 gain floors
  to 1 and still changes the race (I1 29.0 -> 28.5 s). The self-test holds the knobs' default to today's numbers
  (TODAY), the lag rows (LAGGED), the lead rows (LED) with the I1-I3 recovery and the I4 finding, and the controls.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from ai_nose_lead import NoseLead, judge_and_want, NOSE_FOLLOW   # the AI nose lead (AIPilot.NoseLeadGain), one shared piece

CRUISE, MULT, LERP, YAWSTERY_YAW = 180.0, 4.0, 1.5, 60.0
BOOST_BAND, YAWSTERY_BAND = 0.35, 0.75


class Tuning:
    corner, mincorner, margin, lead = 520.0, 180.0, 80.0, 0.2


def intent(stick, band):
    if band <= 0: return 0.0
    s = abs(stick)
    if s <= band: return 1.0
    return max(0.0, min(1.0, 1 - (s - band) / max(1e-4, 1 - band)))


def triggers(yaw, stick, band, yband, cap=1.0):
    hold = min(intent(stick, band), cap)
    lt = rt = hold
    y = abs(yaw)
    if y > yband:
        ex = max(0.0, min(1.0, (y - yband) / max(1e-4, 1 - yband)))
        outer = min(1.0, hold + ex)
        if yaw > 0: rt = outer
        else: lt = outer
    return lt, rt


def corner_speed(deg, t=Tuning):
    th = 90 if deg < 0 else min(179, max(1, deg))
    return max(t.mincorner, t.corner / math.tan(math.radians(th / 2)))


def brake_distance(v, vc, cruise, k):
    """Units to fall from v to vc with every trigger released: v - cruise decays as e^-kt."""
    if v <= vc or vc <= cruise: return 0.0 if v <= vc else float('inf')
    tt = math.log((v - cruise) / (vc - cruise)) / k
    return (v - vc) / k + cruise * tt


def cap_for(dist, corner_deg, v, t=Tuning):
    """The most Soar the drive may hold now: none inside the brake zone while above the corner speed, then just enough to
    hold the corner speed."""
    vc = max(corner_speed(corner_deg, t), CRUISE * 1.05)
    if dist - t.margin - v * t.lead > brake_distance(v, vc, CRUISE, LERP): return 1.0
    if v > vc + 5: return 0.0
    return max(0.0, min(1.0, (vc / CRUISE - 1) / (MULT - 1)))


def race(aware, intensity, T=300, dt=1 / 60, bands=None, nose_rate=None, nose_lead=None):
    """(seconds for 3 laps or None = stalled, rings threaded, top speed); race.misses (ring planes crossed beside the
    mouth) is set as well. nose_rate: None (today: the heading turns at once) or the nose follow rate per second (1.5 =
    the game's AI hulls); nose_lead: AIPilot.NoseLeadGain on top of the lag (None = off, a gain, {"gain", "max_extra_deg"}
    or a NoseLead). Both are OFF by default: the module docstring."""
    band, yband = bands or (BOOST_BAND, YAWSTERY_BAND)
    # the nose lag and the AI nose lead, both OFF by default: a rate of 0, inf or one that closes the whole gap in a step
    # is no lag, today's model; the effective gain is AIPilot.EffectiveNoseLeadGain from THIS race's rate, computed once
    follow = float(nose_rate) if nose_rate is not None and 0 < nose_rate < math.inf and nose_rate * dt < 1.0 else None
    lead_knob = NoseLead.from_config(nose_lead)
    g_lead = lead_knob.effective(follow) if follow is not None else 0.0
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); total = n * 3
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    pos = add(P[0], mul(A[0], 1500)); fwd = unit(sub(P[0], pos)); v = CRUISE
    cmd = list(fwd)   # the stick's command; it is the nose itself until a nose_rate splits them
    idx, locked, side, lead, t, top = 0, -1, 1, 480, 0.0, 0
    race.misses = 0
    while t < T and idx < total:
        g = idx % n; ring, ax = P[g], A[g]
        if idx != locked:
            locked = idx; side = 1 if dot(sub(pos, ring), ax) >= 0 else -1
            lead = min(480, 0.4 * norm(sub(P[(idx - 1) % n] if idx > 0 else pos, ring)))
        R = v / math.radians(0.2 * v + 30)
        approach = add(ring, mul(ax, side * lead))
        approaching = norm(sub(ring, pos)) > min(420, lead + 0.5 * R) and norm(sub(approach, pos)) > max(60, 0.5 * R)
        through = add(ring, mul(unit(sub(ring, pos)), 320))
        target, after = (approach, through) if approaching else (through, P[(idx + 1) % n] if idx + 1 < total else None)
        to = sub(target, pos)
        # the stick is proportional on the angle error, judged from the nose; with the nose lead (AIPilot.TryNoseLead) it
        # is judged from the COMMAND, wanting the nose turned toward the aim by g_lead x its error, past it
        # (AIPilot.NoseLeadDirection = ai_nose_lead.slerp_beyond); the err/20 law, the triggers and the turn rate are as they were
        judge, want = judge_and_want(fwd, cmd, to, g_lead, lead_knob.max_extra_deg) if g_lead > 0 else (fwd, to)
        err = ang(judge, want); stick = min(1.0, err / 20.0)
        corner = ang(to, sub(after, target)) if after is not None else -1
        cap = cap_for(norm(to), corner, v) if aware else 1.0
        lt, rt = triggers(stick, stick, band, yband, cap)
        boost = min(lt, rt); net = abs(rt - lt)
        v += (CRUISE * (1 + (MULT - 1) * boost) - v) * LERP * dt
        top = max(top, v)
        rate = (0.2 * v + 30) * stick + YAWSTERY_YAW * net
        if follow is None:
            fwd = rotate_toward(fwd, want, math.radians(rate) * dt)   # today: the heading IS the command
        else:
            # the game: the stick turns the COMMAND at the Manta's rate, the nose follows it first-order
            # (VesselTransformer.LERP_AMOUNT) and the hull flies along the nose
            cmd = rotate_toward(cmd, want, math.radians(rate) * dt)
            fwd = rotate_toward(fwd, cmd, math.radians(ang(fwd, cmd)) * min(1.0, follow * dt))
        newpos = add(pos, mul(fwd, v * dt))
        a0, a1 = dot(sub(pos, ring), ax), dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1)
            if norm(sub(add(pos, mul(sub(newpos, pos), k)), ring)) <= gates[g]['r']: idx += 1
            else: race.misses += 1
        pos = newpos; t += dt
    return (round(t, 1) if idx >= total else None), idx, round(top)


def _sweep_one(a):
    Tuning.corner, Tuning.margin, Tuning.lead = a[:3]
    knobs = dict(zip(("nose_rate", "nose_lead"), a[3:5]))   # the nose lag / lead, when the grid carries them
    res = [race(True, I, **knobs) for I in (1, 2, 3, 4)]
    return sum(r[0] if r[0] else 300 + (24 - r[1]) * 10 for r in res), a, [r[0] for r in res]


REGATTA_BANDS = (0.8, 0.85)   # MantaSoarAITuning_Regatta.asset


def _bands_one(b):
    knobs = dict(zip(("nose_rate", "nose_lead"), b[2:4]))   # the nose lag / lead, when the grid carries them
    res = [race(False, I, bands=b[:2], **knobs) for I in (1, 2, 3, 4)]
    return sum(r[0] if r[0] else 300 + (24 - r[1]) * 10 for r in res), b, [r[0] for r in res]


def _with_knobs(grid, nose_rate, nose_lead):
    """The grid's cells with the nose lag / lead appended when a run carries them (a lead without a rate is raced at the
    fleet lag: a lag-free nose has nothing to lead); the lead travels as its config dict so the pool can pickle it."""
    if nose_rate is None and nose_lead is None:
        return grid
    rate = NOSE_FOLLOW if nose_rate is None else nose_rate
    lead_cfg = NoseLead.from_config(nose_lead).to_config() if nose_lead is not None else None
    return [cell + (rate, lead_cfg) for cell in grid]


def _knob_rows(nose_rate, nose_lead):
    """The extra rows --nose-rate / --nose-lead add to the default table: (label, race kwargs) for the lag alone and,
    with a lead, the lead on top of it. A lead without a rate is raced at the fleet lag (a lag-free nose has nothing to
    lead)."""
    if nose_rate is None and nose_lead is None:
        return []
    rate = NOSE_FOLLOW if nose_rate is None else nose_rate
    rows = [(f"nose lag {rate:g}/s", dict(nose_rate=rate))]
    if nose_lead is not None:
        lead = NoseLead.from_config(nose_lead)
        rows.append((f"nose lag {rate:g}/s + AI nose lead {lead.gain:g} (+{lead.max_extra_deg:g} deg max, effective gain "
                     f"{lead.effective(rate):g})", dict(nose_rate=rate, nose_lead=lead)))
    return rows


def knob_args(argv):
    """--nose-rate R (the nose follow rate per second; 1.5 is the game's AI hulls) and --nose-lead G[,EXTRA] (the AI nose
    lead's gain at the fleet lag and, after a comma, how far past the aim it may place the command, 40 deg unless given),
    or None for each: off, today's model."""
    rate = float(argv[argv.index("--nose-rate") + 1]) if "--nose-rate" in argv else None
    lead = None
    if "--nose-lead" in argv:
        parts = argv[argv.index("--nose-lead") + 1].split(",")
        lead = NoseLead(float(parts[0]), float(parts[1]) if len(parts) > 1 else None)
    return rate, lead


# the default table with the knobs unset: (aware, bands) -> I1-I4 (seconds, rings, top speed), 0 missed mouths each
TODAY = {
    (False, None): ((26.5, 24, 709), (32.8, 24, 709), (37.5, 24, 709), (37.6, 24, 709)),
    (False, REGATTA_BANDS): ((25.0, 24, 709), (31.0, 24, 709), (35.6, 24, 709), (35.5, 24, 709)),
    (True, None): ((27.6, 24, 680), (34.6, 24, 680), (40.5, 24, 680), (39.8, 24, 680)),
}
# ...under the game's nose lag (nose_rate 1.5, lead off; `--nose-rate 1.5`): today's steering through the lag, the defect
# this model did not show before. (aware, bands) -> I1-I4 of ((seconds, rings, top speed), missed mouths)
LAGGED = {
    (False, None): (((66.4, 24, 709), 0), ((78.2, 24, 709), 0), ((87.5, 24, 709), 0), ((None, 10, 709), 184)),
    (False, REGATTA_BANDS): (((62.1, 24, 709), 0), ((72.4, 24, 709), 0), ((80.4, 24, 709), 0), ((None, 2, 709), 203)),
    (True, None): (((67.6, 24, 680), 0), ((78.2, 24, 680), 0), ((87.6, 24, 680), 0), ((None, 10, 680), 183)),
}
# ...and with the AI nose lead 2.5 on top of the lag (`--nose-lead 2.5`): I1-I3 recovered in every column, I4 only corner-aware
LED = {
    (False, None): (((45.7, 24, 709), 0), ((55.5, 24, 709), 0), ((64.4, 24, 709), 0), ((None, 1, 709), 207)),
    (False, REGATTA_BANDS): (((42.6, 24, 709), 0), ((50.7, 24, 709), 0), ((64.8, 24, 709), 6), ((None, 1, 709), 207)),
    (True, None): (((46.7, 24, 680), 0), ((56.0, 24, 680), 0), ((64.9, 24, 680), 0), ((61.0, 24, 680), 2)),
}


def self_test():
    bad = []

    def run(aware, I, bands=None, **kw):
        r = race(aware, I, bands=bands, **kw)
        return r, race.misses

    # 1. the nose lag and the AI nose lead (ai_nose_lead.py) are OFF by default: with the knobs unset every race of the
    # default table is today's (TODAY), with no missed mouth...
    for (aware, b), want in TODAY.items():
        for I in (1, 2, 3, 4):
            got = run(aware, I, b)
            if got != (want[I - 1], 0):
                bad.append(f"aware={aware} bands={b} I{I} with the knobs unset: {got} is not today's {want[I - 1]}")
            # ...NoseLead() (gain 0) is the unset knob, and on a lag-free nose (nose_rate None or 0: today's model) the
            # lead has nothing to lead and changes nothing
            for kw in ({"nose_lead": NoseLead()}, {"nose_lead": 2.5}, {"nose_rate": 0, "nose_lead": 2.5}):
                if run(aware, I, b, **kw) != (want[I - 1], 0):
                    bad.append(f"aware={aware} bands={b} I{I} with {kw}: not today's race {want[I - 1]}")
    for kw in ({"nose_lead": 0}, {"nose_lead": {}}, {"nose_lead": False}, {"nose_rate": -1},
               {"nose_rate": float("inf"), "nose_lead": 2.5}, {"nose_rate": 60.0, "nose_lead": 2.5}):
        if run(False, 1, **kw) != (TODAY[(False, None)][0], 0):
            bad.append(f"I1 with {kw}: not today's race")
    # 2. the game's nose lag (nose_rate 1.5), lead off, is today's steering through the lag and shows the defect: slower
    # at I1-I3 in every column and stranded at I4 (LAGGED)
    for (aware, b), want in LAGGED.items():
        for I in (1, 2, 3, 4):
            got = run(aware, I, b, nose_rate=1.5)
            if got != want[I - 1]:
                bad.append(f"aware={aware} bands={b} I{I} under the nose lag: {got} is not {want[I - 1]}")
            today = TODAY[(aware, b)][I - 1][0]
            if I < 4 and (got[0][0] is None or got[0][0] <= today):
                bad.append(f"aware={aware} bands={b} I{I}: the nose lag did not slow the race ({got} vs today's {today})")
        if want[3][0][0] is not None or want[3][1] == 0:
            bad.append(f"aware={aware} bands={b}: the nose lag no longer strands I4 ({want[3]}): re-measure the docstring")
    if run(False, 3, nose_rate=1.5, nose_lead=NoseLead()) != LAGGED[(False, None)][2]:
        bad.append("NoseLead() (gain 0) is not the unset lead under the lag")
    # 3. the AI nose lead 2.5 on top of the lag recovers I1-I3 in every column, faster than the lag alone (LED); at I4 it
    # finishes only the corner-aware variant and leaves the shipped drive stranded (the docstring: no orbit break-off here)
    for (aware, b), want in LED.items():
        for I in (1, 2, 3, 4):
            got = run(aware, I, b, nose_rate=1.5, nose_lead=2.5)
            if got != want[I - 1]:
                bad.append(f"aware={aware} bands={b} I{I} with the 2.5 lead: {got} is not {want[I - 1]}")
            lag = LAGGED[(aware, b)][I - 1]
            if I < 4 and (got[0][0] is None or got[0][0] >= lag[0][0]):
                bad.append(f"aware={aware} bands={b} I{I}: the 2.5 lead did not recover the lagged race: {got} vs the lag alone {lag}")
    if LED[(True, None)][3][0][0] is None or LED[(False, None)][3][0][0] is not None:
        bad.append("the I4 finding moved (corner-aware finishes with the lead, the shipped drive does not): re-measure the docstring")
    if run(False, 1, nose_rate=1.5, nose_lead={"nose_lead": 2.5}) != LED[(False, None)][0]:
        bad.append("{'nose_lead': 2.5} is not the 2.5 lead")
    # 4. the controls: gain 1 (judged from the command, with no pull past the aim) strands I1-I3 where the 2.5 lead
    # finishes; it IS the lead with no room past the aim (max_extra_deg 0, the same wanted direction); on a 9/s nose the
    # 2.5 gain floors to 1 (AIPilot.EffectiveNoseLeadGain from the race's own rate) and still changes that race
    for I in (1, 2, 3):
        g1 = run(False, I, nose_rate=1.5, nose_lead=1.0)
        led = LED[(False, None)][I - 1]
        if g1[0][0] is not None and g1[0][0] <= led[0][0] and g1[1] <= led[1]:
            bad.append(f"I{I}: gain 1.0 (no pull past the aim) rivals the 2.5 lead: {g1} vs {led}")
        if run(False, I, nose_rate=1.5, nose_lead=NoseLead(2.5, 0.0)) != g1:
            bad.append(f"I{I}: the lead with no room past the aim (max_extra_deg 0) is not gain 1.0")
    quick, quick_led = run(False, 1, nose_rate=9.0), run(False, 1, nose_rate=9.0, nose_lead=2.5)
    if quick_led != run(False, 1, nose_rate=9.0, nose_lead=1.0) or quick_led == quick:
        bad.append("on a 9/s nose the 2.5 lead is not gain 1 judged from the command, or it did not change the race")
    # 5. the CLI and the pool workers carry the knobs: a lead without a rate races the fleet lag; the grid cells carry the
    # lead as its config dict; a cell without knobs is today's race
    rate, lead = knob_args(["x", "--nose-rate", "1.5", "--nose-lead", "2.5,20"])
    if rate != 1.5 or lead.to_config() != {"gain": 2.5, "max_extra_deg": 20.0} or knob_args(["x"]) != (None, None):
        bad.append("knob_args")
    rows = _knob_rows(None, 2.5)
    if len(rows) != 2 or rows[0][1] != {"nose_rate": NOSE_FOLLOW} or rows[1][1]["nose_lead"].gain != 2.5 or rows[1][1]["nose_rate"] != NOSE_FOLLOW:
        bad.append("--nose-lead without --nose-rate does not race the fleet lag")
    if _with_knobs([(0.35, 0.75)], None, None) != [(0.35, 0.75)] or \
            _with_knobs([(0.35, 0.75)], None, 2.5) != [(0.35, 0.75, NOSE_FOLLOW, {"gain": 2.5, "max_extra_deg": 40.0})]:
        bad.append("_with_knobs")
    plain, led_cell = _bands_one((BOOST_BAND, YAWSTERY_BAND)), _bands_one((BOOST_BAND, YAWSTERY_BAND, 1.5, {"gain": 2.5}))
    if plain[2] != [r[0] for r in TODAY[(False, None)]] or led_cell[2] != [r[0][0] for r in LED[(False, None)]]:
        bad.append(f"the --bands worker does not race today's / the lead's races: {plain[2]} / {led_cell[2]}")
    print("manta_drive_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


COLUMNS = (("default bands", False, None), (f"Regatta bands {REGATTA_BANDS}", False, REGATTA_BANDS),
           ("corner-aware (not shipped)", True, None))   # the default table's three races: (label, aware, bands)


def _table_row(I, **kw):
    cells = []
    for name, aware, b in COLUMNS:
        r = race(aware, I, bands=b, **kw)
        cells.append(f" {name} {r} m{race.misses}")
    return f"I{I}:" + " ".join(cells)


if __name__ == '__main__':
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    nose_rate, nose_lead = knob_args(sys.argv)
    knob_cols = ", nose_rate, nose_lead" if nose_rate is not None or nose_lead is not None else ""
    if "--bands" in sys.argv:
        from concurrent.futures import ProcessPoolExecutor
        grid = [(b, y) for b in (0.2, 0.35, 0.5, 0.65, 0.8, 0.9) for y in (0.5, 0.75, 0.85, 0.95) if y >= b]
        with ProcessPoolExecutor() as ex:
            out = sorted(ex.map(_bands_one, _with_knobs(grid, nose_rate, nose_lead)))
        print(f"total s (I1-I4), (BoostStickBand, YawsteryStickBand{knob_cols}), per course")
        for o in out[:10]: print(o)
    elif "--sweep" in sys.argv:
        import itertools
        from concurrent.futures import ProcessPoolExecutor
        grid = list(itertools.product((260, 360, 440, 520, 640, 800), (0, 80, 200), (0.0, 0.2, 0.5)))
        with ProcessPoolExecutor() as ex:
            out = sorted(ex.map(_sweep_one, _with_knobs(grid, nose_rate, nose_lead)))
        print(f"total s (I1-I4), (cornerSpeed, brakeMarginUnits, brakeLeadSeconds{knob_cols}), per course")
        for o in out[:10]: print(o)
    else:
        print("(seconds for 3 laps or None = stalled, rings threaded, top speed)")
        for I in (1, 2, 3, 4):
            print(f"I{I}: default bands", race(False, I), f" Regatta bands {REGATTA_BANDS}", race(False, I, bands=REGATTA_BANDS),
                  " corner-aware (not shipped)", race(True, I))
        for label, kw in _knob_rows(nose_rate, nose_lead):
            print(f"-- {label} (m = missed mouths):")
            for I in (1, 2, 3, 4):
                print(_table_row(I, **kw))
