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
symmetric centroidal Voronoi tessellation of the relaxed surface; the growth order and
the bond tree come out of the site graph; and every plate is a ratio of the MEASURED site
spacing, so changing the site count re-derives consistent plates instead of leaving stale
ones behind.

The authored numbers are the ones at the top of TUNING, and each is a design decision
rather than a measurement: how big the plant is, how many prisms it spends, how much
clear space its heart gets, how far a site graph edge reaches, and what each of the four
ELEMENTS says with its plate.  CHARGE's is fitted here rather than authored, because what
has to look good on a Charge plant is its shielded octahedra.

WHY THE SITES ARE EXACTLY SYMMETRIC
-----------------------------------
A site is stored as an ORBIT REPRESENTATIVE and the table is the union of its images
under the measured order-6 group, so G-invariance is a property of the CONSTRUCTION and
not a tolerance that could drift.  The flora lays one whole orbit per grow tick, so a
Borromean plant is exactly symmetric at every stage of its growth rather than only when
it is finished.

WHY IT ALSO GROWS CONNECTED
---------------------------
The order is by HOP DISTANCE over the surface's own site graph, not by radius: on a
surface that wraps, a radius shell is several disjoint rings, and the first pass grew up
to three separate patches that met up later.  Hop distance is an ORBIT property (the
graph is G-invariant), so ordering by it costs the symmetry nothing - and every site's
PARENT then lands earlier in the table than the site itself, which is what lets the flora
refuse to lay a plate on the far end of a limb that does not exist.  Asserted here, one
component after every grow tick.
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
GRAPH_REACH   = 1.55    # site-graph edge length, in mean site spacings.  Measured: at 1.55
                        # the valence is 3..7 (mean 5.2), the graph is connected, and NO edge
                        # crosses a fold of the surface - the worst edge's midpoint sits
                        # 0.12 of its own length off the membrane, so every edge is a real
                        # surface neighbour rather than two sheets passing close.

# THE FOUR ELEMENTS, as multiples of the MEASURED mean site spacing.
#
# TIME is the ANCHOR - the plate tuned by rendering it, with the other three authored as
# perturbations of it, so "what does this element do to the plant" is one comparison
# rather than four independent fits:
#
#   TIME   the optimum.  At 1.40 along the grain the plates lap 61% and the membrane reads
#          as one smooth blob; at 0.85 they lap not at all and it reads as a perforated
#          mesh.  1.15 laps 36% - unmistakably a membrane, with the plates legible in it.
#   MASS   more VOLUME: a little wider, three times as thick.  3.9x the plate.
#   SPACE  more ASPECT: nearly twice as long and little more than half as wide, at the same
#          volume as Time - so the element reads as SHAPE and not as size.
#   CHARGE fitted, not authored - see CHARGE_ASPECT.
LEAF_TIME     = (1.15, 0.68, 0.115)
LEAF_MASS     = (1.25, 0.80, 0.350)
LEAF_SPACE    = (1.95, 0.40, 0.115)

# CHARGE armours its mass by law, and a shield swaps the plate for its CIRCUMSCRIBING
# octahedron - 1.5 x leafSize from the centre.  So a Charge plant is a different geometry
# problem: what has to look good is the SHIELDED form, and the plate is what is left
# between shield refreshes.  Two measured decisions, neither of them a uniform shrink of
# the Time plate:
#
#   * the footprint is SQUARE.  The clearance is set by the tightest BOND, which runs
#     along the grain, so length bought along the grain is paid for twice.  Sweeping the
#     in-plane aspect at the shield limit, a square footprint covers 24.1% of the membrane
#     with octahedra against 15.6% at Time's 1.69 - half again as much shielded surface
#     for the same constraint.  A plate whose shielded form has no grain does not need one.
#   * the THICKNESS is Time's, unshrunk.  Thickness is spent along the surface NORMAL,
#     where the neighbours are not, so it costs nothing in clearance and is the difference
#     between a solid little jewel and a foil: it carries 4.3x the volume of the uniform
#     shrink this replaced.
CHARGE_ASPECT = 1.0     # in-plane x:y of a Charge plate (1 = square)
# ------------------------------------------------------------------ solver settings
GRID_N, GRID_L, NSEG = 129, 1.9, 96
RELAX_STEPS, RELAX_POLISH = 40, 8
CVT_ITERS, SAMPLES = 60, 300000


