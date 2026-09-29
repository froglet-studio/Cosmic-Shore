#!/usr/bin/env python3
"""
Broadside's offline balance model - the mixed-fleet brawl.

WHAT THIS IS FOR. Regatta had to answer "can every hull finish the lap?"; this has to answer
"can every hull reach the point target?", which is a harder question because the hulls do not
share a weapon. Seven kits land four different verbs at four different rates, so the card cannot
be balanced by eyeballing the price list - the price of a verb and the RATE it can be landed at
are one system, and neither is legible without the other.

THE INSIGHT THE MODEL IS BUILT ON. A weapon's fire rate is almost never what bounds its scoring.
`VesselCombatHitLatch` admits one hit per (shooter, victim, class) per that effect's own
`sameVictimCooldownSeconds`, so against ONE rival a hull's ceiling is

    points/second  =  points_per_hit / latch_window        (x how often it actually connects)

and the Sparrow's 90-rounds-per-second full-auto is capped by a 0.05 s window long before its
trigger is. That is what makes a mixed fleet tractable at all: the windows are the real dial, and
they are read here off the shipped assets rather than assumed.

WHAT IT CANNOT DO, STATED PLAINLY. The per-hull CONNECT FRACTION - how much of an engagement a
hull actually spends with its weapon on a rival - is an ESTIMATE, not a measurement. It is the
one term no static read can supply (it is a function of flight model, arena and pilot), and it
is where this model is weakest. Every fraction below is labelled with its reasoning so a playtest
can correct it by argument rather than by re-deriving the file. The output is a SPREAD, and the
honest claim is only that the spread is small enough that no hull is unplayable - not that the
roster is even.

Run it:  python3 Tools/Build/broadside_balance.py
"""
import os, re, sys, json

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# ── the price list and the windows this mode AUTHORS (the generator asserts these) ───────────
# Both halves live here because they are one system: raising a price without shortening the
# window (or the reverse) changes a hull's rate, not just its worth.
POINTS = {
    "bullet":   1,   # a round that connects - Sparrow tracer, Urchin spike
    "strike":   8,   # a CONTACT hit - Rhino sword, Squirrel joust
    "debuff":  12,   # an area effect that strips elements - Dolphin cone, Scarab plate, Manta bloom
    "missile_shockwave": 10,
    "missile_blast":     20,
    "missile_direct":    30,
}

# Windows the mode's own new effects author. The three that already ship are READ, below, so the
# model can never disagree with them.
# The windows are PER ASSET, not per class, and that is the mode's finest balance lever: two
# hulls sharing the Strike class can still be separated by how OFTEN the class admits, and the
# separation is grounded in the mechanism rather than invented.
NEW_WINDOWS = {
    # A swung blade is in contact CONTINUOUSLY while the Rhino is alongside, so without the
    # longer window it would score every second of a pass it never had to re-earn.
    "strike_sword": 1.4,
    # A joust is DISCRETE - it requires a fresh overtake, at a closing speed the Squirrel has to
    # win each time - so it re-earns its window and is allowed a shorter one.
    "strike_joust": 1.0,
    # A spike volley is ~10 projectiles arriving inside ~0.1 s. Long enough that one volley
    # cannot pay ten times, short enough that a well-aimed volley pays for more than one spike -
    # which is what a charged, committed shot should buy over a held trigger.
    "spike": 0.12,
}

# ── per-hull kit: which verbs it lands, and what fraction of an engagement it lands them ──────
# CONNECT is the estimate (see the header). Each carries the reasoning that set it.
HULLS = {
    "Sparrow": dict(verbs=["bullet", "missile"], connect=0.18,
        why="a 375 u/s round on a manoeuvring hull; rockets are ammo-bound (50 prisms each)"),
    "Urchin": dict(verbs=["spike"], connect=0.30,
        why="a 10-spike shotgun is far more forgiving than a single tracer, but it is a charged "
            "volley on a cooldown rather than a held trigger"),
    "Rhino": dict(verbs=["strike_sword"], connect=0.38,
        why="the sword needs no ammunition and has real reach, but since the playtest it also "
            "requires being FASTER than its victim (requireFasterThanVictim, the Squirrel "
            "joust's own rule) - so a parked blade scores nothing and the Rhino must keep "
            "re-attacking at speed. Above the Squirrel's 0.35 because the Rhino's ramp tops "
            "out far higher, so once wound up it clears the speed bar on more passes"),
    "Squirrel": dict(verbs=["strike_joust"], connect=0.35,
        why="the joust requires being FASTER than the victim, so roughly half of all passes "
            "score nothing - the price of the fleet's cheapest boost economy"),
    "Dolphin": dict(verbs=["debuff"], connect=0.16,
        why="the cone is armed by skimming and fired only by touching a crystal, so scoring is "
            "gated on a resource run rather than on the fight"),
    "Scarab": dict(verbs=["debuff"], connect=0.22,
        why="the plate is a dash on a recharge - frequent, but it sweeps once and is spent"),
    "Manta": dict(verbs=["debuff"], connect=0.20,
        why="a bomb is planted by grazing and pays on the bloom; capacity 3 and a 25 s fuse "
            "make it bursty rather than sustained"),
}

