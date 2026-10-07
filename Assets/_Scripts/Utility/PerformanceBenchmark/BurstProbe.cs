#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The DiagnosticsHUD <c>burst</c> console command: are THIS project's Burst jobs running as Burst?
    ///
    /// <para>Why it exists: from 2026-10-07 every <c>[BurstCompile]</c> job in Assembly-CSharp ran as
    /// managed code in the editor (<c>prof</c>'s managed-job flag; <c>Docs/SKIM_RACE_AI.md</c> §8.0j).
    /// That persisted across sessions, with Synchronous Compilation on and with no Burst error.
    /// The Burst Inspector compiled every one of those jobs, and package jobs still ran as Burst. Nothing
    /// outside the running game could say why, so this asks from inside it.</para>
    ///
    /// <para>It prints Burst's runtime switches and the editor state that can turn Burst off for user
    /// code, then runs one probe job in this assembly with <c>CompileSynchronously</c>, both
    /// <c>Run()</c> on the main thread and <c>Schedule()</c> on a worker. The probe reports how it
    /// actually ran: a <c>[BurstDiscard]</c> method (Burst's own detection idiom, see
    /// <c>BurstCompiler.IsCompiledByBurst</c>) only executes when the code is NOT Burst-compiled.</para>
    ///
    /// <para>A READER: it changes no setting and has no ship panel (Docs/TOOLING.md).</para>
    /// </summary>
    public static class BurstProbe
    {
        public const string CommandName = "burst";

        const int Burst = 1, Managed = 2;

        [BurstCompile(CompileSynchronously = true)]
        struct ProbeJob : IJob
        {
            public NativeArray<int> Ran;

            public void Execute()
            {
                bool burst = true;
                MarkManaged(ref burst);
                Ran[0] = burst ? Burst : Managed;
            }

            [BurstDiscard]
            static void MarkManaged(ref bool burst) => burst = false;
        }

        /// <summary>The console handler. No arguments.</summary>
        public static string Handle(string[] args) => Report();

        public static string Report()
        {
            var sb = new StringBuilder("burst:");
            sb.Append(" enabled=").Append(BurstCompiler.IsEnabled);
            sb.Append(" compilation=").Append(BurstCompiler.Options.EnableBurstCompilation);
            sb.Append(" synchronous=").Append(BurstCompiler.Options.EnableBurstCompileSynchronously);
            sb.Append(" jobCompiler=").Append(JobsUtility.JobCompilerEnabled);
            sb.Append(" jobsDebugger=").Append(JobsUtility.JobDebuggerEnabled);
            sb.Append(" debuggerAttached=").Append(System.Diagnostics.Debugger.IsAttached);
#if UNITY_EDITOR
            sb.Append(" code=").Append(UnityEditor.Compilation.CompilationPipeline.codeOptimization);
#endif
            sb.Append(" | probe job in Assembly-CSharp: Run() ").Append(Probe(run: true));
            sb.Append(", Schedule() ").Append(Probe(run: false));
            return sb.ToString();
        }

        static string Probe(bool run)
        {
            var ran = new NativeArray<int>(1, Allocator.TempJob);
            try
            {
                var job = new ProbeJob { Ran = ran };
                if (run) job.Run();
                else job.Schedule().Complete();
                return ran[0] switch
                {
                    Burst => "BURST",
                    Managed => "MANAGED",
                    _ => "did not run",
                };
            }
            finally
            {
                ran.Dispose();
            }
        }
    }
}
#endif
