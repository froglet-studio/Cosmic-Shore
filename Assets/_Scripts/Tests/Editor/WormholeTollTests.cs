#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Who pays to thread a Butterfly's wormhole (<see cref="WormholeMouth.OwesToll(bool,int,bool,bool,Domains,Domains)"/>).
    /// A mouth carries ANYONE; a pilot not of its domain has petals stripped at the surface. Every
    /// rule here fails silently in play — a rival who rides free, or a Butterfly charged for its own
    /// road — so each is pinned.
    /// </summary>
    public class WormholeTollTests
    {
        const Domains Mouth = Domains.Jade;
        const Domains Rival = Domains.Ruby;

        static bool Owes(bool tolled = true, int petals = 15, bool isOwner = false,
                         bool known = true, Domains pilot = Rival) =>
            WormholeMouth.OwesToll(tolled, petals, isOwner, known, pilot, Mouth);

        [Test]
        public void ARivalPays()
        {
            Assert.IsTrue(Owes(), "a pilot of another domain must pay to thread the pair");
        }

        [Test]
        public void TheMouthsOwnDomainRidesFree()
        {
            Assert.IsFalse(Owes(pilot: Mouth), "the Butterfly's own team must ride free");
        }

        [Test]
        public void TheOwnerNeverPays_WhateverTheDomainReadsSay()
        {
            Assert.IsFalse(Owes(isOwner: true),
                "the placing Butterfly must never be charged for its own wormhole, even if a stale " +
                "domain read disagrees with the mouth's");
        }

        [Test]
        public void AnUnreadablePilotRidesFree()
        {
            // The toll is a restriction, so it is applied only on positive evidence (vessel skill
            // §4.ac): a pilot with no Player yet is carried free rather than charged on a guess.
            Assert.IsFalse(Owes(known: false));
        }

        [Test]
        public void AnUntolledMouthOrAZeroTollChargesNobody()
        {
            Assert.IsFalse(Owes(tolled: false), "an untolled mouth must charge nobody");
            Assert.IsFalse(Owes(petals: 0), "a toll of 0 petals is the authored 'everyone rides free'");
        }
    }
}
#endif
