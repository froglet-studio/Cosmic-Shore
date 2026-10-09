#!/usr/bin/env python3
"""
Authors the Time crystal's FLIP WAVE profile from the artist's take, and proves the procedural
wave rebuilds that take.

    python3 Tools/Build/author_time_crystal_flip_wave.py [--check | --self-test]

The Time crystal (TimeCrystalExport.fbx) is 30 rigid golden-rhombus plates on an icosidodecahedral
shell. Its one take (`TimeSequenceAnimFinal.001`, frames 0-50 at 25 fps, keyed once per frame with
linear interpolation, so the 51 frame samples ARE the authored motion) flips every plate 180 degrees
in a wave from one five-fold vertex to the opposite one. `CrystalFlipWave` now plays that wave
procedurally - no Animator, no clip - from any of the 12 vertices. This script owns the numbers it
plays and the proof that they reproduce the take:

 1. FACTS the procedural wave rests on, asserted from the FBX:
    a. the rest mesh is congruent under the icosahedral group (so a wave from any vertex looks the
       same as the authored one);
    b. both ends of the take are the bind pose (so resetting every plate from 180 to 0 degrees at the
       loop wrap is invisible - a plate flipped 180 degrees about a diagonal is the same plate);
    c. the plates flip in RINGS by height along the start axis (5 / 5 / 10 / 5 / 5), every ring
       starting a fixed 5 frames after the one before, every later ring sharing ONE curve;
    d. each plate turns about the one of its two diagonals that is more perpendicular to the wave's
       direction across it, its outer face rolling WITH the wave - away from the start vertex.

 2. THE PROFILE, fitted per frame and written to
    Assets/_SO_Assets/Environment/TimeCrystalFlipWaveProfile.asset (FlipWaveProfileSO):
      - leadRing:   the five plates round the start vertex - flip, a 10% squash, and a pinch
                    (dip inward + slide toward the vertex) measured in units of the plate's reach
                    (half its long diagonal), so the same profile reads the same on another crystal;
      - laterRings: one flip curve every other ring shares;
      - firstRingStartSeconds / ringStaggerSeconds / loopSeconds.
    Every key is a measured frame; slopes are the linear ones the FBX interpolates with.

 3. THE PROOF: the procedural rule (rings, axis choice, sign, pinch) applied to the fitted profile
    rebuilds all 240 vertices at all 51 frames; the worst error is printed against the shell radius.
    The C# that ships is proven against the same data by the edit-mode parity test
    (CrystalFlipWaveTests) and, out of editor, by running it in a Roslyn harness.

Writes ONE asset (the profile + its .meta). --check rebuilds it in memory and fails on drift.
"""

import hashlib
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FBX = os.path.join(ROOT, "Assets/_Models/TimeCrystalExport.fbx")
ASSET = os.path.join(ROOT, "Assets/_SO_Assets/Environment/TimeCrystalFlipWaveProfile.asset")
PROFILE_SCRIPT_GUID = "748b56e27b2c4fec91e34aef4e232127"   # FlipWaveProfileSO.cs.meta
ASSET_GUID = hashlib.md5(b"CosmicShore/FlipWave/TimeCrystalFlipWaveProfile").hexdigest()

TAKE = "TimeSequenceAnimFinal.001"
KTIME_PER_SECOND = 46186158000
FPS = 25
FRAMES = 51
PHI = (1 + 5 ** 0.5) / 2
RING_COUNTS = [5, 5, 10, 5, 5]


def text(v):
    return v.decode("utf8", "replace").split("\x00")[0] if isinstance(v, bytes) else v


def rotation(axis, angle):
    x, y, z = np.asarray(axis, float) / np.linalg.norm(axis)
    c, s, C = math.cos(angle), math.sin(angle), 1 - math.cos(angle)
    return np.array([[c + x * x * C, x * y * C - z * s, x * z * C + y * s],
                     [y * x * C + z * s, c + y * y * C, y * z * C - x * s],
                     [z * x * C - y * s, z * y * C + x * s, c + z * z * C]])


