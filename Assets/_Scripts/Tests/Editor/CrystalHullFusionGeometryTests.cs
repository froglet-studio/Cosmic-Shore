using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The pure half of a crystal's faces coming off it and mating with a vessel hull
    /// (CRYSTAL_HULL_FUSION.md): which face of each solid flies, where on a hull it lands, and the
    /// beat sequence.
    ///
    /// Each assertion is about something the animation depends on: a panel's corners must be the
    /// crystal's own corners (or frame 0 is not the crystal), the face nearest the hull must stay
    /// nearest and the far face must wrap to the antipode, a corner must never snap through a thin
    /// wing to its underside.
    ///
    /// Pure geometry against plain arrays, so it also RUNS headlessly:
    /// <c>Tools/Build/crystal_morph_harness/run.sh</c>.
    /// </summary>
    public class CrystalHullFusionGeometryTests
    {
        // ── Panels ────────────────────────────────────────────────────────────────────────────

        /// <summary>N unwelded unit-ish cubes on a ring of radius 2 - one vertex per triangle
        /// corner, like the charge crystal's edge-arc twin.</summary>
        static (Vector3[] verts, Vector3[] normals, int[] tris) Ring(int cubes)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int c = 0; c < cubes; c++)
            {
                float a = c * Mathf.PI * 2f / cubes;
                Vector3 centre = new(Mathf.Cos(a) * 2f, 0f, Mathf.Sin(a) * 2f);
                var corners = new Vector3[8];
                for (int i = 0; i < 8; i++)
                    corners[i] = centre + new Vector3((i & 1) == 0 ? -0.2f : 0.2f, (i & 2) == 0 ? -0.2f : 0.2f, (i & 4) == 0 ? -0.2f : 0.2f);
                int[,] faces = { { 0, 1, 3, 2 }, { 4, 6, 7, 5 }, { 0, 4, 5, 1 }, { 2, 3, 7, 6 }, { 0, 2, 6, 4 }, { 1, 5, 7, 3 } };
                for (int f = 0; f < 6; f++)
                {
                    // Wound so Cross(b - a, c - a) faces OUT, as Unity's own meshes are.
                    Vector3 n = -Vector3.Cross(corners[faces[f, 1]] - corners[faces[f, 0]], corners[faces[f, 2]] - corners[faces[f, 0]]).normalized;
                    foreach (var k in new[] { 0, 2, 1, 0, 3, 2 })
                    {
                        tris.Add(verts.Count);
                        verts.Add(corners[faces[f, k]]);
                        normals.Add(n);
                    }
                }
            }
            return (verts.ToArray(), normals.ToArray(), tris.ToArray());
        }

        [Test]
        public void Panels_OneOuterFacePerSolid_TheRestIsFiller()
        {
            var (verts, _, tris) = Ring(4);
            var set = CrystalHullFusionGeometry.BuildPanels(verts, new[] { tris });

            Assert.IsNotNull(set);
            Assert.AreEqual(4, set.PanelCount, "every separate cube is one solid, though no vertex is shared");
            for (int p = 0; p < set.PanelCount; p++)
            {
                Assert.Greater(Vector3.Dot(set.PanelNormals[p], set.Radials[p]), 0.999f,
                    "the panel is the face looking OUT of the crystal");
                Assert.AreEqual(4, set.CornerCount[p], "a cube face has four corners");
            }

            for (int cube = 0; cube < 4; cube++)
            {
                int corners = 0;
                for (int v = cube * 36; v < cube * 36 + 36; v++)
                {
                    Assert.AreEqual(cube, set.VertexPanel[v], $"vertex {v} sits in cube {cube}");
                    if (set.VertexCorner[v] >= 0) corners++;
                }
                Assert.AreEqual(6, corners, "one face of six flies (its two triangles); the other 30 vertices fold away");
            }
        }

        [Test]
        public void Panels_CornersAreTheCrystalsOwnCorners()
        {
            var (verts, _, tris) = Ring(4);
            var set = CrystalHullFusionGeometry.BuildPanels(verts, new[] { tris });
            for (int v = 0; v < verts.Length; v++)
            {
                int k = set.VertexCorner[v];
                if (k < 0) continue;
                int p = set.VertexPanel[v];
                Assert.Less((set.Corners[set.CornerStart[p] + k] - verts[v]).magnitude, 1e-5f,
                    "frame 0 must BE the crystal");
            }
        }

        [Test]
        public void Faces_NonPlanarQuadIsOneFace_ARealCreaseIsTwo()
        {
            // Two triangles sharing an edge. The charge crystal's side quads are non-planar by 5.2
            // degrees and must still read as one face; a 60-degree crease must not.
            static int PanelCorners(float foldDegrees)
            {
                float h = Mathf.Tan(foldDegrees * Mathf.Deg2Rad) * 0.7071f;
                var v = new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
                    new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, h), new Vector3(0f, 1f, 0f),
                };
                var set = CrystalHullFusionGeometry.BuildPanels(v, new[] { new[] { 0, 1, 2, 3, 4, 5 } });
                return set.CornerCount[0];
            }

            Assert.AreEqual(4, PanelCorners(5.2f));
            Assert.AreEqual(3, PanelCorners(60f));
        }

        [Test]
        public void Panels_NoTriangles_ReturnsNull()
        {
            Assert.IsNull(CrystalHullFusionGeometry.BuildPanels(new[] { Vector3.zero }, new[] { new int[0] }));
        }

        // ── The drawn template ────────────────────────────────────────────────────────────────

        static CrystalHullFusionGeometry.FusionTemplate Template(int subdivisions, out CrystalHullFusionGeometry.PanelSet set)
        {
            var (verts, normals, tris) = Ring(4);
            set = CrystalHullFusionGeometry.BuildPanels(verts, new[] { tris });
            return CrystalHullFusionGeometry.BuildTemplate(set, verts, normals, null, null, null,
                new[] { tris }, subdivisions, 2.2f);
        }

        [Test]
        public void Template_SubdividesEachPanel_AndKeepsTheFillerVerbatim()
        {
            const int n = 3;
            var template = Template(n, out var set);

            int filler = 0, panelVertices = 0;
            foreach (int k in template.VertexPoint) { if (k < 0) filler++; else panelVertices++; }
            Assert.AreEqual(4 * 30, filler, "five of each cube's six faces (10 triangles) are filler, copied as is");
            Assert.AreEqual(4 * 4 * n * n * 3, panelVertices, "a quad panel is 4 fan triangles of n x n each");

            for (int p = 0; p < set.PanelCount; p++)
            {
                Assert.AreEqual(1 + 4 * n * (n + 1) / 2, template.PointCount[p],
                    "grid points are shared across the fan's spokes: the centre, then n(n+1)/2 per fan triangle");
            }
        }

        [Test]
        public void Template_PanelPointsStartOnTheCrystalsOwnFace()
        {
            var template = Template(4, out var set);
            for (int v = 0; v < template.Vertices.Length; v++)
            {
                int k = template.VertexPoint[v];
                if (k < 0) continue;
                int p = template.VertexPanel[v];
                Vector2 q = template.Points[template.PointStart[p] + k];
                Vector3 rebuilt = set.PanelCentroids[p] + template.AxisU[p] * q.x + template.AxisV[p] * q.y;
                Assert.Less((rebuilt - template.Vertices[v]).magnitude, 1e-5f, "frame 0 must BE the crystal");
                Assert.AreEqual(0f, Vector3.Dot(template.Vertices[v] - set.PanelCentroids[p], set.PanelNormals[p]), 1e-5f,
                    "a subdivided panel is still flat on the crystal's face");
            }
        }

        [Test]
        public void Template_OnlyThePanelOutlineCarriesBolts()
        {
            const int n = 3;
            var template = Template(n, out _);
            int boltEdges = 0;
            for (int v = 0; v < template.Vertices.Length; v += 3)
            {
                if (template.VertexPoint[v] < 0) continue;
                for (int e = 0; e < 3; e++) if (template.EdgeH[v][e] > 0f) boltEdges++;
                Assert.AreEqual(Vector3.right, template.Bary[v], "each sub-triangle carries its own barycentric basis");
            }
            Assert.AreEqual(4 * 4 * n, boltEdges, "4 panels x 4 outline edges x n segments - interior edges never fire");
        }

        [Test]
        public void Template_PanelWindingMatchesTheCrystal()
        {
            var template = Template(2, out var set);
            for (int s = 0; s < template.SubmeshTriangles.Length; s++)
            {
                var t = template.SubmeshTriangles[s];
                for (int i = 0; i < t.Length; i += 3)
                {
                    int a = t[i];
                    if (template.VertexPoint[a] < 0) continue;
                    Vector3 cross = Vector3.Cross(template.Vertices[t[i + 1]] - template.Vertices[a],
                                                  template.Vertices[t[i + 2]] - template.Vertices[a]);
                    Assert.Greater(Vector3.Dot(cross, set.PanelNormals[template.VertexPanel[a]]), 0f,
                        "a reversed sub-triangle is culled - the panel would show holes");
                }
            }
        }

        // ── Landing ───────────────────────────────────────────────────────────────────────────

        /// <summary>A flat 10x10 skin at z = 0, facing +z, two triangles per cell.</summary>
        static CrystalHullFusionGeometry.HullSurface FlatSkin(float cellSize, out Vector3[] verts)
        {
            var v = new List<Vector3>(); var nrm = new List<Vector3>(); var t = new List<int>();
            for (int y = 0; y <= 10; y++) for (int x = 0; x <= 10; x++) { v.Add(new Vector3(x, y, 0f)); nrm.Add(Vector3.forward); }
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                {
                    int i = y * 11 + x;
                    t.AddRange(new[] { i, i + 1, i + 11, i + 1, i + 12, i + 11 }); // wound to face +z
                }
            verts = v.ToArray();
            return new CrystalHullFusionGeometry.HullSurface(verts, nrm.ToArray(), t.ToArray(), cellSize);
        }

        [Test]
        public void Surface_ProjectsOntoTheSkin_InsideALargeTriangle()
        {
            var skin = FlatSkin(0.25f, out _);
            Assert.IsTrue(skin.TryProject(new Vector3(3.4f, 6.3f, 0.2f), Vector3.forward, 0.5f,
                out var point, out var normal, out int nearest));
            Assert.Less((point - new Vector3(3.4f, 6.3f, 0f)).magnitude, 1e-5f,
                "the foot of the perpendicular, though no vertex is within the 0.25 cell");
            Assert.Greater(Vector3.Dot(normal, Vector3.forward), 0.999f);
            Assert.GreaterOrEqual(nearest, 0);
        }

        [Test]
        public void Surface_NeverProjectsThroughToAnUnderside()
        {
            var skin = FlatSkin(0.5f, out _);
            Assert.IsFalse(skin.TryProject(new Vector3(3f, 3f, -0.2f), Vector3.back, 1f, out _, out _, out _),
                "a point under the skin asking for a downward face finds none");
        }

        [Test]
        public void ClosestPoint_OnTriangle_ClampsToEdgesAndCorners()
        {
            Vector3 a = Vector3.zero, b = Vector3.right, c = Vector3.up;
            Assert.Less((CrystalHullFusionGeometry.ClosestPointOnTriangle(new Vector3(-1f, -1f, 1f), a, b, c, out var bc) - a).magnitude, 1e-6f);
            Assert.AreEqual(1f, bc.x, 1e-6f);
            Assert.Less((CrystalHullFusionGeometry.ClosestPointOnTriangle(new Vector3(0.5f, -2f, 0f), a, b, c, out _) - new Vector3(0.5f, 0f, 0f)).magnitude, 1e-6f);
            Assert.Less((CrystalHullFusionGeometry.ClosestPointOnTriangle(new Vector3(0.2f, 0.2f, 3f), a, b, c, out bc) - new Vector3(0.2f, 0.2f, 0f)).magnitude, 1e-6f);
            Assert.AreEqual(1f, bc.x + bc.y + bc.z, 1e-6f);
        }

        // ── Hull spots ────────────────────────────────────────────────────────────────────────

        [Test]
        public void HullSpot_PicksOutermostOutwardVertex_NotAnInnerFace()
        {
            var verts = new[]
            {
                new Vector3(0f, 0f, 1f),   // inner face, facing in
                new Vector3(0f, 0f, 2f),   // outer skin
                new Vector3(0f, 0f, 1.5f), // between, facing out but nearer
                new Vector3(0f, 0f, -2f),
            };
            var normals = new[] { Vector3.back, Vector3.forward, Vector3.forward, Vector3.back };

            int spot = CrystalHullFusionGeometry.SelectHullSpot(verts, normals, Vector3.zero, Vector3.one * 2f,
                Vector3.forward, Mathf.Cos(10f * Mathf.Deg2Rad), 1);
            Assert.AreEqual(1, spot);
        }

        [Test]
        public void HullSpot_DirectionIsInNormalisedSpace()
        {
            // The same 45-degree direction lands on a different vertex once the hull is wide: in a
            // 4:1 hull the diagonal of the BOX is (4,1), not (1,1). That is what spreads patches by
            // area over a flat hull instead of piling them on its tips.
            var verts = new[] { new Vector3(1f, 1f, 0f), new Vector3(4f, 1f, 0f) };
            var normals = new[] { new Vector3(1f, 1f, 0f).normalized, new Vector3(1f, 1f, 0f).normalized };
            Vector3 diagonal = new Vector3(1f, 1f, 0f).normalized;
            float cone = Mathf.Cos(5f * Mathf.Deg2Rad);

            Assert.AreEqual(0, CrystalHullFusionGeometry.SelectHullSpot(verts, normals, Vector3.zero,
                Vector3.one, diagonal, cone, 1));
            Assert.AreEqual(1, CrystalHullFusionGeometry.SelectHullSpot(verts, normals, Vector3.zero,
                new Vector3(4f, 1f, 1f), diagonal, cone, 1));
        }

        [Test]
        public void HullSpot_EmptyHull_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, CrystalHullFusionGeometry.SelectHullSpot(new Vector3[0], null, Vector3.zero,
                Vector3.one, Vector3.up, 0.9f, 1));
        }

        // ── Wrap ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Wrap_ContactFaceStays_FarFaceGoesToAntipode_EquatorHolds()
        {
            Vector3 pole = Vector3.up; // the hull side the crystal came from

            Assert.Less((CrystalHullFusionGeometry.WrapDirection(-pole, pole) - pole).magnitude, 1e-6f,
                "the face that touched the hull lands at the contact");
            Assert.Less((CrystalHullFusionGeometry.WrapDirection(pole, pole) + pole).magnitude, 1e-6f,
                "the face on the crystal's far side wraps to the hull's far side");
            Assert.Less((CrystalHullFusionGeometry.WrapDirection(Vector3.right, pole) - Vector3.right).magnitude, 1e-6f);

            var r = new Vector3(0.3f, -0.8f, 0.52f).normalized;
            float before = Vector3.Angle(r, -pole), after = Vector3.Angle(CrystalHullFusionGeometry.WrapDirection(r, pole), pole);
            Assert.AreEqual(before, after, 1e-3f, "every face keeps its angular distance from the contact");
        }

        [Test]
        public void Spots_AreSpreadEvenly_AndStartAtTheContact()
        {
            // A 7x7 grid on a plane facing +z: 4 spots must take the four corners after the seed.
            var verts = new List<Vector3>();
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                    verts.Add(new Vector3(x, y, 1f));
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.forward;

            int centre = 3 * 7 + 3;
            var spots = CrystalHullFusionGeometry.FarthestPointSpots(verts.ToArray(), normals, Vector3.zero,
                centre, 5, 1, out float spacing);

            Assert.AreEqual(centre, spots[0], "the contact is always the first spot");
            var corners = new HashSet<int> { 0, 6, 42, 48 };
            for (int k = 1; k < 5; k++) Assert.IsTrue(corners.Contains(spots[k]), $"spot {k} = {spots[k]} is a corner");
            Assert.AreEqual(Mathf.Sqrt(18f), spacing, 1e-4f,
                "the last corner's nearest neighbour is the centre seed, half a diagonal away");
        }

        [Test]
        public void Spots_SkipInwardFacingVertices()
        {
            var verts = new[] { new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, -1f) };
            var normals = new[] { Vector3.forward, Vector3.forward, Vector3.back }; // [1] faces IN
            var spots = CrystalHullFusionGeometry.FarthestPointSpots(verts, normals, Vector3.zero, 0, 2, 1, out _);
            Assert.AreEqual(2, spots[1], "the far vertex is further, but it is an inner face");
        }

        [Test]
        public void Assignment_IsTheOptimum_NotTheGreedyMatch()
        {
            // Greedy takes (0,0)=0 first and is then forced into (1,1)=100: total 100.
            // The optimum crosses: (0,1)+(1,0) = 1 + 1.
            var cost = new float[,] { { 0f, 1f }, { 1f, 100f } };
            var assignment = CrystalHullFusionGeometry.AssignMinCost(cost);
            Assert.AreEqual(1, assignment[0]);
            Assert.AreEqual(0, assignment[1]);
        }

        [Test]
        public void Assignment_IsAPermutation()
        {
            var rng = new System.Random(7);
            const int n = 60;
            var cost = new float[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    cost[i, j] = (float)rng.NextDouble();
            var assignment = CrystalHullFusionGeometry.AssignMinCost(cost);
            var seen = new HashSet<int>(assignment);
            Assert.AreEqual(n, seen.Count, "every face gets its own patch");
        }
    }
}
