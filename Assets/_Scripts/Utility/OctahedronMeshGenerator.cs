using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Generates a runtime octahedron mesh from box half-extents, using the
    /// *circumscribing dual* of the box (vertices at ±3·halfExtent on each axis),
    /// so the authored box collider nests inside the resulting shape.
    ///
    /// Geometry (circumscribing dual):
    ///   Given box half-extents (a, b, c):
    ///     vertices = { (±3a, 0, 0), (0, ±3b, 0), (0, 0, ±3c) }
    ///     inequality:   |x/(3a)| + |y/(3b)| + |z/(3c)| ≤ 1
    ///     volume:       V_oct = (4/3) * (3a)(3b)(3c) = 36·a·b·c
    ///     V_box = 8·a·b·c  →  mass ratio (shielded/unshielded) = 36/8 = 4.5
    ///
    /// 8 triangular faces, 6 shared vertices. For flat shading each face is
    /// expanded to its own 3 unique vertices (24 verts, 8 tris) so normals
    /// are not smoothed across edges.
    ///
    /// GPU MORPH DATA (Docs/PRISM_ANIMATION.md §5 B4): every vertex also carries
    /// its own face's CENTROID in <see cref="FaceCentroidUVChannel"/> (TEXCOORD1).
    /// That is the one piece of information a vertex shader cannot derive, and with
    /// it the engage bloom and the shatter overlay are pure functions of the clock
    /// (PrismShieldMorph in PrismClockAnimation.hlsl) evaluated on THIS mesh — so
    /// the cache-shared settled octahedron is also the morph mesh, and same-size
    /// shields keep batching through the whole animation instead of each rebuilding
    /// a per-prism mesh every frame.
    /// </summary>
    public static class OctahedronMeshGenerator
    {
        /// <summary>
        /// UV channel carrying each vertex's per-face centroid (object space) for the
        /// GPU shield morph. TEXCOORD1 — read by the UV node wired into BlockGraph /
        /// ExplodingBlockGraph by Tools/Shaders/wire_prism_shield_morph.py. Shared with
        /// <see cref="StellatedOctahedronMeshGenerator"/> so ONE shader path serves both
        /// shield tiers.
        /// </summary>
        /// <summary>
        /// UV channel carrying each face's own [0,1] frame, which
        /// <c>PrismErosionFade</c> sweeps its erosion front across when a shield shatters
        /// — the same body-anchored fade the exploding prism uses, instead of scaling each
        /// face down to nothing. TEXCOORD0.
        /// </summary>
        public const int ErosionUVChannel = 0;

        public const int FaceCentroidUVChannel = 1;

        /// <summary>
        /// Mass ratio between the circumscribing octahedron shield and the
        /// inscribed box, assuming uniform density.
        /// V_oct_circum / V_box = 36·a·b·c / 8·a·b·c = 4.5
        /// </summary>
        public const float SHIELD_TO_BOX_VOLUME_RATIO = 4.5f;

        /// <summary>
        /// Scale factor applied to box half-extents to produce the octahedron
        /// semi-axes. Factor 3 guarantees box-corner containment:
        /// a/(3a) + b/(3b) + c/(3c) = 1.
        /// </summary>
        public const float CIRCUMSCRIBING_SCALE = 3f;

        /// <summary>
        /// Generate a flat-shaded circumscribing octahedron mesh for a box of
        /// the given half-extents. Returns a new Mesh instance; callers are
        /// responsible for its lifecycle (DestroyImmediate/Destroy).
        /// </summary>
        public static Mesh Generate(Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            var mesh = new Mesh { name = "Octahedron_Shield" };
            PopulateMesh(mesh, halfExtents, shieldScale);
            return mesh;
        }

        // Settled-shield meshes shared across prisms, keyed by quantized geometry.
        // Half-extents come from the authored LOCAL BoxCollider size (growth animates
        // transform.localScale, not the collider), so every prism of a prefab type
        // resolves to ONE mesh — settled shielded prisms then share a MeshCollider
        // cook AND batch on the instanced render path, instead of each owning a
        // unique octahedron (the per-prism meshes behind the "different meshes with
        // GPU instancing" draw-call storm). Entries are Unity-null-checked on fetch
        // so a stale cache (play-mode exit with domain reload disabled destroys
        // runtime meshes) rebuilds instead of returning destroyed meshes.
        static readonly Dictionary<(long x, long y, long z, long s), Mesh> s_sharedShieldMeshes = new();

        /// <summary>
        /// A cache-shared full-size shield octahedron for the given geometry.
        /// Callers must NOT destroy the returned mesh — the cache owns it for the
        /// session.
        /// </summary>
        public static Mesh GetSharedShieldMesh(Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            // Quantize to 1/1024 units — far below visible precision; collapses float noise.
            var key = (x: (long)Mathf.Round(halfExtents.x * 1024f),
                       y: (long)Mathf.Round(halfExtents.y * 1024f),
                       z: (long)Mathf.Round(halfExtents.z * 1024f),
                       s: (long)Mathf.Round(shieldScale * 1024f));

            if (!s_sharedShieldMeshes.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = Generate(halfExtents, shieldScale);
                mesh.name = $"Octahedron_Shield_Shared_{key.x}x{key.y}x{key.z}";
                s_sharedShieldMeshes[key] = mesh;
            }
            return mesh;
        }

        /// <summary>
        /// Rewrite an existing mesh in-place, complete with the per-face centroids the
        /// GPU morph reads. Called once per quantized geometry (the shared cache) and by
        /// the edit-mode previews — NOT per frame: morph animation is
        /// <c>f(clock, stamp)</c> on this same mesh, never a rebuild.
        /// </summary>
        public static void PopulateMesh(Mesh mesh, Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            if (mesh == null) return;

            float a = halfExtents.x * shieldScale;
            float b = halfExtents.y * shieldScale;
            float c = halfExtents.z * shieldScale;

            // Six shared octahedron vertices (face centers of a box scaled by shieldScale).
            Vector3 pX = new Vector3( a,  0,  0);
            Vector3 nX = new Vector3(-a,  0,  0);
            Vector3 pY = new Vector3( 0,  b,  0);
            Vector3 nY = new Vector3( 0, -b,  0);
            Vector3 pZ = new Vector3( 0,  0,  c);
            Vector3 nZ = new Vector3( 0,  0, -c);

            // 8 triangular faces, one per octant. For octant (sx,sy,sz) the
            // face vertices are (sx·pX, sy·pY, sz·pZ). Winding v_X → v_Y → v_Z
            // yields an outward-pointing normal iff sx·sy·sz == +1; otherwise
            // we swap to v_X → v_Z → v_Y to flip the normal.
            //
            // For flat shading each face owns its own 3 vertices (24 verts total).
            var verts = new Vector3[24];
            var norms = new Vector3[24];
            var tangents = new List<Vector4>(24);
            var uvs   = new List<Vector2>(24);
            var cents = new List<Vector3>(24);
            var tris  = new int[24];

            int vi = 0;
            // sx·sy·sz = +1 octants: standard winding (X → Y → Z)
            AddFace(verts, norms, cents, uvs, tangents, tris, ref vi, pX, pY, pZ); // (+,+,+)
            AddFace(verts, norms, cents, uvs, tangents, tris, ref vi, pX, nY, nZ); // (+,-,-)
            AddFace(verts, norms, cents, uvs, tangents, tris, ref vi, nX, pY, nZ); // (-,+,-)
            AddFace(verts, norms, cents, uvs, tangents, tris, ref vi, nX, nY, pZ); // (-,-,+)
            // sx·sy·sz = -1 octants: flipped winding (X → Z → Y)
            AddFace(verts, norms, cents, uvs, tangents, tris, ref vi, pX, pZ, nY); // (+,-,+)
            AddFace(verts, norms, cents, uvs, tangents, tris, ref vi, pX, nZ, pY); // (+,+,-)
            AddFace(verts, norms, cents, uvs, tangents, tris, ref vi, nX, pZ, pY); // (-,+,+)
            AddFace(verts, norms, cents, uvs, tangents, tris, ref vi, nX, nZ, nY); // (-,-,-)

            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.triangles = tris;
            // Per-face centroid, one copy per vertex — the GPU morph's only
            // non-derivable input (see the class docstring). Written AFTER
            // mesh.vertices so the channel is sized against the new vertex count.
            mesh.SetUVs(FaceCentroidUVChannel, cents);
            // UV0: the erosion's face-local frame. Nothing else on the prism graphs reads
            // UV0 (verified: the only other UV node in either graph is this mesh's
            // FaceCentroidUVChannel feed), so authoring it changes no existing shading.
            mesh.SetUVs(ErosionUVChannel, uvs);
            // Tangents: the debris pipeline's RotateFacesAlongAxis reads the mesh tangent
            // as one of its rotation axes; a mesh without them hands it a zero vector and
            // the rotation silently degenerates. Set AFTER vertices (channel sizing).
            mesh.SetTangents(tangents);
            mesh.RecalculateBounds();
            // Normals are authored per-face for flat shading; do not recalculate.
        }

        // NOTE (Docs/PRISM_ANIMATION.md §5 B4): PopulateMeshFaceScale /
        // PopulateMeshFaceShatter — the per-frame CPU morph rebuilds — are GONE.
        // Both animations are now f(clock, stamp) in PrismShieldMorph_float, driven
        // off the FaceCentroidUVChannel data this generator bakes. Do not
        // reintroduce a CPU mesh-rebuild morph: it is exactly what the clock-material
        // law forbids, and it also forfeits batching (a per-prism mesh is a per-prism
        // draw call).

        // A shield SHATTERS as ordinary prism-explosion DEBRIS (Docs/PRISM_ANIMATION.md
        // §4.8.1): the shards are explosion entities on ExplodingBlockGraph, whose vertex
        // chain (RotateFacesAlongAxis) and fade (PrismErosionFade) are pure functions of
        // the mesh's own attributes. This method therefore authors the SAME attribute set
        // the exploding cube carries — the pipeline is never forked, the mesh conforms:
        //   * UV0       — the face-local frame the erosion front wipes across
        //   * NORMAL    — flat per face; the rotate subgraph's cross(velocity, n) axis
        //   * TANGENT   — in-plane per face (dP/dU of the UV frame); the subgraph's
        //                 second rotation axis, missing from these meshes until now
        //   * TEXCOORD1 — the face centroid; the shield ENGAGE bloom's pivot (the one
        //                 shield-specific animation that remains, and it is not a shatter)
        private static void AddFace(Vector3[] verts, Vector3[] norms, List<Vector3> cents,
                                    List<Vector2> uvs, List<Vector4> tangents, int[] tris, ref int vi,
                                    Vector3 v0, Vector3 v1, Vector3 v2)
        {
            int i0 = vi, i1 = vi + 1, i2 = vi + 2;
            verts[i0] = v0; verts[i1] = v1; verts[i2] = v2;

            // FACE-LOCAL UV0 — the frame PrismErosionFade wipes across
            // (PrismOcclusionCorridor.hlsl). Each triangle gets the same isoceles
            // mapping into the unit square, which is all the erosion needs: it centres
            // UV to [-1,1] and sweeps one front across it. The wipe's DIRECTION and jag
            // are hashed per entity from the stamped velocity, and each face's UV frame
            // is ORIENTED differently in object space, so the fronts still run in
            // different world directions per face — the same mechanism as the cube.
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0.5f, 1f));

            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            norms[i0] = n; norms[i1] = n; norms[i2] = n;

            // The tangent is dP/dU of the UV frame above (v0 → v1 IS the U axis), which
            // is the standard convention an imported mesh carries. Flat per face, like
            // the normal.
            Vector3 t = (v1 - v0).normalized;
            var tangent = new Vector4(t.x, t.y, t.z, 1f);
            tangents.Add(tangent); tangents.Add(tangent); tangents.Add(tangent);

            Vector3 centroid = (v0 + v1 + v2) * (1f / 3f);
            cents.Add(centroid); cents.Add(centroid); cents.Add(centroid);

            tris[i0] = i0; tris[i1] = i1; tris[i2] = i2;
            vi += 3;
        }

        /// <summary>
        /// Branchless containment test for a point in local space relative to
        /// the circumscribing octahedron. Uses the L1-norm inequality
        ///   |x|·invA + |y|·invB + |z|·invC ≤ 1
        /// where invA/B/C = 1 / (shieldScale · halfExtent).
        ///
        /// Precompute the inverses once per prism and reuse - this is the
        /// fast path for gameplay overlap checks without a MeshCollider.
        /// </summary>
        public static bool ContainsPointLocal(Vector3 localPoint, float invA, float invB, float invC)
        {
            float sum = Mathf.Abs(localPoint.x) * invA
                      + Mathf.Abs(localPoint.y) * invB
                      + Mathf.Abs(localPoint.z) * invC;
            return sum <= 1f;
        }

        /// <summary>
        /// Convenience overload taking raw half-extents. Prefer the precomputed
        /// inverse overload inside hot loops.
        /// </summary>
        public static bool ContainsPointLocal(Vector3 localPoint, Vector3 halfExtents, float shieldScale = CIRCUMSCRIBING_SCALE)
        {
            float invA = 1f / (shieldScale * halfExtents.x);
            float invB = 1f / (shieldScale * halfExtents.y);
            float invC = 1f / (shieldScale * halfExtents.z);
            return ContainsPointLocal(localPoint, invA, invB, invC);
        }
    }
}
