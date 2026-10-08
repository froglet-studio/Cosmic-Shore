# Branch archive: `claude/optimistic-planck-3f1ztv`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-08 by Claude
- **Unmerged commits:** 1
- **Forked from:** `0d38cdff3` (2026-06-08, Merge branch 'bleeding-edge' of https://github.com/froglet-studio/Cosmic-Shore)
- **Tip:** `81746e28b`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs`
  - `Assets/_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs`
  - `Assets/_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs.meta`
  - `Assets/_Scripts/Utility/StellatedOctahedronMeshGenerator.cs`

### `81746e28b` — perf(prism): render super-shield as 8 tetra faces instead of 24

_Claude, 2026-06-08 22:16:28 +0000_

```text
The stellated-octahedron super-shield is the union of two regular
tetrahedra. Each big tetra face is coplanar with the 3 spike lateral
faces tiling its corners, so rendering the 8 tetra faces as opaque
geometry is pixel-identical to the 24-spike-face stella octangula — the
protruding dual tetra occludes each face's center via the depth buffer.

Drops triangle/vertex count 3x (24->8 tris, 72->24 verts) for the static
shield mesh and both morph meshes (engage bloom, disengage shatter),
matching the two-tetrahedra model the collision test (ContainsPointLocal)
already uses. The convex MeshCollider (hull = bounding cube) and the
volume/mass math are unchanged. Material is opaque with ZWrite, so the
depth-buffer occlusion the trick relies on holds.

Adds StellatedOctahedronMeshGeneratorTests locking the 8-triangle
topology, outward unit normals, cube-corner vertices, and the
two-tetrahedra containment (core/tips inside, valleys/beyond-tip outside).
```

```text
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs   |  15 ++-
 .../_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs  | 173 ++++++++++++++++++++++++++++++++
 .../Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs.meta      |   2 +
 Assets/_Scripts/Utility/StellatedOctahedronMeshGenerator.cs           | 115 +++++++++++++--------
 4 files changed, 258 insertions(+), 47 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 399 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs b/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
index 8272923aa..6186d1a8f 100644
--- a/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
+++ b/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
@@ -19,10 +19,15 @@ namespace CosmicShore.Gameplay
     ///                      mass = ρ · 108·a·b·c (exactly 13.5× box mass by default,
     ///                      3× the inscribed octahedron shield's mass)
     ///
-    /// Engage: per-face bloom morph — 24 outer faces grow outward from their
+    /// The stellation is rendered as just its 8 tetrahedral faces (the two
+    /// constituent tetrahedra), not 24 spike faces — see
+    /// <see cref="StellatedOctahedronMeshGenerator"/>. Opaque rendering makes the
+    /// 8 triangles pixel-identical to the full stella octangula at ⅓ the cost.
+    ///
+    /// Engage: per-face bloom morph — the 8 tetra faces grow outward from their
     /// centroids.
     /// Disengage: box mesh snaps back immediately, then a shatter overlay plays
-    ///   where each of the 24 faces simultaneously shrinks and flies outward
+    ///   where each of the 8 faces simultaneously shrinks and flies outward
     ///   along its face normal, mirroring the prism destruction VFX.
     ///
     /// Fast overlap test: <see cref="IsPointInsideShield"/> uses the
@@ -224,7 +229,7 @@ namespace CosmicShore.Gameplay
             else Engage();
         }
 
