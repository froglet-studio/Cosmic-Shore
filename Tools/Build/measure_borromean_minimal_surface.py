#!/usr/bin/env python3
"""
Measure the minimal surface spanning the BORROMEAN RINGS, and emit the C# table the
BorromeanFlora species grows on.

    python3 Tools/Build/measure_borromean_minimal_surface.py           # measure + print
    python3 Tools/Build/measure_borromean_minimal_surface.py --check   # fail if the shipped file has drifted
    python3 Tools/Build/measure_borromean_minimal_surface.py --write   # rewrite the C# file

Needs numpy + scipy and NOTHING else (the level set is extracted by the marching-
tetrahedra implementation in borromean_surface.py).  It runs the whole computation from
the implicit definition every time and takes a few minutes; the CHEAP structural
re-proof of the shipped table is Tools/Build/verify_borromean_surface_tables.py, which
is the gate meant for CI.

WHAT IS MEASURED
----------------
Everything here is derived, not authored.  The rings are the canonical realization; the
surface is a level set of their summed solid angle, relaxed to zero discrete mean
curvature; the symmetry group is measured rather than assumed; the prism sites are a
symmetric centroidal Voronoi tessellation of the relaxed surface; and the leaf is a
fixed ratio of the MEASURED site spacing, so changing the site count re-derives a
consistent plate instead of leaving a stale one behind.

The only authored numbers are the four at the top of TUNING, and each is a design
decision rather than a measurement: how big the plant is, how many prisms it spends,
how much clear space its heart gets, and the plate's aspect.

WHY THE SITES ARE EXACTLY SYMMETRIC
-----------------------------------
A site is stored as an ORBIT REPRESENTATIVE and the table is the union of its images
under the measured order-6 group, so G-invariance is a property of the CONSTRUCTION and
not a tolerance that could drift.  Growth order follows the orbits outward from the
heart, which means a Borromean plant is exactly symmetric at every stage of its growth
rather than only when it is finished - the flora lays one whole orbit per grow tick.
"""
import sys, os, argparse, hashlib
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import borromean_surface as B

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, 'Assets')), \
    f'repo root resolved to {ROOT}, which has no Assets/ - this script lives two levels down'
OUT = os.path.join(ROOT, 'Assets/_Scripts/Controller/Environment/FloraAndFauna/BorromeanSurfaceData.cs')

# ------------------------------------------------------------------ TUNING (authored)
PLANT_SCALE   = 36.0    # local units per ring unit -> a plant 116.5 units across
ORBITS        = 60      # orbit representatives; x6 = the prism budget
HEART_SEAT    = 0.16    # ring units kept clear around the heart (5.76 local units)
LEAF_RATIOS   = (1.15, 0.68, 0.115)   # plate (along grain, across grain, thickness)
                                      # as multiples of the MEASURED mean site spacing.
                                      # A LOOK call, made by rendering the shipped plates:
                                      # at 1.40 the plates lap 61% and the membrane reads as
                                      # one smooth blob; at 0.85 they lap not at all and it
                                      # reads as a perforated mesh rather than a surface.
                                      # 1.15 laps 30% - still unmistakably a membrane, with
                                      # the individual plates legible inside it.
# ------------------------------------------------------------------ solver settings
GRID_N, GRID_L, NSEG = 129, 1.9, 96
RELAX_STEPS, RELAX_POLISH = 40, 8
CVT_ITERS, SAMPLES = 60, 300000


def solver_fingerprint():
    """Everything that can change the relaxed mesh: the solver settings AND the library
    that implements the rings, the potential, the extraction and the relaxation.  A cache
    keyed on this cannot serve a mesh from a different surface - edit `ring()` or
    `relax_minimal()` and the key moves."""
    lib = open(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            'borromean_surface.py'), 'rb').read()
    key = f'{GRID_N}|{GRID_L}|{NSEG}|{RELAX_STEPS}|{RELAX_POLISH}|{hashlib.md5(lib).hexdigest()}'
    return hashlib.md5(key.encode()).hexdigest()


