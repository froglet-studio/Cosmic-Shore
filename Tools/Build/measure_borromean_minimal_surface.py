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
the bond tree come out of the site graph; and every plate is FITTED to the largest of its
shape that does not interpenetrate its neighbours.

The authored numbers are the ones at the top of TUNING, and each is a design decision
rather than a measurement: how big the plant is, how much clear space its heart gets, how
far a site graph edge reaches, how big a gap a plate must leave, and - per element - the
SHAPE of its plate and the SPACING of the tiling it grows on.  A plate's SIZE is not among
them: no prism may interpenetrate another, which is a guarantee, and a guarantee cannot be
authored as a number.

ONE TESSELLATION PER ELEMENT
----------------------------
A plate that does not overlap its neighbours is bounded by how far apart the neighbours
are, so an element whose body is bigger takes MORE ROOM PER SITE rather than a shrunken
plate - and coverage is a property of the tiling rather than of the count, so a looser
one covers the same membrane with fewer, bigger pieces.  Four tables are therefore emitted,
and the ladder runs one way: the more room per site, the bigger the body it carries.  Room
is bought two ways, by cutting the membrane into fewer pieces and by growing the membrane,
and SPACE - the element whose identity IS room - is the one that does the second.
CHARGE is fitted against its SHIELD rather than its plate, because a shield swaps the plate
for its circumscribing octahedron three times its reach, which is the body a Charge plant
actually wears.

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
PLANT_SCALE   = 36.0    # local units per ring unit -> a plant ~115 units across
HEART_SEAT    = 0.16    # ring units kept clear around the heart (5.76 local units)
GRAPH_REACH   = 1.55    # site-graph edge length, in mean site spacings.  Measured: at 1.55
                        # the valence is 3..7 (mean ~5), the graph is connected, and NO edge
                        # crosses a fold of the surface - the worst edge's midpoint sits
                        # ~0.12 of its own length off the membrane, so every edge is a real
                        # surface neighbour rather than two sheets passing close.
CLEARANCE     = 0.03    # every plate clears its neighbours by this fraction of itself.  The
                        # fit is run on a body inflated by it and the answer is not, so the
                        # shipped plate clears by a VISIBLE gap rather than by a float
                        # epsilon: "the largest that clears" and "clears by a gap" are
                        # different claims and the second is the one worth shipping.

# ------------------------------------------------------------------ THE FOUR ELEMENTS
#
# NO PRISM MAY INTERPENETRATE ANOTHER, so a plate's SIZE is no longer authored: what an
# element authors is the SHAPE of its plate and the SPACING of the tessellation it grows
# on, and the size is FITTED here to the largest that clears.  Two measured facts set the
# whole design:
#
#   * THE FOOTPRINT COSTS CLEARANCE AND THE THICKNESS DOES NOT.  A plate's neighbours lie
#     in the membrane beside it, so growing it along the surface runs into them, while
#     growing it along the surface NORMAL runs into nothing.  Measured on this surface,
#     taking a plate from 0.1 to 0.8 of its own width in thickness costs 1.3% of its
#     footprint and buys 7.7x its volume.  So the footprint is fitted and the thickness is
#     spent - which is how the element contract below survives the zero-overlap rule
#     instead of being flattened by it.
#   * COVERAGE IS A PROPERTY OF THE TILING, NOT OF THE COUNT.  Every plate is fitted
#     against its own neighbours, so a coarser tessellation does not cover more membrane -
#     it covers the same membrane with FEWER, BIGGER pieces.  That is what makes the site
#     count an element's own decision rather than a budget.
#
# So the quantity the ladder is stated in is ROOM PER SITE, and it runs one way: the more
# room a site has, the bigger the body it carries.  An element buys that room TWO ways -
# by cutting the membrane into fewer pieces (ORBITS) or by growing the membrane
# (SURFACE_SCALE) - which is why SPACE can take the most room per site while having a
# FINER cut than Mass or Charge.  TIME's plate is the anchor and gets the tightest
# membrane; MASS is a brick; CHARGE's real body is its SHIELD, three times the reach of
# the plate it replaces; SPACE grows the membrane itself and spends the room on length.
ELEMENTS      = ('Charge', 'Mass', 'Space', 'Time')
ORBITS        = dict(Time=60, Space=48, Mass=36, Charge=30)   # x6 sites, and x6 grow ticks

