#!/usr/bin/env python3
"""
Analytic prism budget for the Boneyard - the Dog Fight arena.

This is the model of `SpawnableBoneyard.cs`, and it is a MIRROR, not an estimate: the
generator (`author_dogfight_assets.py`) imports it, so the arena's PhaseThresholds are
derived from the same numbers that build the arena and the two cannot drift apart. That
mirroring is only possible because the C# was written to make it possible:

  * every per-unit prism count is a `const` (never randomised) - randomness in that file
    moves things around, it never changes how many there are;
  * the only count-affecting rejections are DETERMINISTIC (the reactor's blow-out wedge,
    the overpass's collapsed span), and both are reproduced exactly below;
  * `spawnClearRadius` is authored 0 on every Boneyard prefab, so `Emit`'s clearance
    rejection - the one genuinely awkward term - never fires. Players spawn on a shell
    well outside the wreck field and fly in, so there is nothing to clear.

`Hash01` is ported verbatim from `CellEnvironmentSpawnableBase` because the overpass gap
width is drawn from it. If either implementation changes, both must.

Run it directly to print the table that belongs in DOGFIGHT.md:

    python3 Tools/Build/boneyard_budget.py

CONFIRM IN EDITOR before trusting the thresholds: FrogletTools > Ecology >
Measure Cell Environment Baselines. If the measurer disagrees with this table, the C# and
this file have drifted - fix both, do not paper over it in the asset.
"""

import math

# ── Geometry constants: must match SpawnableBoneyard.cs ──────────────────────

ARENA_RADIUS = 520.0
CRUST_DEPTH = -200.0

# ── Scatter ─────────────────────────────────────────────────────────────────
# These do NOT affect the prism count - they only move structures around - so they play no part
# in the budget below. They live here anyway because the generator authors them onto the prefab
# variants, and a tuning value that exists in two files drifts. Same reasoning as the geometry
# constants above: one place, imported by whoever needs it.
DEBRIS_FIELDS = 7
FIELD_RADIUS_FRACTION = 0.2
CORE_CLEAR_RADIUS = 120.0
DRIFT_FRACTION = 0.4
DRIFT_HEIGHT = 300.0

CRUST_PLATES = 3000       # before density
RUBBLE_CHUNKS = 2400      # before density
ASH_MOTES = 1700          # before density

HULK_RIBS = 10
HULK_RIB_SEGMENTS = 22
HULK_STATIONS = 34
HULK_PLATE_ARC = 11

SPIRE_SEGMENTS = 26
SPIRE_RING = 8
SPIRE_BEACONS = 1         # one shielded tip per spire

FRAME_EDGES = 12
FRAME_EDGE_PRISMS = 20
FRAME_BRACES = 6
FRAME_BRACE_PRISMS = 16

OVERPASS_DECK = 64
OVERPASS_RAILS = 2

REACTOR_RING = 24
REACTOR_SHELL_RIBS = 10
REACTOR_SHELL_PRISMS = 26

# The reactor shell's blow-out: prisms whose angle falls in this window are skipped.
REACTOR_GAP_LO = 0.35
REACTOR_GAP_HI = 1.92

# The four shipped intensities. Structure counts are the intensity dial; the arena RADIUS
# deliberately never changes (see the C# summary for why).
#   (density, hulks, spires, frames, overpasses)
#
# SPREAD, not shape. All four fly the same Boneyard - the playtest read was that the ARENA is
# right and the LEVELS were too close together (intensity 1 -> 4 spanned only 1.9x, so picking a
# level barely changed the match). The ladder below spans 3.1x on the same recipe: intensity 1 is
# a genuinely open field where a fleeing Sparrow has few places to break line of sight, and
# intensity 4 is a dense wreck maze. Intensity 2 is left EXACTLY where it was, so the level the
# arena was tuned at is untouched and the others move around it.
#
# The top end stays well inside what this mode can afford: ~34k prisms is the same order as the
# freestyle cell environments (34-41k), not the ~69k of Scurry's Atlantis, and Dog Fight adds
# four Sparrows' projectile + AOE traffic on top of whatever the arena costs.
INTENSITIES = [
    (0.55,  4,  6,  3,  2),
    (0.90,  8, 12,  5,  4),
    (1.30, 13, 19,  8,  7),
    (1.75, 19, 27, 11, 10),
]

SEED = 41  # SpawnableBoneyard.DefaultSeed

