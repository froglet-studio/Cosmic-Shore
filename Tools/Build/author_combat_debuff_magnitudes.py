#!/usr/bin/env python3
"""
The fleet's combat elemental-drain table - one magnitude per attack, DERIVED from what
Broadside prices that attack at.

THE RULE, in the unit the player reads:

    TEN POINTS IS ONE PETAL, on every element the hit touches.

A petal is one integer element level (the flower steps one colour per level), which is 0.1 in
the normalized units `ResourceSystem.ApplyElementalEffect` takes. So a direct rocket strike (30
points) drains three petals from each element; ten bullets (1 point each) drain one. Stated as
code:

    per-element magnitude (normalized) = points / 10 * 0.1 = points x 0.01

PER ELEMENT, NOT PER TOTAL - and this is the half that changed. The table used to hold the TOTAL
over the elements an attack touches proportional to its points, so the Manta's two-element bloom
bit twice as deep per element as the Dolphin's four-element cone and the two landed the same
sum. That is a defensible rule and it is not the spec: "three petals in each element" is a
statement about each element, so how WIDE an attack spreads its drain is now part of what the
attack is, not something normalised away. The Manta's bloom consequently bites the same per
element as the cone and half as much in total, which is what a two-element weapon should do.

THE DRAIN INHERITS THE BALANCE THE PRICE ALREADY HAS. `broadside_balance.py` flattened POINTS
PER SECOND across the seven hulls to a 1.33x spread by tuning the latch windows. Drain-per-second
is (hits/s) x magnitude x duration/2, and magnitude is k x points, so drain-per-second is
k x (duration/2) x points-per-second - the same 1.33x spread, for free. Tying the two together is
what makes the drain balanced without a second balance pass.

WHERE A DRAIN LIVES - two places, and they must stay DISJOINT.

  1. `CombatHitDrain` (C#) drains straight off the HIT REPORT, for every class whose bite is a
     pure function of its price: Bullet and the three missile tiers. It has to be there rather
     than on a per-blast asset because ONE rocket lands in three ranked classes against one
     victim, and a per-asset drain would stack all three - six petals for a thirty-point event.
     `VesselCombatHitLatch` already pays the best tier once and reports what an upgrade
     SUPERSEDES; draining from that same seam, with the same difference, makes the bite and the
     score agree by construction.

  2. The per-weapon assets below, for the classes whose drain carries design a price cannot
     express: which elements it touches (the Manta's bloom is Mass and Space only - the rule
     that bars an overtaker from touching Time) and whether it mirrors as an ally BUFF (the
     Squirrel's overtake). Those are the Debuff and Strike classes.

A class drawing from both would bite twice for one hit, so this script asserts the two sets are
disjoint and that the price list `CombatHitDrain` restates matches the shipped scoring rule.

WHAT MOVED, AND WHAT IT COSTS. Every shipped magnitude was flat -0.5 per element whatever the
attack - which in these units is FIVE petals, a quarter of the whole [-5, +15] element band, from
one graze. Under the rule above the Debuff class lands on -0.12 (1.2 petals). So the Dolphin's
crystal cone and the Scarab's cavitation plate bite 4.2x LESS than they shipped, and The Bends
and Undertow - the two modes scored ENTIRELY on that drain - will feel correspondingly lighter.
Their SCORING is untouched (both pay for the hit landing, not for its depth, via
`requireDebuffableVictim`), so this is a feel change and wants a playtest. It is deliberate: the
old numbers were not derived from anything, and a spec that prices a 30-point centre-punch at
three petals cannot also price a 12-point graze at five.

OWNERSHIP. This file owns `debuffMagnitude` / `buffMagnitude` on the assets listed below and
nothing else on them. One of those is authored WHOLE by another generator
(`author_manta_kit_assets.py`, which writes MantaBombDebuffByExplosionEffect.asset from scratch),
so that script READS the live magnitude back instead of restating it - two generators owning one
field means whichever ran last wins and the loser's --check reports a drift belonging to nobody's
change. If you add a drain asset that another generator authors, do the same there.

Run it:   python3 Tools/Build/author_combat_debuff_magnitudes.py [--check]
"""
import os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

