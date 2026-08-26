using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The claims the Squirrel's omni-crystal morph rests on, asserted against the SHIPPED
    /// <see cref="CrystalMorphMeshBuilder"/> (`SQUIRREL_CRYSTAL_MORPH.md`).
    ///
    /// The source here is a synthetic cage rather than the omni crystal's own — one built the
    /// same way the real one is (disjoint solids; prisms whose end caps are the panels and whose
    /// rims are the leftovers), so it exercises every branch without the suite depending on an
    /// FBX being import-readable. The real cage is measured separately and offline by
    /// `Tools/Build/measure_omni_crystal_morph.py`, which proves the census that makes this
    /// mapping 1:1 in the first place: 40 triangular + 24 pentagonal faces = 64 = 8 × 8.
    /// </summary>
    public class CrystalMorphMeshBuilderTests
    {
        const int Faces = 8;

        // ── The claims ───────────────────────────────────────────────────────────────────

        [Test]
        public void EveryPanelCoversItsOctahedronFaceExactly()
        {
            var mesh = BuildCage(prisms: 4, boxes: 2);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };

            var built = CrystalMorphMeshBuilder.TryBuild(mesh, targets, 0f, 0.55f, 1f, out var why);
            Assert.IsNotNull(built, $"builder refused a valid cage: {why}");

            var (panels, _) = Split(mesh, built, targets[0]);
            Assert.AreEqual(Faces, panels.Count, "one panel per octahedron face");

            float faceArea = Area(targets[0].FaceCorners[0], targets[0].FaceCorners[1], targets[0].FaceCorners[2]);
            foreach (var poly in panels.Values)
                Assert.AreEqual(faceArea, PolygonArea(poly), faceArea * 1e-3f,
                    "a panel must BECOME its face, not sit inside it — the corner map anchors " +
                    "three source corners to the target's three corners");

            Object.DestroyImmediate(built);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void PanelsCoverEveryFaceExactlyOnce()
        {
            var mesh = BuildCage(prisms: 4, boxes: 2);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };
            var built = CrystalMorphMeshBuilder.TryBuild(mesh, targets, 0f, 0.55f, 1f, out _);

            var (panels, _) = Split(mesh, built, targets[0]);
            var claimed = new HashSet<int>();
            foreach (var poly in panels.Values)
            {
                int face = NearestFace(targets[0], Centroid(poly));
                Assert.IsTrue(claimed.Add(face), $"face {face} was claimed by two panels");
            }
            Assert.AreEqual(Faces, claimed.Count);

            Object.DestroyImmediate(built);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void LeftoverFacesCollapseIntoTheOctahedronAndGoFirst()
        {
            var mesh = BuildCage(prisms: 4, boxes: 2);
            var centre = new Vector3(3f, 1f, -2f);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(centre) };
            var built = CrystalMorphMeshBuilder.TryBuild(mesh, targets, 0f, 0.55f, 1f, out _);

            var uv2 = TargetChannel(built);
            int collapsed = 0;
            for (int i = 0; i < uv2.Length; i++)
            {
                var p = new Vector3(uv2[i].x, uv2[i].y, uv2[i].z);
                if ((p - centre).sqrMagnitude > 1e-6f) continue;
                collapsed++;
                Assert.AreEqual(0f, uv2[i].w,
                    "a leftover face must be stamped phase 0 so it is absorbed BEFORE the panels land");
            }
            Assert.Greater(collapsed, 0, "the cage's quad faces must collapse into the shield");

            Object.DestroyImmediate(built);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void FrameZeroIsTheSourceMeshVertexForVertex()
        {
            var mesh = BuildCage(prisms: 4, boxes: 2);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };
            var built = CrystalMorphMeshBuilder.TryBuild(mesh, targets, 0f, 0.55f, 1f, out _);

            var verts = built.vertices;
            var tris = mesh.triangles;
            Assert.AreEqual(tris.Length, verts.Length,
                "the morph mesh is UNSHARED — one vertex per triangle corner, so no vertex can " +
                "need two targets");
            for (int i = 0; i < verts.Length; i++)
                Assert.AreEqual(mesh.vertices[tris[i]], verts[i],
                    "the morph's first frame must be the crystal, exactly — that is the whole " +
                    "reason it draws the crystal's own geometry");

            Object.DestroyImmediate(built);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void PanelPhasesSitInsideTheAuthoredBand()
        {
            var mesh = BuildCage(prisms: 4, boxes: 2);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };
            var built = CrystalMorphMeshBuilder.TryBuild(mesh, targets, 0f, 0.6f, 0.9f, out _);

            var uv2 = TargetChannel(built);
            for (int i = 0; i < uv2.Length; i++)
            {
                var p = new Vector3(uv2[i].x, uv2[i].y, uv2[i].z);
                if (p.sqrMagnitude < 1e-6f) continue;          // a leftover, at the centre
                Assert.GreaterOrEqual(uv2[i].w, 0.6f - 1e-4f);
                Assert.LessOrEqual(uv2[i].w, 0.9f + 1e-4f);
            }

            Object.DestroyImmediate(built);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ACensusMismatchFailsLoudInsteadOfMappingHalfTheCrystal()
        {
            var mesh = BuildCage(prisms: 3, boxes: 1);          // 6 panels, not 8
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };

            var built = CrystalMorphMeshBuilder.TryBuild(mesh, targets, 0f, 0.55f, 1f, out var why);
            Assert.IsNull(built, "a cage whose panels do not match the target faces must be refused");
            Assert.IsNotNull(why);
            StringAssert.Contains("8", why, "the diagnosis must name the count it needed");

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void AnUnreadableSourceMeshIsRefusedByName()
        {
            // The failure this guards is the one that shipped: an IMPORTED mesh without
            // Read/Write does not return empty vertices, it THROWS — and the throw escapes
            // through whatever raised the event, so the only symptom is the animation silently
            // not happening. The guard has to run before any vertex is touched.
            var mesh = BuildCage(prisms: 4, boxes: 2);
            mesh.UploadMeshData(markNoLongerReadable: true);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };

            var built = CrystalMorphMeshBuilder.TryBuild(mesh, targets, 0f, 0.55f, 1f, out var why);
            Assert.IsNull(built, "an unreadable mesh must be refused, not read");
            Assert.IsNotNull(why);
            StringAssert.Contains("Read/Write", why, "the diagnosis must name the fix");

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void PentagonalPanelsAreSupported()
        {
            // The omni crystal's 12 pentagonal prisms contribute 24 of its 64 panels, and a
            // pentagon becoming a triangle is the case a corner-to-corner map cannot express.
            var mesh = BuildCage(prisms: 0, boxes: 1, pentagons: 4);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };

            var built = CrystalMorphMeshBuilder.TryBuild(mesh, targets, 0f, 0.55f, 1f, out var why);
            Assert.IsNotNull(built, $"pentagonal panels refused: {why}");

            var (panels, _) = Split(mesh, built, targets[0]);
            float faceArea = Area(targets[0].FaceCorners[0], targets[0].FaceCorners[1], targets[0].FaceCorners[2]);
            foreach (var poly in panels.Values)
            {
                Assert.AreEqual(5, poly.Count, "a pentagonal panel keeps all five corners");
                Assert.AreEqual(faceArea, PolygonArea(poly), faceArea * 1e-3f,
                    "the two corners that are not anchors must ride ON the target's edges, so " +
                    "they add vertices without changing the shape");
            }

            Object.DestroyImmediate(built);
            Object.DestroyImmediate(mesh);
        }

        // ── Synthetic source ─────────────────────────────────────────────────────────────

        /// <summary>
        /// A cage in the shape of the real one: disjoint solids, each an extruded n-gon whose two
        /// END CAPS are non-quad (the panels) and whose rims are quads (the leftovers), plus
        /// plain boxes that are all leftover. Vertices are UNSHARED per polygon, which is what a
        /// hard-edged FBX import produces and what the builder's structural face grouping reads.
        /// </summary>
        static Mesh BuildCage(int prisms, int boxes, int pentagons = 0)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float x = 0f;

            for (int i = 0; i < prisms; i++, x += 4f) AddPrism(verts, tris, new Vector3(x, 0f, 0f), 3);
            for (int i = 0; i < pentagons; i++, x += 4f) AddPrism(verts, tris, new Vector3(x, 0f, 0f), 5);
            for (int i = 0; i < boxes; i++, x += 4f) AddPrism(verts, tris, new Vector3(x, 0f, 0f), 4);

            var mesh = new Mesh { name = "SyntheticCage" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, true);
            return mesh;
        }

        /// <summary>One extruded n-gon: 2 n-gon caps + n quad sides, every polygon unshared.</summary>
        static void AddPrism(List<Vector3> verts, List<int> tris, Vector3 origin, int sides)
        {
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * 2f * Mathf.PI / sides;
                bottom[i] = origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a), -0.5f);
                top[i] = origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.5f);
            }
            AddPolygon(verts, tris, bottom);
            AddPolygon(verts, tris, top);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                AddPolygon(verts, tris, new[] { bottom[i], bottom[j], top[j], top[i] });
            }
        }

        static void AddPolygon(List<Vector3> verts, List<int> tris, IList<Vector3> poly)
        {
            int b = verts.Count;
            foreach (var p in poly) verts.Add(p);
            for (int k = 1; k < poly.Count - 1; k++) { tris.Add(b); tris.Add(b + k); tris.Add(b + k + 1); }
        }

        /// <summary>An octahedron target with the ring's own proportions (long on z).</summary>
        static CrystalMorphMeshBuilder.OctahedronTarget Octahedron(Vector3 centre)
        {
            Vector3 ax = new(2.7f, 0f, 0f), ay = new(0f, 2.7f, 0f), az = new(0f, 0f, 11.25f);
            var corners = new Vector3[Faces * 3];
            int w = 0;
            for (int sx = 0; sx < 2; sx++)
                for (int sy = 0; sy < 2; sy++)
                    for (int sz = 0; sz < 2; sz++)
                    {
                        corners[w++] = centre + (sx == 0 ? ax : -ax);
                        corners[w++] = centre + (sy == 0 ? ay : -ay);
                        corners[w++] = centre + (sz == 0 ? az : -az);
                    }
            return new CrystalMorphMeshBuilder.OctahedronTarget(centre, corners);
        }

        // ── Readback ─────────────────────────────────────────────────────────────────────

        static Vector4[] TargetChannel(Mesh built)
        {
            var uv = new List<Vector4>();
            built.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv);
            return uv.ToArray();
        }

        /// <summary>Groups the emitted targets back into per-source-face polygons, split into
        /// panels (mapped onto a face) and leftovers (collapsed onto the centre).</summary>
        static (Dictionary<int, List<Vector3>> panels, int leftovers)
            Split(Mesh source, Mesh built, in CrystalMorphMeshBuilder.OctahedronTarget target)
        {
            var uv2 = TargetChannel(built);
            var faceOf = SourceFaces(source);
            var panels = new Dictionary<int, List<Vector3>>();
            int leftovers = 0;
            for (int i = 0; i < uv2.Length; i++)
            {
                var p = new Vector3(uv2[i].x, uv2[i].y, uv2[i].z);
                if ((p - target.Centre).sqrMagnitude < 1e-6f) { leftovers++; continue; }
                int f = faceOf[source.triangles[i]];
                if (!panels.TryGetValue(f, out var list)) panels[f] = list = new List<Vector3>();
                if (!list.Contains(p)) list.Add(p);
            }
            return (panels, leftovers);
        }

        /// <summary>Source-face id per vertex, by the same structural rule the builder uses:
        /// triangles cut from one imported polygon share vertex INDICES.</summary>
        static int[] SourceFaces(Mesh mesh)
        {
            var parent = new int[mesh.vertexCount];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            int Find(int v) { while (parent[v] != v) { parent[v] = parent[parent[v]]; v = parent[v]; } return v; }
            void Union(int a, int b) { int x = Find(a), y = Find(b); if (x != y) parent[x] = y; }

            var tris = mesh.triangles;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Union(tris[t], tris[t + 1]);
                Union(tris[t], tris[t + 2]);
            }
            var ids = new int[parent.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = Find(i);
            return ids;
        }

        static int NearestFace(in CrystalMorphMeshBuilder.OctahedronTarget t, Vector3 p)
        {
            int best = 0; float bestSqr = float.MaxValue;
            for (int f = 0; f < t.FaceCount; f++)
            {
                Vector3 c = (t.FaceCorners[f * 3] + t.FaceCorners[f * 3 + 1] + t.FaceCorners[f * 3 + 2]) / 3f;
                float d = (c - p).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = f; }
            }
            return best;
        }

        static Vector3 Centroid(List<Vector3> pts)
        {
            var c = Vector3.zero;
            foreach (var p in pts) c += p;
            return c / pts.Count;
        }

        static float Area(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross(b - a, c - a).magnitude * 0.5f;

        /// <summary>
        /// Area of a planar polygon whose points arrive in arbitrary order. The plane normal
        /// comes from NEWELL's method, not from the first three points — several of a panel's
        /// corners legitimately ride ONE target edge, and three collinear points give a zero
        /// normal and a silently wrong area.
        /// </summary>
        static float PolygonArea(List<Vector3> pts)
        {
            if (pts.Count < 3) return 0f;
            Vector3 n = Vector3.zero;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 a = pts[i], b = pts[(i + 1) % pts.Count];
                n += new Vector3((a.y - b.y) * (a.z + b.z),
                                 (a.z - b.z) * (a.x + b.x),
                                 (a.x - b.x) * (a.y + b.y));
            }
            n = n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;

            Vector3 c = Centroid(pts);
            Vector3 u = Vector3.Cross(n, pts[0] - c);
            u = u.sqrMagnitude > 1e-20f ? Vector3.Cross(u, n).normalized : Vector3.right;
            Vector3 v = Vector3.Cross(n, u);

            var ordered = new List<Vector3>(pts);
            ordered.Sort((p, q) => Mathf.Atan2(Vector3.Dot(p - c, v), Vector3.Dot(p - c, u))
                        .CompareTo(Mathf.Atan2(Vector3.Dot(q - c, v), Vector3.Dot(q - c, u))));

            float area = 0f;
            for (int i = 1; i < ordered.Count - 1; i++) area += Area(ordered[0], ordered[i], ordered[i + 1]);
            return area;
        }
    }
}
