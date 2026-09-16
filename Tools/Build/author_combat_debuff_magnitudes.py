#!/usr/bin/env python3
"""
The fleet's combat elemental-drain table - one magnitude per attack, DERIVED from what
Broadside prices that attack at.

WHY THIS FILE EXISTS. Broadside priced each VERB by how hard it is to land (a round 1, a
contact strike 8, an area debuff 12, a rocket 10/20/30 - `broadside_balance.py` is the
derivation). The elemental drain those same attacks deliver was NOT derived from anything: every
shipped drain was a flat -0.5 on every element it touched, whatever the attack. So a Manta bloom
and a Dolphin cone were priced identically at 12 and the bloom bit HALF as deep (it drains two
elements, not four), and a Sparrow warhead grazing a pilot for 10 points drained exactly as hard
as a cone worth 12. The price list said one thing and the weapons did another.

THE RULE. A hit's BITE tracks its PRICE:

    total drain (levels, summed over the elements it touches)  =  points x LEVELS_PER_POINT
    per-element magnitude                                      =  total / element_count

Two things follow, and the second is the reason to do it this way rather than by eye.

  1. TOTAL, not per-element, is what is proportional. A 12-point hit is worth 12 points of bite
     however it spreads them, so the Manta's two-element bloom drains twice as deep per element
     as the Dolphin's four-element cone and the two land the same total. Per-element
     proportionality would have left the Manta permanently under-delivering for its price.

  2. THE DRAIN INHERITS THE BALANCE THE PRICE ALREADY HAS. `broadside_balance.py` flattened
     POINTS PER SECOND across the seven hulls to a 1.33x spread by tuning the latch windows.
     Drain-per-second is (hits/s) x magnitude x duration/2, and magnitude is now k x points, so
     drain-per-second is k x (duration/2) x points-per-second - the same 1.33x spread, for free.
     Tying the two together is what makes the drain balanced without a second balance pass.

THE ANCHOR IS A PLAY-TESTED NUMBER, NOT A CHOICE. The Debuff class (12 points) keeps the shipped
-0.5 x 4 elements exactly, so `ScarabCavitationDebuffByExplosionEffect` - the asset the Dolphin's
crystal cone and the Scarab's cavitation plate SHARE - does not move a digit. That matters more
than anywhere else on this list: The Bends and Undertow are scored ENTIRELY on that drain
(`requireDebuffableVictim`), so the two modes whose whole objective is a debuff are unaffected by
construction. Everything else is measured off it. The check below FAILS if that anchor drifts.

WHAT THIS PASS DELIBERATELY DOES NOT DO. Three scoring verbs have no drain path at all - a
Sparrow/Urchin round (Bullet), the Rhino's sword (Strike), and the skyburst's BLAST and DIRECT
tiers, which fold onto the shockwave's drain and add nothing of their own. Giving them one is
arming a weapon with a new property in five shipped modes, not tuning a number, so it is
REPORTED here with the magnitude each would take rather than done silently. Run the script to
see that report.

OWNERSHIP. This file owns `debuffMagnitude` / `buffMagnitude` on every asset listed below and
nothing else on them. One of those assets is authored WHOLE by another generator
(`author_manta_kit_assets.py`, which writes MantaBombDebuffByExplosionEffect.asset from scratch),
so that script READS the live magnitude back instead of restating it - two generators owning one
field means whichever ran last wins and the loser's --check reports a drift belonging to nobody's
change. If you add a drain asset that another generator authors, do the same there.

Run it:   python3 Tools/Build/author_combat_debuff_magnitudes.py [--check]
"""
import os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

SCORING_RULE = "Assets/_SO_Assets/Scoring Rules/BroadsideScoringRule.asset"

# The anchor: the Debuff class keeps the shipped -0.5 on each of its four elements.
ANCHOR_VERB      = "debuff"
ANCHOR_TOTAL     = 2.0      # 4 elements x 0.5 - what ships today, play-tested in The Bends
ANCHOR_PER_ELEM  = 0.5

