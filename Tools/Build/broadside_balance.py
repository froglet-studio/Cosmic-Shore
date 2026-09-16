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
    "Rhino": dict(verbs=["strike_sword"], connect=0.45,
        why="the sword is a swung blade with real reach and needs no ammunition; what bounds it "
            "is getting alongside, and the Rhino is the fleet's fastest hull"),
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
    "Rhino":   (1.00, 1.00),   # Time reaches nothing on the ramp
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


def read_target():
    t = _read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
    m = re.search(r"DefaultBroadsidePointTarget\s*=\s*(\d+)", t)
    assert m, "DefaultBroadsidePointTarget not found"
    return int(m.group(1))


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


def points_per_minute(hull, windows, time_level=0.0):
    kit = HULLS[hull]
    rate = sum(verb_rate(v, windows) for v in kit["verbs"])
    ppm = rate * kit["connect"] * 60.0

    # Time's effect is on ENGAGEMENT RATE, not on the hit itself: a faster hull picks and holds
    # more fights. Modelled as a square-root of the speed multiplier - closing faster helps, but
    # not linearly, because a fight is two-sided.
    at_full, at_floor = TIME_REACH[hull]
    mul = 1.0 + (at_full - 1.0) * time_level if time_level >= 0 else 1.0 + (1.0 - at_floor) * (time_level / 0.5)
    return ppm * (mul ** 0.5)


def solve(levels=None):
    windows = read_shipped_windows()
    target = read_target()
    levels = levels or {h: 0.0 for h in HULLS}

    rest = {h: points_per_minute(h, windows, 0.0) for h in HULLS}
    tuned = {h: points_per_minute(h, windows, levels.get(h, 0.0)) for h in HULLS}
    return dict(windows=windows, target=target, rest=rest, tuned=tuned, levels=levels)


def spread(d):
    return max(d.values()) / min(d.values())


def solve_levels():
    """
    Hand the slow hulls Time and take it off the fast ones, within the -0.5..+1 band the platform
    allows. Only five hulls have a Time endpoint at all; the other three are handed nothing and
    the residual is reported rather than hidden.
    """
    windows = read_shipped_windows()
    rest = {h: points_per_minute(h, windows, 0.0) for h in HULLS}
    mid = sorted(rest.values())[len(rest) // 2]

    levels = {}
    for h in HULLS:
        at_full, at_floor = TIME_REACH[h]
        if at_full == 1.0 and at_floor == 1.0:
            levels[h] = 0.0            # Time reaches nothing on this hull - say so, do not fake it
            continue
        levels[h] = 1.0 if rest[h] < mid else (-0.5 if rest[h] > mid else 0.0)
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
