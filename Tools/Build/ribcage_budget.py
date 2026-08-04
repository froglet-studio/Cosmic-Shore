#!/usr/bin/env python3
"""Analytic budget for SpawnableRibcage. Keep in sync with the C# generator.

The cage is a LAYERED ORANGE: shells are added INWARD from a fixed outer radius,
one more shell per intensity, so intensity picks how many rinds you have to peel.
Counts are exact loop arithmetic (the danger walk is simulated, not estimated);
volume uses E[k^3] ~ 1.04 for Jit(s, 0.2) - one uniform factor on all three axes.

Emits the per-intensity baselines that Tools/Build/author_ribcage_assets.py turns
into the four CellConfigDataSO PhaseThresholds blocks."""
import math

OUTER_R    = 360.0     # outermost shell; SpawnableRibcage.ShellRadius (the AI aims at this)
SHELL_GAP  = 80.0      # radial spacing between rinds -> 360 / 280 / 200 / 120
MAX_SHELLS = 4         # one per intensity
RIBS       = 26        # meridian great circles, per shell
BAR_STEP   = 17.0      # arc spacing along ribs and hoops
HOOP_COUNT = 13        # latitude hoops (odd, symmetric about the equator)
HOOP_SPAN  = 78.0      # outermost hoop latitude, degrees
BANDS, PER_STRUT = 6, 3
CROWN_LAT, CROWN_N = 84.0, 18
DANGER_EVERY = 19      # every Nth rib prism is a DANGER bar (punishes contact)
DANGER_SHELL_PHASE = 7919  # per-shell offset so traps don't stack radially
JIT = ((1.2)**4 - (0.8)**4) / (4*0.2)

# PhaseThresholds = measured baseline + the standard Blob deltas (Docs/ECOSYSTEM.md §18).
BLOB_DELTAS = dict(re=700, rx=500, fe=3600, fx=3000,
                   rev=11200, rxv=8000, fev=57600, fxv=48000)


def hoop_lats():
    half = (HOOP_COUNT - 1) // 2
    out = [0.0]
    for i in range(1, half + 1):
        out += [HOOP_SPAN * i / half, -HOOP_SPAN * i / half]
    return out


def vol(s): return s[0] * s[1] * s[2]


BAR   = (3.6, 3.6, 16.0)
JOINT = (5.4, 5.4, 5.4)
STRUT = (2.4, 2.4, 11.0)
CROWN = (3.2, 3.2, 12.0)
LATS  = hoop_lats()


def shell_radius(k):
    return OUTER_R - k * SHELL_GAP


def shell_rows(k):
    """Exact per-structure counts for shell k, mirroring the C# loops."""
    r = shell_radius(k)
    per_rib = int(round(2 * math.pi * r / BAR_STEP))
    rib_n = RIBS * per_rib
    # Same modular walk BuildRibs does, including the per-shell phase offset.
    danger_n = sum(1 for rib in range(RIBS) for i in range(per_rib)
                   if (k * DANGER_SHELL_PHASE + rib * per_rib + i) % DANGER_EVERY == 0)
    hoop_n = sum(int(round(2 * math.pi * r * math.cos(math.radians(l)) / BAR_STEP))
                 for l in LATS)
    return [
        ("meridian ribs",        rib_n - danger_n, vol(BAR),   f"{RIBS} x {per_rib} minus danger"),
        ("  of which DANGER",    danger_n,         vol(BAR),   f"every {DANGER_EVERY}th rib prism"),
        ("latitude hoops",       hoop_n,           vol(BAR),   f"{len(LATS)} hoops to +/-{HOOP_SPAN:.0f}deg"),
        ("cross-lattice",        RIBS*BANDS*PER_STRUT, vol(STRUT), f"{RIBS}x{BANDS}x{PER_STRUT}"),
        ("joints",               RIBS*len(LATS),   vol(JOINT), f"{RIBS} x {len(LATS)}"),
        ("polar crowns",         2*CROWN_N,        vol(CROWN), f"2 x {CROWN_N}"),
    ]


def shell_totals(k):
    rows = shell_rows(k)
    n = sum(c for _, c, _, _ in rows)
    v = sum(c * v for _, c, v, _ in rows) * JIT
    return n, v, rows[1][1]


def cumulative(shells):
    """Baseline for an intensity that builds `shells` rinds. Returns (count, volume, danger)."""
    n = v = d = 0
    for k in range(shells):
        a, b, c = shell_totals(k)
        n += a; v += b; d += c
    return n, v, d


def phase_thresholds(n, v):
    return dict(
        RestlessEnter=n + BLOB_DELTAS['re'], RestlessExit=n + BLOB_DELTAS['rx'],
        FrenzyEnter=n + BLOB_DELTAS['fe'],   FrenzyExit=n + BLOB_DELTAS['fx'],
        RestlessEnterVolume=round(v + BLOB_DELTAS['rev']),
        RestlessExitVolume=round(v + BLOB_DELTAS['rxv']),
        FrenzyEnterVolume=round(v + BLOB_DELTAS['fev']),
        FrenzyExitVolume=round(v + BLOB_DELTAS['fxv']))


if __name__ == "__main__":
    print(f"{'structure':<24}{'count':>7}{'vol/prism':>11}{'volume':>12}   detail")
    for k in range(MAX_SHELLS):
        n, v, _ = shell_totals(k)
        print(f"\n-- shell {k} @ radius {shell_radius(k):.0f} " + "-" * 46)
        for name, c, per, detail in shell_rows(k):
            print(f"{name:<24}{c:>7}{per*JIT:>11.1f}{c*per*JIT:>12.0f}   {detail}")
        print(f"{'  shell total':<24}{n:>7}{'':>11}{v:>12.0f}")

    rib_gap  = 2*math.pi*OUTER_R/RIBS
    hoop_gap = (2*HOOP_SPAN/360.0)*2*math.pi*OUTER_R/(len(LATS)-1)
    print(f"\ngrille opening (outer shell): {rib_gap:.1f}u x {hoop_gap:.1f}u"
          f"  (squareness {max(rib_gap,hoop_gap)/min(rib_gap,hoop_gap):.2f})")
    print(f"shell radii: " + " / ".join(f"{shell_radius(k):.0f}" for k in range(MAX_SHELLS)))
    print(f"spawn ring  {OUTER_R*1.6:.0f}  (membrane is 1200)")

    print("\n== per-INTENSITY baselines (shells added inward) " + "=" * 30)
    for i in range(1, MAX_SHELLS + 1):
        n, v, d = cumulative(i)
        th = phase_thresholds(n, v)
        print(f"\nintensity {i}: {n:>6} prisms   {v:>10.0f} volume   ({d} danger bars)")
        print(f"  PhaseThresholds  count  {th['RestlessEnter']}/{th['RestlessExit']}"
              f"  {th['FrenzyEnter']}/{th['FrenzyExit']}")
        print(f"                   volume {th['RestlessEnterVolume']}/{th['RestlessExitVolume']}"
              f"  {th['FrenzyEnterVolume']}/{th['FrenzyExitVolume']}")
