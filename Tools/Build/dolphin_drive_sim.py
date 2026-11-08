#!/usr/bin/env python3
"""Offline Regatta race for the Dolphin AI: ChargeBoostAIPolicySO's first drive against its re-drift-at-the-peak drive.

A MODEL, not the game. A point mass on the Regatta course generator's rings (situation_features.course_from) flies the
gate steering (approach point, mouth, commit distance, as rhino_drive_sim.py) with the Dolphin's numbers: cruise 68 u/s,
turn 110 deg/s (RotationThrottleScaler 0), speed lerp 1.5/s toward its target (the scalar model, VesselTransformer
LERP_AMOUNT). The AIPilot COMMIT LOOP owns the drift: drift when the course is within ~25 deg of the steering point
(dot >= 0.9; the course then locks on it and the speed freezes, holdSpeedWhileDrifting), let go when it is not. Holding
the drift banks charge (1 / 3.636 s); letting go discharges it (drains at 0.4/s, target = cruise x m(meter) x m(at
release), m = 1 + 1.259 x meter: DOLPHIN_ENERGY_ECONOMY.md §2).

The Python drivers MIRROR ChargeBoostAIPolicySO.Driver.Tick (old = before 2026-10-17): change both together.

THE NOSE LAG AND THE AI NOSE LEAD (AIPilot.NoseLeadGain, PR #1786; Tools/Build/ai_nose_lead.py is the one shared piece)
are two knobs on race(), both OFF by default, so every number above and every caller (race_models.py "dolphin_drive",
the regatta-course jobs) races what it raced before:
- nose_rate=None is today's model: ONE heading is the nose, the stick's command and the course at once, turned straight
  at the aim by rotate_toward at TURN x min(1, error / 20) deg/s. That lag-free default is the measurement-calibrated
  model (the drive's numbers were fitted on it, DOLPHIN_ENERGY_ECONOMY.md section 2): the game's steering with its
  defect taken out. A number is the rate the NOSE follows the stick's COMMAND at (1.5 = the fleet's
  VesselTransformer.LERP_AMOUNT, every AI hull): the stick turns the command by the same law, judged from the nose, the
  nose follows the command first-order (fwd slerps toward cmd by min(1, nose_rate x dt) a frame, regatta_line_sim's
  turn_with_lag shape) and the hull flies along the nose. That lagged form is the game's steering defect made visible
  (redline_fit.py records the no-lag Manta model about 3x faster than the AI Mantas in the editor): judged from the
  lagging nose the pilot winds the command past the aim, and the nose overshoots the mouth. 0, or a rate that closes the
  gap in one step, is no lag. LERP stays the SPEED lerp.
- nose_lead=None is off; a gain, a {"gain", "max_extra_deg"} dict or a NoseLead (NoseLead.from_config) switches the lead
  on ON TOP of the lag - it is 0 on a lag-free nose, where there is nothing to lead. The effective gain is
  AIPilot.EffectiveNoseLeadGain computed ONCE a race from nose_rate (max(1, gain x 1.5 / rate)); each frame outside a
  drift the pilot judges its error from the COMMAND and turns the command onto the nose turned toward the aim by that
  gain times its error, past it (at most max_extra_deg, 40, past it: ai_nose_lead.judge_and_want, AIPilot's
  NoseLeadDirection); the stick law, the command's turn and the Driver (which reads the real nose, as the game's does)
  are untouched; off while drifting, as the game's is.
MEASURED 2026-11-07 (the new drive, (s, rings, top) and the mouths missed, a crossing of a ring's plane beside the mouth;
the self-test holds the claims, not the digits): the lag at 1.5/s alone is slower at every intensity, I1 54.0 -> 82.5 s,
I2 64.6 -> 112.9, I3 73.1 -> 149.9 (4 missed mouths), I4 71.9 -> 198.6 (26); the 2.5 lead on top recovers I1 75.4, I2
101.9, I3 117.2 (0 missed), I4 109.2 (0). The control, gain 1 (judged from the command with no pull past the aim;
max_extra_deg 0 is the same race), is I4 199.5 s / 22 missed: the pull past the aim is what helps. The old drive: I4
178.1 s / 18 missed with the lag, 135.8 / 6 with the lead.

    python3 Tools/Build/dolphin_drive_sim.py                  # old vs new on I1-I4 (the lag-free model)
    python3 Tools/Build/dolphin_drive_sim.py --nose-rate 1.5  # + the same races with the game's nose lag (the defect)
    python3 Tools/Build/dolphin_drive_sim.py --nose-lead 2.5  # + the lag (1.5/s unless --nose-rate R) and the lead on top
    python3 Tools/Build/dolphin_drive_sim.py --straight       # the 1D straight economy only
    python3 Tools/Build/dolphin_drive_sim.py --self-test
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from ai_nose_lead import NoseLead, NOSE_FOLLOW, judge_and_want   # the AI nose lead (AIPilot.NoseLeadGain), one shared piece

CRUISE, MAXM, FILL, DRAIN, LERP, TURN = 68.0, 2.259, 1 / 3.636, 0.4, 1.5, 110.0
ALIGN, RUNWAY, SPEND, MAX_HOLD, GRACE = 12.0, 2.5, 0.95, 4.0, 0.25
PEAK_MARGIN, MIN_DIS, MIN_GAIN = 3.0, 0.2, 30.0


def m(x): return 1 + (MAXM - 1) * x


class Driver:
    def __init__(s, new): s.new, s.hold, s.req, s.at, s.was = new, False, False, -1e9, False

    def tick(s, t, c, ship):
        dis = ship.discharging
        if s.new and dis and not s.was and not s.hold:
            s.hold, s.at = True, t
        s.was = dis
        if s.hold:
            since = t - s.at
            peaked = s.new and dis and since >= MIN_DIS and ship.speed >= ship.target() - PEAK_MARGIN
            if peaked or (not dis and since > GRACE) or since > MAX_HOLD:
                s.hold = False
            return
        if not ship.drifting: return
        if c['angle'] > ALIGN or c['nose'] > ALIGN: return
        if c['dist'] / max(ship.speed, 1) < RUNWAY: return
        if ship.meter < SPEND: return
        if s.new and CRUISE * m(ship.meter) ** 2 - ship.speed < MIN_GAIN: return
        s.req, s.hold, s.at = True, True, t


class Ship:
    def __init__(s, pos, fwd):
        s.pos, s.fwd, s.course, s.speed = pos, fwd, fwd, CRUISE
        s.meter, s.pinned, s.drifting, s.discharging = 0.0, 1.0, False, False

    def target(s):
        return CRUISE * (m(s.meter) * s.pinned if s.discharging else 1.0)

    def drift(s):
        s.drifting, s.discharging = True, False

    def release(s):
        s.drifting = False
        s.discharging = s.meter > 0
        s.course = s.fwd

    def step(s, dt):
        if s.drifting:
            s.meter = min(1.0, s.meter + FILL * dt)
            s.pinned = m(s.meter)
        else:
            if s.discharging:
                s.meter = max(0.0, s.meter - DRAIN * dt)
                if s.meter <= 0: s.discharging = False
            s.speed += (s.target() - s.speed) * LERP * dt


THROUGH_CENTRE = True   # GateRaceController in Regatta: the through point lies on the line through the ring's centre


def race(new, intensity, T=300, dt=1 / 60, nose_rate=None, nose_lead=None, stats=None):
    """(seconds for 3 laps or None = stalled, rings threaded, top speed). `nose_rate`: None / 0 = today's lag-free
    heading, a number = the rate the nose follows the stick's command at (1.5 = the fleet lag). `nose_lead`: the AI nose
    lead on top of that lag (ai_nose_lead.NoseLead.from_config: None = off, a gain, {"gain", "max_extra_deg"}, a
    NoseLead). `stats`: a dict to fill with the mouths missed (plane crossings beside the mouth, within 1500 u)."""
    lead_knob = NoseLead.from_config(nose_lead)
    # the nose lag: None, 0 or a rate that closes the whole gap in one step is today's model (the nose IS the command)
    follow = float(nose_rate) if nose_rate is not None and nose_rate > 0 and not math.isinf(nose_rate) else 0.0
    follow_step = min(1.0, follow * dt) if follow > 0 else 1.0
    lagged = follow_step < 1.0
    # AIPilot.EffectiveNoseLeadGain, ONCE a race, from this model's own nose rate: 0 on a nose with no lag
    g_lead = lead_knob.effective(follow) if lagged else 0.0
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); total = n * 3
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    start = add(P[0], mul(A[0], 1500))
    ship = Ship(start, unit(sub(P[0], start))); drv = Driver(new)
    cmd = ship.fwd   # the stick's COMMAND (VesselTransformer's accumulated rotation); without a nose lag it IS the nose
    idx, locked, side, lead, t, top, misses = 0, -1, 1, 480, 0.0, 0, 0
    while t < T and idx < total:
        g = idx % n; ring, ax = P[g], A[g]
        if idx != locked:
            locked = idx; side = 1 if dot(sub(ship.pos, ring), ax) >= 0 else -1
            lead = min(480, 0.4 * norm(sub(P[(idx - 1) % n] if idx > 0 else ship.pos, ring)))
        R = ship.speed / math.radians(TURN)
        approach = add(ring, mul(ax, side * lead))
        approaching = norm(sub(ring, ship.pos)) > min(420, lead + 0.5 * R) and norm(sub(approach, ship.pos)) > max(60, 0.5 * R)
        through = add(ring, mul(unit(sub(ring, ship.pos)), 320)) if THROUGH_CENTRE else sub(ring, mul(ax, side * 320))
        target = approach if approaching else through
        to = sub(target, ship.pos)
        c = {'angle': ang(ship.course, to), 'nose': ang(ship.fwd, to), 'dist': norm(to)}
        drv.tick(t, c, ship)
        if drv.req:
            drv.req = False
            if ship.drifting: ship.release()
        looking = dot(unit(to), unit(ship.course)) >= 0.9
        if looking and not ship.drifting and not drv.hold:
            ship.course = unit(to); ship.drift()
        elif ship.drifting and not looking:
            ship.release()
        # the nose turns toward the steering point in and out of the drift; the course follows it only outside one.
        # The stick (min(1, error / 20)) turns the COMMAND at TURN deg/s; with a nose lag the nose follows the command
        # first-order and the hull flies along the nose, else the nose IS the command (today's model, the same numbers).
        # With the lead (AIPilot.TryNoseLead; off while drifting) the error is judged from the command and the command
        # is turned onto the nose turned toward the aim by the effective gain times its error, past it (judge_and_want).
        judge, want = judge_and_want(ship.fwd, cmd, to, 0.0 if ship.drifting else g_lead, lead_knob.max_extra_deg)
        s_ = min(1.0, ang(judge, want) / 20.0)
        cmd = rotate_toward(cmd, want, math.radians(TURN * s_) * dt)
        ship.fwd = rotate_toward(ship.fwd, cmd, math.radians(ang(ship.fwd, cmd)) * follow_step) if lagged else cmd
        if not ship.drifting: ship.course = ship.fwd
        ship.step(dt)
        top = max(top, ship.speed)
        newpos = add(ship.pos, mul(ship.course, ship.speed * dt))
        a0, a1 = dot(sub(ship.pos, ring), ax), dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1)
            hit = norm(sub(add(ship.pos, mul(sub(newpos, ship.pos), k)), ring))
            if hit <= gates[g]['r']: idx += 1
            elif hit < 1500: misses += 1   # the mouth missed: the plane crossed beside it (regatta_line_sim's rule)
        ship.pos = newpos; t += dt
    if stats is not None: stats['misses'] = misses
    return (round(t, 1) if idx >= total else None), idx, round(top)


def straight(new, T=60, dt=0.01):
    """A straight with no corners: average speed when the drive alone decides."""
    ship = Ship([0, 0, 0], [0, 0, 1]); drv = Driver(new); t = dist = 0.0
    ship.drift()
    while t < T:
        c = {'angle': 0.0, 'nose': 0.0, 'dist': 1e9}
        drv.tick(t, c, ship)
        if drv.req:
            drv.req = False; ship.release()
        if not ship.drifting and not drv.hold: ship.drift()
        ship.step(dt); dist += ship.speed * dt; t += dt
    return round(dist / T)


# Today's numbers, the lag-free model (2026-11-07): (drive is new, intensity) -> (s for 3 laps, rings, top u/s), and the
# straight's average u/s (old, new). The self-test holds the knobs' default to them to the digit, so the lag and the lead
# cannot move a number nobody switched on (race_models.py and the regatta-course jobs race this default).
TODAY = {(False, 1): (56.9, 24, 339), (True, 1): (54.0, 24, 337), (False, 2): (66.0, 24, 335), (True, 2): (64.6, 24, 337),
         (False, 3): (80.8, 24, 332), (True, 3): (73.1, 24, 333), (False, 4): (74.0, 24, 330), (True, 4): (71.9, 24, 334)}
TODAY_STRAIGHT = (202, 299)


def _timed(new, I, **kw):
    """(race result, mouths missed) for one race."""
    st = {}
    return race(new, I, stats=st, **kw), st['misses']


def self_test():
    bad = []
    # 1. the knobs are OFF by default: unset is today's numbers (TODAY) and the straight to the digit, and so are
    #    NoseLead() (gain 0), nose_rate 0, a lead on a lag-free nose (nothing to lead: effective gain 0) and a lead on a
    #    rate that closes the whole gap in one step (60/s at dt 1/60)
    got = (straight(False), straight(True))
    if got != TODAY_STRAIGHT: bad.append(f"the straight drifted from {TODAY_STRAIGHT}: {got}")
    for (new, I), want in TODAY.items():
        got = race(new, I)
        if got != want: bad.append(f"I{I} {'new' if new else 'old'} with the knobs unset: {got} is not today's {want}")
        if not new: continue
        for kw in ({"nose_lead": NoseLead()}, {"nose_rate": 0}, {"nose_lead": 2.5}, {"nose_rate": 60.0, "nose_lead": 2.5}):
            if race(new, I, **kw) != want: bad.append(f"I{I} new with {kw} moved from today's {want}: {race(new, I, **kw)}")
    # 2. the game's lag (nose_rate 1.5, no lead) is the defect made visible: the new drive is slower at every intensity
    #    and misses mouths through it at I4 (26 on 2026-11-07; 4 at I3)...
    lag = {I: _timed(True, I, nose_rate=NOSE_FOLLOW) for I in (1, 2, 3, 4)}
    led = {I: _timed(True, I, nose_rate=NOSE_FOLLOW, nose_lead=2.5) for I in (1, 2, 3, 4)}
    for I in (1, 2, 3, 4):
        if not (lag[I][0][0] is None or lag[I][0][0] > TODAY[(True, I)][0]):
            bad.append(f"I{I} new with the 1.5/s nose lag is not slower than today ({lag[I]} vs {TODAY[(True, I)]})")
    if lag[4][1] < 10: bad.append(f"I4 new with the lag no longer misses mouths through it ({lag[4]}): re-measure the docstring")
    # 3. ...and the 2.5 lead on top recovers: it finishes faster than the lag at every intensity and clears I4's misses
    for I in (1, 2, 3, 4):
        if not (led[I][0][0] is not None and (lag[I][0][0] is None or led[I][0][0] < lag[I][0][0])):
            bad.append(f"I{I} new: the 2.5 lead no longer beats the lag ({led[I]} vs {lag[I]})")
    if led[4][1] > lag[4][1] // 4: bad.append(f"I4 new: the 2.5 lead no longer clears the lag's missed mouths ({led[4]} vs {lag[4]})")
    # 4. the controls: gain 1 (judged from the command with no pull past the aim) is weaker than 2.5 at I4 and is the same
    #    race as max_extra_deg 0 (the command placed ON the aim); a dict with the config key is the gain; on a 9/s nose
    #    2.5 floors to 1 (EffectiveNoseLeadGain from the race's own rate) and judging from the command still changes I2
    g1 = _timed(True, 4, nose_rate=NOSE_FOLLOW, nose_lead=1.0)
    if not (g1[0][0] is None or g1[0][0] > led[4][0][0]) or g1[1] <= led[4][1]:
        bad.append(f"I4 new: gain 1.0 (no pull past the aim) rivals the 2.5 lead ({g1} vs {led[4]})")
    if race(True, 4, nose_rate=NOSE_FOLLOW, nose_lead={"gain": 2.5, "max_extra_deg": 0}) != g1[0]:
        bad.append("max_extra_deg 0 is not gain 1.0 (the command on the aim)")
    if race(True, 4, nose_rate=NOSE_FOLLOW, nose_lead={"nose_lead": 2.5}) != led[4][0]:
        bad.append("{'nose_lead': 2.5} is not the 2.5 lead")
    quick, quick_led = race(True, 2, nose_rate=9.0), race(True, 2, nose_rate=9.0, nose_lead=2.5)
    if quick_led != race(True, 2, nose_rate=9.0, nose_lead=1.0) or quick_led == quick:
        bad.append(f"on a 9/s nose the 2.5 lead is not gain 1 judged from the command ({quick_led} vs {quick})")
    print("dolphin_drive_sim self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
    return 0 if not bad else 1


def nose_lead_arg(argv):
    """--nose-lead G[,EXTRA]: the AI nose lead a run adds (AIPilot.NoseLeadGain at the fleet lag and, after a comma, how far
    past the aim it may place the command, 40 deg unless given), or None: off, today's steering."""
    if "--nose-lead" not in argv:
        return None
    parts = argv[argv.index("--nose-lead") + 1].split(",")
    return NoseLead(float(parts[0]), float(parts[1]) if len(parts) > 1 else None)