SCORING_RULE = "Assets/_SO_Assets/Scoring Rules/BroadsideScoringRule.asset"
DRAIN_CS     = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/CombatHitDrain.cs"

# THE rule. Ten points to the petal; one petal is 0.1 normalized (ResourceSystem.IncrementLevel).
POINTS_PER_LEVEL   = 10.0
NORMALIZED_PER_LEVEL = 0.1
PER_POINT = NORMALIZED_PER_LEVEL / POINTS_PER_LEVEL      # 0.01 normalized per point

# The classes CombatHitDrain declines because their drain is authored per weapon below. Asserted
# against the C# so the two can never both cover a class (double bite) or both skip one (none).
PER_WEAPON_VERBS = {"debuff", "strike"}

# Every per-weapon drain asset, with the verb Broadside prices it at. The element count is READ
# from the asset (an absent `elements:` key means the C# initializer: all four) and is reported
# but no longer divides the magnitude - see the header.
DRAINS = [
    # (asset path, verb, magnitude field, optional mirrored buff field, duration field, note)
    ("Assets/_SO_Assets/Effects/Vessel Explosion Effects/ScarabCavitationDebuffByExplosionEffect.asset",
     "debuff", "debuffMagnitude", None, "debuffDuration",
     "Dolphin crystal cone + Scarab cavitation plate (shared asset - The Bends / Undertow)"),
    ("Assets/_SO_Assets/Effects/Vessel Explosion Effects/MantaBombDebuffByExplosionEffect.asset",
     "debuff", "debuffMagnitude", None, "debuffDuration",
     "Manta Kabloom bloom (Mass + Space only)"),
    ("Assets/_SO_Assets/Effects/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffect.asset",
     "strike", "debuffMagnitude", "buffMagnitude", "effectDuration",
     "Squirrel joust overtake (the ally BUFF mirrors the debuff)"),
]

# Verbs that SCORE and still carry no drain path at all. Reported, never authored.
UNARMED = []

# The REPORTER assets, per reporter-driven class: the drain's rate limiter is the LATCH WINDOW
# those assets author, because the latch is what admits the hit the drain rides on. The three
# missile classes SHARE one latch key (VesselCombatHitLatch.Key folds them), so they are priced
# as one family at the dearest tier and the tightest window.
REPORTERS = {
    "bullet": ["Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByBullet.asset",
               "Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitBySpike.asset"],
    "missile": ["Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByMissileShockwave.asset",
                "Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByMissileBlast.asset",
                "Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByMissileDirect.asset"],
}

# Guards. A drain is a decaying effect and they STACK (ResourceSystem.ApplyElementalEffect adds an
# entry per call), so what a saturating attacker holds a victim at is
#     sustained = |magnitude| x duration / (2 x rate limit)
# normalized on each element it touches. The band is [-0.5, +1.5], so a sustained past 0.5 can
# pin a pilot on the floor. Both ceilings are stated against what the rule can actually produce
# rather than against the band - the dearest hit is 30 points = 0.30 per element - and both are
# REACHABLE by a plausible edit, which is the only kind of ceiling worth having:
#   - per-element fires if a verb is repriced past 50 points;
#   - sustained fires if a decay is lengthened or a latch window tightened (the Sparrow's
#     full-auto window is 0.05s and already holds 0.40 of it).
MAX_PER_ELEMENT = 0.50    # five petals from ONE hit - past this a graze is a level reset
MAX_SUSTAINED   = 1.50    # fifteen petals HELD by a saturating attacker


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
    for verb, field_name in want.items():
        m = re.search(rf"^\s*{field_name}:\s*(-?\d+)\s*$", txt, re.M)
        if not m:
            sys.exit(f"FAIL  {SCORING_RULE}: no '{field_name}:' - the price list moved, "
                     f"so the drain table cannot be derived")
        out[verb] = int(m.group(1))
    return out


