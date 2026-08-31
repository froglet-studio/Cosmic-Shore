using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab hull-geometry contract (SCARAB.md §3.0). The hull is generated, so the
    /// properties worth pinning are the ones a silent regression eats: that no profile ever
    /// produces a NaN (the 2026-08-15 incident class — float32 Sin(PI) is negative, and
    /// Pow(negative, fractional) poisons a whole mesh's bounds and freezes the puppetry), that
    /// no triangle is degenerate, that the fit pass really does land the CARAPACE on the
    /// authored extents (measuring the appendages once squashed the body to ~70%), that the
    /// domain/chassis submesh split matches the material contract (slot 1 = what the fleet
    /// paints), that the part list and pivots are what ScarabAnimation resolves by name, and
    /// that generation is deterministic. Assertions are RELATIONSHIPS against the settings —
    /// counts are derived from the same integers the generator consumes — so a legitimate
    /// retune moves the expectations with it (the absolute-number-test trap).
    /// </summary>
    public class ScarabHullFormTests
    {
        static List<ScarabHullForm.Part> Build() =>
            ScarabHullForm.Generate(ScarabHullForm.Settings.Default);

        static ScarabHullForm.Part PartNamed(List<ScarabHullForm.Part> parts, string name)
        {
            var part = parts.FirstOrDefault(p => p.Name == name);
            Assert.IsNotNull(part, $"part '{name}' missing");
            return part;
        }

        // Vertex-count formulas, derived from the same integers the generator consumes.
        static int ShellVerts(ScarabHullForm.Settings s) => (s.LengthSegments + 1) * (s.WidthSegments + 1);
        static int BellyVerts(ScarabHullForm.Settings s) => (s.LengthSegments + 1) * (s.WidthSegments * 2 + 1);
        static int PronotumVerts(ScarabHullForm.Settings s) =>
            (Mathf.Max(4, s.LengthSegments / 2) + 1) * (s.WidthSegments * 2 + 1);
        const int ClypeusVerts = 8;
        static int HornVerts(ScarabHullForm.Settings s) => 8 * s.HornSides + 1;
        const int LegVerts = 16; // two capped 8-vert segments

        [Test]
        public void PartRosterMatchesWhatScarabAnimationResolvesByName()
        {
            var parts = Build();
            var names = parts.Select(p => p.Name).ToList();
            var expected = new[]
            {
                "Core", "elytron.r", "elytron.l", "pronotum", "horn",
                "leg.l1", "leg.l2", "leg.l3", "leg.r1", "leg.r2", "leg.r3",
            };
            foreach (var name in expected)
                Assert.Contains(name, names, "the animation resolves this part by name");
            Assert.AreEqual(expected.Length, names.Count, "unexpected extra/missing parts");
        }

        [Test]
        public void VertexCountsFollowTheSettingsFormulas()
        {
            var s = ScarabHullForm.Settings.Default;
            var parts = Build();
            Assert.AreEqual(BellyVerts(s) + ClypeusVerts, PartNamed(parts, "Core").Verts.Count);
            Assert.AreEqual(ShellVerts(s), PartNamed(parts, "elytron.r").Verts.Count);
            Assert.AreEqual(ShellVerts(s), PartNamed(parts, "elytron.l").Verts.Count);
            Assert.AreEqual(PronotumVerts(s), PartNamed(parts, "pronotum").Verts.Count);
            Assert.AreEqual(HornVerts(s), PartNamed(parts, "horn").Verts.Count);
            foreach (var leg in parts.Where(p => p.Name.StartsWith("leg.")))
                Assert.AreEqual(LegVerts, leg.Verts.Count, leg.Name);
        }

        [Test]
        public void NoNaNAnywhereAcrossTheParameterDomain()
        {
            // Sweep the corners and centre of the authored [Range] domain for the fields that
            // feed Sin/Pow profiles — the NaN class lives there, and a clamp removed "because
            // nothing hits it" is exactly what this fails on.
            foreach (float hornCurve in new[] { 0f, 0.8f, 1.6f })
            foreach (float elytraFront in new[] { 0.4f, 0.63f, 0.85f })
            foreach (float pronotumFront in new[] { 0.6f, 0.9f, 0.98f })
            {
                var s = ScarabHullForm.Settings.Default;
                s.HornCurve = hornCurve;
                s.ElytraFront = elytraFront;
                s.PronotumFront = pronotumFront;
                foreach (var part in ScarabHullForm.Generate(s))
                {
                    foreach (var v in part.Verts)
                        Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z),
                            $"NaN vertex in {part.Name} at hornCurve {hornCurve}, " +
                            $"elytraFront {elytraFront}, pronotumFront {pronotumFront}");
                    foreach (var n in part.Normals)
                        Assert.IsFalse(float.IsNaN(n.x) || float.IsNaN(n.y) || float.IsNaN(n.z),
                            $"NaN normal in {part.Name}");
                }
            }
        }

        [Test]
        public void NoDegenerateTriangles()
        {
            foreach (var part in Build())
            {
                AssertTrianglesHaveArea(part, part.Chassis, "chassis");
                AssertTrianglesHaveArea(part, part.Shell, "shell");
            }

            static void AssertTrianglesHaveArea(ScarabHullForm.Part part, List<int> tris, string label)
            {
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = part.Verts[tris[i]], b = part.Verts[tris[i + 1]], c = part.Verts[tris[i + 2]];
                    float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                    Assert.Greater(area, 1e-6f, $"{part.Name} {label} tri {i / 3} is degenerate");
                }
            }
        }

        [Test]
        public void FitLandsTheCarapaceOnTheAuthoredExtentsAndCentresIt()
        {
            var s = ScarabHullForm.Settings.Default;
            var parts = Build();
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var part in parts.Where(p => p.IsCarapace))
            foreach (var v in part.Verts)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            Assert.AreEqual(s.Width, max.x - min.x, 1e-3f, "carapace width");
            Assert.AreEqual(s.Length, max.z - min.z, 1e-3f, "carapace length");
            Assert.AreEqual(0f, (max.x + min.x) * 0.5f, 1e-3f, "centred in x");
            Assert.AreEqual(0f, (max.z + min.z) * 0.5f, 1e-3f, "centred in z");
        }

        [Test]
        public void AppendagesNeverDriveTheFit()
        {
            // Doubling the leg reach must not change the carapace's fitted extents — the
            // 2026-08-15 squash bug was exactly the appendages driving the divisor.
            var wide = ScarabHullForm.Settings.Default;
            wide.LegLength = Mathf.Min(1.2f, wide.LegLength * 2f);
            Vector3 Extents(List<ScarabHullForm.Part> parts)
            {
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                foreach (var part in parts.Where(p => p.IsCarapace))
                foreach (var v in part.Verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
                return max - min;
            }
            var a = Extents(Build());
            var b = Extents(ScarabHullForm.Generate(wide));
            Assert.AreEqual(a.x, b.x, 1e-3f);
            Assert.AreEqual(a.z, b.z, 1e-3f);
        }

        [Test]
        public void SubmeshSplitMatchesTheMaterialContract()
        {
            // Slot 1 (Shell) is what ShipHelper paints with the domain colour: carapace,
            // pronotum and horn carry it; belly, clypeus and legs stay on the body material.
            var parts = Build();
            foreach (var part in parts)
            {
                bool domainPart = part.Name is "elytron.r" or "elytron.l" or "pronotum" or "horn";
                if (domainPart)
                    Assert.IsEmpty(part.Chassis, $"{part.Name} must be domain-only");
                else
                    Assert.IsEmpty(part.Shell, $"{part.Name} must be chassis-only");
            }
        }

        [Test]
        public void LegsPivotAtTheirSocketsAndElytraAtTheSeam()
        {
            var parts = Build();
            foreach (var leg in parts.Where(p => p.Name.StartsWith("leg.")))
            {
                // The socket is the femur's root ring: the pivot must sit within the femur's
                // own thickness of the nearest vertex, or the leg swings about empty space.
                float nearest = leg.Verts.Min(v => (v - leg.Pivot).magnitude);
                float femur = ScarabHullForm.Settings.Default.Width * 0.5f
                              * ScarabHullForm.Settings.Default.LegThickness;
                Assert.Less(nearest, femur * 2f, $"{leg.Name} pivot is not at its socket");
            }

            // Wing cases hinge about the centreline seam: pivot x must be 0.
            Assert.AreEqual(0f, PartNamed(parts, "elytron.r").Pivot.x, 1e-4f);
            Assert.AreEqual(0f, PartNamed(parts, "elytron.l").Pivot.x, 1e-4f);
        }

        [Test]
        public void NormalsAreUnitLengthAndBellyFacesDown()
        {
            var parts = Build();
            foreach (var part in parts)
            foreach (var n in part.Normals)
                Assert.AreEqual(1f, n.magnitude, 1e-3f, $"{part.Name} normal not unit");

            // Sanity on winding: the belly's interior rows must light from below. Sample the
            // core's belly grid away from the clypeus verts (the last 8).
            var core = PartNamed(parts, "Core");
            int bellyCount = core.Verts.Count - 8;
            int down = 0;
            for (int i = 0; i < bellyCount; i++)
                if (core.Normals[i].y < 0f) down++;
            Assert.Greater(down, bellyCount / 2, "belly normals should predominantly face down");
        }

        [Test]
        public void EveryPartWindsOutward()
        {
            // Signed mesh volume (Σ a·(b×c)/6) is positive iff the triangles wind outward under
            // Unity's front-face convention — calibrated against OctahedronMeshGenerator, whose
            // outward winding is proven on screen by every shield in the game. The hull shipped
            // with its shell, pronotum, belly and clypeus wound INWARD (invisible under the hull
            // materials' Cull Back), found by exactly this check when the geometry was first
            // compiled and run offline; this pins the fix. Open sheets (belly, elytra) still
            // yield a consistently-signed flux, so the per-part aggregate is a valid gate.
            foreach (var part in Build())
            {
                double v6 = 0;
                foreach (var tris in new[] { part.Chassis, part.Shell })
                    for (int i = 0; i < tris.Count; i += 3)
                    {
                        Vector3 a = part.Verts[tris[i]], b = part.Verts[tris[i + 1]], c = part.Verts[tris[i + 2]];
                        v6 += a.x * (b.y * c.z - b.z * c.y)
                            - a.y * (b.x * c.z - b.z * c.x)
                            + a.z * (b.x * c.y - b.y * c.x);
                    }
                Assert.Greater((float)(v6 / 6.0), 0f, $"{part.Name} winds inward (invisible under Cull Back)");
            }
        }

        [Test]
        public void GenerationIsDeterministic()
        {
            var a = Build();
            var b = Build();
            Assert.AreEqual(a.Count, b.Count);
            for (int p = 0; p < a.Count; p++)
            {
                Assert.AreEqual(a[p].Name, b[p].Name);
                CollectionAssert.AreEqual(a[p].Verts, b[p].Verts, a[p].Name);
                CollectionAssert.AreEqual(a[p].Chassis, b[p].Chassis, a[p].Name);
                CollectionAssert.AreEqual(a[p].Shell, b[p].Shell, a[p].Name);
            }
        }
    }
}
