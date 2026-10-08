#!/usr/bin/env python3
"""
READER - measures the facts TimeCrystalVertexHop is built on, straight from TimeCrystalExport.fbx.
Writes nothing. No ledger / ship panel needed.

TimeCrystalVertexHop replaces the Time crystal's continuous tumble with a single-frame snap to a
random rotation of the icosahedral group each time the flip wave loops, so the wave appears to
start from a different vertex while the crystal itself never visibly turns. That is only invisible
if all of these hold, and this script asserts each one against the source model:

  1. The mesh (30 eight-vertex blocks) is congruent under the icosahedral group, with its two-fold
     axes on the coordinate axes.
  2. At both ends of the take every block sits exactly on its bind pose - the shape at the loop
     seam IS the symmetric one.
  3. The wave starts at one five-fold axis and runs to the opposite one, and nothing moves for a
     while after the seam (the snap's still window).
  4. The bones of the blocks that flip first sit on the pentagon around the start vertex, so their
     rest centroid names that vertex - and they are exactly the prefab's `leadBlockBones`.

Usage:  python3 Tools/Build/measure_time_crystal_wave.py [--self-test]
--self-test negative-controls the symmetry check (a mesh with one vertex nudged must fail it).
"""

import math
import os
import re
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FBX = os.path.join(ROOT, "Assets/_Models/TimeCrystalExport.fbx")
PREFAB = os.path.join(ROOT, "Assets/_Prefabs/Environment/CrystalTime.prefab")
TAKE = "TimeSequenceAnimFinal.001"
KTIME_PER_SECOND = 46186158000
PHI = (1 + 5 ** 0.5) / 2


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


class Model:
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

    def head(self, bone):
        return self.armature @ np.array(self.lcl(bone, "Lcl Translation")) - self.centre

    def first_move(self, bone, degrees=1.0, units=0.005):
        """When the block first moves VISIBLY: a rotation channel 1 deg off its seam value, or a
        translation/scale channel 0.005 off. Not "the first key that differs" - two blocks ease in
        from t=0 at ~0.02 deg per frame, which is motion no player can see."""
        grid = np.linspace(0.0, 2.0, 2001)
        starts = []
        for (b, prop), channels in self.curves.items():
            if b != bone:
                continue
            tol = degrees if prop == "Lcl Rotation" else units
            for times, values in channels.values():
                off = np.abs(np.interp(grid, times, values) - values[0]) > tol
                if off.any():
                    starts.append(grid[np.argmax(off)])
        return min(starts) if starts else None

    def mid_flip(self, bone):
        """When the block's largest rotation channel is halfway through its flip."""
        times, values = max(self.curves[(bone, "Lcl Rotation")].values(), key=lambda c: np.ptp(c[1]))
        grid = np.linspace(0.0, 2.0, 2001)
        curve = np.interp(grid, times, values)
        return grid[np.argmin(np.abs(curve - (curve.min() + curve.max()) / 2))]


def symmetry_mismatch(points):
    """Worst mismatch of `points` under the icosahedral generators (and the coordinate 2-folds)."""
    tests = [rotation((0, PHI, 1), 2 * math.pi / 5), rotation((1, 0, PHI), 2 * math.pi / 5)]
    tests += [rotation(a, math.pi) for a in ((1, 0, 0), (0, 1, 0), (0, 0, 1))]
    return max(set_mismatch(points @ m.T, points) for m in tests)


def prefab_lead_bones():
    body = open(PREFAB).read()
    m = re.search(r"guid: 77743c877d59432b8341fe2754a9c118, type: 3\}\n(?:.*\n)*?  leadBlockBones:\n((?:  - .*\n)+)", body)
    assert m, "TimeCrystalVertexHop.leadBlockBones not found in CrystalTime.prefab"
    return [line.strip()[2:] for line in m.group(1).splitlines()]


def main():
    model = Model(FBX)
    mesh = model.vertices - model.centre
    radius = np.linalg.norm(mesh, axis=1).max()

    # 1. The rest shape is icosahedrally symmetric.
    sym = symmetry_mismatch(mesh)
    print(f"1. mesh: {len(mesh)} verts in {len(model.blocks)} blocks, radius {radius:.4f}, "
          f"worst mismatch under the group {sym:.2e}")
    assert sym < 1e-4 * radius, "the mesh is not icosahedrally symmetric - a snap would be visible"

    # 2. The loop seam is the bind pose, block by block.
    seam = max(set_mismatch(model.block_at(b, t), model.vertices[idx])
               for b, idx in model.blocks.items() for t in (0.0, 2.0))
    print(f"2. loop seam (t=0 and t=2) vs bind pose: worst block mismatch {seam:.2e}")
    assert seam < 1e-4 * radius, "the shape at the loop seam is not the symmetric rest shape"

    # 3. The wave runs from one five-fold vertex to the opposite one.
    starts = {b: model.first_move(b) for b in model.blocks}
    centroid = {b: mesh[idx].mean(0) for b, idx in model.blocks.items()}
    names = sorted(model.blocks)
    order = np.array([model.mid_flip(b) for b in names])
    corr, axis = max(((np.corrcoef(order, -np.array([centroid[b] @ a for b in names]))[0, 1], a)
                      for a in five_fold_axes()), key=lambda x: x[0])
    still = min(starts.values())
    print(f"3. wave starts at five-fold axis {np.round(axis, 4)} (flip order vs height: r = {corr:.3f}); "
          f"no block moves visibly for {still:.2f} s after the seam")
    assert corr > 0.9, "the take is not a pole-to-pole wave"
    assert still > 0.05, "the first block moves at the seam - there is no still window for the snap"

    # 4. The first ring's bones name the start vertex.
    lead = sorted(b for b in names if starts[b] <= still + 0.01)
    lead_dir = sum(model.head(b) for b in lead)
    off = math.degrees(math.acos(min(1.0, lead_dir @ axis / np.linalg.norm(lead_dir))))
    print(f"4. first ring: {lead}; their rest centroid is {off:.3f} deg off the start axis")
    assert len(lead) == 5, "expected the five blocks around one vertex to move first"
    assert off < 0.5, "the lead bones' centroid does not name the start vertex"
    authored = prefab_lead_bones()
    assert sorted(authored) == lead, f"CrystalTime.prefab leadBlockBones {authored} != measured {lead}"
    print(f"   CrystalTime.prefab leadBlockBones match.")


def self_test():
    model = Model(FBX)
    mesh = model.vertices - model.centre
    assert symmetry_mismatch(mesh) < 1e-4, "control: shipped mesh should pass"
    nudged = mesh.copy()
    nudged[17] += 0.01
    assert symmetry_mismatch(nudged) > 1e-3, "negative control: a nudged vertex must fail the symmetry check"
    print("self-test ok: the symmetry check passes the shipped mesh and fails a nudged one")


if __name__ == "__main__":
    self_test() if "--self-test" in sys.argv else main()
