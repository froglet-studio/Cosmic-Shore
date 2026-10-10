#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Who settles a networked eject or steal. The shooter's owner decides the hit (and is the one
    /// machine that scores it), the victim's owner takes the petals, and a steal is paid on the
    /// thief's owner (<see cref="ElementalTransfer.ApplyAuthoritative"/>). These assert the routing
    /// table, the decided-here predicate the combat-hit reporters gate their score on, the element
    /// mask, and the petal packing that carries the settled count to the other peers.
    /// </summary>
    public class ElementalTransferRouteTests
    {
        const ElementalTransferForm Eject = ElementalTransferForm.Eject;
        // Named once, so check_elemental_economy's sink audit (which looks for a Burn passed as an
        // argument) does not count a routing assertion as a second sink.
        const ElementalTransferForm Burn = ElementalTransferForm.Burn;

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
        public void AStealIsRelayedLikeAnEject()
        {
            const ElementalTransferForm steal = ElementalTransferForm.Steal;
            Assert.AreEqual(ElementalTransferRoute.Relay,
                ElementalTransfer.RouteFor(steal, attackerNetworked: true, attackerOwnedHere: true, victimNetworked: true),
                "the thief's owner decides the joust and hands the take to the victim's owner");
            Assert.AreEqual(ElementalTransferRoute.NotOurs,
                ElementalTransfer.RouteFor(steal, attackerNetworked: true, attackerOwnedHere: false, victimNetworked: true),
                "every other peer's copy of the overlap moves nothing");
            Assert.AreEqual(ElementalTransferRoute.Local,
                ElementalTransfer.RouteFor(steal, attackerNetworked: false, attackerOwnedHere: false, victimNetworked: false),
                "offline a steal settles and pays where it ran, as before");
        }

        [Test]
        public void ABurnIsNeverRelayed()
        {
            foreach (bool owned in new[] { true, false })
                Assert.AreEqual(ElementalTransferRoute.Local,
                    ElementalTransfer.RouteFor(Burn, attackerNetworked: true, attackerOwnedHere: owned, victimNetworked: true),
                    "a burn has no attacker to own it and settles where it ran");
        }

        [Test]
        public void AHitIsDecidedOnlyOnTheShootersOwner()
        {
            Assert.IsTrue(ElementalTransfer.DecidedHere(attackerNetworked: false, attackerOwnedHere: false), "offline");
            Assert.IsTrue(ElementalTransfer.DecidedHere(attackerNetworked: true, attackerOwnedHere: true), "the shooter's owner");
            Assert.IsFalse(ElementalTransfer.DecidedHere(attackerNetworked: true, attackerOwnedHere: false),
                "a replay of somebody else's shot must not score: the server credited the host's copy " +
                "of a client's round AND the client's own report");
            Assert.IsTrue(ElementalTransfer.IsDecidedHere(null), "an anonymous hit is decided where it ran");
        }

        [Test]
        public void ElementMaskCollapsesDuplicatesAndDropsNonElements()
        {
            Assert.AreEqual(0b0110, ElementalTransfer.MaskOf(new[] { Element.Mass, Element.Space }), "the Manta bomb");
            Assert.AreEqual(ElementalTransfer.AllElementsMask,
                ElementalTransfer.MaskOf(new[] { Element.Charge, Element.Mass, Element.Space, Element.Time }));
            Assert.AreEqual(0b0010, ElementalTransfer.MaskOf(new[] { Element.Mass, Element.Mass, Element.Omni, Element.None }));
            Assert.AreEqual(0, ElementalTransfer.MaskOf((Element[])null));
            Assert.IsTrue(ElementalTransfer.InMask(0b0110, Element.Space));
            Assert.IsFalse(ElementalTransfer.InMask(0b0110, Element.Charge));
            Assert.IsFalse(ElementalTransfer.InMask(ElementalTransfer.AllElementsMask, Element.Omni));
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
