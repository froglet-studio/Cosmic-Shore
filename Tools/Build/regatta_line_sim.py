#!/usr/bin/env python3
"""Offline Regatta race on the hull's RACE LINE, with AIPilot's orbit break-off: does the break-off fire on the line?

A STEERING model, not the game. Each hull flies at a constant speed (its median speed as an AI rival in people's editor
Regatta races, regatta_rival_report.py) with its own turn law ((v x RotationThrottleScaler + min(Pitch, Yaw)) deg/s, the
prefab's numbers) and AIPilot's own stick law (scarab_drive_sim.ai_stick), on:

- the race line RegattaController.TryRaceLine flies: RegattaRaceLine.BuildGapLine over the exported rails (the gap
  between the two lanes that are not the pilot's, at RaceLineStyleSO.LaneRadius from the spine; the hull's
  Resources/RaceLines/<Hull>_Regatta numbers at its style risk), aimed RaceLineStyleSO.Lookahead ahead of the nearest
  line point;
- AIPilot.UpdateOrbitBreak as shipped: the OrbitDetector (360 deg swept without the range falling below 0.9 of the
  window's best) and the inside-the-turning-circle test (only when not closing, cos 0.5; capture radius 18), then an
  escape leg (heading + 0.35 x away, until the aim is RequiredApproachRun away, 0.6-6 s);
- GateRaceController's miss rule: a crossing of the ring's plane outside the mouth leaves the line for that ring and flies
  the gate way (through the ring's centre, 320 u past it) until it is threaded.

The fix (2026-10-25), both halves shipped:
- exempt=True: the race line's aim is a lead point on a path, like a rail's, which UpdateOrbitBreak already exempts, so
  the break-off never judges it (AIPilot.MarkAimAsPathLead, called by GateRaceController for the line's aim);
- gate=True, only_when_cut=True: when the chord to the look-ahead point would cross the next ring's plane outside the
  mouth less the hull's room, aim straight through the line's crossing point instead (RegattaRaceLine.AimThroughRing).

The nose lead (AIPilot.NoseLeadGain, PR #1786; the shared piece is Tools/Build/ai_nose_lead.py), OFF by default:
race(..., lag=True, nose_lead=2.5), where nose_lead is a gain, a {"gain", "max_extra_deg"} dict, a NoseLead, or None /
0 for today's steering. With the lag on and a gain over 0 the pilot judges its error from the COMMAND and steers the
command onto the nose turned toward the aim by the effective gain times its error - past the aim, at most
max_extra_deg (40) past it - so the lagging nose is pulled from beyond the mouth instead of overshooting it; the stick
law and the command / nose update are the ones above, untouched. The effective gain (AIPilot.EffectiveNoseLeadGain) is
computed once a race from this model's own nose rate, NOSE_FOLLOW (the fleet's 1.5/s; there is no per-hull rate
here), and is 0 with lag=False, where the nose IS the command and has nothing to lead, so a lag-free race - the
default, and what the compute evaluator (race_models.py "line") races - is untouched by the knob. The command is
steered onto the lead point by turn_with_lag's capped slerp (it stops AT the lead point; the game's Pitch / Yaw carry
on past it, scarab_nose_lead_sim's capped=False), and the lead stays on through the orbit break-off's escape leg, as
the game's does. What it buys here (the Scarab on the fix with the lag, 2026-11-07): I2 104.8 s / 21 missed mouths ->
50.6 s / 0, I3 174.2 s / 51 -> 56.8 s / 0, I4 a DNF at 17 rings -> 56.7 s / 0; the one cell it hurts is the Manta at
I3 (109.3 s / 9 -> 122.3 s / 17), the hull scarab_nose_lead_sim --fleet races on its own 9/s nose, which this model
has no knob for. The self-test holds the default to today's numbers (TODAY) and the claim to its negative control.

    python3 Tools/Build/regatta_line_sim.py              # every styled hull, I1-I4, before vs the fix
    python3 Tools/Build/regatta_line_sim.py --nose-lead 2.5   # the fix with the nose lag: today's steering vs the lead
    python3 Tools/Build/regatta_line_sim.py --self-test
"""
import functools, math, os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang, rotate_toward
from scarab_drive_sim import ai_stick
from ai_nose_lead import NoseLead, effective_gain, judge_and_want