def euler_xyz(degrees):
    rx, ry, rz = (math.radians(d) for d in degrees)
    return rotation((0, 0, 1), rz) @ rotation((0, 1, 0), ry) @ rotation((1, 0, 0), rx)


def set_mismatch(a, b):
    """Largest distance from a point of `a` to its nearest point of `b`."""
    return np.sqrt(((a[:, None, :] - b[None, :, :]) ** 2).sum(-1)).min(1).max()


def five_fold_axes():
    axes = []
    for p in ((0, PHI, 1), (PHI, 1, 0), (1, 0, PHI)):
        for s1 in (1, -1):
            for s2 in (1, -1):
                q = list(p)
                nz = [i for i in range(3) if q[i]]
                q[nz[0]] *= s1
                q[nz[1]] *= s2
                axes.append(np.array(q) / np.linalg.norm(q))
    return axes


def similarity(a, b):
    """b ~= s * R @ (a - mean(a)) + mean(b): returns R, s, mean(b) - mean(a)."""
    ca, cb = a.mean(0), b.mean(0)
    a0, b0 = a - ca, b - cb
    u, sv, vt = np.linalg.svd(a0.T @ b0)
    d = np.diag([1.0, 1.0, np.sign(np.linalg.det(vt.T @ u.T))])
    r = vt.T @ d @ u.T
    return r, np.trace(np.diag(sv) @ d) / (a0 ** 2).sum(), cb - ca


def angle_of(r):
    # atan2, not acos: acos of a trace near 3 turns float noise into a visible fraction of a degree.
    sin = np.linalg.norm([r[2, 1] - r[1, 2], r[0, 2] - r[2, 0], r[1, 0] - r[0, 1]]) / 2
    return math.degrees(math.atan2(sin, (np.trace(r) - 1) / 2))


def axis_of(r):
    v = np.array([r[2, 1] - r[1, 2], r[0, 2] - r[2, 0], r[1, 0] - r[0, 1]])
    return v / np.linalg.norm(v)


