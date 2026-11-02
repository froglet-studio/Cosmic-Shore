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

        // ── B47 round 2: a REJOIN that did not land keeps the match on offer ──

        [Test]
        public void Attempt_Landed_WhenTheClaimReRememberedTheMatch()
        {
            Assert.AreEqual(MatchRejoin.AttemptOutcome.Landed, MatchRejoin.ClassifyAttempt(true, 200, 100, null));
            Assert.AreEqual(MatchRejoin.AttemptOutcome.Landed, MatchRejoin.ClassifyAttempt(true, 100, 100, null));
        }

        [Test]
        public void Attempt_Refused_OnlyByTheHostsRejoinRefusal()
        {
            Assert.AreEqual(MatchRejoin.AttemptOutcome.Refused,
                MatchRejoin.ClassifyAttempt(true, 50, 100, MatchRejoin.RefusalReasonPrefix + " the match has no seat to give back"));
            Assert.AreEqual(MatchRejoin.AttemptOutcome.Failed, MatchRejoin.ClassifyAttempt(true, 50, 100, "Host disconnected"));
            Assert.AreEqual(MatchRejoin.AttemptOutcome.Failed, MatchRejoin.ClassifyAttempt(true, 50, 100, null),
                "a link that dropped during the hand-back: the seat may still be held");
            Assert.AreEqual(MatchRejoin.AttemptOutcome.Failed, MatchRejoin.ClassifyAttempt(false, 0, 100, ""));
        }

        [Test]
        public void FailedAttempt_KeepsTheRecord_MarkedDropped_UntilTheLimit()
        {
            MatchRejoin.Remember("sess-1", GameModes.Random);
            for (int i = 1; i < MatchRejoin.MaxFailedAttempts; i++)
            {
                MatchRejoin.RecordAttemptOutcome(MatchRejoin.AttemptOutcome.Failed);
                Assert.IsTrue(MatchRejoin.TryLoad(out var r), $"still offered after {i} failed attempt(s)");
                Assert.AreEqual(i, r.failedAttempts);
                Assert.IsTrue(r.Dropped, "the window says \"You dropped out\" and offers REJOIN");
                Assert.AreEqual("sess-1", r.sessionId);
            }
            MatchRejoin.RecordAttemptOutcome(MatchRejoin.AttemptOutcome.Failed);
            Assert.IsFalse(MatchRejoin.TryLoad(out _), "the last allowed attempt failed: no longer offered");
        }

        [Test]
        public void RefusedAttempt_ForgetsTheMatch()
        {
            MatchRejoin.Remember("sess-1", GameModes.Random);
            MatchRejoin.RecordAttemptOutcome(MatchRejoin.AttemptOutcome.Refused);
            Assert.IsFalse(MatchRejoin.TryLoad(out _));
        }

        [Test]
        public void LandedAttempt_LeavesTheFreshRecord_AndRememberResetsTheCount()
        {
            MatchRejoin.Remember("sess-1", GameModes.Random);
            MatchRejoin.RecordAttemptOutcome(MatchRejoin.AttemptOutcome.Failed);
            MatchRejoin.Remember("sess-1", GameModes.Random);   // the claim re-remembers on a seat handed back
            MatchRejoin.RecordAttemptOutcome(MatchRejoin.AttemptOutcome.Landed);
            Assert.IsTrue(MatchRejoin.TryLoad(out var r));
            Assert.AreEqual(0, r.failedAttempts);
            Assert.IsFalse(r.Dropped);
        }

        [Test]
        public void WithFailedAttempt_IsPure()
        {
            var r = new MatchRejoin.Record { sessionId = "s", failedAttempts = 1 };
            var now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
            var after = MatchRejoin.WithFailedAttempt(r, now);
            Assert.AreEqual(2, after.failedAttempts);
            Assert.AreEqual(now.Ticks, after.droppedAtUtcTicks);
            Assert.AreEqual(1, r.failedAttempts, "the struct passed in is not changed");
        }
    }
}
#endif
