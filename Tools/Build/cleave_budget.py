#!/usr/bin/env python3
"""Cleave's arena ladder: the measured baselines and the PhaseThresholds they ride.

**This model measures nothing itself, and that is the point.** Three of Cleave's four arenas
cull prisms with value noise, so an analytic model would have to re-implement that noise - and
the float path into it - to be exact, and would silently become an ESTIMATE the first time
either drifted. Instead ``Tools/Build/cleave_arena_harness`` COMPILES THE SHIPPED GENERATORS and
counts what they emit, writing ``cleave_arena_measurements.json``; this file reads that and
turns it into thresholds.

Two consequences worth knowing:

* Volume is **exact**, not an expectation. The harness sums the real jittered scales rather than
  applying an ``E[k^3]`` factor to nominal ones. The predecessor model (``ribcage_budget.py``)
  applied ``((1.2)**4 - (0.8)**4) / (4*0.2)`` = **2.08** where ``Jit(s, 0.2)`` draws k from
  U(0.8, 1.2) and therefore has ``E[k^3] = 1 + 0.2^2 =`` **1.04** - the divisor used the
  half-width of the range instead of its width. Its own comment said 1.04. Every shipped Cleave
  volume threshold was consequently ~2x the arena's real volume, which put the cell's live
  volume permanently below ``RestlessEnterVolume``: **volume is the spine**
  (``CellPhaseThresholds.Compute`` steps phase on volume and uses count only as a Frenzy
  backstop), so the ladder described a cell twice as heavy as the one that exists and never
  moved. The measured baselines here fix it. ``Tools/Build/wildlife_cage_budget.py`` still
  carries the same expression and is NOT touched here - that is another mode's tuning.
* The ENVELOPE is measured too, not restated, and it is **per intensity**. Each arena row's
  ``outerRadius`` / ``aiStationRadius`` / ``lengthScale`` / ``gapScale`` are the compiled values of
  that rung's ``SliceArenaGeometry`` constants, written by the same run that counted the prisms -
  so this file holds no copy of them to drift. Intensities 1 and 2 are 2,160-radius arenas at
  triple rib spacing; 3 and 4 are 720 at single.
* The measurement can go stale, so it is HASH-GUARDED. Every source that could move a count -
  the four arena generators, the shared envelope, the noise, and the harness's own shims - is
  hashed into the JSON, and :func:`verify` refuses to answer from a measurement whose sources
  have moved. Re-measure with::

      bash Tools/Build/cleave_arena_harness/run.sh

Usage:
    python3 Tools/Build/cleave_budget.py            # print the ladder
    python3 Tools/Build/cleave_budget.py --check    # verify only, no output on success
"""
import hashlib
import json
import math
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MEASUREMENTS = os.path.join(REPO, "Tools/Build/cleave_arena_measurements.json")

# ── Numbers this file OWNS (everything else is measured) ────────────────────────────────
#
# The spawn ring is authored onto the scene's ServerPlayerVesselInitializer
# (spawnRingRadiusFloorByIntensity) and the membrane comes from each cell config's
# MembranePrefab; both are restated here so the ORDERING below can be asserted in one place.
# author_cleave_assets.py writes both from these tables.
#
# PER INTENSITY, because the arenas are: one ring for a 2,160-radius arena and the same ring for
# a 720 one either spawns a pilot inside the big arenas or parks them 3,000 units from a speck.
# Rungs 3 and 4 keep the play-tested 1,050 / 1,200 pair; rungs 1 and 2 are that pair x3, which is
# also the factor their arenas grew by, so the clearances are proportionally identical.
#
# The ring is NOT a pure scale of the ARENA, and that is the one place a scale-up does not simply
# multiply a number: at the 2x pass the MEMBRANE did not scale, so the pre-scale 576 doubled would
# have put players 48 units off the membrane wall. 1,050 keeps the same absolute clearance above
# the AI station (114 units, was 108) and 150 inside the membrane. At 6x the membrane DOES scale
# (CleaveMembrane.prefab, radius 3,600 on rungs 1 and 2), so there the ring is a clean x3.
SPAWN_RING = {1: 3150.0, 2: 3150.0, 3: 1050.0, 4: 1050.0}
MEMBRANE_RADIUS = {1: 3600.0, 2: 3600.0, 3: 1200.0, 4: 1200.0}

