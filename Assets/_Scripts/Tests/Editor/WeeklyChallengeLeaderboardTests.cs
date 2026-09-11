#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The weekly leaderboard's two hand-rolled pieces, both of which fail SILENTLY: a metadata
    /// scan that returns a plausible wrong number shows the wrong player's face, and a countdown
    /// that rolls over shows a week as an hour. Neither throws, so neither is caught by running it.
    /// </summary>
    public class WeeklyChallengeLeaderboardTests
    {
        // ── Avatar metadata ────────────────────────────────────────────────────

        [Test]
        public void Metadata_ReadsTheAvatarId()
        {
            Assert.AreEqual(3, WeeklyChallengeRanking.ReadAvatarIdFromMetadata("{\"a\":3}"));
            Assert.AreEqual(0, WeeklyChallengeRanking.ReadAvatarIdFromMetadata("{\"a\":0}"),
                "0 is a REAL icon id and must not be confused with 'no avatar'.");
            Assert.AreEqual(12, WeeklyChallengeRanking.ReadAvatarIdFromMetadata("{ \"a\" : 12 }"));
            Assert.AreEqual(7, WeeklyChallengeRanking.ReadAvatarIdFromMetadata("{\"a\":\"7\"}"),
                "A serializer that quotes the number must still be readable.");
        }

        [Test]
        public void Metadata_ReadsPastOtherFields()
        {
            Assert.AreEqual(5, WeeklyChallengeRanking.ReadAvatarIdFromMetadata("{\"v\":2,\"a\":5}"));
            Assert.AreEqual(5, WeeklyChallengeRanking.ReadAvatarIdFromMetadata("{\"a\":5,\"v\":2}"));
        }

        [Test]
        public void Metadata_FallsBackToNoAvatar_RatherThanGuessing()
        {
            // Every one of these is a row that predates avatars, or a payload we do not understand.
            // The right answer is always "this row told us nothing", never a number.
            foreach (string payload in new[]
            {
                null, "", "{}", "{\"v\":2}", "{\"a\":}", "{\"a\":-1}", "{\"a\":null}",
                "{\"a\"", "{\"a\":", "not json at all",
            })
            {
                Assert.AreEqual(WeeklyChallengeRanking.NoAvatar,
                    WeeklyChallengeRanking.ReadAvatarIdFromMetadata(payload),
                    $"'{payload ?? "null"}' should read as no avatar.");
            }
        }

        [Test]
        public void Metadata_DoesNotMatchAKeyThatMerelyCONTAINSTheAvatarKey()
        {
            // "area" contains "a" - but "\"a\"" does not appear in it, which is exactly why the
            // scan looks for the QUOTED key rather than the bare letter.
            Assert.AreEqual(WeeklyChallengeRanking.NoAvatar,
                WeeklyChallengeRanking.ReadAvatarIdFromMetadata("{\"area\":9}"));
        }

        [Test]
        public void NoAvatar_IsNegative_SoZeroStaysARealIcon()
        {
            Assert.Less(WeeklyChallengeRanking.NoAvatar, 0,
                "A sentinel of 0 would silently show icon 0 for every row that carries no avatar.");

            Assert.IsFalse(new WeeklyChallengeRanking { AvatarId = WeeklyChallengeRanking.NoAvatar }.HasAvatar);
            Assert.IsTrue(new WeeklyChallengeRanking { AvatarId = 0 }.HasAvatar);
        }

        // ── The countdown clock ────────────────────────────────────────────────

        [Test]
        public void Countdown_IsHoursMinutesSeconds()
        {
            Assert.AreEqual("00:00:00",
                WeeklyChallengeLeaderboardModal.FormatHoursMinutesSeconds(TimeSpan.Zero));
            Assert.AreEqual("01:02:03",
                WeeklyChallengeLeaderboardModal.FormatHoursMinutesSeconds(new TimeSpan(1, 2, 3)));
            Assert.AreEqual("12:28:36",
                WeeklyChallengeLeaderboardModal.FormatHoursMinutesSeconds(new TimeSpan(12, 28, 36)));
        }

        [Test]
        public void Countdown_LetsHoursRunPastTwentyFour_RatherThanRollingOver()
        {
            // A week is up to 168 hours. Rolling over would print the top of the week as
            // "23:59:59" - a countdown that lies about the day.
            Assert.AreEqual("167:59:59", WeeklyChallengeLeaderboardModal.FormatHoursMinutesSeconds(
                new TimeSpan(6, 23, 59, 59)));
            Assert.AreEqual("24:00:00", WeeklyChallengeLeaderboardModal.FormatHoursMinutesSeconds(
                TimeSpan.FromHours(24)));
        }

        [Test]
        public void Countdown_ClampsAtZero()
        {
            Assert.AreEqual("00:00:00", WeeklyChallengeLeaderboardModal.FormatHoursMinutesSeconds(
                TimeSpan.FromSeconds(-5)));
        }

        [Test]
        public void Countdown_KeepsAConstantWidthWithinAnHour()
        {
            // A proportional font makes a label that changes width jitter every second.
            int width = WeeklyChallengeLeaderboardModal
                .FormatHoursMinutesSeconds(new TimeSpan(1, 0, 0)).Length;

            for (int s = 0; s < 3600; s += 137)
                Assert.AreEqual(width, WeeklyChallengeLeaderboardModal
                    .FormatHoursMinutesSeconds(TimeSpan.FromSeconds(3600 + s)).Length);
        }

        // ── Regional boards ────────────────────────────────────────────────────

        [Test]
        public void RegionalBoard_LookupIsCaseInsensitiveAndSkipsParkedRows()
        {
            var catalog = UnityEngine.ScriptableObject.CreateInstance<WeeklyChallengeCatalogSO>();
            try
            {
                catalog.regionalLeaderboards.Add(
                    new WeeklyChallengeCatalogSO.RegionalBoard { regionKey = "sg", leaderboardId = "" });
                catalog.regionalLeaderboards.Add(
                    new WeeklyChallengeCatalogSO.RegionalBoard { regionKey = "US", leaderboardId = "wc_us" });
                catalog.regionalLeaderboards.Add(
                    new WeeklyChallengeCatalogSO.RegionalBoard { regionKey = "gb", leaderboardId = "wc_eu" });

                Assert.AreEqual("wc_us", catalog.RegionalLeaderboardId("us"));
                Assert.AreEqual("wc_us", catalog.RegionalLeaderboardId("US"));
                Assert.AreEqual("wc_eu", catalog.RegionalLeaderboardId("gb"));

                Assert.IsNull(catalog.RegionalLeaderboardId("sg"),
                    "A row with no id is PARKED, not a board - the tab must report no board.");
                Assert.IsNull(catalog.RegionalLeaderboardId("jp"));
                Assert.IsNull(catalog.RegionalLeaderboardId(null));
                Assert.IsNull(catalog.RegionalLeaderboardId(""));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        // ── Period metadata (the week a row belongs to) ────────────────────────

        [Test]
        public void Metadata_ReadsThePeriodKey()
        {
            Assert.AreEqual("2026-09-07",
                WeeklyChallengeRanking.ReadPeriodKeyFromMetadata("{\"w\":\"2026-09-07\"}"));
            Assert.AreEqual("2026-09-07",
                WeeklyChallengeRanking.ReadPeriodKeyFromMetadata("{ \"w\" : \"2026-09-07\" }"));
            Assert.AreEqual("T42",
                WeeklyChallengeRanking.ReadPeriodKeyFromMetadata("{\"w\":\"T42\"}"),
                "A shrunken test period is a legitimate key and a DIFFERENT shape from a week.");
            Assert.AreEqual("2026-09-07",
                WeeklyChallengeRanking.ReadPeriodKeyFromMetadata("{\"w\":2026-09-07}"),
                "Not a shape this game writes, but an unquoted token is still unambiguous.");
        }

        [Test]
        public void Metadata_ReadsBothFieldsWhicheverOrderTheyArrive()
        {
            // The two fields are independent: an entry may carry a period and no avatar, and a
            // pre-stamp entry carries an avatar and no period.
            const string both = "{\"w\":\"2026-09-07\",\"a\":5}";
            const string swapped = "{\"a\":5,\"w\":\"2026-09-07\"}";

            Assert.AreEqual("2026-09-07", WeeklyChallengeRanking.ReadPeriodKeyFromMetadata(both));
            Assert.AreEqual(5, WeeklyChallengeRanking.ReadAvatarIdFromMetadata(both));
            Assert.AreEqual("2026-09-07", WeeklyChallengeRanking.ReadPeriodKeyFromMetadata(swapped));
            Assert.AreEqual(5, WeeklyChallengeRanking.ReadAvatarIdFromMetadata(swapped));

            Assert.AreEqual(WeeklyChallengeRanking.NoAvatar,
                WeeklyChallengeRanking.ReadAvatarIdFromMetadata("{\"w\":\"2026-09-07\"}"),
                "A period with no avatar must not resolve to an icon.");
            Assert.IsNull(WeeklyChallengeRanking.ReadPeriodKeyFromMetadata("{\"a\":5}"),
                "An avatar with no period is every entry submitted before the stamp shipped.");
        }

        [Test]
        public void Metadata_PeriodKey_FallsBackToNull_RatherThanGuessing()
        {
            foreach (string payload in new[]
                     {
                         null, "", "{}", "{\"w\"", "{\"w\":", "{\"w\":\"\"}", "{\"w\":\"2026-09",
                         "{\"week\":\"2026-09-07\"}", "{\"ww\":\"2026-09-07\"}",
                     })
            {
                Assert.IsNull(WeeklyChallengeRanking.ReadPeriodKeyFromMetadata(payload),
                    $"'{payload}' does not state a period, so it must not appear to.");
            }
        }

        [Test]
        public void Metadata_TheTwoKeysAreDistinct()
        {
            // One scan serves both fields, so a shared key would make each read the other's value.
            Assert.AreNotEqual(WeeklyChallengeRanking.AvatarMetadataKey,
                WeeklyChallengeRanking.PeriodMetadataKey);
        }

        // ── The period filter (what makes a stale board un-showable) ───────────

        [Test]
        public void IsForPeriod_AnUnstampedRowIsNotThisPeriod()
        {
            // The load-bearing direction. An entry that does not say which week it is from is
            // exactly the entry a board that failed to reset is full of, so "unknown" must read as
            // "not this week" - the opposite choice leaves the bug in place.
            Assert.IsTrue(WeeklyChallengeRanking.IsForPeriod("2026-09-07", "2026-09-07"));
            Assert.IsFalse(WeeklyChallengeRanking.IsForPeriod("2026-08-31", "2026-09-07"));
            Assert.IsFalse(WeeklyChallengeRanking.IsForPeriod(null, "2026-09-07"));
            Assert.IsFalse(WeeklyChallengeRanking.IsForPeriod("", "2026-09-07"));
        }

        [Test]
        public void IsForPeriod_AnUnknownCURRENTPeriodKeepsEverything()
        {
            // The other direction is the opposite call: with nothing to judge against, emptying
            // the board would be a worse answer than showing it.
            Assert.IsTrue(WeeklyChallengeRanking.IsForPeriod("2026-08-31", null));
            Assert.IsTrue(WeeklyChallengeRanking.IsForPeriod("2026-08-31", ""));
            Assert.IsTrue(WeeklyChallengeRanking.IsForPeriod(null, null));
        }

        [Test]
        public void RetainPeriod_DropsOtherWeeksAndRenumbersWhatIsLeft()
        {
            // A board that never reset, sorted ascending: last week's fastest run outranks every
            // run of this week's challenge, forever.
            var rows = new List<WeeklyChallengeRanking>
            {
                Row("2026-08-31", rank: 1, seconds: 10d),
                Row("2026-09-07", rank: 2, seconds: 40d),
                Row(null,         rank: 3, seconds: 41d),
                Row("2026-09-07", rank: 4, seconds: 55d),
            };

            Assert.AreEqual(2, WeeklyChallengeRanking.RetainPeriod(rows, "2026-09-07"));
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(40d, rows[0].Seconds);
            Assert.AreEqual(55d, rows[1].Seconds, "Board order is preserved; only the holes go.");
            Assert.AreEqual(1, rows[0].Rank, "1st, 4th, 812th reads as a board with rows missing.");
            Assert.AreEqual(2, rows[1].Rank);
        }

        [Test]
        public void RetainPeriod_LeavesTrueRanksAloneWhenNothingIsDropped()
        {
            // The healthy case, and the reason renumbering is conditional: a board that reset
            // correctly has nothing to drop, and its rows are genuinely 7th and 9th in the world.
            var rows = new List<WeeklyChallengeRanking>
            {
                Row("2026-09-07", rank: 7, seconds: 12d),
                Row("2026-09-07", rank: 9, seconds: 13d),
            };

            Assert.AreEqual(0, WeeklyChallengeRanking.RetainPeriod(rows, "2026-09-07"));
            Assert.AreEqual(7, rows[0].Rank);
            Assert.AreEqual(9, rows[1].Rank);
        }

        [Test]
        public void RetainPeriod_WithNoCurrentPeriod_ChangesNothing()
        {
            var rows = new List<WeeklyChallengeRanking>
            {
                Row("2026-08-31", rank: 1, seconds: 1d),
                Row(null,         rank: 2, seconds: 2d),
            };

            Assert.AreEqual(0, WeeklyChallengeRanking.RetainPeriod(rows, string.Empty));
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(1, rows[0].Rank);
        }

        [Test]
        public void RetainPeriod_CanEmptyTheListEntirely()
        {
            // An empty panel is the honest reading of a board with none of this week's runs on it.
            var rows = new List<WeeklyChallengeRanking>
            {
                Row("2026-08-31", rank: 1, seconds: 1d),
                Row("2026-08-24", rank: 2, seconds: 2d),
            };

            Assert.AreEqual(2, WeeklyChallengeRanking.RetainPeriod(rows, "2026-09-07"));
            Assert.IsEmpty(rows);

            Assert.AreEqual(0, WeeklyChallengeRanking.RetainPeriod(null, "2026-09-07"));
            Assert.AreEqual(0,
                WeeklyChallengeRanking.RetainPeriod(new List<WeeklyChallengeRanking>(), "2026-09-07"));
        }

        [Test]
        public void Renumber_RanksByPOSITION_SoTiedUgsRanksStillCome1ToN()
        {
            // UGS can hand two players the same rank. Once anything has been removed, position is
            // the only basis that still produces 1..n - and both read paths go through this one
            // call so they cannot disagree about that.
            var rows = new List<WeeklyChallengeRanking>
            {
                Row("2026-09-07", rank: 4, seconds: 20d),
                Row("2026-09-07", rank: 4, seconds: 20d),
                Row("2026-09-07", rank: 6, seconds: 30d),
            };

            WeeklyChallengeRanking.Renumber(rows);

            Assert.AreEqual(1, rows[0].Rank);
            Assert.AreEqual(2, rows[1].Rank);
            Assert.AreEqual(3, rows[2].Rank);

            Assert.DoesNotThrow(() => WeeklyChallengeRanking.Renumber(null));
        }

        static WeeklyChallengeRanking Row(string periodKey, int rank, double seconds) =>
            new WeeklyChallengeRanking
            {
                PeriodKey = periodKey,
                Rank = rank,
                Seconds = seconds,
                AvatarId = WeeklyChallengeRanking.NoAvatar,
            };

        [Test]
        public void ScopeValues_ArePinned()
        {
            // A scope can be persisted as "the tab you had open".
            Assert.AreEqual(0, (int)LeaderboardScope.World);
            Assert.AreEqual(1, (int)LeaderboardScope.Regional);
            Assert.AreEqual(2, (int)LeaderboardScope.Friends);
        }
    }
}
#endif
