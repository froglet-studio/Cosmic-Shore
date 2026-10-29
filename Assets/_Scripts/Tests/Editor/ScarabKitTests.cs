using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab's kit as Garrett recut it on 2026-10-29 (R_VesselActions/SCARAB.md section 7): Charge scales the
    /// shove's DEBUFF and Charge 5 (Backdraft) adds the pull; Mass 5 (Sun Cores) adds the dais's super-shielded suns;
    /// Space sizes the seeded balls and its card shows them growing; Time 5's Snap Dash waits 5 s between dashes, and the Throttle card's wake grows by thirds of the pull.
    /// A slip here hands a level its old meaning back without anyone noticing.
    /// </summary>
    public class ScarabKitTests
    {
        [Test]
        public void TheShove_PushesOnlyAtRest() => Assert.IsFalse(ScarabCavitationBlast.PlateMirrors(false));

        [Test]
        public void Backdraft_AddsThePull() => Assert.IsTrue(ScarabCavitationBlast.PlateMirrors(true));

        [Test]
        public void ChargeZero_BitesAtTheBase()
            => Assert.AreEqual(1f, VesselElementalDebuffByExplosionEffectSO.ChargeScale(2.5f, 0), 1e-5f);

        [Test]
        public void ChargeTen_BitesTwoAndAHalfTimesHarder()
            => Assert.AreEqual(2.5f, VesselElementalDebuffByExplosionEffectSO.ChargeScale(2.5f, 10), 1e-5f);

        [Test]
        public void Overcharge_DoesNotKeepPaying()
            => Assert.AreEqual(2.5f, VesselElementalDebuffByExplosionEffectSO.ChargeScale(2.5f, 15), 1e-5f);

        [Test]
        public void ABlastWithoutChargeScaling_IsUntouchedByCharge()
            => Assert.AreEqual(1f, VesselElementalDebuffByExplosionEffectSO.ChargeScale(1f, 10), 1e-5f);

        [Test]
        public void WithoutSunCores_TheDaisRaisesNoSuperShieldedSuns()
            => Assert.IsFalse(ScarabSwitch.LaysDaisElement(PrismKind.SuperShielded, false));

        [Test]
        public void WithSunCores_TheSunsRise()
            => Assert.IsTrue(ScarabSwitch.LaysDaisElement(PrismKind.SuperShielded, true));

        [Test]
        public void TheWings_AlwaysRise()
        {
            Assert.IsTrue(ScarabSwitch.LaysDaisElement(PrismKind.Shielded, false));
            Assert.IsTrue(ScarabSwitch.LaysDaisElement(PrismKind.Plain, false));
        }

        [Test]
        public void SnapDash_WaitsItsCooldown()
        {
            Assert.IsFalse(ScarabVesselTransformer.SnapDashOffCooldown(14.9f, 10f, 5f));
            Assert.IsTrue(ScarabVesselTransformer.SnapDashOffCooldown(15f, 10f, 5f));
        }

        [Test]
        public void SpaceOne_DrawsTheSeededBallAtAQuarter() => Assert.AreEqual(0.25f, ScarabHUDView.SeedBallScale(1f, 4f), 1e-5f);

        [Test]
        public void Released_ShowsNoWake() => Assert.AreEqual(-1, ScarabHUDView.WakeStepFor(0f));

        [Test]
        public void TheWake_GrowsByThirdsOfThePull()
        {
            Assert.AreEqual(0, ScarabHUDView.WakeStepFor(0.2f));
            Assert.AreEqual(0, ScarabHUDView.WakeStepFor(1f / 3f));
            Assert.AreEqual(1, ScarabHUDView.WakeStepFor(0.5f));
            Assert.AreEqual(2, ScarabHUDView.WakeStepFor(1f));
        }

        [Test]
        public void SpaceTen_DrawsTheSeededBallFull() => Assert.AreEqual(1f, ScarabHUDView.SeedBallScale(4f, 4f), 1e-5f);
    }
}