class Model:
    """The FBX's rest mesh, its 30 rigid blocks, and the take evaluated per block."""

    def __init__(self, path):
        nodes, _, _ = fbx_binary.read(path)
        top = {n.name: n for n in nodes}
        self.objects = {o.props[0][1]: o for o in top["Objects"].children}
        self.conns = [(c.props[1][1], c.props[2][1], text(c.props[3][1]) if len(c.props) > 3 else None)
                      for c in top["Connections"].children]
        self.children = {}
        for src, dst, prop in self.conns:
            self.children.setdefault(dst, []).append((src, prop))
        self.models = {text(o.props[1][1]): o for o in self.objects.values() if o.name == "Model"}

        geometry = next(o for o in self.objects.values() if o.name == "Geometry")
        self.vertices = np.array(geometry.first("Vertices").props[0][1]).reshape(-1, 3)
        # The shell is centred 0.00027 off the origin on z; measure about its own centre.
        self.centre = (self.vertices.max(0) + self.vertices.min(0)) / 2
        self.armature = euler_xyz(self.lcl("TimeCrystalArmature", "Lcl Rotation"))

        self.blocks = {}
        for o in self.objects.values():
            if o.name == "Deformer" and text(o.props[2][1]) == "Cluster" and o.first("Indexes") is not None:
                bone = next(text(self.objects[s].props[1][1]) for s, _ in self.children.get(o.props[0][1], [])
                            if s in self.objects and self.objects[s].name == "Model")
                weights = np.array(o.first("Weights").props[0][1])
                assert np.allclose(weights, 1.0), f"{bone} is not rigidly skinned - a plate must move as one piece"
                self.blocks[bone] = np.array(o.first("Indexes").props[0][1])

        stack = next(o for o in self.objects.values()
                     if o.name == "AnimationStack" and text(o.props[1][1]).endswith(TAKE))
        layer = next(s for s, _ in self.children[stack.props[0][1]] if self.objects[s].name == "AnimationLayer")
        self.curves = {}   # (bone, property) -> {channel: (times, values)}
        for node_id, _ in self.children.get(layer, []):
            for dst, prop in [(d, p) for s, d, p in self.conns if s == node_id and d in self.objects
                              and self.objects[d].name == "Model"]:
                bone = text(self.objects[dst].props[1][1])
                for curve_id, channel in self.children.get(node_id, []):
                    curve = self.objects.get(curve_id)
                    if curve is None or curve.name != "AnimationCurve":
                        continue
                    flags = set(curve.first("KeyAttrFlags").props[0][1])
                    assert all(f & 0x0E == 0x04 for f in flags), f"{bone} {prop} is not linearly interpolated"
                    times = np.array(curve.first("KeyTime").props[0][1]) / KTIME_PER_SECOND
                    values = np.array(curve.first("KeyValueFloat").props[0][1])
                    self.curves.setdefault((bone, prop), {})[channel[-1]] = (times, values)

    def lcl(self, model, prop, default=(0.0, 0.0, 0.0)):
        for p in self.models[model].first("Properties70").children:
            if text(p.props[0][1]) == prop:
                return [x[1] for x in p.props[4:]]
        return list(default)

    def sample(self, bone, prop, t, rest):
        out = list(rest)
        for channel, (times, values) in self.curves.get((bone, prop), {}).items():
            out["XYZ".index(channel)] = float(np.interp(t, times, values))
        return out

    def block_at(self, bone, t):
        """The block's vertices at time t, in mesh space (bones all hang off the armature)."""
        rest_r, rest_t = self.lcl(bone, "Lcl Rotation"), self.lcl(bone, "Lcl Translation")
        rest_s = self.lcl(bone, "Lcl Scaling", (1.0, 1.0, 1.0))
        r0, t0 = euler_xyz(rest_r), np.array(rest_t)
        rt = euler_xyz(self.sample(bone, "Lcl Rotation", t, rest_r))
        tt = np.array(self.sample(bone, "Lcl Translation", t, rest_t))
        st = np.array(self.sample(bone, "Lcl Scaling", t, rest_s))
        bind = self.vertices[self.blocks[bone]] @ self.armature            # mesh -> armature space
        moved = (((bind - t0) @ r0) * st) @ rt.T + tt
        return moved @ self.armature.T                                      # armature -> mesh space


class Plate:
    """One block as a flip plate: centroid, outward radial, unit diagonals and reach (half the long one)."""

    def __init__(self, points, centre):
        self.centroid = points.mean(0)
        self.radial = (self.centroid - centre) / np.linalg.norm(self.centroid - centre)
        rel = points - self.centroid
        assert np.ptp(rel @ self.radial) < 0.1 * np.abs(rel).max(), "a plate is not tangent to the shell"
        flat = rel - np.outer(rel @ self.radial, self.radial)
        far = flat[np.argmax(np.linalg.norm(flat, axis=1))]
        self.reach = float(np.linalg.norm(far))
        self.long = far / self.reach
        self.short = np.cross(self.radial, self.long)
        half_short = float(np.abs(flat @ self.short).max())
        self.diagonal_ratio = self.reach / half_short


def wave_plan(plates, start):
    """The procedural rule CrystalFlipWave runs: ring by height, axis = the diagonal more perpendicular
    to the wave's direction, signed so the outer face rolls WITH the wave (away from the start vertex)."""
    heights = [float(p.radial @ start) for p in plates]
    levels = []
    for h in sorted(heights, reverse=True):
        if not levels or levels[-1] - h > 0.05:
            levels.append(h)
    plan = []
    for p, h in zip(plates, heights):
        ring = min(range(len(levels)), key=lambda i: abs(levels[i] - h))
        toward = start - (start @ p.radial) * p.radial
        toward /= np.linalg.norm(toward)
        axis = p.long if abs(p.long @ toward) < abs(p.short @ toward) else p.short
        if np.cross(axis, p.radial) @ toward > 0:
            axis = -axis
        plan.append((ring, axis, toward))
    return plan, levels


