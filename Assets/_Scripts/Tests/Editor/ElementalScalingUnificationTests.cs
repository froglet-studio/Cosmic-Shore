using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Edit-mode coverage for the element-scaling unification: every quantitative element→parameter
    /// multiplier in the fleet is ONE <see cref="ElementalFloat"/> living on the asset or component
    /// that owns the number, and the retired <c>ElementalAbilityMapSO.MultiplierAtFullLevel</c> +
    /// <c>handler.Multiplier(element)</c> channel is gone.
    ///
    /// <para>What this file is FOR: the ten migrated multipliers were proved bit-identical to the
    /// retired <see cref="ElementalScaling.Multiplier"/> at migration time, over 201 samples each
    /// across the whole [-5, 15] band, by compiling the shipped ElementalFloat.cs against a
    /// transcription of the old path and running it. That proof is not reproducible here (the old
    /// path no longer exists to compare against), so what is locked instead is each migration's
    /// three load-bearing values — at rest, at level 10, and floored in the deficit band. Those are
    /// what an asset edit or a careless retune can silently move.</para>
    ///
    /// <para>Lives under an Editor/ folder per CLAUDE.md — a test anywhere else compiles into the
    /// player and breaks the Windows build at the IL2CPP linker.</para>
    /// </summary>
    public class ElementalScalingUnificationTests
    {
        // The ten migrated multipliers, with the endpoints read off the map assets before the
        // generic channel was removed. Min is 1 in every row and that is not a coincidence:
        // ElementalScaling.Multiplier anchored its lerp at 1, so an element could only ever ADD to
        // a vessel's authored baseline. MinMultiplier was a FLOOR, never the value at rest —
        // reading it as the rest value is the one transcription error that would look plausible.
        static readonly (string Label, Element Element, float AtFull, float Floor)[] Migrated =
        {
            ("Squirrel CHARGE  skim energy per hit",   Element.Charge, 2.0f,  0.25f),
            ("Urchin   CHARGE  spike reach",           Element.Charge, 2.5f,  0.4f),
            ("Rhino    MASS    trail slab ceiling",    Element.Mass,   1.5f,  0.25f),
            ("Sparrow  MASS    turret prism stretch",  Element.Mass,   2.5f,  0.4f),
            ("Sparrow  SPACE   muzzle speed",          Element.Space,  9.0f,  0.4f),
            ("Scarab   SPACE   forged ball size",      Element.Space,  4.0f,  0.5f),
            ("Manta    TIME    boost speed (Soar)",    Element.Time,   1.3f,  0.7f),
            ("Sparrow  TIME    boost speed",           Element.Time,   1.5f,  0.5f),
            ("Rhino    TIME    ramp wind-up rate",     Element.Time,   2.5f,  0.5f),
            ("Serpent  TIME    boost duration",        Element.Time,   1.6f,  0.25f),
        };

        const float RestingLevel = 0f;     // normalized: integer level 0
        const float FullLevel = 1f;        // normalized: integer level 10
        const float DeficitLevel = -0.5f;  // normalized: integer level -5, the band floor

        [Test]
        public void EveryMigratedMultiplierIsExactlyOneAtRest()
        {
            // The anchor that makes an element additive rather than a replacement: at the resting
            // level the vessel flies on exactly its authored baseline, on every hull.
            foreach (var m in Migrated)
            {
                var ef = ElementalFloat.Multiplier(1f, m.AtFull, m.Element, m.Floor);
                Assert.AreEqual(1f, ef.EvaluateAtNormalizedLevel(RestingLevel), 1e-6f, m.Label);
            }
        }

        [Test]
        public void EveryMigratedMultiplierHitsItsAuthoredFullLevelValue()
        {
            foreach (var m in Migrated)
            {
                var ef = ElementalFloat.Multiplier(1f, m.AtFull, m.Element, m.Floor);
                Assert.AreEqual(m.AtFull, ef.EvaluateAtNormalizedLevel(FullLevel), 1e-6f, m.Label);
            }
        }

        [Test]
        public void TheFloorIsLoadBearingNotDecoration()
        {
            // Without a floor the deficit band extrapolates through zero and INVERTS the parameter
            // instead of weakening it: the Sparrow's SPACE muzzle-speed factor reaches
            // 1 + (9-1)(-0.5) = -3. Every migrated multiplier must clamp there.
            foreach (var m in Migrated)
            {
                var ef = ElementalFloat.Multiplier(1f, m.AtFull, m.Element, m.Floor);
                float atDeficit = ef.EvaluateAtNormalizedLevel(DeficitLevel);
                Assert.GreaterOrEqual(atDeficit, m.Floor - 1e-6f, m.Label + " fell below its floor");
                Assert.Greater(atDeficit, 0f, m.Label + " inverted in the deficit band");
            }
        }

        [Test]
        public void SparrowSpaceWouldInvertWithoutItsFloor()
        {
            // The negative control for the test above: prove the floor is actually doing work on
            // the widest-range row rather than being satisfied vacuously.
            var unfloored = new ElementalFloat(1f);   // UseFloor defaults false
            var floored = ElementalFloat.Multiplier(1f, 9f, Element.Space, 0.4f);

            Assert.AreEqual(1f, unfloored.EvaluateAtNormalizedLevel(DeficitLevel), 1e-6f,
                "a DISABLED ElementalFloat must return its authored Value, not a lerp");
            Assert.AreEqual(0.4f, floored.EvaluateAtNormalizedLevel(DeficitLevel), 1e-6f,
                "the floor did not clamp the Sparrow's SPACE factor");
        }

        [Test]
        public void ADisabledElementalFloatIsExactlyOneEverywhere()
        {
            // Six of the eight hulls leave VesselTransformer.BoostSpeedMultiplier disabled, so
            // CurrentBoostAmount must be arithmetically unchanged for them. This is the invariant
            // that makes the fleet-wide read's removal a no-op rather than a retune.
            var off = new ElementalFloat(1f);
            for (int i = -5; i <= 15; i++)
                Assert.AreEqual(1f, off.EvaluateAtNormalizedLevel(i / 10f), 1e-6f,
                    $"disabled ElementalFloat moved at level {i}");
        }

        [Test]
        public void TheRetiredGenericChannelIsGone()
        {
            // A structural assert, not a behavioural one: if either surface comes back, scaling can
            // be addressed by ELEMENT again and the four defensive 1.0 pins become necessary again.
            Assert.IsNull(typeof(R_VesselElementalAbilityHandler).GetMethod("Multiplier"),
                "R_VesselElementalAbilityHandler.Multiplier(Element) is back — element-addressed "
                + "scaling reaches every reader of that element and cannot name its parameter.");

            var entry = typeof(ElementalAbilityEntry);
            Assert.IsNull(entry.GetField("MultiplierAtFullLevel"),
                "ElementalAbilityEntry.MultiplierAtFullLevel is back; scaling belongs in an "
                + "ElementalFloat on whatever owns the number.");
            Assert.IsNull(entry.GetField("MinMultiplier"),
                "ElementalAbilityEntry.MinMultiplier is back; a multiplier's floor belongs with it.");
        }

        [Test]
        public void TheMapStillOwnsTheQualitativeHalf()
        {
            // The unification removed the NUMBER from the map and deliberately kept the BOOLEAN:
            // "is this element's upgrade on" is genuinely a fact about the element, so it stays
            // element-addressed. If this ever fails, the map has lost the half it should own.
            var entry = typeof(ElementalAbilityEntry);
            Assert.IsNotNull(entry.GetField("UnlockLevel"));
            Assert.IsNotNull(entry.GetField("RelockBelowLevel"));
            Assert.IsNotNull(entry.GetField("LatchPolicy"));
            Assert.IsNotNull(typeof(R_VesselElementalAbilityHandler).GetMethod("IsUpgradeActive"));
        }
    }
}
