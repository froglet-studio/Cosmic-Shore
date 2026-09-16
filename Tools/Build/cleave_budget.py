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
* The ENVELOPE is measured too, not restated. ``outerRadius`` and ``aiStationStandoff`` in the
  JSON are the compiled values of ``SliceArenaGeometry``'s own constants, written by the same run
  that counted the prisms - so this file holds no copy of them to drift.
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
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MEASUREMENTS = os.path.join(REPO, "Tools/Build/cleave_arena_measurements.json")

# ── Numbers this file OWNS (everything else is measured) ────────────────────────────────
#
# The spawn ring is authored onto the scene's ServerPlayerVesselInitializer and the membrane
# comes from the cell config's MembranePrefab; both are restated here so the ORDERING below can
# be asserted in one place. author_cleave_assets.py writes the ring from this constant.
SPAWN_RING = 576.0
MEMBRANE_RADIUS = 1200.0

# PhaseThresholds = measured baseline + the standard Blob deltas (Docs/ECOSYSTEM.md §18).
BLOB_DELTAS = dict(re=700, rx=500, fe=3600, fx=3000,
                   rev=11200, rxv=8000, fev=57600, fxv=48000)

# The heaviest arena the mode has ever shipped (PeelTheCage intensity 4, five rinds). The new
# ladder may not exceed it: one box collider per prism means the arena IS the collider count,
# and this number was already an explicit product decision at ~13x the masterplan's per-cell
# target. A ladder that only ever gets LIGHTER than what shipped needs no new sign-off.
HISTORICAL_COLLIDER_CEILING = 20153


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

    Each intensity needs its own block: the arenas run 11,021 to 16,423 prisms and 2.43M to
    4.06M volume, so one shared threshold set would put three of the four cells in the wrong
    phase from frame one.
    """
    return dict(
        RestlessEnter=count + BLOB_DELTAS['re'], RestlessExit=count + BLOB_DELTAS['rx'],
        FrenzyEnter=count + BLOB_DELTAS['fe'],   FrenzyExit=count + BLOB_DELTAS['fx'],
        RestlessEnterVolume=round(volume + BLOB_DELTAS['rev']),
        RestlessExitVolume=round(volume + BLOB_DELTAS['rxv']),
        FrenzyEnterVolume=round(volume + BLOB_DELTAS['fev']),
        FrenzyExitVolume=round(volume + BLOB_DELTAS['fxv']))


def verify(data):
    """Everything the ladder promises, asserted. Returns the arena list on success."""
    arenas = data["arenas"]
    outer = data["outerRadius"]
    standoff = data["aiStationStandoff"]
    station = outer * standoff

    assert len(arenas) == 4, f"the ladder is four intensities, got {len(arenas)}"
    for i, a in enumerate(arenas):
        assert a["intensity"] == i + 1, f"{a['key']} is at ladder position {i+1} but says {a['intensity']}"

    # 1. INTENSITY READS AS MORE. The four arenas are different PLACES rather than four sizes of
    #    one, so nothing forces this - but a ladder whose prism count wandered up and down would
    #    make "intensity" mean nothing to a player choosing a rung.
    counts = [a["prisms"] for a in arenas]
    assert counts == sorted(counts), f"prism counts are not monotone across the ladder: {counts}"

    # 2. THE SHARED ENVELOPE. Measured on the prism's far CORNER, not its lay point - a lay at
    #    exactly OuterRadius still puts geometry outside it. What has to hold is that the AI's
    #    stations are clear of the mass, or AIPilot (which never arrives, it flies through) ends
    #    up orbiting a point inside the arena forever.
    assert standoff > 1.0, f"AiStationStandoff {standoff} must exceed 1 - see SliceArenaGeometry"
    for a in arenas:
        assert a["maxReachWithExtent"] < station, (
            f"{a['key']} reaches {a['maxReachWithExtent']:.0f}, at or past the AI station radius "
            f"{station:.0f}. Pull the arena in or move SliceArenaGeometry.OuterRadius.")
    assert station < SPAWN_RING < MEMBRANE_RADIUS, (
        f"ordering broken: arena {outer} < station {station:.0f} < spawn ring {SPAWN_RING} "
        f"< membrane {MEMBRANE_RADIUS}")

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
    heaviest = max(counts)
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

    return arenas


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

    outer, standoff = data["outerRadius"], data["aiStationStandoff"]
    print(f"envelope: arena {outer:.0f}  ->  AI station {outer * standoff:.0f}  ->  "
          f"spawn ring {SPAWN_RING:.0f}  ->  membrane {MEMBRANE_RADIUS:.0f}\n")

    print(f"{'i':>2}  {'arena':<14}{'prisms':>8}{'volume':>12}{'danger':>8}{'reach':>8}   composition")
    for a in arenas:
        doms = " ".join(f"{d[:1]}{n}" for d, n in a["byDomain"].items() if n)
        print(f"{a['intensity']:>2}  {a['label']:<14}{a['prisms']:>8}{a['volume']:>12.0f}"
              f"{a['byKind']['Danger']:>8}{a['maxReachWithExtent']:>8.0f}   {doms}")

    print("\nPhaseThresholds (baseline + Blob deltas), per intensity:")
    for a in arenas:
        t = a["thresholds"]
        print(f"  {a['intensity']}  {a['label']:<14} count  {t['RestlessEnter']}/{t['RestlessExit']}"
              f"  {t['FrenzyEnter']}/{t['FrenzyExit']}")
        print(f"     {'':<14} volume {t['RestlessEnterVolume']}/{t['RestlessExitVolume']}"
              f"  {t['FrenzyEnterVolume']}/{t['FrenzyExitVolume']}")
