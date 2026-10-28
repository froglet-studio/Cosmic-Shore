using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's Time card shows the danger prism ahead of the nose until Momentum holds, then the ward up and
    /// the prism glanced away (<see cref="RhinoVesselHUDView.MomentumPartsFor"/>, the states sheet of
    /// Tools/Build/icon_kit/specs/rhino.py). A slip here draws the ward with the prism still in its path, or no
    /// threat at all - the two pictures the card exists to tell apart.
    /// </summary>
    public class RhinoMomentumCardTests
    {
        [Test]
        public void Unwarded_ShowsOnlyThePrismAhead()
        {
            var p = RhinoVesselHUDView.MomentumPartsFor(false);
            Assert.IsTrue(p.Prism);
            Assert.IsFalse(p.Glance);
            Assert.IsFalse(p.Ward);
        }

        [Test]
        public void Warded_ShowsTheWardAndTheGlancedPrism()
        {
            var p = RhinoVesselHUDView.MomentumPartsFor(true);
            Assert.IsFalse(p.Prism);
            Assert.IsTrue(p.Glance);
            Assert.IsTrue(p.Ward);
        }
    }
}