# Time is the only element that reaches a speed on more than one hull, which is why it is the
# handicap axis (see REGATTA.md). Normalized level -0.5..1 -> the multiplier the map applies.
TIME_REACH = {           # multiplier at normalized level +1 / -0.5, per REGATTA's measured table
    "Manta":   (1.30, 0.70),
    "Sparrow": (1.50, 0.50),
    "Serpent": (1.60, 0.25),
    "Scarab":  (1.50, 0.75),
    "Dolphin": (1.20, 0.85),   # fill rate, not speed
    # CORRECTED after the first playtest. An earlier pass recorded "Time reaches nothing on the
    # Rhino", which was FALSE: RampBoostActionExecutor.Begin reads Multiplier(Element.Time) and
    # scales accelerationPerSecond by it. The map's Time slot was an OPEN DESIGN SLOT authored
    # 1.0/1.0, so the hook was live and the asset behind it was flat - a capability that exists
    # in code and is switched off in data reads exactly like a capability that does not exist.
    # Time here is the ramp's WIND-UP RATE, not its top speed: it does not make the Rhino faster,
    # it makes it reach fast sooner, which is what a brawl's short straights actually bound.
    # NOT a speed multiplier - see rhino_speed_multiplier() for the conversion. Stored as the
    # acceleration endpoint the executor actually reads so the model and the asset match.
    "Rhino":   (2.50, 0.50),   # Time -> ramp acceleration (RhinoRampBoostAction 220/s base)
    "Urchin":  (1.00, 1.00),   # the grind is not elemental
    "Squirrel":(1.00, 1.00),   # skim energy is not elemental
}


def _read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


def _key(text, key, label):
    m = re.search(rf"^\s*{re.escape(key)}:\s*(-?[0-9.]+)\s*$", text, re.M)
    assert m, f"{label}: '{key}' not found"
    return float(m.group(1))


def read_shipped_windows():
    """The three latch windows that already ship - read, never assumed."""
    base = "Assets/_SO_Assets/Effects"
    w = {}
    w["bullet"] = _key(_read(f"{base}/Vessel Projectile Effects/VesselCombatHitByBullet.asset"),
                       "sameVictimCooldownSeconds", "bullet")
    w["debuff"] = _key(_read(f"{base}/Vessel Explosion Effects/VesselCombatHitByCrystalBlast.asset"),
                       "sameVictimCooldownSeconds", "debuff")
    w["missile"] = _key(_read(f"{base}/Vessel Explosion Effects/VesselCombatHitByMissileShockwave.asset"),
                        "sameVictimCooldownSeconds", "missile")
    return w


def read_target(team_size=1):
    """
    The point target a DOMAIN races to, which since the first playtest SCALES WITH TEAM SIZE:

        target = perPilot x (1 + extraFraction x (teamSize - 1))

    so 100 / 160 / 220 / 280 for a 1 / 2 / 3 / 4 pilot team. Both numbers are read off the
    shipped C# rather than restated, so the model and the turn monitor cannot disagree.

    The fraction is 0.6 rather than 1.0 deliberately: a second pilot roughly DOUBLES a domain's
    scoring rate (the latch is per shooter-victim pair, so two pilots on one victim really do
    both score), and a target that doubled with them would leave match length flat while making
    every teammate's contribution feel like a rounding error. At 0.6 a bigger team finishes
    somewhat FASTER - which is the reward for filling your side - and the model reports by how
    much rather than hiding it.
    """
    t = _read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
    per = re.search(r"DefaultBroadsidePointsPerPilot\s*=\s*(\d+)", t)
    frac = re.search(r"BroadsideExtraPilotFraction\s*=\s*([0-9.]+)f", t)
    assert per, "DefaultBroadsidePointsPerPilot not found"
    assert frac, "BroadsideExtraPilotFraction not found"
    return int(round(int(per.group(1)) * (1.0 + float(frac.group(1)) * (max(1, team_size) - 1))))


