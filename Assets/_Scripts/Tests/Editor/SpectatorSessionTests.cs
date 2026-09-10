#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The pure halves of spectate mode and the direct party join: the approval-payload
    /// token the host reads to decline a spectator a Player object, the PartyPlayerData
    /// predicates the online row keys its Join/Spectate button on, and the mode resolver
    /// FriendsListPanel drives that button with. Everything that needs a NetworkManager or
    /// UGS is play-mode only and is covered by Docs/PartySystem/SPECTATOR.md's manual tests.
    /// </summary>
    [TestFixture]
    public class SpectatorSessionTests
    {
        [TearDown]
        public void TearDown() => SpectatorSession.EndLocal();

        #region Approval payload

        [Test]
        public void SpectatorPayload_RoundTrips()
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(SpectatorSession.ApprovalPayloadToken);
            Assert.IsTrue(SpectatorSession.IsSpectatorPayload(bytes));
        }

        [Test]
        public void SpectatorPayload_RejectsNullEmptyAndForeign()
        {
            Assert.IsFalse(SpectatorSession.IsSpectatorPayload(null));
            Assert.IsFalse(SpectatorSession.IsSpectatorPayload(System.Array.Empty<byte>()));
            Assert.IsFalse(SpectatorSession.IsSpectatorPayload(System.Text.Encoding.UTF8.GetBytes("cosmicshore.spectator.v0")));
            // Same length, one byte off - a prefix/length test is not enough.
            var near = System.Text.Encoding.UTF8.GetBytes(SpectatorSession.ApprovalPayloadToken);
            near[near.Length - 1] ^= 1;
            Assert.IsFalse(SpectatorSession.IsSpectatorPayload(near));
        }

        [Test]
        public void BeginLocal_SetsFlagAndTarget_EndLocal_ClearsBoth()
        {
            var target = new PartyPlayerData("p1", "Gradies", 3, 1, 4, "Rampage", "sess-1");
            int changes = 0;
            SpectatorSession.LocalSpectatorChanged += () => changes++;

            SpectatorSession.BeginLocal(target);
            Assert.IsTrue(SpectatorSession.IsLocalSpectator);
            Assert.AreEqual("p1", SpectatorSession.Target.PlayerId);

            SpectatorSession.BeginLocal(target); // idempotent - no second change event
            Assert.AreEqual(1, changes);

            SpectatorSession.EndLocal();
            Assert.IsFalse(SpectatorSession.IsLocalSpectator);
            Assert.AreEqual(string.Empty, SpectatorSession.Target.PlayerId ?? string.Empty);
            Assert.AreEqual(2, changes);
        }

        #endregion

        #region PartyPlayerData predicates

        [Test]
        public void PartyPlayerData_SessionAndMatchPredicates()
        {
            var idle = new PartyPlayerData("a", "A", 0);
            Assert.IsFalse(idle.HasJoinableSession);
            Assert.IsFalse(idle.IsInMatch);
            Assert.AreEqual(string.Empty, idle.PartySessionId);

            var inParty = new PartyPlayerData("b", "B", 0, 2, 4, null, "sess");
            Assert.IsTrue(inParty.HasJoinableSession);
            Assert.IsFalse(inParty.IsInMatch);

            var inMatch = new PartyPlayerData("c", "C", 0, 2, 4, "Joust", "sess");
            Assert.IsTrue(inMatch.IsInMatch);
            Assert.IsTrue(inMatch.HasJoinableSession);
        }

        [Test]
        public void PartyPlayerData_EqualityStaysByPlayerId()
        {
            var a1 = new PartyPlayerData("a", "A", 0, 1, 4, null, "s1");
            var a2 = new PartyPlayerData("a", "A", 0, 3, 4, "Joust", "s2");
            Assert.AreEqual(a1, a2, "the session id must not break SOAP-list dedup");
        }

        #endregion

        #region Join / Spectate mode resolution

        static PartyPlayerData With(string session, string match = null, int count = 1) =>
            new("x", "X", 0, count, 4, match, session);

        [Test]
        public void JoinMode_OnlineWithSession_IsJoin()
        {
            Assert.AreEqual(OnlineInfoEntry.JoinMode.Join,
                FriendsListPanel.ResolveJoinMode(With("s"), OnlineInfoEntry.Status.Online));
            Assert.AreEqual(OnlineInfoEntry.JoinMode.Join,
                FriendsListPanel.ResolveJoinMode(With("s", count: 2), OnlineInfoEntry.Status.InLobby));
        }

        [Test]
        public void JoinMode_NoAdvertisedSession_IsHidden()
        {
            Assert.AreEqual(OnlineInfoEntry.JoinMode.Hidden,
                FriendsListPanel.ResolveJoinMode(With(null), OnlineInfoEntry.Status.Online));
            Assert.AreEqual(OnlineInfoEntry.JoinMode.Hidden,
                FriendsListPanel.ResolveJoinMode(With(""), OnlineInfoEntry.Status.LobbyFull));
        }

        [Test]
        public void JoinMode_FullParty_IsJoinDisabled()
        {
            Assert.AreEqual(OnlineInfoEntry.JoinMode.JoinDisabled,
                FriendsListPanel.ResolveJoinMode(With("s", count: 4), OnlineInfoEntry.Status.LobbyFull));
        }

        [Test]
        public void JoinMode_InMatch_IsSpectate_OrDisabledWithoutSession()
        {
            Assert.AreEqual(OnlineInfoEntry.JoinMode.Spectate,
                FriendsListPanel.ResolveJoinMode(With("s", "Rampage"), OnlineInfoEntry.Status.InMatch));
            // A spectator (or an older peer) in a match advertises no session: eye drawn, inert.
            Assert.AreEqual(OnlineInfoEntry.JoinMode.SpectateDisabled,
                FriendsListPanel.ResolveJoinMode(With(null, "Rampage"), OnlineInfoEntry.Status.InMatch));
        }

        [Test]
        public void JoinMode_InYourParty_IsHidden()
        {
            Assert.AreEqual(OnlineInfoEntry.JoinMode.Hidden,
                FriendsListPanel.ResolveJoinMode(With("s", count: 2), OnlineInfoEntry.Status.InYourParty));
        }

        #endregion
    }
}
#endif
