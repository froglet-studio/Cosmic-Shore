#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// JoinTargetValidator Tests - the zero-request pre-flight that runs before an Accept / Join /
    /// Spectate tears the local host down (Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md §3.7).
    ///
    /// WHY THIS MATTERS:
    /// A refusal here costs the player a toast; a wrong refusal costs them a join they could have
    /// made, and a missed refusal costs them their own session plus a scene-reload bounce. Both
    /// directions are pinned, including the "unknown list passes" rule - a null OnlinePlayers must
    /// never read as "nobody is online".
    /// </summary>
    [TestFixture]
    public class JoinTargetValidatorTests
    {
        private static PartyPlayerData Player(string id, string session, int count = 1, int max = 4, string match = "", string name = "Pilot") =>
            new PartyPlayerData(id, name, 0, count, max, match, session);

        private static JoinTargetVerdict Validate(IEnumerable<PartyPlayerData> online, string target, string session, bool spectate = false, bool offline = false, string expectMessageContains = null)
        {
            var verdict = JoinTargetValidator.Validate(online, target, session, spectate, offline, out var message);
            if (verdict == JoinTargetVerdict.Ok) Assert.IsEmpty(message);
            else
            {
                Assert.IsNotEmpty(message, "every refusal carries a player-facing sentence");
                if (expectMessageContains != null) StringAssert.Contains(expectMessageContains, message);
            }
            return verdict;
        }

        [Test]
        public void HappyPath_TargetOnlineSameSessionWithRoom_IsOk() =>
            Assert.AreEqual(JoinTargetVerdict.Ok, Validate(new[] { Player("A", "S1", count: 2, max: 4) }, "A", "S1"));

        [Test]
        public void OfflineSession_RefusedBeforeAnythingElse() =>
            Assert.AreEqual(JoinTargetVerdict.OfflineSession, Validate(new[] { Player("A", "S1") }, "A", "S1", offline: true, expectMessageContains: "Offline"));

        [Test]
        public void EmptySessionId_IsNoSession() =>
            Assert.AreEqual(JoinTargetVerdict.NoSession, Validate(new[] { Player("A", "S1") }, "A", "", expectMessageContains: "joinable"));

        [Test]
        public void NullOnlineList_IsUnknownAndPasses() =>
            Assert.AreEqual(JoinTargetVerdict.Ok, Validate(null, "A", "S1"));

        [Test]
        public void TargetNotInLobby_IsTargetOffline() =>
            Assert.AreEqual(JoinTargetVerdict.TargetOffline, Validate(new[] { Player("B", "S2") }, "A", "S1", expectMessageContains: "no longer online"));

        [Test]
        public void TargetAdvertisesAnotherSession_IsSessionChanged() =>
            Assert.AreEqual(JoinTargetVerdict.SessionChanged, Validate(new[] { Player("A", "S9", name: "Ada") }, "A", "S1", expectMessageContains: "Ada's party is no longer available"));

        [Test]
        public void TargetAdvertisesNoSession_IsSessionChanged()
        {
            // A spectator or an offline peer publishes an empty partySession - nothing to join.
            Assert.AreEqual(JoinTargetVerdict.SessionChanged, Validate(new[] { Player("A", "") }, "A", "S1"));
        }

        // ── B29, the Accept case (defect 5 of the five-process runs) ────────────────────────────
        // Right after a host drop the sender re-creates its session and invites at once. The invite
        // used to be published ALONE, so a poll read the new invite next to the sender's OLD
        // partySession, and this pre-flight refused a valid invite as "no longer available".

        [Test]
        public void AFreshInvite_NextToAStaleAdvertisement_WasRefused()
        {
            // The failure mode itself, pinned: the invite names S2 while the row still says S1.
            Assert.AreEqual(JoinTargetVerdict.SessionChanged,
                Validate(new[] { Player("C", "S1", name: "PilotC") }, "C", "S2", expectMessageContains: "no longer available"));
        }

        [Test]
        public void AnInvitePublish_CarriesTheSessionItNames_SoTheAcceptPreFlightPasses()
        {
            var props = HostConnectionService.InvitePublicationProperties("line naming S2", "S2");
            Assert.AreEqual(2, props.Count, "the invite lines and the advertisement, in one save");
            Assert.AreEqual("line naming S2", props[Const("INVITE_PAYLOADS_KEY")]);
            Assert.AreEqual("S2", props[Const("PARTY_SESSION_KEY")]);
            // What the invitee's next poll reads for the sender, whenever it lands after that save.
            var sender = Player("C", props[Const("PARTY_SESSION_KEY")], name: "PilotC");
            Assert.AreEqual(JoinTargetVerdict.Ok, Validate(new[] { sender }, "C", "S2"));
        }

        [Test]
        public void AnInvitePublish_WithNoLiveSession_AdvertisesNone()
        {
            var props = HostConnectionService.InvitePublicationProperties(null, null);
            Assert.AreEqual(string.Empty, props[Const("INVITE_PAYLOADS_KEY")]);
            Assert.AreEqual(string.Empty, props[Const("PARTY_SESSION_KEY")]);
        }

        private static string Const(string name) =>
            (string)typeof(HostConnectionService)
                .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .GetRawConstantValue();

        [Test]
        public void FullParty_IsPartyFull() =>
            Assert.AreEqual(JoinTargetVerdict.PartyFull, Validate(new[] { Player("A", "S1", count: 4, max: 4, name: "Ada") }, "A", "S1", expectMessageContains: "Ada's party is full"));

        [Test]
        public void UnknownPartyMax_DoesNotRefuse()
        {
            // partyMax 0 is "not published yet", not "no seats".
            Assert.AreEqual(JoinTargetVerdict.Ok, Validate(new[] { Player("A", "S1", count: 7, max: 0) }, "A", "S1"));
        }

        [Test]
        public void Spectate_TargetInMatch_WithASeat_IsOk() =>
            Assert.AreEqual(JoinTargetVerdict.Ok, Validate(new[] { Player("A", "S1", count: 3, max: 4, match: "Dogfight") }, "A", "S1", spectate: true));

        [Test]
        public void Spectate_FullParty_IsPartyFull() =>
            // A spectator takes a seat in the party's session, whose seat count is the party size.
            Assert.AreEqual(JoinTargetVerdict.PartyFull, Validate(new[] { Player("A", "S1", count: 4, max: 4, match: "Dogfight", name: "Ada") }, "A", "S1", spectate: true, expectMessageContains: "Ada's party is full"));

        [Test]
        public void Spectate_TargetNotInMatch_IsNotInMatch() =>
            Assert.AreEqual(JoinTargetVerdict.NotInMatch, Validate(new[] { Player("A", "S1", name: "Ada") }, "A", "S1", spectate: true, expectMessageContains: "Ada is not in a match"));

        [Test]
        public void Spectate_EmptySessionId_HasSpectateWording() =>
            Assert.AreEqual(JoinTargetVerdict.NoSession, Validate(new[] { Player("A", "S1") }, "A", "", spectate: true, expectMessageContains: "spectate"));

        [Test]
        public void MissingDisplayName_FallsBackToThatPilot() =>
            Assert.AreEqual(JoinTargetVerdict.PartyFull, Validate(new[] { Player("A", "S1", count: 4, max: 4, name: "") }, "A", "S1", expectMessageContains: "That pilot's party is full"));

        [Test]
        public void VerdictValues_AreStable()
        {
            // Serialization / analytics may persist the verdict; the numbers must not drift.
            Assert.AreEqual(0, (int)JoinTargetVerdict.Ok);
            Assert.AreEqual(1, (int)JoinTargetVerdict.OfflineSession);
            Assert.AreEqual(2, (int)JoinTargetVerdict.NoSession);
            Assert.AreEqual(3, (int)JoinTargetVerdict.TargetOffline);
            Assert.AreEqual(4, (int)JoinTargetVerdict.SessionChanged);
            Assert.AreEqual(5, (int)JoinTargetVerdict.PartyFull);
            Assert.AreEqual(6, (int)JoinTargetVerdict.NotInMatch);
        }
    }
}
#endif