# Every drain asset that exists today, with the verb Broadside prices it at. The element count is
# READ from the asset (an absent `elements:` key means the C# initializer: all four), so a future
# re-authoring of which elements an attack touches re-derives its magnitude instead of silently
# changing how hard it bites.
DRAINS = [
    # (asset path, verb, magnitude field, optional mirrored buff field, duration field)
    ("Assets/_SO_Assets/Effects/Vessel Explosion Effects/ScarabCavitationDebuffByExplosionEffect.asset",
     "debuff", "debuffMagnitude", None, "debuffDuration",
     "Dolphin crystal cone + Scarab cavitation plate (shared asset - THE ANCHOR)"),
    ("Assets/_SO_Assets/Effects/Vessel Explosion Effects/MantaBombDebuffByExplosionEffect.asset",
     "debuff", "debuffMagnitude", None, "debuffDuration",
     "Manta Kabloom bloom (Mass + Space only)"),
    ("Assets/_SO_Assets/Effects/Vessel Explosion Effects/MissileWarheadDebuffByExplosionEffect.asset",
     "missile_shockwave", "debuffMagnitude", None, "debuffDuration",
     "Sparrow skyburst warhead shockwave"),
    ("Assets/_SO_Assets/Effects/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffect.asset",
     "strike", "debuffMagnitude", "buffMagnitude", "effectDuration",
     "Squirrel joust overtake (the ally BUFF mirrors the debuff)"),
]

# Verbs that SCORE and carry no drain path. Reported, never authored - see the header.
UNARMED = [
    ("bullet", "Sparrow full-auto + turret prism rounds, Urchin spike",
     "projectile family has no elemental-drain effect SO"),
    ("strike", "Rhino energised sword",
     "skimmer family's only drain SO is the Squirrel's overtake, which gates on being FASTER"),
    ("missile_blast", "Sparrow skyburst prism blast",
     "SkyBurstExplosionImpactorDataContainer carries the reporter only"),
    ("missile_direct", "Sparrow skyburst direct strike",
     "SparrowSkyBurstProjectileImpactContainer carries the reporter only"),
]

# Guards. A drain is a decaying effect and they STACK (ResourceSystem.ApplyElementalEffect adds an
# entry per call), so what a saturating attacker holds a victim at is
#     sustained = |magnitude| x duration / (2 x cooldown)
# levels on each element it touches. The element band is [-5, +15]; these ceilings keep even a
# fully saturated stream well clear of the -5 floor, so a drain can never lock a pilot out.
MAX_PER_ELEMENT = 1.5
MAX_SUSTAINED   = 3.0


def read(path):
    with open(os.path.join(ROOT, path), "r", encoding="utf-8") as fh:
        return fh.read()


def points_table():
    """The price list, read from the SHIPPED scoring rule so the two can never disagree."""
    txt = read(SCORING_RULE)
    want = {
        "bullet": "bulletPoints", "strike": "strikePoints", "debuff": "debuffPoints",
        "missile_shockwave": "missileShockwavePoints",
        "missile_blast": "missileBlastPoints", "missile_direct": "missileDirectPoints",
    }
    out = {}
    for verb, field in want.items():
        m = re.search(rf"^\s*{field}:\s*(-?\d+)\s*$", txt, re.M)
        if not m:
            sys.exit(f"FAIL  {SCORING_RULE}: no '{field}:' - the price list moved, "
                     f"so the drain table cannot be derived")
        out[verb] = int(m.group(1))
    return out


def element_count(txt):
    """How many elements this asset drains. No `elements:` key = the C# initializer, all four."""
    m = re.search(r"^  elements:\s*\n((?:  - \d+\s*\n)*)", txt, re.M)
    if not m:
        return 4
    n = len(re.findall(r"- (\d+)", m.group(1)))
    return n if n else 4


def field(txt, name):
    m = re.search(rf"^\s*{name}:\s*(-?[\d.]+)\s*$", txt, re.M)
    return float(m.group(1)) if m else None


def set_field(txt, name, value):
    return re.sub(rf"^(\s*{name}:\s*)-?[\d.]+\s*$", rf"\g<1>{value}", txt, count=1, flags=re.M)


def fmt(v):
    """Unity reads a plain decimal; trim so an integral value stays integral."""
    s = f"{v:.4f}".rstrip("0").rstrip(".")
    return s if s not in ("", "-") else "0"