# CombatHitDrain.PointsFor's own switch, by the enum member each verb corresponds to.
CS_CLASS_FOR_VERB = {
    "bullet": "Bullet", "strike": "Strike", "debuff": "Debuff",
    "missile_shockwave": "MissileShockwave", "missile_blast": "MissileBlast",
    "missile_direct": "MissileDirect",
}


def assert_cs_table(pts):
    """The C# restates the price list and the per-weapon set. Prove both against the assets."""
    txt = read(DRAIN_CS)

    for verb, member in CS_CLASS_FOR_VERB.items():
        m = re.search(rf"CombatHitClass\.{member}\s*=>\s*(\d+),", txt)
        if not m:
            sys.exit(f"FAIL  {DRAIN_CS}: PointsFor has no arm for CombatHitClass.{member}")
        if int(m.group(1)) != pts[verb]:
            sys.exit(f"FAIL  {DRAIN_CS}: PointsFor({member}) = {m.group(1)} but "
                     f"{SCORING_RULE} prices it {pts[verb]}. The drain would not track the score.")

    m = re.search(r"DrainAuthoredPerWeapon\(CombatHitClass hitClass\) =>\s*(.+?);", txt, re.S)
    if not m:
        sys.exit(f"FAIL  {DRAIN_CS}: no DrainAuthoredPerWeapon - cannot prove the two drain "
                 f"sources are disjoint")
    declared = {CS_CLASS_FOR_VERB[v] for v in PER_WEAPON_VERBS}
    found = set(re.findall(r"CombatHitClass\.(\w+)", m.group(1)))
    if found != declared:
        sys.exit(f"FAIL  {DRAIN_CS}: DrainAuthoredPerWeapon covers {sorted(found)} but this "
                 f"table authors {sorted(declared)}. A class in both bites twice for one hit; "
                 f"a class in neither never bites.")

    for const, want in (("PointsPerLevel", POINTS_PER_LEVEL),
                        ("NormalizedPerLevel", NORMALIZED_PER_LEVEL)):
        m = re.search(rf"public const float {const} = ([\d.]+)f;", txt)
        if not m or abs(float(m.group(1)) - want) > 1e-9:
            sys.exit(f"FAIL  {DRAIN_CS}: {const} is {m.group(1) if m else 'missing'}, "
                     f"this table derives from {want}")


def cs_duration_seconds():
    m = re.search(r"public const float DurationSeconds = ([\d.]+)f;", read(DRAIN_CS))
    if not m:
        sys.exit(f"FAIL  {DRAIN_CS}: no DurationSeconds - cannot price the sustained drain")
    return float(m.group(1))


