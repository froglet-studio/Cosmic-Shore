#!/usr/bin/env python3
"""Offline Breakwater race for the AI SPARROW, on the courses the game deals (breakwater_arena.generate, the mirror of
BreakwaterCourse): the start gate and the fourteen-station circuit flown twice, 29 crossings, at I1-I4.

The Sparrow's universal Time AI is trained here (Docs/AI_SYSTEM/AI_PLAN.md step time-arcade; Breakwater is the
Sparrow's Time game, MINIGAME_TRAINING_PLAN.md section 6). Its genome is the HoldBoostAIPolicySO drive: when to hold the
afterburner, when to let go before a station, and when to fire the SkyBurst rocket at the station's plug.

The model (a STEERING, SPEED and STATION model, not the game):

- **The hull, measured** from the AI Sparrow rivals in Breakwater flights (2026-10-26 and 10-31, least squares on
  their 10 Hz velocities): cruise 35 u/s, afterburner 135 u/s (Breakwater's Time rest; Sparrow.prefab MinimumSpeed 10 +
  DefaultThrottleScaler 25, x boostMultiplier 5 on the throttle term), the speed easing toward either at 1.4 / s; turn
  rate v x 0.1 + 80 deg/s (83 u radius at the afterburner). People hold the afterburner 80-99% of a race at 135 u/s.
- **The steering**: GateRaceController with the Regatta steering BreakwaterController turns on
  (`AICommitFromApproachPoint`): approach point min(280, 0.4 x leg, 0.8 x 240) behind the station on its latched side,
  commit inside min(240, lead + R/2), then the through point 200 u past the station on the line through its centre;
  AIPilot's stick law and nose lag; the orbit break-off; after a crossing outside the port, re-latch and break off.
  The AI nose lead (AIPilot.NoseLeadGain, PR #1786; Tools/Build/ai_nose_lead.py is the shared piece) is a knob of
  race(), `nose_lead`, OFF by default: a gain over 0 judges the error from the COMMAND and steers it onto the nose
  turned toward the aim by the gain times its error, past it (at most max_extra_deg, 40), so the lagging nose is
  pulled from beyond the station instead of overshooting it; off with lag=False (nothing to lead).
  Ported from switchback_dolphin_sim.race (SPARROW_BREAKWATER), whose Breakwater mode it replaces for training.
- **The driver**: HoldBoostAIPolicySO.Driver.Tick line for line (sparrow_regatta_sim.Hold), judged on the station:
  its bearing off the course, its range (runway) at the current speed, and the stick written last frame; and the
  "clear the way" rocket (HoldBoostAIPolicySO.RocketShot): once per station, nose within rocketAimDegrees of it, between
  rocketMinRange and rocketMaxRange, a cooldown, ammo >= SkyBurstGunAction.ammoCost 0.25 of a tank of 1.
- **The station** (BREAKWATER.md "The station, in full"): a crossing of the port plane outside the port radius is a
  miss; inside it, the hull's centre within EYE_CLEAR of the axis threads clean (the 18 u eye less half the 12.32 u hull),
  within COLLAR_REACH it brushes the Plain keystone collar (x0.5 easing back over 1 s), and past it the hull clips the
  Danger weave (x0, a full stop, easing back over 3 s): SparrowVesselChangeSpeedByPrism (PLAIN_HIT, DANGER_HIT). The
  danger prism's all-element debuff and "boost reset" (BREAKWATER.md) are not modelled. A rocket that reaches the plug opens a BLAST_RADIUS
  hole round the point its nose ray meets the port plane (the round inherits the hull's velocity, Gun.cs, so it always
  arrives first); a crossing inside a hole is clean. Holes stay open for the second lap.
- **Ammo**: the tank starts full; REFILL_PER_STATION is what the timer-driven guns are ASSUMED to grind back from each
  station's dish (ammoPerPrism 0.01; people gained 0.25-3 tanks a race).

NOT modelled: rivals, the shoals, the dish's Plain plates, the all-element debuff, frame hitches, the AI's skill
throttle on other modes. The editor is the measurement.

    python3 Tools/Build/breakwater_sparrow_sim.py                  # shipped base and Breakwater drives, I1-I4
    python3 Tools/Build/breakwater_sparrow_sim.py --nose-lead 2.5  # ...and each drive with the AI nose lead on
    python3 Tools/Build/breakwater_sparrow_sim.py --self-test
"""
import math, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import breakwater_arena as B
import regatta_line_sim as L
from rhino_drive_sim import add, sub, mul, dot, norm, unit, ang
from scarab_drive_sim import ai_stick
from sparrow_regatta_sim import Hold, bands_from_fields as bands
from ai_nose_lead import NoseLead, effective_gain, judge_and_want   # the AI nose lead (AIPilot.NoseLeadGain), one shared piece

