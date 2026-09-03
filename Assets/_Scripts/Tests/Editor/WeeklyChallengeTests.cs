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
    /// The weekly challenge's pure halves: the DRAW (a function of the UTC date over the catalog)
    /// and the RECORD (the cloud model's rollover and attempt folding).
    ///
    /// WHY THIS MATTERS: the whole feature rests on one promise - every player on a given UTC date
    /// faces the same challenge, and yesterday's progress never bleeds into this week. Both are pure
    /// functions, so both are testable without a play-mode session; the service that ties them
    /// together is a thin MonoBehaviour over exactly these two pieces.
    /// </summary>
    [TestFixture]
    public class WeeklyChallengeTests
    {
        WeeklyChallengeCatalogSO _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<WeeklyChallengeCatalogSO>();
            _catalog.Pool = new List<WeeklyChallengeCatalogSO.Entry>
            {
                new() { Mode = GameModes.Scurry, Metric = ScoringMetric.Crystals,
                        Target = 8, Intensity = 1,
                        Verb = "Collect", Noun = "crystals" },
                new() { Mode = GameModes.Joust, Metric = ScoringMetric.Jousts,
                        Target = 1, Intensity = 1,
                        Verb = "Land", Noun = "joust" },
                new() { Mode = GameModes.Rampage, Metric = ScoringMetric.PrismsDestroyed,
                        Target = 300, Intensity = 1,
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
            Assert.AreEqual(a.PeriodKey, b.PeriodKey);
        }

        [Test]
        public void ForDate_IgnoresTimeOfDay()
        {
            // Two instants on the same UTC week must be the same challenge - otherwise a player
            // who launched at 23:59 and finished at 00:01 would have been scored against a
            // challenge that no longer existed by the time it was recorded.
            var morning = new DateTime(2026, 8, 29, 0, 0, 1, DateTimeKind.Utc);
            var night = new DateTime(2026, 8, 29, 23, 59, 59, DateTimeKind.Utc);

            Assert.AreEqual(_catalog.ForDate(morning).GameMode, _catalog.ForDate(night).GameMode);
            Assert.AreEqual(_catalog.ForDate(morning).PeriodKey, _catalog.ForDate(night).PeriodKey);
        }

        [Test]
        public void ForDate_ConvertsLocalInstantsToUtcBeforeDrawing()
        {
            // A DateTimeKind.Local instant must land on the UTC week, not the local one - the
            // draw is a promise about a shared calendar, so the timezone of the machine asking
            // must not be able to change the answer.
            var utc = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
            var local = utc.ToLocalTime();

            Assert.AreEqual(_catalog.ForDate(utc).PeriodKey, _catalog.ForDate(local).PeriodKey);
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
            _catalog.Pool = new List<WeeklyChallengeCatalogSO.Entry>();
            Assert.IsFalse(_catalog.ForDate(DateTime.UtcNow).IsValid);
        }

        [Test]
        public void ForDate_SkipsEntriesWithNoTarget()
        {
            _catalog.Pool = new List<WeeklyChallengeCatalogSO.Entry>
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
        public void HashPeriodKey_IsPlatformIndependentFnv1a()
        {
            // Pinned values. System.Random is deterministic only within one runtime's
            // implementation, which is not a promise two clients on two platforms can hold each
            // other to - these constants are what makes "the same challenge for everyone" real.
            Assert.AreEqual(2166136261u, WeeklyChallengeCatalogSO.HashPeriodKey(""),
                "Empty input must be the FNV-1a offset basis.");
            Assert.AreEqual(WeeklyChallengeCatalogSO.HashPeriodKey("2026-08-29"),
                            WeeklyChallengeCatalogSO.HashPeriodKey("2026-08-29"));
            Assert.AreNotEqual(WeeklyChallengeCatalogSO.HashPeriodKey("2026-08-29"),
                               WeeklyChallengeCatalogSO.HashPeriodKey("2026-08-30"));
        }

        [Test]
        public void NextRolloverUtc_IsTheFollowingMonday()
        {
            var noon = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc),
                            WeeklyChallengeCatalogSO.NextRolloverUtc(noon));
        }

        [Test]
        public void ObjectiveText_ReadsAsOneSentence()
        {
            var entry = new WeeklyChallengeCatalogSO.Entry
            {
                Target = 30, Verb = "Collect", Noun = "crystals"
            };
            Assert.AreEqual("Collect 30 crystals", WeeklyChallengeCatalogSO.BuildObjectiveText(entry));

            // No duration, ever. A weekly run is an ordinary match of its mode played for a
            // personal objective on top, so an objective line that said "in 1:00" was describing a
            // rule the run does not have.
            StringAssert.DoesNotContain(" in ", WeeklyChallengeCatalogSO.BuildObjectiveText(entry));
        }

        [Test]
        public void FormatCountdown_StepsDownAsTheWeekRunsOut()
        {
            // A week is up to 168 hours, and "163:04:11" is not a reading anyone parses as time.
            Assert.AreEqual("6d 3h",
                WeeklyChallengeCard.FormatCountdown(new TimeSpan(6, 3, 20, 0)));

            // Inside the last day, hours - the resolution that decides "can I fit a run in".
            Assert.AreEqual("7:12:33",
                WeeklyChallengeCard.FormatCountdown(new TimeSpan(7, 12, 33)));

            // Inside the last hour, seconds start to matter.
            Assert.AreEqual("4:07", WeeklyChallengeCard.FormatCountdown(new TimeSpan(0, 4, 7)));

            Assert.AreEqual("0:00", WeeklyChallengeCard.FormatCountdown(TimeSpan.FromSeconds(-5)),
                "A countdown must never render negative.");
        }

        [Test]
        public void ForDate_DefaultsToJadeAndRejectsUnplayableDomains()
        {
            // Domains has no member at 0, which is exactly what an entry authored before the field
            // existed deserializes to - and Blue is the "no team" sentinel, never a colour anyone
            // flies. Both must resolve to Jade rather than reaching a spawn.
            Assert.AreEqual(Domains.Jade, WeeklyChallengeCatalogSO.ResolvePlayableDomain(default));
            Assert.AreEqual(Domains.Jade, WeeklyChallengeCatalogSO.ResolvePlayableDomain(Domains.Blue));
            Assert.AreEqual(Domains.Ruby, WeeklyChallengeCatalogSO.ResolvePlayableDomain(Domains.Ruby));
            Assert.AreEqual(Domains.Gold, WeeklyChallengeCatalogSO.ResolvePlayableDomain(Domains.Gold));

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

        // ── The week ───────────────────────────────────────────────────────────

        [Test]
        public void WeekStart_IsTheUtcMonday()
        {
            // 2026-08-29 is a Saturday; its week begins on Monday the 24th.
            var sat = new DateTime(2026, 8, 29, 13, 45, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc),
                            WeeklyChallengeCatalogSO.WeekStartUtc(sat));

            // Sunday is the END of its week, not the start - the trap in every hand-rolled week
            // boundary, because DayOfWeek numbers Sunday 0.
            var sun = new DateTime(2026, 8, 30, 23, 59, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc),
                            WeeklyChallengeCatalogSO.WeekStartUtc(sun));

            // Monday is its own start.
            var mon = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(mon, WeeklyChallengeCatalogSO.WeekStartUtc(mon));
        }

        [Test]
        public void EveryDayOfOneWeekDrawsTheSameChallenge()
        {
            var monday = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc);
            var first = _catalog.ForDate(monday);

            for (int day = 1; day < 7; day++)
            {
                var later = _catalog.ForDate(monday.AddDays(day).AddHours(11));
                Assert.AreEqual(first.PeriodKey, later.PeriodKey,
                    "Every day of one week is the same period.");
                Assert.AreEqual(first.GameMode, later.GameMode,
                    "The challenge must not change mid-week.");
            }

            var nextWeek = _catalog.ForDate(monday.AddDays(7));
            Assert.AreNotEqual(first.PeriodKey, nextWeek.PeriodKey,
                "...and it must change when the week does.");
        }

        [Test]
        public void Rollover_IsTheNextMonday()
        {
            var wed = new DateTime(2026, 8, 26, 9, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                            WeeklyChallengeCatalogSO.NextRolloverUtc(wed));
        }

        [Test]
        public void Challenge_CarriesNoEndConditionOfItsOwn()
        {
            // A weekly run is an ORDINARY match of its mode: the objective rides on top of the
            // mode's own end conditions and nothing in the challenge shortens them. This test is
            // the guard on that - a challenge that grew a race target again would fail to compile
            // here, which is the point.
            var challenge = _catalog.ForDate(new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc));
            Assert.IsTrue(challenge.IsValid);
            Assert.Greater(challenge.TargetValue, 0);
            Assert.IsNull(typeof(WeeklyChallenge).GetField("EndConditionValue"),
                "The run-scoped race target was removed on purpose - a weekly run uses the mode's " +
                "own end conditions. Do not reintroduce it without re-reading Docs/WEEKLY_CHALLENGE.md.");
        }

        // ── Test mode ──────────────────────────────────────────────────────────

        [Test]
        public void TestMode_ShrinksThePeriodAndKeepsItsKeyDistinct()
        {
            _catalog.test.enabled = true;
            _catalog.test.periodLengthMinutes = 5f;

            var t0 = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(_catalog.PeriodKeyFor(t0), _catalog.PeriodKeyFor(t0.AddMinutes(4)));
            Assert.AreNotEqual(_catalog.PeriodKeyFor(t0), _catalog.PeriodKeyFor(t0.AddMinutes(6)));

            // A test key must never be readable as a real date - switching back has to WIPE the
            // record rather than blend a shortened cycle's progress into a real day's.
            StringAssert.StartsWith("T", _catalog.PeriodKeyFor(t0));
            Assert.AreNotEqual(WeeklyChallengeCatalogSO.WeekKeyFor(t0), _catalog.PeriodKeyFor(t0));

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
        public void TheChallengeNeverAltersTheMode()
        {
            // The whole reason the time limit is gone: it ended the turn. A run whose clock
            // expired had its attempt already spent (spent at LAUNCH) and NOTHING submitted, so a
            // player could lose their one weekly attempt to a rule the mode itself does not have.
            var challenge = _catalog.ForDate(DateTime.UtcNow);

            Assert.IsTrue(challenge.IsValid);
            StringAssert.DoesNotContain(" in ", challenge.ObjectiveText,
                "The objective must not describe a clock - the run uses the mode's own end conditions.");
        }

        [Test]
        public void TestMode_IgnoreAttemptLimitMakesAttemptsUnlimited()
        {
            _catalog.attemptsPerPeriod = 1;
            Assert.AreEqual(1, _catalog.EffectiveAttemptsPerPeriod);

            _catalog.test.enabled = true;
            _catalog.test.ignoreAttemptLimit = true;
            Assert.AreEqual(0, _catalog.EffectiveAttemptsPerPeriod, "0 means unlimited.");
        }

        [Test]
        public void DisabledEntry_IsNeverDrawn()
        {
            foreach (var e in _catalog.Pool) e.Enabled = false;
            _catalog.Pool[1].Enabled = true;

            for (int i = 0; i < 10; i++)
                Assert.AreEqual(GameModes.Joust,
                    _catalog.ForDate(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)).GameMode);
        }

        // ── The record ─────────────────────────────────────────────────────────

        [Test]
        public void CloudData_IsStale_ForAnEarlierDay()
        {
            var data = new WeeklyChallengeCloudData { ChallengeWeek = "2026-08-28" };
            Assert.IsTrue(data.IsStale("2026-08-29"));
            Assert.IsFalse(data.IsStale("2026-08-28"));
        }

        [Test]
        public void CloudData_ResetForNewDay_ClearsTheDaysProgress()
        {
            var data = new WeeklyChallengeCloudData
            {
                ChallengeWeek = "2026-08-28",
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
            var data = new WeeklyChallengeCloudData();
            data.ResetForNewDay("2026-08-29", "Scurry", 1, "Crystals", 8);

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
            var shipped = Resources.Load<WeeklyChallengeCatalogSO>(WeeklyChallengeCatalogSO.ResourcePath);
            Assert.IsNotNull(shipped,
                $"Missing Resources/{WeeklyChallengeCatalogSO.ResourcePath} - the card cannot draw a challenge without it.");
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

                Assert.AreEqual(WeeklyChallengeCatalogSO.ResolvePlayableDomain(entry.Domain), entry.Domain,
                    $"{entry.Mode}: the opening domain is not one a player flies.");
            }

            Assert.AreEqual(1, shipped.attemptsPerPeriod,
                "The weekly challenge is played ONCE - see Docs/WEEKLY_CHALLENGE.md §1.");
            Assert.IsFalse(shipped.test != null && shipped.test.enabled,
                "Test mode must never ship enabled.");
        }

        [Test]
        public void ShippedCatalog_ObjectivesAreReachableInsideANormalMatch()
        {
            // A weekly run plays the mode at its OWN end conditions, so the turn ends when that
            // mode's race target is met - and an objective above what a match of it can produce is
            // unreachable by construction. This is the one authoring mistake that looks fine in
            // the inspector and is impossible to hit in play.
            var shipped = Resources.Load<WeeklyChallengeCatalogSO>(WeeklyChallengeCatalogSO.ResourcePath);
            if (shipped == null) Assert.Ignore("No shipped catalog.");

            var end = Resources.Load<EndConditionOverridesSO>(EndConditionOverridesSO.ResourcePath);
            if (end == null) Assert.Ignore("No EndConditionOverrides asset.");

            foreach (var entry in shipped.Pool)
            {
                if (entry == null || !entry.Enabled) continue;
                if (!end.TryGetAuthoredTurnTarget(entry.Mode, out int normal)) continue;

                Assert.LessOrEqual(entry.Target, normal,
                    $"{entry.Mode}: the objective is {entry.Target} but a match races to {normal}, " +
                    "so the turn ends before the objective can be met.");
            }
        }

        [Test]
        public void ShippedCatalog_EveryModeHasAnArcadeCard()
        {
            // A pooled mode with no SO_ArcadeGame cannot be launched - SelectWeeklyChallenge would
            // warn and do nothing, which reads on screen as a dead card on whichever date drew it.
            var shipped = Resources.Load<WeeklyChallengeCatalogSO>(WeeklyChallengeCatalogSO.ResourcePath);
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
                    $"Weekly challenge pool names {entry.Mode}, which has no card in any SO_GameList.");
        }

        // ── Leaderboard ────────────────────────────────────────────────────────

        [Test]
        public void RankingTime_ReadsAsARaceTime()
        {
            Assert.AreEqual("0:47.30", WeeklyChallengeRanking.FormatSeconds(47.3d));
            Assert.AreEqual("1:00.00", WeeklyChallengeRanking.FormatSeconds(60d));
            Assert.AreEqual("2:05.09", WeeklyChallengeRanking.FormatSeconds(125.09d));
        }

        [Test]
        public void RankingTime_NeverRendersNonsense()
        {
            // A time is the SCORE here, so a bad one is a row on a public board rather than a log
            // line - it has to render as something a player can read either way.
            Assert.AreEqual("0:00.00", WeeklyChallengeRanking.FormatSeconds(-3d));
            Assert.AreEqual("0:00.00", WeeklyChallengeRanking.FormatSeconds(double.NaN));

            // The whole value converts to centiseconds in ONE step and rounds. Doing it the
            // obvious way - whole seconds, then floor((seconds - whole) * 100) - prints 47.3 as
            // "0:47.29", because the double nearest 47.3 is a hair below it and the subtraction
            // keeps the error. This case is that bug's guard.
            Assert.AreEqual("0:47.30", WeeklyChallengeRanking.FormatSeconds(47.3d));

            // Rounding carries properly across the minute rather than printing "0:60.00".
            Assert.AreEqual("1:00.00", WeeklyChallengeRanking.FormatSeconds(59.999d));

            Assert.AreEqual("--:--.--", WeeklyChallengeRanking.FormatSeconds(double.PositiveInfinity));
        }

        [Test]
        public void ShippedCatalog_LeaderboardIdIsAuthoredOrRankingIsOff()
        {
            // Not an assertion that an id EXISTS - shipping without one is a legitimate state (the
            // challenge runs, the ranking is off). This asserts the field is not whitespace, which
            // is the state that looks authored and behaves as empty.
            var shipped = Resources.Load<WeeklyChallengeCatalogSO>(WeeklyChallengeCatalogSO.ResourcePath);
            if (shipped == null) Assert.Ignore("No shipped catalog.");

            Assert.IsFalse(shipped.leaderboardId != null &&
                           shipped.leaderboardId.Length > 0 &&
                           shipped.leaderboardId.Trim().Length == 0,
                "leaderboardId is whitespace - it reads as authored and behaves as empty.");
        }
    }
}
#endif