def measure(model):
    names = sorted(model.blocks)
    bind = {b: model.vertices[model.blocks[b]] for b in names}
    # The take's frame 0 is the bind pose AS A SHAPE (1b), but 22 of its bones start turned 180 degrees
    # about a diagonal, so vertex for vertex it is not. Every per-frame measurement is taken against
    # frame 0's own pose so the vertex correspondence is the take's.
    rest = {b: model.block_at(b, 0.0) for b in names}
    mesh = model.vertices - model.centre
    radius = float(np.linalg.norm(mesh, axis=1).max())

    # 1a. Symmetric rest shape.
    tests = [rotation((0, PHI, 1), 2 * math.pi / 5), rotation((1, 0, PHI), 2 * math.pi / 5)]
    tests += [rotation(a, math.pi) for a in ((1, 0, 0), (0, 1, 0), (0, 0, 1))]
    sym = max(set_mismatch(mesh @ m.T, mesh) for m in tests)
    print(f"1a. rest mesh: {len(mesh)} verts in {len(names)} rigid blocks, radius {radius:.4f}, "
          f"worst mismatch under the icosahedral group {sym:.1e}")
    assert sym < 1e-4 * radius, "the mesh is not icosahedrally symmetric - a wave from another vertex would look different"

    # 1b. Seam = bind pose.
    seam = max(set_mismatch(model.block_at(b, t), bind[b]) for b in names for t in (0.0, (FRAMES - 1) / FPS))
    print(f"1b. both ends of the take vs the bind pose: worst block mismatch {seam:.1e}")
    assert seam < 1e-4 * radius, "the take does not start and end on the bind pose"

    # Per block, per frame: the similarity that carries the rest block to its pose.
    track = {}
    for b in names:
        angles, scales, shifts, axes = [], [], [], []
        for f in range(FRAMES):
            pose = model.block_at(b, f / FPS)
            r, s, shift = similarity(rest[b], pose)
            # The rotation from rest, not the path length: three-channel Euler keys wobble the axis a little
            # between frames, so a summed path overshoots 180 degrees while the pose itself lands on it.
            total = angle_of(r)
            angles.append(total)
            scales.append(s)
            shifts.append(shift)
            axes.append(axis_of(r) if 5.0 < total < 175.0 else None)
        track[b] = (np.array(angles), np.array(scales), np.array(shifts), axes)

    plates = {b: Plate(rest[b], model.centre) for b in names}
    ratios = [p.diagonal_ratio for p in plates.values()]
    print(f"    plates: reach {np.mean([p.reach for p in plates.values()]):.4f}, diagonal ratio "
          f"{min(ratios):.4f}..{max(ratios):.4f} (golden {PHI:.4f})")

    # 1c. Rings: the lead ring is the five blocks that move first; its centroid names the start vertex.
    # A block's flip starts on the frame before its first real step (> 0.05 deg). Not "the first frame it
    # differs": three blocks creep ~0.03 deg per frame from t = 0 across a sparse key gap in the FBX.
    first = {b: next(f for f in range(1, FRAMES) if track[b][0][f] - track[b][0][f - 1] > 0.05) - 1 for b in names}
    lead = sorted(b for b in names if first[b] == min(first.values()))
    assert len(lead) == 5, f"expected five blocks to lead the wave, got {lead}"
    lead_dir = sum(plates[b].radial for b in lead)
    start = max(five_fold_axes() + [-a for a in five_fold_axes()], key=lambda a: a @ lead_dir)
    off = math.degrees(math.acos(min(1.0, start @ lead_dir / np.linalg.norm(lead_dir))))
    assert off < 0.5, f"the lead ring's centroid is {off:.2f} deg off every five-fold axis"
    order = [plates[b] for b in names]
    plan, levels = wave_plan(order, start)
    ring = {b: plan[i][0] for i, b in enumerate(names)}
    counts = [sum(1 for b in names if ring[b] == k) for k in range(len(levels))]
    assert counts == RING_COUNTS, f"rings {counts}, expected {RING_COUNTS}"
    # A ring starts with its earliest block. Two equator blocks (89, Bone.002) lack the key on their
    # ring's first frame and so step one frame late in the FBX - by 0.5 deg, inside the proof's budget.
    starts = {}
    for k in range(len(levels)):
        frames_k = sorted(first[b] for b in names if ring[b] == k)
        assert frames_k[-1] - frames_k[0] <= 1, f"ring {k} blocks start on frames {frames_k}"
        starts[k] = frames_k[0]
    stagger = {starts[k + 1] - starts[k] for k in range(len(levels) - 1)}
    assert len(stagger) == 1, f"rings do not start a fixed interval apart: {starts}"
    stagger = stagger.pop()
    print(f"1c. wave from five-fold axis {np.round(start, 4)} (lead ring {off:.3f} deg off it); rings "
          f"{counts} start on frames {[starts[k] for k in sorted(starts)]} - every {stagger} frames")

    # 1d. Axis choice and sign.
    worst_axis = 0.0
    for i, b in enumerate(names):
        predicted = plan[i][1]
        for a in track[b][3]:
            if a is not None:
                worst_axis = max(worst_axis, math.degrees(math.acos(min(1.0, float(a @ predicted)))))
    print(f"1d. every block turns about the predicted signed diagonal: worst axis error {worst_axis:.2f} deg")
    assert worst_axis < 2.0, "a block does not turn about the diagonal (or in the sense) the procedural rule picks"

    # Blocks off the ring's cadence: ones a missing FBX key makes start a frame late (89, Bone.002) or
    # creep the last fraction of a degree across a key gap at the end (Bone.006, Bone.024). They are held
    # to the proof's budget like every block, but the shared curves are fitted from the on-cadence ones.
    finish = {b: next(f for f in range(FRAMES) if track[b][0][f] > 179.99) for b in names}
    durations = [finish[b] - first[b] for b in names]
    duration = max(set(durations), key=durations.count)
    late = sorted(b for b in names if first[b] != starts[ring[b]] or finish[b] - first[b] != duration)
    print(f"    every ring flips in {duration} frames; off-cadence blocks (FBX key gaps): {', '.join(late)}")
    return dict(names=names, rest=rest, radius=radius, plates=plates, plan=plan, ring=ring, starts=starts,
                stagger=stagger, start=start, track=track, levels=levels, late=late)


