#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Elemental hull-morph mapping tests — validates VesselElementalMorphConfigSO's pure helpers:
    /// the level→weight band clamp (models express only the [0,10] progression band; the deficit
    /// band [-5,0) holds the level-0 silhouette and the overcharge band (10,15] holds the level-10
    /// extreme) and the by-name blend-shape labeling contract with art (case-insensitive element
    /// names, FBX deformer-prefix tails, ambiguity rejection).
    ///
    /// WHY THIS MATTERS:
    /// The vessel model is an element display. If the clamp drifts, a debuffed ship would morph
    /// "below zero" or an overcharged one past its authored extreme; if name resolution drifts,
    /// vessels silently stop morphing (or an art shape like a jaw gets driven by element levels).
    /// </summary>
    [TestFixture]
    public class VesselElementalMorphTests
    {
        const float Tolerance = 0.0001f;

        #region Level → weight band clamp

        [TestCase(-5, 0f)]
        [TestCase(-1, 0f)]
        [TestCase(0, 0f)]
        [TestCase(3, 0.3f)]
        [TestCase(5, 0.5f)]
        [TestCase(10, 1f)]
        [TestCase(12, 1f)]
        [TestCase(15, 1f)]
        public void NormalizedMorphWeight_ClampsToProgressionBand(int level, float expected)
        {
            Assert.AreEqual(expected, VesselElementalMorphConfigSO.NormalizedMorphWeight(level), Tolerance,
                "The model expresses only element levels 0-10; deficits hold the 0 silhouette and " +
                "overcharge holds the 10 extreme.");
        }

        #endregion

        #region Blend-shape labeling contract

        [TestCase("charge", Element.Charge)]
        [TestCase("Mass", Element.Mass)]
        [TestCase("SPACE", Element.Space)]
        [TestCase("time", Element.Time)]
        [TestCase("blendShape1.charge", Element.Charge)] // FBX deformer-prefixed channel
        [TestCase("Body.Time", Element.Time)]
        [TestCase("mass_hull", Element.Mass)]            // unique containment fallback
        public void TryResolveElement_ResolvesLabeledShapes(string shapeName, Element expected)
        {
            Assert.IsTrue(VesselElementalMorphConfigSO.TryResolveElement(shapeName, out var element),
                $"'{shapeName}' is labeled for {expected} and must resolve.");
            Assert.AreEqual(expected, element);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("blendShape1.serpent_tendril_r")] // non-element art shape stays untouched
        [TestCase("jaw_open")]
        [TestCase("spacetime_warp")]                // no stand-alone element token → none
        [TestCase("timeline_flap")]                 // element name as substring only → none
        [TestCase("massive_jaw")]
        [TestCase("spacer_fin")]
        [TestCase("charge_mass_test")]              // two stand-alone element tokens → ambiguous
        public void TryResolveElement_RejectsUnlabeledShapes(string shapeName)
        {
            Assert.IsFalse(VesselElementalMorphConfigSO.TryResolveElement(shapeName, out var element),
                $"'{shapeName}' is not an element label and must not be driven by element levels.");
            Assert.AreEqual(Element.None, element);
        }

        #endregion
    }
}
#endif