def reporter_window(path):
    m = re.search(r"^\s*sameVictimCooldownSeconds:\s*([\d.]+)\s*$", read(path), re.M)
    if not m:
        sys.exit(f"FAIL  {path}: no 'sameVictimCooldownSeconds:' - the latch window IS the "
                 f"drain's rate limit for a reporter-driven class, so it cannot be priced")
    return float(m.group(1))


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
    assert_cs_table(pts)

    # Reporter-driven classes: priced here because nothing else prices them, and because the
    # sustained figure is the only bound they have (the latch window, not an asset cooldown).
    dur = cs_duration_seconds()
    reporter_rows = []
    for family, paths in REPORTERS.items():
        window = min(reporter_window(p) for p in paths)
        verbs = (["bullet"] if family == "bullet"
                 else ["missile_shockwave", "missile_blast", "missile_direct"])
        worst = max(pts[v] for v in verbs) * PER_POINT
        sustained = worst * dur / (2.0 * window)
        if sustained > MAX_SUSTAINED + 1e-6:
            sys.exit(f"FAIL  {family}: a saturating attacker holds {sustained:.3f} over the "
                     f"{MAX_SUSTAINED} ceiling ({worst:.4f} x {dur}s / 2x{window}s latch window)")
        reporter_rows.append((family, verbs, window, worst, sustained))

    rows, drift = [], []
    for path, verb, mag_field, buff_field, dur_field, note in DRAINS:
        if verb not in PER_WEAPON_VERBS:
            sys.exit(f"FAIL  {path} authors the '{verb}' class, which CombatHitDrain also "
                     f"drains - one hit would bite twice.")
        txt = read(path)
        n = element_count(txt)
        per = pts[verb] * PER_POINT
        asset_dur = field(txt, dur_field)
        cd = field(txt, "cooldown") or 1.0
        sustained = per * asset_dur / (2.0 * cd)

        if per > MAX_PER_ELEMENT + 1e-6:
            sys.exit(f"FAIL  {path}: per-element {per:.4f} over the {MAX_PER_ELEMENT} ceiling")
        if sustained > MAX_SUSTAINED + 1e-6:
            sys.exit(f"FAIL  {path}: saturated sustained drain {sustained:.3f} over the "
                     f"{MAX_SUSTAINED} ceiling ({per:.4f} x {asset_dur}s / 2x{cd}s cooldown)")

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

        rows.append((os.path.basename(path)[:-6], verb, pts[verb], n, per * n, per,
                     asset_dur, cd, sustained, note))

    # NOTE: there is deliberately no "a dearer hit must bite harder" assert. per-element =
    # points x k is monotone in points BY CONSTRUCTION, so such a check can never fire - and a
    # check nobody has watched fail is a check nobody should trust. One was written here and
    # removed after its negative control came back green. The asserts that CAN fire are the two
    # ceilings, the price list losing a field, and the C# table drifting from either the prices
    # or the per-weapon set.

    print("combat elemental-drain table   "
          f"{POINTS_PER_LEVEL:.0f} points = 1 petal = {NORMALIZED_PER_LEVEL} normalized "
          f"({PER_POINT:.4f} per point)\n")
    print(f"  DRAWN FROM THE HIT REPORT (CombatHitDrain, C#, decay {dur:.0f}s):")
    print(f"      {'class':<19}{'pts':>4}{'per-el':>9}  petals")
    for verb in ("bullet", "missile_shockwave", "missile_blast", "missile_direct"):
        per = pts[verb] * PER_POINT
        print(f"      {verb:<19}{pts[verb]:>4}{-per:>9.4f}  {-pts[verb] / POINTS_PER_LEVEL:>6.1f}")
    print("      (missile tiers are NETTED against the tier they supersede, so one rocket "
          "bites its best tier only)")
    for family, verbs, window, worst, sustained in reporter_rows:
        print(f"      saturating {family:<10} latch window {window:>5.2f}s  worst per-el "
              f"{-worst:.4f}  ->  sustained {-sustained:.3f}")
    print()

    print("  AUTHORED PER WEAPON (this script):")
    print(f"  {'asset':<42}{'verb':<9}{'pts':>4}{'el':>4}{'per-el':>9}{'petals':>8}"
          f"{'dur':>6}{'cd':>5}{'sustained':>11}")
    for r in rows:
        print(f"  {r[0]:<42}{r[1]:<9}{r[2]:>4}{r[3]:>4}{-r[5]:>9.4f}"
              f"{-r[2] / POINTS_PER_LEVEL:>8.1f}{r[6]:>6.1f}{r[7]:>5.1f}{-r[8]:>10.3f}")
    for r in rows:
        print(f"      {r[0]}: {r[9]}")

    if UNARMED:
        print("\n  verbs that SCORE and carry no drain path (reported, not authored):")
        for verb, who, why in UNARMED:
            per = pts[verb] * PER_POINT
            print(f"      {verb:<19}{pts[verb]:>3}pts -> would be {-per:7.4f} per element   {who}")
            print(f"      {'':<19}     {why}")
    else:
        print("\n  every scoring verb now carries a drain path.")

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