def fraction(angles, start):
    """A block's flip as 0..1 from its ring's start frame to the end of the take, so a creep before the
    start (an FBX key-gap artefact, at most 0.3 deg) neither offsets the rest pose nor stops the flip
    one hair short of the 180 degrees the loop wrap relies on."""
    a = np.clip((angles - angles[start]) / (angles[-1] - angles[start]), 0.0, 1.0)
    a[:start + 1] = 0.0
    return a


def fit_profile(m):
    names, ring, track, plates, starts = m["names"], m["ring"], m["track"], m["plates"], m["starts"]
    toward = {b: m["plan"][i][2] for i, b in enumerate(names)}
    lead = [b for b in names if ring[b] == 0]
    later = [b for b in names if ring[b] > 0]
    # Fitted from the on-cadence blocks only: an off-cadence one would pull a curve short of the full
    # 180 degrees the loop wrap needs.
    fitted = [b for b in later if b not in m["late"]]
    lead = [b for b in lead if b not in m["late"]]

    # Lead ring, on its own clock (tau = 0 at its start frame), keyed over the whole loop.
    lead_taus = [(f - starts[0]) / FPS for f in range(FRAMES)]
    lead_flip = np.mean([fraction(track[b][0], starts[0]) for b in lead], axis=0)
    lead_scale = np.mean([track[b][1] for b in lead], axis=0)
    lead_dip = np.mean([-(track[b][2] @ plates[b].radial) / plates[b].reach for b in lead], axis=0)
    lead_slide = np.mean([(track[b][2] @ toward[b]) / plates[b].reach for b in lead], axis=0)

    # Later rings, each on its own clock, sharing one curve: tau 0 .. the end of the last ring's flip.
    span = FRAMES - 1 - starts[max(starts)]
    later_taus = [j / FPS for j in range(span + 1)]
    later_flip = np.mean([fraction(track[b][0], starts[ring[b]])[starts[ring[b]]:starts[ring[b]] + span + 1]
                          for b in fitted], axis=0)
    assert abs(later_flip[-1] - 1.0) < 1e-6 and abs(lead_flip[-1] - 1.0) < 1e-6, "a fitted flip stops short of 180 degrees"
    for b in later:
        assert np.all(track[b][0][:starts[ring[b]] + 1] < 1.0), f"{b} moves a visible degree before its ring starts"
        assert np.all(np.abs(track[b][1] - 1) < 1e-5) and np.abs(track[b][2]).max() < 1e-5, f"{b} squashes or shifts"

    spread = max(np.abs(fraction(track[b][0], starts[ring[b]])[starts[ring[b]]:starts[ring[b]] + span + 1]
                        - later_flip).max() * 180 for b in later)
    lead_spread = max(np.abs(fraction(track[b][0], starts[0]) - lead_flip).max() * 180 for b in lead)
    print(f"2.  lead ring: flip in {np.argmax(lead_flip > 0.999) - starts[0]} frames, squash to "
          f"{lead_scale.min():.3f}, dip {lead_dip.max():.4f} and slide {lead_slide.max():.4f} reach; later rings "
          f"share one curve ({spread:.2f} deg widest block off it; lead ring {lead_spread:.2f})")
    return dict(
        loopSeconds=(FRAMES - 1) / FPS,
        firstRingStartSeconds=starts[0] / FPS,
        ringStaggerSeconds=m["stagger"] / FPS,
        leadRing=dict(flip=(lead_taus, lead_flip), scale=(lead_taus, lead_scale),
                      dip=(lead_taus, lead_dip), slide=(lead_taus, lead_slide)),
        laterRings=dict(flip=(later_taus, later_flip), scale=None, dip=None, slide=None),
    )


