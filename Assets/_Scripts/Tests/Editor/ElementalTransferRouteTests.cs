#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Who settles a networked eject. The shooter's owner decides the hit and the victim's owner
    /// takes the petals (<see cref="ElementalTransfer.ApplyAllAuthoritative"/>). These assert the
    /// routing table, and the petal packing that carries the settled count to the other peers.
    /// </summary>
    public class ElementalTransferRouteTests
    {
        const ElementalTransferForm Eject = ElementalTransferForm.Eject;

        [Test]
        public void OfflineSettlesLocally()
        {
            Assert.AreEqual(ElementalTransferRoute.Local,
                ElementalTransfer.RouteFor(Eject, attackerNetworked: false, attackerOwnedHere: false, victimNetworked: false));
            Assert.AreEqual(ElementalTransferRoute.Local,
                ElementalTransfer.RouteFor(Eject, attackerNetworked: true, attackerOwnedHere: true, victimNetworked: false),
                "a victim with no relay (a mini hull) has no owner to send to");
            Assert.AreEqual(ElementalTransferRoute.Local,
                ElementalTransfer.RouteFor(Eject, attackerNetworked: false, attackerOwnedHere: false, victimNetworked: true),
                "an attacker with no relay keeps the old local behaviour");
        }

        [Test]
        public void OnlyTheShootersOwnerRelays()
        {
            Assert.AreEqual(ElementalTransferRoute.Relay,
                ElementalTransfer.RouteFor(Eject, attackerNetworked: true, attackerOwnedHere: true, victimNetworked: true));
            Assert.AreEqual(ElementalTransferRoute.NotOurs,
                ElementalTransfer.RouteFor(Eject, attackerNetworked: true, attackerOwnedHere: false, victimNetworked: true),
                "a peer replaying somebody else's shot must move nothing, or the victim pays twice");
        }

        [Test]
        public void OnlyAnEjectIsRelayed()
        {
            foreach (var form in new[] { ElementalTransferForm.Steal, ElementalTransferForm.Burn })
                Assert.AreEqual(ElementalTransferRoute.Local,
                    ElementalTransfer.RouteFor(form, attackerNetworked: true, attackerOwnedHere: false, victimNetworked: true),
                    $"{form} is not relayed and settles where it ran, as before");
        }

        [Test]
        public void PackedPetalsRoundTrip()
        {
            uint packed = ElementalTransfer.PackPetals(1, 0, 3, 15);
            Assert.AreEqual(1, ElementalTransfer.PetalsIn(packed, Element.Charge));
            Assert.AreEqual(0, ElementalTransfer.PetalsIn(packed, Element.Mass));
            Assert.AreEqual(3, ElementalTransfer.PetalsIn(packed, Element.Space));
            Assert.AreEqual(15, ElementalTransfer.PetalsIn(packed, Element.Time));
            Assert.AreEqual(19, ElementalTransfer.TotalPetals(packed));
            Assert.AreEqual(0, ElementalTransfer.PetalsIn(packed, Element.Omni), "Omni is not one of the four");
            Assert.AreEqual(0, ElementalTransfer.PetalsIn(packed, Element.None));
        }

        [Test]
        public void PackedPetalsClampAndNothingIsZero()
        {
            Assert.AreEqual(0u, ElementalTransfer.PackPetals(0, 0, 0, 0),
                "nothing settled packs to 0, which the owner reads as 'publish nothing'");
            uint packed = ElementalTransfer.PackPetals(-4, 300, 0, 0);
            Assert.AreEqual(0, ElementalTransfer.PetalsIn(packed, Element.Charge), "a negative count cannot mint");
            Assert.AreEqual(255, ElementalTransfer.PetalsIn(packed, Element.Mass), "an overflow clamps rather than bleeding into Space");
            Assert.AreEqual(0, ElementalTransfer.PetalsIn(packed, Element.Space));
        }
    }
}
#endif
