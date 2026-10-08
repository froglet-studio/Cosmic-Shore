# Branch archive: `claude/space-crystal-model-update-wcg14v`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-13 by Claude
- **Unmerged commits:** 3
- **Forked from:** `0a94cb38b` (2026-08-11, Merge pull request #707 from froglet-studio/claude/gold-shielded-prism-contras)
- **Tip:** `3ec848b2e`
- **Files touched (7):**
  - `Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx`
  - `Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx.meta`
  - `Assets/_Prefabs/Environment/ActiveCrystalSpace.prefab`
  - `Assets/_Prefabs/Environment/CrystalSpace.prefab`
  - `Assets/_Prefabs/Environment/SpaceDandruff.prefab`
  - `Tools/Models/build_space_crystal_prism_darts.py`
  - `Tools/Models/fbx_binary.py`

### `3e73a550b` — feat(crystals): give the space crystal faceted dart prisms

_Claude, 2026-08-12 04:24:15 +0000_

```text
Each of the space crystal's 60 flat kite ("dart") faces becomes a small solid
faceted prism: a crown cap standing proud of the old face plane, four crown
bevels, four tapered side walls and a back cap — 10 faces and 12 vertices per
dart, 600 faces / 720 vertices against the source's 60 / 240. Same deltoidal
hexecontahedron layout and the same shape-key driven shuffle, now with real
thickness and facets.

The model is generated, not hand-authored, because the transformation is
mechanical. Measured off the source: every dart is an independent 4-vertex
island and all three shape keys move each dart as a RIGID motion (uniform 25.9
deg rotation, faces staying perfectly planar). So each prism is rebuilt in its
own face's frame per pose, and each blend shape is the difference between the
prism in that pose's frame and in the rest frame — which makes the thickness
rotate WITH its dart instead of being frozen in the rest orientation, so the
solid darts stay correctly oriented right through the shuffle.

The FBX node tree is cloned from the source and only the data arrays are
replaced, so every node name and FBX object id is preserved and Unity's
name-based sub-asset id generation yields the same mesh fileID
(-5993354799466719267). Only the asset GUID is new, which is what lets the
three space-crystal prefabs repoint while MassSharkFauna, MassBrittlestarFauna,
GyroidFlora and TadPoleFauna — which share the original solid as body geometry
— keep it untouched.

No code change: SpaceCrystalAnimator drives blend shapes 0 and 1 by index and
the shape order (5pin, pin, Key 3) is unchanged.

Validated before writing and re-verified from disk: 60/60 dart prisms are
closed manifolds (every edge used exactly twice), all faces wind outward, unit
normals, blend shapes complete, node tree identical to the source apart from
payloads, FBX footer alignment matching Blender's own exports. Generator and
prefab wiring are both idempotent.
```

```text
 Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx      | Bin 0 -> 98748 bytes
 Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx.meta | 106 ++++++++++++++++
 Assets/_Prefabs/Environment/ActiveCrystalSpace.prefab      |   4 +-
 Assets/_Prefabs/Environment/CrystalSpace.prefab            |   4 +-
 Assets/_Prefabs/Environment/SpaceDandruff.prefab           |   4 +-
 Tools/Models/build_space_crystal_prism_darts.py            | 295 +++++++++++++++++++++++++++++++++++++++++++
 Tools/Models/fbx_binary.py                                 | 163 ++++++++++++++++++++++++
 7 files changed, 570 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 470 lines)</summary>

```diff
diff --git a/Tools/Models/build_space_crystal_prism_darts.py b/Tools/Models/build_space_crystal_prism_darts.py
new file mode 100644
index 000000000..ba691a5a7
--- /dev/null
+++ b/Tools/Models/build_space_crystal_prism_darts.py
@@ -0,0 +1,295 @@
+#!/usr/bin/env python3
+"""Generate the space crystal's prism-dart model from the flat-kite original.
+
+    python3 Tools/Models/build_space_crystal_prism_darts.py [--force]
+
+Source  : Assets/_Models/spacecrystalanim.fbx
+Output  : Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx (+ .meta)
+
+WHY A GENERATOR AND NOT A HAND-AUTHORED FBX
+-------------------------------------------
+The source is a deltoidal hexecontahedron: 60 kite ("dart") faces, each exported as
+an INDEPENDENT 4-vertex island, plus three blend shapes that shuffle those 60 darts
+into a second closed solid and back. Measured from the source, every shape moves each
+dart as a RIGID motion — a uniform 25.9 deg rotation, faces staying perfectly planar.
+
+That rigidity is what makes this mechanical: each flat kite is replaced by a small
+faceted PRISM built in its face's own frame (crown cap, four crown bevels, four side
+walls, back cap => 10 faces and 12 vertices per dart, 600 faces / 720 vertices total),
+and each blend shape is rebuilt by constructing the same prism in THAT POSE's frame
+and taking the difference. The thickness therefore rotates with its dart instead of
+being frozen in the rest orientation, so the solid darts stay correctly oriented all
+the way through the shuffle.
+
+The whole FBX node tree is CLONED from the source and only the data arrays are
+replaced. Every node name and every FBX object id is preserved, so Unity's name-based
+sub-asset id generation (`fileIdsGeneration: 2`, `internalIDToNameTable: []`) yields
+the SAME mesh fileID as the original, -5993354799466719267. Only the asset GUID is new,
+which is what lets the three space-crystal prefabs repoint with a one-line edit while
+the fauna/flora prefabs that share the original solid keep it untouched.
+
+Idempotent: re-running reproduces the same bytes and the same .meta GUID.
+"""
+import argparse
+import math
+import os
+import sys
+
+sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
+import fbx_binary as fbx
+
+ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
+SRC = os.path.join(ROOT, 'Assets/_Models/spacecrystalanim.fbx')
+DST = os.path.join(ROOT, 'Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx')
+
+# Stable GUID: md5("CosmicShore/SpaceCrystalPrismDartsAnim_8-12-26.fbx"), so re-running
+# the generator never orphans the prefab references.
+GUID = 'f1dc806d1143ef8c83887c883f47db94'
+
+# ---------------------------------------------------------------- dart profile
+# Ring scales are fractions of the kite about its own centroid; offsets are in model
+# units (the crystal's circumradius is ~74, a dart's centroid->corner reach is ~28).
+PROFILE = dict(
+    inset=0.05,     # shrink each dart so neighbours read as separate solids, not a shell
+    crown=1.5,      # how far the crown cap stands proud of the original face plane
+    crown_s=0.87,   # crown cap size relative to the rim (the bevel's width)
+    depth=7.0,      # inward extrusion — this is the "thickness"
+    back_s=0.84,    # back cap taper, so the side walls catch light instead of going dark
+)
+
+
+# ---------------------------------------------------------------- vector helpers
+def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
+def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
+def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
+def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
+def cross(a, b): return (a[1] * b[2] - a[2] * b[1],
+                         a[2] * b[0] - a[0] * b[2],
+                         a[0] * b[1] - a[1] * b[0])
+
+
+def normalize(a):
+    l = math.sqrt(dot(a, a))
+    return (a[0] / l, a[1] / l, a[2] / l) if l > 1e-12 else (0.0, 0.0, 0.0)
+
+
+def face_frame(pts):
+    """Centroid and outward normal of a planar polygon (outward = away from origin)."""
+    c = mul((sum(p[0] for p in pts), sum(p[1] for p in pts), sum(p[2] for p in pts)),
+            1.0 / len(pts))
+    n = (0.0, 0.0, 0.0)
+    for i in range(len(pts)):
+        n = add(n, cross(sub(pts[i], c), sub(pts[(i + 1) % len(pts)], c)))
+    n = normalize(n)
+    if dot(n, c) < 0:
+        n = mul(n, -1.0)
+    return c, n
+
+
+# ---------------------------------------------------------------- source parsing
+def load_source(path):
+    ver, root = fbx.read(path)
+    objs = root.find(b'Objects')
+    base = [c for c in objs.children
+            if c.name == b'Geometry' and c.props[2] == b'Mesh'][0]
+    shape_geos = [c for c in objs.children
+                  if c.name == b'Geometry' and c.props[2] == b'Shape']
+
+    V = base.find(b'Vertices').props[0]
+    verts = [(V[i * 3], V[i * 3 + 1], V[i * 3 + 2]) for i in range(len(V) // 3)]
+
+    polys, cur = [], []
+    for i in base.find(b'PolygonVertexIndex').props[0]:
+        if i < 0:
+            cur.append(~i)
+            polys.append(cur)
+            cur = []
+        else:
+            cur.append(i)
+
+    shapes = []
+    for s in shape_geos:
+        idxs = s.find(b'Indexes').props[0]
+        sv = s.find(b'Vertices').props[0]
+        shapes.append((s, {vi: (sv[j * 3], sv[j * 3 + 1], sv[j * 3 + 2])
+                           for j, vi in enumerate(idxs)}))
+    return ver, root, base, verts, polys, shapes
+
+
+def check_source(verts, polys, shapes):
+    """Assert the properties the construction relies on, before building anything."""
+    assert len(polys) == 60, f'expected 60 kite faces, got {len(polys)}'
+    assert all(len(p) == 4 for p in polys), 'expected every face to be a quad'
+    used = [i for p in polys for i in p]
+    assert len(used) == len(set(used)) == len(verts), \
+        'expected every dart to be an independent 4-vertex island'
+    for p in polys:                                  # CCW seen from outside
+        c, _ = face_frame([verts[i] for i in p])
+        raw = (0.0, 0.0, 0.0)
+        for i in range(4):
+            raw = add(raw, cross(sub(verts[p[i]], c), sub(verts[p[(i + 1) % 4]], c)))
+        assert dot(raw, c) > 0, 'face winding is not CCW-from-outside'
+    for _, deltas in shapes:                         # rigid + planar per dart
+        dv = [add(v, deltas.get(i, (0.0, 0.0, 0.0))) for i, v in enumerate(verts)]
+        for p in polys:
+            c, n = face_frame([dv[i] for i in p])
+            flat = max(abs(dot(sub(dv[i], c), n)) for i in p)
+            assert flat < 1e-3, f'deformed dart is not planar ({flat})'
+
+
+# ---------------------------------------------------------------- construction
+def prism_verts(pts, p):
+    """The 12 vertices of one dart prism: crown[0..3], rim[0..3], back[0..3]."""
+    c, n = face_frame(pts)
+    s = 1.0 - p['inset']
```

</details>

### `ce0029190` — fix(crystals): hold the dart spread constant through the shuffle

_Claude, 2026-08-12 06:24:30 +0000_

```text
The dart prisms blew apart mid-animation: measured off the source, the two
shuffle shape keys rotate every dart rigidly 72 deg ('5pin') / 120 deg ('pin')
about axes through the origin, and Unity interpolates a single-frame blend
shape LINEARLY in vertex space — so mid-sweep every vertex cuts the straight
chord, each dart shrank to 55% of its size (rms corner reach 20.5 -> 11.2) and
the gaps between darts ballooned.

Fix is data, not code: the two shuffle channels are now PROGRESSIVE morphs —
7 FBX in-between targets each (frame weights 12.5..100), sampled along every
dart's true rotation arc via Rodrigues at equal angle steps. Unity imports
in-between targets as blend shape frames and interpolates piecewise-linearly
between them, so SetBlendShapeWeight(0|1, 0..100) walks the arc and
SpaceCrystalAnimator needs no change; channel count stays 3 and channel order
is unchanged, so the prefabs' m_BlendShapeWeights and the index-based driver
keep working. 'Key 3' is a small in-plane shrink with zero rotation and keeps
its single frame.

Measured through a 161-sample sweep of the written file: dart size, dart
centroid radius (66.96) and minimum inter-dart gap (1.54) are now constant at
every weight — worst mid-segment deviation 0.8% size / 0.06% radius, versus a
45% size collapse before. The generator now also asserts the axis-angle
decomposition reproduces each dart's motion (guards the 180-degree degeneracy
where the extraction silently fails), keeps the FBX Definitions counts honest
(28 objects / 18 geometries), and stays byte-idempotent. Prefab AABB extents
re-derived over all frames.

Adversarially reviewed by three independent verification agents (Unity FBX
in-between import contract, generator math + surgery, repo wiring) — all pass.
```

```text
 Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx | Bin 98748 -> 342972 bytes
 Assets/_Prefabs/Environment/ActiveCrystalSpace.prefab |   2 +-
 Assets/_Prefabs/Environment/CrystalSpace.prefab       |   2 +-
 Assets/_Prefabs/Environment/SpaceDandruff.prefab      |   2 +-
 Tools/Models/build_space_crystal_prism_darts.py       | 205 ++++++++++++++++++++++++++++++++++++++++++++++--
 5 files changed, 200 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 275 lines)</summary>

```diff
diff --git a/Tools/Models/build_space_crystal_prism_darts.py b/Tools/Models/build_space_crystal_prism_darts.py
index ba691a5a7..a80aa1c69 100644
--- a/Tools/Models/build_space_crystal_prism_darts.py
+++ b/Tools/Models/build_space_crystal_prism_darts.py
@@ -10,8 +10,10 @@ WHY A GENERATOR AND NOT A HAND-AUTHORED FBX
 -------------------------------------------
 The source is a deltoidal hexecontahedron: 60 kite ("dart") faces, each exported as
 an INDEPENDENT 4-vertex island, plus three blend shapes that shuffle those 60 darts
-into a second closed solid and back. Measured from the source, every shape moves each
-dart as a RIGID motion — a uniform 25.9 deg rotation, faces staying perfectly planar.
+into a second closed solid and back. Measured from the source, every animated shape
+moves every dart as an EXACT rigid rotation about an axis through the origin —
+uniformly 72 deg for '5pin' and 120 deg for 'pin' (residual < 1e-4 model units) —
+while 'Key 3' is a small in-plane shrink with zero rotation.
 
 That rigidity is what makes this mechanical: each flat kite is replaced by a small
 faceted PRISM built in its face's own frame (crown cap, four crown bevels, four side
@@ -21,6 +23,20 @@ and taking the difference. The thickness therefore rotates with its dart instead
 being frozen in the rest orientation, so the solid darts stay correctly oriented all
 the way through the shuffle.
 
+WHY THE ANIMATED SHAPES CARRY IN-BETWEEN FRAMES
+-----------------------------------------------
+Unity interpolates a single-frame blend shape LINEARLY in vertex space, so between
+rest and a rotated pose every vertex cuts the straight chord. At the midpoint of a
+120 deg rotation the chord passes through HALF the radius — the darts dive toward
+the centre and the gaps between them yawn open, reading as the crystal blowing
+apart instead of darts sliding around on the sphere. The fix is data, not code:
+each animated channel is authored as a PROGRESSIVE morph (FBX in-between targets,
+which Unity imports as blend shape frames) sampled along each dart's true rotation
+arc via Rodrigues at equal angle steps. Between adjacent frames the residual chord
+is under 1 percent of the radius, so the darts stay on the sphere at constant
+spread for the whole sweep. SetBlendShapeWeight(i, 0..100) walks the frames
+automatically — SpaceCrystalAnimator needs no change.
+
 The whole FBX node tree is CLONED from the source and only the data arrays are
 replaced. Every node name and every FBX object id is preserved, so Unity's name-based
 sub-asset id generation (`fileIdsGeneration: 2`, `internalIDToNameTable: []`) yields
@@ -86,6 +102,72 @@ def face_frame(pts):
     return c, n
 
 
+# ---------------------------------------------------------------- rigid motion
+def dart_frame(pts):
+    """Orthonormal frame (u, v, n) + centroid of a kite, built from corner 0."""
+    c, n = face_frame(pts)
+    u = sub(pts[0], c)
+    u = normalize(sub(u, mul(n, dot(u, n))))
+    v = cross(n, u)
+    return c, (u, v, n)
+
+
+def rotation_between(F0, F1):
+    """R = F1 * F0^T where the frames' vectors are columns."""
+    return [[sum(F1[k][i] * F0[k][j] for k in range(3)) for j in range(3)]
+            for i in range(3)]
+
+
+def rot_apply(R, v):
+    return tuple(sum(R[i][j] * v[j] for j in range(3)) for i in range(3))
+
+
+def axis_angle(R):
+    tr = R[0][0] + R[1][1] + R[2][2]
+    ang = math.acos(max(-1.0, min(1.0, (tr - 1.0) / 2.0)))
+    ax = normalize((R[2][1] - R[1][2], R[0][2] - R[2][0], R[1][0] - R[0][1]))
+    return ax, ang
+
+
+def rodrigues(axis, ang, v):
+    c, s = math.cos(ang), math.sin(ang)
+    return add(add(mul(v, c), mul(cross(axis, v), s)),
+               mul(axis, dot(axis, v) * (1.0 - c)))
+
+
+def dart_rotations(verts, polys, deltas):
+    """Per dart: (axis, angle) of the pure origin rotation this shape applies to it.
+
+    Asserts the motion really is that rotation (both the corner residual and the
+    'axis passes through the origin' condition R*c0 == c1), so a future re-export
+    that breaks the assumption fails loudly instead of producing bent arcs.
+    """
+    dv = [add(v, deltas.get(i, (0.0, 0.0, 0.0))) for i, v in enumerate(verts)]
+    rots = []
+    for p in polys:
+        P = [verts[i] for i in p]
+        Q = [dv[i] for i in p]
+        c0, F0 = dart_frame(P)
+        c1, F1 = dart_frame(Q)
+        R = rotation_between(F0, F1)
+        for pi, qi in zip(P, Q):
+            r = sub(add(c1, rot_apply(R, sub(pi, c0))), qi)
+            assert math.sqrt(dot(r, r)) < 1e-2, 'dart motion is not rigid'
+        drift = sub(rot_apply(R, c0), c1)
+        assert math.sqrt(dot(drift, drift)) < 1e-2, \
+            'dart rotation axis does not pass through the origin'
+        axis, ang = axis_angle(R)
+        # Guard the axis-angle extraction itself: at ang ~ 180 deg the skew part of R
+        # vanishes and the axis degenerates, which would emit garbage arcs while every
+        # assert above still passes. Round-tripping through Rodrigues catches it.
+        for pi, qi in zip(P, Q):
+            r = sub(rodrigues(axis, ang, pi), qi)
+            assert math.sqrt(dot(r, r)) < 1e-2, \
+                'axis-angle decomposition does not reproduce the dart motion'
+        rots.append((axis, ang))
+    return rots
+
+
 # ---------------------------------------------------------------- source parsing
 def load_source(path):
     ver, root = fbx.read(path)
@@ -183,6 +265,22 @@ def build(verts, polys, shapes, profile):
     return rest, faces, out_shapes
 
 
+# How many segments each animated channel's rotation is split into. 7 in-between
+# frames + the full pose = 8 segments: 'pin' rotates 120 deg total, so 15 deg per
+# segment — the residual linear chord dips the darts under 0.9% of the radius,
+# versus 50% with no in-betweens.
+IB_SEGMENTS = 8
+
+
+def inbetween_deltas(prism_rest, rots, t):
+    """Deltas from the rest prisms with every dart rigidly rotated t of the way."""
+    out = []
+    for di, (axis, ang) in enumerate(rots):
+        for v in prism_rest[di * 12:(di + 1) * 12]:
+            out.append(sub(rodrigues(axis, ang * t, v), v))
+    return out
+
+
 def flat_normals(verts, faces):
     """One normal per polygon-vertex, matching the source's faceted shading."""
     out = []
@@ -221,6 +319,13 @@ def set_array(node, name, values, prop_type):
     child.prop_types[0] = prop_type
 
 
+def clone_node(n):
+    return fbx.Node(n.name,
+                    [list(p) if isinstance(p, list) else p for p in n.props],
+                    list(n.prop_types),
+                    [clone_node(k) for k in n.children])
+
+
 def main():
     ap = argparse.ArgumentParser()
     ap.add_argument('--force', action='store_true',
@@ -230,6 +335,15 @@ def main():
     ver, root, base, verts, polys, shapes = load_source(SRC)
```

</details>

### `3ec848b2e` — chore(crystals): TEMP - charge material on the space crystal for geometry check

_Claude, 2026-08-13 23:17:09 +0000_

```text
Diagnostic only, to be reverted after visual confirmation of the prism-dart
geometry: every space-crystal material reference in CrystalSpace,
ActiveCrystalSpace and SpaceDandruff now points at ChargeCrystalMaterial.
Both surfaces are covered - the SkinnedMeshRenderer slot AND the Crystal
component's CrystalModelData (defaultMaterial / explodingMaterial /
inactiveMaterial), because Crystal.cs re-applies those at spawn and on state
changes, so a renderer-only swap would be overwritten at runtime.

Revert this commit to restore SpaceCrystalMaterial / ActiveSpaceCrystalMaterial.
```

```text
 Assets/_Prefabs/Environment/ActiveCrystalSpace.prefab | 8 ++++----
 Assets/_Prefabs/Environment/CrystalSpace.prefab       | 8 ++++----
 Assets/_Prefabs/Environment/SpaceDandruff.prefab      | 2 +-
 3 files changed, 9 insertions(+), 9 deletions(-)
```