def read_comeback_rate():
    """Off the shipped card when it exists; the generator authors it on the first run."""
    p = os.path.join(ROOT, "Assets/_SO_Assets/Games/ArcadeGameBroadside.asset")
    if not os.path.exists(p):
        return None
    m = re.search(r"ComebackRatePerScoreDeficit:\s*([0-9.]+)", open(p, encoding="utf-8").read())
    return float(m.group(1)) if m else None


def verb_rate(verb, windows):
    """Points per second of CONNECTED time, before the connect fraction."""
    if verb == "bullet":
        return POINTS["bullet"] / windows["bullet"]
    if verb == "spike":
        # A spike is a Bullet-class hit but its own container authors a longer window, because a
        # volley is many projectiles arriving together.
        return POINTS["bullet"] / NEW_WINDOWS["spike"]
    if verb in ("strike_sword", "strike_joust"):
        return POINTS["strike"] / NEW_WINDOWS[verb]
    if verb == "debuff":
        return POINTS["debuff"] / windows["debuff"]
    if verb == "missile":
        # Ammo-bound, not window-bound: 50 hostile prisms buy one rocket and a rocket costs half
        # the tank, so sustained rocket cadence in a brawl is slow. Priced at the shockwave tier,
        # which is the ordinary outcome of a proximity kill.
        ROCKETS_PER_MIN = 2.0
        return POINTS["missile_shockwave"] * ROCKETS_PER_MIN / 60.0
    raise AssertionError(verb)


# A BRAWL'S STRAIGHT. The one number the Rhino conversion below rests on, and the reason its
# Time row is worth anything at all: a dogfight in the Boneyard gives you a couple of seconds of
# clear line before you have to turn, not the ten-second runs a circuit race is made of. Chosen
# from the arena rather than measured, and stated so a later pass can measure it.
BRAWL_STRAIGHT_SECONDS = 2.0


def elemental_multiplier(level, at_full, at_floor):
    """The platform's own anchored-at-rest lerp: 1.0 at level 0, at_full at +1, at_floor at -0.5."""
    if level >= 0:
        return 1.0 + (at_full - 1.0) * level
    return 1.0 + (1.0 - at_floor) * (level / 0.5)


def _rhino_ramp_constants():
    """(cruise, top, base acceleration) for the Rhino, read off the shipped assets.

    Shares REGATTA's reader rather than re-parsing: that model already resolves every hull's
    throttle scaler, minimum speed and the ramp asset by key, and two readers of one prefab is
    two places a retune has to land.
    """
    import regatta_balance as _rb
    c = _rb.read_constants()
    v = c["vessels"]["Rhino"]
    cruise = v["ts"] + v["ms"]                       # plain cruise, ramp disengaged
    top = v["ts"] * c["rhino_max_boost"] + v["ms"]   # ramp held at full straightness
    return cruise, top, c["rhino_accel"]