# Hostile mass a rung's arena must hold per prism of its own destruction target
# (EndConditionOverridesSO.cleavePrismTargetByIntensity). See check 1.
TARGET_BY_INTENSITY = {1: 1200, 2: 1200, 3: 1500, 4: 1500}
MIN_ARENA_TARGET_MULTIPLE = 4.0
MAX_ARENA_TARGET_MULTIPLE = 12.0

# PhaseThresholds = measured baseline + the standard Blob deltas (Docs/ECOSYSTEM.md §18).
BLOB_DELTAS = dict(re=700, rx=500, fe=3600, fx=3000,
                   rev=11200, rxv=8000, fev=57600, fxv=48000)

# The heaviest arena the mode has ever shipped (PeelTheCage intensity 4, five rinds). The new
# ladder may not exceed it: one box collider per prism means the arena IS the collider count,
# and this number was already an explicit product decision at ~13x the masterplan's per-cell
# target. A ladder that only ever gets LIGHTER than what shipped needs no new sign-off.
HISTORICAL_COLLIDER_CEILING = 20153

# A Rhino trail prism's nominal volume (Rhino.prefab BaseScale 3 x 3 x 0.5). The smallest thing
# that ever has to move this mode's volume ladder, and therefore what check 7 measures the
# ladder's float32 resolution against.
RHINO_TRAIL_PRISM_VOLUME = 3.0 * 3.0 * 0.5

# The cell whose PhaseThresholds these are. Read by check 7, which needs to know whether the
# ladder is LOAD-BEARING (does this cell grow anything?) rather than merely authored.
SPAWN_PROFILE = os.path.join(REPO, "Assets/_SO_Assets/Cell Configs/Cleave Cell/Cleave Spawn Profile.asset")


def load():
    """The measurements, with every hashed source re-checked against the tree."""
    if not os.path.exists(MEASUREMENTS):
        raise SystemExit(
            "Tools/Build/cleave_arena_measurements.json is missing. Re-measure with:\n"
            "  bash Tools/Build/cleave_arena_harness/run.sh")

    with open(MEASUREMENTS, encoding="utf-8") as fh:
        data = json.load(fh)

    stale = []
    for rel, recorded in data["sourceHashes"].items():
        path = os.path.join(REPO, rel)
        if not os.path.exists(path):
            stale.append(f"{rel} (missing)")
            continue
        with open(path, "rb") as fh:
            actual = hashlib.sha256(fh.read()).hexdigest()[:16]
        if actual != recorded:
            stale.append(rel)

    if stale:
        raise SystemExit(
            "The arena measurement is STALE - these sources changed since it was taken:\n  "
            + "\n  ".join(stale)
            + "\n\nRe-measure with:\n  bash Tools/Build/cleave_arena_harness/run.sh")

    return data


def phase_thresholds(count, volume):
    """A cell's own ladder, riding ITS arena's measured baseline.

    Each intensity needs its own block, and the spread is now enormous: the arenas run ~4.6k to
    ~16.4k prisms and 23M to 345M volume (rungs 1 and 2 are 6x the authored radius, so their
    prisms are 6x on every axis and 216x in volume apiece), so one shared threshold set would put
    three of the four cells in the wrong phase from frame one.
    """
    return dict(
        RestlessEnter=count + BLOB_DELTAS['re'], RestlessExit=count + BLOB_DELTAS['rx'],
        FrenzyEnter=count + BLOB_DELTAS['fe'],   FrenzyExit=count + BLOB_DELTAS['fx'],
        RestlessEnterVolume=round(volume + BLOB_DELTAS['rev']),
        RestlessExitVolume=round(volume + BLOB_DELTAS['rxv']),
        FrenzyEnterVolume=round(volume + BLOB_DELTAS['fev']),
        FrenzyExitVolume=round(volume + BLOB_DELTAS['fxv']))


