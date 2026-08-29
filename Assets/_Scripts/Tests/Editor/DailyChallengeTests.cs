#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The daily challenge's pure halves: the DRAW (a function of the UTC date over the catalog)
    /// and the RECORD (the cloud model's rollover and attempt folding).
    ///
    /// WHY THIS MATTERS: the whole feature rests on one promise - every player on a given UTC date
    /// faces the same challenge, and yesterday's progress never bleeds into today. Both are pure
    /// functions, so both are testable without a play-mode session; the service that ties them
    /// together is a thin MonoBehaviour over exactly these two pieces.
    /// </summary>
    [TestFixture]
    public class DailyChallengeTests
    {
        DailyChallengeCatalogSO _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<DailyChallengeCatalogSO>();
            _catalog.Pool = new List<DailyChallengeCatalogSO.Entry>
            {
                new() { Mode = GameModes.MultiplayerCrystalCapture, Metric = ScoringMetric.Crystals,
                        Target = 15, TimeLimitSeconds = 60f, Intensity = 1, Verb = "Collect", Noun = "crystals" },
                new() { Mode = GameModes.MultiplayerJoust, Metric = ScoringMetric.Jousts,
                        Target = 1, TimeLimitSeconds = 60f, Intensity = 1, Verb = "Land", Noun = "joust" },
                new() { Mode = GameModes.Rampage, Metric = ScoringMetric.PrismsDestroyed,
                        Target = 400, TimeLimitSeconds = 90f, Intensity = 1, Verb = "Destroy", Noun = "prisms" },
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null) UnityEngine.Object.DestroyImmediate(_catalog);
        }

        // ── The draw ───────────────────────────────────────────────────────────

        [Test]
        public void ForDate_IsStableAcrossCalls()
        {
            var date = new DateTime(2026, 8, 29, 13, 45, 0, DateTimeKind.Utc);

            var a = _catalog.ForDate(date);
            var b = _catalog.ForDate(date);

            Assert.AreEqual(a.GameMode, b.GameMode);
            Assert.AreEqual(a.TargetValue, b.TargetValue);
            Assert.AreEqual(a.DateKey, b.DateKey);
        }

        [Test]
        public void ForDate_IgnoresTimeOfDay()
        {
            // Two instants on the same UTC day must be the same challenge - otherwise a player
            // who launched at 23:59 and finished at 00:01 would have been scored against a
            // challenge that no longer existed by the time it was recorded.
            var morning = new DateTime(2026, 8, 29, 0, 0, 1, DateTimeKind.Utc);
            var night = new DateTime(2026, 8, 29, 23, 59, 59, DateTimeKind.Utc);

            Assert.AreEqual(_catalog.ForDate(morning).GameMode, _catalog.ForDate(night).GameMode);
            Assert.AreEqual(_catalog.ForDate(morning).DateKey, _catalog.ForDate(night).DateKey);
        }

        [Test]
        public void ForDate_ConvertsLocalInstantsToUtcBeforeDrawing()
        {
            // A DateTimeKind.Local instant must land on the UTC day, not the local one - the
            // draw is a promise about a shared calendar, so the timezone of the machine asking
            // must not be able to change the answer.
            var utc = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
            var local = utc.ToLocalTime();

            Assert.AreEqual(_catalog.ForDate(utc).DateKey, _catalog.ForDate(local).DateKey);
        }

        [Test]
        public void ForDate_VariesAcrossTheWeek()
        {
            // Not a distribution test - just proof the date actually reaches the index. A draw
            // that returned the same mode every day would pass every other test here.
            var seen = new HashSet<GameModes>();
            for (int i = 0; i < 60; i++)
                seen.Add(_catalog.ForDate(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)).GameMode);

            Assert.Greater(seen.Count, 1, "The date is not reaching the pool index.");
        }

        [Test]
        public void ForDate_EmptyPool_ReturnsInvalidChallenge()
        {
            _catalog.Pool = new List<DailyChallengeCatalogSO.Entry>();
            Assert.IsFalse(_catalog.ForDate(DateTime.UtcNow).IsValid);
        }

        [Test]
        public void ForDate_SkipsEntriesWithNoTarget()
        {
            _catalog.Pool = new List<DailyChallengeCatalogSO.Entry>
            {
                new() { Mode = GameModes.Rampage, Target = 0 },
            };

            Assert.IsFalse(_catalog.ForDate(DateTime.UtcNow).IsValid);
        }

        [Test]
        public void ForDate_ProgressionFilter_OnlyAppliesWhenOptedIn()
        {
            _catalog.respectModeProgression = false;
            var unfiltered = _catalog.ForDate(new DateTime(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc), _ => false);
            Assert.IsTrue(unfiltered.IsValid, "The filter must be ignored while respectModeProgression is off.");

            _catalog.respectModeProgression = true;
            var filtered = _catalog.ForDate(new DateTime(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc), _ => false);
            Assert.IsFalse(filtered.IsValid);
        }

        [Test]
        public void HashDateKey_IsPlatformIndependentFnv1a()
        {
            // Pinned values. System.Random is deterministic only within one runtime's
            // implementation, which is not a promise two clients on two platforms can hold each
            // other to - these constants are what makes "the same challenge for everyone" real.
            Assert.AreEqual(2166136261u, DailyChallengeCatalogSO.HashDateKey(""),
                "Empty input must be the FNV-1a offset basis.");
            Assert.AreEqual(DailyChallengeCatalogSO.HashDateKey("2026-08-29"),
                            DailyChallengeCatalogSO.HashDateKey("2026-08-29"));
            Assert.AreNotEqual(DailyChallengeCatalogSO.HashDateKey("2026-08-29"),
                               DailyChallengeCatalogSO.HashDateKey("2026-08-30"));
        }

        [Test]
        public void NextRolloverUtc_IsTheFollowingMidnight()
        {
            var noon = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc),
                            DailyChallengeCatalogSO.NextRolloverUtc(noon));
        }

        [Test]
        public void ObjectiveText_ReadsAsOneSentence()
        {
            var entry = new DailyChallengeCatalogSO.Entry
            {
                Target = 30, TimeLimitSeconds = 60f, Verb = "Collect", Noun = "crystals"
            };
            Assert.AreEqual("Collect 30 crystals in 1:00", DailyChallengeCatalogSO.BuildObjectiveText(entry));

            entry.TimeLimitSeconds = 0f;
            Assert.AreEqual("Collect 30 crystals", DailyChallengeCatalogSO.BuildObjectiveText(entry),
                "A challenge with no time budget must not claim one.");
        }

        [Test]
        public void FormatCountdown_ReadsAsHoursMinutesSeconds()
        {
            Assert.AreEqual("7:12:33", DailyChallengeCard.FormatCountdown(new TimeSpan(7, 12, 33)));
            Assert.AreEqual("0:00:00", DailyChallengeCard.FormatCountdown(TimeSpan.FromSeconds(-5)),
                "A countdown must never render negative.");
        }

        // ── The record ─────────────────────────────────────────────────────────

        [Test]
        public void CloudData_IsStale_ForAnEarlierDay()
        {
            var data = new DailyChallengeCloudData { ChallengeDate = "2026-08-28" };
            Assert.IsTrue(data.IsStale("2026-08-29"));
            Assert.IsFalse(data.IsStale("2026-08-28"));
        }

        [Test]
        public void CloudData_ResetForNewDay_ClearsProgressButKeepsBankedTickets()
        {
            var data = new DailyChallengeCloudData
            {
                ChallengeDate = "2026-08-28",
                BestValue = 42,
                Completed = true,
                Attempts = 3,
                TicketBalance = 5,
                LastTicketIssuedDate = "2026-08-28",
            };

            data.ResetForNewDay("2026-08-29", "Rampage", 1, "PrismsDestroyed", 400, 3);

            Assert.AreEqual(0, data.BestValue);
            Assert.IsFalse(data.Completed);
            Assert.AreEqual(0, data.Attempts);
            Assert.AreEqual(400, data.TargetValue);
            Assert.AreEqual("Rampage", data.GameMode);
            Assert.AreEqual(5, data.TicketBalance,
                "Topping up must never take banked attempts away.");
        }

        [Test]
        public void CloudData_RecordAttempt_KeepsTheBestAndLatchesCompletion()
        {
            var now = new DateTime(2026, 8, 29, 10, 0, 0, DateTimeKind.Utc);
            var data = new DailyChallengeCloudData();
            data.ResetForNewDay("2026-08-29", "MultiplayerCrystalCapture", 1, "Crystals", 15, 0);

            data.RecordAttempt(9, 15, now);
            Assert.AreEqual(9, data.BestValue);
            Assert.IsFalse(data.Completed);

            // A worse run must not erase a better one.
            data.RecordAttempt(4, 15, now);
            Assert.AreEqual(9, data.BestValue);
            Assert.AreEqual(2, data.Attempts);

            data.RecordAttempt(15, 15, now);
            Assert.AreEqual(15, data.BestValue);
            Assert.IsTrue(data.Completed);
            Assert.Greater(data.CompletedAtUnixMs, 0);

            // Completion is a latch - a later worse attempt cannot un-complete the day.
            long stamp = data.CompletedAtUnixMs;
            data.RecordAttempt(1, 15, now.AddMinutes(5));
            Assert.IsTrue(data.Completed);
            Assert.AreEqual(stamp, data.CompletedAtUnixMs);
        }

        // ── The shipped asset ──────────────────────────────────────────────────

        [Test]
        public void ShippedCatalog_ResolvesAndNamesRealModes()
        {
            var shipped = Resources.Load<DailyChallengeCatalogSO>(DailyChallengeCatalogSO.ResourcePath);
            Assert.IsNotNull(shipped,
                $"Missing Resources/{DailyChallengeCatalogSO.ResourcePath} - the card cannot draw a challenge without it.");
            Assert.IsNotEmpty(shipped.Pool, "An empty pool leaves the card permanently UNAVAILABLE.");

            foreach (var entry in shipped.Pool)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(GameModes), entry.Mode),
                    $"Pool entry names an undefined GameModes value: {(int)entry.Mode}");
                Assert.IsTrue(Enum.IsDefined(typeof(ScoringMetric), entry.Metric),
                    $"Pool entry for {entry.Mode} names an undefined ScoringMetric.");
                Assert.Greater(entry.Target, 0, $"Pool entry for {entry.Mode} has no target.");
                Assert.GreaterOrEqual(entry.Intensity, 1);
                Assert.LessOrEqual(entry.Intensity, 4);
            }
        }

        [Test]
        public void ShippedCatalog_EveryModeHasAnArcadeCard()
        {
            // A pooled mode with no SO_ArcadeGame cannot be launched - SelectDailyChallenge would
            // warn and do nothing, which reads on screen as a dead card on whichever date drew it.
            var shipped = Resources.Load<DailyChallengeCatalogSO>(DailyChallengeCatalogSO.ResourcePath);
            if (shipped == null) Assert.Ignore("No shipped catalog.");

            var lists = Resources.FindObjectsOfTypeAll<SO_GameList>();
            if (lists == null || lists.Length == 0) Assert.Ignore("No SO_GameList loaded in this test session.");

            var modes = new HashSet<GameModes>();
            foreach (var list in lists)
            {
                if (list.Games == null) continue;
                foreach (var game in list.Games)
                    if (game) modes.Add(game.Mode);
            }

            foreach (var entry in shipped.Pool)
                Assert.IsTrue(modes.Contains(entry.Mode),
                    $"Daily challenge pool names {entry.Mode}, which has no card in any SO_GameList.");
        }
    }
}
#endif
