#if UNITY_EDITOR
using CosmicShore.Editor;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The launch smoke test's verdict and files (<see cref="ModeSmokeResult"/>, <see cref="ModeSmokeReport"/>;
    /// Docs/LAUNCH/SMOKE_TEST.md). The smoke itself needs a Unity editor in Play mode; these hold the part that
    /// decides pass or fail, so a result can never pass on facts that do not support it.
    /// </summary>
    public class ModeSmokeReportTests
    {
        static ModeSmokeResult Clean() => new()
        {
            mode = "SkimRace", modeId = 33, scene = "MinigameSkimRace", intensity = 1, players = 3,
            reachedMenu = true, sceneLoaded = true, turnStarted = true, seats = 3, aiSeats = 3, movingSeats = 3,
            gameEnded = true, matchBudgetSeconds = 600f,
        };

        [Test]
        public void CleanRun_Passes()
        {
            var r = Clean();
            r.Evaluate();
            Assert.AreEqual(ModeSmokeResult.Passed, r.status, r.failure);
            Assert.AreEqual("", r.failure);
        }

        [Test]
        public void AnException_Fails_EvenWhenTheGameEnded()
        {
            var r = Clean();
            r.exceptions = 1;
            r.status = ModeSmokeResult.Passed; // a stale claim is overwritten by the facts
            r.Evaluate();
            Assert.AreEqual(ModeSmokeResult.Failed, r.status);
            StringAssert.Contains("exception", r.failure);
        }

        [Test]
        public void EachMissingFact_Fails()
        {
            void Expect(System.Action<ModeSmokeResult> spoil, string words)
            {
                var r = Clean();
                spoil(r);
                r.Evaluate();
                Assert.AreEqual(ModeSmokeResult.Failed, r.status, words);
                StringAssert.Contains(words, r.failure);
            }
            Expect(r => r.inBuild = false, "build settings");
            Expect(r => r.harnessFault = "boom", "the run stopped: boom");
            Expect(r => r.reachedMenu = false, "menu");
            Expect(r => r.sceneLoaded = false, "never loaded");
            Expect(r => r.seats = 0, "no seat");
            Expect(r => r.turnStarted = false, "turn");
            Expect(r => r.aiSeats = 2, "not flown by the AI");
            Expect(r => r.movingSeats = 0, "moved");
            Expect(r => r.gameEnded = false, "game-over");
            Expect(r => r.errors = 2, "error");
        }

        [Test]
        public void Skipped_StaysSkipped()
        {
            var r = new ModeSmokeResult { mode = "Maelstrom", status = ModeSmokeResult.Skipped, skipReason = "chains modes" };
            r.Evaluate();
            Assert.AreEqual(ModeSmokeResult.Skipped, r.status);
        }

        [Test]
        public void Summary_ExitCode_CountsFailuresAndModesThatNeverRan()
        {
            var s = new ModeSmokeSummary();
            var pass = Clean(); pass.Evaluate();
            s.modes.Add(pass);
            s.modes.Add(new ModeSmokeResult { mode = "Maelstrom", status = ModeSmokeResult.Skipped, skipReason = "x" });
            Assert.AreEqual(0, s.ExitCode);
            s.modes.Add(new ModeSmokeResult { mode = "Joust" }); // not_run
            Assert.AreEqual(1, s.ExitCode);
        }

        [Test]
        public void ComponentIds_AreKebabCase()
        {
            Assert.AreEqual("smoke-skim-race", ModeSmokeResult.ComponentId("SkimRace"));
            Assert.AreEqual("smoke-joust", ModeSmokeResult.ComponentId("Joust"));
            Assert.AreEqual("smoke-wildlife-liberation", ModeSmokeResult.ComponentId("WildlifeLiberation"));
        }

        [Test]
        public void Redact_RemovesTokensAndQueries_AndKeepsOneLine()
        {
            string token = new string('A', 48);
            string line = ModeSmokeReport.Redact($"auth failed {token} at https://example.com/x?token=abc\n  at Stack.Frame()");
            StringAssert.DoesNotContain(token, line);
            StringAssert.DoesNotContain("token=abc", line);
            StringAssert.DoesNotContain("Stack.Frame", line);
        }

        [Test]
        public void JUnit_EscapesAndCounts()
        {
            var s = new ModeSmokeSummary { startedAt = "2026-11-05T00:00:00Z" };
            var bad = Clean();
            bad.errors = 1;
            ModeSmokeReport.KeepError(bad, "match", "NullReferenceException <in> \"Foo\" & bar");
            bad.Evaluate();
            s.modes.Add(bad);
            s.modes.Add(new ModeSmokeResult { mode = "Maelstrom", status = ModeSmokeResult.Skipped, skipReason = "a < b" });
            string xml = ModeSmokeReport.JUnit(s);
            StringAssert.Contains("failures=\"1\"", xml);
            StringAssert.Contains("skipped=\"1\"", xml);
            StringAssert.Contains("&lt;in&gt; &quot;Foo&quot; &amp; bar", xml);
            StringAssert.Contains("<skipped message=\"a &lt; b\"/>", xml);
            Assert.DoesNotThrow(() => new System.Xml.XmlDocument().LoadXml(xml));
        }

        [Test]
        public void Json_RoundTrips_TheFactsSmokeReportReads()
        {
            var r = Clean();
            r.Evaluate();
            var back = JsonUtility.FromJson<ModeSmokeResult>(JsonUtility.ToJson(r));
            Assert.AreEqual(r.status, back.status);
            Assert.AreEqual(r.movingSeats, back.movingSeats);
            Assert.AreEqual(r.gameEnded, back.gameEnded);
            Assert.AreEqual(r.inBuild, back.inBuild);
        }
    }
}
#endif
