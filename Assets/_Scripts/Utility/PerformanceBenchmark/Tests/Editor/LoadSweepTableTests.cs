using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    /// <summary>
    /// Holds the load-time sweep's contract: the published target and the code's copy of it agree,
    /// the options parse, the plan is the menu's rosters minus unbuildable scenes, the table ranks
    /// the worst cell first, the verdicts read the target, and an early stop says why.
    /// </summary>
    [TestFixture]
    public class LoadSweepTableTests
    {
        const string PerfDoc = "Docs/PERFORMANCE_OPTIMIZATION.md";

        // The doc carries one machine-readable line so the publication and the gate cannot drift
        // apart silently: LOAD_TIME_TARGET cold_boot_to_menu_s=<n> menu_to_playable_s=<n>
        static readonly Regex TargetLine = new(
            @"LOAD_TIME_TARGET\s+cold_boot_to_menu_s=(?<boot>\d+(?:\.\d+)?)\s+menu_to_playable_s=(?<play>\d+(?:\.\d+)?)",
            RegexOptions.CultureInvariant);

        [Test]
        public void PublishedTargets_MatchTheCodeConstants()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", PerfDoc));
            Assert.That(File.Exists(path), Is.True, $"{PerfDoc} is missing; the target has no publication.");

            var match = TargetLine.Match(File.ReadAllText(path));
            Assert.That(match.Success, Is.True,
                $"{PerfDoc} has no LOAD_TIME_TARGET line; §0.6 must carry one so this test can read the published numbers.");

            float boot = float.Parse(match.Groups["boot"].Value, System.Globalization.CultureInfo.InvariantCulture);
            float play = float.Parse(match.Groups["play"].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(boot, Is.EqualTo(LoadTimeTargets.ColdBootToMenuSeconds).Within(0.001f),
                "The published cold-boot target and LoadTimeTargets.ColdBootToMenuSeconds disagree.");
            Assert.That(play, Is.EqualTo(LoadTimeTargets.MenuToPlayableSeconds).Within(0.001f),
                "The published menu→playable target and LoadTimeTargets.MenuToPlayableSeconds disagree.");
        }

        [Test]
        public void Options_Defaults()
        {
            var o = LoadSweepOptions.Parse(new[] { "game.exe", LoadSweepOptions.LaunchArg });
            Assert.That(o.Modes, Is.Null);
            Assert.That(o.Intensities, Is.EqualTo(new List<int> { 1, 2, 3, 4 }));
            Assert.That(o.Players, Is.EqualTo("4"));
            Assert.That(o.Repeats, Is.EqualTo(1));
            Assert.That(o.IncludeMaelstrom, Is.False);
            Assert.That(o.StayWhenDone, Is.False);
        }

        [Test]
        public void Options_ParseBothArgumentShapes()
        {
            var o = LoadSweepOptions.Parse(new[]
            {
                LoadSweepOptions.LaunchArg,
                LoadSweepOptions.ModesArg, "SkimRace, rampage",
                LoadSweepOptions.IntensitiesArg + "=4,1,9",
                LoadSweepOptions.PlayersArg, "MAX",
                LoadSweepOptions.RepeatsArg + "=3",
                LoadSweepOptions.TimeoutArg, "120",
                LoadSweepOptions.IncludeMaelstromArg,
                LoadSweepOptions.StayArg
            });
            Assert.That(o.Modes, Is.EqualTo(new List<string> { "SkimRace", "rampage" }));
            Assert.That(o.WantsMode("Rampage"), Is.True);
            Assert.That(o.WantsMode("Joust"), Is.False);
            Assert.That(o.Intensities, Is.EqualTo(new List<int> { 1, 4 }), "out-of-range intensities are dropped, the rest sorted");
            Assert.That(o.Players, Is.EqualTo(LoadSweepOptions.PlayersMax));
            Assert.That(o.Repeats, Is.EqualTo(3));
            Assert.That(o.CellTimeoutSeconds, Is.EqualTo(120f));
            Assert.That(o.IncludeMaelstrom, Is.True);
            Assert.That(o.StayWhenDone, Is.True);
        }

        [Test]
        public void Options_ResolvePlayers_ClampsIntoTheCardsRange()
        {
            var o = new LoadSweepOptions { Players = "4" };
            Assert.That(o.ResolvePlayers(2, 12), Is.EqualTo(4));
            Assert.That(o.ResolvePlayers(6, 6), Is.EqualTo(6), "a card that seats exactly six seats six");
            Assert.That(o.ResolvePlayers(1, 2), Is.EqualTo(2));
            o.Players = LoadSweepOptions.PlayersMax;
            Assert.That(o.ResolvePlayers(2, 12), Is.EqualTo(12));
            o.Players = LoadSweepOptions.PlayersMin;
            Assert.That(o.ResolvePlayers(2, 12), Is.EqualTo(2));
        }

        [Test]
        public void SortedWorstFirst_UnrecordedThenAbortedThenLongest()
        {
            var quick = Cell("Joust", 1, 8_000f);
            var slow = Cell("Scurry", 4, 45_000f);
            var aborted = Cell("Rampage", 1, 12_000f);
            aborted.completed = false;
            aborted.completionReason = "Aborted: timeout";
            var missing = new LoadSweepCell { mode = "Cleave", intensity = 2, recorded = false };

            var sorted = LoadSweepTable.SortedWorstFirst(new[] { quick, slow, aborted, missing });
            Assert.That(sorted[0], Is.SameAs(missing), "no data is the worst thing a row can say");
            Assert.That(sorted[1], Is.SameAs(aborted), "then a load that never reached playable");
            Assert.That(sorted[2], Is.SameAs(slow));
            Assert.That(sorted[3], Is.SameAs(quick));
        }

        [Test]
        public void Verdicts_ReadTheTarget()
        {
            float target = LoadTimeTargets.MenuToPlayableSeconds;
            Assert.That(LoadSweepTable.Verdict(Cell("Joust", 1, target * 1000f), target), Is.EqualTo("PASS"), "on the line passes");
            Assert.That(LoadSweepTable.Verdict(Cell("Joust", 1, target * 1000f + 1f), target), Is.EqualTo("FAIL"));
            var aborted = Cell("Joust", 1, 100f);
            aborted.completed = false;
            Assert.That(LoadSweepTable.Verdict(aborted, target), Is.EqualTo("FAIL"), "a fast abort is not a pass");
            Assert.That(LoadSweepTable.Verdict(new LoadSweepCell(), target), Is.EqualTo("NO DATA"));
            Assert.That(LoadSweepTable.Verdict(null, target), Is.EqualTo("NO DATA"));
        }

        [Test]
        public void CellFromReport_ReadsTheReport()
        {
            var report = new LoadInsightReport
            {
                gameMode = "Rampage", intensity = 1, totalPlayers = 4, aiBackfill = 3, sceneTo = "MinigameRampage",
                totalMs = 31_500f, visualReadyMs = 9_000f, unattributedMs = 2_000f, worstFrameMs = 410f,
                completionReason = "Loaded — arena complete",
                topCosts = new List<LoadTopCost> { new() { label = "Streamed prism lay", exclusiveMs = 20_000f } },
                counters = new List<LoadCounter> { new() { name = "Prisms laid during load", value = 59_590 } },
                errors = new List<SweepError> { new() { type = "Error", message = "x" } }
            };

            var cell = LoadSweepTable.CellFromReport(report, 2);
            Assert.That(cell.recorded, Is.True);
            Assert.That(cell.completed, Is.True);
            Assert.That(cell.mode, Is.EqualTo("Rampage"));
            Assert.That(cell.intensity, Is.EqualTo(1));
            Assert.That(cell.aiBackfill, Is.EqualTo(3));
            Assert.That(cell.topCost, Is.EqualTo("Streamed prism lay"));
            Assert.That(cell.topCostMs, Is.EqualTo(20_000f));
            Assert.That(cell.prismsLaid, Is.EqualTo(59_590L));
            Assert.That(cell.errorCount, Is.EqualTo(1));
            Assert.That(cell.repeat, Is.EqualTo(2));

            report.completionReason = "Aborted: returned to menu";
            Assert.That(LoadSweepTable.CellFromReport(report, 1).completed, Is.False);
            report.completionReason = "Loaded";
            report.interrupted = true;
            Assert.That(LoadSweepTable.CellFromReport(report, 1).completed, Is.False);
            Assert.That(LoadSweepTable.CellFromReport(null, 1).recorded, Is.False);
        }

        [Test]
        public void BuildMarkdown_ListsWorstFirstWithVerdictsAndCounts()
        {
            var summary = new LoadSweepSummary
            {
                timestamp = "20261010_120000",
                origin = "DevBuild",
                platform = "WindowsPlayer",
                playersPolicy = "4",
                hasBoot = true,
                boot = Cell("Cold boot", 0, 7_200f),
                cells = new List<LoadSweepCell>
                {
                    Cell("Joust", 1, 8_000f),
                    Cell("Scurry", 4, 45_000f),
                    new LoadSweepCell { mode = "Cleave", intensity = 2, recorded = false }
                }
            };

            string md = LoadSweepTable.BuildMarkdown(summary);
            int cleave = md.IndexOf("| Cleave |", System.StringComparison.Ordinal);
            int scurry = md.IndexOf("| Scurry |", System.StringComparison.Ordinal);
            int joust = md.IndexOf("| Joust |", System.StringComparison.Ordinal);
            Assert.That(cleave, Is.GreaterThan(0));
            Assert.That(cleave, Is.LessThan(scurry), "no data outranks every timed row");
            Assert.That(scurry, Is.LessThan(joust), "the longest load comes before the shortest");
            Assert.That(md, Does.Contain("**1 pass · 1 fail · 1 no data**"));
            Assert.That(md, Does.Contain("| PASS | 7.2 s |"), "the boot row reads its own target");
            Assert.That(md, Does.Contain("+25.0 s"), "a failing row says how far over it is");
        }

        [Test]
        public void BuildMarkdown_AnEarlyStopAndAnAbortedBootSayWhy()
        {
            var boot = Cell("Cold boot", 0, 240_500f);
            boot.completed = false;
            boot.completionReason = "Aborted: sweep: the menu was not ready within 240s";
            var summary = new LoadSweepSummary
            {
                timestamp = "20261012_120000",
                hasBoot = true,
                boot = boot,
                stopReason = "The menu was not ready within 240s of the cold boot (active scene 'Authentication'), so no cell was launched."
            };

            string md = LoadSweepTable.BuildMarkdown(summary);
            Assert.That(md, Does.Contain("**Stopped early:** The menu was not ready within 240s of the cold boot"));
            Assert.That(md, Does.Contain("| FAIL | 240.5 s |"), "an aborted boot is a FAIL with its time, not NO DATA");
            Assert.That(md, Does.Contain("Aborted: sweep: the menu was not ready within 240s |"),
                "the boot row carries the abort reason");
            Assert.That(md, Does.Contain("0 cells:"));

            summary.stopReason = "";
            Assert.That(LoadSweepTable.BuildMarkdown(summary), Does.Not.Contain("Stopped early"),
                "a sweep that ran to the end has no stop line");
        }

        [Test]
        public void CardsFromRosters_ReadsBothRostersOnce_AndSkipsScenesNotInTheBuild()
        {
            var created = new List<Object>();
            T Make<T>(string name) where T : ScriptableObject
            {
                var so = ScriptableObject.CreateInstance<T>();
                so.name = name;
                created.Add(so);
                return so;
            }
            SO_ArcadeGame Card(string name, string scene)
            {
                var card = Make<SO_ArcadeGame>(name);
                card.SceneName = scene;
                return card;
            }

            try
            {
                var skim = Card("SkimRace", "MinigameSkimRace");
                var joust = Card("Joust", "MinigameJoust_Gameplay");
                var retired = Card("BlockBandit", "MinigameBlockBandit");
                var blank = Card("NoScene", "");
                var arenaCard = Card("Arena", "MinigameArena");
                var arcade = Make<SO_GameList>("ArcadeGames");
                arcade.Games = new List<SO_ArcadeGame> { skim, null, retired, joust, skim, blank };
                var arena = Make<SO_GameList>("ArenaGames");
                arena.Games = new List<SO_ArcadeGame> { arenaCard, joust };
                var empty = Make<SO_GameList>("Empty");
                empty.Games = null;

                var inBuild = new HashSet<string> { "MinigameSkimRace", "MinigameJoust_Gameplay", "MinigameArena" };
                var skipped = new List<SO_ArcadeGame>();
                var cards = LoadSweepRunner.CardsFromRosters(new[] { arcade, null, empty, arena }, inBuild.Contains, skipped);

                Assert.That(cards, Is.EqualTo(new[] { skim, joust, arenaCard }),
                    "both rosters, roster order, each card once, null rosters and entries ignored");
                Assert.That(skipped, Is.EqualTo(new[] { retired, blank }),
                    "a card whose scene the build cannot load is skipped, not planned");
            }
            finally
            {
                foreach (var so in created) Object.DestroyImmediate(so);
            }
        }

        static LoadSweepCell Cell(string mode, int intensity, float totalMs) => new()
        {
            mode = mode,
            intensity = intensity,
            players = 4,
            aiBackfill = 3,
            recorded = true,
            completed = true,
            totalMs = totalMs,
            completionReason = "Loaded — arena complete"
        };
    }
}
