using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The geometry behind the hull-absorb omni-crystal morph (Brittle Star, Sparrow, Stoat, Orca, Wrasse:
    /// <c>R_VesselActions/VESSEL_ABSORB_CRYSTAL_MORPH.md</c>): the shell inside a hull's bounds, and the cage folding onto
    /// it from a crystal collected beside the hull.
    ///
    /// Negative control: the plain cast from the shell's centre fails <see cref="TheCageWrapsTheWholeHull"/> — seen from
    /// inside the hull, a crystal several units off it lies in one direction and lands on one side.
    /// </summary>
    public class VesselAbsorbCrystalMorphGeometryTests
    {
        // A long, flat hull: 4 x 2 x 10 u, centred a little forward of its pivot.
        static readonly Bounds Hull = new Bounds(new Vector3(0f, 0.2f, 1f), new Vector3(4f, 2f, 10f));
        static readonly Vector3 CrystalOffset = new Vector3(6f, 1f, 3f);   // where the crystal was, beside the nose

        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        [Test]
        public void TheShellSitsInsideTheHullsBounds()
        {
            var m = VesselAbsorbMorphGeometry.UnitSphereToShell(Hull);
            for (int i = 0; i < 6; i++)
            {
                var axis = i switch { 0 => new Vector3(1, 0, 0), 1 => new Vector3(-1, 0, 0), 2 => new Vector3(0, 1, 0),
                                      3 => new Vector3(0, -1, 0), 4 => new Vector3(0, 0, 1), _ => new Vector3(0, 0, -1) };
                var p = m.MultiplyPoint3x4(axis);
                Assert.IsTrue(Hull.Contains(p), $"{p} is outside the hull's bounds");
            }
            var tip = m.MultiplyPoint3x4(new Vector3(0, 0, 1));
            Assert.AreEqual(Hull.center.z + Hull.extents.z * VesselAbsorbMorphGeometry.Sink, tip.z, 1e-4f);
        }

        [Test]
        public void AFlatHullStillHasAVolume()
        {
            var flat = new Bounds(Vector3.zero, new Vector3(4f, 0f, 4f));
            var p = VesselAbsorbMorphGeometry.UnitSphereToShell(flat).MultiplyPoint3x4(new Vector3(0, 1, 0));
            Assert.AreEqual(VesselAbsorbMorphGeometry.MinSemiAxis, p.y, 1e-6f);
        }

        [Test]
        public void BoundsCarriedThroughAFrameEncloseEveryCorner()
        {
            var b = default(Bounds);
            bool any = false;
            var rot = Matrix4x4.TRS(new Vector3(5, 0, 0), Quaternion.identity, new Vector3(2, 2, 2));
            VesselAbsorbMorphGeometry.Encapsulate(ref b, ref any, new Bounds(Vector3.zero, Vector3.one), rot);
            Assert.IsTrue(any);
            Assert.AreEqual(new Vector3(5, 0, 0).x, b.center.x, 1e-5f);
            Assert.AreEqual(1f, b.extents.x, 1e-5f, "a unit box scaled by 2 is 2 wide");
        }

        List<Vector3> Landed(bool aroundTheCrystal, out Matrix4x4 shell)
        {
            shell = VesselAbsorbMorphGeometry.UnitSphereToShell(Hull);
            var sphere = IcosphereMeshGenerator.Generate(3, 1f, true);
            _spawned.Add(sphere);
            Vector3 centre = shell.MultiplyPoint3x4(Vector3.zero);
            Assert.IsTrue(CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(sphere, shell, centre, out var target, out string why), why);

            var cage = Cage(122, 1.5f, CrystalOffset, 20261102);
            _spawned.Add(cage);
            var morph = aroundTheCrystal
                ? CrystalMorphRecast.TryBuildAround(cage, in target, cage.bounds.center, 1f, 0f, out string diagnosis)
                : CrystalMorphMeshBuilder.TryBuild(cage, in target, 1f, 0f, out diagnosis);
            Assert.IsNotNull(morph, diagnosis);
            _spawned.Add(morph);

            var uv2 = new List<Vector4>();
            morph.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);
            var pts = new List<Vector3>(uv2.Count);
            foreach (var v in uv2) pts.Add(new Vector3(v.x, v.y, v.z));
            return pts;
        }

        [Test]
        public void EveryVertexLandsOnTheShell()
        {
            var pts = Landed(true, out var shell);
            foreach (var p in pts)
                Assert.AreEqual(1f, ToUnit(shell, p).magnitude, 0.02f, $"{p} is not on the shell");
        }

        [Test]
        public void TheCageWrapsTheWholeHull()
        {
            int Octants(List<Vector3> pts, Matrix4x4 shell)
            {
                var seen = new HashSet<int>();
                foreach (var p in pts)
                {
                    var q = ToUnit(shell, p);
                    seen.Add((q.x >= 0 ? 1 : 0) | (q.y >= 0 ? 2 : 0) | (q.z >= 0 ? 4 : 0));
                }
                return seen.Count;
            }
            Assert.AreEqual(8, Octants(Landed(true, out var s1), s1), "cast around the crystal, the cage wraps the hull");
            Assert.LessOrEqual(Octants(Landed(false, out var s2), s2), 4,
                "negative control: cast from inside the hull, a crystal beside it lands on one side");
        }

        /// <summary>A point back in the unit sphere's frame: the shell is a translate-and-scale, no rotation.</summary>
        static Vector3 ToUnit(Matrix4x4 shell, Vector3 p)
        {
            Vector3 c = shell.MultiplyPoint3x4(Vector3.zero);
            Vector3 e = shell.MultiplyPoint3x4(new Vector3(1, 1, 1)) - c;
            var d = p - c;
            return new Vector3(d.x / e.x, d.y / e.y, d.z / e.z);
        }

        static Mesh Cage(int struts, float shellRadius, Vector3 centreOffset, int seed)
        {
            var rnd = new System.Random(seed);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int[,] quads = { {0,1,3,2}, {4,6,7,5}, {0,2,6,4}, {1,5,7,3}, {0,4,5,1}, {2,3,7,6} };
            for (int s = 0; s < struts; s++)
            {
                float u = (float)(rnd.NextDouble() * 2 - 1);
                float th = (float)(rnd.NextDouble() * Mathf.PI * 2);
                float r = Mathf.Sqrt(Mathf.Max(0f, 1 - u * u));
                var centre = centreOffset + new Vector3(r * Mathf.Cos(th), u, r * Mathf.Sin(th))
                             * (shellRadius * (0.75f + 0.25f * (float)rnd.NextDouble()));
                float h = 0.06f * shellRadius;
                int b = verts.Count;
                for (int i = 0; i < 8; i++)
                    verts.Add(centre + new Vector3((i & 1) == 0 ? -h : h, (i & 2) == 0 ? -h : h, (i & 4) == 0 ? -h : h));
                for (int f = 0; f < 6; f++)
                {
                    tris.Add(b + quads[f, 0]); tris.Add(b + quads[f, 1]); tris.Add(b + quads[f, 2]);
                    tris.Add(b + quads[f, 0]); tris.Add(b + quads[f, 2]); tris.Add(b + quads[f, 3]);
                }
            }
            var mesh = new Mesh { name = "TestCage" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, false);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
