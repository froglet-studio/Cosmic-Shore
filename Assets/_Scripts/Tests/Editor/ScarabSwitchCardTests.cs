using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab's Mass card holds the ring at one size and shows Mass by the ball shrinking inside it, to scale
    /// (<see cref="ScarabHUDView.SwitchBallScale"/>; the states sheet of Tools/Build/icon_kit/specs/scarab.py), and dims
    /// while no switch is banked (<see cref="ScarabHUDView.SwitchCardAlpha"/>). A slip here grows the ball with Mass -
    /// the opposite of what the level buys - or dims a card that can place a switch.
    /// </summary>
    public class ScarabSwitchCardTests
    {
        [Test]
        public void RestingRing_KeepsTheBallAtFullSize() => Assert.AreEqual(1f, ScarabHUDView.SwitchBallScale(1f), 1e-5f);

        [Test]
        public void MassTen_ShrinksTheBallByTheRingsGrowth() => Assert.AreEqual(0.4f, ScarabHUDView.SwitchBallScale(2.5f), 1e-5f);

        [Test]
        public void ADeficitBelowOne_NeverGrowsTheBall() => Assert.AreEqual(1f, ScarabHUDView.SwitchBallScale(0.6f), 1e-5f);

        [Test]
        public void ABankedSwitch_IsTheFullCard() => Assert.AreEqual(1f, ScarabHUDView.SwitchCardAlpha(1, 0.3f), 1e-5f);

        [Test]
        public void NoSwitchBanked_DimsTheCard() => Assert.AreEqual(0.3f, ScarabHUDView.SwitchCardAlpha(0, 0.3f), 1e-5f);
    }
}
