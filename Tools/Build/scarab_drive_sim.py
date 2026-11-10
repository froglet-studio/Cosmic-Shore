#!/usr/bin/env python3
"""Offline Regatta race for the Scarab AI: today's full-throttle drive against SnapDashAIPolicySO's corner lift.

A MODEL, not the game: a point mass on the Regatta course generator's rings (situation_features.course_from), AIPilot's
own steering law (ai_stick; --linear for the older sims' proportional stick) at the Scarab's turn rate ((v x 0.1 + 100) deg/s at full stick, Scarab.prefab), the
Scarab's integrator (ScarabVesselTransformer: +90 u/s^2 x throttle, -120 u/s^2 only with the throttle released, ceiling
216 x the Time multiplier, 324 u/s at the card's Time 10), the Time-5 Snap Dash (+100 u/s for 0.4 s, at most every 2 s
on a straight with runway) and the gate steering of rhino_drive_sim.py (through the ring's centre, as Regatta flies it).
No orbit break-off: a pilot that circles a point stalls here where the game would break off. Use it to compare drives
and pick defaults; the editor is the measurement.

The corner lift MIRRORS SnapDashAIPolicySO.Driver.Tick: change both together. It is the Rhino's brake rule
(RampBoostAIPolicySO.CornerSpeedFor / BrakePoint) on the Scarab's only brake, the released throttle.

Result (2026-10-25): the corner lift is NOT shipped. On Regatta's through-the-centre steering the full-throttle Scarab
finishes I1-I4 (49-60 s) and the best lift the sweep finds is no lift (223.9 vs 224.1 s in all). The real Scarab's lost
races (5 rings in 50-61 s, two editor races of 2026-10-23) were the race line's break-offs and cut mouths, fixed for
every hull in RegattaRaceLine.AimThroughRing / AIPilot.MarkAimAsPathLead (regatta_line_sim.py: 43.8-53.6 s, no miss).

    python3 Tools/Build/scarab_drive_sim.py            # today vs the corner lift on I1-I4
    python3 Tools/Build/scarab_drive_sim.py --sweep    # the corner-speed / margin / lead grid
    python3 Tools/Build/scarab_drive_sim.py --nose-rate 1.5   # + the rows with the game's nose lag (1.5/s), lead off
    python3 Tools/Build/scarab_drive_sim.py --nose-lead 2.5   # + the lag rows and the AI nose lead 2.5 on top of the lag
    python3 Tools/Build/scarab_drive_sim.py --self-test

NOSE LAG AND THE AI NOSE LEAD (2026-11-07; Tools/Build/ai_nose_lead.py is the one shared piece): race() takes two knobs,
both OFF by default, so every number above and every caller (the nine models that import ai_stick from here, the
evaluators that hash this file, --sweep) races byte for byte what it raced before.
- nose_rate=None is today's model: ONE heading is the nose, the stick's command and the velocity at once, turned straight
  at the aim by rotate_toward. That lag-free model is the one the measurements were calibrated on, and it is known to be
  optimistic (Tools/Compute/evaluators/redline_fit.py records that the no-lag Manta model raced about 3x faster than the
  AI Mantas in the editor's recorded races). A number is the nose follow rate (1.5 = VesselTransformer.LERP_AMOUNT, every
  AI hull): the stick turns a COMMAND direction (the same rate-limited rotate_toward, the same ai_stick law, judged from
  the NOSE as the game judges it today), the nose follows the command first-order each frame,
  fwd = rotate_toward(fwd, cmd, ang(fwd, cmd) x min(1, nose_rate x dt)), and the hull flies along the NOSE. That lagged
  form is the game's steering defect made visible: judged from the nose, the pilot keeps the command wound on while the
  nose is still catching up, the nose runs 20 deg behind an aim whose bearing moves as the hull closes, and the hull
  crosses the ring's plane beside the mouth. 0, inf or a rate that closes the whole gap in one step (nose_rate x dt >= 1)
  is no lag: today's model. race.misses counts the crossings of a ring's plane outside its mouth
  (RaceGateRing.PlaneCrossing's OutsideMouth) so the tables can show them without a fifth number in the tuple; about 200
  of them is an orbit round a missed ring: this model has no break-off (above), so at full throttle one missed mouth is
  a stall.
- nose_lead=None is off; a gain, a {"gain", "max_extra_deg"} dict or a NoseLead (ai_nose_lead.NoseLead.from_config) is
  AIPilot.NoseLeadGain on top of the lag. The effective gain is computed ONCE a race from this race's own nose rate
  (AIPilot.EffectiveNoseLeadGain: max(1, gain x 1.5 / nose_rate), and 0 with no lag, so a lag-free race is the same
  race with the lead on or off), and each frame the pilot judges its error from the COMMAND and wants the nose turned
  toward the aim by that gain times its error, past it (at most max_extra_deg, never past 170 deg:
  ai_nose_lead.slerp_beyond = AIPilot.NoseLeadDirection); the ai_stick law, the Snap Dash, the integrator and the command
  update are the lines above, untouched (the command stops AT the lead point, rotate_toward's cap; the game's Pitch / Yaw
  carry on past it). The lead is on everywhere: this model has no drift, rail, shell or break-off mode to switch it off
  in, and the Snap Dash steers by the nose. The corner lift still reads the NOSE's angle to the objective and the stick
  the pilot outputs, as SnapDashAIPolicySO.Driver does.
  MEASURED 2026-11-07 (`--nose-lead 2.5`): the lag alone takes full throttle from 49.2 / 54.7 / 59.1 / 59.2 s (I1-I4) to
  55.2 s at I1 and a stall at I2 / I3 / I4 (8 / 1 / 2 rings, then 201 / 214 / 212 plane crossings: an orbit), and the
  corner lift from 49.5 / 56.6 / 62.3 / 62.4 s to 57.6 / 73.0 / 88.1 / 108.0 s with 0 / 0 / 0 / 12 missed mouths: in this
  model, under the game's lag, the lift retired above finishes where flat out stalls (a brake is what a lagging nose
  needs; whether that changes the Scarab's drive is its owner's call, measured in the editor). The 2.5 lead on top of the
  lag does NOT recover this model's full-throttle races: it stalls all four, I1 included (3 rings, 209 crossings), and on
  the corner lift it recovers only I4 (76.6 s, no miss) while costing I1-I3 (68.5 / 79.3 / 91.1 s, 5 misses at I3). Why:
  this is the GATE steering, whose aim JUMPS from the approach point to the through point at the commit distance (420 u
  out, with the nose still 20-30 deg off at 324 u/s); the lead has parked the command up to 40 deg past the old aim (an
  effective gain of 2.5 on a 60 deg error), so at the jump the command is 100-120 deg from where it is now wanted and
  takes 0.8 s at (0.1 v + 100) deg/s to come back, the nose follows the stale command through the ring's plane 158 u from
  a 110 u mouth, and without a break-off that one miss is the race. A milder pull fits this steering better: gain 1.5
  finishes I1 in 55.5 s and I2 in 71.0 s (4 misses) where the lag alone stalls I2; gain 1 (judging from the command
  without the pull; the same race as max_extra_deg 0) is weaker everywhere, 160.9 s and 34 misses on the I4 lift. The
  race-line models (regatta_line_sim.py, scarab_nose_lead_sim.py), whose aim slides along a line instead of jumping, are
  where the 2.5 lead clears the Scarab's misses; the step response of these two lines alone (a fixed aim) settles the
  nose within 2 deg in 0.48 / 0.77 / 1.12 s for 10 / 30 / 60 deg against 1.12 / 1.88 / 2.47 s with the lag alone, so the
  loop is the game's and the loss is the gate steering's aim jump meeting a command parked past it. The self-test holds
  the knobs' default to today's numbers (TODAY, TODAY_LINEAR), the lag rows (LAGGED), the lead rows (LED), the I4 lift
  claim with its gain-1 control, and the two findings a port must re-measure.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
from ai_nose_lead import NoseLead, judge_and_want, NOSE_FOLLOW   # the AI nose lead (AIPilot.NoseLeadGain), one shared piece
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward

ACC, COAST, CEILING, DEADZONE = 90.0, 120.0, 216.0 * 1.5, 0.05
DASH_SPEED, DASH_SECONDS, DASH_COOLDOWN = 100.0, 0.4, 2.0


def omega(v): return v * 0.1 + 100.0   # deg/s at full stick


def ai_stick(err_deg):
    """AIPilot's steering law at racing range (the denominator is min(d^2, _maxDistance = 50), so 50 past 7 u):
    asin(clamp(sin^2(err) x aggressiveness 100 / 50)) in degrees, times the axis's share of the cross product, clamped.
    Roughly cubic: 0.08 stick at 5 deg off, 0.6 at 10, full from about 16."""
    e = math.radians(min(err_deg, 90.0))
    a = math.degrees(math.asin(min(1.0, math.sin(e) ** 2 * 100.0 / 50.0)))
    return min(1.0, a * math.sin(e))


class Policy:  # base AIBoostPolicySO defaults + the Snap Dash fields + the corner lift
    align, ralign, stick, rstick, erun, rrun = 12, 25, 0.35, 0.6, 1.5, 0.5
    corner, mincorner, margin, lead, turn_lift = 150.0, 60.0, 40.0, 0.2, 45.0

    def is_straight(s, c): return c['angle'] <= s.align and c['stick'] <= s.stick
    def runway(s, c): return c['dist'] / max(c['speed'], 1) >= s.erun

    def corner_speed(s, deg):   # RampBoostAIPolicySO.CornerSpeedFor
        th = 90 if deg < 0 else min(179, max(1, deg))
        return max(s.mincorner, s.corner / math.tan(math.radians(th / 2)))

    def wanted(s, c):
        """The speed to be at now: the corner ahead once its brake point is reached, and the turn being flown now when
        the objective is more than turn_lift degrees off the course. inf = hold the throttle."""
        w = math.inf
        vc = s.corner_speed(c['corner'])
        room = c['dist'] - s.margin - c['speed'] * s.lead
        if c['speed'] > vc and room <= (c['speed'] ** 2 - vc ** 2) / (2 * COAST):
            w = vc
        if c['angle'] >= s.turn_lift:
            w = min(w, s.corner_speed(180 - c['angle']))
        return w


THROUGH_CENTRE = True


def race(kind, intensity, T=300, dt=1 / 60, pol=None, real_stick=True, nose_rate=None, nose_lead=None):
    """(seconds for 3 laps or None = stalled, rings threaded of 24, top speed, share of the race lifted); race.misses
    (crossings of a ring's plane outside its mouth, RaceGateRing.PlaneCrossing's OutsideMouth) is set as well.
    nose_rate: None (today: the heading turns at once) or the nose follow rate per second (1.5 = the game's AI hulls);
    nose_lead: AIPilot.NoseLeadGain on top of the lag (None = off, a gain, {"gain", "max_extra_deg"} or a NoseLead)."""
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); laps = 3; total = n * laps
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    pos = add(P[0], mul(A[0], 1500)); fwd = unit(sub(P[0], pos)); speed = 0.0
    pol = pol or Policy()
    # the nose lag and the AI nose lead, both OFF by default (the docstring): a rate of 0, inf or one that closes the whole
    # gap in a step is no lag, today's model; the effective gain is AIPilot.EffectiveNoseLeadGain from THIS race's rate, once
    follow = float(nose_rate) if nose_rate is not None and 0 < nose_rate < math.inf and nose_rate * dt < 1.0 else None
    cmd = list(fwd)   # the stick's command; it is the nose itself until a nose_rate splits them
    lead_knob = NoseLead.from_config(nose_lead)
    g_lead = lead_knob.effective(follow) if follow is not None else 0.0
    race.misses = 0
    idx = 0; locked = -1; side = 1; t = 0.0; stick = 0.0; maxv = 0; dash_until = -1; next_dash = 0; lifted = 0.0
    while t < T and idx < total:
        g = idx % n; ring = P[g]; ax = A[g]
        if idx != locked:
            locked = idx; side = 1 if dot(sub(pos, ring), ax) >= 0 else -1
            leg_from = P[(idx - 1) % n] if idx > 0 else pos
            lead = min(480, 0.4 * norm(sub(leg_from, ring)))
        R = max(speed, 1) / math.radians(omega(speed))
        commit = min(420, lead + 0.5 * R)
        approaching = norm(sub(ring, pos)) > commit and norm(sub(add(ring, mul(ax, side * lead)), pos)) > max(60, 0.5 * R)
        through = add(ring, mul(unit(sub(ring, pos)), 320)) if THROUGH_CENTRE else sub(ring, mul(ax, side * 320))
        if approaching: target = add(ring, mul(ax, side * lead)); after = through
        else: target = through; after = P[(idx + 1) % n] if idx + 1 < total else None
        to = sub(target, pos)
        nl = sub(after, target) if after is not None else None
        c = {'angle': ang(fwd, to), 'stick': stick, 'dist': norm(to), 'speed': speed,
             'corner': ang(to, nl) if nl is not None else -1}
        # the drive
        x = 1.0
        if kind == 'lift' and speed > pol.wanted(c):
            x = 0.0
        if x <= DEADZONE: lifted += dt
        if t >= next_dash and pol.is_straight(c) and pol.runway(c) and x > DEADZONE:
            dash_until = t + DASH_SECONDS; next_dash = t + DASH_COOLDOWN
        # pilot steering: AIPilot's stick law on the angle error, judged from the nose; with the nose lead
        # (AIPilot.TryNoseLead) judged from the COMMAND, wanting the nose turned toward the aim by g_lead x its error,
        # past it (ai_nose_lead.slerp_beyond = AIPilot.NoseLeadDirection)
        if g_lead > 0:
            judge, want = judge_and_want(fwd, cmd, to, g_lead, lead_knob.max_extra_deg); err = ang(judge, want)
        else:
            want = to; err = c['angle']
        s = ai_stick(err) if real_stick else min(1.0, err / 20.0)
        stick = s
        # the integrator
        speed = speed + x * ACC * dt
        if x <= DEADZONE: speed = max(speed - COAST * dt, 0.0)
        speed = min(max(speed, 0.0), CEILING)
        v_fly = speed + (DASH_SPEED if t < dash_until else 0.0)
        maxv = max(maxv, v_fly)
        if follow is None:
            fwd = rotate_toward(fwd, want, math.radians(omega(v_fly) * s) * dt)   # today: the heading IS the command
        else:
            # the game: the stick turns the COMMAND at the hull's rate, the nose follows it first-order
            # (VesselTransformer.LERP_AMOUNT) and the hull flies along the nose
            cmd = rotate_toward(cmd, want, math.radians(omega(v_fly) * s) * dt)
            fwd = rotate_toward(fwd, cmd, math.radians(ang(fwd, cmd)) * min(1.0, follow * dt))
        newpos = add(pos, mul(fwd, v_fly * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= gates[g]['r']: idx += 1
            else:
                race.misses += 1
                if not approaching: locked = -1   # a missed mouth: re-latch the side and the approach point
        pos = newpos; t += dt
    return (round(t, 1) if idx >= total else None), idx, round(maxv), round(lifted / max(t, 1e-6), 2)


def score(res):
    return sum(r[0] if r[0] else 300 + (24 - r[1]) * 10 for r in res)


def _sweep_one(args):
    p = Policy()
    p.corner, p.margin, p.lead, p.turn_lift = args[:4]
    knobs = dict(zip(("nose_rate", "nose_lead"), args[4:6]))   # the nose lag / lead, when the sweep carries them
    res = [race('lift', I, pol=p, **knobs) for I in (1, 2, 3, 4)]
    return score(res), args, [r[0] for r in res]


def sweep(nose_rate=None, nose_lead=None):
    import itertools
    from concurrent.futures import ProcessPoolExecutor
    grid = list(itertools.product((60, 100, 150, 220, 320, 500), (0, 40, 120), (0, 0.2, 0.5), (30, 45, 70, 180)))
    if nose_rate is not None or nose_lead is not None:
        if nose_rate is None: nose_rate = NOSE_FOLLOW   # a lead has nothing to do on a lag-free nose: the fleet lag
        lead_cfg = NoseLead.from_config(nose_lead).to_config() if nose_lead is not None else None
        grid = [cell + (nose_rate, lead_cfg) for cell in grid]
    with ProcessPoolExecutor() as ex:
        out = sorted(ex.map(_sweep_one, grid))
    print("total s (I1-I4; a stall costs 300 + 10 per ring left), (cornerSpeed, brakeMarginUnits, brakeLeadSeconds, "
          "turnLiftDegrees" + (", nose_rate, nose_lead" if nose_rate is not None or nose_lead is not None else "")
          + "), per course")
    for o in out[:12]:
        print(o)


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


def _knob_rows(nose_rate, nose_lead):
    """The extra rows --nose-rate / --nose-lead add to the table: (label, race kwargs) for the lag alone and, with a lead,
    the lead on top of it. A lead without a rate is raced at the fleet lag (a lag-free nose has nothing to lead)."""
    if nose_rate is None and nose_lead is None:
        return []
    rate = NOSE_FOLLOW if nose_rate is None else nose_rate
    rows = [(f"nose lag {rate:g}/s", dict(nose_rate=rate))]
    if nose_lead is not None:
        lead = NoseLead.from_config(nose_lead)
        rows.append((f"nose lag {rate:g}/s + AI nose lead {lead.gain:g} (+{lead.max_extra_deg:g} deg max, effective gain "
                     f"{lead.effective(rate):g})", dict(nose_rate=rate, nose_lead=lead)))
    return rows


def _table_row(I, real, **kw):
    a = race('today', I, real_stick=real, **kw); am = race.misses
    b = race('lift', I, real_stick=real, **kw); bm = race.misses
    return f"I{I}: today {a} m{am}   corner lift {b} m{bm}"


# Today's numbers with both knobs OFF (2026-11-07, `python3 Tools/Build/scarab_drive_sim.py`): (kind, intensity) ->
# (seconds, rings, top speed, lifted share), no crossing outside a mouth. The self-test holds the knobs' default to them,
# so the lag and the lead cannot move a number nobody switched on.
TODAY = {
    ('today', 1): (49.2, 24, 424, 0.0), ('lift', 1): (49.5, 24, 424, 0.05),
    ('today', 2): (54.7, 24, 424, 0.0), ('lift', 2): (56.6, 24, 424, 0.12),
    ('today', 3): (59.1, 24, 424, 0.0), ('lift', 3): (62.3, 24, 424, 0.24),
    ('today', 4): (59.2, 24, 424, 0.0), ('lift', 4): (62.4, 24, 424, 0.22),
}
TODAY_LINEAR = {1: (49.2, 24, 424, 0.0), 2: (55.2, 24, 424, 0.0), 3: (59.5, 24, 424, 0.0), 4: (60.2, 24, 424, 0.0)}   # --linear, today
# ...the same races under the game's nose lag (nose_rate 1.5, lead off; `--nose-rate 1.5`): ((seconds, rings, top speed,
# lifted share), crossings of a ring's plane outside its mouth). About 200 crossings is an orbit round a missed ring: this
# model has no break-off, so one missed mouth at full throttle is a stall.
LAGGED = {
    ('today', 1): ((55.2, 24, 424, 0.0), 0), ('lift', 1): ((57.6, 24, 424, 0.18), 0),
    ('today', 2): ((None, 8, 424, 0.0), 201), ('lift', 2): ((73.0, 24, 424, 0.31), 0),
    ('today', 3): ((None, 1, 424, 0.0), 214), ('lift', 3): ((88.1, 24, 424, 0.38), 0),
    ('today', 4): ((None, 2, 424, 0.0), 212), ('lift', 4): ((108.0, 24, 424, 0.38), 12),
}
# ...and with the AI nose lead 2.5 on top of the lag (`--nose-lead 2.5`): the corner-lift drive at I4 is the one cell it
# recovers; full throttle stalls at every intensity, I1 included (the docstring has why).
LED = {
    ('today', 1): ((None, 3, 424, 0.0), 209), ('lift', 1): ((68.5, 24, 424, 0.29), 0),
    ('today', 2): ((None, 2, 424, 0.0), 212), ('lift', 2): ((79.3, 24, 424, 0.33), 0),
    ('today', 3): ((None, 1, 424, 0.0), 214), ('lift', 3): ((91.1, 24, 424, 0.37), 5),
    ('today', 4): ((None, 1, 424, 0.0), 214), ('lift', 4): ((76.6, 24, 424, 0.29), 0),
}


def _raced(kind, I, **kw):
    """race()'s tuple and its race.misses, as one value to compare."""
    r = race(kind, I, **kw)
    return r, race.misses


def self_test():
    bad = []
    # (a) the nose lag and the AI nose lead (ai_nose_lead.py) are OFF by default: with the knobs unset every race of the
    # default table is today's (TODAY, the numbers `python3 Tools/Build/scarab_drive_sim.py` printed before the knobs)...
    for (kind, I), want in TODAY.items():
        got = _raced(kind, I)
        if got != (want, 0):
            bad.append(f"{kind} I{I} with the knobs unset: {got} is not today's {want} / 0 misses")
        # ...NoseLead() (gain 0) and the other off forms are the unset knob, and on a lag-free nose (nose_rate None, 0, -1,
        # inf or a rate that closes the whole gap in one step: today's model) the lead has nothing to lead and changes nothing
        for kw in ({"nose_lead": NoseLead()}, {"nose_lead": 2.5}, {"nose_rate": 0, "nose_lead": 2.5}):
            if _raced(kind, I, **kw) != (want, 0):
                bad.append(f"{kind} I{I} with {kw}: not today's race {want}")
    for I, want in TODAY_LINEAR.items():
        if _raced('today', I, real_stick=False) != (want, 0) or _raced('today', I, real_stick=False, nose_lead=2.5) != (want, 0):
            bad.append(f"--linear today I{I} with the knobs unset / a lag-free lead: not today's {want}")
    for kw in ({"nose_lead": 0}, {"nose_lead": {}}, {"nose_lead": False}, {"nose_rate": -1},
               {"nose_rate": float("inf"), "nose_lead": 2.5}, {"nose_rate": 60.0, "nose_lead": 2.5}):
        if _raced('today', 1, **kw) != (TODAY[('today', 1)], 0):
            bad.append(f"today I1 with {kw}: not today's race")
    # (b) the game's nose lag (nose_rate 1.5), lead off, is today's steering through the lag and shows the defect: slower in
    # every cell, and at full throttle a stall at I2-I4 (LAGGED: one missed mouth, then an orbit this model never breaks off)...
    lagged = {}
    for (kind, I), want in LAGGED.items():
        lagged[(kind, I)] = got = _raced(kind, I, nose_rate=1.5)
        if got != want:
            bad.append(f"{kind} I{I} under the nose lag: {got} is not {want}")
        if got[0][0] is not None and got[0][0] <= TODAY[(kind, I)][0]:
            bad.append(f"{kind} I{I}: the nose lag did not slow the race ({got} vs today's {TODAY[(kind, I)]})")
    if _raced('lift', 4, nose_rate=1.5, nose_lead=NoseLead()) != lagged[('lift', 4)]:
        bad.append("NoseLead() (gain 0) is not the unset lead under the lag")
    # ...the AI nose lead 2.5 on top of the lag recovers the corner-lift drive at I4: faster, and no crossing outside a mouth
    # where the lag alone had 12...
    led = {}
    for (kind, I), want in LED.items():
        led[(kind, I)] = got = _raced(kind, I, nose_rate=1.5, nose_lead=2.5)
        if got != want:
            bad.append(f"{kind} I{I} under the lag with the 2.5 lead: {got} is not {want} (re-measure the docstring)")
    (lt, _, _, _), lm = led[('lift', 4)]; (gt, _, _, _), gm = lagged[('lift', 4)]
    if lt is None or gt is None or lt >= gt or lm != 0 or gm == 0:
        bad.append(f"corner lift I4: the 2.5 lead did not recover the lagged race: {led[('lift', 4)]} vs the lag alone {lagged[('lift', 4)]}")
    if _raced('lift', 4, nose_rate=1.5, nose_lead={"nose_lead": 2.5}) != led[('lift', 4)]:
        bad.append("{'nose_lead': 2.5} is not the 2.5 lead")
    # ...and what it does NOT do here (the docstring): at full throttle the 2.5 lead stalls I1, which the lag alone finishes
    # (the command parked past the aim when the gate steering's aim jumps to the through point); a milder pull, gain 1.5,
    # finishes I2 where the lag alone stalls. A port that changes either re-measures the docstring.
    if led[('today', 1)][0][0] is not None or lagged[('today', 1)][0][0] is None:
        bad.append(f"full throttle I1: the 2.5 lead no longer stalls the race the lag alone finishes ({led[('today', 1)]} vs {lagged[('today', 1)]})")
    g15 = _raced('today', 2, nose_rate=1.5, nose_lead=1.5)
    if g15[0][0] is None or lagged[('today', 2)][0][0] is not None:
        bad.append(f"full throttle I2: gain 1.5 no longer finishes the race the lag alone stalls ({g15} vs {lagged[('today', 2)]})")
    # (c) the controls: gain 1 (judged from the command, with no pull past the aim) is weaker than 2.5 where 2.5 recovers,
    # slower with more misses; it IS the lead with no room past the aim (max_extra_deg 0, the same wanted direction); and on
    # a 9/s nose the 2.5 gain floors to 1 (AIPilot.EffectiveNoseLeadGain from the race's own rate) and still changes that race
    g1 = _raced('lift', 4, nose_rate=1.5, nose_lead=1.0)
    if g1[0][0] is None or g1[0][0] <= lt or g1[1] <= lm:
        bad.append(f"corner lift I4: gain 1.0 (no pull past the aim) rivals the 2.5 lead: {g1} vs {led[('lift', 4)]}")
    for kind, I in (('lift', 4), ('today', 1)):
        if _raced(kind, I, nose_rate=1.5, nose_lead=NoseLead(2.5, 0.0)) != _raced(kind, I, nose_rate=1.5, nose_lead=1.0):
            bad.append(f"{kind} I{I}: the lead with no room past the aim (max_extra_deg 0) is not gain 1.0")
    quick, quick_led = _raced('today', 1, nose_rate=9.0), _raced('today', 1, nose_rate=9.0, nose_lead=2.5)
    if quick_led != _raced('today', 1, nose_rate=9.0, nose_lead=1.0) or quick_led == quick:
        bad.append("on a 9/s nose the 2.5 lead is not gain 1 judged from the command, or it did not change the race")
    print("scarab_drive_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == '__main__':
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    nose_rate, nose_lead = knob_args(sys.argv)
    if "--sweep" in sys.argv:
        sweep(nose_rate, nose_lead)
    else:
        print("(seconds for 3 laps or None = stalled, rings threaded of 24, top speed, share of the race lifted)")
        real = "--linear" not in sys.argv
        for I in (1, 2, 3, 4):
            print(f"I{I}: today", race('today', I, real_stick=real), "  corner lift", race('lift', I, real_stick=real))
        for label, kw in _knob_rows(nose_rate, nose_lead):
            print(f"-- {label} (m = crossings of a ring's plane outside its mouth):")
            for I in (1, 2, 3, 4):
                print(_table_row(I, real, **kw))
