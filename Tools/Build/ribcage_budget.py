#!/usr/bin/env python3
"""Analytic budget for SpawnableRibcage. Keep in sync with the C# generator.

The cage is a LAYERED ORANGE: shells are added INWARD from a fixed outer radius, one more
shell per intensity, so intensity picks how many rinds you have to peel. Each shell inward
is DENSITY_STEP times denser in ribs and hoops than the one outside it, which compounds
with the tightening that shrinking radius already gives - the core is a far harder skin
than the surface. Every rib x hoop cell carries one diagonal, so the openings are
TRIANGLES rather than quads.

Counts are exact loop arithmetic (the danger walk and the per-cell diagonal are simulated,
not estimated); volume uses E[k^3] ~ 1.04 for Jit(s, 0.2) - one uniform factor on all three
axes.

Emits the per-intensity baselines that Tools/Build/author_ribcage_assets.py turns into the
four CellConfigDataSO PhaseThresholds blocks."""
import math

OUTER_R      = 360.0   # outermost shell; SpawnableRibcage.ShellRadius (the AI aims at this)
SHELL_GAP    = 65.0    # radial spacing between rinds -> 360 / 295 / 230 / 165 / 100
MAX_SHELLS   = 5
BASE_RIBS    = 24      # meridian great circles on the OUTERMOST shell
BASE_HOOPS   = 11      # latitude hoops on the outermost shell (odd)
DENSITY_STEP = 1.05    # per-shell rib/hoop multiplier, applied inward

# Shells built at each intensity (1..4). Intensity 1 is already a LAYERED cage: a single shell
# cannot reach the ~10k prism budget without closing the outer weave the design depends on, so
# the ramp starts at two rinds and adds one per step. Totals: 10,620 / 14,731 / 17,992 / 20,153.
SHELLS_FOR_INTENSITY = [2, 3, 4, 5]
BAR_STEP     = 17.0    # arc spacing along ribs and hoops
STRUT_STEP   = 26.0    # arc spacing along a triangulating diagonal
STRUT_LEN    = 24.0    # long axis of a diagonal prism
HOOP_SPAN    = 78.0    # outermost hoop latitude, degrees
CROWN_LAT, CROWN_N = 84.0, 18
DANGER_EVERY = 19          # every Nth rib prism is a DANGER bar (punishes contact)
DANGER_SHELL_PHASE = 7919  # per-shell offset so traps don't stack radially
JIT = ((1.2)**4 - (0.8)**4) / (4*0.2)

# PhaseThresholds = measured baseline + the standard Blob deltas (Docs/ECOSYSTEM.md §18).
BLOB_DELTAS = dict(re=700, rx=500, fe=3600, fx=3000,
                   rev=11200, rxv=8000, fev=57600, fxv=48000)


def vol(s): return s[0] * s[1] * s[2]


BAR   = (3.6, 3.6, 16.0)
JOINT = (5.4, 5.4, 5.4)
STRUT = (2.4, 2.4, STRUT_LEN)
CROWN = (3.2, 3.2, 12.0)


def shell_radius(k):
    return OUTER_R - k * SHELL_GAP


def shell_ribs_hoops(k):
    """Mirrors SpawnableRibcage.ShellSpec's constructor exactly."""
    scale = DENSITY_STEP ** k
    ribs = max(3, round(BASE_RIBS * scale))
    hoops = max(3, round(BASE_HOOPS * scale))
    if hoops % 2 == 0:
        hoops += 1
    return ribs, hoops