# ── Per-prism volume, family by family ───────────────────────────────────────
# (x, y, z, jitter-amount). `Jit(s, amt)` in CellEnvironmentSpawnableBase draws ONE factor
# k = 1 + U(-amt, amt) and scales all three axes by it, so volume scales by k^3 and
#   E[k^3] = 1 + 3E[u] + 3E[u^2] + E[u^3] = 1 + amt^2
# for u ~ U(-amt, amt). amt = 0 means the family emits its authored scale verbatim.
#
# (The corresponding constant in wildlife_cage_budget.py integrates k^3 over the jitter range
# but divides by half the width, so it lands at exactly 2x the true expectation. Not corrected
# there - Wildlife Liberation's thresholds were tuned against that model and moving it would
# silently retune a shipped mode - but this file uses the correct factor.)
FAMILY_SCALE = {
    "crust":           (9.0, 8.4, 1.4, 0.35),
    "hulk_rib":        (3.4, 5.6, 2.2, 0.20),
    "hulk_plate":      (8.6, 8.0, 1.5, 0.25),
    "spire_body":      (3.6, 5.4, 2.0, 0.25),
    "spire_beacon":    (4.0, 4.0, 4.0, 0.0),
    "girder":          (2.6, 2.6, 4.4, 0.0),
    "overpass_deck":  (11.0, 7.0, 1.8, 0.0),
    "overpass_rail":   (1.6, 3.2, 3.6, 0.0),
    "rubble":          (3.6, 3.0, 5.2, 0.45),
    "reactor_ring":    (5.0, 8.0, 4.0, 0.0),
    "reactor_shell":   (4.4, 5.8, 1.8, 0.20),
    "ash":             (1.1, 1.1, 2.2, 0.0),
}


def unit_volume(family: str) -> float:
    x, y, z, amt = FAMILY_SCALE[family]
    return x * y * z * (1.0 + amt * amt)


def hash01(n: int) -> float:
    """Verbatim port of CellEnvironmentSpawnableBase.Hash01 (unchecked uint arithmetic)."""
    m = 0xFFFFFFFF
    h = n & m
    h = (h ^ 61) ^ (h >> 16)
    h = (h * 9) & m
    h ^= h >> 4
    h = (h * 0x27D4EB2D) & m
    h ^= h >> 15
    return (h & 0xFFFFFF) / float(0x1000000)


def scaled(n: int, density: float) -> int:
    """Mirror of Mathf.Max(1, Mathf.RoundToInt(n * density)).

    Unity's RoundToInt is banker's rounding (round half to even), which is also Python's
    round() for floats - so the two agree without special-casing.
    """
    return max(1, int(round(n * density)))


def crust(density: float) -> int:
    return scaled(CRUST_PLATES, density)


def rubble(density: float) -> int:
    return scaled(RUBBLE_CHUNKS, density)


def ash(density: float) -> int:
    return scaled(ASH_MOTES, density)


def hulks(count: int) -> tuple:
    """(total, danger) - danger rides the two END ribs only, at a 0.22 hash gate."""
    ribs = count * HULK_RIBS * HULK_RIB_SEGMENTS
    plating = count * HULK_STATIONS * HULK_PLATE_ARC

    danger = 0
    for k in range(count):
        for rib in (0, HULK_RIBS - 1):
            for s in range(HULK_RIB_SEGMENTS):
                if hash01(k * 97 + rib * 31 + s) < 0.22:
                    danger += 1
    return ribs + plating, danger


def spires(count: int) -> tuple:
    """(total, shielded)."""
    body = count * SPIRE_SEGMENTS * SPIRE_RING
    return body + count * SPIRE_BEACONS, count * SPIRE_BEACONS


def frames(count: int) -> int:
    return count * (FRAME_EDGES * FRAME_EDGE_PRISMS + FRAME_BRACES * FRAME_BRACE_PRISMS)


def overpasses(count: int) -> int:
    """The collapsed span is a per-overpass hash draw, so this walks the real loop."""
    total = 0
    for k in range(count):
        gap_half = 0.10 + 0.09 * hash01(k * 73 + 8)
        for i in range(OVERPASS_DECK):
            t = i / float(OVERPASS_DECK - 1)
            if abs(t - 0.5) < gap_half:
                continue
            total += 1 + OVERPASS_RAILS
    return total


