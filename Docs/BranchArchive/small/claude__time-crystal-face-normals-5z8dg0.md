# Branch archive: `claude/time-crystal-face-normals-5z8dg0`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-14 by Claude
- **Unmerged commits:** 2
- **Forked from:** `0a94cb38b` (2026-08-11, Merge pull request #707 from froglet-studio/claude/gold-shielded-prism-contras)
- **Tip:** `a17363396`
- **Files touched (11):**
  - `Assets/_Graphics/Materials/CrystalMaterials/TimeCrystalFacetMaterial.mat`
  - `Assets/_Graphics/Materials/CrystalMaterials/TimeCrystalFacetMaterial.mat.meta`
  - `Assets/_Graphics/Materials/CrystalMaterials/TimeCrystalNoEdgeMaterial.mat`
  - `Assets/_Graphics/Materials/CrystalMaterials/TimeCrystalNoEdgeMaterial.mat.meta`
  - `Assets/_Graphics/Materials/Graphs/CrystalFacetGraph.shadergraph`
  - `Assets/_Graphics/Materials/Graphs/CrystalFacetGraph.shadergraph.meta`
  - `Assets/_Graphics/Materials/Graphs/CrystalFacetNormal.hlsl`
  - `Assets/_Graphics/Materials/Graphs/CrystalFacetNormal.hlsl.meta`
  - `Assets/_Graphics/Materials/Graphs/CrystalGraphNoEdge.shadergraph`
  - `Assets/_Graphics/Materials/Graphs/CrystalGraphNoEdge.shadergraph.meta`
  - `Assets/_Prefabs/Environment/CrystalTime.prefab`

### `0c82b2268` — fix(crystals): time crystal wears the charge-crystal shader minus the edge effect

_Claude, 2026-08-12 18:30:28 +0000_

```text
The time crystal's face-flip animation ends on geometry identical to its
start but with flipped normals, and both of its materials (Fringe /
InverseFringe) computed a fresnel from the interpolated normal - so every
loop seam popped visibly. The fix removes the normal dependence instead of
chasing the normals.

- CrystalGraphNoEdge.shadergraph: byte-clone of CrystalGraph (the charge
  crystal's shader) with ONE edge retargeted - the BaseColor blend's Base
  now reads DullCrystalColor directly instead of the fresnel-darkened
  inner blend, orphaning the edge-darkening subtree out of compilation.
  The fragment stage is now provably normal-free (Blend / Distance /
  Clamp / OneMinus / Position / Property only); the camera-distance
  bright-beacon blend, the dissolve alpha, and the velocity-driven
  explode machinery all survive unchanged.

- TimeCrystalNoEdgeMaterial.mat: ChargeCrystalMaterial donor on the new
  graph. _spread 0 zeroes the normal-driven breathing term, so the vertex
  stage adds exactly nothing in idle - the loop seam is invisible by
  construction. Palette is the time crystal's canonical teal (the pair
  authored in the legacy time material and InverseFringe), with the
  bright beacon HDR-boosted at the charge material's ~8x ratio.

- CrystalTime.prefab: both renderer slots plus the Crystal component's
  default/exploding/inactive materials repointed at the new material.
  The explode husk (Crystal Explosion Dummy) therefore now runs the same
  _velocity-driven scatter the charge crystal uses. The Animator and the
  armature flip animation are untouched.

The legacy TimeCrystalGraph / TimeCrystalMaterial trio (OldCrystalTime's
noise-swirl look) is left as-is; names were chosen not to collide with it.
```

```text
 .../Materials/CrystalMaterials/TimeCrystalNoEdgeMaterial.mat          |  150 +
 .../Materials/CrystalMaterials/TimeCrystalNoEdgeMaterial.mat.meta     |    8 +
 Assets/_Graphics/Materials/Graphs/CrystalGraphNoEdge.shadergraph      | 9408 +++++++++++++++++++++++++++++++
 Assets/_Graphics/Materials/Graphs/CrystalGraphNoEdge.shadergraph.meta |   10 +
 Assets/_Prefabs/Environment/CrystalTime.prefab                        |   10 +-
 5 files changed, 9581 insertions(+), 5 deletions(-)
```

### `a17363396` — fix(crystals): restore the time crystal's facets, shaded from derived normals

_Claude, 2026-08-14 02:02:32 +0000_

```text
The previous commit killed the pop by deleting the fresnel term - but that term
IS the crystal's only per-pixel shading, so the result was a uniform teal blob.
Correct fix: keep the facet shading and take away its dependence on the MESH
normal, which is the thing that flips.

CrystalFacetNormal.hlsl (new): derives the shading normal from the surface being
rasterized - cross(ddy(positionWS), ddx(positionWS)) is the true face normal at
this instant - and orients it toward the viewer. Two consequences, both load
bearing:

  - Identical geometry now yields an identical normal BY CONSTRUCTION, so the
    animation's return to initial conditions cannot pop. Nothing is baked and
    nothing is per-mesh, so this survives a re-export, a re-rig, or a swap back
    to blend shapes with nothing to update.
  - Viewer-orientation makes a plate shade the same from either side. The
    crystal renders two-sided and its plates turn through edge-on constantly;
    this is the second, independent reason the seam is invisible.

Verified by compiling the shipped file with clang (-Wall -Wextra, clean): after
a 180-degree flip the fresnel is bit-identical, and a degenerate quad returns a
front-facing normal rather than a NaN.

Because it is a FACE normal, every fragment of a facet shares one value - flat
gem plates instead of the soft within-facet rim gradient smooth normals give.
That gradient was the "edge effect" this crystal is meant to do without, so the
brief is satisfied by the mechanism rather than by deleting shading.

  - CrystalFacetGraph.shadergraph (was CrystalGraphNoEdge): facet blend restored;
    Fresnel's Normal now reads the custom function and its View Dir a world-space
    View Vector; Power is exposed as _FacetPower. The charge crystal's own
    CrystalGraph is untouched, as are the shared Fresnel subgraphs that build the
    prism rim.
  - TimeCrystalFacetMaterial.mat (was TimeCrystalNoEdgeMaterial): _FacetPower
    0.32, matching the charge crystal. Lower = darker, higher contrast; ~1.2 is
    noticeably brighter. _spread stays 0: it displaces along the mesh normal, so
    any nonzero value would reintroduce a geometric pop at the same seam.
```

```text
 .../{TimeCrystalNoEdgeMaterial.mat => TimeCrystalFacetMaterial.mat}   |   3 +-
 ...ystalNoEdgeMaterial.mat.meta => TimeCrystalFacetMaterial.mat.meta} |   0
 .../{CrystalGraphNoEdge.shadergraph => CrystalFacetGraph.shadergraph} | 502 ++++++++++++++++++++++++++++----
 ...raphNoEdge.shadergraph.meta => CrystalFacetGraph.shadergraph.meta} |   0
 Assets/_Graphics/Materials/Graphs/CrystalFacetNormal.hlsl             |  61 ++++
 Assets/_Graphics/Materials/Graphs/CrystalFacetNormal.hlsl.meta        |   7 +
 6 files changed, 519 insertions(+), 54 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/CrystalFacetNormal.hlsl b/Assets/_Graphics/Materials/Graphs/CrystalFacetNormal.hlsl
new file mode 100644
index 000000000..331262c05
--- /dev/null
+++ b/Assets/_Graphics/Materials/Graphs/CrystalFacetNormal.hlsl
@@ -0,0 +1,61 @@
+// CrystalFacetNormal.hlsl — the crystal's shading normal, derived from the surface
+// itself rather than read from the mesh.
+//
+// PURPOSE. A crystal's whole look is its fresnel: facets that face you stay bright,
+// facets turned away darken. That term is a dot product against a NORMAL, so whatever
+// the normal does, the shading does too.
+//
+// THE BUG THIS EXISTS TO KILL. The time crystal's faces flip. The animation drives
+// them a half turn and then returns to its initial conditions — geometrically the SAME
+// crystal, but every authored normal has been carried 180 degrees around and now points
+// the other way. Skinned normals ride the bone matrices, so at the loop seam the shading
+// inverted between one frame and the next: bright facets went black. Identical geometry,
+// completely different picture, and the eye reads it as a hard cut.
+//
+// THE FIX. Derive the normal from the rasterized surface — the screen-space derivatives
+// of world position are two vectors lying IN the triangle, so their cross product is
+// that triangle's true normal at this instant. It is a pure function of the geometry
+// being drawn, so identical geometry gives an identical normal BY CONSTRUCTION and the
+// loop seam cannot pop. No baked data, no bone dependence, no per-mesh authoring — it
+// survives a re-export, a re-rig, or a swap back to blend shapes with nothing to update.
+//
+// TWO PROPERTIES WORTH NAMING:
+//   • It is a FACE normal, so every fragment of a facet shares one value. That is the
+//     faceted-gem read (each plate a flat plane of colour) rather than the soft rim
+//     gradient smooth normals give — which is the "edge effect" this crystal is meant
+//     to do without.
+//   • It is oriented toward the viewer, so a plate seen from behind shades exactly like
+//     the same plate seen from the front. The crystal renders two-sided and its plates
+//     turn through edge-on constantly; without this they would invert as they pass 90
+//     degrees. This is also the second, independent reason the flip seam is invisible.
+//
+// COST. Two derivative instructions and a cross product per fragment, on a handful of
+// crystals. No CPU work, no extra draw call, nothing to keep in sync.
+
+#ifndef CRYSTAL_FACET_NORMAL_INCLUDED
+#define CRYSTAL_FACET_NORMAL_INCLUDED
+
+// PositionWS — interpolated world position (Position node, World space).
+// ViewWS     — world-space vector from the fragment toward the camera (View Vector node,
+//              World space). Need not be normalized.
+// Out        — unit face normal in world space, hemisphere-aligned with ViewWS.
+void CrystalFacetNormal_float(float3 PositionWS, float3 ViewWS, out float3 Out)
+{
+    float3 view = ViewWS;
+    float viewLen = length(view);
+    view = viewLen > 1e-8 ? view / viewLen : float3(0.0, 0.0, 1.0);
+
+    // ddy x ddx are two in-plane tangents of the triangle under this fragment.
+    float3 faceNormal = cross(ddy(PositionWS), ddx(PositionWS));
+    float faceLen = length(faceNormal);
+
+    // A silhouette sliver or a degenerate quad can hand us a zero-area derivative pair.
+    // Fall back to the view direction: that yields dot(N,V) = 1, i.e. fresnel 0 and no
+    // darkening, which reads as a normal front-facing facet instead of a NaN speckle.
+    faceNormal = faceLen > 1e-8 ? faceNormal / faceLen : view;
+
+    // Flip toward the viewer so front and back of a plate shade identically.
+    Out = dot(faceNormal, view) < 0.0 ? -faceNormal : faceNormal;
+}
+
+#endif // CRYSTAL_FACET_NORMAL_INCLUDED
```

</details>