RAIL_OFFSET = 22.0   # RegattaCourse.DefaultRails.LaneOffset
# hull: (speed u/s as an AI rival, RotationThrottleScaler, min(Pitch, Yaw), LaneShare, HullClearance, Risk)
HULLS = {
    "Manta":   (267, 0.2, 30, 0.809, 16, 0.5),
    "Dolphin": (120, 0.0, 110, 0.785, 12, 0.5),
    "Rhino":   (130, 0.2, 90, 0.676, 18, 0.4),
    "Scarab":  (322, 0.1, 100, 0.6, 12, 0.45),
    "Serpent": (157, 0.4, 80, 0.6, 12, 0.5),
    "Sparrow": (80, 0.1, 80, 0.55, 10, 0.5),
}
CAREFUL, BOLD, MIN_LOOK, MAX_LOOK = 0.35, 0.8, 60, 520
RACE_LINES = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..",
                                          "Assets", "Resources", "RaceLines"))


@functools.lru_cache(maxsize=None)
def authored_look_seconds(hull):
    """(careful, bold) look-ahead seconds a hull's Regatta race line is authored with (Resources/RaceLines/<hull>_Regatta),
    or None when the asset or its fields are missing. A per-hull model's default run reads this, so "shipped" is the
    shipped look-ahead and not every hull's 0.35 / 0.8 (the Manta's and the Rhino's moved off it on 2026-10-25)."""
    try:
        text = open(os.path.join(RACE_LINES, f"{hull}_Regatta.asset"), encoding="utf-8").read()
        careful = float(re.search(r"^\s+CarefulLookaheadSeconds:\s*([-0-9.eE]+)", text, re.M).group(1))
        bold = float(re.search(r"^\s+BoldLookaheadSeconds:\s*([-0-9.eE]+)", text, re.M).group(1))
        return careful, bold
    except (OSError, AttributeError, ValueError):
        return None


