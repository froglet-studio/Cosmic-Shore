#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using Unity.Services.Multiplayer;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The party recovery fixes from the four-player test of 2026-10-30 (Docs/PartySystem/BUGS.md B39-B42): a dead
    /// session stops being offered, a stale lobby membership is recognised, and a host with others in its party cannot
    /// end it by pressing Join or Spectate.
    /// </summary>
    [TestFixture]
    public class PartyRecoveryTests
    {
        readonly List<Object> _made = new();
        T Track<T>(T o) where T : Object { _made.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ── DeadPartySessions ──────────────────────────────────────────────────────────────────

        [Test]
        public void DeadSession_IsDeadForItsLifetime_ThenOfferedAgain()
        {
            var t0 = new DateTime(2026, 10, 30, 23, 59, 0, DateTimeKind.Utc);
            DeadPartySessions.Mark("kyoki-session", t0);

            Assert.IsTrue(DeadPartySessions.IsDead("kyoki-session", t0.AddMinutes(1)));
            Assert.IsFalse(DeadPartySessions.IsDead("another-session", t0.AddMinutes(1)), "only the session that failed");
            Assert.IsFalse(DeadPartySessions.IsDead("kyoki-session", t0 + DeadPartySessions.Lifetime + TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void DeadSession_EmptyIdIsNeverDead()
        {
            DeadPartySessions.Mark(string.Empty);
            DeadPartySessions.Mark(null);
            Assert.IsFalse(DeadPartySessions.IsDead(string.Empty));
            Assert.AreEqual(string.Empty, DeadPartySessions.Live(string.Empty));
        }

        [Test]
        public void DeadSession_LiveClearsTheAdvertisement()
        {
            DeadPartySessions.Mark("gone-1");
            Assert.AreEqual(string.Empty, DeadPartySessions.Live("gone-1"), "a dead session is advertised as none, so the row offers no Join");
            Assert.AreEqual("alive-1", DeadPartySessions.Live("alive-1"));
        }

        // ── StaleLobbyMembership ───────────────────────────────────────────────────────────────

        [Test]
        public void AlreadyMember_IsRecognisedAnywhereInTheChain()
        {
            var conflict = new SessionException("[Error: SessionConflict] [Message: player is already a member of the lobby]",
                SessionError.SessionConflict, null);
            Assert.IsTrue(StaleLobbyMembership.IsAlreadyMember(conflict));
            Assert.IsTrue(StaleLobbyMembership.IsAlreadyMember(new Exception("join failed", new Exception("Player is ALREADY A MEMBER"))));
            Assert.IsFalse(StaleLobbyMembership.IsAlreadyMember(new Exception("lobby not found")));
            Assert.IsFalse(StaleLobbyMembership.IsAlreadyMember(null));
        }

        [Test]
        public void FailedJoin_LeavesAMembershipUnlessRefusedAtTheDoor()
        {
            Assert.IsTrue(StaleLobbyMembership.MayHaveLeftAMembership(
                new SessionException("Failed to join allocation", SessionError.Unknown, new Exception("Not Found: join code not found"))),
                "the lobby took us, the Relay did not: we are still listed");
            Assert.IsFalse(StaleLobbyMembership.MayHaveLeftAMembership(new SessionException("lobby is full", SessionError.Unknown, null)));
            Assert.IsFalse(StaleLobbyMembership.MayHaveLeftAMembership(new OperationCanceledException()));
        }

        // ── A host's own party ─────────────────────────────────────────────────────────────────

        [Test]
        public void HostWithOthers_WouldEndItsParty_SoloHostWouldNot()
        {
            var data = Track(ScriptableObject.CreateInstance<HostConnectionDataSO>());
            data.PartyMembers = Track(ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>());
            data.IsPartyHost = true;
            data.PartyMembers.Add(new PartyPlayerData("me", "Me", 0));
            Assert.IsFalse(PartyInviteController.WouldEndOwnParty(data), "the eager solo host: nobody else to bounce");

            data.PartyMembers.Add(new PartyPlayerData("p2", "Shombith", 0));
            Assert.IsTrue(PartyInviteController.WouldEndOwnParty(data), "Caleb's spectate on 2026-10-30 bounced Shombith");

            data.IsPartyHost = false;
            Assert.IsFalse(PartyInviteController.WouldEndOwnParty(data), "a guest's join ends nobody else's party");
            Assert.IsFalse(PartyInviteController.WouldEndOwnParty(null));
        }
    }
}
#endif