# HOW BIG THE WHOLE SURFACE IS, per element - the second half of "positioning and spacing".
# ORBITS says how many pieces the membrane is cut into; this says how far apart they are, so it
# moves the plant's RADIUS, its site spacing, its bonds and (through the fit) its plates, all
# together.  It is a SIMILARITY, which is the one transform that cannot break the no-overlap
# guarantee: scaling every site and every plate by the same k maps a clearing arrangement onto
# a clearing arrangement exactly.
#
# SPACE is the element that spends it, and it is why ROOM PER SITE rather than orbit count is
# the quantity the ladder is stated in.  Space's identity is ASPECT at the anchor's VOLUME, and
# on one fixed membrane that can only be bought by making the plate NARROWER - the plant stays
# the same size and its struts get thinner until they read as wires.  On a membrane twice as
# big it is bought by making the whole thing LONGER: the fit hands a 2x surface a 2x footprint,
# and holding the plate's volume then drives its thickness down by 4.  Same plant volume, same
# site count, twice the span.
SURFACE_SCALE = dict(Time=1.0, Space=2.0, Mass=1.0, Charge=1.0)

# The in-plane x:y of each element's plate - its SHAPE, and the only part of it authored.
#   TIME   the anchor, 1.69:1 - the plate tuned by rendering it.
#   MASS   1.20:1, nearly square, and THICK: the brick.  Its three axes come out
#          1 : 0.83 : 0.50, which is as near a cube as this contract can take it - the
#          footprint is FITTED, so the only way to make a plate chunkier is to spend
#          thickness, and thickness is volume.
#   SPACE  8.50:1 on a membrane TWICE the anchor's - it spends its area on LENGTH twice
#          over, so at equal volume its membrane reads as a frame of long thin struts
#          rather than a skin of plates.
#   CHARGE square.  The clearance of a SHIELDED plate is set by the tightest bond, which
#          runs along the grain, so length there is paid for twice - a square footprint
#          covers half again as much membrane with octahedra as Time's aspect does.  A
#          plate whose shielded form has no grain does not need one.
ASPECT        = dict(Time=1.69, Space=8.5, Mass=1.2, Charge=1.0)

# THICKNESS, the axis that is nearly free.  Time's is authored (in mean site spacings, the
# thickness its approved plate had); Mass's and Space's are SOLVED so that their plate
# volumes land on the authored multiples of Time's - which is the element contract from
# Docs/ECOSYSTEM.md 48.6, now bought on the axis that costs no clearance instead of on the
# footprint, which no longer has any to give.  Charge's is a fraction of its own fitted
# footprint, so its circumscribing octahedron is a jewel lying IN the membrane rather than
# a flat lozenge or a spike standing out of it.
TIME_THICKNESS   = 0.115
VOLUME_OF_TIME   = dict(Mass=8.00, Space=1.00)
CHARGE_THICKNESS = 0.5
FIT_PASSES       = 6    # fit -> solve the thickness -> refit.  Converges in 3; ends on a FIT,
                        # so the shipped plate is always one that cleared at its own thickness.

