using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The geometry behind the HULL crystal retirement — an elemental crystal's cage opening onto
    /// the sphere that circumscribes the vessel that took it
    /// (<c>_Scripts/Controller/Vessel/R_VesselActions/VESSEL_CRYSTAL_ABSORB.md</c>).
    ///
    /// The sibling of <see cref="CrystalMorphMeshBuilderTests"/>, and it guards a different claim:
    /// that suite proves the BUILDER lands a cage on a convex hull, this one proves the two
    /// decisions the hull path makes on top of it — that the target is built from the crystal's own
    /// frame so the landing radius is exactly what was asked for, and that the STAGGER is off
    /// because this cage has no radial spread to stagger.
    ///
    /// That second one is the reason this file exists. The shipped Mass cage welds into 60 solids
    /// whose mean radii are identical to within ~1.4e-6, and the builder's own
    /// <c>span = Max(1e-5, maxR - minR)</c> floor divides that float noise by the epsilon and
    /// emits a phase band across 16 distinct values. It looks like a cascade and nothing authored
    /// it. If someone re-exports the cage with genuine radial structure,
    /// <see cref="ShippedElementalCageIsASingleRadiusShell"/> fails and the decision gets revisited
    /// instead of silently continuing to be wrong in the other direction.
    /// </summary>
    public class VesselCrystalAbsorbMorphTests
    {
        // Measured from the repo: Rhino hull circumscribing radius, and the Mass cage's world
        // radius at its shipped scales (shell 1.38 x root 1.5).
        const float RhinoHullRadius = 18.51f;
        const float MassCageWorldScale = 1.38f * 1.5f;

        const string MassCagePath = "Assets/_Models/MassCrystalExport1_8-21-25.fbx";

        readonly List<Mesh> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _spawned) if (m) Object.DestroyImmediate(m);
            _spawned.Clear();
        }

        Mesh Track(Mesh m) { _spawned.Add(m); return m; }

        /// <summary>A shell of N disjoint quads at ONE radius — the shape an elemental cage is,
        /// and therefore the shape whose phase spread is degenerate.</summary>
        Mesh SingleRadiusShell(int facets = 24, float radius = 1f)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < facets; i++)
            {
                // Fibonacci-ish placement; only the RADIUS matters for what this tests.
                float z = 1f - 2f * (i + 0.5f) / facets;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float a = i * 2.39996f;
                var n = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z).normalized;
                var t = Vector3.Cross(n, Vector3.up);
                t = t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.right;
                var b = Vector3.Cross(n, t);
                int v0 = verts.Count;
                verts.Add(n * radius + (t + b) * 0.05f);
                verts.Add(n * radius + (t - b) * 0.05f);
                verts.Add(n * radius + (-t - b) * 0.05f);
                verts.Add(n * radius + (-t + b) * 0.05f);
                tris.AddRange(new[] { v0, v0 + 1, v0 + 2, v0, v0 + 2, v0 + 3 });
            }
            var mesh = Track(new Mesh { name = "SingleRadiusShell" });
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        CrystalMorphMeshBuilder.ConvexHullTarget ShellTarget(float localRadius)
        {
            // Exactly the matrix VesselCrystalAbsorbMorph.TryBegin builds: a unit icosphere scaled
            // into the crystal's own local space.
            var sphere = Track(IcosphereMeshGenerator.Generate(
                IcosphereMeshGenerator.DefaultSubdivisions, 1f, true));
            Assert.IsTrue(CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                    sphere, Matrix4x4.Scale(Vector3.one * localRadius), Vector3.zero,
                    out var target, out string diagnosis),
                $"the landing shell could not be read: {diagnosis}");
            return target;
        }

        static void ReadTargets(Mesh morph, out List<Vector4> positions, out List<Vector4> normals)
        {
            positions = new List<Vector4>();
            normals = new List<Vector4>();
            morph.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, positions);
            morph.GetUVs(CrystalMorphMeshBuilder.TargetNormalUVChannel, normals);
        }

        [Test]
        public void CageLandsOnTheShellAtTheRequestedRadius()
        {
            // Every fraction the doc's tuning table quotes, so the table cannot drift from the code.
            foreach (float fraction in new[] { 1f, 0.55f, 0.25f })
            {
                float localRadius = RhinoHullRadius * fraction / MassCageWorldScale;
                var target = ShellTarget(localRadius);
                var morph = CrystalMorphMeshBuilder.TryBuild(SingleRadiusShell(), in target, 0f, 0f,
                                                             out string diagnosis);
                Assert.IsNotNull(morph, $"fraction {fraction}: {diagnosis}");
                Track(morph);
                ReadTargets(morph, out var pos, out var nrm);
                Assert.AreEqual(morph.vertexCount, pos.Count, "one target per vertex");
                Assert.AreEqual(morph.vertexCount, nrm.Count, "one target normal per vertex");

                // A point landing ON a level-2 icosphere sits between its inradius and its radius -
                // NOT on the circumscribed sphere. Asserting the sphere would be asserting the
                // wrong shape and would pass only by tolerance.
                float min = float.MaxValue, max = 0f;
                for (int i = 0; i < pos.Count; i++)
                {
                    float r = new Vector3(pos[i].x, pos[i].y, pos[i].z).magnitude;
                    min = Mathf.Min(min, r);
                    max = Mathf.Max(max, r);
                }
                Assert.GreaterOrEqual(min, localRadius * 0.98f,
                    $"fraction {fraction}: a vertex landed inside the polyhedron's inradius");
                Assert.LessOrEqual(max, localRadius * 1.0001f,
                    $"fraction {fraction}: a vertex landed outside the polyhedron");

                // The whole point of scaling in the MATRIX: the landing radius must track the ask.
                Assert.AreEqual(localRadius, max, localRadius * 0.02f,
                    $"fraction {fraction}: the landing radius does not track targetHullFraction");
            }
        }

        [Test]
        public void EveryVertexArrivesWearingItsFacetsOutwardNormal()
        {
            var target = ShellTarget(4f);
            var morph = Track(CrystalMorphMeshBuilder.TryBuild(SingleRadiusShell(), in target,
                                                               0f, 0f, out string diagnosis));
            Assert.IsNotNull(morph, diagnosis);
            ReadTargets(morph, out var pos, out var nrm);

            // Both graphs shade from (1 - N.V)^4, so a shape that arrives without its normals is
            // the right surface wearing the wrong shading - the seam this exists to remove.
            for (int i = 0; i < pos.Count; i++)
            {
                var p = new Vector3(pos[i].x, pos[i].y, pos[i].z);
                var n = new Vector3(nrm[i].x, nrm[i].y, nrm[i].z);
                Assert.Greater(Vector3.Dot(n.normalized, p.normalized), 0.97f,
                    $"vertex {i}'s target normal does not point outward from the shell");
                Assert.AreEqual(pos[i].w, nrm[i].w, 1e-6f,
                    $"vertex {i}: the position and normal phases have drifted apart");
            }
        }

        [Test]
        public void ShellMovesAsOneBecauseItHasNoRadialSpreadToStagger()
        {
            var target = ShellTarget(4f);
            var morph = Track(CrystalMorphMeshBuilder.TryBuild(SingleRadiusShell(), in target,
                                                               0f, 0f, out _));
            ReadTargets(morph, out var pos, out _);
            for (int i = 0; i < pos.Count; i++)
                Assert.AreEqual(0f, pos[i].w, 1e-6f,
                    "the hull path passes ONE phase, so the shader's stagger term is identically " +
                    "zero however CrystalMorphConfig is tuned");
        }

        [Test]
        public void StaggeringASingleRadiusShellIsFloatNoise()
        {
            // The NEGATIVE CONTROL for the decision above: hand the builder the config's phases and
            // watch a cage with no radial structure come back with a spread anyway. This is the
            // behaviour the shipped call avoids, asserted so the reason is not just a comment.
            var target = ShellTarget(4f);
            var morph = Track(CrystalMorphMeshBuilder.TryBuild(SingleRadiusShell(), in target,
                                                               1f, 0f, out _));
            ReadTargets(morph, out var pos, out _);
            float min = 1f, max = 0f;
            for (int i = 0; i < pos.Count; i++) { min = Mathf.Min(min, pos[i].w); max = Mathf.Max(max, pos[i].w); }
            Assert.Greater(max - min, 1e-4f,
                "a single-radius shell handed phaseNear != phaseFar is expected to produce a " +
                "spread from float noise amplified by the builder's 1e-5 span floor. If this now " +
                "passes cleanly the builder's floor has changed and the hull path can use the " +
                "config's phases after all.");
        }

        [Test]
        public void ShippedElementalCageIsASingleRadiusShell()
        {
            var cage = AssetDatabase.LoadAssetAtPath<Mesh>(MassCagePath);
            if (!cage)
            {
                // A missing model is a different problem and not this test's to report.
                Assert.Ignore($"{MassCagePath} is not in this checkout.");
                return;
            }
            Assert.IsTrue(cage.isReadable,
                $"{MassCagePath} is not Read/Write enabled, so CrystalMorphMeshBuilder cannot read " +
                "its vertices and the hull morph refuses (named) at runtime. Tick Read/Write on the " +
                "model importer.");

            // Weld + union-find, the builder's own grouping, so this measures what it measures.
            var v = cage.vertices;
            var tris = cage.triangles;
            var parent = new int[v.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }

            var quantized = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(v[i].x / CrystalMorphMeshBuilder.WeldEpsilon),
                    Mathf.RoundToInt(v[i].y / CrystalMorphMeshBuilder.WeldEpsilon),
                    Mathf.RoundToInt(v[i].z / CrystalMorphMeshBuilder.WeldEpsilon));
                if (quantized.TryGetValue(key, out int first)) Union(i, first); else quantized[key] = i;
            }
            for (int t = 0; t < tris.Length; t += 3)
            {
                Union(tris[t], tris[t + 1]);
                Union(tris[t + 1], tris[t + 2]);
            }

            var sum = new Dictionary<int, float>();
            var count = new Dictionary<int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                int r = Find(i);
                sum[r] = (sum.TryGetValue(r, out float s) ? s : 0f) + v[i].magnitude;
                count[r] = (count.TryGetValue(r, out int n) ? n : 0) + 1;
            }

            float minR = float.MaxValue, maxR = 0f;
            foreach (int r in sum.Keys)
            {
                float mean = sum[r] / count[r];
                minR = Mathf.Min(minR, mean);
                maxR = Mathf.Max(maxR, mean);
            }

            Assert.Greater(sum.Count, 1, "the cage should weld into several disjoint solids");
            Assert.Less(maxR - minR, 1e-4f * Mathf.Max(1e-4f, maxR),
                $"the shipped cage's {sum.Count} solids now span a real radius range " +
                $"({minR:F6}..{maxR:F6}). The hull morph passes ONE phase precisely because they " +
                "did not — re-read VESSEL_CRYSTAL_ABSORB.md and decide whether to hand the builder " +
                "CrystalMorphConfig's phaseNear/phaseFar now that a stagger would mean something.");
        }
    }
}
