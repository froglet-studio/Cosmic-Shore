using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The claims the Squirrel's omni-crystal morph rests on — the crystal's body becoming the
    /// eight shielded prisms of its boost ring — asserted against the SHIPPED
    /// <see cref="CrystalMorphMeshBuilder"/> panel census (<c>SQUIRREL_CRYSTAL_MORPH.md</c>).
    ///
    /// The source is a synthetic cage built the way the real one is (disjoint solids; prisms whose
    /// end caps are the PANELS and whose rims are the leftovers; boxes that are all leftover), so
    /// the suite exercises every branch without depending on an FBX being import-readable. The real
    /// cage is measured offline by <c>Tools/Build/measure_omni_crystal_morph.py</c>, which proves
    /// the census that makes the mapping 1:1: 40 triangular + 24 pentagonal panels = 64 = 8 × 8.
    /// </summary>
    public class CrystalMorphPanelCensusTests
    {
        const int Faces = 8;

        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        Mesh Track(Mesh m) { _spawned.Add(m); return m; }

        Mesh Build(Mesh cage, List<CrystalMorphMeshBuilder.OctahedronTarget> targets,
                   float filler = 0f, float start = 0.55f, float end = 1f)
        {
            var built = CrystalMorphMeshBuilder.TryBuild(cage, targets, filler, start, end, out var why);
            Assert.IsNotNull(built, $"builder refused a valid cage: {why}");
            return Track(built);
        }

        // ── One octahedron ─────────────────────────────────────────────────────────────────────

        [Test]
        public void EveryPanelCoversItsOctahedronFaceExactly()
        {
            var cage = Track(BuildCage(prisms: 4, boxes: 2));
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };
            var built = Build(cage, targets);

            var (panels, _) = Split(cage, built, targets);
            Assert.AreEqual(Faces, panels.Count, "one panel per octahedron face");

            float faceArea = Area(targets[0].FaceCorners[0], targets[0].FaceCorners[1], targets[0].FaceCorners[2]);
            foreach (var poly in panels.Values)
                Assert.AreEqual(faceArea, PolygonArea(poly), faceArea * 1e-3f,
                    "a panel must BECOME its face, not sit inside it — three source corners are " +
                    "anchored to the target's three corners");
        }

        [Test]
        public void LeftoverFacesCollapseIntoTheOctahedronAndGoFirst()
        {
            var cage = Track(BuildCage(prisms: 4, boxes: 2));
            var centre = new Vector3(3f, 1f, -2f);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(centre) };
            var built = Build(cage, targets);

            var uv2 = Channel(built, CrystalMorphMeshBuilder.TargetUVChannel);
            int collapsed = 0;
            for (int i = 0; i < uv2.Length; i++)
            {
                if ((Xyz(uv2[i]) - centre).sqrMagnitude > 1e-6f) continue;
                collapsed++;
                Assert.AreEqual(0f, uv2[i].w,
                    "a leftover face must be stamped phase 0 so it is absorbed BEFORE the panels land");
            }
            Assert.Greater(collapsed, 0, "the cage's quad faces must collapse into the shield");
        }

        [Test]
        public void FrameZeroIsTheSourceMeshVertexForVertex()
        {
            var cage = Track(BuildCage(prisms: 4, boxes: 2));
            var built = Build(cage, new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) });

            var verts = built.vertices;
            var srcVerts = cage.vertices;
            var tris = cage.triangles;
            Assert.AreEqual(tris.Length, verts.Length,
                "the morph mesh is UNSHARED — one vertex per triangle corner, so no vertex can need two targets");
            for (int i = 0; i < verts.Length; i++)
                Assert.AreEqual(0f, (srcVerts[tris[i]] - verts[i]).magnitude, 0f,
                    "the morph's first frame must be the crystal, exactly");
        }

        [Test]
        public void PanelPhasesSitInsideTheAuthoredBand()
        {
            var cage = Track(BuildCage(prisms: 4, boxes: 2));
            var built = Build(cage, new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) },
                              0f, 0.6f, 0.9f);

            var uv2 = Channel(built, CrystalMorphMeshBuilder.TargetUVChannel);
            int panels = 0;
            for (int i = 0; i < uv2.Length; i++)
            {
                if (Xyz(uv2[i]).sqrMagnitude < 1e-6f) continue;          // a leftover, at the centre
                panels++;
                Assert.GreaterOrEqual(uv2[i].w, 0.6f - 1e-4f);
                Assert.LessOrEqual(uv2[i].w, 0.9f + 1e-4f);
            }
            Assert.Greater(panels, 0);
        }

        [Test]
        public void ACensusMismatchFailsLoudInsteadOfMappingHalfTheCrystal()
        {
            var cage = Track(BuildCage(prisms: 3, boxes: 1));          // 6 panels, not 8
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };

            var built = CrystalMorphMeshBuilder.TryBuild(cage, targets, 0f, 0.55f, 1f, out var why);
            Assert.IsNull(built, "a cage whose panels do not match the target faces must be refused");
            StringAssert.Contains("8", why, "the diagnosis must name the count it needed");
        }

        [Test]
        public void AnUnreadableSourceMeshIsRefusedByName()
        {
            // The failure this guards is the one that shipped first: an IMPORTED mesh without
            // Read/Write does not return empty vertices, it THROWS, and the throw escapes through
            // the ring builder's event — so the only symptom is the animation not happening.
            var cage = Track(BuildCage(prisms: 4, boxes: 2));
            cage.UploadMeshData(markNoLongerReadable: true);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };

            var built = CrystalMorphMeshBuilder.TryBuild(cage, targets, 0f, 0.55f, 1f, out var why);
            Assert.IsNull(built, "an unreadable mesh must be refused, not read");
            StringAssert.Contains("Read/Write", why, "the diagnosis must name the fix");
        }

        [Test]
        public void PentagonalPanelsAreSupported()
        {
            // The omni cage's 12 pentagonal prisms contribute 24 of its 64 panels, and a pentagon
            // becoming a triangle is the case a corner-to-corner map cannot express.
            var cage = Track(BuildCage(prisms: 0, boxes: 1, pentagons: 4));
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { Octahedron(Vector3.zero) };
            var built = Build(cage, targets);

            var (panels, _) = Split(cage, built, targets);
            float faceArea = Area(targets[0].FaceCorners[0], targets[0].FaceCorners[1], targets[0].FaceCorners[2]);
            foreach (var poly in panels.Values)
            {
                Assert.AreEqual(5, poly.Count, "a pentagonal panel keeps all five corners");
                Assert.AreEqual(faceArea, PolygonArea(poly), faceArea * 1e-3f,
                    "the two corners that are not anchors must ride ON the target's edges");
            }
        }

        [Test]
        public void EveryPanelArrivesWearingItsFacesOutwardNormal()
        {
            var cage = Track(BuildCage(prisms: 4, boxes: 2));
            var target = Octahedron(Vector3.zero);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget> { target };
            var built = Build(cage, targets);

            var uv2 = Channel(built, CrystalMorphMeshBuilder.TargetUVChannel);
            var uv3 = Channel(built, CrystalMorphMeshBuilder.TargetNormalUVChannel);
            Assert.AreEqual(uv2.Length, uv3.Length, "every vertex carries a target NORMAL too");
            for (int i = 0; i < uv3.Length; i++)
            {
                Assert.AreEqual(1f, Xyz(uv3[i]).magnitude, 1e-4f, "a target normal must be unit");
                Assert.AreEqual(uv2[i].w, uv3[i].w, 0f,
                    "position and normal must carry the SAME phase, or a face is shaded before or after it lands");
            }

            // Identified per source PANEL, never per vertex: a panel corner lands ON a target
            // corner, which three faces share; the panel's whole outline is not ambiguous.
            var (panels, _) = Split(cage, built, targets);
            var normalOf = PanelNormals(cage, built, targets);
            var seen = new HashSet<int>();
            foreach (var kv in panels)
            {
                int face = FaceContainingAll(target, kv.Value);
                Assert.GreaterOrEqual(face, 0, "a panel must land on ONE of the eight faces");
                Assert.IsTrue(seen.Add(face), "two panels landed on the same face");

                Vector3 want = OutwardNormal(target, face);
                Assert.AreEqual(1f, Vector3.Dot(normalOf[kv.Key], want), 1e-4f,
                    "a panel's target normal is its FACE's OUTWARD normal — otherwise the morph lands " +
                    "the cage's normals on the shield, and the shape is right while the shading is not");
            }
            Assert.AreEqual(Faces, seen.Count, "every face received a normal");
        }

        [Test]
        public void ALeftoverKeepsItsOwnNormalRatherThanSwingingIntoNothing()
        {
            var cage = Track(BuildCage(prisms: 4, boxes: 2));
            cage.RecalculateNormals();
            var target = Octahedron(Vector3.zero);
            var built = Build(cage, new List<CrystalMorphMeshBuilder.OctahedronTarget> { target });

            var uv2 = Channel(built, CrystalMorphMeshBuilder.TargetUVChannel);
            var uv3 = Channel(built, CrystalMorphMeshBuilder.TargetNormalUVChannel);
            var sourceNormals = cage.normals;
            var tris = cage.triangles;
            int leftovers = 0;
            for (int i = 0; i < uv2.Length; i++)
            {
                if ((Xyz(uv2[i]) - target.Centre).sqrMagnitude >= 1e-6f) continue;
                leftovers++;
                Assert.AreEqual(1f, Vector3.Dot(Xyz(uv3[i]), sourceNormals[tris[i]]), 1e-4f,
                    "a leftover collapses to a POINT and has no orientation to arrive at, so its " +
                    "normal is held at its own — the blend is a no-op for it");
            }
            Assert.Greater(leftovers, 0, "the cage contributed no leftover faces");
        }

        // ── The full ring: the shipped census, eight octahedra ────────────────────────────────

        /// <summary>
        /// The shape of the real pickup: 20 triangular + 12 pentagonal panel plates and 90 struts
        /// on a sphere, landing on a ring of eight octahedra with the ring's own proportions. Every
        /// one of the 64 faces is claimed exactly once, every shield gets exactly eight panels, and
        /// every panel covers its face — so the finished ring is eight whole shields with nothing
        /// invented and nothing spare.
        /// </summary>
        [Test]
        public void TheShippedCensusFillsEveryFaceOfAllEightShieldsOnce()
        {
            var cage = Track(SphericalCage(triangles: 20, pentagons: 12, boxes: 90, radius: 7.35f));
            var targets = Ring();
            var built = Build(cage, targets);

            var (panels, _) = Split(cage, built, targets);
            Assert.AreEqual(64, panels.Count, "40 triangular + 24 pentagonal panels");

            var claimed = new HashSet<(int oct, int face)>();
            var perOct = new int[targets.Count];
            foreach (var poly in panels.Values)
            {
                int oct = -1, face = -1;
                for (int k = 0; k < targets.Count && face < 0; k++)
                {
                    face = FaceContainingAll(targets[k], poly);
                    if (face >= 0) oct = k;
                }
                Assert.GreaterOrEqual(face, 0, "every panel lands wholly on one face of one shield");
                Assert.IsTrue(claimed.Add((oct, face)), $"shield {oct} face {face} was claimed twice");
                perOct[oct]++;

                float faceArea = Area(targets[oct].FaceCorners[face * 3], targets[oct].FaceCorners[face * 3 + 1],
                                      targets[oct].FaceCorners[face * 3 + 2]);
                Assert.AreEqual(faceArea, PolygonArea(poly), faceArea * 1e-3f, "a panel covers its face exactly");
            }
            Assert.AreEqual(64, claimed.Count, "all 64 faces of the ring are filled");
            for (int k = 0; k < perOct.Length; k++)
                Assert.AreEqual(Faces, perOct[k], $"shield {k} must assemble from exactly eight panels");
        }

        /// <summary>A solid's parts travel together — both caps of one plate and its rim land on
        /// the SAME shield. Splitting a plate across two shields would tear it in mid-air.</summary>
        [Test]
        public void EverySolidLandsOnASingleShield()
        {
            var cage = Track(SphericalCage(triangles: 20, pentagons: 12, boxes: 90, radius: 7.35f));
            var targets = Ring();
            var built = Build(cage, targets);

            var uv2 = Channel(built, CrystalMorphMeshBuilder.TargetUVChannel);
            var solid = SourceSolids(cage);
            var tris = cage.triangles;
            var shieldOf = new Dictionary<int, int>();
            for (int i = 0; i < uv2.Length; i++)
            {
                int k = NearestCentre(targets, Xyz(uv2[i]));
                int s = solid[tris[i]];
                if (shieldOf.TryGetValue(s, out int have))
                    Assert.AreEqual(have, k, $"solid {s} is split between shields {have} and {k}");
                else shieldOf[s] = k;
            }
            Assert.AreEqual(122, shieldOf.Count, "the cage's 122 solids");
        }

        /// <summary>
        /// The target face set is the shield's own: each face is one OCTANT — one apex per axis,
        /// on the side its sign says — so the morph lands on the faces
        /// <see cref="OctahedronMeshGenerator"/> draws, whatever order it draws them in.
        /// </summary>
        [Test]
        public void FromSemiAxesIsTheShieldsOctantFaceSet()
        {
            var centre = new Vector3(0.1f, -0.2f, 0.3f);
            var semi = new Vector3(1.5f, 1.5f, 1.5f);
            var toLocal = Matrix4x4.TRS(new Vector3(5f, 0f, 0f), Quaternion.identity, new Vector3(1.8f, 1.8f, 7.5f));
            var t = CrystalMorphMeshBuilder.OctahedronTarget.FromSemiAxes(toLocal, centre, semi);

            Assert.AreEqual(Faces, t.FaceCount);
            Assert.AreEqual(0f, (t.Centre - toLocal.MultiplyPoint3x4(centre)).magnitude, 1e-5f);

            var octants = new HashSet<(int, int, int)>();
            for (int f = 0; f < Faces; f++)
            {
                Vector3 x = t.FaceCorners[f * 3] - t.Centre;
                Vector3 y = t.FaceCorners[f * 3 + 1] - t.Centre;
                Vector3 z = t.FaceCorners[f * 3 + 2] - t.Centre;
                Assert.AreEqual(1.5f * 1.8f, Mathf.Abs(x.x), 1e-4f, "corner 0 is the x apex at the semi-axis");
                Assert.AreEqual(1.5f * 1.8f, Mathf.Abs(y.y), 1e-4f, "corner 1 is the y apex");
                Assert.AreEqual(1.5f * 7.5f, Mathf.Abs(z.z), 1e-4f, "corner 2 is the z apex");
                Assert.IsTrue(octants.Add((System.Math.Sign(x.x), System.Math.Sign(y.y), System.Math.Sign(z.z))),
                              "each face is a different octant");
            }
            Assert.AreEqual(Faces, octants.Count);
        }

        // ── Synthetic sources and targets ─────────────────────────────────────────────────────

        /// <summary>
        /// A cage in the shape of the real one: disjoint solids, each an extruded n-gon whose two
        /// END CAPS are non-quad (the panels) and whose rims are quads (the leftovers), plus plain
        /// boxes that are all leftover. Vertices are UNSHARED per polygon, which is what a
        /// hard-edged FBX import produces and what the builder's structural face grouping reads.
        /// </summary>
        static Mesh BuildCage(int prisms, int boxes, int pentagons = 0)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float x = 0f;

            for (int i = 0; i < prisms; i++, x += 4f) AddPrism(verts, tris, new Vector3(x, 0f, 0f), Vector3.forward, 3);
            for (int i = 0; i < pentagons; i++, x += 4f) AddPrism(verts, tris, new Vector3(x, 0f, 0f), Vector3.forward, 5);
            for (int i = 0; i < boxes; i++, x += 4f) AddPrism(verts, tris, new Vector3(x, 0f, 0f), Vector3.forward, 4);

            var mesh = new Mesh { name = "SyntheticCage" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, true);
            return mesh;
        }

        /// <summary>Plates spread over a sphere (golden spiral), each extruded RADIALLY — so its
        /// two caps face in and out, as the omni cage's plates do.</summary>
        static Mesh SphericalCage(int triangles, int pentagons, int boxes, float radius)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int total = triangles + pentagons + boxes;
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < total; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / total;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var dir = new Vector3(Mathf.Cos(golden * i) * r, y, Mathf.Sin(golden * i) * r);
                int sides = i < triangles ? 3 : i < triangles + pentagons ? 5 : 4;
                AddPrism(verts, tris, dir * radius, dir, sides, 0.6f, 0.2f);
            }
            var mesh = new Mesh { name = "SphericalCage" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, true);
            return mesh;
        }

        /// <summary>One extruded n-gon along <paramref name="axis"/>: 2 n-gon caps + n quad sides,
        /// every polygon unshared.</summary>
        static void AddPrism(List<Vector3> verts, List<int> tris, Vector3 origin, Vector3 axis, int sides,
                             float size = 1f, float halfDepth = 0.5f)
        {
            axis = axis.normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);

            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * 2f * Mathf.PI / sides;
                Vector3 rim = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * size;
                bottom[i] = origin + rim - axis * halfDepth;
                top[i] = origin + rim + axis * halfDepth;
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

        /// <summary>An octahedron with the ring prisms' own proportions (long on z).</summary>
        static CrystalMorphMeshBuilder.OctahedronTarget Octahedron(Vector3 centre) =>
            CrystalMorphMeshBuilder.OctahedronTarget.FromSemiAxes(
                Matrix4x4.Translate(centre), Vector3.zero, new Vector3(2.7f, 2.7f, 11.25f));

        /// <summary>The Squirrel's ring as laid (SpawnableRings on AOERingSpawner): eight prisms at
        /// radius 8.2, 8 ahead of the hull, long axis along the ring axis, "up" radial.</summary>
        static List<CrystalMorphMeshBuilder.OctahedronTarget> Ring()
        {
            var ring = new List<CrystalMorphMeshBuilder.OctahedronTarget>(8);
            var centre = new Vector3(0f, 0f, 8f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 2f * Mathf.PI / 8f;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var toLocal = Matrix4x4.TRS(centre + radial * 8.2f,
                                            Quaternion.LookRotation(Vector3.forward, radial),
                                            new Vector3(1.8f, 1.8f, 7.5f));
                ring.Add(CrystalMorphMeshBuilder.OctahedronTarget.FromSemiAxes(toLocal, Vector3.zero,
                                                                               new Vector3(1.5f, 1.5f, 1.5f)));
            }
            return ring;
        }

        // ── Readback ──────────────────────────────────────────────────────────────────────────

        static Vector3 Xyz(Vector4 v) => new(v.x, v.y, v.z);

        static Vector4[] Channel(Mesh built, int channel)
        {
            var uv = new List<Vector4>();
            built.GetUVs(channel, uv);
            return uv.ToArray();
        }

        static bool IsLeftover(List<CrystalMorphMeshBuilder.OctahedronTarget> targets, Vector3 p)
        {
            for (int k = 0; k < targets.Count; k++)
                if ((p - targets[k].Centre).sqrMagnitude < 1e-6f) return true;
            return false;
        }

        static int NearestCentre(List<CrystalMorphMeshBuilder.OctahedronTarget> targets, Vector3 p)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int k = 0; k < targets.Count; k++)
            {
                float d = (targets[k].Centre - p).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = k; }
            }
            return best;
        }

        static Vector3 OutwardNormal(in CrystalMorphMeshBuilder.OctahedronTarget t, int face)
        {
            Vector3 a = t.FaceCorners[face * 3], b = t.FaceCorners[face * 3 + 1], c = t.FaceCorners[face * 3 + 2];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            return Vector3.Dot(n, (a + b + c) / 3f - t.Centre) < 0f ? -n : n;
        }

        /// <summary>The one face whose triangle contains EVERY point of a mapped panel. Per-point
        /// is ambiguous — a panel corner lands on a target corner, which three faces share.</summary>
        static int FaceContainingAll(in CrystalMorphMeshBuilder.OctahedronTarget t, IList<Vector3> poly)
        {
            for (int f = 0; f < t.FaceCount; f++)
            {
                Vector3 a = t.FaceCorners[f * 3], b = t.FaceCorners[f * 3 + 1], c = t.FaceCorners[f * 3 + 2];
                float whole = Area(a, b, c);
                bool all = true;
                foreach (var p in poly)
                {
                    float parts = Area(p, b, c) + Area(a, p, c) + Area(a, b, p);
                    if (Mathf.Abs(parts - whole) > whole * 1e-3f) { all = false; break; }
                }
                if (all) return f;
            }
            return -1;
        }

        /// <summary>The baked target normal of each mapped source panel, keyed as <see cref="Split"/>
        /// keys its polygons.</summary>
        static Dictionary<int, Vector3> PanelNormals(Mesh source, Mesh built,
                                                     List<CrystalMorphMeshBuilder.OctahedronTarget> targets)
        {
            var uv2 = Channel(built, CrystalMorphMeshBuilder.TargetUVChannel);
            var uv3 = Channel(built, CrystalMorphMeshBuilder.TargetNormalUVChannel);
            var faceOf = SourceFaces(source);
            var tris = source.triangles;
            var byFace = new Dictionary<int, Vector3>();
            for (int i = 0; i < uv2.Length; i++)
            {
                if (IsLeftover(targets, Xyz(uv2[i]))) continue;
                int f = faceOf[tris[i]];
                var n = Xyz(uv3[i]);
                if (byFace.TryGetValue(f, out var have))
                    Assert.AreEqual(1f, Vector3.Dot(have, n), 1e-4f, "every vertex of one panel carries ONE target normal");
                else byFace[f] = n;
            }
            return byFace;
        }

        /// <summary>Groups the emitted targets back into per-source-face polygons: panels (mapped
        /// onto a face) and leftovers (collapsed onto a centre).</summary>
        static (Dictionary<int, List<Vector3>> panels, int leftovers)
            Split(Mesh source, Mesh built, List<CrystalMorphMeshBuilder.OctahedronTarget> targets)
        {
            var uv2 = Channel(built, CrystalMorphMeshBuilder.TargetUVChannel);
            var faceOf = SourceFaces(source);
            var tris = source.triangles;
            var panels = new Dictionary<int, List<Vector3>>();
            int leftovers = 0;
            for (int i = 0; i < uv2.Length; i++)
            {
                var p = Xyz(uv2[i]);
                if (IsLeftover(targets, p)) { leftovers++; continue; }
                int f = faceOf[tris[i]];
                if (!panels.TryGetValue(f, out var list)) panels[f] = list = new List<Vector3>();
                bool seen = false;
                foreach (var q in list) if ((q - p).sqrMagnitude < 1e-10f) { seen = true; break; }
                if (!seen) list.Add(p);
            }
            return (panels, leftovers);
        }

        /// <summary>Source-face id per vertex, by the builder's own structural rule: triangles cut
        /// from one imported polygon share vertex INDICES.</summary>
        static int[] SourceFaces(Mesh mesh) => Components(mesh, weld: false);

        /// <summary>Source-solid id per vertex: connected through shared POSITIONS as well.</summary>
        static int[] SourceSolids(Mesh mesh) => Components(mesh, weld: true);

        static int[] Components(Mesh mesh, bool weld)
        {
            var verts = mesh.vertices;
            var parent = new int[verts.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            int Find(int v) { while (parent[v] != v) { parent[v] = parent[parent[v]]; v = parent[v]; } return v; }
            void Union(int a, int b) { int x = Find(a), y = Find(b); if (x != y) parent[x] = y; }

            var tris = mesh.triangles;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Union(tris[t], tris[t + 1]);
                Union(tris[t], tris[t + 2]);
            }
            if (weld)
            {
                var first = new Dictionary<Vector3Int, int>();
                for (int i = 0; i < verts.Length; i++)
                {
                    var key = new Vector3Int(Mathf.RoundToInt(verts[i].x * 1e4f), Mathf.RoundToInt(verts[i].y * 1e4f),
                                             Mathf.RoundToInt(verts[i].z * 1e4f));
                    if (first.TryGetValue(key, out int j)) Union(i, j);
                    else first[key] = i;
                }
            }
            var ids = new int[parent.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = Find(i);
            return ids;
        }

        static float Area(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross(b - a, c - a).magnitude * 0.5f;

        /// <summary>
        /// Area of a planar polygon whose points arrive in arbitrary order. The plane normal comes
        /// from NEWELL's method, not the first three points — several corners legitimately ride ONE
        /// target edge, and three collinear points give a zero normal and a silently wrong area.
        /// </summary>
        static float PolygonArea(List<Vector3> pts)
        {
            if (pts.Count < 3) return 0f;
            Vector3 n = Vector3.zero;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 a = pts[i], b = pts[(i + 1) % pts.Count];
                n += new Vector3((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y));
            }
            n = n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;

            Vector3 c = Vector3.zero;
            foreach (var p in pts) c += p;
            c /= pts.Count;
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