# A shield swaps the plate for its CIRCUMSCRIBING octahedron, which reaches 1.5 x leafSize
# from the centre (Docs/ECOSYSTEM.md 35) - so on a Charge plant the body that has to clear
# is three times the plate, and it is the SHIELDED form that is fitted here.
SHIELD_BODY   = 1.5
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

    log('[5/6] one tessellation, growth order and bond tree PER ELEMENT')
    P, N = B.area_samples(V, F, SAMPLES)
    area = r['area'] * PLANT_SCALE ** 2

    # TIME first: its plate volume is the anchor Mass's and Space's thicknesses are solved
    # against, so what the other three get cannot depend on the order they are measured in.
    tables = {}
    for name in ('Time', 'Mass', 'Space', 'Charge'):
        t = _cvt(name, ORBITS[name], P, N, G, log)
        _fit(t, tables['Time']['volume'] if name != 'Time' else 0.0, log)
        # against THIS element's own membrane: an element that grows the surface by k has
        # k^2 of it to cover, so measuring against the anchor's area would report a bigger
        # plant as a fuller one.
        t['cover'] = float(t['sites'] * t['leaf'][0] * t['leaf'][1] / (area * t['surface_scale'] ** 2))
        t['plant_volume'] = float(t['volume'] * t['sites'])
        tables[name] = t

    log('[6/6] the plant, by element')
    vt = tables['Time']['volume']
    for name in ELEMENTS:
        t = tables[name]
        log(f'      {name:<6} {t["sites"]:3d} plates  plate volume {t["volume"]:7.2f} '
            f'({t["volume"]/vt:4.2f}x Time)  plant {t["plant_volume"]:8.0f}  '
            f'membrane covered {100*t["cover"]:4.1f}%  radius {t["radius"]:.1f}')
    ch = tables['Charge']
    oc = SHIELD_BODY * ch['leaf']
    r['charge_cover'] = float(ch['sites'] * 2 * oc[0] * oc[1] / (area * ch['surface_scale'] ** 2))
    log(f'      CHARGE is fitted to its SHIELD, not to its plate: its octahedra reach '
        f'{oc[0]:.2f} x {oc[1]:.2f} x {oc[2]:.2f} and cover '
        f'{100*r["charge_cover"]:.1f}% of the membrane')
    r['tables'] = tables
    r['ring_semi'] = (1.0 * PLANT_SCALE, B.PHI * PLANT_SCALE)
    r['surface_area'] = area
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


