#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The home hub's "remember my last setup" contract (<see cref="LaunchPreference"/> +
    /// <see cref="LaunchPreferenceRules"/>): a card re-opens on what it was last launched with,
    /// and every remembered value is a WISH that is re-validated against the card, the unlocks
    /// and the party before it is seeded. These hold the pure half; the store itself is a thin
    /// list over the disk accessor and is exercised by the modal in play.
    /// </summary>
    public class HomeHubPreferenceTests
    {
        // ── Intensity ────────────────────────────────────────────────────────

        [Test]
        public void Intensity_NeverWritten_OpensOnCardMinimum()
        {
            Assert.AreEqual(1, LaunchPreferenceRules.ResolveIntensity(0, 1, 4, 4));
            Assert.AreEqual(2, LaunchPreferenceRules.ResolveIntensity(0, 2, 4, 4));
        }

        [Test]
        public void Intensity_Remembered_IsRestored()
        {
            Assert.AreEqual(3, LaunchPreferenceRules.ResolveIntensity(3, 1, 4, 4));
        }

        [Test]
        public void Intensity_Remembered_IsClampedToUnlocks()
        {
            // Saved 4, but only 1..2 unlocked: opens on 2, never on a locked button.
            Assert.AreEqual(2, LaunchPreferenceRules.ResolveIntensity(4, 1, 4, 2));
        }

        [Test]
        public void Intensity_Remembered_IsClampedToCardRange()
        {
            Assert.AreEqual(3, LaunchPreferenceRules.ResolveIntensity(4, 1, 3, 4));
            Assert.AreEqual(2, LaunchPreferenceRules.ResolveIntensity(1, 2, 4, 4));
        }

        // ── AI placements ────────────────────────────────────────────────────

        [Test]
        public void AiPlacements_KeepOrder_AndDropBlue()
        {
            var saved = new List<Domains> { Domains.Ruby, Domains.Blue, Domains.Gold };
            var got = LaunchPreferenceRules.ResolveAiPlacements(saved, 4);
            CollectionAssert.AreEqual(new[] { Domains.Ruby, Domains.Gold }, got);
        }

        [Test]
        public void AiPlacements_AreCutToFreeSeats()
        {
            // A party that grew since the launch gets fewer bots back, in placement order.
            var saved = new List<Domains> { Domains.Ruby, Domains.Ruby, Domains.Gold };
            var got = LaunchPreferenceRules.ResolveAiPlacements(saved, 1);
            CollectionAssert.AreEqual(new[] { Domains.Ruby }, got);
            Assert.IsEmpty(LaunchPreferenceRules.ResolveAiPlacements(saved, 0));
            Assert.IsEmpty(LaunchPreferenceRules.ResolveAiPlacements(null, 3));
        }

        // ── Domain count ─────────────────────────────────────────────────────

        [Test]
        public void DomainCount_NeverWritten_AnswersFallback()
        {
            Assert.AreEqual(3, LaunchPreferenceRules.ResolveDomainCount(0, 3, 1, 3, null));
        }

        [Test]
        public void DomainCount_CoversEveryPlacement()
        {
            // A Gold bot needs all three domains even if the launch was saved at two.
            var placed = new List<Domains> { Domains.Gold };
            Assert.AreEqual(3, LaunchPreferenceRules.ResolveDomainCount(2, 3, 1, 3, placed));
        }

        [Test]
        public void DomainCount_IsClampedToTheCard()
        {
            // Astro League pins two domains: a saved three collapses to two.
            Assert.AreEqual(2, LaunchPreferenceRules.ResolveDomainCount(3, 3, 1, 2, null));
            // Joust's minimum of two floors a saved one.
            Assert.AreEqual(2, LaunchPreferenceRules.ResolveDomainCount(1, 2, 2, 3, null));
        }

        // ── Pilot domain ─────────────────────────────────────────────────────

        [Test]
        public void PilotDomain_Unwritten_IsJade()
        {
            Assert.AreEqual(Domains.Jade, LaunchPreferenceRules.ResolvePilotDomain(Domains.Blue, 3));
        }

        [Test]
        public void PilotDomain_InsideThePrefix_IsRestored()
        {
            Assert.AreEqual(Domains.Gold, LaunchPreferenceRules.ResolvePilotDomain(Domains.Gold, 3));
            Assert.AreEqual(Domains.Ruby, LaunchPreferenceRules.ResolvePilotDomain(Domains.Ruby, 2));
        }

        [Test]
        public void PilotDomain_OutsideThePrefix_FallsBackToJade()
        {
            // A Gold pick on a two-domain lobby would light a dimmed tile.
            Assert.AreEqual(Domains.Jade, LaunchPreferenceRules.ResolvePilotDomain(Domains.Gold, 2));
        }

        // ── The two halves of the record ─────────────────────────────────────

        [Test]
        public void HostTerms_WriteBothHalves()
        {
            var record = LaunchPreference.Empty(GameModes.ScarabScramble)
                .WithHostTerms(3, 3, new List<Domains> { Domains.Ruby, Domains.Ruby },
                               Domains.Gold, VesselClassType.Scarab);

            Assert.IsTrue(record.HasHostTerms);
            Assert.IsTrue(record.HasPilotChoice);
            Assert.AreEqual(3, record.Intensity);
            Assert.AreEqual(3, record.DomainCount);
            CollectionAssert.AreEqual(new[] { Domains.Ruby, Domains.Ruby }, record.AIDomains);
            Assert.AreEqual(Domains.Gold, record.Domain);
            Assert.AreEqual(VesselClassType.Scarab, record.Vessel);
        }

        [Test]
        public void PilotChoice_LeavesHostTermsAlone()
        {
            // A guest readying on a card this machine once hosted must not clobber the host
            // terms it last launched with.
            var hosted = LaunchPreference.Empty(GameModes.ScarabScramble)
                .WithHostTerms(3, 3, new List<Domains> { Domains.Ruby }, Domains.Jade, VesselClassType.Scarab);
            var asGuest = hosted.WithPilotChoice(Domains.Ruby, VesselClassType.Scarab);

            Assert.IsTrue(asGuest.HasHostTerms);
            Assert.AreEqual(3, asGuest.Intensity);
            Assert.AreEqual(3, asGuest.DomainCount);
            CollectionAssert.AreEqual(new[] { Domains.Ruby }, asGuest.AIDomains);
            Assert.AreEqual(Domains.Ruby, asGuest.Domain);
        }

        [Test]
        public void Copies_DoNotShareTheirPlacementList()
        {
            var a = LaunchPreference.Empty(GameModes.Rampage)
                .WithHostTerms(1, 3, new List<Domains> { Domains.Ruby }, Domains.Jade, VesselClassType.Dolphin);
            var b = a.WithPilotChoice(Domains.Gold, VesselClassType.Dolphin);
            b.AIDomains.Add(Domains.Gold);

            Assert.AreEqual(1, a.AIDomains.Count);
        }

        [Test]
        public void Empty_HasNeitherHalf()
        {
            var empty = LaunchPreference.Empty(GameModes.Rampage);
            Assert.IsFalse(empty.HasHostTerms);
            Assert.IsFalse(empty.HasPilotChoice);
            Assert.AreEqual(Domains.Blue, empty.Domain);
            Assert.AreEqual(VesselClassType.Random, empty.Vessel);
        }
    }
}
#endif
