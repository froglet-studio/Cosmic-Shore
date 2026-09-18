using CosmicShore.Data;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pins THE FOUR ELEMENTAL IDENTITIES OF A PLANT (Docs/ECOSYSTEM.md §45) - the sibling of
    /// <see cref="FloraReproductionRulesTests"/>, and the in-editor half of the gate
    /// <c>Tools/Build/measure_flora_elemental_form.py</c> runs offline.
    ///
    /// <para>The property these tests exist to defend is that the law is a
    /// <b>REDISTRIBUTION</b> of one species' form and never an inflation of it: the four
    /// volume multipliers average to exactly 1 and the aspect term cannot move volume, so a
    /// mixed-element forest holds the mass it held before the law existed and <b>no cell's
    /// volume phase ladder moves</b>. Break either and every cell that grows flora needs
    /// re-authoring (Docs/ECOSYSTEM.md §4.6).</para>
    /// </summary>
    public class FloraElementalFormTests
    {
        static readonly Element[] Four =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        // Real shipped leaves, plus the Flora default. A law about aspect has to be pinned on
        // leaves with different aspects, or the anisotropy term is never exercised.
        static readonly Vector3[] Leaves =
        {
            new(9f, 3.4f, 1.5f),      // the gyroid's authored leaf
            new(3.51f, 3.51f, 2f),    // a Hesperides phyllotactic cross-section
            new(4f, 4f, 1f),          // Flora's own default
            new(0.85f, 0.85f, 0.85f), // a cube - no aspect to exaggerate
        };

        static float Volume(Vector3 v) => v.x * v.y * v.z;
        static float Aspect(Vector3 v) =>
            Mathf.Max(v.x, Mathf.Max(v.y, v.z)) / Mathf.Min(v.x, Mathf.Min(v.y, v.z));

        // ── the neutrality property ─────────────────────────────────────────────────────

        [Test]
        public void FourLeafVolumeMultipliers_AverageToExactlyOne()
        {
            float sum = 0f;
            foreach (var e in Four) sum += FloraElementalForm.LeafVolumeScale(e);
            Assert.That(sum / 4f, Is.EqualTo(1f).Within(2e-4f),
                "The four leaf volume multipliers must average to 1 - a mixed-element forest " +
                "holds the same mass as before the law, so no cell's volume ladder moves.");
        }

        [Test]
        public void Anisotropy_IsVolumeExact([ValueSource(nameof(Leaves))] Vector3 leaf)
        {
            foreach (var e in Four)
            {
                float ratio = Volume(FloraElementalForm.ShapeLeaf(leaf, e)) / Volume(leaf);
                Assert.That(ratio, Is.EqualTo(FloraElementalForm.LeafVolumeScale(e))
                        .Within(1e-4f * FloraElementalForm.LeafVolumeScale(e)),
                    $"{e}: the aspect term must not move volume, or aspect and volume stop " +
                    "being independent dials.");
            }
        }

        // ── the four identities ─────────────────────────────────────────────────────────

        [Test]
        public void Mass_IsTheHeaviestLeaf_AndSpaceTheLightest()
        {
            foreach (var e in Four)
            {
                if (e == Element.Mass) continue;
                Assert.That(FloraElementalForm.LeafVolumeScale(Element.Mass),
                    Is.GreaterThan(FloraElementalForm.LeafVolumeScale(e)),
                    "MASS is the most cumulative prism volume.");
                if (e == Element.Space) continue;
                Assert.That(FloraElementalForm.LeafVolumeScale(Element.Space),
                    Is.LessThan(FloraElementalForm.LeafVolumeScale(e)),
                    "SPACE trades cumulative prism volume for the assembly's bounding volume.");
            }
        }

        [Test]
        public void Mass_PullsTheAxesTogether_AndSpaceDrivesThemApart(
            [ValueSource(nameof(Leaves))] Vector3 leaf)
        {
            float authored = Aspect(leaf);
            float mass = Aspect(FloraElementalForm.ShapeLeaf(leaf, Element.Mass));
            float space = Aspect(FloraElementalForm.ShapeLeaf(leaf, Element.Space));

            if (Mathf.Approximately(authored, 1f))
            {
                // A cube has no long axis to exaggerate - the exponent is a no-op on it, and
                // the law must not invent an aspect a species never authored.
                Assert.That(mass, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(space, Is.EqualTo(1f).Within(1e-4f));
                return;
            }

            Assert.That(mass, Is.LessThan(authored), "MASS: x, y and z sit closest together.");
            Assert.That(space, Is.GreaterThan(authored), "SPACE: the highest aspect ratio.");
        }

        [Test]
        public void ChargeAndTime_KeepTheSpeciesOwnShape(
            [ValueSource(nameof(Leaves))] Vector3 leaf)
        {
            // Charge's identity is armour and Time's is tempo, so neither restates the
            // species' form - they take it, scaled only by the neutrality normaliser.
            foreach (var e in new[] { Element.Charge, Element.Time })
                Assert.That(Aspect(FloraElementalForm.ShapeLeaf(leaf, e)),
                    Is.EqualTo(Aspect(leaf)).Within(1e-4f * Aspect(leaf)), $"{e}");
        }

        [Test]
        public void Time_HasTheFastestClock_OnOneConstant()
        {
            // "Time grows and regenerates the fastest" is ONE number, not three that can
            // drift: the grow period, the growth quota and the lattice colony cycle are all
            // costs-per-thing and all divide by the same rate.
            float time = FloraElementalForm.ScaleGrowPeriod(10f, Element.Time);
            foreach (var e in new[] { Element.Charge, Element.Mass, Element.Space })
                Assert.That(FloraElementalForm.ScaleGrowPeriod(10f, e), Is.GreaterThan(time),
                    $"{e} must lay a prism more slowly than Time.");

            Assert.That(time, Is.EqualTo(
                10f / FloraReproductionRules.ReproductionRateFor(Element.Time)).Within(1e-4f),
                "The grow clock must ride the reproduction rate, not a second constant.");
        }

        // ── reach: the assembly half, which introduces no constant of its own ───────────

        [Test]
        public void Reach_IsTheInverseCubeRootOfVolume_SoTheTwoDialsCannotDrift()
        {
            foreach (var e in Four)
            {
                float v = FloraElementalForm.LeafVolumeScale(e);
                Assert.That(FloraElementalForm.ReachScale(e),
                    Is.EqualTo(Mathf.Pow(v, -1f / 3f)).Within(1e-5f), $"{e}");
            }
            Assert.That(FloraElementalForm.ReachScale(Element.Space),
                Is.GreaterThan(FloraElementalForm.ReachScale(Element.Mass)),
                "SPACE reaches further than MASS - that is what its lost volume buys.");
        }

        // ── the sentinels and the elements that are not one of the four ────────────────

        [Test]
        public void ASentinelLeafIsReturnedUnchanged()
        {
            // Vector3.zero means "keep the prefab's size" - a sentinel, not a shape, and the
            // size/shape decomposition is undefined on it (Docs/ECOSYSTEM.md §44, the skill's
            // sentinel rule).
            foreach (var e in Four)
            {
                Assert.That(FloraElementalForm.ShapeLeaf(Vector3.zero, e), Is.EqualTo(Vector3.zero));
                Assert.That(FloraElementalForm.ShapeLeaf(new Vector3(2f, -1f, 3f), e),
                    Is.EqualTo(new Vector3(2f, -1f, 3f)));
            }
        }

        [Test]
        public void AnUnresolvedElementChangesNothing()
        {
            // Element.None means "no crystal resolved yet"; Omni is not one of the four. A
            // plant that reaches growth without an element must behave exactly as it did.
            foreach (var e in new[] { Element.None, Element.Omni })
            {
                Assert.That(FloraElementalForm.LeafVolumeScale(e), Is.EqualTo(1f));
                Assert.That(FloraElementalForm.LeafAnisotropy(e), Is.EqualTo(1f));
                Assert.That(FloraElementalForm.ReachScale(e), Is.EqualTo(1f));
                Assert.That(FloraElementalForm.ScaleGrowPeriod(10f, e), Is.EqualTo(10f));
                Assert.That(FloraElementalForm.ShapeLeaf(new Vector3(9f, 3.4f, 1.5f), e),
                    Is.EqualTo(new Vector3(9f, 3.4f, 1.5f)));
            }
        }
    }
}