# The library functions that can change the RELAXED MESH.  The cache key below hashes
# exactly these, so the growth-topology and plate-fitting helpers in the same library can
# be edited without invalidating a twelve-minute solve - and touching the rings, the
# potential, the extraction or the relaxation still moves the key, which is the property
# the key exists for.
SOLVER_SOURCES = ('ring', 'solid_angle', 'omega', 'omega_grid', 'marching_tets',
                  'biggest_component', 'orient_consistently', 'boundary_loops',
                  'vertex_normals', 'cot_laplacian', 'mean_curvature', 'uniform_laplacian',
                  'area', 'euler', 'RingSnap', 'relax_minimal')


def solver_fingerprint():
    """Everything that can change the relaxed mesh: the solver settings AND the source of
    every library function that builds or relaxes it.  A cache keyed on this cannot serve a
    mesh from a different surface - edit `ring()` or `relax_minimal()` and the key moves."""
    import inspect
    src = ''.join(inspect.getsource(getattr(B, n)) for n in SOLVER_SOURCES)
    key = f'{GRID_N}|{GRID_L}|{NSEG}|{RELAX_STEPS}|{RELAX_POLISH}|{hashlib.md5(src.encode()).hexdigest()}'
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

    log("[5/6] sites, growth order and the bond tree")
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
    """Sites, frames, growth order and the bond tree - everything about WHERE a prism goes
    and WHEN it is laid."""
    from scipy.spatial import cKDTree
    k = len(G)
    T = B.group_table(G)
    reps = B.farthest_point_reps(P, ORBITS, HEART_SEAT, G)
    reps = B.symmetric_cvt(P, reps, G, iters=CVT_ITERS, seat=HEART_SEAT)
    x, y, z = B.rep_frames(reps, P, N)
    S, SX, SY, SZ = B.expand_frames(reps, x, y, z, G)
    nn_mean = float(cKDTree(S).query(S, k=2)[0][:, 1].mean())
    reach = GRAPH_REACH * nn_mean
    adj = B.site_graph(S, reach)
    val = np.array([len(a) for a in adj])
    log(f'      {len(reps)} orbits x {k} = {len(S)} sites   site graph valence '
        f'{val.min()}..{val.max()} (mean {val.mean():.2f})')

    # An edge has to be a SURFACE neighbour, not two sheets of a genus-1 surface passing
    # close: a chord between real neighbours has its midpoint essentially on the membrane.
    tree = cKDTree(P)
    fold = 0.0
    for i in range(len(S)):
        for j in adj[i]:
            if j <= i: continue
            L = float(np.linalg.norm(S[i] - S[j]))
            fold = max(fold, float(tree.query(0.5 * (S[i] + S[j]))[0]) / L)
    log(f'      worst edge midpoint sits {fold:.3f} of its own length off the surface '
        f'(a fold crossing would be ~0.5)')
    assert fold < 0.25, 'the site graph has an edge that leaves the membrane'

    # COMB the asymptotic directions.  Both are equally valid and rep_frames picks between
    # them from the sign of an eigenvector in an arbitrary tangent basis - i.e. at random -
    # so neighbouring plates flip 90 degrees and the TILING reads as noise even though
    # every plate is individually flush.
    c, before, after = B.comb_axes(adj, SX, SY, k, restarts=24)
    log(f'      combing the grain: neighbouring plates {np.degrees(np.arccos(min(before,1))):.1f} deg '
        f'apart -> {np.degrees(np.arccos(min(after,1))):.1f} deg  ({int(c.sum())} of {len(reps)} orbits swapped)')
    assert after > before, 'combing made the tiling worse'
    x = np.where(c[:, None] == 0, x, y)
    y = np.cross(z, x)

    # GROWTH ORDER: hops out from the orbit nearest the heart, not radius.  Hop distance is
    # an ORBIT property (the graph is G-invariant), so ordering by it keeps a grow tick one
    # whole orbit AND makes every site's parent earlier in the table than the site itself.
    S, SX, SY, SZ = B.expand_frames(reps, x, y, z, G)
    adj = B.site_graph(S, reach)
    seed = int(np.argmin(np.linalg.norm(reps, axis=1)))
    hop = B.hop_layers(adj, np.arange(seed * k, (seed + 1) * k))
    assert (hop >= 0).all(), 'the site graph is disconnected'
    hop_rep = np.array([hop[i * k] for i in range(len(reps))])
    assert all(len(set(hop[i * k:(i + 1) * k])) == 1 for i in range(len(reps))), \
        'hop distance is not an orbit invariant - the graph is not G-symmetric'
    order = np.lexsort((np.linalg.norm(reps, axis=1), hop_rep))
    reps, x, y, z = reps[order], x[order], y[order], z[order]
    S, SX, SY, SZ = B.expand_frames(reps, x, y, z, G)
    adj = B.site_graph(S, reach)
    hop = B.hop_layers(adj, np.arange(k))
    log(f'      growth: {hop.max() + 1} hop layers from the heart, '
        f'{np.bincount(hop).tolist()} sites each')

    par = B.bond_tree(S, adj, hop, None, SX, SY, G, T, k)
    assert all(par[i] < i for i in range(len(S)) if par[i] >= 0), \
        'a site is laid before the limb that carries it'
    assert int((par == -1).sum()) == k, 'the heart carries more than one orbit of limbs'
    bl = np.array([np.linalg.norm(S[i] - S[par[i]]) for i in range(len(S)) if par[i] >= 0])
    al = np.array([max(abs(float((S[i] - S[par[i]]) @ SX[i])), abs(float((S[i] - S[par[i]]) @ SY[i])))
                   / np.linalg.norm(S[i] - S[par[i]]) for i in range(len(S)) if par[i] >= 0])
    log(f'      bonds: {len(bl)} limbs + {k} out of the heart   length '
        f'{bl.min()*PLANT_SCALE:.2f}..{bl.max()*PLANT_SCALE:.2f} (mean {bl.mean()*PLANT_SCALE:.2f})')
    log(f'      a limb runs along one of its plate\'s own axes: |cos| mean {al.mean():.3f} '
        f'worst {al.min():.3f}')

    heart_link = 1.6 * nn_mean
    comps = B.connected_prefixes(S, adj, k, heart_link)
    log(f'      CONNECTED after every grow tick: components {sorted(set(comps))} '
        f'over all {len(comps)} ticks (heart links any site within '
        f'{heart_link*PLANT_SCALE:.2f}; nearest is {np.linalg.norm(S,axis=1).min()*PLANT_SCALE:.2f})')
    assert set(comps) == {1}, 'the plant grows disconnected patches and seals them up later'

    cov = cKDTree(S).query(P)[0].max()
    dd, _ = cKDTree(S).query(S, k=2)
    nn = dd[:, 1]
    resid = max(cKDTree(S).query(S @ M.T)[0].max() for M in G)
    log(f'      nearest-neighbour spacing: min {nn.min():.4f} mean {nn.mean():.4f} max {nn.max():.4f}')
    log(f'      covering radius {cov:.4f}   site radius {np.linalg.norm(S,axis=1).min():.4f}'
        f' .. {np.linalg.norm(S,axis=1).max():.4f}')
    log(f'      EXACT group invariance of the site set: residual {resid:.2e}')
    assert resid < 1e-12, 'sites are not a union of orbits'
    r['_sites'] = (S, SX, SY, SZ, nn, G, P)
    r.update(orbits=len(reps), sites=len(S), parents=par, hops=int(hop.max()) + 1,
             valence=(int(val.min()), float(val.mean()), int(val.max())),
             fold=float(fold), comb_before=float(before), comb_after=float(after),
             bond_align=float(al.mean()), bond_align_worst=float(al.min()),
             bond_len=(float(bl.min()*PLANT_SCALE), float(bl.mean()*PLANT_SCALE), float(bl.max()*PLANT_SCALE)),
             spacing=(float(nn.min()), float(nn.mean()), float(nn.max())),
             covering=float(cov), sym_residual=float(resid))


