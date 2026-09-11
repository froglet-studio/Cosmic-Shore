#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Invite-wave cohort tests - the dimension the paid-EA gate splits retention and
    /// stability by (D1 >= 35%, D7 >= 15% across two consecutive waves; crash-free sessions
    /// >= 99.5%).
    ///
    /// WHY THIS MATTERS:
    /// The wave is DERIVED - Steam hands the client no grant batch - so its correctness is
    /// entirely a property of this one function. Three things can silently break the gate and
    /// each is asserted here:
    ///
    /// 1. THE WEEK BOUNDARY. DayOfWeek numbers Sunday = 0, so the naive subtraction puts a
    ///    Sunday in the FOLLOWING week. A player who installs on a Sunday would land one wave
    ///    late, and only players who install on a Sunday - a seventh of them, invisibly.
    /// 2. ONE DEFINITION OF A WEEK. The wave must agree with the weekly challenge's boundary
    ///    to the character, or the project holds two answers to "which week is it".
    /// 3. UNKNOWN IS NOT A COHORT. first_seen = 0 means never stamped; bucketing it would put
    ///    that player in the week of 1969-12-29, a cohort that looks real and is not.
    /// </summary>
    [TestFixture]
    public class AnalyticsWaveCohortTests
    {
        static long Utc(int y, int m, int d, int h = 0, int min = 0) =>
            new DateTimeOffset(new DateTime(y, m, d, h, min, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        // ── The Monday boundary ────────────────────────────────────────────────

        [Test]
        public void MondayIsItsOwnWeekStart()
        {
            // 2026-09-07 is a Monday.
            Assert.AreEqual("2026-09-07", AnalyticsServiceFacade.InviteWaveFor(Utc(2026, 9, 7)));
        }

        [Test]
        public void EveryDayOfOneWeekSharesOneWave()
        {
            // Monday 2026-09-07 .. Sunday 2026-09-13 are one wave.
            for (int offset = 0; offset < 7; offset++)
            {
                long t = Utc(2026, 9, 7 + offset, 13, 45);
                Assert.AreEqual("2026-09-07", AnalyticsServiceFacade.InviteWaveFor(t),
                    $"day offset {offset} fell outside its own week");
            }
        }

        [Test]
        public void SundayBelongsToTheWeekThatPrecedesIt()
        {
            // The DayOfWeek-numbers-Sunday-0 trap. A naive ((int)DayOfWeek) subtraction
            // returns 2026-09-14 here, putting a Sunday installer one wave late.
            Assert.AreEqual("2026-09-07", AnalyticsServiceFacade.InviteWaveFor(Utc(2026, 9, 13, 23, 59)));
        }

        [Test]
        public void MondayMidnightUtcStartsTheNextWave()
        {
            Assert.AreEqual("2026-09-07", AnalyticsServiceFacade.InviteWaveFor(Utc(2026, 9, 13, 23, 59)));
            Assert.AreEqual("2026-09-14", AnalyticsServiceFacade.InviteWaveFor(Utc(2026, 9, 14, 0, 0)));
        }

        [Test]
        public void ConsecutiveWavesAreSevenDaysApart()
        {
            // The gate reads "two consecutive waves", so adjacency has to be exact.
            var first = DateTime.ParseExact(
                AnalyticsServiceFacade.InviteWaveFor(Utc(2026, 9, 9)), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var second = DateTime.ParseExact(
                AnalyticsServiceFacade.InviteWaveFor(Utc(2026, 9, 16)), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            Assert.AreEqual(7, (second - first).TotalDays);
        }

        // ── One definition of a week ───────────────────────────────────────────

        [Test]
        public void WaveMatchesTheWeeklyChallengeWeekKey()
        {
            // Same boundary, same formatter, same string - the whole point of reusing
            // WeekKeyFor rather than writing a second one.
            foreach (var probe in new[]
                     {
                         new DateTime(2026, 1, 1, 5, 0, 0, DateTimeKind.Utc),
                         new DateTime(2026, 9, 13, 23, 59, 0, DateTimeKind.Utc),
                         new DateTime(2026, 12, 31, 12, 0, 0, DateTimeKind.Utc),
                         new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                     })
            {
                long ms = new DateTimeOffset(probe).ToUnixTimeMilliseconds();
                Assert.AreEqual(WeeklyChallengeCatalogSO.WeekKeyFor(probe),
                    AnalyticsServiceFacade.InviteWaveFor(ms),
                    $"wave and weekly-challenge week disagree at {probe:O}");
            }
        }

        [Test]
        public void WaveCrossesAYearBoundaryWithoutResetting()
        {
            // 2026-12-28 is a Monday; the week it starts runs into 2027.
            Assert.AreEqual("2026-12-28", AnalyticsServiceFacade.InviteWaveFor(Utc(2026, 12, 31)));
            Assert.AreEqual("2026-12-28", AnalyticsServiceFacade.InviteWaveFor(Utc(2027, 1, 3)));
            Assert.AreEqual("2027-01-04", AnalyticsServiceFacade.InviteWaveFor(Utc(2027, 1, 4)));
        }

        // ── Unknown is not a cohort ────────────────────────────────────────────

        [Test]
        public void UnstampedFirstSeenHasNoWave()
        {
            // 0 is "never stamped". FromUnixTimeMilliseconds(0) would answer 1969-12-29.
            Assert.AreEqual(string.Empty, AnalyticsServiceFacade.InviteWaveFor(0));
        }

        [Test]
        public void NegativeFirstSeenHasNoWave()
        {
            Assert.AreEqual(string.Empty, AnalyticsServiceFacade.InviteWaveFor(-1));
            Assert.AreEqual(string.Empty, AnalyticsServiceFacade.InviteWaveFor(long.MinValue));
        }

        // ── Shape ──────────────────────────────────────────────────────────────

        /// <summary>
        /// A cohort label must name the same wave on every device. Unity takes CurrentCulture from
        /// the device locale, and a bare <c>ToString("yyyy-MM-dd")</c> renders through that
        /// culture's CALENDAR - so before the InvariantCulture fix in WeeklyChallengeCatalogSO this
        /// same instant keyed as 1448-03-25 on ar-SA and 2569-09-07 on th-TH. Players on those
        /// devices would each have formed their own one-person "wave", quietly draining the real
        /// ones - and the gate reads a RATIO, so it would have looked like a retention problem.
        /// </summary>
        [Test]
        public void WaveIsTheSameInEveryLocale()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            int checkedCultures = 0;

            try
            {
                foreach (var name in new[] { "ar-SA", "th-TH", "fa-IR", "he-IL", "en-US" })
                {
                    System.Globalization.CultureInfo culture;
                    try { culture = new System.Globalization.CultureInfo(name); }
                    catch (System.Globalization.CultureNotFoundException) { continue; }

                    System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                    checkedCultures++;

                    Assert.AreEqual("2026-09-07", AnalyticsServiceFacade.InviteWaveFor(Utc(2026, 9, 9)),
                        $"wave drifted under {name} ({culture.Calendar.GetType().Name})");
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }

            Assert.GreaterOrEqual(checkedCultures, 2,
                "no alternate locales were available, so this asserted nothing");
        }

        // ── The identify dedup ─────────────────────────────────────────────────
        //
        // IdentifyPlayer now fires on every cloud-profile change, not just at session
        // boundaries, so it drops payloads identical to the last one sent. That is a
        // SILENT mechanism in both directions: too sensitive and it spends PostHog events
        // on no-op repaints, not sensitive enough and it swallows an update nobody will
        // ever notice is missing. These pin both edges.

        static Dictionary<string, object> SampleProperties() => new Dictionary<string, object>
        {
            ["display_name"] = "Ren",
            ["crystal_balance"] = 10,
            ["first_seen_utc_ms"] = 1757203200000L,
            ["invite_wave"] = "2026-09-07",
            ["games_completed"] = 3,
            ["total_flight_time_seconds"] = 12.5f,
        };

        [Test]
        public void SignatureIgnoresPropertyOrder()
        {
            // Dictionary enumeration order is not contractual. An order-sensitive signature
            // would re-send unchanged payloads - safe, but it defeats the dedup entirely.
            var forward = SampleProperties();
            var reversed = new Dictionary<string, object>();
            var keys = new List<string>(forward.Keys);
            keys.Reverse();
            foreach (var k in keys) reversed[k] = forward[k];

            Assert.AreEqual(AnalyticsServiceFacade.BuildIdentifySignature("u1", forward),
                            AnalyticsServiceFacade.BuildIdentifySignature("u1", reversed));
        }

        [Test]
        public void SignatureChangesWhenAValueChanges()
        {
            var before = SampleProperties();
            var after = new Dictionary<string, object>(before) { ["crystal_balance"] = 11 };

            Assert.AreNotEqual(AnalyticsServiceFacade.BuildIdentifySignature("u1", before),
                               AnalyticsServiceFacade.BuildIdentifySignature("u1", after));
        }

        [Test]
        public void SignatureChangesWhenIdentityChanges()
        {
            var props = SampleProperties();
            Assert.AreNotEqual(AnalyticsServiceFacade.BuildIdentifySignature("player-a", props),
                               AnalyticsServiceFacade.BuildIdentifySignature("player-b", props));
        }

        [Test]
        public void SignatureChangesWhenTheWaveFirstAppears()
        {
            // THE case the profile-change hook exists for: PlayerDataService builds a
            // local-default profile in Awake, so the first identify of a session can carry no
            // wave at all. When the cloud profile lands, that identify MUST go out.
            var beforeCloudLoad = SampleProperties();
            beforeCloudLoad.Remove("invite_wave");
            beforeCloudLoad.Remove("first_seen_utc_ms");

            Assert.AreNotEqual(AnalyticsServiceFacade.BuildIdentifySignature("u1", beforeCloudLoad),
                               AnalyticsServiceFacade.BuildIdentifySignature("u1", SampleProperties()),
                               "a profile gaining its wave must not be deduped away");
        }

        [Test]
        public void SignatureIsStableForAnUnchangedPayload()
        {
            Assert.AreEqual(AnalyticsServiceFacade.BuildIdentifySignature("u1", SampleProperties()),
                            AnalyticsServiceFacade.BuildIdentifySignature("u1", SampleProperties()));
        }

        [Test]
        public void SignatureFormatsNumbersInvariantly()
        {
            // A locale that renders 12.5 as "12,5" would make an unchanged float look changed
            // on every identify, quietly turning the dedup off for anyone outside en-US.
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            string reference = AnalyticsServiceFacade.BuildIdentifySignature("u1", SampleProperties());
            int checkedCultures = 0;

            try
            {
                foreach (var name in new[] { "de-DE", "fr-FR", "ar-SA" })
                {
                    System.Globalization.CultureInfo culture;
                    try { culture = new System.Globalization.CultureInfo(name); }
                    catch (System.Globalization.CultureNotFoundException) { continue; }

                    System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                    checkedCultures++;

                    Assert.AreEqual(reference,
                        AnalyticsServiceFacade.BuildIdentifySignature("u1", SampleProperties()),
                        $"signature drifted under {name}");
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }

            Assert.GreaterOrEqual(checkedCultures, 1,
                "no alternate locales were available, so this asserted nothing");
        }

        [Test]
        public void SignatureToleratesANullValue()
        {
            // display_name is coalesced today, but a future property need not be.
            Assert.DoesNotThrow(() => AnalyticsServiceFacade.BuildIdentifySignature(
                "u1", new Dictionary<string, object> { ["display_name"] = null }));
        }
    }
}
#endif