def gap_line(lanes, own, radius):
    """RegattaRaceLine.BuildGapLine(lanes[own+1], lanes[own+2], lanes[own], radius), matched by nearest point."""
    a, b, c = lanes[(own + 1) % 3], lanes[(own + 2) % 3], lanes[own]

    def match(lst, p, guess):
        best, bd = guess, 1e18
        for k in range(guess - 40, guess + 41):
            i = k % len(lst)
            d = dot(sub(lst[i], p), sub(lst[i], p))
            if d < bd: best, bd = i, d
        return best

    pts = []
    for i, pa in enumerate(a):
        pb = b[match(b, pa, i * len(b) // len(a))]
        pc = c[match(c, pa, i * len(c) // len(a))]
        spine = mul(add(add(pa, pb), pc), 1 / 3)
        gap = sub(mul(add(pa, pb), 0.5), spine)
        m = norm(gap)
        pts.append(add(spine, mul(gap, radius / m)) if m > 1e-4 else spine)
    return pts


def nearest(line, p, hint):
    n = len(line)
    rng = range(n) if hint < 0 else range(hint - 60, hint + 61)
    best, bd = (hint if hint >= 0 else 0), 1e18
    for k in rng:
        i = k % n
        d = dot(sub(line[i], p), sub(line[i], p))
        if d < bd: best, bd = i, d
    return best


def point_ahead(line, i, dist):
    n = len(line)
    left = dist
    for _ in range(n):
        j = (i + 1) % n
        seg = norm(sub(line[j], line[i]))
        if seg >= left:
            return add(line[i], mul(sub(line[j], line[i]), left / max(seg, 1e-6)))
        left -= seg; i = j
    return line[i]


def along(line, i, j):
    """Distance along the closed line from point i forward to point j."""
    n = len(line); d = 0.0
    while i != j:
        k = (i + 1) % n; d += norm(sub(line[k], line[i])); i = k
    return d


def chord_cuts_mouth(pos, aim, ring, axis, radius, room):
    """Whether the straight line from the pilot to its aim crosses the ring's plane outside the mouth less the hull's
    room (RaceGateRing.PlaneCrossing's test, on the chord the pilot is about to fly). False when it does not reach the
    plane at all."""
    a0 = dot(sub(pos, ring), axis); a1 = dot(sub(aim, ring), axis)
    if a0 * a1 > 0 or a0 == a1:
        return False
    k = a0 / (a0 - a1)
    hit = add(pos, mul(sub(aim, pos), k))
    return norm(sub(hit, ring)) > max(0.0, radius - room)


def ring_gated_aim(line, hint, look, ring_i, pos, ring=None, axis=None, radius=0.0, room=0.0):
    """The fix's aim: the look-ahead point, unless the ring's crossing point (the line point nearest its centre) is
    nearer along the line than the look-ahead - then the crossing point, pushed straight on through it from the pilot
    by what is left of the look-ahead, so the pilot flies at the mouth and never cuts across it."""
    left = along(line, hint, ring_i)
    free = point_ahead(line, hint, look)
    if left > look:
        return free
    if ring is not None and not chord_cuts_mouth(pos, free, ring, axis, radius, room):
        return free
    cross = line[ring_i]
    return add(cross, mul(unit(sub(cross, pos)), max(0.0, look - left)))


class Orbit:   # PursuitReachability.OrbitDetector
    def __init__(s): s.reset()
    def reset(s): s.has = False; s.swept = 0.0
    def tick(s, to):
        d = norm(to)
        if d < 1e-6: s.reset(); return False
        b = mul(to, 1 / d)
        if not s.has:
            s.has, s.last, s.best, s.lastd, s.swept = True, b, d, d, 0.0; return False
        if d > s.lastd * 1.6:
            s.last, s.best, s.lastd, s.swept = b, d, d, 0.0; return False
        s.swept += ang(s.last, b); s.last, s.lastd = b, d
        if d < s.best * 0.9:
            s.best, s.swept = d, 0.0; return False
        return s.swept >= 360


def inside_turning_circle(to, heading, R, capture=18.0):
    d = norm(to)
    if R <= 0 or d < 1e-6: return False
    f = unit(heading)
    lateral = norm(sub(to, mul(f, dot(to, f))))
    if lateral < 1e-6: return False
    # inside the circle of radius R tangent to the heading on the target's side: |to - R n|^2 < (R - c)^2
    n = unit(sub(to, mul(f, dot(to, f))))
    centre = mul(n, R)
    return norm(sub(to, centre)) < max(0.0, R - capture)


NOSE_FOLLOW = 1.5   # VesselTransformer.LERP_AMOUNT: AI, autopilot and remote hulls' nose follows the command at 1.5/s


def turn_with_lag(fwd, cmd, target, turn_rad, dt, lag):
    """One frame of the stick's turn. lag=False: the nose IS the command (the models before 2026-10-25, evening).
    lag=True, the game: the stick turns the COMMANDED heading (VesselTransformer.accumulatedRotation) and the nose
    slerps toward it at NOSE_FOLLOW per second (AI hulls kept this when people lost it, 2026-10-21). Returns (fwd, cmd)."""
    if not lag:
        f = rotate_toward(fwd, target, turn_rad)
        return f, f
    cmd = rotate_toward(cmd, target, turn_rad)
    return rotate_toward(fwd, cmd, math.radians(ang(fwd, cmd)) * min(1.0, NOSE_FOLLOW * dt)), cmd


def race(hull, intensity, exempt, T=240, dt=1 / 60, own=0, gate=False, only_when_cut=False, lag=False, look_s=None, max_look=None, blend=0.0,
         nose_lead=None):
    v, rts, base, share, clear, risk = HULLS[hull]
    lead = NoseLead.from_config(nose_lead)
    # the nose lead's gain, once per race from THIS model's nose rate (AIPilot.EffectiveNoseLeadGain): 0 with lag=False,
    # where the nose IS the command and has nothing to lead
    lead_gain = effective_gain(lead.gain, NOSE_FOLLOW) if lag else 0.0
    C = sf.course_from(sf.load_course_js(), intensity)
    gates = C['gates']; n = len(gates); total = 3 * n
    P = [g['p'] for g in gates]; A = [unit(g['a']) for g in gates]
    tight = min(g['r'] for g in gates)
    room = max(0.0, tight - clear * (1.5 + (0.75 - 1.5) * risk) - RAIL_OFFSET)
    line = gap_line(C['lanes'], own, RAIL_OFFSET + share * room)
    careful, bold = look_s or (CAREFUL, BOLD)
    look = min(max_look or MAX_LOOK, max(MIN_LOOK, v * (careful + (bold - careful) * risk)))
    omega = v * rts + base
    R = v / math.radians(omega)
    run = max(max(0.0, 2 * R - 18), v * 1.5)
    ring_at = [nearest(line, p, -1) for p in P]
    hint = nearest(line, P[0], -1)
    pos = point_ahead(line, (hint - 40) % len(line), 0); fwd = unit(sub(line[(hint - 38) % len(line)], pos))
    hint = -1; cmd = list(fwd)
    idx = 0; t = 0.0; lost = -1; orbit = Orbit(); extending = False; ext_t = 0.0; breaks = 0; misses = 0
    while t < T and idx < total:
        g = idx % n; ring = P[g]; ax = A[g]
        on_line = lost != idx
        if on_line:
            hint = nearest(line, pos, hint)
            if norm(sub(line[hint], pos)) > 250: hint = nearest(line, pos, -1)
            if not gate:
                aim = point_ahead(line, hint, look)
            elif only_when_cut:
                aim = ring_gated_aim(line, hint, look, ring_at[g], pos, ring, ax, gates[g]['r'], clear)
            else:
                aim = ring_gated_aim(line, hint, look, ring_at[g], pos)
        else:
            aim = add(ring, mul(unit(sub(ring, pos)), 320))
        to = sub(aim, pos)
        # AIPilot.UpdateOrbitBreak
        if exempt and on_line:
            extending = False; orbit.reset()
        elif extending:
            ext_t += dt
            if (norm(to) >= run and ext_t >= 0.6) or ext_t >= 6: extending = False
        else:
            closing = dot(unit(to), fwd) > 0.5
            if (not closing and inside_turning_circle(to, fwd, R)) or orbit.tick(to):
                extending = True; ext_t = 0.0; breaks += 1; orbit.reset()
        steer = add(fwd, mul(unit(to), -0.35)) if extending else to
        if lead_gain > 0:
            # the nose lead (AIPilot.NoseLeadGain): judge the error from the COMMAND, and steer the command onto the nose
            # turned toward the steer direction by the effective gain times its error, past it (capped), where the nose is pulled
            # from; on during the escape leg too, as the game keeps the lead on whatever the steer direction is
            judge, want = judge_and_want(fwd, cmd, steer, lead_gain, lead.max_extra_deg)
        else:
            # blend > 0: judge the error from where the nose is about to point (the nose moved blend of the way to the command)
            judge = rotate_toward(fwd, cmd, math.radians(ang(fwd, cmd)) * blend) if (lag and blend > 0) else fwd
            want = steer
        s = ai_stick(ang(judge, want))
        fwd, cmd = turn_with_lag(fwd, cmd, want, math.radians(omega * s) * dt, dt, lag)
        newpos = add(pos, mul(fwd, v * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            k = a0 / (a0 - a1); hit = add(pos, mul(sub(newpos, pos), k))
            if norm(sub(hit, ring)) <= gates[g]['r']:
                idx += 1
            elif norm(sub(hit, ring)) < 1500:
                misses += 1; lost = idx
        pos = newpos; t += dt
    return {"time": round(t, 1) if idx >= total else None, "rings": idx, "rpm": round(60 * idx / t, 1),
            "breaks": breaks, "misses": misses}


def _before_after(job):
    h, I = job
    return race(h, I, False), race(h, I, True, gate=True, only_when_cut=True)


def _lag_today_lead(job):
    """--nose-lead's pool worker: the fix with the nose lag, today's steering vs the lead at the job's gain."""
    h, I, gain = job
    fix = dict(gate=True, only_when_cut=True, lag=True)
    return race(h, I, True, **fix), race(h, I, True, nose_lead=gain, **fix)


# Today's numbers with the lead OFF (2026-11-07): the Scarab on the fix at I1-I4, (intensity, lag). The lag-off rows are
# what the compute evaluator races; the lag-on rows are the overshoot the lead exists for. The self-test holds the knob's
# default to them, so the lead cannot move a number nobody switched on.
TODAY = {
    (1, False): {"time": 43.8, "rings": 24, "rpm": 32.9, "breaks": 0, "misses": 0},
    (1, True): {"time": 46.1, "rings": 24, "rpm": 31.2, "breaks": 0, "misses": 0},
    (2, False): {"time": 48.2, "rings": 24, "rpm": 29.9, "breaks": 0, "misses": 0},
    (2, True): {"time": 104.8, "rings": 24, "rpm": 13.7, "breaks": 6, "misses": 21},
    (3, False): {"time": 53.6, "rings": 24, "rpm": 26.9, "breaks": 0, "misses": 0},
    (3, True): {"time": 174.2, "rings": 24, "rpm": 8.3, "breaks": 15, "misses": 51},
    (4, False): {"time": 53.3, "rings": 24, "rpm": 27.0, "breaks": 0, "misses": 0},
    (4, True): {"time": None, "rings": 17, "rpm": 4.2, "breaks": 27, "misses": 91},
}


def self_test():
    bad = []
    o = Orbit(); c = [0, 0, 0]
    # a constant-range lead point swept round a full circle IS read as an orbit by the shipped detector...
    fired = any(o.tick([100 * math.cos(math.radians(a)), 0, 100 * math.sin(math.radians(a))]) for a in range(0, 400, 2))
    if not fired: bad.append("detector never fires on a constant-range sweep")
    # ...and a closing approach is not
    o = Orbit()
    if any(o.tick([0, 0, 1000 - 5 * i]) for i in range(150)): bad.append("detector fired on a straight approach")
    r = race("Scarab", 1, exempt=True)
    if r["breaks"] != 0: bad.append("the exemption still broke off on the line")
    before, after = race("Scarab", 3, False), race("Scarab", 3, True, gate=True, only_when_cut=True)
    if not (before["breaks"] + before["misses"] > 0 and after["breaks"] + after["misses"] == 0):
        bad.append(f"Scarab I3: before {before}, after {after} (the fix no longer clears the misses, or the model lost them)")
    fix = dict(gate=True, only_when_cut=True)
    # the nose lead is OFF by default: with it unset the fix races today's numbers (TODAY), lag off and on...
    got = {k: race("Scarab", k[0], True, lag=k[1], **fix) for k in TODAY}
    for k, want in TODAY.items():
        if got[k] != want: bad.append(f"Scarab I{k[0]} lag={k[1]} with the lead unset: {got[k]} is not today's {want}")
    today = got[(3, True)]
    # ...and NoseLead() (gain 0) is the unset knob
    if race("Scarab", 3, True, lag=True, nose_lead=NoseLead(), **fix) != today:
        bad.append("NoseLead() (gain 0) is not the unset lead")
    # the claim: at I3 with the lag, judged from the nose the Scarab overshoots 51 mouths and breaks off 15 times; the
    # lead at 2.5 clears them and finishes faster...
    led = race("Scarab", 3, True, lag=True, nose_lead=2.5, **fix)
    if not (today["misses"] >= 20 and led["misses"] <= 0.25 * today["misses"] and led["time"] is not None
            and led["time"] < today["time"]):
        bad.append(f"the lead no longer fixes Scarab I3 with the lag: today {today}, lead 2.5 {led}")
    # ...the negative control: an effective gain of 1 (judged from the command, with no pull past the aim) does not...
    pull_less = race("Scarab", 3, True, lag=True, nose_lead=1.0, **fix)
    if pull_less["misses"] < 4 * led["misses"] + 5:
        bad.append(f"gain 1.0 (no pull past the aim) now rivals the lead at I3: {pull_less} vs lead 2.5 {led}")
    # ...it IS the lead with no room past the aim (max_extra_deg 0, the same wanted direction) and this file's own
    # command-judged prediction (blend 1.0), so the pull is what the lead adds...
    if race("Scarab", 3, True, lag=True, nose_lead={"gain": 2.5, "max_extra_deg": 0}, **fix) != pull_less:
        bad.append("the lead with no room past the aim (max_extra_deg 0) is not gain 1.0")
    pred = race("Scarab", 3, True, lag=True, blend=1.0, **fix)
    if (pred["misses"], pred["breaks"], pred["rings"]) != (pull_less["misses"], pull_less["breaks"], pull_less["rings"]):
        bad.append(f"gain 1.0 is not the command-judged (blend 1.0) steering: {pull_less} vs {pred}")
    # ...and on a lag-free race (the nose IS the command, effective gain 0) the lead changes nothing
    if race("Scarab", 3, True, nose_lead=2.5, **fix) != after:
        bad.append(f"the lead moved a lag-free race: {race('Scarab', 3, True, nose_lead=2.5, **fix)} vs {after}")
    print("regatta_line_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    from concurrent.futures import ProcessPoolExecutor
    jobs = [(h, I) for h in HULLS for I in (1, 2, 3, 4)]
    if "--nose-lead" in sys.argv:
        # the fix with the nose lag, today's steering vs the lead at this gain (a lag-free race has nothing to lead)
        gain = float(sys.argv[sys.argv.index("--nose-lead") + 1])
        worker, jobs = _lag_today_lead, [(h, I, gain) for h, I in jobs]
        print(f"hull     I  the fix + nose lag, today (s / rings, rings/min, break-offs, misses)   nose lead {gain:g}")
    else:
        worker = _before_after
        print("hull     I  before (s / rings, rings/min, break-offs, misses)       the fix")
    with ProcessPoolExecutor() as ex:
        for job, (a, b) in zip(jobs, ex.map(worker, jobs)):
            h, I = job[0], job[1]
            f = lambda r: f"{str(r['time']):>5} / {r['rings']:2d}, {r['rpm']:5.1f} r/min, {r['breaks']:2d} brk, {r['misses']:2d} miss"
            print(f"{h:8} I{I}  {f(a)}     {f(b)}")