def _fit(r, log):
    """The four plates.  Time is the authored anchor, Mass and Space are authored
    perturbations of it, and Charge is FITTED to its own shielded form."""
    log('[6/6] the four plates')
    S, SX, SY, SZ, nn, G, samples = r.pop('_sites')
    Sw, Pw, sp = S * PLANT_SCALE, samples * PLANT_SCALE, float(nn.mean()) * PLANT_SCALE
    time_leaf = np.array(LEAF_TIME) * sp
    shield_pairs = B.candidate_pairs(Sw, 3.0 * float(np.linalg.norm(time_leaf)))

    # CHARGE: the largest square in-plane footprint, at Time's thickness, whose shielded
    # octahedra still clear one another.  Bisection on one scalar, because the aspect and
    # the thickness are decided above.
    def clear(leaf):
        return B.obb_overlap_count(Sw, SX, SY, SZ, 1.5 * np.asarray(leaf), shield_pairs) == 0
    base = np.array([np.sqrt(CHARGE_ASPECT), 1.0 / np.sqrt(CHARGE_ASPECT)])
    lo, hi = 0.0, 4.0 * sp
    assert not clear([base[0] * hi, base[1] * hi, time_leaf[2]]), 'the shield fit is unbounded'
    while hi - lo > 1e-4 * sp:
        m = 0.5 * (lo + hi)
        if clear([base[0] * m, base[1] * m, time_leaf[2]]): lo = m
        else: hi = m
    charge = np.array([base[0] * lo, base[1] * lo, time_leaf[2]])
    assert clear(charge) and B.obb_overlap_count(Sw, SX, SY, SZ, 1.5 * charge, shield_pairs) == 0

    leaves = dict(Charge=charge, Mass=np.array(LEAF_MASS) * sp,
                  Space=np.array(LEAF_SPACE) * sp, Time=time_leaf)
    log(f'      mean site spacing {sp:.3f}')
    stats = {}
    for name in ('Time', 'Mass', 'Space', 'Charge'):
        leaf = leaves[name]
        pairs = B.candidate_pairs(Sw, float(np.linalg.norm(leaf)))
        ov = B.obb_overlap_count(Sw, SX, SY, SZ, 0.5 * leaf, pairs)
        lift_mean, lift_max = B.plate_flushness(Sw, SX, SY, SZ, Pw, leaf)
        vol = float(np.prod(leaf))
        stats[name] = dict(leaf=leaf, overlaps=int(ov), pairs=int(len(pairs)), volume=vol,
                           lift_mean=lift_mean, lift_max=lift_max)
        log(f'      {name:<6} {leaf[0]:7.3f} x {leaf[1]:6.3f} x {leaf[2]:6.3f}  aspect {leaf[0]/leaf[1]:4.2f}'
            f'  volume {vol:7.3f} ({vol/float(np.prod(time_leaf)):4.2f}x Time)'
            f'  lap {100.0*ov/max(len(pairs),1):4.1f}%'
            f'  corner lift {lift_mean:4.2f} ({lift_mean/leaf[2]:.2f} of its own thickness)')
    area = r['area'] * PLANT_SCALE ** 2
    ch_cov = len(Sw) * 2.0 * (1.5 * charge[0]) * (1.5 * charge[1]) / area
    log(f'      CHARGE is fitted, not authored: a square footprint at Time\'s thickness, '
        f'largest that clears its own shields')
    log(f'      its octahedra reach {1.5*charge[0]:.2f} x {1.5*charge[1]:.2f} x {1.5*charge[2]:.2f} '
        f'and cover {100*ch_cov:.1f}% of the membrane (Time\'s aspect would cover 15.6%)')
    vol = stats['Time']['volume'] * len(Sw)
    log(f'      whole plant {vol:.0f} volume at Time  |  plant radius '
        f'{np.linalg.norm(Sw,axis=1).max():.1f}')
    r.update(leaves=stats, charge_cover=float(ch_cov),
             positions=Sw, quats=B.quaternion_from_frame(SX, SY, SZ),
             orbit_size=len(G), heart_seat=HEART_SEAT * PLANT_SCALE,
             ring_semi=(1.0 * PLANT_SCALE, B.PHI * PLANT_SCALE),
             prism_volume=stats['Time']['volume'], plant_volume=vol,
             plant_radius=float(np.linalg.norm(Sw, axis=1).max()))


