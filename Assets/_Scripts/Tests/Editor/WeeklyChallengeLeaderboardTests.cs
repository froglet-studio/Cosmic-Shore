#if UNITY_EDITOR
using System;
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
