using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Toy Box's two big buttons, against a fake roster shaped like the shipped toys.
    ///
    /// <para>These run the REAL <see cref="ToyShuffle"/> and <see cref="DailyToyActivity"/> rather
    /// than a transcription of them, which is the point: both are pure over
    /// <see cref="ToyShellRegistry"/>, so the one thing worth proving is the SCOPE rule -
    /// <see cref="ToyShellOption.RequiresFreestyle"/> divides an activity from a setting, and
    /// neither button may ever reach into the other's half.</para>
    /// </summary>
    public class ToyboxTopButtonsTests
    {
        // ── the fake roster ────────────────────────────────────────────────

        class TestDefinition : ToyDefinitionSO
        {
            public ToyCategory CategoryValue = ToyCategory.Pilot;
            public override ToyCategory Category => CategoryValue;

            // These tests never build a runtime toy - the two buttons are pure over the
            // registry's shell options, so a definition here only has to exist and carry
            // a category.
            public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context) { }
        }

        class TestSurface : IToyShellSurface
        {
            public ToyDefinitionSO Definition;
            public bool Available = true;
            public Func<List<ToyShellOption>> Builder;

            public ToyDefinitionSO ShellDefinition => Definition;
            public bool ShellAvailable => Available;
            public void BuildShellOptions(List<ToyShellOption> into) => into.AddRange(Builder());
        }

        readonly List<TestSurface> _registered = new();
        readonly List<ToyDefinitionSO> _definitions = new();
        readonly List<string> _applied = new();

        TestSurface Surface(string name, ToyCategory category, params ToyShellOption[] options)
        {
            var definition = ScriptableObject.CreateInstance<TestDefinition>();
            definition.name = name;
            definition.CategoryValue = category;
            _definitions.Add(definition);

            var captured = new List<ToyShellOption>(options);
            var surface = new TestSurface
            {
                Definition = definition,
                Builder = () => captured,
            };
            _registered.Add(surface);
            ToyShellRegistry.Register(surface);
            return surface;
        }

        ToyShellOption Leaf(string label, bool activity = false, bool current = false,
                            bool appliesOnSelect = false, bool readOnly = false) =>
            new ToyShellOption
            {
                Label = label,
                RequiresFreestyle = activity,
                IsCurrent = current,
                AppliesOnSelect = appliesOnSelect,
                Apply = readOnly ? null : () => _applied.Add(label),
            };

        static ToyShellOption Branch(string label, params ToyShellOption[] children) =>
            new ToyShellOption { Label = label, Expand = () => new List<ToyShellOption>(children) };

        [TearDown]
        public void TearDown()
        {
            foreach (var surface in _registered) ToyShellRegistry.Unregister(surface);
            _registered.Clear();
            foreach (var definition in _definitions)
                if (definition) UnityEngine.Object.DestroyImmediate(definition);
            _definitions.Clear();
            _applied.Clear();
        }

        /// <summary>The shipped shape: settings flat, one nested Creation toy, two activity toys.</summary>
        void BuildRoster()
        {
            Surface("Cell Selector", ToyCategory.World,
                Leaf("Blob", current: true), Leaf("Atlantis"), Leaf("Garland"));
            Surface("Domain Changer", ToyCategory.Pilot,
                Leaf("Ruby", appliesOnSelect: true), Leaf("Gold", appliesOnSelect: true));
            Surface("Vessel Changer", ToyCategory.Pilot,
                Leaf("Squirrel", current: true, readOnly: true), Leaf("Manta"), Leaf("Rhino"));
            Surface("Lifeform Matrix", ToyCategory.Creation,
                Branch("Fauna", Branch("Shark", Leaf("Charge"), Leaf("Mass"))),
                Branch("Flora", Branch("Gyroid", Leaf("Time"))));
            Surface("Connect the Dots", ToyCategory.Creation,
                Leaf("Star", activity: true), Leaf("Rainbow", activity: true));
            Surface("Wanderway", ToyCategory.World, Leaf("Wander", activity: true));
        }

        // ── the scope rule ─────────────────────────────────────────────────

        [Test]
        public void Shuffle_TakesOneSettingPerToy_AndNoActivity()
        {
            BuildRoster();
            var plan = ToyShuffle.Plan(new System.Random(1));

            // Four SETTING toys; the gallery and the Wanderway offer activities only.
            Assert.AreEqual(4, plan.Count, "one pick per toy that offers a setting");
            foreach (var pick in plan)
            {
                Assert.IsFalse(pick.Option.RequiresFreestyle,
                    $"'{pick.Option.Label}' is an activity - it belongs to the other button");
                Assert.IsNotNull(pick.Option.Apply, $"'{pick.Option.Label}' must be appliable");
                Assert.IsFalse(pick.Option.IsBranch, $"'{pick.Option.Label}' must be a leaf");
            }
        }

        [Test]
        public void Shuffle_AppliesWorldLast()
        {
            BuildRoster();
            // A cell swap tears the toybox down and unregisters every other surface, so the World
            // pick has to be the last thing applied.
            for (int seed = 0; seed < 50; seed++)
            {
                var plan = ToyShuffle.Plan(new System.Random(seed));
                int lastNonWorld = -1, firstWorld = int.MaxValue;
                for (int i = 0; i < plan.Count; i++)
                {
                    if (plan[i].Category == ToyCategory.World) firstWorld = Mathf.Min(firstWorld, i);
                    else lastNonWorld = i;
                }
                if (firstWorld != int.MaxValue)
                    Assert.Greater(firstWorld, lastNonWorld,
                        $"seed {seed}: a World pick is applied before a Pilot/Creation one");
            }
        }

        [Test]
        public void Shuffle_NeverPicksWhereThePlayerAlreadyIs()
        {
            BuildRoster();
            // The cell selector DOES offer the current world (flying it is the freestyle reset),
            // so without the preference a shuffle could land on it and read as having done nothing.
            for (int seed = 0; seed < 200; seed++)
                foreach (var pick in ToyShuffle.Plan(new System.Random(seed)))
                    Assert.IsFalse(pick.Option.IsCurrent,
                        $"seed {seed}: shuffled onto '{pick.Option.Label}', which is current");
        }

        [Test]
        public void Shuffle_ReachesEverySettingLeaf_AndNothingElse()
        {
            BuildRoster();
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 400; seed++)
                foreach (var pick in ToyShuffle.Plan(new System.Random(seed)))
                    seen.Add(pick.Option.Label);

            foreach (var reachable in new[] { "Atlantis", "Garland", "Ruby", "Gold",
                                              "Manta", "Rhino", "Charge", "Mass", "Time" })
                Assert.IsTrue(seen.Contains(reachable), $"'{reachable}' is never shuffled to");

            // Current rows, read-only rows and activities must never be shuffled.
            foreach (var never in new[] { "Blob", "Squirrel", "Star", "Rainbow", "Wander" })
                Assert.IsFalse(seen.Contains(never), $"'{never}' must never be shuffled to");
        }

        [Test]
        public void Shuffle_SkipsAnUnavailableToy()
        {
            BuildRoster();
            foreach (var surface in _registered)
                if (surface.Definition.name is "Cell Selector" or "Vessel Changer")
                    surface.Available = false;

            foreach (var pick in ToyShuffle.Plan(new System.Random(3)))
                Assert.IsFalse(pick.ToyName is "Cell Selector" or "Vessel Changer",
                    "a toy that cannot answer must not be shuffled");
        }

        // ── the daily activity ─────────────────────────────────────────────

        [Test]
        public void DailyActivity_PoolIsActivitiesOnly()
        {
            BuildRoster();
            var pool = DailyToyActivity.Candidates;

            Assert.AreEqual(3, pool.Count, "two paintings plus the Wanderway");
            foreach (var candidate in pool)
            {
                Assert.IsTrue(candidate.Option.RequiresFreestyle,
                    $"'{candidate.ActivityName}' is a setting - it belongs to the shuffle");
                Assert.IsFalse(candidate.Option.IsBranch);
                Assert.IsFalse(candidate.Option.AppliesOnSelect,
                    "BindToPath declines to select an applies-on-select row, so one here would be "
                    + "an activity the button could never arm");
                Assert.AreEqual(1, candidate.Path.Count, "a top-layer activity is a one-label path");
            }
        }

        [Test]
        public void DailyActivity_IsStableWithinADay()
        {
            BuildRoster();
            var first = DailyToyActivity.Today;
            Assert.IsTrue(first.IsValid);
            for (int i = 0; i < 25; i++)
                Assert.AreEqual(first.ActivityName, DailyToyActivity.Today.ActivityName,
                    "the day's activity must not change between reads");
        }

        [Test]
        public void DailyActivity_PoolSurvivesAToyGoingUnavailable()
        {
            BuildRoster();
            int before = DailyToyActivity.Candidates.Count;
            string activity = DailyToyActivity.Today.ActivityName;

            foreach (var surface in _registered) surface.Available = false;

            // Availability must NOT move the pool: a shorter list moves the hash, which would
            // change the day's activity for as long as a cell swap lasted and then change it back.
            Assert.AreEqual(before, DailyToyActivity.Candidates.Count);
            Assert.AreEqual(activity, DailyToyActivity.Today.ActivityName);
        }

        [Test]
        public void DailyActivity_IsUnavailableWithNoToys()
        {
            // No roster registered at all - the button must report it rather than looking live.
            Assert.IsFalse(DailyToyActivity.Today.IsValid);
            Assert.AreEqual(0, DailyToyActivity.Candidates.Count);
        }

        [Test]
        public void DailyActivity_DrawSpreadsOverEveryPoolSizeTheToyboxCouldHave()
        {
            // The draw is FNV-1a over the UTC day key, modulo the pool size. A hash whose low bits
            // cycled with the pool size would starve some activities forever - measured here rather
            // than assumed, because the pool is derived at runtime and its size is not a constant
            // anybody authored. Three years of day keys, every pool size the toybox could produce.
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var hashes = new List<uint>();
            for (int day = 0; day < 1095; day++)
                hashes.Add(WeeklyChallengeCatalogSO.HashPeriodKey(
                    DailyToyActivity.DayKeyFor(start.AddDays(day))));

            for (int size = 2; size <= 40; size++)
            {
                var counts = new int[size];
                foreach (var hash in hashes) counts[(int)(hash % (uint)size)]++;

                // The bound that matters is STARVATION - an activity nobody ever sees. The skew
                // floor beside it is a sanity rail, and it is set from the MEASURED worst rather
                // than from taste: over pool sizes 2..40 the thinnest option lands at 0.452 of
                // ideal (pool 33, 15 draws against 33.2), and nothing starves. A floor of 0.5
                // would have failed on that size - measured before it shipped, which is the only
                // reason this assertion is a rail and not a trap.
                double ideal = hashes.Count / (double)size;
                for (int i = 0; i < size; i++)
                {
                    Assert.Greater(counts[i], 0,
                        $"pool size {size}: option {i} is never drawn in three years");
                    Assert.Greater(counts[i], ideal / 3d,
                        $"pool size {size}: option {i} is drawn {counts[i]} times against an ideal "
                        + $"of {ideal:0.#} - the draw is skewed");
                }
            }
        }

        [Test]
        public void DailyActivity_DayKeyIsInvariantAndUtc()
        {
            // A bare ToString("yyyy-MM-dd") formats through CurrentCulture, whose CALENDAR is not
            // always Gregorian - and Unity takes CurrentCulture from the device locale, so those
            // players would draw a different activity from everyone around them. This is the same
            // trap WeeklyChallengeCatalogSO.WeekKeyFor records.
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                var utc = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
                string expected = DailyToyActivity.DayKeyFor(utc);
                Assert.AreEqual("2026-09-24", expected);

                foreach (var culture in new[] { "ar-SA", "th-TH", "fa-IR" })
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture =
                        new System.Globalization.CultureInfo(culture);
                    Assert.AreEqual(expected, DailyToyActivity.DayKeyFor(utc),
                        $"the day key changed under {culture} - it is not InvariantCulture");
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void DailyActivity_ClaimIsANoOpWithNoProfile()
        {
            // Edit mode has no PlayerDataService. The claim must pay nothing and throw nothing -
            // the activity itself still ran.
            Assert.AreEqual(0, DailyToyActivity.TryClaim());
            Assert.IsFalse(DailyToyActivity.ClaimedToday);
        }

        [Test]
        public void RewardIdCarriesTheDay()
        {
            // Yesterday's claim can never satisfy today's.
            Assert.AreNotEqual(DailyToyActivity.RewardIdFor("2026-09-24"),
                               DailyToyActivity.RewardIdFor("2026-09-25"));
            StringAssert.Contains("2026-09-24", DailyToyActivity.RewardIdFor("2026-09-24"));
        }
    }
}
