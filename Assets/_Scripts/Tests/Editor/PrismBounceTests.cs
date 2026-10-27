using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Prism movement's shield rule (Garrett, 2026-10-27; Docs/PRISM_MOTION.md § "Shielded prisms bounce"): a collision a
    /// shielded prism is part of bounces and destroys neither; super-shielded mass only returns a shielded mover. These pin
    /// <see cref="PrismBounce"/>, the arithmetic the gravity field, the Rhino plough and the Jacks net share.
    /// </summary>
    public class PrismBounceTests
    {
        const float Eps = 1e-4f;

        // moverShielded, struckShielded, struckSuperShielded -> bounces
        [TestCase(false, false, false, false)]   // two plain prisms: both go, as before
        [TestCase(true, false, false, true)]     // a shielded mover bounces off plain mass
        [TestCase(false, true, false, true)]     // a plain mover bounces off a shielded prism
        [TestCase(true, true, false, true)]      // two shielded prisms bounce off each other
        [TestCase(false, false, true, false)]    // a plain mover still goes against super-shielded mass
        [TestCase(true, false, true, true)]      // a shielded mover bounces off super-shielded mass
        public void WhoBounces(bool moverShielded, bool struckShielded, bool struckSuperShielded, bool bounces)
        {
            Assert.AreEqual(bounces, PrismBounce.Bounces(moverShielded, struckShielded, struckSuperShielded));
        }

        [Test]
        public void AHeadOnBounceOffAStillPrism_ReversesTheClosingSpeed_ByTheRestitution()
        {
            var v = new Vector3(10f, 0f, 0f);
            var n = PrismBounce.Normal(new Vector3(-1f, 0f, 0f), Vector3.zero, v);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(-1f, 0f, 0f), n), Eps, "normal points from the struck to the mover");
            var after = v + PrismBounce.Impulse(v, n, 0.6f, 1f);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(-6f, 0f, 0f), after), Eps);
        }

        [Test]
        public void AGlancingBounce_KeepsTheSlide_AndTurnsOnlyTheNormalPart()
        {
            var v = new Vector3(4f, -3f, 0f);
            var after = v + PrismBounce.Impulse(v, Vector3.up, 1f, 1f);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(4f, 3f, 0f), after), Eps);
        }

        [Test]
        public void TwoMovingBodies_ShareTheImpulse_EquallyAndOppositely()
        {
            var va = new Vector3(5f, 0f, 0f);
            var vb = new Vector3(-5f, 0f, 0f);
            var n = Vector3.left;   // from b (on the right) to a (on the left)
            var dv = PrismBounce.Impulse(va - vb, n, 1f, 0.5f);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(-5f, 0f, 0f), va + dv), Eps, "a rebounds");
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(5f, 0f, 0f), vb - dv), Eps, "b rebounds");
            Assert.AreEqual(0f, Vector3.Distance(va + vb, (va + dv) + (vb - dv)), Eps, "momentum is kept");
        }

        [Test]
        public void SeparatingPrisms_DoNotBounce()
        {
            Assert.AreEqual(Vector3.zero, PrismBounce.Impulse(new Vector3(-3f, 0f, 0f), Vector3.left, 0.6f, 1f));
        }

        [Test]
        public void ARebound_MirrorsTheRestOfTheThrow_OffTheContact()
        {
            // thrown 10 u to the right, met at 40% of the way by a prism to its right
            var end = PrismBounce.Rebound(new Vector3(4f, 0f, 0f), new Vector3(10f, 0f, 0f), 0.4f, Vector3.left, 0.5f);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(1f, 0f, 0f), end), Eps, "6 u left over, mirrored, halved");
        }

        [Test]
        public void CoincidentCentres_FallBackToTheWayTheMoverCame()
        {
            var n = PrismBounce.Normal(Vector3.one, Vector3.one, new Vector3(0f, 0f, 2f));
            Assert.AreEqual(0f, Vector3.Distance(Vector3.back, n), Eps);
        }
    }
}