def emit(r):
    f = lambda v: ('%.5f' % v).rstrip('0').rstrip('.') or '0'
    P, Q, par = r['positions'], r['quats'], r['parents']
    lines = [f'        new({f(P[i,0])}f, {f(P[i,1])}f, {f(P[i,2])}f),' for i in range(len(P))]
    qlines = [f'        new({f(Q[i,0])}f, {f(Q[i,1])}f, {f(Q[i,2])}f, {f(Q[i,3])}f),' for i in range(len(Q))]
    plines = [('        ' + ', '.join(f'{int(v)}' for v in par[i:i+12]) + ',')
              for i in range(0, len(par), 12)]
    L = r['leaves']
    vec = lambda n: (f"new({f(L[n]['leaf'][0])}f, {f(L[n]['leaf'][1])}f, {f(L[n]['leaf'][2])}f)")
    deg = lambda v: np.degrees(np.arccos(min(v, 1.0)))
    asp = lambda n: L[n]['leaf'][0] / L[n]['leaf'][1]
    lift = lambda n: L[n]['lift_mean'] / L[n]['leaf'][2]
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
    /// <para><b>Sites are the union of whole ORBITS</b>, so the table is group-invariant by
    /// construction rather than to a tolerance - which is why a half-grown plant is exactly
    /// as symmetric as a finished one.</para>
    ///
    /// <para><b>They are ordered by HOP DISTANCE from the heart, not by radius</b>, over the
    /// surface's own site graph ({r['hops']} layers, valence {r['valence'][0]}..{r['valence'][2]}).
    /// Hop distance is an orbit property, so one grow tick is still one whole orbit, and
    /// every site's PARENT is earlier in the table than the site itself: the plant is one
    /// connected object from its first tick (measured: exactly one component after every one
    /// of the {r['orbits']} ticks) instead of six patches that meet up later.</para>
    ///
    /// <para><b>The grain is COMBED.</b> A plate's long axis lies along one of the surface's
    /// two ASYMPTOTIC directions - the directions in which a minimal surface does not bend,
    /// which is why a flat rectangle sits flush on a saddle at all - and the two are equally
    /// valid, so a per-site choice picks between them effectively at random and neighbouring
    /// plates flip 90 degrees. Choosing them together takes neighbouring grains from
    /// {deg(r['comb_before']):.1f} degrees apart to {deg(r['comb_after']):.1f}.</para>
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

        /// <summary>Longest limb in the plant, in local units - the bond a spindle spans.</summary>
        public const float LongestBond = {f(r['bond_len'][2])}f;

        /// <summary>
        /// The plate every leaf grows to, by element. A lifeform is its species and its
        /// ELEMENT and nothing else (Docs/ECOSYSTEM.md 40), so the element states the plate
        /// exactly once - here.
        ///
        /// <para>TIME is the anchor, tuned by rendering it. MASS is the same plate with more
        /// VOLUME ({L['Mass']['volume']/L['Time']['volume']:.2f}x). SPACE is the same VOLUME at {asp('Space'):.2f}:1 aspect against Time's
        /// {asp('Time'):.2f}:1, so the element reads as shape rather than as size - and it pays for
        /// it in flushness, lifting its corners {lift('Space'):.2f} of its own thickness off the membrane
        /// against Time's {lift('Time'):.2f}.</para>
        ///
        /// <para>CHARGE is FITTED rather than authored, because what has to look good on a
        /// Charge plant is its SHIELDED form: a shield swaps the plate for its circumscribing
        /// octahedron, reaching 1.5 x leafSize from the centre. Its footprint is SQUARE (the
        /// clearance is set by the tightest bond, which runs along the grain, so length there
        /// is paid for twice - a square footprint covers {100*r['charge_cover']:.1f}% of the membrane with
        /// octahedra against 15.6% at Time's aspect) and its THICKNESS is Time's unshrunk
        /// (thickness is spent along the surface normal, where the neighbours are not, so it
        /// costs nothing in clearance). Docs/ECOSYSTEM.md 35.</para>
        /// </summary>
        public static readonly Vector3 TimeLeafSize   = {vec('Time')};
        /// <inheritdoc cref="TimeLeafSize"/>
        public static readonly Vector3 MassLeafSize   = {vec('Mass')};
        /// <inheritdoc cref="TimeLeafSize"/>
        public static readonly Vector3 SpaceLeafSize  = {vec('Space')};
        /// <inheritdoc cref="TimeLeafSize"/>
        public static readonly Vector3 ChargeLeafSize = {vec('Charge')};

        /// <summary>The anchor plate - Time's - used when a config authors no leaf of its own.</summary>
        public static readonly Vector3 LeafSize = TimeLeafSize;

        /// <summary>
        /// The LIMB each site hangs off: the index of the site one hop closer to the heart
        /// whose bond runs most nearly along one of this plate's own axes, or -1 for the
        /// {r['orbit_size']} sites that hang off the HEART itself.
        ///
        /// <para>This is the plant's skeleton, and it is what makes a Borromean plant grow
        /// the way a flora withers, RUN BACKWARDS: the crystal first, then limbs out of the
        /// crystal, then limbs and plates out of limbs. A spindle is posed ON this bond -
        /// rooted at the parent, aimed at the child - so the limbs lie IN the membrane
        /// instead of standing off it along the surface normal. Measured, a limb runs along
        /// one of its plate's own axes to within {deg(r['bond_align']):.0f} degrees on average
        /// ({deg(r['bond_align_worst']):.0f} at worst), so the limbs read as veins following the tiling.</para>
        ///
        /// <para>A parent is always EARLIER in the table than its child, so laying the table
        /// in order can never put a plate on the far end of a limb that does not exist.</para>
        /// </summary>
        public static readonly int[] Parents =
    {{
{chr(10).join(plines)}
    }};

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
