using System.Collections.Generic;
using System.Text.RegularExpressions;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The plate filter that puts the charge crystal's edge discharge on the omni crystal's PENTAGONS
    /// and nowhere else (Docs/PALETTE.md §2.10). The source here is a miniature omni: three disjoint
    /// plates — a pentagonal prism, a box and a triangular prism — each authored the way the importer
    /// delivers a hard-edged model (every face its own vertices, fan-triangulated), so a pentagonal
    /// prism is 30 raw vertices and 10 corners.
    ///
    /// Negative-controlled: counting RAW vertices instead of welded corners keeps no plate at all, and
    /// normalising the edge heights by the KEPT plates' radius instead of the source's moves every
    /// height — each fails this suite.
    /// </summary>
    public class CrystalEdgeArcMeshBakerTests
    {
        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        Mesh Track(Mesh m)
        {
            if (m) _spawned.Add(m);
            return m;
        }

        /// <summary>A k-gonal prism of circumradius 1 and half-depth 0.2 centred on <paramref name="centre"/>.</summary>
        static void AddPrism(List<Vector3> verts, List<Vector3> normals, List<int> tris, int k, Vector3 centre)
        {
            var bottom = new Vector3[k];
            var top = new Vector3[k];
            for (int i = 0; i < k; i++)
            {
                float a = 2f * Mathf.PI * i / k;
                bottom[i] = centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), -0.2f);
                top[i] = centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.2f);
            }

            void Face(IReadOnlyList<Vector3> polygon)
            {
                int start = verts.Count;
                var n = Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]).normalized;
                foreach (var p in polygon) { verts.Add(p); normals.Add(n); }
                for (int i = 1; i < polygon.Count - 1; i++) tris.AddRange(new[] { start, start + i, start + i + 1 });
            }

            var reversed = new List<Vector3>(bottom);
            reversed.Reverse();
            Face(reversed);
            Face(top);
            for (int i = 0; i < k; i++)
            {
                int j = (i + 1) % k;
                Face(new[] { bottom[i], bottom[j], top[j], top[i] });
            }
        }

        /// <summary>Pentagonal prism at the origin, box at +4x, triangular prism at -4x.</summary>
        Mesh MiniOmni()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            AddPrism(verts, normals, tris, 5, Vector3.zero);
            AddPrism(verts, normals, tris, 4, new Vector3(4f, 0f, 0f));
            AddPrism(verts, normals, tris, 3, new Vector3(-4f, 0f, 0f));
            var mesh = new Mesh { name = "MiniOmni", vertices = verts.ToArray(), normals = normals.ToArray(),
                                  triangles = tris.ToArray() };
            return Track(mesh);
        }

        // Triangles per k-gonal prism: two k-gon fans (k-2 each) plus k quads (2 each).
        static int PrismTriangles(int k) => 2 * (k - 2) + 2 * k;

        [Test]
        public void PentagonFilter_KeepsOnlyThePentagonalPrism()
        {
            var baked = Track(CrystalEdgeArcMeshBaker.Bake(MiniOmni(), 10));
            Assert.IsNotNull(baked);
            Assert.AreEqual(PrismTriangles(5) * 3, baked.vertexCount, "one unwelded vertex per kept triangle corner");
            foreach (var v in baked.vertices)
                Assert.Less(v.magnitude, 1.1f, $"vertex {v} belongs to another plate");
        }

        [Test]
        public void NoFilter_KeepsEveryPlate()
        {
            var baked = Track(CrystalEdgeArcMeshBaker.Bake(MiniOmni(), 0));
            Assert.AreEqual((PrismTriangles(5) + PrismTriangles(4) + PrismTriangles(3)) * 3, baked.vertexCount);
        }

        [Test]
        public void PentagonFilter_BoltsRunOnTheFifteenCreasesOnly()
        {
            var baked = Track(CrystalEdgeArcMeshBaker.Bake(MiniOmni(), 10));
            var edgeH = new List<Vector3>();
            baked.GetUVs(2, edgeH);

            int crease = 0, diagonal = 0;
            for (int v = 0; v < edgeH.Count; v += 3)
                for (int e = 0; e < 3; e++)
                    if (edgeH[v][e] > 0f) crease++; else diagonal++;

            // 15 crease edges (5 + 5 rim, 5 lateral), each seen from both triangles that share it;
            // the rest are fan diagonals: 2 per pentagon x 2, 1 per quad x 5 — also seen twice.
            Assert.AreEqual(15 * 2, crease);
            Assert.AreEqual((2 * 2 + 5) * 2, diagonal);
        }

        [Test]
        public void PentagonFilter_KeepsTheSourceModelRadius()
        {
            var source = MiniOmni();
            var whole = Track(CrystalEdgeArcMeshBaker.Bake(source, 0));
            var filtered = Track(CrystalEdgeArcMeshBaker.Bake(source, 10));

            var wholeH = new List<Vector3>();
            var filteredH = new List<Vector3>();
            whole.GetUVs(2, wholeH);
            filtered.GetUVs(2, filteredH);

            // The pentagonal prism is emitted first in both bakes, so its triangles line up one to one.
            for (int i = 0; i < filteredH.Count; i++)
                Assert.AreEqual(wholeH[i], filteredH[i], $"edge heights of vertex {i} moved under the filter");
        }

        [Test]
        public void FilterMatchingNoPlate_ReturnsNullAndSaysWhy()
        {
            LogAssert.Expect(LogType.Error, new Regex("no plate with exactly 7 corners"));
            Assert.IsNull(CrystalEdgeArcMeshBaker.Bake(MiniOmni(), 7));
        }

        [Test]
        public void GetOrBake_CachesPerFilter()
        {
            var source = MiniOmni();
            // Tracked so TearDown destroys them; the cache then holds fake-nulls, which it re-bakes.
            var pentagons = Track(CrystalEdgeArcMeshBaker.GetOrBake(source, 10));
            var all = Track(CrystalEdgeArcMeshBaker.GetOrBake(source));

            Assert.AreSame(pentagons, CrystalEdgeArcMeshBaker.GetOrBake(source, 10));
            Assert.AreNotSame(pentagons, all);
            StringAssert.EndsWith(CrystalEdgeArcMeshBaker.BakedSuffix, pentagons.name);
            StringAssert.EndsWith(CrystalEdgeArcMeshBaker.BakedSuffix, all.name);
        }
    }
}
