using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Builds the mesh that carries a collected crystal's BODY onto another shape — the geometry
    /// behind a vessel's bespoke omni-crystal retirement, the per-hull replacement for the shared
    /// husk spray.
    ///
    /// The mesh is the SOURCE mesh, vertex for vertex, plus TWO extra attributes per vertex:
    /// TEXCOORD2 = (target position, phase) and TEXCOORD3 = (target NORMAL, the same phase). A
    /// vertex shader lerps both off one stamped clock (<c>CrystalMorph.hlsl</c>), so the animation
    /// costs zero CPU per frame and — critically — at t = 0 the mesh renders EXACTLY as the crystal
    /// did, because it IS the crystal's geometry with the crystal's own normals, tangents and UVs.
    /// That identity is what makes the hand-off seamless; do not "optimise" it by re-generating a
    /// simplified cage.
    ///
    /// **The normal is not decoration.** Both the crystal's shader and the shape it lands on derive
    /// their base colour from <c>(1 − N·V)⁴</c> through the same <c>FresnelColors</c> subgraph, so a
    /// morph that carried only POSITION would arrive with the crystal cage's normals sitting on the
    /// target's faces: the right shape wearing the wrong surface, which is a seam no colour match
    /// can close.
    ///
    /// ── The target is a CONVEX HULL, and the mapping is per-vertex ────────────────────────────
    /// Every source vertex slides along its own ray from the hull's centre until it meets the
    /// hull's surface, and takes that facet's normal. This needs no census, no assignment and no
    /// 1:1 correspondence between the crystal's panels and the target's faces — which matters,
    /// because the crystal's cage (122 disjoint solids, 64 non-quad panels) has no arithmetic
    /// relationship to a subdivided icosphere's 320 facets. What it gives up is the "every panel
    /// becomes exactly one face" reading; what it buys is that the last frame lies EXACTLY on the
    /// target's real surface, with the target's real per-facet normals, for any convex target.
    ///
    /// A hull's surface is a continuous function of direction, so adjacent source vertices landing
    /// either side of a crease land on the crease — the cage folds around the shape rather than
    /// tearing.
    ///
    /// ── PHASE is per SOLID, not per vertex ────────────────────────────────────────────────────
    /// A solid is found by welded POSITION (connected components over shared vertices), which is
    /// what keeps one strut's faces travelling together instead of stretching it between two
    /// schedules. Each solid's phase is its centroid's rank by distance from the centre, mapped
    /// into <c>[phaseStart, phaseEnd]</c> — so authoring <c>start &gt; end</c> inverts the cascade
    /// (outermost-first becomes innermost-first) with no code change.
    /// </summary>
    public static class CrystalMorphMeshBuilder
    {
        /// <summary>UV channel carrying (target position .xyz, phase .w). Read by CrystalMorph.hlsl.</summary>
        public const int TargetUVChannel = 2;

        /// <summary>UV channel carrying (target normal .xyz, phase .w). Read by CrystalMorphNormal
        /// in CrystalMorph.hlsl. The phase is duplicated rather than shared because a Custom
        /// Function node reads one input: position and normal must travel on the SAME schedule or a
        /// face's shading arrives before or after its shape.</summary>
        public const int TargetNormalUVChannel = 3;

        /// <summary>Weld tolerance for the solid grouping, in the source mesh's own local units.</summary>
        public const float WeldEpsilon = 1e-4f;

        /// <summary>
        /// The shape a crystal is morphing into, as a closed convex hull in the morph object's own
        /// local space: a centre strictly inside it, and its triangles flat in <see cref="Corners"/>
        /// (face f owns [3f, 3f+2]) with one outward <see cref="Normals"/> entry each.
        ///
        /// Built from the target's OWN shipped mesh — never a re-derived approximation — so the
        /// morph's last frame and the target's first frame are the same geometry and there is no
        /// second authority to drift from.
        /// </summary>
        public readonly struct ConvexHullTarget
        {
            public readonly Vector3 Centre;
            /// <summary>Faces × 3 corners, flat. Face f owns [3f, 3f+2].</summary>
            public readonly Vector3[] Corners;
            /// <summary>One outward normal per face.</summary>
            public readonly Vector3[] Normals;

            public ConvexHullTarget(Vector3 centre, Vector3[] corners, Vector3[] normals)
            {
                Centre = centre;
                Corners = corners;
                Normals = normals;
            }

            public int FaceCount => Corners == null ? 0 : Corners.Length / 3;
            public bool IsValid => Corners != null && Normals != null
                                   && Corners.Length >= 3 && Corners.Length % 3 == 0
                                   && Normals.Length == Corners.Length / 3;

            /// <summary>
            /// Reads a target hull straight out of a mesh, transformed by <paramref name="toLocal"/>
            /// into the morph object's frame. The normal is recomputed from the transformed corners
            /// rather than carried from the mesh, because a transform can mirror (and a flat-shaded
            /// icosphere's authored normals are per-corner duplicates of the same face normal
            /// anyway) — deriving it keeps the outward sense correct by construction, which is
            /// checked against the centre.
            /// </summary>
            public static bool TryFromMesh(Mesh mesh, Matrix4x4 toLocal, Vector3 centreLocal,
                                           out ConvexHullTarget target, out string diagnosis)
            {
                target = default;
                diagnosis = null;

                if (mesh == null) { diagnosis = "the target mesh is null"; return false; }
                if (!mesh.isReadable)
                {
                    diagnosis = $"the target mesh '{mesh.name}' is not readable — a generated mesh " +
                                "is readable by default, so an unreadable one came from an importer " +
                                "with Read/Write off";
                    return false;
                }

                var verts = mesh.vertices;
                var tris = mesh.triangles;
                if (tris.Length < 3 || tris.Length % 3 != 0)
                {
                    diagnosis = $"the target mesh '{mesh.name}' has {tris.Length} indices — not a " +
                                "whole number of triangles";
                    return false;
                }

                int faces = tris.Length / 3;
                var corners = new Vector3[tris.Length];
                var normals = new Vector3[faces];

                for (int f = 0; f < faces; f++)
                {
                    Vector3 a = toLocal.MultiplyPoint3x4(verts[tris[3 * f]]);
                    Vector3 b = toLocal.MultiplyPoint3x4(verts[tris[3 * f + 1]]);
                    Vector3 c = toLocal.MultiplyPoint3x4(verts[tris[3 * f + 2]]);
                    corners[3 * f] = a;
                    corners[3 * f + 1] = b;
                    corners[3 * f + 2] = c;

                    Vector3 n = Vector3.Cross(b - a, c - a);
                    float len = n.magnitude;
                    // A degenerate facet cannot state a direction; point it outward from the centre
                    // so it can never flip a vertex's shading inside out.
                    Vector3 outward = (a + b + c) / 3f - centreLocal;
                    n = len > 1e-8f ? n / len : outward.normalized;
                    if (Vector3.Dot(n, outward) < 0f) n = -n;
                    normals[f] = n;
                }

                target = new ConvexHullTarget(centreLocal, corners, normals);
                return true;
            }
        }

        /// <summary>
        /// Emits the morph mesh: the source's geometry unshared (one vertex per triangle corner),
        /// with every vertex's hull destination in TEXCOORD2 and its destination NORMAL in
        /// TEXCOORD3, both stamped with its solid's phase.
        ///
        /// Returns null with a <paramref name="diagnosis"/> naming the fix rather than throwing —
        /// this runs inside an impact-effect dispatch, and an exception there unwinds a caller that
        /// has already minted the thing being morphed into.
        /// </summary>
        /// <param name="source">The crystal's own cage mesh. MUST be Read/Write enabled.</param>
        /// <param name="target">The hull to land on, in the same local space as the morph object.</param>
        /// <param name="phaseStart">Phase of the solid nearest the centre.</param>
        /// <param name="phaseEnd">Phase of the solid furthest from it. Author below
        /// <paramref name="phaseStart"/> to invert the cascade.</param>
        public static Mesh TryBuild(Mesh source, in ConvexHullTarget target,
                                    float phaseStart, float phaseEnd, out string diagnosis)
        {
            diagnosis = null;

            if (source == null) { diagnosis = "the crystal exposed no source mesh"; return null; }
            if (!source.isReadable)
            {
                diagnosis = $"'{source.name}' is not Read/Write enabled, so its vertices cannot be " +
                            "read on the CPU (an imported mesh THROWS rather than returning empty). " +
                            "Fix it on the model importer: select the FBX, tick Read/Write, apply.";
                return null;
            }
            if (!target.IsValid)
            {
                diagnosis = "the target hull is empty or malformed (corners must be a whole number " +
                            "of triangles with one normal each)";
                return null;
            }

            var srcVerts = source.vertices;
            var srcTris = source.triangles;
            if (srcTris.Length < 3)
            {
                diagnosis = $"'{source.name}' has no triangles to carry";
                return null;
            }

            // ── Solids, by welded position ────────────────────────────────────────────────────
            // Union-find over the source's own index buffer: two corners at the same POSITION are
            // the same point of the same solid, whichever triangles reference them. This is the
            // opposite grouping to anything face-based, and it is what keeps a strut's six faces
            // travelling on one schedule instead of stretching between two.
            int[] weld = WeldMap(srcVerts, WeldEpsilon);
            var parent = new int[srcVerts.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int i = 0; i < srcVerts.Length; i++) Union(parent, i, weld[i]);
            for (int t = 0; t < srcTris.Length; t += 3)
            {
                Union(parent, srcTris[t], srcTris[t + 1]);
                Union(parent, srcTris[t + 1], srcTris[t + 2]);
            }

            // Solid id per source vertex, plus each solid's centroid distance from the centre.
            var solidOf = new Dictionary<int, int>();
            var solidSumR = new List<float>();
            var solidCount = new List<int>();
            var vertSolid = new int[srcVerts.Length];
            for (int i = 0; i < srcVerts.Length; i++)
            {
                int root = Find(parent, i);
                if (!solidOf.TryGetValue(root, out int id))
                {
                    id = solidSumR.Count;
                    solidOf[root] = id;
                    solidSumR.Add(0f);
                    solidCount.Add(0);
                }
                vertSolid[i] = id;
                solidSumR[id] += (srcVerts[i] - target.Centre).magnitude;
                solidCount[id]++;
            }

            int solids = solidSumR.Count;
            var solidRadius = new float[solids];
            for (int s = 0; s < solids; s++)
                solidRadius[s] = solidCount[s] > 0 ? solidSumR[s] / solidCount[s] : 0f;

            float minR = float.MaxValue, maxR = float.MinValue;
            for (int s = 0; s < solids; s++)
            {
                if (solidRadius[s] < minR) minR = solidRadius[s];
                if (solidRadius[s] > maxR) maxR = solidRadius[s];
            }
            float span = Mathf.Max(1e-5f, maxR - minR);

            var solidPhase = new float[solids];
            for (int s = 0; s < solids; s++)
                solidPhase[s] = Mathf.Lerp(phaseStart, phaseEnd, (solidRadius[s] - minR) / span);

            // ── Per-unique-position hull landing ──────────────────────────────────────────────
            // Cached by welded index so the ~2.9k distinct points of a cage are cast once each,
            // not once per triangle corner.
            var landedPos = new Vector3[srcVerts.Length];
            var landedNrm = new Vector3[srcVerts.Length];
            var landed = new bool[srcVerts.Length];

            var faceCentroidDir = new Vector3[target.FaceCount];
            for (int f = 0; f < target.FaceCount; f++)
            {
                Vector3 c = (target.Corners[3 * f] + target.Corners[3 * f + 1] + target.Corners[3 * f + 2]) / 3f;
                faceCentroidDir[f] = (c - target.Centre).normalized;
            }

            for (int i = 0; i < srcVerts.Length; i++)
            {
                int w = weld[i];
                if (!landed[w])
                {
                    LandOnHull(srcVerts[w], in target, faceCentroidDir,
                               out landedPos[w], out landedNrm[w]);
                    landed[w] = true;
                }
                landedPos[i] = landedPos[w];
                landedNrm[i] = landedNrm[w];
            }

            // ── Emit, unshared ────────────────────────────────────────────────────────────────
            // One vertex per triangle corner. The source's own normals/tangents/UV0 are carried
            // verbatim so frame 0 IS the crystal; only the two target channels are new.
            var srcNormals = source.normals;
            var srcTangents = source.tangents;
            var srcUv0 = source.uv;
            bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
            bool hasTangents = srcTangents != null && srcTangents.Length == srcVerts.Length;
            bool hasUv0 = srcUv0 != null && srcUv0.Length == srcVerts.Length;

            int n = srcTris.Length;
            var verts = new Vector3[n];
            var normals2 = new Vector3[n];
            var tangents = new Vector4[n];
            var uv0 = new Vector2[n];
            var uv2 = new Vector4[n];
            var uv3 = new Vector4[n];
            var tris = new int[n];

            for (int k = 0; k < n; k++)
            {
                int si = srcTris[k];
                verts[k] = srcVerts[si];
                normals2[k] = hasNormals ? srcNormals[si] : Vector3.up;
                tangents[k] = hasTangents ? srcTangents[si] : new Vector4(1f, 0f, 0f, 1f);
                uv0[k] = hasUv0 ? srcUv0[si] : Vector2.zero;

                float phase = Mathf.Clamp01(solidPhase[vertSolid[si]]);
                uv2[k] = new Vector4(landedPos[si].x, landedPos[si].y, landedPos[si].z, phase);
                uv3[k] = new Vector4(landedNrm[si].x, landedNrm[si].y, landedNrm[si].z, phase);
                tris[k] = k;
            }

            var mesh = new Mesh
            {
                name = $"CrystalMorph_{source.name}",
                indexFormat = n > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals2);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(TargetUVChannel, uv2);
            mesh.SetUVs(TargetNormalUVChannel, uv3);
            mesh.SetTriangles(tris, 0, false);

            // The morph DISPLACES vertices in the vertex stage, so the culling envelope has to
            // cover both ends of the animation or the mesh is frustum-culled mid-flight. The union
            // of the source's bounds and the hull's is exact: every vertex travels a straight line
            // between a point in one and a point in the other, and both are convex.
            var bounds = source.bounds;
            for (int f = 0; f < target.Corners.Length; f++) bounds.Encapsulate(target.Corners[f]);
            mesh.bounds = bounds;

            return mesh;
        }

        /// <summary>
        /// Slides one point along its own ray from the hull's centre until it meets the hull.
        ///
        /// The facet is picked by best angular fit (max dot against the facet centroids' directions)
        /// and then VERIFIED by barycentric containment, falling back to an exhaustive ray test when
        /// the cheap pick misses — which it can near a vertex of an irregular hull. The verify is
        /// what makes the fast path safe rather than merely usual.
        /// </summary>
        static void LandOnHull(Vector3 point, in ConvexHullTarget target, Vector3[] faceCentroidDir,
                               out Vector3 position, out Vector3 normal)
        {
            Vector3 d = point - target.Centre;
            float len = d.magnitude;
            if (len < 1e-6f)
            {
                // A point AT the centre has no direction to slide along, so it stays put and keeps
                // its own shading. It is inside the hull either way, which is all the morph needs.
                position = target.Centre;
                normal = Vector3.up;
                return;
            }
            d /= len;

            int best = -1;
            float bestDot = float.MinValue;
            for (int f = 0; f < faceCentroidDir.Length; f++)
            {
                float dot = Vector3.Dot(d, faceCentroidDir[f]);
                if (dot > bestDot) { bestDot = dot; best = f; }
            }

            if (best >= 0 && TryRayFace(target.Centre, d, in target, best, out position))
            {
                normal = target.Normals[best];
                return;
            }

            for (int f = 0; f < target.FaceCount; f++)
            {
                if (f == best) continue;
                if (!TryRayFace(target.Centre, d, in target, f, out position)) continue;
                normal = target.Normals[f];
                return;
            }

            // Unreachable for a closed hull containing the centre. Degrading to the best facet's
            // plane keeps the vertex on the surface rather than leaving it hanging in space.
            int fallback = Mathf.Max(0, best);
            normal = target.Normals[fallback];
            Vector3 a = target.Corners[3 * fallback];
            float denom = Vector3.Dot(d, normal);
            float t = Mathf.Abs(denom) > 1e-6f ? Vector3.Dot(a - target.Centre, normal) / denom : len;
            position = target.Centre + d * Mathf.Max(0f, t);
        }

        /// <summary>Möller–Trumbore, front and back faces alike — the ray starts inside the hull, so
        /// the only hit that exists is the one leaving through this facet.</summary>
        static bool TryRayFace(Vector3 origin, Vector3 dir, in ConvexHullTarget target, int face,
                               out Vector3 hit)
        {
            hit = default;
            Vector3 a = target.Corners[3 * face];
            Vector3 b = target.Corners[3 * face + 1];
            Vector3 c = target.Corners[3 * face + 2];

            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return false;

            float inv = 1f / det;
            Vector3 tv = origin - a;
            float u = Vector3.Dot(tv, p) * inv;
            if (u < -1e-4f || u > 1f + 1e-4f) return false;

            Vector3 q = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(dir, q) * inv;
            if (v < -1e-4f || u + v > 1f + 1e-4f) return false;

            float t = Vector3.Dot(e2, q) * inv;
            if (t <= 0f) return false;

            hit = origin + dir * t;
            return true;
        }

        /// <summary>Maps every vertex index onto the lowest index sharing its position, so a solid's
        /// connectivity survives the importer having split corners apart for shading.</summary>
        static int[] WeldMap(Vector3[] verts, float epsilon)
        {
            var map = new int[verts.Length];
            var buckets = new Dictionary<Vector3Int, List<int>>(verts.Length);
            float inv = 1f / Mathf.Max(1e-6f, epsilon);

            for (int i = 0; i < verts.Length; i++)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(verts[i].x * inv),
                    Mathf.RoundToInt(verts[i].y * inv),
                    Mathf.RoundToInt(verts[i].z * inv));

                if (!buckets.TryGetValue(key, out var list))
                {
                    list = new List<int>(4);
                    buckets[key] = list;
                }

                int hit = -1;
                for (int j = 0; j < list.Count; j++)
                {
                    if ((verts[list[j]] - verts[i]).sqrMagnitude <= epsilon * epsilon) { hit = list[j]; break; }
                }

                if (hit < 0) { list.Add(i); hit = i; }
                map[i] = hit;
            }
            return map;
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }

        static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }
    }
}