CRUISE, BOOST, SPEED_RATE = 35.0, 135.0, 1.4
RTS, BASE_TURN = 0.1, 80.0
COMMIT, LEAD, THROUGH = 240.0, 280.0, 200.0        # MinigameBreakwater.unity GateRaceController
LEAD_SHARE, LEAD_INSIDE_SHARE = 0.4, 0.8            # GateRaceController.ApproachLeadShareOfLeg / ApproachLeadFor
HULL = 12.32
EYE_CLEAR = B.EYE_RADIUS - HULL / 2                 # 11.84
COLLAR_REACH = B.COLLAR_CLEAR_RADIUS - HULL / 2     # 20.15: past it the hull touches the Danger weave
# What a prism does to the Sparrow (SparrowImpactorDataContainer -> SparrowVesselChangeSpeedByPrism,
# VesselChangeSpeedByPrismEffectSO -> VesselTransformer.ModifyThrottle): the speed is multiplied at impact by a factor
# that eases linearly back to 1 (overlapping factors multiply). A collar block (Plain, 8 u cube: volume x massScaling 0.1
# past the maxSlowStrength 0.5 cap) x0.5 over 1 s; a Danger bar x0 over 3 s (0.5 x dangerSlowMultiplier 3, clamped;
# duration 1 s x dangerSlowDurationMultiplier 3). Not modelled: VesselElementalDebuffByDangerPrismEffect (-0.1 to -0.5 of
# a level on every element for 4 s: a few percent of the afterburner) and VesselRollByPrismEffect (a 60 deg roll about
# the nose, which does not turn the course).
PLAIN_HIT, DANGER_HIT = (0.5, 1.0), (0.0, 3.0)
BLAST_RADIUS = B.BLAST_RADIUS_REST                  # 50 u
AMMO_COST, REFILL_PER_STATION = 0.25, 0.05
TARGET = B.crossing_target()                         # 29

# The shipped drives (HoldBoostAIPolicySO fields, by the asset's names). BASE is Assets/_SO_Assets/AI Boost Policies/
# SparrowAIBoostPolicy.asset, BREAKWATER Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset (what the game
# flies in Breakwater). Read from the assets by the evaluator; these copies are for the CLI and the self-test.
BASE = {"alignDegrees": 12, "releaseAlignDegrees": 25, "stickBand": 0.35, "releaseStickBand": 0.6,
        "engageRunwaySeconds": 1.5, "releaseRunwaySeconds": 0.5, "minHoldSeconds": 0.6, "minRestSeconds": 0.4,
        "rocketMaxRange": 220, "rocketMinRange": 60, "rocketAimDegrees": 4, "rocketCooldownSeconds": 1, "rocket": False}
BREAKWATER = dict(BASE, alignDegrees=45, releaseAlignDegrees=60, stickBand=1, releaseStickBand=1,
                  engageRunwaySeconds=2, releaseRunwaySeconds=1.25, rocketMaxRange=651.421, rocketMinRange=55.448,
                  rocketAimDegrees=16.727, rocket=True)


def course(intensity, seed, pad=0):
    cfg = B.INTENSITIES[intensity - 1]
    st = B.generate(seed, cfg)
    if st is None:
        return None
    gates = [{"p": list(p), "a": unit(list(a)), "r": cfg["port"]} for p, a, _ in st]
    start = list(B.spawn_pads()[pad % len(B.SPAWN_PAD_BEARINGS_DEG)])
    return {"gates": gates, "start": start, "heading": unit(sub(gates[0]["p"], start)), "intensity": intensity,
            "seed": seed}


# The nose lead's module globals of 2026-11-06 (NOSE_LEAD_GAIN, NOSE_LEAD_MAX_EXTRA, lead_point: off while breaking off)
# were replaced 2026-11-07 by the shared piece, ai_nose_lead, as race()'s `nose_lead` knob - the game's rule (on through
# the break-off, off on a lag-free nose) and the same numbers as every other model's.