def main():
    check = "--check" in sys.argv
    pts = points_table()

    levels_per_point = ANCHOR_TOTAL / pts[ANCHOR_VERB]

    rows, drift = [], []
    for path, verb, mag_field, buff_field, dur_field, note in DRAINS:
        txt = read(path)
        n = element_count(txt)
        total = pts[verb] * levels_per_point
        per = total / n
        dur = field(txt, dur_field)
        cd = field(txt, "cooldown") or 1.0
        sustained = per * dur / (2.0 * cd)

        if per > MAX_PER_ELEMENT + 1e-6:
            sys.exit(f"FAIL  {path}: per-element {per:.4f} over the {MAX_PER_ELEMENT} ceiling")
        if sustained > MAX_SUSTAINED + 1e-6:
            sys.exit(f"FAIL  {path}: saturated sustained drain {sustained:.2f} levels over the "
                     f"{MAX_SUSTAINED} ceiling ({per:.4f} x {dur}s / 2x{cd}s cooldown)")

        want = {mag_field: fmt(-per)}
        if buff_field:
            want[buff_field] = fmt(per)

        have = {k: field(txt, k) for k in want}
        changed = any(abs((have[k] or 0.0) - float(want[k])) > 1e-6 for k in want)
        if changed:
            drift.append((path, {k: (have[k], want[k]) for k in want}))
            if not check:
                for k, v in want.items():
                    txt = set_field(txt, k, v)
                with open(os.path.join(ROOT, path), "w", encoding="utf-8") as fh:
                    fh.write(txt)

        rows.append((os.path.basename(path)[:-6], verb, pts[verb], n, total, per,
                     dur, cd, sustained, note))

    # The anchor must not have moved - it is what every other row is measured against, and it is
    # the entire scoring event of two shipped modes.
    anchor = [r for r in rows if "ScarabCavitation" in r[0]][0]
    if abs(anchor[5] - ANCHOR_PER_ELEM) > 1e-6:
        sys.exit(f"FAIL  the anchor moved: the Debuff class now derives {anchor[5]:.4f} per element "
                 f"against the shipped {ANCHOR_PER_ELEM}. The Bends and Undertow are scored on that "
                 f"asset - re-derive ANCHOR_TOTAL deliberately or put the price back.")

    # NOTE: there is deliberately no "a dearer hit must bite harder" assert. total = points x k
    # is monotone in points BY CONSTRUCTION, so such a check can never fire - and a check nobody
    # has watched fail is a check nobody should trust. One was written here and removed after its
    # negative control came back green. The asserts above are the ones that CAN fire: the anchor
    # drifting, a per-element magnitude or a saturated sustained drain past its ceiling, and the
    # price list losing a field.

    print("combat elemental-drain table   "
          f"anchor: {ANCHOR_VERB} {pts[ANCHOR_VERB]}pts = {ANCHOR_TOTAL} levels total "
          f"({levels_per_point:.4f} levels/point)\n")
    print(f"  {'asset':<42}{'verb':<19}{'pts':>4}{'el':>4}{'total':>8}{'per-el':>9}"
          f"{'dur':>6}{'cd':>5}{'sustained':>11}")
    for r in rows:
        print(f"  {r[0]:<42}{r[1]:<19}{r[2]:>4}{r[3]:>4}{r[4]:>8.3f}{-r[5]:>9.4f}"
              f"{r[6]:>6.1f}{r[7]:>5.1f}{-r[8]:>10.2f}L")
    for r in rows:
        print(f"      {r[0]}: {r[9]}")

    print("\n  verbs that SCORE and carry no drain path (reported, not authored):")
    for verb, who, why in UNARMED:
        would = pts[verb] * levels_per_point
        print(f"      {verb:<19}{pts[verb]:>3}pts -> would be {-would:7.3f} levels total   {who}")
        print(f"      {'':<19}     {why}")

    if drift:
        if check:
            print("\nFAIL  drain assets differ from the derived table:")
            for path, d in drift:
                for k, (have, want) in d.items():
                    print(f"      {path}\n          {k}: {have} -> {want}")
            sys.exit(1)
        print("\n  wrote:")
        for path, d in drift:
            for k, (have, want) in d.items():
                print(f"      {path}  {k}: {have} -> {want}")
    else:
        print("\nOK  every drain asset matches the derived table"
              + ("" if check else " (nothing to write)"))


if __name__ == "__main__":
    main()
