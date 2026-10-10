#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// RecentPlayersStore tests - the "recently played with" list behind the friends panel's
    /// RECENT section.
    ///
    /// WHY THIS MATTERS:
    /// The list is the only path from a good match with a stranger to a friend request, and it is
    /// written once per match end from the live roster, which a single-machine play test cannot
    /// exercise in every shape: a roster that carries AI seats (no UGS id, flagged), the local
    /// pilot's own seat (and a stale second object for the same account), a pilot met twice, and a
    /// list that has hit its cap. Each of those must come out right without a Unity player object
    /// or a disk, so the pure list-shaping functions are asserted here.
    /// </summary>
    [TestFixture]
    public class RecentPlayersStoreTests
    {
        static readonly DateTime T0 = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        static MatchParticipant Human(string id, string name = null, int avatar = 0) =>
            new(id, name ?? id, avatar, isAI: false, isLocal: false);

        static MatchParticipant Local(string id) => new(id, "me", 1, isAI: false, isLocal: true);

        static MatchParticipant AI(string name, string id = "") => new(id, name, 0, isAI: true, isLocal: false);

        static List<string> Ids(List<RecentPlayerRecord> records)
        {
            var ids = new List<string>();
            foreach (var r in records) ids.Add(r.PlayerId);
            return ids;
        }

        #region SelectOthers: who counts

        [Test]
        public void SelectOthers_KeepsOnlyRemoteHumansWithAnId()
        {
            var seats = new List<MatchParticipant>
            {
                Local("me-id"),
                Human("a"),
                AI("Bot 1"),
                AI("Bot 2", "bot-with-id"),
                Human(""),          // a human seat with no id yet is not an identity we can befriend
                Human("b"),
            };

            var others = RecentPlayersStore.SelectOthers(seats);

            Assert.AreEqual(new[] { "a", "b" }, others.ConvertAll(o => o.PlayerId).ToArray());
        }

        [Test]
        public void SelectOthers_DropsTheLocalPilotByIdToo()
        {
            // A stale pre-party Player object for the local account sits in the roster as a
            // remote-looking seat with the local id: it must not record "you played with yourself".
            var seats = new List<MatchParticipant> { Local("me-id"), Human("me-id", "me again"), Human("a") };

            var others = RecentPlayersStore.SelectOthers(seats);

            Assert.AreEqual(new[] { "a" }, others.ConvertAll(o => o.PlayerId).ToArray());
        }

        [Test]
        public void SelectOthers_KeepsAPilotSeatedTwiceOnce()
        {
            var seats = new List<MatchParticipant> { Human("a"), Human("a"), Human("b") };

            var others = RecentPlayersStore.SelectOthers(seats);

            Assert.AreEqual(new[] { "a", "b" }, others.ConvertAll(o => o.PlayerId).ToArray());
        }

        [Test]
        public void SelectOthers_SoloOrAiOnlyMatch_RecordsNobody()
        {
            Assert.IsEmpty(RecentPlayersStore.SelectOthers(new[] { Local("me-id") }));
            Assert.IsEmpty(RecentPlayersStore.SelectOthers(new[] { Local("me-id"), AI("Bot"), AI("Bot 2") }));
            Assert.IsEmpty(RecentPlayersStore.SelectOthers(null));
        }

        #endregion

        #region Merge: order, de-dup, cap

        [Test]
        public void Merge_NewestFirst_InRosterOrder()
        {
            var records = new List<RecentPlayerRecord>();
            RecentPlayersStore.Merge(records, new[] { Human("old") }, T0, 20);

            int touched = RecentPlayersStore.Merge(records, new[] { Human("a"), Human("b") }, T0.AddMinutes(5), 20);

            Assert.AreEqual(2, touched);
            Assert.AreEqual(new[] { "a", "b", "old" }, Ids(records).ToArray());
            Assert.AreEqual(T0.AddMinutes(5), records[0].GetLastPlayedUtc());
            Assert.AreEqual(T0, records[2].GetLastPlayedUtc());
        }

        [Test]
        public void Merge_DeDuplicatesByPlayerId_MovingThePilotToTheFrontWithFreshFacts()
        {
            var records = new List<RecentPlayerRecord>();
            RecentPlayersStore.Merge(records, new[] { Human("a", "Alpha", 1), Human("b", "Bravo", 2) }, T0, 20);

            RecentPlayersStore.Merge(records, new[] { Human("b", "Bravo renamed", 7) }, T0.AddHours(1), 20);

            Assert.AreEqual(2, records.Count, "a pilot met twice holds one slot");
            Assert.AreEqual("b", records[0].PlayerId);
            Assert.AreEqual("Bravo renamed", records[0].DisplayName);
            Assert.AreEqual(7, records[0].AvatarId);
            Assert.AreEqual(T0.AddHours(1), records[0].GetLastPlayedUtc());
            Assert.AreEqual("a", records[1].PlayerId);
        }

        [Test]
        public void Merge_CapsAtMaxEntries_DroppingTheOldest()
        {
            var records = new List<RecentPlayerRecord>();
            for (int i = 0; i < 25; i++)
                RecentPlayersStore.Merge(records, new[] { Human($"p{i}") }, T0.AddMinutes(i), 20);

            Assert.AreEqual(20, records.Count);
            Assert.AreEqual("p24", records[0].PlayerId, "newest at the front");
            Assert.AreEqual("p5", records[19].PlayerId, "p0..p4 fell off the end");
        }

        [Test]
        public void Merge_OneMatchLargerThanTheCap_KeepsTheRosterHeadAndReportsWhatFit()
        {
            var records = new List<RecentPlayerRecord>();
            var roster = new List<MatchParticipant>();
            for (int i = 0; i < 6; i++) roster.Add(Human($"p{i}"));

            int touched = RecentPlayersStore.Merge(records, roster, T0, 4);

            Assert.AreEqual(4, touched);
            Assert.AreEqual(new[] { "p0", "p1", "p2", "p3" }, Ids(records).ToArray());
        }

        [Test]
        public void Merge_EmptyNameFallsBackToUnknownPilot()
        {
            var records = new List<RecentPlayerRecord>();
            RecentPlayersStore.Merge(records, new[] { Human("a", "") }, T0, 20);

            Assert.AreEqual("Unknown Pilot", records[0].DisplayName);
        }

        #endregion

        #region FormatLastPlayed

        [TestCase(0, "JUST NOW")]
        [TestCase(59, "JUST NOW")]
        [TestCase(60, "1 MIN AGO")]
        [TestCase(5 * 60, "5 MIN AGO")]
        [TestCase(3 * 3600, "3 HR AGO")]
        [TestCase(24 * 3600, "1 DAY AGO")]
        [TestCase(2 * 24 * 3600, "2 DAYS AGO")]
        [TestCase(7 * 24 * 3600, "1 WEEK AGO")]
        [TestCase(21 * 24 * 3600, "3 WEEKS AGO")]
        public void FormatLastPlayed_ReadsAsTheRowLabel(int secondsAgo, string expected)
        {
            Assert.AreEqual(expected, RecentPlayersStore.FormatLastPlayed(T0.AddSeconds(-secondsAgo), T0));
        }

        [Test]
        public void FormatLastPlayed_AFutureStampReadsAsJustNow()
        {
            // A clock that went backwards must not print a negative age.
            Assert.AreEqual("JUST NOW", RecentPlayersStore.FormatLastPlayed(T0.AddMinutes(10), T0));
        }

        #endregion

        #region Config

        [Test]
        public void Config_DefaultsAreTheShippedNumbers()
        {
            var config = ScriptableObject.CreateInstance<RecentPlayersConfigSO>();
            try
            {
                Assert.AreEqual(20, config.MaxEntries);
                Assert.AreEqual("recent_players.data", config.SaveFileName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        #endregion
    }
}
#endif