def race(c, p, T=300.0, dt=1 / 50, lag=True, trace=None, nose_lead=None):
    """One AI Sparrow alone through the 29 crossings. p: the drive's fields (BASE / BREAKWATER / a searched point).
    nose_lead: the AI nose lead (AIPilot.NoseLeadGain; ai_nose_lead.NoseLead.from_config: None = OFF, today's steering;
    a gain stated at the fleet lag, a {"gain", "max_extra_deg"} dict or a NoseLead), scaled once to this model's nose
    rate (regatta_line_sim.NOSE_FOLLOW, the fleet's 1.5/s); 0 with lag=False, where the nose IS the command."""
    gates = c["gates"]; n = len(gates)
    drv = Hold(bands(p))
    rocket = p.get("rocket", True)
    pos = list(c["start"]); fwd = list(c["heading"]); cmd = list(fwd)
    nlead = NoseLead.from_config(nose_lead)   # the knob (`lead` below is the gate approach lead)
    lead_gain = effective_gain(nlead.gain, L.NOSE_FOLLOW) if lag else 0.0   # AIPilot.EffectiveNoseLeadGain, once a race
    v = CRUISE
    turn = lambda s: RTS * s + BASE_TURN
    R = v / math.radians(turn(v)); run = max(max(0.0, 2 * R - 18), v * 1.5)
    k = 0; locked = -1; side = 1.0; lead = LEAD; t = 0.0
    orbit = L.Orbit(); extending = False; ext_t = 0.0
    misses = breaks = clean = collar = danger = rockets = 0
    ammo = 1.0; rocket_at = -1e9; fired_for = -1; holes = {}
    boosted = 0.0; stick = 0.0; slows = []
    while t < T and k < TARGET:
        gi = B.ring_for_crossing(k)
        g = gates[gi]; ring, ax = g["p"], g["a"]
        if k != locked:
            locked = k
            side = 1.0 if dot(sub(pos, ring), ax) >= 0 else -1.0
            prev = gates[B.ring_for_crossing(k - 1)]["p"] if k > 0 else pos
            lead = min(LEAD, LEAD_SHARE * norm(sub(prev, ring)), LEAD_INSIDE_SHARE * COMMIT)
        commit = min(COMMIT, lead + 0.5 * R)
        to_ring = sub(ring, pos); dist = norm(to_ring)
        approaching = dist > commit
        aim = add(ring, mul(ax, side * lead)) if approaching else add(ring, mul(unit(to_ring), THROUGH))
        to = sub(aim, pos)
        desired = unit(to) if norm(to) > 1.0 else list(fwd)
        if extending:
            ext_t += dt
            if (norm(to) >= run and ext_t >= 0.6) or ext_t >= 6: extending = False
        else:
            closing = dot(desired, fwd) > 0.5
            if (not closing and L.inside_turning_circle(to, fwd, R)) or orbit.tick(to):
                extending = True; ext_t = 0.0; breaks += 1; orbit.reset()
        # the drive: the rocket first (Driver.TickRocket), then the hold, judged on the station
        if (rocket and fired_for != gi and t - rocket_at >= p["rocketCooldownSeconds"] and not extending
                and p["rocketMinRange"] <= dist <= p["rocketMaxRange"] and ang(fwd, to_ring) <= p["rocketAimDegrees"]
                and ammo >= AMMO_COST):
            ammo -= AMMO_COST; rocket_at = t; fired_for = gi; rockets += 1
            a0 = dot(sub(pos, ring), ax); d0 = dot(fwd, ax)
            if abs(d0) > 1e-3:
                hit = add(pos, mul(fwd, -a0 / d0))
                if norm(sub(hit, ring)) <= B.DISH_RATIO * g["r"] and dot(sub(hit, pos), fwd) > 0:
                    holes.setdefault(gi, []).append(hit)
        drv.tick({"angle": ang(fwd, to_ring), "stick": stick, "dist": dist, "speed": v, "breaking": extending}, t)
        if drv.held: boosted += dt
        v += ((BOOST if drv.held else CRUISE) - v) * min(1.0, SPEED_RATE * dt)
        R = v / math.radians(turn(v)); run = max(max(0.0, 2 * R - 18), v * 1.5)
        steer = add(fwd, mul(desired, -0.35)) if extending else to
        if lead_gain > 0:
            # the nose lead (AIPilot.NoseLeadGain): judge the error from the COMMAND and steer the command onto the nose
            # turned toward the steer direction by the effective gain times its error, past it (capped), where the lagging
            # nose is pulled from; on through the break-off's escape leg, as the game's is. The drive above keeps reading
            # the NOSE's angle (AIBoostContext.Forward is transform.forward) and the stick the pilot wrote (LastStickDeflection).
            judge, want = judge_and_want(fwd, cmd, steer, lead_gain, nlead.max_extra_deg)
        else:
            judge, want = fwd, steer
        stick = ai_stick(ang(judge, want))
        fwd, cmd = L.turn_with_lag(fwd, cmd, want, math.radians(turn(v) * stick) * dt, dt, lag)
        mod = 1.0
        for f, t0, d in list(slows):
            e = (t - t0) / d
            if e >= 1.0: slows.remove((f, t0, d)); continue
            mod *= f + (1.0 - f) * e
        newpos = add(pos, mul(fwd, v * mod * dt))
        a0 = dot(sub(pos, ring), ax); a1 = dot(sub(newpos, ring), ax)
        if a0 * a1 <= 0 and a0 != a1:
            hit = add(pos, mul(sub(newpos, pos), a0 / (a0 - a1)))
            r = norm(sub(hit, ring))
            if r <= g["r"]:
                k += 1
                ammo = min(1.0, ammo + REFILL_PER_STATION)
                if r <= EYE_CLEAR or any(norm(sub(hit, h)) <= BLAST_RADIUS - HULL / 2 for h in holes.get(gi, ())):
                    clean += 1
                elif r <= COLLAR_REACH:
                    collar += 1; slows.append((PLAIN_HIT[0], t, PLAIN_HIT[1]))
                else:
                    danger += 1; slows.append((DANGER_HIT[0], t, DANGER_HIT[1]))
            elif r < 1500:
                misses += 1; locked = -1; extending = True; ext_t = 0.0
        if trace is not None and int(round(t / dt)) % 25 == 0:
            trace.append((round(t, 1), k, round(dist), round(v), drv.held, approaching, extending))
        pos = newpos; t += dt
    return {"time": round(t, 2) if k >= TARGET else None, "crossings": k, "clean": clean, "collar": collar,
            "danger": danger, "misses": misses, "breaks": breaks, "rockets": rockets,
            "boosted": round(boosted / max(t, 1e-6), 2)}