def reactor() -> tuple:
    """(total, super_shielded, danger)."""
    total = REACTOR_RING
    danger = 0
    for rib in range(REACTOR_SHELL_RIBS):
        for s in range(REACTOR_SHELL_PRISMS):
            ang = 2.0 * math.pi * s / REACTOR_SHELL_PRISMS
            if REACTOR_GAP_LO < ang < REACTOR_GAP_HI:
                continue
            total += 1
            hot = rib in (REACTOR_SHELL_RIBS // 2, REACTOR_SHELL_RIBS // 2 - 1)
            if hot and hash01(rib * 51 + s * 7) < 0.3:
                danger += 1
    return total, REACTOR_RING, danger


# PhaseThresholds = measured baseline + the standard Blob deltas (Docs/ECOSYSTEM.md §18).
# Same deltas every authored cell uses, so the fauna ladder sits the same distance above THIS
# arena's own baseline as it does above every other one.
BLOB_DELTAS = dict(re=700, rx=500, fe=3600, fx=3000,
                   rev=11200, rxv=8000, fev=57600, fxv=48000)


def phase_thresholds(n: int, v: float) -> dict:
    return dict(
        RestlessEnter=n + BLOB_DELTAS["re"], RestlessExit=n + BLOB_DELTAS["rx"],
        FrenzyEnter=n + BLOB_DELTAS["fe"], FrenzyExit=n + BLOB_DELTAS["fx"],
        RestlessEnterVolume=round(v + BLOB_DELTAS["rev"]),
        RestlessExitVolume=round(v + BLOB_DELTAS["rxv"]),
        FrenzyEnterVolume=round(v + BLOB_DELTAS["fev"]),
        FrenzyExitVolume=round(v + BLOB_DELTAS["fxv"]))


def budget(density, hulk_count, spire_count, frame_count, overpass_count) -> dict:
    hulk_total, hulk_danger = hulks(hulk_count)
    spire_total, spire_shielded = spires(spire_count)
    reactor_total, reactor_super, reactor_danger = reactor()

    n_crust = crust(density)
    n_rubble = rubble(density)
    n_ash = ash(density)
    n_ribs = hulk_count * HULK_RIBS * HULK_RIB_SEGMENTS
    n_plate = hulk_count * HULK_STATIONS * HULK_PLATE_ARC
    n_spire_body = spire_count * SPIRE_SEGMENTS * SPIRE_RING
    n_girder = frames(frame_count)
    n_overpass = overpasses(overpass_count)
    # The overpass loop emits one deck prism and OVERPASS_RAILS rails per surviving station.
    n_deck = n_overpass // (1 + OVERPASS_RAILS)
    n_rail = n_overpass - n_deck
    n_shell = reactor_total - REACTOR_RING

    volume = (
        n_crust * unit_volume("crust")
        + n_ribs * unit_volume("hulk_rib")
        + n_plate * unit_volume("hulk_plate")
        + n_spire_body * unit_volume("spire_body")
        + spire_shielded * unit_volume("spire_beacon")
        + n_girder * unit_volume("girder")
        + n_deck * unit_volume("overpass_deck")
        + n_rail * unit_volume("overpass_rail")
        + n_rubble * unit_volume("rubble")
        + REACTOR_RING * unit_volume("reactor_ring")
        + n_shell * unit_volume("reactor_shell")
        + n_ash * unit_volume("ash")
    )

    families = {
        "crust": n_crust,
        "hulks": hulk_total,
        "spires": spire_total,
        "frames": n_girder,
        "overpasses": n_overpass,
        "rubble": n_rubble,
        "reactor": reactor_total,
        "ash": n_ash,
    }
    total = sum(families.values())
    return {
        "families": families,
        "total": total,
        "volume": volume,
        "danger": hulk_danger + reactor_danger,
        "shielded": spire_shielded,
        "super_shielded": reactor_super,
    }


def all_intensities() -> list:
    return [budget(*params) for params in INTENSITIES]


def main():
    rows = all_intensities()
    order = ["crust", "hulks", "spires", "frames", "overpasses", "rubble", "reactor", "ash"]

    header = (f"{'intensity':<10}" + "".join(f"{k:>12}" for k in order)
              + f"{'TOTAL':>10}{'volume':>12}{'danger':>9}{'shield':>8}{'super':>7}")
    print(header)
    print("-" * len(header))
    for i, (row, params) in enumerate(zip(rows, INTENSITIES), start=1):
        line = f"{i:<10}" + "".join(f"{row['families'][k]:>12,}" for k in order)
        line += (f"{row['total']:>10,}{row['volume']:>12,.0f}{row['danger']:>9,}"
                 f"{row['shielded']:>8,}{row['super_shielded']:>7,}")
        print(line)

    print()
    print("PhaseThresholds (baseline + the standard Blob deltas):")
    for i, row in enumerate(rows, start=1):
        th = phase_thresholds(row["total"], row["volume"])
        print(f"  intensity {i}: restless {th['RestlessEnter']}/{th['RestlessExit']}  "
              f"frenzy {th['FrenzyEnter']}/{th['FrenzyExit']}  "
              f"vol restless {th['RestlessEnterVolume']:,}/{th['RestlessExitVolume']:,}  "
              f"frenzy {th['FrenzyEnterVolume']:,}/{th['FrenzyExitVolume']:,}")

    print()
    print("params (density, hulks, spires, frames, overpasses):")
    for i, p in enumerate(INTENSITIES, start=1):
        print(f"  intensity {i}: {p}")

    print()
    print("always-on mesh colliders (shielded + super-shielded), per intensity:")
    for i, row in enumerate(rows, start=1):
        armored = row["shielded"] + row["super_shielded"]
        print(f"  intensity {i}: {armored} of {row['total']:,}  ({100.0 * armored / row['total']:.2f}%)")


if __name__ == "__main__":
    main()
