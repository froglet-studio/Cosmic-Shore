using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// SILENCE (Docs/ElementalAbilitySystem/SILENCE.md): a strip or a steal silences its victim for a
    /// time proportional to the petals lost. These pin the pure rules and the registry's gates; the
    /// transfer half (who is silenced, a silenced attacker taking nothing, the owner's publish) runs
    /// in Tools/Build/elemental_transfer_harness, block T11.
    /// </summary>
    public class VesselSilenceTests
    {
        [TearDown]
        public void ClearRegistry() => VesselSilence.ClearAll();

        [Test]
        public void SecondsAreProportionalToPetals()
        {
            Assert.AreEqual(4f, VesselSilence.SecondsFor(4, 1f));
            Assert.AreEqual(1.5f, VesselSilence.SecondsFor(3, 0.5f));
            Assert.AreEqual(0f, VesselSilence.SecondsFor(0, 1f));
            Assert.AreEqual(0f, VesselSilence.SecondsFor(-3, 1f));
        }

        [Test]
        public void ANewTakeAddsUpToTheCeiling()
        {
            Assert.AreEqual(5f, VesselSilence.Extend(2f, 3f, 8f));
            Assert.AreEqual(8f, VesselSilence.Extend(6f, 5f, 8f));
            Assert.AreEqual(2f, VesselSilence.Extend(-1f, 2f, 8f), "an expired window counts as none");
            Assert.AreEqual(0f, VesselSilence.Extend(3f, 3f, 0f), "a zero ceiling switches silence off");
        }

        [Test]
        public void OnlyStripsAndStealsSilence()
        {
            var shipped = ElementalDebuffSources.Explosion | ElementalDebuffSources.VesselContact
                          | ElementalDebuffSources.Other;
            Assert.IsTrue(VesselSilence.Silences(ElementalDebuffSources.VesselContact, shipped));
            Assert.IsTrue(VesselSilence.Silences(ElementalDebuffSources.Explosion, shipped));
            Assert.IsTrue(VesselSilence.Silences(ElementalDebuffSources.Other, shipped));
            Assert.IsFalse(VesselSilence.Silences(ElementalDebuffSources.DangerPrism, shipped));
            Assert.IsFalse(VesselSilence.Silences(ElementalDebuffSources.WormholeToll, shipped));
            Assert.IsFalse(VesselSilence.Silences(ElementalDebuffSources.None, shipped));
        }

        [Test]
        public void ASilencedPilotIsRefusedOnRivalMassAndAllowedOnTheirOwn()
        {
            VesselSilence.Record("silenced", Domains.Ruby, 60f);
            Assert.IsTrue(VesselSilence.IsSilenced("silenced"));
            Assert.IsTrue(VesselSilence.Protects("silenced", Domains.Jade));
            Assert.IsTrue(VesselSilence.Protects("silenced", Domains.Blue), "neutral (Blue) mass is protected too");
            Assert.IsFalse(VesselSilence.Protects("silenced", Domains.Ruby), "their own domain's mass is theirs to touch");
            Assert.IsFalse(VesselSilence.Protects("free", Domains.Jade), "nobody else is touched");
            Assert.IsFalse(VesselSilence.Protects(null, Domains.Jade));
        }

        [Test]
        public void ATurnResetLiftsTheSilence()
        {
            VesselSilence.Record("silenced", Domains.Ruby, 60f);
            VesselSilence.Lift("silenced");
            Assert.IsFalse(VesselSilence.IsSilenced("silenced"));
            Assert.IsFalse(VesselSilence.Protects("silenced", Domains.Jade));
        }

        [Test]
        public void AZeroWindowRecordsNothing()
        {
            VesselSilence.Record("silenced", Domains.Ruby, 0f);
            Assert.IsFalse(VesselSilence.IsSilenced("silenced"));
            Assert.AreEqual(0f, VesselSilence.Remaining01("silenced"));
        }
    }
}