def _cvt(name, orbits, P, N, G, log):
    """ONE ELEMENT'S TESSELLATION: its sites, its frames, its growth order and its bond
    tree - everything about WHERE a prism goes and WHEN it is laid.

    Each element grows on its own, because the size of a plate that does not interpenetrate
    its neighbours is set by how far apart the neighbours are: an element whose body is
    bigger takes a coarser tessellation rather than a shrunken plate."""
    from scipy.spatial import cKDTree
    k = len(G)
    scale = PLANT_SCALE * SURFACE_SCALE[name]
    T = B.group_table(G)
    reps = B.farthest_point_reps(P, orbits, HEART_SEAT, G)
    reps = B.symmetric_cvt(P, reps, G, iters=CVT_ITERS, seat=HEART_SEAT)
    x, y, z = B.rep_frames(reps, P, N)
    S, SX, SY, SZ = B.expand_frames(reps, x, y, z, G)
    nn_mean = float(cKDTree(S).query(S, k=2)[0][:, 1].mean())
    reach = GRAPH_REACH * nn_mean
    adj = B.site_graph(S, reach)
    val = np.array([len(a) for a in adj])
    log(f'      {name}: {len(reps)} orbits x {k} = {len(S)} sites   site graph valence '
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
    assert fold < 0.25, 'the site graph has an edge that leaves the membrane'

    # COMB the asymptotic directions.  Both are equally valid and rep_frames picks between
    # them from the sign of an eigenvector in an arbitrary tangent basis - i.e. at random -
    # so neighbouring plates flip 90 degrees and the TILING reads as noise even though
    # every plate is individually flush.
    c, before, after = B.comb_axes(adj, SX, SY, k, restarts=24)
    log(f'      {name}: combing the grain: neighbouring plates '
        f'{np.degrees(np.arccos(min(before,1))):.1f} deg apart -> '
        f'{np.degrees(np.arccos(min(after,1))):.1f} deg  ({int(c.sum())} of {len(reps)} orbits swapped)')
    assert after > before, 'combing made the tiling worse'
    x = np.where(c[:, None] == 0, x, y)
    y = np.cross(z, x)

    # GROWTH ORDER: hops out from the orbit nearest the heart, not radius.  Hop distance is
    # an ORBIT property (the graph is G-invariant), so ordering by it keeps a grow tick one
    # whole orbit AND makes every site's PARENT earlier in the table than the site itself.
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

    par = B.bond_tree(S, adj, hop, None, SX, SY, G, T, k)
    assert all(par[i] < i for i in range(len(S)) if par[i] >= 0), \
        'a site is laid before the limb that carries it'
    assert int((par == -1).sum()) == k, 'the heart carries more than one orbit of limbs'
    bl = np.array([np.linalg.norm(S[i] - S[par[i]]) for i in range(len(S)) if par[i] >= 0])
    al = np.array([max(abs(float((S[i] - S[par[i]]) @ SX[i])), abs(float((S[i] - S[par[i]]) @ SY[i])))
                   / np.linalg.norm(S[i] - S[par[i]]) for i in range(len(S)) if par[i] >= 0])

    heart_link = 1.6 * nn_mean
    comps = B.connected_prefixes(S, adj, k, heart_link)
    assert set(comps) == {1}, 'the plant grows disconnected patches and seals them up later'

    dd, _ = cKDTree(S).query(S, k=2)
    nn = dd[:, 1]
    resid = max(cKDTree(S).query(S @ M.T)[0].max() for M in G)
    assert resid < 1e-12, 'sites are not a union of orbits'
    log(f'      {name}: {hop.max()+1} hop layers, ONE component after every grow tick; '
        f'spacing {nn.min()*scale:.2f}..{nn.max()*scale:.2f} '
        f'(mean {nn.mean()*scale:.2f}); a limb runs along one of its plate\'s own axes '
        f'to within {np.degrees(np.arccos(min(al.mean(),1))):.0f} deg')
    return dict(
        name=name, orbits=len(reps), sites=len(S), orbit_size=k, surface_scale=SURFACE_SCALE[name],
        S=S * scale, X=SX, Y=SY, Z=SZ, parents=par, samples=P * scale,
        heart_seat=HEART_SEAT * scale,
        spacing=(float(nn.min()*scale), float(nn.mean()*scale), float(nn.max()*scale)),
        hops=int(hop.max()) + 1, valence=(int(val.min()), float(val.mean()), int(val.max())),
        fold=float(fold), comb_before=float(before), comb_after=float(after),
        bond_align=float(al.mean()), bond_align_worst=float(al.min()),
        bond_len=(float(bl.min()*scale), float(bl.mean()*scale), float(bl.max()*scale)),
        covering=float(cKDTree(S).query(P)[0].max()*scale), sym_residual=float(resid),
        radius=float(np.linalg.norm(S, axis=1).max()*scale))


def _fit(t, time_volume, log):
    """ONE ELEMENT'S PLATE: the largest that clears its own neighbours, at the thickness
    its element is entitled to.

    The footprint SCALE is fitted rather than authored because the thing being bought is a
    guarantee - no prism may interpenetrate another - and a guarantee cannot be authored as
    a number.  The thickness is then spent to put the plate's VOLUME where the element
    contract says it belongs, because thickness costs almost no clearance."""
    S, X, Y, Z = t['S'], t['X'], t['Y'], t['Z']
    sp, name = t['spacing'][1], t['name']
    body = SHIELD_BODY if name == 'Charge' else 0.5
    thick = TIME_THICKNESS * sp
    leaf = None
    for _ in range(FIT_PASSES):
        s = B.fit_clear_scale(S, X, Y, Z, (ASPECT[name], 1.0, max(thick, 1e-6)),
                              body, 5.0 * sp, margin=CLEARANCE)
        leaf = np.array([ASPECT[name] * s, s, thick])
        if name == 'Charge':      thick = CHARGE_THICKNESS * leaf[0]
        elif name in VOLUME_OF_TIME:
            thick = VOLUME_OF_TIME[name] * time_volume / (leaf[0] * leaf[1])
        else:                     break

    # The guarantee, proven on the shipped number rather than on the bisection's last
    # candidate - and with the tightness of the fit as its own control, because a plate
    # that clears by a mile is not a plate that was fitted.
    def overlaps(f):
        pairs = B.candidate_pairs(S, 2.0 * body * f * float(np.linalg.norm(leaf)))
        return B.obb_overlap_count(S, X, Y, Z, body * f * leaf, pairs)
    assert overlaps(1.0) == 0, f'{name} plates interpenetrate'
    assert overlaps(1.0 + CLEARANCE) == 0, f'{name} plates do not clear by the authored margin'
    tight = next((f for f in (1.05, 1.10, 1.15, 1.20, 1.30) if overlaps(f) > 0), None)
    assert tight is not None, f'{name} is not FITTED - it still clears at 1.30x'

    lift_mean, lift_max = B.plate_flushness(S, X, Y, Z, t['samples'], leaf)
    hx, hy, hz = 0.5 * leaf
    corners = np.stack([S + a * hx * X + b * hy * Y + c * hz * Z
                        for a in (-1, 1) for b in (-1, 1) for c in (-1, 1)])
    t.update(leaf=leaf, volume=float(np.prod(leaf)), body=body, tight=float(tight),
             lift_mean=lift_mean, lift_max=lift_max,
             heart_clear=float(np.linalg.norm(corners, axis=2).min()),
             footprint_of_spacing=float(leaf[0] / sp))
    log(f'      {name:<6} {leaf[0]:7.3f} x {leaf[1]:6.3f} x {leaf[2]:6.3f}  aspect {leaf[0]/leaf[1]:4.2f}'
        f'  volume {t["volume"]:7.2f}  ZERO overlaps (and {int(100*(tight-1))}% bigger collides)'
        f'  footprint {t["footprint_of_spacing"]:.2f} of the spacing'
        f'  corner lift {lift_mean/leaf[2]:.2f} of its own thickness')
    return t


def emit(r):
    f = lambda v: ('%.5f' % v).rstrip('0').rstrip('.') or '0'
    deg = lambda v: np.degrees(np.arccos(min(v, 1.0)))
    T = r['tables']
    anchor = T['Time']
    vec = lambda v: f'new Vector3({f(v[0])}f, {f(v[1])}f, {f(v[2])}f)'

    def table(name):
        t = T[name]
        P = t['S']
        Q = B.quaternion_from_frame(t['X'], t['Y'], t['Z'])
        par = t['parents']
        pos = '\n'.join(f'            new({f(P[i,0])}f, {f(P[i,1])}f, {f(P[i,2])}f),' for i in range(len(P)))
        rot = '\n'.join(f'            new({f(Q[i,0])}f, {f(Q[i,1])}f, {f(Q[i,2])}f, {f(Q[i,3])}f),' for i in range(len(Q)))
        pp = '\n'.join('            ' + ', '.join(f'{int(v)}' for v in par[i:i+12]) + ','
                       for i in range(0, len(par), 12))
        leaf, sp = t['leaf'], t['spacing'][1]
        note = {
            'Time': (f"the ANCHOR: the finest membrane ({t['sites']} plates) and the plate every "
                     f"other element is a perturbation of. Its thickness is the one authored "
                     f"number in the four ({f(TIME_THICKNESS)} of the site spacing); the other three "
                     f"spend theirs."),
            'Mass': (f"VOLUME, and the CHUNKIEST plate of the four: its axes come out 1 : "
                     f"{leaf[1]/leaf[0]:.2f} : {leaf[2]/leaf[0]:.2f}, which is as near a cube as this contract can "
                     f"take it - the footprint is FITTED, so the only way to make a plate "
                     f"chunkier is to spend thickness, and thickness IS the volume. "
                     f"{t['volume']/anchor['volume']:.2f}x Time's plate, bought on the axis that costs no clearance. "
                     f"A looser tessellation ({t['sites']} plates) gives each brick the room it needs, "
                     f"so the membrane reads as fewer, heavier pieces."),
            'Space': (f"ROOM, spent twice over. Its membrane is {f(t['surface_scale'])}x the anchor's, so the "
                      f"plant spans {t['radius']/anchor['radius']:.2f}x as far ({f(2*t['radius'])} against {f(2*anchor['radius'])} "
                      f"across) - and its plate is {leaf[0]/leaf[1]:.2f}:1 against Time's "
                      f"{anchor['leaf'][0]/anchor['leaf'][1]:.2f}:1 at {t['volume']/anchor['volume']:.2f}x its volume, so the "
                      f"element reads as SHAPE and REACH rather than as mass. A similarity is the "
                      f"one transform that cannot break the no-overlap guarantee, and holding the "
                      f"plate's VOLUME across it is what drives the thickness down by the square: "
                      f"same plant volume, same site count, twice the span, struts {anchor['leaf'][2]/leaf[2]:.1f}x thinner. "
                      f"Its membrane covers {100*t['cover']:.0f}% of the surface against Time's {100*anchor['cover']:.0f}% and "
                      f"reads as a frame of struts rather than a skin of plates."),
            'Charge': (f"ARMOUR, and the only element fitted to something other than its own plate: "
                       f"a shield swaps the plate for its circumscribing octahedron, reaching 1.5 x "
                       f"leafSize from the centre (Docs/ECOSYSTEM.md 35), so what is fitted here is "
                       f"the SHIELDED body - three times the plate. Its octahedra reach "
                       f"{f(1.5*leaf[0])} x {f(1.5*leaf[1])} x {f(1.5*leaf[2])} and cover {100*r['charge_cover']:.0f}% of the membrane; "
                       f"the plate is what is left between shield refreshes."),
        }[name]
        return f'''
        // ------------------------------------------------------------------ {name.upper()}
        //  {t['orbits']} orbits x {t['orbit_size']} = {t['sites']} sites   mean spacing {f(sp)}   plate {f(leaf[0])} x {f(leaf[1])} x {f(leaf[2])}
        //  The plate is the largest of its shape that CLEARS its neighbours, by {int(100*CLEARANCE)}% of
        //  itself: measured zero interpenetrating pairs at this size, and {int(100*(t['tight']-1))}% bigger collides.
        static readonly Vector3[] {name}Positions =
        {{
{pos}
        }};

        static readonly Quaternion[] {name}Rotations =
        {{
{rot}
        }};

        static readonly int[] {name}Parents =
        {{
{pp}
        }};
'''

    def instance(name):
        t = T[name]
        return (f'''        /// <summary>{name}: {t['sites']} plates of {f(t['leaf'][0])} x {f(t['leaf'][1])} x {f(t['leaf'][2])} on a
        /// tessellation of mean spacing {f(t['spacing'][1])}, covering {100*t['cover']:.0f}% of the membrane.
        /// Fitted: zero interpenetrating pairs, and {int(100*(t['tight']-1))}% bigger collides.</summary>
        public static readonly SurfaceTable {name} = new SurfaceTable(
            {name}Positions, {name}Rotations, {name}Parents,
            {vec(t['leaf'])}, {f(t['radius'])}f, {f(t['spacing'][1])}f, {f(t['bond_len'][2])}f,
            {f(t['heart_seat'])}f);
''')

    bodies = ''.join(table(n) for n in ELEMENTS)
    insts = '\n'.join(instance(n) for n in ELEMENTS)
    rows = '\n'.join(
        f"    /// {n:<6} {T[n]['sites']:>3} plates   plate {f(T[n]['leaf'][0])} x {f(T[n]['leaf'][1])} x {f(T[n]['leaf'][2])}"
        f"   volume {T[n]['volume']:.2f} ({T[n]['volume']/anchor['volume']:.2f}x Time)"
        f"   room per site {T[n]['spacing'][1]:.2f}   span {2*T[n]['radius']:.0f}"
        f"   membrane {100*T[n]['cover']:.0f}%" for n in ELEMENTS)

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
    /// <para><b>The symmetry is order {anchor['orbit_size']} (C3i: a 3-fold rotation about a body diagonal,
    /// times inversion), and that is the MAXIMUM available, not a shortfall.</b> As an
    /// unoriented set the rings carry the pyritohedral group of order 24, but half of those
    /// elements reverse some rings' orientations and leave others alone, so they carry this
    /// level set to a different one. The tool measures the stabiliser for every assignment
    /// of orientations and gets 6 each time.</para>
    ///
    /// <para><b>Sites are the union of whole ORBITS</b>, so every table is group-invariant by
    /// construction rather than to a tolerance - which is why a half-grown plant is exactly
    /// as symmetric as a finished one.</para>
    ///
    /// <para><b>They are ordered by HOP DISTANCE from the heart, not by radius</b>, over the
    /// surface's own site graph. Hop distance is an orbit property, so one grow tick is still
    /// one whole orbit, and every site's PARENT is earlier in the table than the site itself:
    /// the plant is one connected object from its first tick instead of several patches that
    /// meet up later.</para>
    ///
    /// <para><b>The grain is COMBED.</b> A plate's long axis lies along one of the surface's
    /// two ASYMPTOTIC directions - the directions in which a minimal surface does not bend,
    /// which is why a flat rectangle sits flush on a saddle at all - and the two are equally
    /// valid, so a per-site choice picks between them effectively at random and neighbouring
    /// plates flip 90 degrees. Choosing them together takes neighbouring grains from
    /// {deg(anchor['comb_before']):.1f} degrees apart to {deg(anchor['comb_after']):.1f}.</para>
    ///
    /// <para><b>EVERY ELEMENT GROWS ON ITS OWN TESSELLATION, AND NO PRISM INTERPENETRATES
    /// ANOTHER.</b> A plate that does not overlap its neighbours is bounded by how far apart
    /// the neighbours are, so an element whose body is bigger takes MORE ROOM PER SITE
    /// rather than a shrunken plate - and its size is fitted offline to the largest that
    /// clears (by {int(100*CLEARANCE)}% of itself), never authored. An element buys that room two ways:
    /// by cutting the membrane into FEWER pieces, or by growing the MEMBRANE. Space is the
    /// one that does the second, at {f(SURFACE_SCALE['Space'])}x - a similarity, which is the only transform that
    /// cannot break the no-overlap guarantee - so it has a finer cut than Mass or Charge and
    /// still the most room of the four. What an element authors is the SHAPE of its plate and
    /// the ROOM its tiling gives one:</para>
    ///
{rows}
    ///
    /// <para>The one axis that is nearly free is THICKNESS: a plate's neighbours lie in the
    /// membrane beside it, so growing it along the surface runs into them and growing it
    /// along the NORMAL runs into nothing (measured on this surface, 0.1 to 0.8 of its own
    /// width in thickness costs 1.3% of the footprint and buys 7.7x the volume). That is
    /// what lets the element contract survive the zero-overlap rule: Mass's volume and
    /// Space's equal-volume-at-higher-aspect are both bought there.</para>
    /// </summary>
    public static class BorromeanSurfaceData
    {{
        /// <summary>Sites per orbit - the number of prisms one grow tick lays. This is the
        /// order of the surface's symmetry group, so it is the same for every element.</summary>
        public const int OrbitSize = {anchor['orbit_size']};

        /// <summary>The largest table any element grows. A live-prism budget authored above
        /// it is simply never spent; the plant clamps to its OWN element's site count.</summary>
        public const int MaxSiteCount = {max(T[n]['sites'] for n in ELEMENTS)};

        /// <summary>Area of the minimal surface at the ANCHOR's scale, in local units
        /// squared. An element may grow the membrane itself - SPACE does, at {f(SURFACE_SCALE['Space'])}x -
        /// and then carries the square of that much of it; its own
        /// <see cref="SurfaceTable.PlantRadius"/> is what says how big its membrane is.</summary>
        public const float SurfaceArea = {f(r['surface_area'])}f;

        /// <summary>
        /// One element's take on the surface: where its prisms go, how they are turned, which
        /// limb each hangs off, and how big its plate is.
        ///
        /// <para>The four differ in their SPACING as well as their plate, which is what makes
        /// "no prism interpenetrates another" a property of the tables rather than a tuning
        /// that has to be re-checked: each element's plate was fitted against its own
        /// neighbours, so the guarantee travels with the table.</para>
        /// </summary>
        public readonly struct SurfaceTable
        {{
            /// <summary>Prism sites, in growth order: orbit by orbit, outward from the heart.</summary>
            public readonly Vector3[] Positions;

            /// <summary>Each site's frame - z is the surface normal, x and y the two asymptotic
            /// directions the plate lies along.</summary>
            public readonly Quaternion[] Rotations;

            /// <summary>The LIMB each site hangs off: the index of the site one hop closer to
            /// the heart whose bond runs most nearly along one of this plate's own axes, or -1
            /// for the <see cref="OrbitSize"/> sites that hang off the HEART itself.
            ///
            /// <para>This is the plant's skeleton, and it is what makes a Borromean plant grow
            /// the way a flora withers, RUN BACKWARDS: the crystal first, then limbs out of the
            /// crystal, then limbs and plates out of limbs. A parent is always EARLIER in the
            /// table than its child, so laying the table in order can never put a plate on the
            /// far end of a limb that does not exist.</para></summary>
            public readonly int[] Parents;

            /// <summary>The plate itself - the largest of this element's shape that clears its
            /// own neighbours. It is a MEASUREMENT, not a preference.</summary>
            public readonly Vector3 LeafSize;

            /// <summary>Furthest site from the plant's root, in local units.</summary>
            public readonly float PlantRadius;

            /// <summary>Mean distance between neighbouring sites, in local units.</summary>
            public readonly float SiteSpacing;

            /// <summary>Longest limb in the plant, in local units - the bond a spindle spans.</summary>
            public readonly float LongestBond;

            /// <summary>Radius kept clear of prisms around the heart, in local units. It is
            /// per element rather than shared because an element that grows the MEMBRANE
            /// grows the alcove its crystal sits in by the same factor.</summary>
            public readonly float HeartSeat;

            public SurfaceTable(Vector3[] positions, Quaternion[] rotations, int[] parents,
                                Vector3 leafSize, float plantRadius, float siteSpacing, float longestBond,
                                float heartSeat)
            {{
                Positions = positions; Rotations = rotations; Parents = parents;
                LeafSize = leafSize; PlantRadius = plantRadius;
                SiteSpacing = siteSpacing; LongestBond = longestBond; HeartSeat = heartSeat;
            }}

            /// <summary>Prisms in a complete plant of this element.</summary>
            public int SiteCount => Positions.Length;

            /// <summary>Grow ticks to a complete plant - one whole orbit per tick.</summary>
            public int OrbitCount => Positions.Length / OrbitSize;
        }}
{bodies}
        // The tables themselves, declared AFTER every array above them: a static field
        // initializer runs in textual order, so a table declared first would capture nulls.
{insts}
        /// <summary>The anchor table - Time's - used wherever an element is not yet known
        /// (a preview runs on the prefab, whose crystal has not been seated).</summary>
        public static readonly SurfaceTable Anchor = Time;

        /// <summary>The surface this element grows. An unknown or absent element gets the
        /// anchor, so a plant whose heart has not landed yet still has a shape.</summary>
        public static SurfaceTable For(CosmicShore.Data.Element element) => element switch
        {{
            CosmicShore.Data.Element.Charge => Charge,
            CosmicShore.Data.Element.Mass   => Mass,
            CosmicShore.Data.Element.Space  => Space,
            _                               => Time,
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
