using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's 2026-11-12 rework (R_VesselActions/RHINO_PLOUGH.md § "The 2026-11-12 rework"): the Gore's price is the
    /// fleet Strike's, the element scaling sits on the elements the fundamentals give it, and a Stampede is a window that
    /// opens and closes.
    /// </summary>
    public class RhinoReworkTests
    {
        [Test]
        public void TheGoreBaseTakeIsTheStrikePrice()
        {
            float perElement = CombatHitDrain.PointsFor(CombatHitClass.Strike) / CombatHitDrain.PointsPerLevel
                               * CombatHitDrain.NormalizedPerLevel;
            Assert.AreEqual(0.08f, perElement, 1e-5f);
        }

        [Test]
        public void TheBladeIsSpaceAndTheHeftIsMass()
        {
            var blade = ElementalFloat.Multiplier(14f, 40f, Element.Space, 10f);
            var heft = ElementalFloat.Multiplier(0f, 60f, Element.Mass, 0f);
            Assert.AreEqual(Element.Space, blade.ScaledElement);
            Assert.AreEqual(Element.Mass, heft.ScaledElement);
            Assert.AreEqual(40f, blade.EvaluateAtNormalizedLevel(1f), 1e-4f);
            Assert.AreEqual(60f, heft.EvaluateAtNormalizedLevel(1f), 1e-4f);
        }

        [Test]
        public void ANullRhinoNeverStampedes()
        {
            RhinoStampede.Begin(null, 8f);
            Assert.IsFalse(RhinoStampede.IsActive(null));
        }
    }
}