def measure(log=print, cache=None):
    r = {}
    log('[1/6] rings')
    t = np.linspace(0, B.TWO_PI, 600, endpoint=False)
    C = [B.ring(i, t) for i in range(3)]
    lk = [B.gauss_linking(C[i], C[j]) for i in range(3) for j in range(i+1, 3)]
    sep = min(np.linalg.norm(C[i][:,None,:]-C[j][None,:,:], axis=-1).min()
              for i in range(3) for j in range(i+1, 3))
    log(f'      pairwise linking numbers {[round(v,9) for v in lk]}   min separation {sep:.6f}')
    assert max(abs(v) for v in lk) < 1e-6, 'a pair is linked: not Borromean'
    r['linking'], r['separation'] = lk, sep

    log('[2/6] symmetry of the ORIENTED link')
    G24 = B.pyritohedral()
    G = B.stabiliser_of_oriented_link()
    orders = {e: len(B.stabiliser_of_oriented_link(e))
              for e in [(1,1,1), (1,1,-1), (1,-1,1), (-1,1,1)]}
    log(f'      unoriented rings: order {len(G24)} (pyritohedral)')
    log(f'      oriented link:    order {len(G)}  - and order {sorted(set(orders.values()))} '
        f'for every orientation assignment, so 6 is the maximum available')
    assert len(G) == 6 and set(orders.values()) == {6}
    G = np.array(G)
    r['group_order'], r['group'] = len(G), G

    fp = solver_fingerprint()
    cached = None
    if cache and os.path.exists(cache):
        z = np.load(cache)
        if str(z['fingerprint']) == fp:
            cached = (z['V'], z['F'])
            log(f'[3/6+4/6] relaxed surface loaded from {os.path.relpath(cache, ROOT)} '
                f'(fingerprint {fp[:8]})')
        else:
            log(f'      cache {os.path.relpath(cache, ROOT)} is for a different solver - ignoring')

    if cached is not None:
        V, F = cached
        loops = B.boundary_loops(V, F)
        bset = np.zeros(len(V), bool)
        for lp in loops: bset[lp] = True
        m = np.linalg.norm(B.mean_curvature(V, F)[~bset], axis=1)
        flat = 3 * np.pi * 1.0 * B.PHI
        a0 = float('nan')
        chi = B.euler(V, F)
        genus = (2 - chi - len(loops)) / 2
        r['chi'], r['genus'], r['loops'] = chi, genus, [len(l) for l in loops]
        log(f'      {len(V)} verts {len(F)} faces   chi {chi}   genus {genus:.0f}   '
            f'area {B.area(V,F):.5f}')
    else:
        V, F, a0, flat, m, loops, bset = _solve(log, r)

    assert B.euler(V, F) == -3, 'relaxation changed the topology'
    bv = np.unique(np.concatenate(B.boundary_loops(V, F)))
    snap = B.RingSnap()
    o, _, _ = snap(V[bv])
    log(f'      max boundary deviation from the rings: {np.linalg.norm(o-V[bv],axis=1).max():.2e}')
    # The minimal AREA is a property of the SURFACE - not of the grid, the mesh or the
    # solver settings - so it is the one number that catches an under-converged run, and it
    # is the reason the relaxation is a direct sparse SOLVE rather than a Jacobi sweep: the
    # sweep was still at 12.33 after 1200 iterations and would have shipped an inflated
    # membrane that passes every structural check.  The solve reaches 11.955 in five outer
    # steps and holds it; a looser grid or fewer iterations now fails HERE instead.
    assert abs(B.area(V, F) - 11.955) < 0.02, \
        f'area {B.area(V, F):.5f} has not converged to the minimal surface (expected 11.955)'
    r['area'], r['area_flat_discs'], r['H_rms'] = B.area(V, F), flat, float(np.sqrt((m**2).mean()))
    if cache and cached is None:
        np.savez_compressed(cache, V=V, F=F, fingerprint=fp)
        log(f'      cached the relaxed surface in {os.path.relpath(cache, ROOT)}')

    log('[5/6] symmetric centroidal Voronoi sites')
    P, N = B.area_samples(V, F, SAMPLES)
    _cvt(r, P, N, G, log)
    _fit(r, log)
    return r


