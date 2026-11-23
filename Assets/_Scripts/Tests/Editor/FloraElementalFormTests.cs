using CosmicShore.Data;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pins THE FOUR ELEMENTAL IDENTITIES OF A PLANT (Docs/ECOSYSTEM.md §51) - the sibling of
    /// <see cref="FloraReproductionRulesTests"/>, and the in-editor half of the gate
    /// <c>Tools/Build/measure_flora_elemental_form.py</c> runs offline.
    ///
    /// <para>The law is a REDISTRIBUTION of one species' form for Charge, Time and Space (their volumes are §51's,
    /// and the aspect term cannot move volume). MASS left that average in Docs/ECOSYSTEM.md §65: its volume is its
    /// SHIELD's, its box 0.26x the neutral one, so a cell's prism-volume ladder reads less of a Mass plant than it
    /// did - stated, not hidden.</para>
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
        public void SpaceAndTheNeutrals_KeepTheVolumesTheyShippedWith()
        {
            // Docs/ECOSYSTEM.md §65 took MASS out of the average (its volume is its shield's); the other three are
            // exactly where §51 put them, so no Charge, Time or Space plant changed size.
            Assert.That(FloraElementalForm.NeutralLeafVolume, Is.EqualTo(0.8778f));
            Assert.That(FloraElementalForm.SpaceLeafVolume, Is.EqualTo(0.4097f));
        }

        [Test]
        public void MassVolumeIsItsShield_AndSpaceSeesTheLeast()
        {
            // A shield is the octahedron circumscribing the box: (4/3) x (1.5)^3 = 4.5x its volume.
            float shielded = FloraElementalForm.MassLeafVolume * 4.5f;
            Assert.That(shielded, Is.GreaterThan(FloraElementalForm.NeutralLeafVolume),
                "the shielding increases the volume of Mass - armoured it shows more than the neutral leaf");
            Assert.That(FloraElementalForm.SpaceLeafVolume, Is.LessThan(shielded * 0.5f),
                "Space carries significantly less volume than a Mass plant shows");
        }

        [Test]
        public void SpaceAlwaysOutSpansMass()
        {
            Assert.That(FloraElementalForm.ReachScale(Element.Space), Is.GreaterThan(FloraElementalForm.ReachScale(Element.Mass)));
            // the fit that sets the Mass plate: its shields clear on a flush tiling spread under Space's narrowest span (2x)
            Assert.That(3f * FloraElementalForm.MassAxisScale * 1.03f, Is.LessThan(2f));
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
        public void Mass_IsTheHeaviestLeafShielded_AndSpaceTheLightest()
        {
            foreach (var e in Four)
            {
                if (e == Element.Mass) continue;
                // §65: its volume is its SHIELD's - the octahedron is 4.5x the box
                Assert.That(4.5f * FloraElementalForm.LeafVolumeScale(Element.Mass),
                    Is.GreaterThan(FloraElementalForm.LeafVolumeScale(e)),
                    "MASS, shielded, is the most cumulative volume.");
                if (e == Element.Space) continue;
                Assert.That(FloraElementalForm.LeafVolumeScale(Element.Space),
                    Is.LessThan(FloraElementalForm.LeafVolumeScale(e)),
                    "SPACE trades cumulative prism volume for the assembly's bounding volume.");
            }
        }

        [Test]
        public void SpaceDrivesTheAxesApart(
            [ValueSource(nameof(Leaves))] Vector3 leaf)
        {
            float authored = Aspect(leaf);
            float space = Aspect(FloraElementalForm.ShapeLeaf(leaf, Element.Space));

            if (Mathf.Approximately(authored, 1f))
            {
                // A cube has no long axis to exaggerate - the exponent is a no-op on it, and
                // the law must not invent an aspect a species never authored.
                Assert.That(space, Is.EqualTo(1f).Within(1e-4f));
                return;
            }

            Assert.That(space, Is.GreaterThan(authored), "SPACE: the highest aspect ratio.");
        }

        [Test]
        public void ChargeMassAndTime_KeepTheSpeciesOwnShape(
            [ValueSource(nameof(Leaves))] Vector3 leaf)
        {
            // Charge's identity is danger, Time's is tempo and Mass's is armour and size (Docs/ECOSYSTEM.md §65: the
            // "chunky" Mass plate is retired), so none of the three restates the species' form - they take it, scaled
            // only by their volume.
            foreach (var e in new[] { Element.Charge, Element.Mass, Element.Time })
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
        public void Reach_SpaceAndTheNeutralsSpendAFixedMaterial()
        {
            foreach (var e in new[] { Element.Charge, Element.Space, Element.Time })
            {
                float v = FloraElementalForm.LeafVolumeScale(e);
                Assert.That(FloraElementalForm.ReachScale(e),
                    Is.EqualTo(Mathf.Pow(v, -1f / 3f)).Within(1e-5f), $"{e}");
            }
            Assert.That(FloraElementalForm.ReachScale(Element.Space),
                Is.GreaterThan(FloraElementalForm.ReachScale(Element.Time)),
                "SPACE reaches further than the neutral plant - that is what its lost volume buys.");
        }

        [Test]
        public void Reach_MassIsTheNeutralPlantScaled()
        {
            // Docs/ECOSYSTEM.md §65: every position AND every prism of the Time plant x the same axis scale, so the
            // Mass plant is a similarity of the Time plant - never the old compact block.
            float axis = FloraElementalForm.MassAxisScale;
            Assert.That(axis, Is.EqualTo(Mathf.Pow(FloraElementalForm.MassLeafVolume / FloraElementalForm.NeutralLeafVolume, 1f / 3f)).Within(1e-6f));
            Assert.That(FloraElementalForm.ReachScale(Element.Mass) / FloraElementalForm.ReachScale(Element.Time),
                Is.EqualTo(axis).Within(1e-5f), "the Mass plant reaches the Time plant's extent x the axis scale");
            var leaf = new Vector3(9f, 3.4f, 1.5f);
            Vector3 mass = FloraElementalForm.ShapeLeaf(leaf, Element.Mass), time = FloraElementalForm.ShapeLeaf(leaf, Element.Time);
            for (int i = 0; i < 3; i++)
                Assert.That(mass[i] / time[i], Is.EqualTo(axis).Within(1e-4f), $"axis {i}: the leaf grows by the same scale");
            Assert.That(FloraElementalForm.ReachScale(Element.Mass), Is.LessThan(FloraElementalForm.ReachScale(Element.Time)),
                "Mass is fitted UNDER the neutral plant - its shields carry its size");
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