DNF_SECONDS_PER_CROSSING = 15.0


def priced(r, T):
    """A race's seconds, a DNF priced above any finish: the cut plus DNF_SECONDS_PER_CROSSING per crossing left."""
    return r["time"] if r["time"] is not None else T + DNF_SECONDS_PER_CROSSING * (TARGET - r["crossings"])


def composite(per_intensity, worst=0.5):
    """The AI plan's composite: mean + worst x the worst, over a {key: value} of per-intensity scores."""
    vals = list(per_intensity.values())
    return sum(vals) / len(vals) + worst * max(vals)


def self_test():
    bad = []
    c = course(1, 101)
    if c is None or len(c["gates"]) != B.STATION_COUNT:
        bad.append("the course mirror did not deal 15 stations")
        print("breakwater_sparrow_sim self-test: FAILED " + repr(bad)); return 1
    base, bw = race(c, BASE), race(c, BREAKWATER)
    # the base bands stutter on Breakwater's short legs; the Breakwater variant holds longer and is faster
    if not (base["time"] and bw["time"] and bw["time"] < base["time"] and bw["boosted"] > base["boosted"]):
        bad.append(f"I1: base {base}, Breakwater's {bw}")
    # negative control: a release far too late (no release runway, no stick band) clips the weave more than the variant
    late = race(c, dict(BREAKWATER, releaseRunwaySeconds=0.0, rocket=False))
    if not (late["danger"] + late["collar"] > bw["danger"] + bw["collar"]):
        bad.append(f"letting go at the station did not clip more: late {late}, variant {bw}")
    # the rocket opens holes: with it the variant clips no more Danger than without it
    no_rocket = race(c, dict(BREAKWATER, rocket=False))
    if bw["rockets"] == 0 or bw["danger"] > no_rocket["danger"]:
        bad.append(f"the rocket: with {bw}, without {no_rocket}")
    # the nose lead is OFF by default: NoseLead() (gain 0) is the unset knob; a lag-free race has nothing to lead and
    # the knob leaves it alone; with the lag a gain changes the race (the knob is live)
    if race(c, BREAKWATER, nose_lead=NoseLead()) != bw: bad.append("NoseLead() (gain 0) is not the unset lead")
    free = race(c, BREAKWATER, lag=False)
    if race(c, BREAKWATER, lag=False, nose_lead=2.5) != free: bad.append(f"the lead moved a lag-free race: {free}")
    led = race(c, BREAKWATER, nose_lead=2.5)
    if led == bw: bad.append("the nose lead at 2.5 changed nothing (the knob is dead)")
    print("breakwater_sparrow_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


def _job(a):
    name, p, I, s = a[:4]
    return name, I, s, race(course(I, s), p, nose_lead=a[4] if len(a) > 4 else None)


def measure_clips(paths, slowed_bit=8, min_speed=60.0):
    """MEASURED, not modelled: every slowdown in recorded Breakwater flights (HumanFlightRecorder's 10 Hz speed and
    `state` columns; bit 8 = slowed). Returns one row per slowdown from min_speed or faster: (intensity, speed before,
    lowest speed / before within 2 s, seconds the bit stayed set, seconds of travel lost until back to 95% or 5 s).
    Under 0.25 is a Danger weave clip (x0), 0.25-0.75 a Plain collar clip (x0.5)."""
    import json
    rows = []
    for f in paths:
        d = json.load(open(f)); cols = d["columns"]; S_ = d["samples"]
        ti, si, vi = 0, cols.index("state"), cols.index("speed")
        t = [r[ti] for r in S_]; v = [r[vi] for r in S_]; sl = [(int(r[si]) & slowed_bit) > 0 for r in S_]
        for i in range(1, len(t)):
            if not sl[i] or sl[i - 1]: continue
            pre = sorted(v[j] for j in range(i) if t[j] >= t[i] - 0.6) or [v[i - 1]]
            vb = pre[len(pre) // 2]
            if vb < min_speed: continue
            low = min(v[j] for j in range(i, len(t)) if t[j] < t[i] + 2)
            k = i
            while k < len(t) and sl[k]: k += 1
            lost, j = 0.0, i
            while j < len(t) - 1 and t[j] < t[i] + 5:
                lost += max(0.0, 1 - v[j] / vb) * (t[j + 1] - t[j])
                if j > i + 2 and v[j] >= 0.95 * vb: break
                j += 1
            rows.append((d.get("intensity"), vb, low / vb, t[k - 1] - t[i], lost))
    return rows


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    if "--clips" in sys.argv:   # --clips <flight json>...: the measured price of a clip
        rows = measure_clips(sys.argv[sys.argv.index("--clips") + 1:])
        for name, lo, hi in (("Danger (x0)", 0.0, 0.25), ("Plain (x0.5)", 0.25, 0.75)):
            r = sorted(x[4] for x in rows if lo <= x[2] < hi)
            if r: print(f"{name}: {len(r)} clips, seconds lost median {r[len(r) // 2]:.2f} (p25 {r[len(r) // 4]:.2f}, p75 {r[3 * len(r) // 4]:.2f})")
        sys.exit(0)
    from concurrent.futures import ProcessPoolExecutor
    drives = [("base", BASE, None), ("Breakwater", BREAKWATER, None)]
    if "--nose-lead" in sys.argv:   # --nose-lead G[,EXTRA]: each drive again with the AI nose lead on
        lead = NoseLead.from_cli(sys.argv[sys.argv.index("--nose-lead") + 1])
        drives += [(f"{name}+lead{lead.gain:g}", p, lead) for name, p, _ in drives]
    jobs = [(name, p, I, s, nl) for name, p, nl in drives for I in (1, 2, 3, 4) for s in (101, 102, 103)]
    with ProcessPoolExecutor() as ex:
        for name, I, s, r in ex.map(_job, jobs):
            print(f"{name:18} I{I} seed {s}: {r}")
