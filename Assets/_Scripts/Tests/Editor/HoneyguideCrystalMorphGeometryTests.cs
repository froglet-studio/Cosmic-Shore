using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The geometry behind the Honeyguide's omni-crystal morph (<c>R_VesselActions/HONEYGUIDE_CRYSTAL_MORPH.md</c>): the
    /// crystal's 64 panels breaking into the eight cells at the heart of the hive comb, laid hiveOffset (40 u) behind
    /// where it was collected, as <c>HoneyguideCrew.LayComb</c> lays it (radius-3 hex, spacing 2.5).
    /// </summary>
    public class HoneyguideCrystalMorphGeometryTests
    {
        const float Spacing = 2.5f;   // HoneyguideConfig.hiveCellSpacing
        const float Offset = 40f;     // HoneyguideConfig.hiveOffset
        const float CellSemiAxis = 0.9f;

        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        /// <summary>LayComb's cell positions: axial (q, r) of a radius-3 hex, on the plane square to the flight, behind.</summary>
        static List<Vector3> Comb(int radius = 3)
        {
            var cells = new List<Vector3>();
            float root3 = Mathf.Sqrt(3f);
            for (int r = -radius; r <= radius; r++)
                for (int q = Mathf.Max(-radius, -r - radius); q <= Mathf.Min(radius, -r + radius); q++)
                    cells.Add(new Vector3(Spacing * root3 * (q + r / 2f), -Spacing * 1.5f * r, -Offset));
            return cells;
        }

        [Test]
        public void AFullCombPicksItsCentreAndFirstRing()
        {
            var cells = Comb();
            Assert.AreEqual(37, cells.Count);
            var picked = HiveCombMorphGeometry.CentralCells(cells, HiveCombMorphGeometry.CellsCrystalFills);
            Assert.AreEqual(8, picked.Count);
            var centre = new Vector3(0f, 0f, -Offset);
            Assert.AreEqual(0f, (cells[picked[0]] - centre).magnitude, 1e-4f, "the comb's middle cell comes first");
            float ring = Spacing * Mathf.Sqrt(3f);
            for (int i = 1; i < 7; i++)
                Assert.AreEqual(ring, (cells[picked[i]] - centre).magnitude, 1e-3f, "then its six neighbours");
            Assert.Greater((cells[picked[7]] - centre).magnitude, ring + 1e-3f, "the eighth is from the next ring");
        }

        [Test]
        public void ThePickIsTheSameEveryTime()
        {
            var a = HiveCombMorphGeometry.CentralCells(Comb(), 8);
            var b = HiveCombMorphGeometry.CentralCells(Comb(), 8);
            CollectionAssert.AreEqual(a, b, "ties keep the lower index, so every peer agrees");
        }

        [Test]
        public void ACombTooSmallPicksNothing()
        {
            Assert.AreEqual(0, HiveCombMorphGeometry.CentralCells(Comb(1), 8).Count, "7 cells cannot take 8 octahedra");
            Assert.AreEqual(0, HiveCombMorphGeometry.CentralCells(null, 8).Count);
        }

        [Test]
        public void EveryPlateLandsInOneOfTheHeartCells()
        {
            var cells = Comb();
            var picked = HiveCombMorphGeometry.CentralCells(cells, 8);
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget>();
            foreach (int i in picked)
                targets.Add(CrystalMorphMeshBuilder.OctahedronTarget.FromSemiAxes(
                    Matrix4x4.Translate(cells[i]), Vector3.zero, Vector3.one * CellSemiAxis));

            var cage = SphericalCage(32, 6, 3f);   // 32 extruded triangles: 64 panels, as the omni cage
            _spawned.Add(cage);
            var morph = CrystalMorphMeshBuilder.TryBuild(cage, targets, 0f, 0.55f, 1f, out string why);
            Assert.IsNotNull(morph, why);
            _spawned.Add(morph);

            var uv2 = new List<Vector4>();
            morph.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);
            var hit = new HashSet<int>();
            foreach (var v in uv2)
            {
                var p = new Vector3(v.x, v.y, v.z);
                int inside = -1;
                for (int k = 0; k < picked.Count; k++)
                {
                    var q = p - cells[picked[k]];
                    if (Mathf.Abs(q.x) + Mathf.Abs(q.y) + Mathf.Abs(q.z) <= CellSemiAxis * 1.001f) { inside = k; break; }
                }
                Assert.GreaterOrEqual(inside, 0, $"{p} landed outside every heart cell");
                hit.Add(inside);
            }
            Assert.AreEqual(8, hit.Count, "all eight heart cells take plates");
        }

        static Mesh SphericalCage(int triangles, int boxes, float radius)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int total = triangles + boxes;
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < total; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / total;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var dir = new Vector3(Mathf.Cos(golden * i) * r, y, Mathf.Sin(golden * i) * r);
                AddPrism(verts, tris, dir * radius, dir, i < triangles ? 3 : 4, 0.6f, 0.2f);
            }
            var mesh = new Mesh { name = "SphericalCage" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, true);
            return mesh;
        }

        static void AddPrism(List<Vector3> verts, List<int> tris, Vector3 origin, Vector3 axis, int sides, float size, float halfDepth)
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
    }
}