def hostile_floor(a):
    """The hostile prisms the WORST-OFF domain can find in this arena, before any trail is laid.

    Environment mass is hostile by COLOUR (Docs/ECOSYSTEM.md §27 (5)): a prism is friendly iff it
    wears the attacker's own domain, and ``Domains.Blue`` is hostile to everyone. So the floor for
    a domain is everything minus its own colour, and the number that has to clear the target is
    the SMALLEST of those - the domain that happens to own the most of this arena.
    """
    playable = ("Jade", "Ruby", "Gold")
    return min(a["prisms"] - a["byDomain"].get(d, 0) for d in playable)


def verify(data):
    """Everything the ladder promises, asserted. Returns the arena list on success."""
    arenas = data["arenas"]
    standoff = data["aiStationStandoff"]

    assert len(arenas) == 4, f"the ladder is four intensities, got {len(arenas)}"
    for i, a in enumerate(arenas):
        assert a["intensity"] == i + 1, f"{a['key']} is at ladder position {i+1} but says {a['intensity']}"

    # 1. EVERY RUNG HOLDS ITS OWN TARGET, WITH ROOM.
    #
    #    This REPLACES a monotone-prism-count check, and the replacement is the point. That check
    #    was a proxy for "intensity reads as more to destroy", and its own comment said nothing
    #    forced it - which was true while one target served all four rungs. It no longer is: the
    #    target is per-intensity now (1200/1200/1500/1500) because rungs 1 and 2 are vast open
    #    places you cross while 3 and 4 are compact objects you peel, so raw count says nothing
    #    about how long a match runs and the counts are deliberately NOT monotone (the Panes hold
    #    more than the Swell because tripling the rib step takes more from a sheet than from a
    #    road).
    #
    #    What actually has to hold is that a domain can REACH its target off the arena alone. The
    #    band is two-sided on purpose: under it a match leans on trails and fauna to be winnable
    #    at all, over it the arena is mostly scenery and the rungs stop reading as one ladder.
    for a in arenas:
        target = TARGET_BY_INTENSITY[a["intensity"]]
        mult = hostile_floor(a) / target
        assert MIN_ARENA_TARGET_MULTIPLE <= mult <= MAX_ARENA_TARGET_MULTIPLE, (
            f"{a['key']} holds {hostile_floor(a)} hostile prisms for the worst-off domain against "
            f"a target of {target} ({mult:.1f}x) - outside "
            f"{MIN_ARENA_TARGET_MULTIPLE}x..{MAX_ARENA_TARGET_MULTIPLE}x. Move the target in "
            "EndConditionOverridesSO.cleavePrismTargetByIntensity, or the arena.")

    # 2. THE ENVELOPE, PER INTENSITY. Measured on the prism's far CORNER, not its lay point - a
    #    lay at exactly OuterRadius still puts geometry outside it. What has to hold is that the
    #    AI's stations are clear of the mass, or AIPilot (which never arrives, it flies through)
    #    ends up orbiting a point inside the arena forever.
    assert standoff > 1.0, f"AiStationStandoff {standoff} must exceed 1 - see SliceArenaGeometry"
    for a in arenas:
        i = a["intensity"]
        station, ring, membrane = a["aiStationRadius"], SPAWN_RING[i], MEMBRANE_RADIUS[i]
        assert a["maxReachWithExtent"] < station, (
            f"{a['key']} reaches {a['maxReachWithExtent']:.0f}, at or past its AI station radius "
            f"{station:.0f}. Pull the arena in or raise SliceArenaGeometry.OuterRadiusI{i}.")
        # A transcription guard on the row's own two numbers, not an equality: the harness
        # computes both in FLOAT32 (720 x 1.3f is 935.9999, not 936) and prints them rounded.
        assert math.isclose(a["outerRadius"] * standoff, station, rel_tol=1e-4), (
            f"{a['key']}'s station {station:.4f} is not its own radius "
            f"{a['outerRadius']:.4f} x the standoff {standoff}")
        assert station < ring < membrane, (
            f"intensity {i} ordering broken: arena {a['outerRadius']:.0f} < station "
            f"{station:.0f} < spawn ring {ring:.0f} < membrane {membrane:.0f}")

    # 3. NOTHING IS SHIELDED. A super-shielded prism can only be popped by an ENERGIZED blade;
    #    energizing needs the both-triggers stance; an AI never pulls a trigger. So shielded mass
    #    is mass an all-AI domain can never score against, and the mode is scored on destroying
    #    it. (Plain shielded mass merely costs two passes, but it is excluded with its sibling so
    #    the rule is one rule.) See CLEAVE.md.
    for a in arenas:
        for kind in ("Shielded", "SuperShielded"):
            assert a["byKind"][kind] == 0, (
                f"{a['key']} lays {a['byKind'][kind]} {kind} prisms - an AI Rhino can never break "
                "them, so an all-AI domain could not play this arena.")

    # 4. TRAPS ARE SEASONING. Enough to make a pilot read the arena, never enough to make
    #    touching it a mistake - the mode's verb is contact.
    for a in arenas:
        frac = a["byKind"]["Danger"] / a["prisms"]
        assert 0.005 <= frac <= 0.06, f"{a['key']} is {frac:.1%} danger prisms - outside 0.5%..6%"

    # 5. COLLIDER BUDGET. One box collider per prism.
    heaviest = max(a["prisms"] for a in arenas)
    assert heaviest <= HISTORICAL_COLLIDER_CEILING, (
        f"the heaviest arena is {heaviest} prisms, above the {HISTORICAL_COLLIDER_CEILING} this "
        "mode has already shipped. That needs a fresh product decision, not a re-run.")

    # 6. PRE-SIZING. LayCapacity only avoids list growth churn, so it is a soft check - but a
    #    capacity UNDER the real count defeats its whole purpose, and one many times over it is a
    #    pointless allocation on a mobile target.
    for a in arenas:
        assert a["layCapacity"] >= a["prisms"], (
            f"{a['key']} lays {a['prisms']} into a list pre-sized for {a['layCapacity']}")
        assert a["layCapacity"] <= a["prisms"] * 2, (
            f"{a['key']} pre-sizes {a['layCapacity']} for {a['prisms']} prisms")

    # 7. THE VOLUME LADDER MUST BE ABLE TO MOVE - or nothing may depend on it.
    #
    #    Cell.liveVolumeTotal is a float32 RUNNING ACCUMULATOR, so its resolution is the ulp at
    #    the value it currently holds. Scaling an arena 6x makes every prism 216x, and these
    #    baselines reach 345M, where float32's ulp is 32 - four times a Rhino trail prism's whole
    #    volume (4.5). Adding one is a NO-OP: round-to-nearest hands back the same total. So at
    #    intensities 1 and 2 the ladder cannot be moved by laying trail at all, and the cell stays
    #    in Calm for the whole match.
    #
    #    That is harmless TODAY and only because this cell grows nothing (SupportedFloras and
    #    SupportedFaunas are both empty), which is what makes it a gate rather than a comment: the
    #    day somebody gives Cleave a food web, this fails and names the reason, instead of the
    #    fauna silently never escalating. There is no fix inside the thresholds - the resolution
    #    is a property of the BASELINE, so the answer would be a smaller arena, smaller prisms, or
    #    a double accumulator.
    #
    #    The band is stated at HALF an ulp because that is the real cliff: below half an ulp an
    #    add rounds to nothing every time, at half it alternates, and it only accumulates cleanly
    #    once a prism is worth several ulps.
    grows = _cell_grows_anything()
    for a in arenas:
        ulp = _float32_ulp(a["volume"])
        if RHINO_TRAIL_PRISM_VOLUME >= ulp:
            continue  # the ladder can be moved one prism at a time; nothing to prove
        assert not grows, (
            f"{a['key']}'s volume baseline {a['volume']:.0f} gives float32 a resolution of {ulp:g} "
            f"volume units, but a Rhino trail prism is only {RHINO_TRAIL_PRISM_VOLUME:g} - "
            "Cell.liveVolumeTotal cannot register one, so this cell's volume ladder is FROZEN. "
            "That is survivable only while the cell grows nothing, and this spawn profile does. "
            "Shrink the arena or its prisms, or move the ladder off a float32 accumulator.")

    return arenas