def _row(I, **kw):
    """One extra table row at intensity I: the old and new drives, each with the mouths it missed."""
    cells = []
    for new in (False, True):
        r, missed = _timed(new, I, **kw)
        cells.append(f"{'new' if new else 'old'} {r} {missed} miss")
    return "  ".join(cells)


if __name__ == '__main__':
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    rate = float(sys.argv[sys.argv.index("--nose-rate") + 1]) if "--nose-rate" in sys.argv else None
    lead = nose_lead_arg(sys.argv)
    if lead is not None and rate is None: rate = NOSE_FOLLOW   # a lead needs a lag to lead: the fleet's 1.5/s
    print(f"straight, average u/s: old {straight(False)}  new {straight(True)}")
    if "--straight" not in sys.argv:
        print("(seconds for 3 laps or None = stalled, rings threaded, top speed)")
        if rate is not None: print("(the lagged rows add the mouths missed: crossings of a ring's plane beside the mouth)")
        for I in (1, 2, 3, 4):
            print(f"I{I}: old", race(False, I), " new", race(True, I))
            if rate is not None:
                print(f"    nose lag {rate:g}/s: " + _row(I, nose_rate=rate))
                if lead is not None:
                    print(f"    nose lag {rate:g}/s + lead {lead.gain:g} (+{lead.max_extra_deg:g} deg max): "
                          + _row(I, nose_rate=rate, nose_lead=lead))