def rhino_speed_multiplier(level):
    """
    Converts the Rhino's ACCELERATION endpoint into the SPEED multiplier the rest of the model
    is written in, because the two are not the same thing and treating them as one overpays the
    hull badly.

    The ramp climbs at `accelerationPerSecond x Multiplier(Time)` from cruise toward a ceiling
    Time does not move (maxBoostMultiplier stays 24). Over a straight of
    BRAWL_STRAIGHT_SECONDS the mean speed is therefore

        mean = (1/T) INTEGRAL min(top, cruise + a t) dt

    and what Time buys is the ratio of that mean to the mean at rest. It saturates: once the
    hull tops out inside the straight, more acceleration buys nothing further, which is exactly
    the shape the ceiling implies and exactly what a raw 2.5x acceleration ratio would have
    claimed instead. At the shipped numbers (cruise 50, top 1200, a 220/s, T 2 s) the resting
    mean is 270 u/s and +1 Time buys 600 - a 2.22x speed ratio rather than the 2.5x the
    acceleration row reads.

    **Every one of the three constants is READ off the shipped assets**, and that is not
    tidiness - the first cut hardcoded `cruise 60 / top 1210` and was stale within the week,
    because a parallel branch zeroed `Rhino.prefab`'s `DefaultMinimumSpeed` (the one-thumb
    hulls' floor, retired so a two-thumb flier can actually STOP) and moved both numbers by
    10 u/s. Nothing failed; the model simply went on describing a vessel the project no longer
    ships. A constant copied out of an asset is true on the day it is copied.
    """
    cruise, top, base_accel = _rhino_ramp_constants()
    T = BRAWL_STRAIGHT_SECONDS

    def mean_speed(a):
        t_cap = (top - cruise) / a                     # when the ramp reaches the ceiling
        if t_cap >= T:
            return cruise + 0.5 * a * T                # never tops out inside the straight
        area = cruise * t_cap + 0.5 * a * t_cap ** 2   # ramping
        area += top * (T - t_cap)                      # held at the ceiling
        return area / T

    at_full, at_floor = TIME_REACH["Rhino"]
    return mean_speed(base_accel * elemental_multiplier(level, at_full, at_floor)) / \
           mean_speed(base_accel)


def speed_multiplier(hull, time_level):
    """What Time is worth to this hull, expressed as a SPEED ratio for every hull alike."""
    if hull == "Rhino":
        return rhino_speed_multiplier(time_level)
    at_full, at_floor = TIME_REACH[hull]
    return elemental_multiplier(time_level, at_full, at_floor)


def points_per_minute(hull, windows, time_level=0.0):
    kit = HULLS[hull]
    rate = sum(verb_rate(v, windows) for v in kit["verbs"])
    ppm = rate * kit["connect"] * 60.0

    # Time's effect is on ENGAGEMENT RATE, not on the hit itself: a faster hull picks and holds
    # more fights. Modelled as a square-root of the speed multiplier - closing faster helps, but
    # not linearly, because a fight is two-sided.
    return ppm * (speed_multiplier(hull, time_level) ** 0.5)


def solve(levels=None):
    windows = read_shipped_windows()
    target = read_target()
    levels = levels or {h: 0.0 for h in HULLS}

    rest = {h: points_per_minute(h, windows, 0.0) for h in HULLS}
    tuned = {h: points_per_minute(h, windows, levels.get(h, 0.0)) for h in HULLS}
    return dict(windows=windows, target=target, rest=rest, tuned=tuned, levels=levels)


def spread(d):
    return max(d.values()) / min(d.values())


# A hull whose Time level is a PLAYABILITY FLOOR rather than a free balance variable. The
# balance pass may raise one of these and must never lower it.
#
# The Rhino is the only entry and the first playtest is why. Its identity is the full-speed
# straight run, and the ramp takes accelerationPerSecond (220) to climb from ~60 u/s cruise to
# ~1200 - 5.2 SECONDS of near-straight flight, which a brawl's short straights simply do not
# contain, while bleedPerSecond (300) drags the speed back down FASTER than it built. So at Time
# rest the Rhino in this mode is structurally never fast, which is exactly what came back as "I
# played rhino and was not charging full speed and straight to be a crazy fast and scary menace".
# At +0.5 the wind-up is 385/s - about 3 s to top speed, and ~830 u/s out of a 2 s straight -
# which is the pace the hull is supposed to read at.
#
# It is a CARD-level row (SO_ArcadeGame.StartingElements), never a prefab edit: a per-hull
# handicap is a fact about the card, so Headlong's Rhino is untouched.
TIME_FLOOR = {"Rhino": 0.5}


# The element ladder is integers, so a normalized level is a multiple of 0.1 and the solver may
# not propose anything finer - a 0.37 it cannot author is a number that quietly becomes 0.4.
LEVEL_STEP = 0.1
LEVEL_BAND = (-0.5, 1.0)