def hoop_lats(hoops):
    """ASCENDING, evenly spaced across +/-HOOP_SPAN - matches BuildHoopLats."""
    n = 1 + ((hoops - 1) // 2) * 2
    return [-HOOP_SPAN + 2.0 * HOOP_SPAN * i / (n - 1) for i in range(n)]


def _sphere(lon, lat, r):
    return (r * math.cos(lat) * math.cos(lon), r * math.sin(lat), r * math.cos(lat) * math.sin(lon))


def shell_rows(k):
    """Exact per-structure counts for shell k, mirroring the C# loops."""
    r = shell_radius(k)
    ribs, hoops = shell_ribs_hoops(k)
    lats = hoop_lats(hoops)

    per_rib = int(round(2 * math.pi * r / BAR_STEP))
    rib_n = ribs * per_rib
    danger_n = sum(1 for rib in range(ribs) for i in range(per_rib)
                   if (k * DANGER_SHELL_PHASE + rib * per_rib + i) % DANGER_EVERY == 0)

    hoop_n = sum(int(round(2 * math.pi * r * math.cos(math.radians(l)) / BAR_STEP)) for l in lats)

    # One diagonal per rib x band cell; count its prisms the same way BuildTriangulation does.
    strut_n = 0
    dlon = 2 * math.pi / ribs
    for band in range(len(lats) - 1):
        lo, hi = math.radians(lats[band]), math.radians(lats[band + 1])
        for rib in range(ribs):
            lean = ((rib + band) & 1) == 0
            a = _sphere(0.0, lo if lean else hi, r)
            b = _sphere(dlon, hi if lean else lo, r)
            d = math.dist(a, b)
            strut_n += max(1, int(round(d / STRUT_STEP)))

    return [
        ("meridian ribs",     rib_n - danger_n, vol(BAR),   f"{ribs} x {per_rib} minus danger"),
        ("  of which DANGER", danger_n,         vol(BAR),   f"every {DANGER_EVERY}th rib prism"),
        ("latitude hoops",    hoop_n,           vol(BAR),   f"{len(lats)} hoops to +/-{HOOP_SPAN:.0f}deg"),
        ("triangulation",     strut_n,          vol(STRUT), f"1 diagonal x {ribs} x {len(lats)-1} cells"),
        ("joints",            ribs * len(lats), vol(JOINT), f"{ribs} x {len(lats)}"),
        ("polar crowns",      2 * CROWN_N,      vol(CROWN), f"2 x {CROWN_N}"),
    ]


def shell_totals(k):
    rows = shell_rows(k)
    n = sum(c for _, c, _, _ in rows)
    v = sum(c * per for _, c, per, _ in rows) * JIT
    return n, v, rows[1][1]


def shells_for_intensity(intensity):
    return SHELLS_FOR_INTENSITY[max(1, min(intensity, len(SHELLS_FOR_INTENSITY))) - 1]


def cumulative(intensity):
    """Baseline for an INTENSITY (1..4). Returns (count, volume, danger)."""
    n = v = d = 0
    for k in range(shells_for_intensity(intensity)):
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


def cell_opening(k):
    """Quad cell size on shell k, before the diagonal halves it into two triangles."""
    r = shell_radius(k)
    ribs, hoops = shell_ribs_hoops(k)
    lats = hoop_lats(hoops)
    return 2 * math.pi * r / ribs, math.radians(2 * HOOP_SPAN / (len(lats) - 1)) * r


if __name__ == "__main__":
    print(f"{'structure':<24}{'count':>7}{'vol/prism':>11}{'volume':>12}   detail")
    for k in range(MAX_SHELLS):
        n, v, _ = shell_totals(k)
        lon, lat = cell_opening(k)
        ribs, hoops = shell_ribs_hoops(k)
        print(f"\n-- shell {k} @ radius {shell_radius(k):.0f}  "
              f"{ribs} ribs x {hoops} hoops  cell {lon:.1f}u x {lat:.1f}u " + "-" * 12)
        for name, c, per, detail in shell_rows(k):
            print(f"{name:<24}{c:>7}{per*JIT:>11.1f}{c*per*JIT:>12.0f}   {detail}")
        print(f"{'  shell total':<24}{n:>7}{'':>11}{v:>12.0f}")

    print("\ncell size by depth (each is split into TWO triangles by its diagonal):")
    for k in range(MAX_SHELLS):
        lon, lat = cell_opening(k)
        print(f"  shell {k} @ {shell_radius(k):>3.0f}:  {lon:>5.1f}u x {lat:>5.1f}u")
    print(f"shell radii: " + " / ".join(f"{shell_radius(k):.0f}" for k in range(MAX_SHELLS)))
    print(f"spawn ring  {OUTER_R*1.6:.0f}  (membrane is 1200)")

    print("\n== per-INTENSITY baselines (shells added inward) " + "=" * 30)
    for i in range(1, len(SHELLS_FOR_INTENSITY) + 1):
        n, v, d = cumulative(i)
        th = phase_thresholds(n, v)
        print(f"\nintensity {i}: {shells_for_intensity(i)} shells, {n:>6} prisms   "
              f"{v:>10.0f} volume   ({d} danger bars)")
        print(f"  PhaseThresholds  count  {th['RestlessEnter']}/{th['RestlessExit']}"
              f"  {th['FrenzyEnter']}/{th['FrenzyExit']}")
        print(f"                   volume {th['RestlessEnterVolume']}/{th['RestlessExitVolume']}"
              f"  {th['FrenzyEnterVolume']}/{th['FrenzyExitVolume']}")
