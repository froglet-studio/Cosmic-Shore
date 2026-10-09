using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    /// <summary>
    /// The log half of the <c>burst</c> console command. The lines are the 2026-10-08 Editor.log's own (trimmed after
    /// the job name): Burst named the MathF InternalCalls it could not link, then disabled every Assembly-CSharp job.
    /// The load-bearing test is the reload one: Editor.log keeps the whole editor session, so a refusal from before a
    /// fix must not be reported as current.
    /// </summary>
    [TestFixture]
    public class BurstProbeTests
    {
        const string Reload = "Domain Reload Profiling: 96269ms";
        const string Sqrt = "Unable to find internal function `System.MathF::Sqrt`";
        const string Sin = "Unable to find internal function `System.MathF::Sin`";
        const string CellVolume =
            "Burst is disabled for `Unity.Jobs.IJobExtensions+JobStruct`1[[CosmicShore.Gameplay.CellVolumeSumJob, " +
            "Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], UnityEngine.CoreModule::Execute(...)` " +
            "due to a failure to resolve one or more `extern` methods called by this entry-point.";
        const string WriteLocalToWorld =
            "Burst is disabled for `Unity.Jobs.IJobParallelForExtensions+ParallelForJobStruct`1[[CosmicShore.ECS." +
            "PrismRenderService+WriteLocalToWorldJob, Assembly-CSharp, Version=0.0.0.0]]::Execute(...)` " +
            "due to a failure to resolve one or more `extern` methods called by this entry-point.";

        [Test]
        public void Refusal_NamesTheJobs_TheReason_AndTheUnresolvedExterns()
        {
            string r = BurstProbe.LogReasons(new[] { Reload, "noise", Sqrt, Sin, Sqrt, CellVolume, WriteLocalToWorld });

            StringAssert.Contains("Burst disabled 2 job(s) [CellVolumeSumJob, PrismRenderService+WriteLocalToWorldJob]", r);
            StringAssert.Contains("due to a failure to resolve one or more `extern` methods called by this entry-point", r);
            StringAssert.Contains("unresolved InternalCall(s): System.MathF::Sin, System.MathF::Sqrt", r,
                "each extern once, sorted - Burst logs the same one from every library that calls it");
        }

        [Test]
        public void RefusalBeforeTheLastReload_IsCounted_NotReportedAsCurrent()
        {
            string stale = BurstProbe.LogReasons(new[] { Sqrt, CellVolume, Reload, "noise" });
            Assert.AreEqual("log: no Burst refusal since the last domain reload (2 before it)", stale);

            // negative control: the same lines AFTER the reload are current
            string current = BurstProbe.LogReasons(new[] { Reload, Sqrt, CellVolume });
            StringAssert.Contains("log since the last domain reload: Burst disabled 1 job(s) [CellVolumeSumJob]", current);
        }

        [Test]
        public void CleanLog_SaysSo()
        {
            Assert.AreEqual("log: no Burst refusal", BurstProbe.LogReasons(new[] { Reload, "Burst compiled 15 entry points" }));
        }

        [Test]
        public void ExternsWithoutADisabledLine_AreStillReported()
        {
            Assert.AreEqual("log since the last domain reload: unresolved InternalCall(s): System.MathF::Sqrt",
                BurstProbe.LogReasons(new[] { Sqrt }));
        }
    }
}