-        /// <summary>Engage the super-shield with per-face bloom across all 24 faces.</summary>
+        /// <summary>Engage the super-shield with per-face bloom across all 8 tetra faces.</summary>
         public void Engage(bool instant = false)
         {
             if (_isShielded && !_isEngaging) return;
@@ -250,7 +255,7 @@ namespace CosmicShore.Gameplay
 
         /// <summary>
         /// Disengage the super-shield. Box mesh snaps back immediately; a
-        /// shatter overlay plays where each of the 24 faces flies outward
+        /// shatter overlay plays where each of the 8 tetra faces flies outward
         /// along its normal while shrinking to a point.
         /// </summary>
         public void Disengage(bool instant = false)
@@ -332,7 +337,7 @@ namespace CosmicShore.Gameplay
         // --- Mesh updates ----------------------------------------------------
 
         /// <summary>
-        /// Per-face bloom for engage: all 24 faces grow from centroid points to full size.
+        /// Per-face bloom for engage: all 8 tetra faces grow from centroid points to full size.
         /// </summary>
         private void UpdateEngageMesh(float faceScale)
         {
diff --git a/Assets/_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs b/Assets/_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs
new file mode 100644
index 000000000..2d1a901bd
--- /dev/null
+++ b/Assets/_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs
@@ -0,0 +1,173 @@
+#if UNITY_EDITOR
+using System.Collections.Generic;
+using NUnit.Framework;
+using UnityEngine;
+using CosmicShore.Utility;
+
+namespace CosmicShore.Tests
+{
+    /// <summary>
+    /// StellatedOctahedronMeshGenerator Tests — locks the two-tetrahedra model
+    /// of the super-shield (Stella Octangula).
+    ///
+    /// WHY THIS MATTERS:
+    /// The super-shielded prism is modeled as the union of two regular tetrahedra
+    /// — 8 half-space planes for collision (ContainsPointLocal) and, since each
+    /// big tetra face is coplanar with the spike faces it tiles, 8 opaque
+    /// triangles for rendering instead of 24. These tests guard both invariants:
+    ///   • the rendered mesh is exactly 8 triangles whose vertices are the 8
+    ///     cube corners (spike tips), with outward-pointing flat-shaded normals;
+    ///   • the containment test matches the stellation surface (octahedron core
+    ///     and spike tips inside; concave valleys and beyond-tip points outside).
+    /// If either drifts, the shield's silhouette or its overlap queries break.
+    /// </summary>
+    [TestFixture]
+    public class StellatedOctahedronMeshGeneratorTests
+    {
+        private const float Tol = 1e-4f;
+
+        // ---- Mesh topology -------------------------------------------------
+
+        [Test]
+        public void Mesh_Has8TrianglesAnd24Vertices()
+        {
+            Assert.AreEqual(8, StellatedOctahedronMeshGenerator.FACE_COUNT,
+                "Super-shield must render as the 8 faces of the two tetrahedra (not 24 spike faces).");
+            Assert.AreEqual(24, StellatedOctahedronMeshGenerator.VERTEX_COUNT);
+
+            var mesh = StellatedOctahedronMeshGenerator.Generate(new Vector3(0.5f, 0.5f, 0.5f));
+            try
+            {
+                Assert.AreEqual(24, mesh.vertexCount);
+                Assert.AreEqual(24, mesh.triangles.Length, "8 triangles × 3 indices.");
+                Assert.AreEqual(24, mesh.normals.Length);
+            }
+            finally { Object.DestroyImmediate(mesh); }
+        }
+
+        [Test]
+        public void Mesh_DistinctVerticesAreThe8CubeCorners()
+        {
+            var h = new Vector3(1f, 2f, 3f);
+            float s = StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
+            float a = h.x * s, b = h.y * s, c = h.z * s;
+
+            var mesh = StellatedOctahedronMeshGenerator.Generate(h);
+            try
+            {
+                var signTuples = new HashSet<(int, int, int)>();
+                foreach (var v in mesh.vertices)
+                {
+                    // Every vertex is a spike tip: a cube corner (±a, ±b, ±c).
+                    Assert.AreEqual(a, Mathf.Abs(v.x), Tol, "|x| must equal a.");
+                    Assert.AreEqual(b, Mathf.Abs(v.y), Tol, "|y| must equal b.");
+                    Assert.AreEqual(c, Mathf.Abs(v.z), Tol, "|z| must equal c.");
+                    signTuples.Add((Sign(v.x), Sign(v.y), Sign(v.z)));
+                }
+                Assert.AreEqual(8, signTuples.Count,
+                    "All 8 cube corners (spike tips) must be present exactly once as distinct positions.");
+            }
+            finally { Object.DestroyImmediate(mesh); }
+        }
+
+        [Test]
+        public void Mesh_AllFaceNormalsPointOutwardAndAreUnitFlat()
+        {
+            var mesh = StellatedOctahedronMeshGenerator.Generate(new Vector3(0.5f, 1.25f, 0.75f));
+            try
+            {
+                var verts = mesh.vertices;
+                var norms = mesh.normals;
+
+                for (int f = 0; f < StellatedOctahedronMeshGenerator.FACE_COUNT; f++)
+                {
+                    int i0 = f * 3, i1 = i0 + 1, i2 = i0 + 2;
+                    Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
+                    Vector3 n = norms[i0];
+
+                    // Flat shading: all 3 vertices share the face normal.
+                    Assert.That((norms[i1] - n).magnitude, Is.LessThan(Tol));
+                    Assert.That((norms[i2] - n).magnitude, Is.LessThan(Tol));
+                    Assert.AreEqual(1f, n.magnitude, Tol, "Face normal must be unit length.");
+
+                    // Each tetra is centered at the origin, so an outward normal
+                    // has positive dot with the face centroid.
+                    Vector3 centroid = (v0 + v1 + v2) * (1f / 3f);
```

</details>
