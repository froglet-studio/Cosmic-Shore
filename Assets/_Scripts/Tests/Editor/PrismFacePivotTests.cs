#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the mesh-centroid face pivot (Docs/PRISM_ANIMATION.md §4.8.2).
    ///
    /// `RotateFacesAlongAxis` spins each face of a dying prism about a pivot it DERIVES:
    /// the foot of the perpendicular from the object origin onto the face's plane, plus a
    /// fixed step along the face tangent. That step is a hardcoded measurement of the prism
    /// CUBE, whose every face is four isoceles wedges fanned from a face-centre vertex — and
    /// since §4.8.1 the shield tiers shatter through the same pipeline on meshes whose faces
    /// are nothing like that. The graphs now lerp the pivot onto the per-face CENTROID both
    /// shield generators already bake into TEXCOORD1, driven per debris entity by
    /// `_FacePivotFromCentroid` (0 = the cube's derived pivot, 1 = the mesh's centroid).
    ///
    /// Four things can silently break it, and each has a test here:
    ///
    ///   1. THE PER-INSTANCE DECLARATION. Without Hybrid Per Instance, DOTS cannot write the
    ///      weight at all: every shard would take the material's 0 and go back to spinning
    ///      about the cube's pivot, with no error anywhere.
    ///   2. THE SLOT-ID DERIVATION. A SubGraphNode's input slot ids are `Guid.GetHashCode()`
    ///      of the subgraph's property guids. The wirer computes them offline; this asserts
    ///      the runtime agrees, because if it ever did not, Unity would drop the two edges on
    ///      import and the fix would evaporate while the graph still looked wired in the file.
    ///   3. THE GEOMETRY THE FIX RESTS ON — that a shield face's plane-foot is NOT its centre
    ///      (and on the stellation is not even inside the triangle), while the baked centroid
    ///      always is.
    ///   4. THE TWO PRODUCERS disagreeing about which they are.
    ///
    /// Graph TOPOLOGY beyond connectivity is gated by
    /// `python3 Tools/Shaders/wire_prism_face_pivot.py --check`, and the shipped subgraph's
    /// arithmetic by `Tools/Shaders/verify_prism_face_pivot.py`. All assertions here run from
    /// assets alone — no play mode.
    /// </summary>
    public class PrismFacePivotTests
    {
        const string GraphPath = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph";
        const string SubGraphPath =
            "Assets/_Graphics/Materials/Graphs/PrismGraphs/Subgraphs/RotateFacesAlongAxis.shadersubgraph";
        const string ShieldShatterPath = "Assets/_Scripts/Utility/Effects/PrismShieldShatter.cs";
        const string PrismDebrisPath = "Assets/_Scripts/Utility/Effects/PrismDebris.cs";

        const string WeightProp = "_FacePivotFromCentroid";

        // Pinned in Tools/Shaders/wire_prism_face_pivot.py. These guids ARE the wiring:
        // every consumer's slot id is derived from them.
        const string FaceCentroidGuid = "f1c3b7a2-4e6d-4a91-9c05-2b7e8d3f1a44";
        const string PivotWeightGuid = "c0d5e8b1-73a4-4f2e-8b16-9d4a5c2e7f30";

        // ── 1. the per-instance declaration ──────────────────────────────────

        [Test]
        public void ExplodingBlockGraph_DeclaresTheWeightAsAPerInstanceStamp()
        {
            Assert.IsTrue(File.Exists(GraphPath), $"{GraphPath} is missing");
            string block = PropertyBlock(File.ReadAllText(GraphPath), WeightProp);

            Assert.IsNotNull(block,
                $"{WeightProp} is not declared on ExplodingBlockGraph. Re-run " +
                "python3 Tools/Shaders/wire_prism_face_pivot.py");
            Assert.IsTrue(block.Contains("\"m_GeneratePropertyBlock\": true"),
                $"{WeightProp} must be EXPOSED or the material has no such property to write.");
            Assert.IsTrue(block.Contains("\"hlslDeclarationOverride\": 3"),
                $"{WeightProp} must be Hybrid Per Instance. Shield shards and prism debris " +
                "share ExplodingBlockMaterial by design (§4.8.1), so a per-material value " +
                "cannot tell them apart — every shard would silently take the cube's pivot.");
            Assert.IsTrue(Regex.IsMatch(block, "\"m_Value\":\\s*0(\\.0+)?\\b"),
                $"{WeightProp} must default to 0 (the cube's derived pivot) so any producer " +
                "that never sets it renders exactly as it did before this change.");
        }

        // ── 2. the slot-id derivation ────────────────────────────────────────

        [Test]
        public void TheSubgraph_DeclaresBothPivotInputsWithTheirPinnedGuids()
        {
            string text = File.ReadAllText(SubGraphPath);
            foreach (var (name, guid) in new[] { ("FaceCentroid", FaceCentroidGuid),
                                                 ("CentroidPivotWeight", PivotWeightGuid) })
            {
                string block = PropertyBlock(text, null, name);
                Assert.IsNotNull(block, $"RotateFacesAlongAxis has no {name} input");
                Assert.IsTrue(block.Contains(guid),
                    $"{name}'s guid drifted from {guid}. That guid IS the wiring: every " +
                    "consuming SubGraphNode's slot id is Guid.GetHashCode() of it, so " +
                    "changing it silently disconnects the input on import.");
            }
        }

        [Test]
        public void TheRotateNodeSlotIds_MatchWhatUnityWillRecompute()
        {
            string text = File.ReadAllText(GraphPath);

            foreach (var (label, guid) in new[] { ("FaceCentroid", FaceCentroidGuid),
                                                  ("CentroidPivotWeight", PivotWeightGuid) })
            {
                // The authority: Unity builds a SubGraphNode's input slots with
                // Guid.GetHashCode(), so this is the id the editor will look for.
                int expected = new System.Guid(guid).GetHashCode();

                Assert.IsTrue(text.Contains($"\"{guid}\""),
                    $"the Rotate Faces Along Axis node does not list {label}'s guid in " +
                    "m_PropertyGuids — the input is not wired at all.");
                Assert.IsTrue(Regex.IsMatch(text, $"\"m_Id\":\\s*{expected}\\b"),
                    $"{label}'s slot (id {expected}, derived from its guid) is missing from " +
                    "the graph. If Unity's Guid.GetHashCode() ever differs from the wirer's " +
                    "offline derivation, this is where it surfaces.");
                Assert.AreEqual(1, Regex.Matches(text, $"\"m_SlotId\":\\s*{expected}\\b").Count,
                    $"{label} must have exactly one feeder edge. Zero means the input is " +
                    "unconnected and reads its default; more than one is an invalid graph.");
            }
        }

        // ── 3. the geometry the fix rests on ─────────────────────────────────

        [Test]
        public void EveryShieldFace_BakesACentroidStrictlyInsideItself()
        {
            foreach (var extents in new[] { new Vector3(0.5f, 0.5f, 0.5f),
                                            new Vector3(0.25f, 0.25f, 5f) })
            {
                AssertBakedCentroidsAreInside(
                    OctahedronMeshGenerator.Generate(extents),
                    OctahedronMeshGenerator.FaceCentroidUVChannel, $"octahedron {extents}");
                AssertBakedCentroidsAreInside(
                    StellatedOctahedronMeshGenerator.Generate(extents),
                    StellatedOctahedronMeshGenerator.FaceCentroidUVChannel, $"stella {extents}");
            }
        }

        /// <summary>
        /// The defect, stated as a measurement rather than an argument: the pivot
        /// RotateFacesAlongAxis derives lands OUTSIDE every stellation face. Its three
        /// lateral spike faces share one tetrahedron-face plane, so the perpendicular foot
        /// from the object origin is that big triangle's centre — the hole between them.
        /// </summary>
        [Test]
        public void TheDerivedPivot_FallsOutsideEveryStellationFace()
        {
            var mesh = StellatedOctahedronMeshGenerator.Generate(new Vector3(0.5f, 0.5f, 0.5f));
            try
            {
                var verts = mesh.vertices;
                var normals = mesh.normals;
                int outside = 0;
                for (int f = 0; f * 3 < verts.Length; f++)
                {
                    int i = f * 3;
                    Vector3 n = normals[i];
                    Vector3 foot = n * Vector3.Dot(verts[i], n);
                    var b = Barycentric(foot, verts[i], verts[i + 1], verts[i + 2]);
                    if (b.x < 0f || b.y < 0f || b.z < 0f) outside++;
                }
                Assert.AreEqual(verts.Length / 3, outside,
                    "Every stellation face's plane-foot should fall outside the face — that is " +
                    "why the shatter needed the baked centroid. If this ever fails, the mesh's " +
                    "face decomposition changed and §4.8.2's reasoning must be re-derived.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        /// <summary>
        /// The octahedron's plane-foot IS its centroid only while the mesh is regular. On an
        /// elongated prism — every trail slab in the game — it slides toward the short edge,
        /// so "just switch the tangent slide off" would not have been enough either.
        /// </summary>
        [Test]
        public void TheDerivedPivot_DriftsOffCentreOnAnElongatedOctahedron()
        {
            var mesh = OctahedronMeshGenerator.Generate(new Vector3(0.25f, 0.25f, 5f));
            try
            {
                var verts = mesh.vertices;
                var normals = mesh.normals;
                float worst = 0f;
                for (int f = 0; f * 3 < verts.Length; f++)
                {
                    int i = f * 3;
                    Vector3 n = normals[i];
                    Vector3 foot = n * Vector3.Dot(verts[i], n);
                    Vector3 centroid = (verts[i] + verts[i + 1] + verts[i + 2]) / 3f;
                    float edge = Mathf.Max(
                        Vector3.Distance(verts[i], verts[i + 1]),
                        Mathf.Max(Vector3.Distance(verts[i + 1], verts[i + 2]),
                                  Vector3.Distance(verts[i + 2], verts[i])));
                    worst = Mathf.Max(worst, Vector3.Distance(foot, centroid) / edge);
                }
                Assert.That(worst, Is.GreaterThan(0.1f),
                    "The derived pivot is expected to sit well off the face centre on an " +
                    "elongated prism; if it no longer does, this test is measuring the wrong thing.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        // ── 4. the two producers ─────────────────────────────────────────────

        [Test]
        public void TheTwoDebrisProducers_DeclareTheirOwnFaceLayout()
        {
            Assert.IsTrue(Regex.IsMatch(File.ReadAllText(ShieldShatterPath),
                    @"FacePivotFromCentroid\s*=\s*1f"),
                "PrismShieldShatter must spawn its shards with FacePivotFromCentroid = 1 — a " +
                "shield face is one triangle and carries its own baked centroid. Without it " +
                "the shards spin about the prism cube's pivot, which on the stellation is " +
                "outside the face entirely.");
            Assert.IsTrue(Regex.IsMatch(File.ReadAllText(PrismDebrisPath),
                    @"FacePivotFromCentroid\s*=\s*0f"),
                "PrismDebris must spawn a dying prism's pieces with FacePivotFromCentroid = 0 " +
                "— the cube's four-wedge faces are what the derived pivot was measured for, " +
                "and its look is approved. Saying so explicitly is what stops the default " +
                "from being changed underneath it.");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        static void AssertBakedCentroidsAreInside(Mesh mesh, int channel, string label)
        {
            try
            {
                var verts = mesh.vertices;
                var centroids = new System.Collections.Generic.List<Vector3>();
                mesh.GetUVs(channel, centroids);
                Assert.AreEqual(verts.Length, centroids.Count, $"{label}: UV{channel} is not per-vertex");

                for (int f = 0; f * 3 < verts.Length; f++)
                {
                    int i = f * 3;
                    var b = Barycentric(centroids[i], verts[i], verts[i + 1], verts[i + 2]);
                    Assert.That(Mathf.Min(b.x, Mathf.Min(b.y, b.z)), Is.GreaterThan(1e-4f),
                        $"{label} face {f}: the baked pivot {centroids[i]} is not strictly inside " +
                        "its own triangle, so the shatter would spin it about a point off the face.");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        /// <summary>Barycentric coordinates of a coplanar point, solved in the triangle's own
        /// edge basis. All three positive == strictly inside.</summary>
        static Vector3 Barycentric(Vector3 p, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 e1 = v1 - v0, e2 = v2 - v0, r = p - v0;
            float d11 = Vector3.Dot(e1, e1), d12 = Vector3.Dot(e1, e2), d22 = Vector3.Dot(e2, e2);
            float det = d11 * d22 - d12 * d12;
            float b = (d22 * Vector3.Dot(r, e1) - d12 * Vector3.Dot(r, e2)) / det;
            float c = (d11 * Vector3.Dot(r, e2) - d12 * Vector3.Dot(r, e1)) / det;
            return new Vector3(1f - b - c, b, c);
        }

        /// <summary>The serialized ShaderProperty document for a reference name (or display
        /// name). The files are CONCATENATED JSON documents separated by blank lines.</summary>
        static string PropertyBlock(string text, string referenceName, string displayName = null)
        {
            foreach (string block in Regex.Split(text.Replace("\r\n", "\n"), "\n\n+"))
            {
                if (!block.Contains("ShaderProperty")) continue;
                if (referenceName != null &&
                    block.Contains($"\"m_DefaultReferenceName\": \"{referenceName}\"")) return block;
                if (displayName != null &&
                    block.Contains($"\"m_Name\": \"{displayName}\"")) return block;
            }
            return null;
        }
    }
}
#endif
