using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Sparrow's Charge card is a bay of rockets that fill ONE AT A TIME
    /// (<see cref="SparrowHUDView.BayRocketFill"/>): every rocket held is full, the one after them
    /// fills with the charge, and the rest are empty. Pinned because an off-by-one draws a plausible
    /// wrong card - the charge filling a rocket the pilot already holds.
    /// </summary>
    public class SparrowBayRocketFillTests
    {
        [Test]
        public void HeldRockets_AreFull()
        {
            Assert.AreEqual(1f, SparrowHUDView.BayRocketFill(0, 2, 0.4f), 1e-5f);
            Assert.AreEqual(1f, SparrowHUDView.BayRocketFill(1, 2, 0.4f), 1e-5f);
        }

        [Test]
        public void TheNextRocket_FillsWithTheCharge() =>
            Assert.AreEqual(0.4f, SparrowHUDView.BayRocketFill(2, 2, 0.4f), 1e-5f);

        [Test]
        public void RocketsBeyondTheNext_AreEmpty() =>
            Assert.AreEqual(0f, SparrowHUDView.BayRocketFill(3, 2, 0.9f), 1e-5f);

        [Test]
        public void EmptyBay_ChargesTheFirst()
        {
            Assert.AreEqual(0.6f, SparrowHUDView.BayRocketFill(0, 0, 0.6f), 1e-5f);
            Assert.AreEqual(0f, SparrowHUDView.BayRocketFill(1, 0, 0.6f), 1e-5f);
        }

        [Test]
        public void FullBay_IsAllFull_AndTheChargeHasNowhereToGo()
        {
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(1f, SparrowHUDView.BayRocketFill(i, 4, 0.7f), 1e-5f);
        }

        [Test]
        public void Charge_IsClamped() =>
            Assert.AreEqual(1f, SparrowHUDView.BayRocketFill(0, 0, 3f), 1e-5f);
    }
}
