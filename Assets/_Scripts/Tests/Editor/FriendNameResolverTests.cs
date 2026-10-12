#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Core;
using NUnit.Framework;
using Outcome = CosmicShore.Core.FriendNameResolver.Outcome;
using Pilot = CosmicShore.Core.FriendNameResolver.KnownPilot;

namespace CosmicShore.Tests
{
    /// <summary>
    /// FriendNameResolver tests - what the friends panel's Add Friend field sends.
    ///
    /// WHY THIS MATTERS:
    /// UGS resolves a friend request by NAME only in the full "Name#1234" form (the Friends SDK
    /// sends the text verbatim as the target's profile name), and the game strips the "#1234" from
    /// every name it shows. The first Add Friend sent the typed name straight to UGS, so typing the
    /// name a player could actually see on a row failed for everyone. The panel now sends a full
    /// Name#1234 by name, resolves a bare name against the pilots it knows to a player id, and
    /// refuses the rest with a line asking for the full Name#1234. Each of those branches decides
    /// whether a real request is made, so each is pinned here without a scene or a sign-in.
    /// </summary>
    [TestFixture]
    public class FriendNameResolverTests
    {
        const string Me = "me-id";
        const string MyTag = "Kestrel#4821";

        static readonly List<Pilot> Known = new()
        {
            new Pilot("id-sky", "Sky Walker"),     // a Cloud Save display name, as the Online and RECENT rows carry it
            new Pilot("id-sky", "SkyWalker"),      // the same pilot as a friend row (UGS stem): one pilot, not two
            new Pilot("id-nova-1", "Nova"),
            new Pilot("id-nova-2", "nova"),        // a different pilot with the same name
            new Pilot(Me, "Kestrel"),              // the local player, as the presence lobby lists them
            new Pilot("", "No Id"),                // a row without an id is never a target
        };

        static FriendNameResolver.Resolution Resolve(string typed) =>
            FriendNameResolver.Resolve(typed, Known, Me, MyTag);

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void NothingTyped_IsEmpty(string typed)
        {
            Assert.AreEqual(Outcome.Empty, Resolve(typed).Outcome);
        }

        [Test]
        public void AFullName_GoesByNameAsTyped()
        {
            var r = Resolve("  Orion#0042 ");

            Assert.AreEqual(Outcome.FullName, r.Outcome);
            Assert.AreEqual("Orion#0042", r.FullName, "case and digits are sent as typed: UGS names are case-sensitive");
            Assert.IsNull(r.PlayerId);
        }

        [Test]
        public void AFullName_LosesItsSpaces_SinceAUgsNameHasNone()
        {
            Assert.AreEqual("SkyWalker#1234", Resolve("Sky Walker#1234").FullName);
        }

        [Test]
        public void AFullName_EvenOfAKnownPilot_GoesByName()
        {
            var r = Resolve("SkyWalker#1234");
            Assert.AreEqual(Outcome.FullName, r.Outcome);
        }

        [Test]
        public void YourOwnFullName_IsSelf()
        {
            Assert.AreEqual(Outcome.Self, Resolve("Kestrel#4821").Outcome);
        }

        [TestCase("Sky Walker")]
        [TestCase("SkyWalker")]
        [TestCase("skywalker")]
        [TestCase("sky-walker")]
        public void AShownName_OfOneKnownPilot_GoesByThatPilotsId(string typed)
        {
            var r = Resolve(typed);

            Assert.AreEqual(Outcome.KnownPilot, r.Outcome);
            Assert.AreEqual("id-sky", r.PlayerId);
            Assert.IsNull(r.FullName);
        }

        [Test]
        public void AShownName_TwoPilotsShare_IsAmbiguous()
        {
            var r = Resolve("Nova");

            Assert.AreEqual(Outcome.Ambiguous, r.Outcome, "only the full Name#1234 can tell them apart");
            Assert.IsNull(r.PlayerId);
        }

        [Test]
        public void ANameNobodyKnownCarries_IsUnknown_NeverSentByName()
        {
            // The defect: this used to go to UGS as "Orion", which UGS cannot resolve.
            var r = Resolve("Orion");

            Assert.AreEqual(Outcome.Unknown, r.Outcome);
            Assert.IsNull(r.FullName);
            Assert.IsNull(r.PlayerId);
        }

        [Test]
        public void YourOwnShownName_IsSelf_NotAKnownPilot()
        {
            Assert.AreEqual(Outcome.Self, Resolve("Kestrel").Outcome);
        }

        [Test]
        public void YourOwnShownName_IsSelf_EvenWhenTheLobbyHasNotListedYou()
        {
            var r = FriendNameResolver.Resolve("kestrel", new List<Pilot>(), Me, MyTag);
            Assert.AreEqual(Outcome.Self, r.Outcome);
        }

        [Test]
        public void ARowWithoutAnId_IsNeverATarget()
        {
            Assert.AreEqual(Outcome.Unknown, Resolve("No Id").Outcome);
        }

        [Test]
        public void PunctuationOnly_IsUnknown()
        {
            Assert.AreEqual(Outcome.Unknown, Resolve("!!!").Outcome);
        }

        [TestCase("Name#1234", true)]
        [TestCase("#1234", false)]
        [TestCase("Name#", false)]
        [TestCase("Name", false)]
        public void IsFullName_NeedsTextOnBothSidesOfTheHash(string text, bool expected)
        {
            Assert.AreEqual(expected, FriendNameResolver.IsFullName(text));
        }

        [Test]
        public void Normalize_KeepsLettersAndDigits_LowerCase()
        {
            Assert.AreEqual("skywalker7", FriendNameResolver.Normalize("Sky Walker-7!"));
            Assert.AreEqual(string.Empty, FriendNameResolver.Normalize(null));
        }
    }
}
#endif
