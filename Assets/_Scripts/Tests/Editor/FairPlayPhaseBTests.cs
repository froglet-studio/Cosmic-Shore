#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Fair play phase B on the client: the leaderboard splice that keeps an Undertow account's own view of the
    /// weekly board normal, the pool suffix the server sends, and the notice's authored words.
    /// </summary>
    public class FairPlayPhaseBTests
    {
        static WeeklyChallengeRanking Row(int rank, double seconds, string id) =>
            new WeeklyChallengeRanking { Rank = rank, Seconds = seconds, PlayerId = id };

        static List<WeeklyChallengeRanking> Page() => new() { Row(1, 60, "a"), Row(2, 70, "b"), Row(3, 80, "c") };

        [Test]
        public void OwnRow_LandsWhereItsTimeFalls_AndPushesTheRestDown()
        {
            var rows = Page();
            WeeklyChallengeRanking.SpliceOwnRow(rows, Row(0, 65, "me"), 10);
            Assert.AreEqual(new[] { "a", "me", "b", "c" }, rows.ConvertAll(r => r.PlayerId));
            Assert.AreEqual(new[] { 1, 2, 3, 4 }, rows.ConvertAll(r => r.Rank));
            Assert.IsTrue(rows[1].IsLocalPlayer);
        }

        [Test]
        public void OwnRow_SlowerThanAFullPage_IsLeftOff_AsOnThePublicBoard()
        {
            var rows = Page();
            WeeklyChallengeRanking.SpliceOwnRow(rows, Row(0, 99, "me"), 3);
            Assert.AreEqual(new[] { "a", "b", "c" }, rows.ConvertAll(r => r.PlayerId));
        }

        [Test]
        public void OwnRow_AtTheEnd_TakesTheNextRank_AndThePageStaysItsSize()
        {
            var rows = Page();
            WeeklyChallengeRanking.SpliceOwnRow(rows, Row(0, 99, "me"), 5);
            Assert.AreEqual(4, rows[3].Rank);
            var full = Page();
            WeeklyChallengeRanking.SpliceOwnRow(full, Row(0, 50, "me"), 3);
            Assert.AreEqual(3, full.Count);
            Assert.AreEqual("me", full[0].PlayerId);
            Assert.AreEqual(1, full[0].Rank);
        }

        [Test]
        public void OwnRow_ReplacesAnyRowAlreadyMarkedLocal()
        {
            var rows = Page();
            var stale = rows[2]; stale.IsLocalPlayer = true; rows[2] = stale;
            WeeklyChallengeRanking.SpliceOwnRow(rows, Row(0, 75, "me"), 10);
            Assert.AreEqual(1, rows.FindAll(r => r.IsLocalPlayer).Count);
        }

        [Test]
        public void PoolSuffix_OnlyAShortTokenIsTrusted()
        {
            Assert.AreEqual("_u1", FairPlayTrust.Sanitize("_u1"));
            Assert.AreEqual("", FairPlayTrust.Sanitize(null));
            Assert.AreEqual("", FairPlayTrust.Sanitize("_u1; DROP"));
            Assert.AreEqual("", FairPlayTrust.Sanitize(new string('x', 40)));
            Assert.AreEqual("PRESENCE_LOBBY", FairPlayTrust.PresenceGameMode("PRESENCE_LOBBY"), "the shore keeps the lobby every old client joins");
        }

        [Test]
        public void ShippedNotice_HasEveryKindAndNamesTheRule()
        {
            var words = Resources.Load<FairPlayNoticeConfigSO>(FairPlayNoticeConfigSO.ResourcePath);
            Assert.IsNotNull(words, "Resources/FairPlayNoticeConfig is missing");
            foreach (var kind in new[] { "cheating", "automated", "quitting" })
            {
                Assert.IsNotEmpty(words.TitleFor(kind), kind);
                StringAssert.Contains("{rule}", words.BodyFor(kind), kind);
            }
            StringAssert.Contains("{until}", words.RestrictedLine);
            Assert.IsNotEmpty(words.PaidContentLine, "a notice always says paid content is untouched (R14)");
        }

        [Test]
        public void TrustGate_ShipsOff()
        {
            Assert.IsFalse(FairPlayTrustGate.Enabled);
            Assert.IsFalse(FairPlayTrustGate.RemoveUnverifiedGuests, "a guest without a pass is not removed until every build carries one");
        }

        [Test]
        public void PoolPass_IsExactly32LowercaseHexDigits()
        {
            Assert.IsTrue(FairPlayTrust.IsPass("0123456789abcdef0123456789abcdef"));
            Assert.IsFalse(FairPlayTrust.IsPass(null));
            Assert.IsFalse(FairPlayTrust.IsPass("0123456789ABCDEF0123456789ABCDEF"));
            Assert.IsFalse(FairPlayTrust.IsPass("0123456789abcdef"));
            Assert.IsFalse(FairPlayTrust.IsPass("0123456789abcdef0123456789abcdeg"));
        }
    }
}
#endif
