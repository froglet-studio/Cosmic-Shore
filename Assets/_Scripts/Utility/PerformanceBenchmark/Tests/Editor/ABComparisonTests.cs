using System.Collections.Generic;
using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    /// <summary>
    /// The pure half of the <c>ab</c> console command. The two tests that matter most are the
    /// negative controls: a world that only DRIFTS must not read as an effect once the schedule
    /// is counterbalanced (and demonstrably does read as one when it is not), and two arms that
    /// differ in renderer count BY DESIGN must not raise a drift warning.
    /// </summary>
    [TestFixture]
    public class ABComparisonTests
    {
        static ABComparison.Arm A => ABComparison.Arm.A;
        static ABComparison.Arm B => ABComparison.Arm.B;

        #region Parse

        [Test]
        public void TryParse_TokensAsTheConsoleSplitsThem_RecoversBothCommands()
        {
            // DiagnosticsHUD splits on spaces and keeps the quotes attached to the tokens.
            var tokens = new[] { "\"renderers", "hide", "Spindle\"", "\"renderers", "show\"", "10", "3" };

            Assert.IsTrue(ABComparison.TryParse(tokens, out var req, out string error), error);
            Assert.AreEqual("renderers hide Spindle", req.CommandA);
            Assert.AreEqual("renderers show", req.CommandB);
            Assert.AreEqual(10, req.Seconds);
            Assert.AreEqual(3, req.Rounds);
        }

        [Test]
        public void TryParse_NoNumbers_UsesDefaults()
        {
            Assert.IsTrue(ABComparison.TryParse(new[] { "\"prismpath", "on\"", "\"prismpath", "off\"" }, out var req, out _));
            Assert.AreEqual(ABComparison.DefaultSeconds, req.Seconds);
            Assert.AreEqual(ABComparison.DefaultRounds, req.Rounds);
        }

        [Test]
        public void TryParse_ClampsToTheMaximums()
        {
            Assert.IsTrue(ABComparison.TryParse(new[] { "\"a\"", "\"b\"", "9999", "9999" }, out var req, out _));
            Assert.AreEqual(ABComparison.MaxSeconds, req.Seconds);
            Assert.AreEqual(ABComparison.MaxRounds, req.Rounds);
        }

        // A string[] handed to [TestCase] is spread across the params object[] by array
        // covariance, so these come from a source instead.
        static IEnumerable<TestCaseData> RejectedInputs()
        {
            yield return new TestCaseData((object)new[] { "\"renderers", "hide" }).SetName("TryParse_UnmatchedQuote_Fails");
            yield return new TestCaseData((object)new[] { "\"renderers", "show\"" }).SetName("TryParse_OneCommand_Fails");
            yield return new TestCaseData((object)new[] { "\"a\"", "\"b\"", "ten" }).SetName("TryParse_NonNumber_Fails");
            yield return new TestCaseData((object)new[] { "\"a\"", "\"b\"", "0" }).SetName("TryParse_ZeroSeconds_Fails");
            yield return new TestCaseData((object)new[] { "\"a\"", "\"b\"", "1", "2", "3" }).SetName("TryParse_TooManyNumbers_Fails");
            yield return new TestCaseData((object)new[] { "\"\"", "\"b\"" }).SetName("TryParse_EmptyCommand_Fails");
        }

        [TestCaseSource(nameof(RejectedInputs))]
        public void TryParse_Rejects(string[] tokens)
        {
            Assert.IsFalse(ABComparison.TryParse(tokens, out _, out string error));
            StringAssert.Contains("usage", error);
        }

        [Test]
        public void CommandNameOf_IsTheLowercasedFirstWord()
        {
            Assert.AreEqual("renderers", ABComparison.CommandNameOf("  Renderers hide Spindle"));
            Assert.AreEqual("fps", ABComparison.CommandNameOf("fps"));
            Assert.AreEqual("", ABComparison.CommandNameOf("   "));
        }

        #endregion

        #region Schedule

        [Test]
        public void Schedule_FlipsTheOrderEveryRound()
        {
            CollectionAssert.AreEqual(new[] { A, B, B, A, A, B }, ABComparison.Schedule(3));
        }

        [Test]
        public void Schedule_EachRoundHoldsOneOfEachArm()
        {
            var s = ABComparison.Schedule(5);
            for (int r = 0; r < 5; r++)
                Assert.AreNotEqual(s[2 * r], s[2 * r + 1], $"round {r}");
        }

        /// <summary>
        /// NEGATIVE CONTROL. A world with no effect at all but a steady drift (mass still
        /// growing): every recording reads 0.5 ms more than the one before it. Counterbalanced,
        /// the drift cancels to exactly zero over an even number of rounds. The same readings
        /// taken in a fixed "A then B" order report the drift AS an effect - and call it real,
        /// because the error bar is zero. That second assertion is what proves the first one is
        /// the schedule doing the work, not the statistic being blind.
        /// </summary>
        [Test]
        public void PairedDelta_PureDrift_CancelsWhenCounterbalanced_AndReadsAsAnEffectWhenNot()
        {
            const int rounds = 4;
            const float drift = 0.5f;

            var schedule = ABComparison.Schedule(rounds);
            var a = new List<float>(); var b = new List<float>();
            for (int t = 0; t < schedule.Length; t++)
                (schedule[t] == A ? a : b).Add(20f + drift * t);

            var balanced = ABComparison.PairedDelta(a, b);
            Assert.AreEqual(0f, balanced.Mean, 1e-5f);
            Assert.IsFalse(balanced.IsReal);

            var naiveA = new List<float>(); var naiveB = new List<float>();
            for (int r = 0; r < rounds; r++)
            {
                naiveA.Add(20f + drift * (2 * r));
                naiveB.Add(20f + drift * (2 * r + 1));
            }
            var naive = ABComparison.PairedDelta(naiveA, naiveB);
            Assert.AreEqual(drift, naive.Mean, 1e-5f);
            Assert.IsTrue(naive.IsReal, "the negative control must fire: uncancelled drift looks like a real effect");
        }

        [Test]
        public void PairedDelta_OddRounds_LeaveAThirdOfTheDrift()
        {
            var schedule = ABComparison.Schedule(3);
            var a = new List<float>(); var b = new List<float>();
            for (int t = 0; t < schedule.Length; t++)
                (schedule[t] == A ? a : b).Add(0.9f * t);
            Assert.AreEqual(0.3f, ABComparison.PairedDelta(a, b).Mean, 1e-5f);
        }

        #endregion

        #region Statistics

        [Test]
        public void PairedDelta_ComputesMeanAndStandardError()
        {
            // Deltas B-A = -4, -3, -3.5 → mean -3.5, sd 0.5, se 0.5/sqrt(3).
            var d = ABComparison.PairedDelta(new List<float> { 20f, 21f, 20.5f }, new List<float> { 16f, 18f, 17f });
            Assert.AreEqual(-3.5f, d.Mean, 1e-5f);
            Assert.AreEqual(0.5f / (float)System.Math.Sqrt(3), d.StdErr, 1e-5f);
            Assert.AreEqual(3, d.Rounds);
            Assert.IsTrue(d.IsReal);
        }

        [Test]
        public void PairedDelta_NoiseAroundZero_IsNotReal()
        {
            var d = ABComparison.PairedDelta(new List<float> { 20f, 20f, 20f }, new List<float> { 20.4f, 19.5f, 20.2f });
            Assert.IsFalse(d.IsReal, $"mean {d.Mean} se {d.StdErr}");
        }

        [Test]
        public void PairedDelta_OneRound_IsNeverCalledReal()
        {
            var d = ABComparison.PairedDelta(new List<float> { 30f }, new List<float> { 10f });
            Assert.AreEqual(-20f, d.Mean, 1e-5f);
            Assert.AreEqual(0f, d.StdErr);
            Assert.IsFalse(d.IsReal);
        }

        [Test]
        public void PairedDelta_MismatchedArms_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ABComparison.PairedDelta(new List<float> { 1f, 2f }, new List<float> { 1f }));
        }

        [TestCase(new[] { 1000, 1040 }, false, TestName = "Drifted_FourPercent_IsNotDrift")]
        [TestCase(new[] { 1000, 1060 }, true, TestName = "Drifted_SixPercent_IsDrift")]
        [TestCase(new[] { 1060, 1000, 1020 }, true, TestName = "Drifted_ComparesExtremesNotEnds")]
        [TestCase(new[] { 0, 50 }, true, TestName = "Drifted_FromZero_IsDrift")]
        [TestCase(new[] { 0, 0 }, false, TestName = "Drifted_AllZero_IsNotDrift")]
        [TestCase(new[] { 1000 }, false, TestName = "Drifted_OneReading_IsNotDrift")]
        public void Drifted(int[] readings, bool expected)
        {
            Assert.AreEqual(expected, ABComparison.Drifted(readings, ABComparison.DriftTolerance, out _));
        }

        [Test]
        public void IsFrameTrustworthy_RejectsCappedAndIdleFrames()
        {
            Assert.IsTrue(ABComparison.IsFrameTrustworthy(FrameBoundness.FrameLimit.Cpu));
            Assert.IsTrue(ABComparison.IsFrameTrustworthy(FrameBoundness.FrameLimit.Gpu));
            Assert.IsFalse(ABComparison.IsFrameTrustworthy(FrameBoundness.FrameLimit.AtNamedCap));
            Assert.IsFalse(ABComparison.IsFrameTrustworthy(FrameBoundness.FrameLimit.CappedByIdle));
            Assert.IsFalse(ABComparison.IsFrameTrustworthy(FrameBoundness.FrameLimit.Stalled));
            Assert.IsFalse(ABComparison.IsFrameTrustworthy(FrameBoundness.FrameLimit.IdleNoCap));
        }

        [Test]
        public void ArmAccumulator_TimingAveragesOnlyFramesThatCarriedTiming()
        {
            var acc = new ABComparison.ArmAccumulator();
            acc.Add(20f, 18f, 12f, 9f, 100, 50, 10, 2.0);
            acc.Add(22f, 0f, 0f, 0f, 102, 50, 10, 4.0); // no timing data this frame
            var r = acc.ToRecord(A, 0);

            Assert.AreEqual(2, r.frames);
            Assert.AreEqual(1, r.timedFrames);
            Assert.AreEqual(21f, r.avgFrameMs, 1e-5f);
            Assert.AreEqual(12f, r.avgBusyCpuMs, 1e-5f);
            Assert.AreEqual(9f, r.avgGpuMs, 1e-5f);
            Assert.AreEqual(101f, r.avgDraws, 1e-5f);
            Assert.AreEqual(3f, r.avgGcKbPerFrame, 1e-5f);
            Assert.AreEqual(1, r.round, "records are 1-based for readers");
        }

        #endregion

        #region Summarize

        static ABComparison.ArmRecord Rec(ABComparison.Arm arm, int round, float busy,
            int renderers = 1000, int ents = 5000)
        {
            return new ABComparison.ArmRecord
            {
                arm = arm.ToString(), round = round, frames = 600, timedFrames = 600,
                avgFrameMs = busy + 2f, avgBusyCpuMs = busy, avgGpuMs = 8f, avgDraws = 40f,
                renderersStart = renderers, renderersEnd = renderers,
                prismEntsStart = ents, prismEntsEnd = ents,
                frameLimit = FrameBoundness.FrameLimit.Cpu.ToString(), frameTrustworthy = true,
            };
        }

        static ABComparison.Report FrozenReport(params ABComparison.ArmRecord[] recs)
        {
            var r = new ABComparison.Report { frozenAtStart = true, frozenAtEnd = true, completed = true };
            r.recordings.AddRange(recs);
            return r;
        }

        [Test]
        public void Summarize_PrintsTheVerdictLine()
        {
            var report = FrozenReport(
                Rec(A, 1, 20f), Rec(B, 1, 16f),
                Rec(B, 2, 17f), Rec(A, 2, 20.5f),
                Rec(A, 3, 21f), Rec(B, 3, 18f));

            string line = ABComparison.Summarize(report);

            StringAssert.StartsWith("ab: CPU busy B-A = -3.5 ms ±", line);
            StringAssert.Contains("(3 rounds, frozen: yes)", line);
            StringAssert.DoesNotContain("WARNING", line);
            Assert.AreEqual(-3.5f, report.cpuBusyDelta, 1e-5f);
            CollectionAssert.IsEmpty(report.warnings);
        }

        /// <summary>
        /// NEGATIVE CONTROL for the drift warning. `renderers hide *Spindle` removes tens of thousands of
        /// renderers from arm A BY DESIGN; a warning that compared arms to each other would fire
        /// on every spindle A/B and be ignored. Only the same arm moving over time is drift.
        /// </summary>
        [Test]
        public void Summarize_CountsThatDifferBetweenArmsByDesign_AreNotDrift()
        {
            var report = FrozenReport(
                Rec(A, 1, 12f, renderers: 5000), Rec(B, 1, 16f, renderers: 45000),
                Rec(B, 2, 16f, renderers: 45000), Rec(A, 2, 12f, renderers: 5000));

            ABComparison.Summarize(report);

            CollectionAssert.IsEmpty(report.warnings);
        }

        [Test]
        public void Summarize_TheSameArmMovingOverTime_IsDrift()
        {
            var report = FrozenReport(
                Rec(A, 1, 12f, ents: 5000), Rec(B, 1, 16f, ents: 5000),
                Rec(B, 2, 16f, ents: 5000), Rec(A, 2, 12f, ents: 6000));

            string line = ABComparison.Summarize(report);

            StringAssert.Contains("WARNING", line);
            Assert.That(report.warnings, Has.Some.Contains("across arm A's rounds"));
        }

        [Test]
        public void Summarize_NotFrozen_Warns()
        {
            var report = FrozenReport(Rec(A, 1, 12f), Rec(B, 1, 16f));
            report.frozenAtStart = report.frozenAtEnd = false;

            string line = ABComparison.Summarize(report);

            StringAssert.Contains("frozen: no", line);
            Assert.That(report.warnings, Has.Some.Contains("not frozen"));
        }

        [Test]
        public void Summarize_CappedFrame_Warns()
        {
            var capped = Rec(B, 1, 16f);
            capped.frameTrustworthy = false;
            capped.frameLimit = FrameBoundness.FrameLimit.CappedByIdle.ToString();

            var report = FrozenReport(Rec(A, 1, 12f), capped);
            ABComparison.Summarize(report);

            Assert.That(report.warnings, Has.Some.Contains("CappedByIdle"));
        }

        [Test]
        public void Summarize_IncompleteRound_IsLeftOutOfTheStatistics()
        {
            var report = FrozenReport(Rec(A, 1, 20f), Rec(B, 1, 16f), Rec(B, 2, 99f));
            report.completed = false;

            string line = ABComparison.Summarize(report);

            Assert.AreEqual(-4f, report.cpuBusyDelta, 1e-5f);
            StringAssert.Contains("(1 round", line);
            StringAssert.Contains("INCOMPLETE", line);
        }

        #endregion
    }
}
