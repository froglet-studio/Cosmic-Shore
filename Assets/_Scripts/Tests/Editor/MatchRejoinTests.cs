#if UNITY_EDITOR
using System;
using NUnit.Framework;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The pure halves of rejoining a running match (Docs/MENU_REDESIGN_B.md section 103): the approval payload the host
    /// reads to hand a seat back instead of minting a second Player, and the remembered match a guest keeps across a drop
    /// or a restart. The hand-back itself needs a NetworkManager and two clients and is in the verification checklist.
    /// </summary>
    [TestFixture]
    public class MatchRejoinTests
    {
        [SetUp]
        public void SetUp() => MatchRejoin.Forget();

        [TearDown]
        public void TearDown() => MatchRejoin.Forget();

        [Test]
        public void Payload_RoundTripsThePlayersUgsId()
        {
            Assert.IsTrue(MatchRejoin.TryParsePayload(MatchRejoin.PayloadFor("abc123"), out var id));
            Assert.AreEqual("abc123", id);
        }

        [Test]
        public void Payload_IsNotTheSpectatorsAndNotEmpty()
        {
            var spectator = System.Text.Encoding.UTF8.GetBytes(SpectatorSession.ApprovalPayloadToken);
            Assert.IsFalse(MatchRejoin.TryParsePayload(spectator, out _), "a spectator must never read as a rejoin");
            Assert.IsFalse(MatchRejoin.TryParsePayload(System.Text.Encoding.UTF8.GetBytes(MatchRejoin.PayloadPrefix), out _),
                "a rejoin with no player id names no seat");
            Assert.IsFalse(MatchRejoin.TryParsePayload(null, out _));
            Assert.IsFalse(SpectatorSession.IsSpectatorPayload(MatchRejoin.PayloadFor("abc123")),
                "a rejoin must never read as a spectator (it would get no seat at all)");
        }

        [Test]
        public void Record_RemembersTheMatchUntilForgotten()
        {
            MatchRejoin.Remember("session-1", GameModes.SkimRace);
            Assert.IsTrue(MatchRejoin.TryLoad(out var r));
            Assert.AreEqual("session-1", r.sessionId);
            Assert.AreEqual(GameModes.SkimRace, r.Mode);
            Assert.IsFalse(r.Dropped);

            MatchRejoin.Forget();
            Assert.IsFalse(MatchRejoin.TryLoad(out _));
        }

        [Test]
        public void Record_MarkDroppedKeepsTheMatchAndSaysSo()
        {
            MatchRejoin.Remember("session-1", GameModes.Joust);
            MatchRejoin.MarkDropped();
            Assert.IsTrue(MatchRejoin.TryLoad(out var r));
            Assert.IsTrue(r.Dropped);
            Assert.AreEqual("session-1", r.sessionId);
        }

        [Test]
        public void Record_TooOldIsNeverOffered()
        {
            MatchRejoin.Remember("session-1", GameModes.SkimRace);
            Assert.IsFalse(MatchRejoin.TryLoad(DateTime.UtcNow + MatchRejoin.MaxAge + TimeSpan.FromMinutes(1), out _));
            Assert.IsFalse(MatchRejoin.TryLoad(out _), "an expired record is forgotten, not kept for later");
        }

        [Test]
        public void Record_WithoutASessionIsNotRemembered()
        {
            MatchRejoin.Remember(string.Empty, GameModes.SkimRace);
            Assert.IsFalse(MatchRejoin.TryLoad(out _));
        }
    }
}
#endif
