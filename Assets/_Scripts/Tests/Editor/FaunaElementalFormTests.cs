using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pins the SPACE SPINDLE (Docs/ECOSYSTEM.md §64.9): a Space body is scaled whole to 1.647x the Mass body and its
    /// thickest sections eroded by the lattice's Space exponent, so its thickest section comes back to the Mass body's
    /// girth while its thin parts keep their whole size (<see cref="FaunaElementalForm.ErodedGirth"/>,
    /// <see cref="SpindleErosion"/>).
    /// </summary>
    public class FaunaElementalFormTests
    {
        [Test]
        public void TheExponent_IsTheLatticeSpaceAnisotropyInverted()
        {
            Assert.That(FaunaElementalForm.SpindleGirthExponent,
                Is.EqualTo(1f / FloraElementalForm.SpaceLeafAnisotropy).Within(1e-6f));
            Assert.That(FaunaElementalForm.ThickestGirth * FaunaElementalForm.SpaceLengthOverMass, Is.EqualTo(1f).Within(1e-5f),
                "the thickest section of a body scaled 1.647x is the Mass body's girth");
        }

        [Test]
        public void TheThickestSection_ComesBackToTheMassGirth([Values(0.5f, 4.48f, 40f)] float thickest)
        {
            float reference = FaunaElementalForm.ErosionReference(thickest);
            Assert.That(FaunaElementalForm.ErodedGirth(thickest, reference),
                Is.EqualTo(FaunaElementalForm.ThickestGirth).Within(1e-4f));
        }

        [Test]
        public void ThinSections_AreLeftWhole_AndThickerSectionsLoseMore()
        {
            float reference = FaunaElementalForm.ErosionReference(4f);
            Assert.That(FaunaElementalForm.ErodedGirth(reference * 0.5f, reference), Is.EqualTo(1f));
            Assert.That(FaunaElementalForm.ErodedGirth(reference, reference), Is.EqualTo(1f));
            float previous = 1f;
            for (float w = reference * 1.1f; w <= 4f; w += 0.25f)
            {
                float g = FaunaElementalForm.ErodedGirth(w, reference);
                Assert.That(g, Is.LessThan(previous), $"width {w}");
                previous = g;
            }
            // the eroded WIDTH still grows with the authored width: a thick section never ends thinner than a thin one
            Assert.That(3f * FaunaElementalForm.ErodedGirth(3f, reference), Is.GreaterThan(2f * FaunaElementalForm.ErodedGirth(2f, reference)));
        }

        /// <summary>A body along +z: a thick barrel (half-width 4) from z 0 to 30, a thin tail (half-width 0.5) beyond.</summary>
        static List<Vector3> Body()
        {
            var v = new List<Vector3>();
            for (int k = 0; k <= 60; k++)
            {
                float z = k * 1f, r = z <= 30f ? 4f : 0.5f, h = r * 1.5f;   // taller than wide, like a shark
                for (int a = 0; a < 16; a++)
                {
                    float t = a * Mathf.PI * 2f / 16f;
                    v.Add(new Vector3(r * Mathf.Cos(t), h * Mathf.Sin(t), z));
                }
            }
            return v;
        }

        [Test]
        public void TheBarrel_IsErodedToTheMassGirth_TheTailIsKept_AndTheSpineIsUntouched()
        {
            var body = Body();
            var erosion = SpindleErosion.Measure(body, Vector3.zero);
            Assert.That(erosion.Thickest, Is.EqualTo(FaunaElementalForm.ThickestGirth).Within(0.02f));

            var barrel = new Vector3(4f, 0f, 15f);
            var mapped = erosion.Map(barrel);
            Assert.That(mapped.z, Is.EqualTo(15f).Within(1e-4f), "a point keeps its place along the spine");
            Assert.That(mapped.x, Is.EqualTo(4f * FaunaElementalForm.ThickestGirth).Within(0.1f));

            var tail = new Vector3(0.5f, 0.75f, 55f);
            Assert.That((erosion.Map(tail) - tail).magnitude, Is.LessThan(1e-4f), "a thin tail keeps its whole size");

            var spine = new Vector3(0f, 0f, 10f);
            Assert.That((erosion.Map(spine) - spine).magnitude, Is.LessThan(1e-4f));
        }

        [Test]
        public void ADirectionAlongTheSpine_KeepsItsLength_AcrossItIsScaledByTheGirth()
        {
            var erosion = SpindleErosion.Measure(Body(), Vector3.zero);
            var at = new Vector3(0f, 0f, 15f);
            Assert.That(erosion.MapDirection(at, Vector3.forward).magnitude, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(erosion.MapDirection(at, Vector3.right).magnitude, Is.EqualTo(erosion.GirthAt(at)).Within(1e-4f));
        }
    }
}
