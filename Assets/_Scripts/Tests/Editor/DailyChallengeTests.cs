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
                        Target = 8, EndConditionOverride = 12, TimeLimitSeconds = 60f, Intensity = 1,
                        Verb = "Collect", Noun = "crystals" },
                new() { Mode = GameModes.MultiplayerJoust, Metric = ScoringMetric.Jousts,
                        Target = 1, EndConditionOverride = 2, TimeLimitSeconds = 60f, Intensity = 1,
                        Verb = "Land", Noun = "joust" },
                new() { Mode = GameModes.Rampage, Metric = ScoringMetric.PrismsDestroyed,
                        Target = 300, EndConditionOverride = 450, TimeLimitSeconds = 90f, Intensity = 1,
                        Verb = "Destroy", Noun = "prisms" },
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

        [Test]
        public void ForDate_DefaultsToJadeAndRejectsUnplayableDomains()
        {
            // Domains has no member at 0, which is exactly what an entry authored before the field
            // existed deserializes to - and Blue is the "no team" sentinel, never a colour anyone
            // flies. Both must resolve to Jade rather than reaching a spawn.
            Assert.AreEqual(Domains.Jade, DailyChallengeCatalogSO.ResolvePlayableDomain(default));
            Assert.AreEqual(Domains.Jade, DailyChallengeCatalogSO.ResolvePlayableDomain(Domains.Blue));
            Assert.AreEqual(Domains.Ruby, DailyChallengeCatalogSO.ResolvePlayableDomain(Domains.Ruby));
            Assert.AreEqual(Domains.Gold, DailyChallengeCatalogSO.ResolvePlayableDomain(Domains.Gold));

            _catalog.test.enabled = true;
            _catalog.test.forcedPoolIndex = 0;
            _catalog.Pool[0].Domain = Domains.Blue;

            Assert.AreEqual(Domains.Jade, _catalog.ForDate(DateTime.UtcNow).Domain);
        }

        [Test]
        public void ForDate_CarriesTheAuthoredDomain()
        {
            _catalog.test.enabled = true;
            _catalog.test.forcedPoolIndex = 1;
            _catalog.Pool[1].Domain = Domains.Gold;

            Assert.AreEqual(Domains.Gold, _catalog.ForDate(DateTime.UtcNow).Domain);
        }

        // ── The smaller game ───────────────────────────────────────────────────

        [Test]
        public void ForDate_CarriesTheRunsRaceTarget()
        {
            var challenge = _catalog.ForDate(new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc));
            Assert.IsTrue(challenge.IsValid);
            Assert.Greater(challenge.EndConditionValue, 0);
            Assert.GreaterOrEqual(challenge.EndConditionValue, challenge.TargetValue);
        }

        [Test]
        public void Entry_RaceTargetFallsBackToTheObjective()
        {
            var entry = new DailyChallengeCatalogSO.Entry { Target = 9, EndConditionOverride = 0 };
            Assert.AreEqual(9, entry.ResolvedEndCondition,
                "0 must mean 'the objective and the run end together', not 'no end condition'.");

            entry.EndConditionOverride = 14;
            Assert.AreEqual(14, entry.ResolvedEndCondition);
        }

        [Test]
        public void RunOverride_ShortensOnlyTheModeItNames()
        {
            var end = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
            try
            {
                end.joustCount = 3;
                end.rampagePrismTarget = 2000;

                EndConditionOverridesSO.SetRunOverride(GameModes.MultiplayerJoust, 2);

                Assert.AreEqual(2, end.GetJoustCount());
                Assert.AreEqual(2000, end.GetRampagePrismTarget(),
                    "An override for one mode must never shorten another.");

                EndConditionOverridesSO.ClearRunOverride();
                Assert.AreEqual(3, end.GetJoustCount(),
                    "Clearing must restore the authored target - a leaked override would " +
                    "silently shorten every later match.");
            }
            finally
            {
                EndConditionOverridesSO.ClearRunOverride();
                UnityEngine.Object.DestroyImmediate(end);
            }
        }

        [Test]
        public void RunOverride_NonPositiveTargetClearsInsteadOfPinningZero()
        {
            var end = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
            try
            {
                end.joustCount = 3;
                EndConditionOverridesSO.SetRunOverride(GameModes.MultiplayerJoust, 2);
                EndConditionOverridesSO.SetRunOverride(GameModes.MultiplayerJoust, 0);

                Assert.AreEqual(3, end.GetJoustCount(),
                    "A 0 target must clear, so a caller never has to branch to avoid a race to 0.");
            }
            finally
            {
                EndConditionOverridesSO.ClearRunOverride();
                UnityEngine.Object.DestroyImmediate(end);
            }
        }

        [Test]
        public void RunOverride_CannotReachAModeThatResolvesItsOwnTarget()
        {
            // Astro League's controller owns its goal target, so the override cannot shorten it.
            // The editor tool warns on this; the law is here so nobody "fixes" the warning by
            // asserting the wrong thing.
            Assert.IsFalse(EndConditionOverridesSO.CanOverrideTurnTarget(GameModes.AstroLeague));
            Assert.IsTrue(EndConditionOverridesSO.CanOverrideTurnTarget(GameModes.MultiplayerJoust));
        }

        [Test]
        public void MaelstromTarget_IsNotShortenedByARunOverride()
        {
            // The Maelstrom's number is a session-level meta ("race to N rounds"), not a turn's
            // end condition, so it deliberately sits outside the override.
            var end = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
            try
            {
                end.maelstromWinTarget = 6;
                EndConditionOverridesSO.SetRunOverride(GameModes.Tournament, 2);
                Assert.AreEqual(6, end.GetMaelstromWinTarget());
            }
            finally
            {
                EndConditionOverridesSO.ClearRunOverride();
                UnityEngine.Object.DestroyImmediate(end);
            }
        }

        // ── Test mode ──────────────────────────────────────────────────────────

        [Test]
        public void TestMode_ShrinksThePeriodAndKeepsItsKeyDistinct()
        {
            _catalog.test.enabled = true;
            _catalog.test.dayLengthMinutes = 5f;

            var t0 = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(_catalog.PeriodKeyFor(t0), _catalog.PeriodKeyFor(t0.AddMinutes(4)));
            Assert.AreNotEqual(_catalog.PeriodKeyFor(t0), _catalog.PeriodKeyFor(t0.AddMinutes(6)));

            // A test key must never be readable as a real date - switching back has to WIPE the
            // record rather than blend a shortened cycle's progress into a real day's.
            StringAssert.StartsWith("T", _catalog.PeriodKeyFor(t0));
            Assert.AreNotEqual(DailyChallengeCatalogSO.DateKeyFor(t0), _catalog.PeriodKeyFor(t0));

            Assert.LessOrEqual((_catalog.PeriodEndUtc(t0) - t0).TotalMinutes, 5.001);
        }

        [Test]
        public void TestMode_ForcedIndexPinsTheDraw()
        {
            _catalog.test.enabled = true;
            _catalog.test.forcedPoolIndex = 2; // Rampage

            for (int i = 0; i < 5; i++)
            {
                var challenge = _catalog.ForDate(
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                Assert.AreEqual(GameModes.Rampage, challenge.GameMode);
            }
        }

        [Test]
        public void TestMode_ForcedIndexPastTheEndFallsBackToTheDraw()
        {
            _catalog.test.enabled = true;
            _catalog.test.forcedPoolIndex = 99;

            Assert.IsTrue(_catalog.ForDate(DateTime.UtcNow).IsValid,
                "An out-of-range forced index must fall back, not blank the challenge.");
        }

        [Test]
        public void TestMode_TimeScaleAppliesToTheObjectiveCopyToo()
        {
            _catalog.test.enabled = true;
            _catalog.test.forcedPoolIndex = 0; // 60s crystal capture entry
            _catalog.test.timeLimitScale = 0.25f;

            var challenge = _catalog.ForDate(DateTime.UtcNow);
            Assert.AreEqual(15f, challenge.TimeLimitSeconds, 0.01f);
            StringAssert.Contains("0:15", challenge.ObjectiveText,
                "The card must describe the clock the run actually uses, not the authored one.");
        }

        [Test]
        public void TestMode_IgnoreAttemptLimitMakesAttemptsUnlimited()
        {
            _catalog.attemptsPerDay = 1;
            Assert.AreEqual(1, _catalog.EffectiveAttemptsPerDay);

            _catalog.test.enabled = true;
            _catalog.test.ignoreAttemptLimit = true;
            Assert.AreEqual(0, _catalog.EffectiveAttemptsPerDay, "0 means unlimited.");
        }

        [Test]
        public void DisabledEntry_IsNeverDrawn()
        {
            foreach (var e in _catalog.Pool) e.Enabled = false;
            _catalog.Pool[1].Enabled = true;

            for (int i = 0; i < 10; i++)
                Assert.AreEqual(GameModes.MultiplayerJoust,
                    _catalog.ForDate(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)).GameMode);
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
        public void CloudData_ResetForNewDay_ClearsTheDaysProgress()
        {
            var data = new DailyChallengeCloudData
            {
                ChallengeDate = "2026-08-28",
                BestValue = 42,
                Completed = true,
                Attempts = 3,
            };

            data.ResetForNewDay("2026-08-29", "Rampage", 1, "PrismsDestroyed", 300);

            Assert.AreEqual(0, data.BestValue);
            Assert.IsFalse(data.Completed);
            Assert.AreEqual(0, data.Attempts,
                "Attempts do not bank - one a day is a rhythm, not a currency.");
            Assert.AreEqual(300, data.TargetValue);
            Assert.AreEqual("Rampage", data.GameMode);
        }

        [Test]
        public void CloudData_RecordResult_KeepsTheBestAndLatchesCompletion()
        {
            var now = new DateTime(2026, 8, 29, 10, 0, 0, DateTimeKind.Utc);
            var data = new DailyChallengeCloudData();
            data.ResetForNewDay("2026-08-29", "MultiplayerCrystalCapture", 1, "Crystals", 8);

            data.RecordResult(5, 8, now);
            Assert.AreEqual(5, data.BestValue);
            Assert.IsFalse(data.Completed);

            // A worse run must not erase a better one.
            data.RecordResult(2, 8, now);
            Assert.AreEqual(5, data.BestValue);

            // The RESULT never touches the attempt counter - an attempt is spent when it STARTS,
            // which is what makes "played only once" survive a mid-run quit.
            Assert.AreEqual(0, data.Attempts);

            data.RecordResult(8, 8, now);
            Assert.AreEqual(8, data.BestValue);
            Assert.IsTrue(data.Completed);
            Assert.Greater(data.CompletedAtUnixMs, 0);

            // Completion is a latch - a later worse attempt cannot un-complete the day.
            long stamp = data.CompletedAtUnixMs;
            data.RecordResult(1, 8, now.AddMinutes(5));
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

                // THE trap: the run ends when the RACE target is met, which ends the challenge
                // with it. An objective above that can never complete.
                Assert.AreEqual(DailyChallengeCatalogSO.ResolvePlayableDomain(entry.Domain), entry.Domain,
                    $"{entry.Mode}: pinned domain is not one a player flies.");

                Assert.LessOrEqual(entry.Target, entry.ResolvedEndCondition,
                    $"{entry.Mode}: the objective is above the run's race target, so the turn " +
                    "ends before the objective can be met.");
            }

            Assert.AreEqual(1, shipped.attemptsPerDay,
                "The daily challenge is played ONCE - see Docs/DAILY_CHALLENGE.md §1.");
            Assert.IsFalse(shipped.test != null && shipped.test.enabled,
                "Test mode must never ship enabled.");
        }

        [Test]
        public void ShippedCatalog_IsSmallerThanTheRealModes()
        {
            // The whole premise: a daily run is a SHORTER version of the mode. A race target at or
            // above the mode's own is a full-length match with a clock on it.
            var shipped = Resources.Load<DailyChallengeCatalogSO>(DailyChallengeCatalogSO.ResourcePath);
            if (shipped == null) Assert.Ignore("No shipped catalog.");

            var end = Resources.Load<EndConditionOverridesSO>(EndConditionOverridesSO.ResourcePath);
            if (end == null) Assert.Ignore("No EndConditionOverrides asset.");

            foreach (var entry in shipped.Pool)
            {
                if (entry == null || !entry.Enabled) continue;
                if (!EndConditionOverridesSO.CanOverrideTurnTarget(entry.Mode)) continue;
                if (!end.TryGetAuthoredTurnTarget(entry.Mode, out int normal)) continue;

                Assert.Less(entry.ResolvedEndCondition, normal,
                    $"{entry.Mode}: a daily run races to {entry.ResolvedEndCondition} but a normal " +
                    $"match races to {normal} - the daily version is not smaller.");
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
