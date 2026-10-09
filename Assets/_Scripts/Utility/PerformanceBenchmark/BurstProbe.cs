#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The DiagnosticsHUD <c>burst</c> console command: are THIS project's Burst jobs running as Burst?
    ///
    /// <para>Why it exists: from 2026-10-07 every <c>[BurstCompile]</c> job in Assembly-CSharp ran as
    /// managed code in the editor (<c>prof</c>'s managed-job flag; <c>Docs/SKIM_RACE_AI.md</c> §8.0j).
    /// That persisted across sessions, with Synchronous Compilation on and with no Burst error.
    /// The Burst Inspector compiled every one of those jobs, and package jobs still ran as Burst, so this asks from
    /// inside the running game.</para>
    ///
    /// <para>It prints Burst's runtime switches and the editor state that can turn Burst off for user
    /// code, then runs one probe job in this assembly with <c>CompileSynchronously</c>, both
    /// <c>Run()</c> on the main thread and <c>Schedule()</c> on a worker. The probe reports how it
    /// actually ran: a <c>[BurstDiscard]</c> method (Burst's own detection idiom, see
    /// <c>BurstCompiler.IsCompiledByBurst</c>) only executes when the code is NOT Burst-compiled.</para>
    ///
    /// <para>Then it prints Burst's own reason from the editor log (<see cref="LogReasons"/>). Burst writes that reason
    /// to Editor.log, while the Burst Inspector compiles the same jobs cleanly and shows nothing, which is why the
    /// 2026-10-07 hunt took a day. On 2026-10-08 the log said
    /// <c>Unable to find internal function `System.MathF::Sqrt`</c> (also Acos/Pow/Exp/Sin/Cos), then
    /// <c>Burst is disabled for ... due to a failure to resolve one or more `extern` methods</c>. MathF's
    /// transcendental members are InternalCalls in Unity's Mono, and Burst cannot link them. Two jobs that landed on
    /// 10-05 called them (SubstrateAgentJob, SwarmPoseJob). Every Assembly-CSharp job shares one Burst library, so
    /// that ran EVERY game job as managed code, this probe included.</para>
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
            sb.Append(" | ").Append(ScanLog());
            return sb.ToString();
        }

        const string ReloadMarker = "Domain Reload Profiling:";
        const string DisabledPrefix = "Burst is disabled for ";
        const string ExternPrefix = "Unable to find internal function ";

        /// <summary>
        /// Burst's refusals in a log, since the log's LAST domain reload: the jobs it disabled, why, and the
        /// InternalCalls it could not resolve. Earlier refusals are only counted, because Editor.log keeps the whole
        /// editor session, and a refusal from before a fix stays in it until the editor restarts. Pure, so the
        /// edit-mode tests feed it lines.
        /// </summary>
        public static string LogReasons(IEnumerable<string> lines)
        {
            var jobs = new SortedSet<string>(StringComparer.Ordinal);
            var externs = new SortedSet<string>(StringComparer.Ordinal);
            string reason = null;
            int sinceReload = 0, earlier = 0;
            foreach (string line in lines)
            {
                if (line.StartsWith(ReloadMarker, StringComparison.Ordinal))
                {
                    earlier += sinceReload;
                    sinceReload = 0;
                    jobs.Clear();
                    externs.Clear();
                    reason = null;
                }
                else if (line.StartsWith(DisabledPrefix, StringComparison.Ordinal))
                {
                    sinceReload++;
                    jobs.Add(JobName(line));
                    int due = line.IndexOf(" due to ", StringComparison.Ordinal);
                    if (reason == null && due >= 0) reason = line.Substring(due + 8).TrimEnd('.');
                }
                else if (line.StartsWith(ExternPrefix, StringComparison.Ordinal))
                {
                    sinceReload++;
                    externs.Add(line.Substring(ExternPrefix.Length).Trim('`', ' '));
                }
            }

            if (sinceReload == 0)
                return earlier > 0
                    ? $"log: no Burst refusal since the last domain reload ({earlier} before it)"
                    : "log: no Burst refusal";
            var sb = new StringBuilder("log since the last domain reload: ");
            if (jobs.Count > 0)
                sb.Append("Burst disabled ").Append(jobs.Count).Append(" job(s) [").Append(string.Join(", ", jobs))
                  .Append("] due to ").Append(reason ?? "an unstated reason");
            if (externs.Count > 0)
                sb.Append(jobs.Count > 0 ? "; " : "").Append("unresolved InternalCall(s): ").Append(string.Join(", ", externs));
            return sb.ToString();
        }

        /// <summary>The job struct's short name out of Burst's entry-point signature: the generic argument of the
        /// job wrapper (<c>JobStruct`1[[CosmicShore.Gameplay.CellVolumeSumJob, Assembly-CSharp ...</c>), else the
        /// first backticked name.</summary>
        static string JobName(string line)
        {
            int open = line.IndexOf("[[", StringComparison.Ordinal);
            if (open >= 0)
            {
                int end = line.IndexOf(',', open);
                if (end > open)
                {
                    string full = line.Substring(open + 2, end - open - 2);
                    return full.Substring(full.LastIndexOf('.') + 1);
                }
            }
            int a = line.IndexOf('`', DisabledPrefix.Length - 1);
            int b = a >= 0 ? line.IndexOf('`', a + 1) : -1;
            return b > a ? line.Substring(a + 1, b - a - 1) : "?";
        }

        static string ScanLog()
        {
            string path = Application.consoleLogPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "log: none to read";
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return LogReasons(ReadLines(reader));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return $"log: {Path.GetFileName(path)} unreadable ({e.Message})";
            }
        }

        static IEnumerable<string> ReadLines(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null) yield return line;
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