def evaluate(curve, t, default):
    if curve is None:
        return default
    times, values = curve
    return float(np.interp(t, times, values))


def rebuild(m, profile, t):
    """Every block at time t, posed by the procedural rule from the fitted profile."""
    out = {}
    for i, b in enumerate(m["names"]):
        k, axis, toward = m["plan"][i]
        p = m["plates"][b]
        track = profile["leadRing"] if k == 0 else profile["laterRings"]
        tau = t - (profile["firstRingStartSeconds"] + k * profile["ringStaggerSeconds"])
        flip = evaluate(track["flip"], tau, 0.0)
        scale = evaluate(track["scale"], tau, 1.0)
        shift = p.reach * (evaluate(track["slide"], tau, 0.0) * toward - evaluate(track["dip"], tau, 0.0) * p.radial)
        r = rotation(axis, math.radians(180.0 * flip))
        out[b] = p.centroid + shift + scale * (m["rest"][b] - p.centroid) @ r.T
    return out


def prove(model, m, profile):
    worst_frame = worst_mid = 0.0
    for f in range(FRAMES):
        posed = rebuild(m, profile, f / FPS)
        worst_frame = max(worst_frame, max(np.linalg.norm(posed[b] - model.block_at(b, f / FPS), axis=1).max()
                                           for b in m["names"]))
    for f in range(FRAMES - 1):
        t = (f + 0.5) / FPS
        posed = rebuild(m, profile, t)
        worst_mid = max(worst_mid, max(np.linalg.norm(posed[b] - model.block_at(b, t), axis=1).max()
                                       for b in m["names"]))
    r = m["radius"]
    print(f"3.  procedural rebuild vs the take, all 240 verts: worst {worst_frame:.5f} on frames "
          f"({100 * worst_frame / r:.2f}% of the radius), {worst_mid:.5f} between frames ({100 * worst_mid / r:.2f}%)")
    assert worst_frame < 0.01 * r and worst_mid < 0.01 * r, "the procedural wave does not rebuild the take"
    return worst_frame, worst_mid


