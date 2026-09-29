using CosmicShore.Data;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Squirrel HUD's palette retry latch. One line of logic, tested because it is the one
    /// defect class in this feature that NO offline gate can see: swapping the predicate for
    /// <c>true</c> is a logic regression, so the project's Roslyn harness and all eight textual
    /// gates compile and pass it clean. It shipped once, and it presented as a JADE bug.
    /// </summary>
    public class SquirrelHudPaletteLatchTests
    {
        static Color Resolved(float a) => new Color(0.18f, 0.49f, 1f, a);

        [Test]
        public void ALandedPushClosesTheLatch()
        {
            foreach (var d in new[] { Domains.Jade, Domains.Ruby, Domains.Gold })
                Assert.IsTrue(SquirrelVesselHUDController.PaletteLanded(d, Resolved(1f)),
                    $"{d} resolved a real colour, so the retry must stop.");
        }

        [Test]
        public void AnUnresolvedPaletteKeepsRetrying()
        {
            // alpha 0 = "the palette had no answer" (ThemeManagerData not available yet). The view
            // keeps its white; the latch must NOT close, or the card stays white until the domain
            // changes - which for Jade, NetDomain's own initialiser, may be never.
            Assert.IsFalse(SquirrelVesselHUDController.PaletteLanded(Domains.Jade, Resolved(0f)));
        }

        [Test]
        public void TheNoTeamSentinelNeverClosesTheLatch()
        {
            // Blue's shielded tint is refused permanently by design, so a latch closed on it would
            // freeze the card for the rest of the match.
            Assert.IsFalse(SquirrelVesselHUDController.PaletteLanded(Domains.Blue, Resolved(1f)));
            Assert.IsFalse(SquirrelVesselHUDController.PaletteLanded(Domains.Blue, Resolved(0f)));
        }

        [Test]
        public void JadeIsTheDomainAWrongLatchSinglesOut()
        {
            // The regression under test is `_domainPainted = true` regardless of the answer. With
            // it, the retry is gated on the domain CHANGING. Jade is the value NetDomain starts at,
            // so a Jade pilot's first (failed) push is also their last; Ruby and Gold arrive as a
            // change and repaint. This asserts the asymmetry that made it read as a Jade bug.
            Assert.IsFalse(SquirrelVesselHUDController.PaletteLanded(Domains.Jade, Resolved(0f)),
                "A failed push on the default domain must stay retryable - this is the whole bug.");
            Assert.IsTrue(SquirrelVesselHUDController.PaletteLanded(Domains.Ruby, Resolved(1f)));
        }
    }
}