def _solve(log, r):
    log('[3/6] level set of the solid-angle potential')
    g = np.linspace(-GRID_L, GRID_L, GRID_N)
    W = B.omega_grid(g, nseg=NSEG)
    V, F = B.marching_tets(np.sin(W/2.0), spacing=g[1]-g[0], origin=-GRID_L)
    keep = np.cos(B.omega(V[F].mean(axis=1))/2.0) < 0        # the Omega == 2pi sheet
    F = F[keep]
    used = np.unique(F); remap = -np.ones(len(V), int); remap[used] = np.arange(len(used))
    V, F = B.biggest_component(V[used], remap[F])
    F = B.orient_consistently(V, F)
    loops = B.boundary_loops(V, F)
    chi = B.euler(V, F)
    genus = (2 - chi - len(loops)) / 2
    log(f'      {len(V)} verts {len(F)} faces   chi {chi}   boundary loops {[len(l) for l in loops]}')
    log(f'      => genus {genus:.0f} over 3 boundary circles: the minimal-genus Seifert surface')
    assert chi == -3 and len(loops) == 3 and genus == 1
    r['chi'], r['genus'], r['loops'] = chi, genus, [len(l) for l in loops]

    log('[4/6] relax to a discrete minimal surface')
    snap = B.RingSnap()
    bset = np.zeros(len(V), bool)
    for lp in loops: bset[lp] = True
    V[bset], _, _ = snap(V[bset])
    a0 = B.area(V, F)
    V, F = B.relax_minimal(V, F, snap, iters=RELAX_STEPS, mu=0.45,
                           polish=RELAX_POLISH, report=RELAX_STEPS // 5, log=log)
    F = B.orient_consistently(V, F)
    m = np.linalg.norm(B.mean_curvature(V, F)[~bset], axis=1)
    flat = 3 * np.pi * 1.0 * B.PHI
    log(f'      area {a0:.5f} -> {B.area(V,F):.5f}   (three flat discs would be {flat:.5f})')
    log(f'      interior |H vector|: rms {np.sqrt((m**2).mean()):.2e}  p99 {np.percentile(m,99):.2e}')
    return V, F, a0, flat, m, loops, bset


def _cvt(r, P, N, G, log):
    reps = B.farthest_point_reps(P, ORBITS, HEART_SEAT, G)
    reps = B.symmetric_cvt(P, reps, G, iters=CVT_ITERS, seat=HEART_SEAT)
    order = np.argsort(np.linalg.norm(reps, axis=1))          # grow outward from the heart
    reps = reps[order]
    x, y, z = B.rep_frames(reps, P, N)
    SP, SX, SY, SZ = B.expand_frames(reps, x, y, z, G)
    from scipy.spatial import cKDTree
    tr = cKDTree(SP)
    dd, _ = tr.query(SP, k=2)
    nn = dd[:, 1]
    cov = cKDTree(SP).query(P)[0].max()
    log(f'      {len(reps)} orbits x {len(G)} = {len(SP)} sites')
    log(f'      nearest-neighbour spacing: min {nn.min():.4f} mean {nn.mean():.4f} max {nn.max():.4f}')
    log(f'      covering radius {cov:.4f}   site radius {np.linalg.norm(SP,axis=1).min():.4f}'
        f' .. {np.linalg.norm(SP,axis=1).max():.4f}')
    resid = max(cKDTree(SP).query(SP @ M.T)[0].max() for M in G)
    log(f'      EXACT group invariance of the site set: residual {resid:.2e}')
    assert resid < 1e-12, 'sites are not a union of orbits'
    r['_sites'] = (SP, SX, SY, SZ, nn, G)
    r.update(orbits=len(reps), sites=len(SP), spacing=(float(nn.min()), float(nn.mean()), float(nn.max())),
             covering=float(cov), sym_residual=float(resid))


def _fit(r, log):
    log('[6/6] prism fit')
    SP, SX, SY, SZ, nn, G = r.pop('_sites')
    leaf_ring = np.array(LEAF_RATIOS) * nn.mean()
    leaf = leaf_ring * PLANT_SCALE
    SPw = SP * PLANT_SCALE
    # Two broadphases, because a SHIELDED prism is three times the plate's reach: a pair
    # set built for the plate is blind to exactly the pairs the shield fit has to test.
    pairs = B.candidate_pairs(SPw, float(np.linalg.norm(leaf)))
    shield_pairs = B.candidate_pairs(SPw, 3.0 * float(np.linalg.norm(leaf)))
    ov = B.obb_overlap_count(SPw, SX, SY, SZ, 0.5 * leaf, pairs)
    log(f'      plate {leaf[0]:.3f} x {leaf[1]:.3f} x {leaf[2]:.3f}   spacing {nn.mean()*PLANT_SCALE:.3f}')
    log(f'      overlapping plate pairs: {ov} of {len(pairs)} near pairs '
        f'({100.0*ov/max(len(pairs),1):.1f}% - deliberate: the plates lap along the grain)')
    s = B.fit_shield_scale(SPw, SX, SY, SZ, leaf, shield_pairs)
    charge = leaf * s
    log(f'      CHARGE leaf (shield law: the octahedron reaches 1.5 x leafSize): '
        f'x{s:.4f} -> {charge[0]:.3f} x {charge[1]:.3f} x {charge[2]:.3f}')
    assert B.obb_overlap_count(SPw, SX, SY, SZ, 1.5 * charge, shield_pairs) == 0
    vol = float(np.prod(leaf)) * len(SP)
    log(f'      per-prism volume {np.prod(leaf):.3f}  |  whole plant {vol:.0f}'
        f'  |  plant radius {np.linalg.norm(SPw,axis=1).max():.1f}')
    r.update(leaf=leaf, charge_leaf=charge, charge_scale=float(s), overlaps=int(ov),
             near_pairs=int(len(pairs)), shield_pairs=int(len(shield_pairs)), prism_volume=float(np.prod(leaf)), plant_volume=vol,
             positions=SPw, quats=B.quaternion_from_frame(SX, SY, SZ),
             orbit_size=len(G), heart_seat=HEART_SEAT*PLANT_SCALE,
             ring_semi=(1.0*PLANT_SCALE, B.PHI*PLANT_SCALE),
             plant_radius=float(np.linalg.norm(SPw, axis=1).max()))


def emit(r):
    f = lambda v: ('%.5f' % v).rstrip('0').rstrip('.') or '0'
    P, Q = r['positions'], r['quats']
    lines = []
    for i in range(len(P)):
        lines.append(f'        new({f(P[i,0])}f, {f(P[i,1])}f, {f(P[i,2])}f),')
    qlines = []
    for i in range(len(Q)):
        qlines.append(f'        new({f(Q[i,0])}f, {f(Q[i,1])}f, {f(Q[i,2])}f, {f(Q[i,3])}f),')
    leaf, ch = r['leaf'], r['charge_leaf']
    return f'''// GENERATED by Tools/Build/measure_borromean_minimal_surface.py - DO NOT EDIT BY HAND.
// Re-run that tool to change anything here; Tools/Build/verify_borromean_surface_tables.py
// re-proves the numbers below and fails the build on a hand-edit.
using UnityEngine;

namespace CosmicShore.Gameplay
{{
    /// <summary>
    /// The prism sites of the BORROMEAN MINIMAL SURFACE - the plant BorromeanFlora grows.
    ///
    /// <para>The three Borromean rings are three congruent ellipses (semi-axes
    /// {f(r['ring_semi'][0])} and {f(r['ring_semi'][1])}) in mutually perpendicular planes: the boundaries of three
    /// golden rectangles whose twelve corners are an icosahedron's vertices. That shape is
    /// forced rather than chosen - by the Freedman-Skora theorem the Borromean rings cannot
    /// be built from three round CIRCLES at all.</para>
    ///
    /// <para>The surface spanning them is a level set of the rings' summed SOLID ANGLE,
    /// relaxed until its discrete mean curvature vanishes (measured rms
    /// {r['H_rms']:.1e}). It comes out with Euler characteristic -3 over three boundary
    /// loops - GENUS 1, the minimal-genus Seifert surface of the link - and area
    /// {r['area']:.3f} against {r['area_flat_discs']:.3f} for three flat discs. Three flat discs are not an
    /// alternative: they intersect, and three DISJOINT discs would split the link, which
    /// the Borromean rings are not. A connected spanning surface is forced.</para>
    ///
    /// <para><b>The symmetry is order {r['orbit_size']} (C3i: a 3-fold rotation about a body diagonal,
    /// times inversion), and that is the MAXIMUM available, not a shortfall.</b> As an
    /// unoriented set the rings carry the pyritohedral group of order 24, but half of those
    /// elements reverse some rings' orientations and leave others alone, so they carry this
    /// level set to a different one. The tool measures the stabiliser for every assignment
    /// of orientations and gets 6 each time.</para>
    ///
    /// <para>Sites are the union of whole ORBITS, so the table is group-invariant by
    /// construction rather than to a tolerance, and they are ordered outward from the
    /// heart - which is why a half-grown plant is exactly as symmetric as a finished one.</para>
    /// </summary>
    public static class BorromeanSurfaceData
    {{
        /// <summary>Prism sites, in growth order: orbit by orbit, outward from the heart.</summary>
        public const int SiteCount = {len(P)};

        /// <summary>Sites per orbit - the number of prisms one grow tick lays.</summary>
        public const int OrbitSize = {r['orbit_size']};

        /// <summary>Orbits, i.e. grow ticks to a complete plant.</summary>
        public const int OrbitCount = {r['orbits']};

        /// <summary>Furthest site from the plant's root, in local units.</summary>
        public const float PlantRadius = {f(r['plant_radius'])}f;

        /// <summary>Radius kept clear of prisms around the heart, in local units.</summary>
        public const float HeartSeatRadius = {f(r['heart_seat'])}f;

        /// <summary>Area of the minimal surface, in local units squared.</summary>
        public const float SurfaceArea = {f(r['area'] * PLANT_SCALE ** 2)}f;

        /// <summary>Mean distance between neighbouring sites, in local units.</summary>
        public const float SiteSpacing = {f(r['spacing'][1] * PLANT_SCALE)}f;

        /// <summary>The plate every leaf but a CHARGE plant's grows to.</summary>
        public static readonly Vector3 LeafSize = new({f(leaf[0])}f, {f(leaf[1])}f, {f(leaf[2])}f);

        /// <summary>
        /// A CHARGE plant's leaf. Charge armours its mass by law (Flora.ResolveShieldPeriod),
        /// and a shield swaps in the CIRCUMSCRIBING octahedron - reaching 1.5 x leafSize from
        /// the prism centre, {3.0*3.0*3.0:.0f}x the volume - so a Charge plant is a different geometry
        /// problem from its three siblings. Fitted here to the largest uniform shrink whose
        /// octahedra still clear one another: x{r['charge_scale']:.4f}. Its plates read as a sparse skeleton
        /// and the octahedra fill the surface in (Docs/ECOSYSTEM.md 35).
        /// </summary>
        public static readonly Vector3 ChargeLeafSize = new({f(ch[0])}f, {f(ch[1])}f, {f(ch[2])}f);

        public static readonly Vector3[] Positions =
    {{
{chr(10).join(lines)}
    }};

        public static readonly Quaternion[] Rotations =
    {{
{chr(10).join(qlines)}
    }};
    }}
}}
'''


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--write', action='store_true')
    ap.add_argument('--cache', metavar='PATH', help=
        'reuse a relaxed surface from PATH, writing it there on the first run.  A DEVELOPER '
        'convenience for a look pass - the expensive half is the level set and the solve, '
        'and neither depends on the plate.  It cannot serve a stale mesh: the key is a hash '
        'of the solver settings AND of borromean_surface.py, so touching the rings, the '
        'potential, the extraction or the relaxation invalidates it.')
    a = ap.parse_args()
    r = measure(cache=a.cache)
    text = emit(r)
    if a.write:
        os.makedirs(os.path.dirname(OUT), exist_ok=True)
        with open(OUT, 'w') as fh: fh.write(text)
        print(f'\nwrote {os.path.relpath(OUT, ROOT)}')
        return 0
    if a.check:
        if not os.path.exists(OUT):
            print(f'\nFAIL: {os.path.relpath(OUT, ROOT)} does not exist'); return 1
        with open(OUT) as fh: cur = fh.read()
        if cur != text:
            print(f'\nFAIL: {os.path.relpath(OUT, ROOT)} differs from what this tool would emit'); return 1
        print(f'\nOK: {os.path.relpath(OUT, ROOT)} matches')
        return 0
    print(f'\n(dry run - pass --write to emit {os.path.relpath(OUT, ROOT)})')
    return 0


if __name__ == '__main__':
    sys.exit(main())
