using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's Space card places its super-shielded prism at the live blade's tip
    /// (<see cref="RhinoVesselHUDView.ReachPrismCentre"/>). Pinned against the icon kit's own layout
    /// (Tools/Build/icon_kit/specs/rhino.py prism_centre), so the card the HUD lays out is the card the
    /// sheet showed: a drift here parks the prism off the sword's tip.
    /// </summary>
    public class RhinoBladeReachLayoutTests
    {
        static readonly Vector2 Hilt = new(20f, 112f);
        const float Angle = 52f, Along = 13.12f, Side = -0.0913043f;

        [Test]
        public void AtRest_MatchesTheKit()
        {
            var c = RhinoVesselHUDView.ReachPrismCentre(Hilt, Angle, 40.8f, Along, Side);
            Assert.AreEqual(64.783f, c.x, 0.01f);
            Assert.AreEqual(81.739f, c.y, 0.01f);
        }

        [Test]
        public void AtFullReach_MatchesTheKit()
        {
            var c = RhinoVesselHUDView.ReachPrismCentre(Hilt, Angle, 68f, Along, Side);
            Assert.AreEqual(87.746f, c.x, 0.01f);
            Assert.AreEqual(66.950f, c.y, 0.01f);
        }
    }
}