def _float32_ulp(x):
    """The gap between consecutive float32 values at ``x`` - i.e. the finest change a float32
    running total can register there. 24-bit significand, so 2**(exponent - 23)."""
    return 2.0 ** (math.floor(math.log2(abs(x))) - 23) if x else 0.0


def _cell_grows_anything():
    """True if Cleave's spawn profile authors any flora or fauna - i.e. if anything in the cell
    reads the phase the volume ladder computes. Parsed rather than assumed: the whole point of
    check 7 is to notice the day this changes."""
    with open(SPAWN_PROFILE, encoding="utf-8") as f:
        text = f.read()
    for key in ("SupportedFloras", "SupportedFaunas"):
        assert key in text, f"{key} not found in {SPAWN_PROFILE} - the profile's shape has changed"
        # "SupportedFloras: []" is the empty form; anything else is a populated list.
        if f"{key}: []" not in text:
            return True
    return False


def ladder():
    """The four arenas with their thresholds attached - what the generator consumes."""
    data = load()
    arenas = verify(data)
    for a in arenas:
        a["thresholds"] = phase_thresholds(a["prisms"], a["volume"])
    return data, arenas


if __name__ == "__main__":
    data, arenas = ladder()

    if "--check" in sys.argv:
        print(f"cleave_budget: {len(arenas)} arenas verified against the shipped sources.")
        sys.exit(0)

    standoff = data["aiStationStandoff"]
    print("envelope, per intensity (arena -> AI station -> spawn ring -> membrane):")
    for a in arenas:
        i = a["intensity"]
        print(f"  {i}  {a['label']:<14} E={a['lengthScale']:g} G={a['gapScale']:g}   "
              f"{a['outerRadius']:.0f}  ->  {a['aiStationRadius']:.0f}  ->  "
              f"{SPAWN_RING[i]:.0f}  ->  {MEMBRANE_RADIUS[i]:.0f}")
    print()

    print(f"{'i':>2}  {'arena':<14}{'prisms':>8}{'volume':>12}{'danger':>8}{'reach':>8}"
          f"{'target':>8}{'x':>6}   composition")
    for a in arenas:
        doms = " ".join(f"{d[:1]}{n}" for d, n in a["byDomain"].items() if n)
        target = TARGET_BY_INTENSITY[a["intensity"]]
        print(f"{a['intensity']:>2}  {a['label']:<14}{a['prisms']:>8}{a['volume']:>12.0f}"
              f"{a['byKind']['Danger']:>8}{a['maxReachWithExtent']:>8.0f}{target:>8}"
              f"{hostile_floor(a) / target:>6.1f}   {doms}")

    print("\nPhaseThresholds (baseline + Blob deltas), per intensity:")
    for a in arenas:
        t = a["thresholds"]
        print(f"  {a['intensity']}  {a['label']:<14} count  {t['RestlessEnter']}/{t['RestlessExit']}"
              f"  {t['FrenzyEnter']}/{t['FrenzyExit']}")
        print(f"     {'':<14} volume {t['RestlessEnterVolume']}/{t['RestlessExitVolume']}"
              f"  {t['FrenzyEnterVolume']}/{t['FrenzyExitVolume']}")