# ---------------------------------------------------------------- asset emission

def fmt(x):
    s = f"{float(x):.7g}"
    return "0" if s in ("-0", "0") else s


def simplify(times, values, tol=1e-7):
    """Drop keys that sit on the straight line between their neighbours - exact under linear interpolation."""
    keep = [0]
    for i in range(1, len(times) - 1):
        a, b = keep[-1], i + 1
        expect = values[a] + (values[b] - values[a]) * (times[i] - times[a]) / (times[b] - times[a])
        if abs(values[i] - expect) > tol:
            keep.append(i)
    keep.append(len(times) - 1)
    return [times[i] for i in keep], [values[i] for i in keep]


def curve_yaml(name, curve, indent):
    pad = " " * indent
    if curve is None:
        return (f"{pad}{name}:\n{pad}  serializedVersion: 2\n{pad}  m_Curve: []\n"
                f"{pad}  m_PreInfinity: 2\n{pad}  m_PostInfinity: 2\n{pad}  m_RotationOrder: 4\n")
    times, values = simplify(list(curve[0]), [float(v) for v in curve[1]])
    lines = [f"{pad}{name}:", f"{pad}  serializedVersion: 2", f"{pad}  m_Curve:"]
    for i, (t, v) in enumerate(zip(times, values)):
        inslope = 0.0 if i == 0 else (v - values[i - 1]) / (t - times[i - 1])
        outslope = 0.0 if i == len(times) - 1 else (values[i + 1] - v) / (times[i + 1] - t)
        lines += [f"{pad}  - serializedVersion: 3", f"{pad}    time: {fmt(t)}", f"{pad}    value: {fmt(v)}",
                  f"{pad}    inSlope: {fmt(inslope)}", f"{pad}    outSlope: {fmt(outslope)}",
                  f"{pad}    tangentMode: 0", f"{pad}    weightedMode: 0",
                  f"{pad}    inWeight: 0", f"{pad}    outWeight: 0"]
    lines += [f"{pad}  m_PreInfinity: 2", f"{pad}  m_PostInfinity: 2", f"{pad}  m_RotationOrder: 4"]
    return "\n".join(lines) + "\n"


def asset_text(profile):
    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
           "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
           "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
           f"  m_Script: {{fileID: 11500000, guid: {PROFILE_SCRIPT_GUID}, type: 3}}",
           "  m_Name: TimeCrystalFlipWaveProfile", "  m_EditorClassIdentifier: ",
           f"  loopSeconds: {fmt(profile['loopSeconds'])}",
           f"  firstRingStartSeconds: {fmt(profile['firstRingStartSeconds'])}",
           f"  ringStaggerSeconds: {fmt(profile['ringStaggerSeconds'])}"]
    text_out = "\n".join(out) + "\n"
    for track in ("leadRing", "laterRings"):
        text_out += f"  {track}:\n"
        for field in ("flip", "scale", "dip", "slide"):
            text_out += curve_yaml(field, profile[track][field], 4)
    return text_out