def solve_levels():
    """
    Pick each hull's starting Time so the tuned points/min sit as close together as the band
    allows. Only six hulls have a Time endpoint at all; the other two are handed nothing and the
    residual is reported rather than hidden.

    This SOLVES rather than bucketing. An earlier pass sorted the hulls around the median and
    handed the lower half +1 and the upper half -0.5, which is not a balance pass - it is a
    coin toss with two faces, and the first playtest is what exposed it: giving the Rhino a real
    Time endpoint flipped it from the slowest scorer straight past every other hull to the
    fastest, because +1 was the only thing the bucket had to offer. Each hull is now moved to
    the level whose tuned rate is nearest the ANCHOR - the median rate of the hulls Time cannot
    reach, which is the part of the roster no handicap can move and therefore the only honest
    thing to converge on.

    A hull in TIME_FLOOR is clamped UP to its floor afterwards - its Time level is answering a
    playability question, not a scoring one, so the balance pass is not allowed to spend it.
    """
    windows = read_shipped_windows()
    rest = {h: points_per_minute(h, windows, 0.0) for h in HULLS}

    fixed = [h for h in HULLS if TIME_REACH[h] == (1.0, 1.0)]
    anchor_pool = sorted(rest[h] for h in fixed) or sorted(rest.values())
    anchor = anchor_pool[len(anchor_pool) // 2]

    lo, hi = LEVEL_BAND
    steps = [round(lo + i * LEVEL_STEP, 2)
             for i in range(int(round((hi - lo) / LEVEL_STEP)) + 1)]

    levels = {}
    for h in HULLS:
        if h in fixed:
            levels[h] = 0.0            # Time reaches nothing on this hull - say so, do not fake it
            continue
        best = min(steps, key=lambda L: abs(points_per_minute(h, windows, L) - anchor))
        if h in TIME_FLOOR:
            best = max(best, TIME_FLOOR[h])
        levels[h] = best
    return levels


def describe():
    levels = solve_levels()
    r = solve(levels)
    tgt = r["target"]

    print("BROADSIDE - mixed-fleet balance model")
    print("=" * 78)
    print(f"point target {tgt}   latch windows "
          f"bullet {r['windows']['bullet']}s  debuff {r['windows']['debuff']}s  "
          f"missile {r['windows']['missile']}s  sword {NEW_WINDOWS['strike_sword']}s  "
          f"joust {NEW_WINDOWS['strike_joust']}s  spike {NEW_WINDOWS['spike']}s")
    print(f"prices  round {POINTS['bullet']}  strike {POINTS['strike']}  debuff {POINTS['debuff']}  "
          f"rocket {POINTS['missile_shockwave']}/{POINTS['missile_blast']}/{POINTS['missile_direct']}")
    print()
    print(f"{'hull':10} {'verbs':16} {'connect':>8} {'pts/min':>9} {'Time':>6} {'tuned':>9} {'min to target':>14}")
    print("-" * 78)
    for h in sorted(HULLS, key=lambda x: -r["tuned"][x]):
        kit = HULLS[h]
        print(f"{h:10} {'+'.join(kit['verbs']):16} {kit['connect']:>8.2f} "
              f"{r['rest'][h]:>9.1f} {levels[h]:>6.2f} {r['tuned'][h]:>9.1f} "
              f"{tgt / r['tuned'][h]:>13.1f}m")
    print("-" * 78)
    print(f"  (minutes above are a SOLO domain racing to {tgt}; a fuller team races to a bigger")
    print("   target but scores proportionally faster - the per-team-size table is below)")
    print()
    print(f"{'team size':>10} {'target':>8} {'fastest hull':>14} {'slowest hull':>14}")
    for n in (1, 2, 3, 4):
        tn = read_target(n)
        fast = max(r["tuned"].values()) * n
        slow = min(r["tuned"].values()) * n
        print(f"{n:>10} {tn:>8} {tn / fast:>13.1f}m {tn / slow:>13.1f}m")
    print()
    print(f"spread at rest  {spread(r['rest']):.2f}x")
    print(f"spread tuned    {spread(r['tuned']):.2f}x")
    print()
    print("Hulls Time cannot reach (residual is structural, not a tuning miss):")
    for h in HULLS:
        if TIME_REACH[h] == (1.0, 1.0):
            print(f"  {h:10} - no elemental endpoint on its weapon or its speed")
    print()
    rate = read_comeback_rate()
    if rate is not None:
        bonus = (tgt / 4.0) * rate
        print(f"comeback: a quarter-of-target deficit ({tgt/4:.0f}) buys {bonus:.2f} element levels "
              f"at rate {rate}")
    return r, levels


if __name__ == "__main__":
    describe()