META = f"""fileFormatVersion: 2
guid: {ASSET_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def parse_asset_curves(text_in):
    """Read the emitted asset back (keys only) - the round trip the self-test holds the writer to."""
    curves, current, keys = {}, None, None
    track = None
    for line in text_in.splitlines():
        stripped = line.strip()
        if line.startswith("  ") and not line.startswith("   ") and stripped.endswith(":"):
            track = stripped[:-1]
        elif line.startswith("    ") and not line.startswith("     ") and stripped.endswith(":"):
            current = (track, stripped[:-1])
            keys = curves.setdefault(current, [])
        elif stripped.startswith("time:"):
            keys.append([float(stripped.split()[1])])
        elif stripped.startswith("value:"):
            keys[-1].append(float(stripped.split()[1]))
    return curves


def main(check):
    model = Model(FBX)
    m = measure(model)
    profile = fit_profile(m)
    prove(model, m, profile)

    want = asset_text(profile)
    have = open(ASSET).read() if os.path.exists(ASSET) else None
    have_meta = open(ASSET + ".meta").read() if os.path.exists(ASSET + ".meta") else None
    if check:
        drift = [p for p, w, h in ((ASSET, want, have), (ASSET + ".meta", META, have_meta)) if w != h]
        if drift:
            print("DRIFT (re-run without --check): " + ", ".join(os.path.relpath(p, ROOT) for p in drift))
            sys.exit(1)
        print("--check: the profile asset matches the take.")
        return
    for path, body in ((ASSET, want), (ASSET + ".meta", META)):
        if open(path).read() == body if os.path.exists(path) else False:
            print(f"unchanged {os.path.relpath(path, ROOT)}")
        else:
            with open(path, "w") as fh:
                fh.write(body)
            print(f"wrote {os.path.relpath(path, ROOT)}")


def self_test():
    model = Model(FBX)
    m = measure(model)
    profile = fit_profile(m)

    # The emitted asset round-trips the fitted keys (after collinear simplification) exactly enough.
    curves = parse_asset_curves(asset_text(profile))
    for track in ("leadRing", "laterRings"):
        for field in ("flip", "scale", "dip", "slide"):
            src = profile[track][field]
            keys = curves[(track, field)]
            if src is None:
                assert not keys, f"{track}.{field} should be empty"
                continue
            t, v = np.array([k[0] for k in keys]), np.array([k[1] for k in keys])
            assert np.abs(np.interp(src[0], t, v) - src[1]).max() < 1e-5, f"{track}.{field} lost keys"

    # Negative controls: each part of the rule, broken alone, must fail the rebuild.
    def broken(label, mutate):
        mm = dict(m, plan=[list(e) for e in m["plan"]])
        pp = {k: (dict(v) if isinstance(v, dict) else v) for k, v in profile.items()}
        mutate(mm, pp)
        try:
            prove(model, mm, pp)
        except AssertionError:
            print(f"   negative control fired: {label}")
            return
        raise AssertionError(f"negative control did NOT fire: {label}")

    def flip_sign(mm, pp):
        mm["plan"] = [(k, -a, w) for k, a, w in mm["plan"]]

    def other_diagonal(mm, pp):
        names = mm["names"]
        mm["plan"] = [(k, np.cross(mm["plates"][names[i]].radial, a), w) for i, (k, a, w) in enumerate(mm["plan"])]

    def no_pinch(mm, pp):
        pp["leadRing"] = dict(pp["leadRing"], dip=None, slide=None)

    def no_squash(mm, pp):
        pp["leadRing"] = dict(pp["leadRing"], scale=None)

    def late_stagger(mm, pp):
        pp["ringStaggerSeconds"] = pp["ringStaggerSeconds"] + 1.0 / FPS

    for label, mutate in (("axis sign", flip_sign), ("wrong diagonal", other_diagonal),
                          ("lead pinch dropped", no_pinch), ("lead squash dropped", no_squash),
                          ("stagger off by a frame", late_stagger)):
        broken(label, mutate)
    print("self-test ok")


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        self_test()
    else:
        main("--check" in sys.argv)
