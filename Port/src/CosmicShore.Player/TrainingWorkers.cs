using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CosmicShore.Player
{
    /// <summary>
    /// The launching process of a train run: a supervisor that flies nothing. It starts every
    /// worker (0..N-1) as a child — the same player, the same arguments, plus --worker I, the
    /// shared seed and run directory — echoes worker 0's console, and writes each worker's
    /// console to wI.log. A worker that exits with <see cref="RecycleExitCode"/> has
    /// checkpointed its (identical, evolved) population at a generation boundary to shed
    /// memory; it is restarted with --resume on that checkpoint. The run ends when every
    /// worker has exited cleanly (worker 0 marks it done; the rest follow).
    ///
    /// <para>Every worker also checkpoints at every generation boundary, so a run that died
    /// (the OS killed a worker, the box rebooted) resumes with <c>--resume-run</c>: the same
    /// command, which keeps the run directory, deletes the result files of the generation that
    /// was in flight (a worker that had already written its slice would otherwise be merged
    /// against the others' re-flown slices), and starts every worker from its own checkpoint.
    /// Every worker needs one: the merge requires all of them in the same generation.</para>
    /// </summary>
    static class TrainingWorkers
    {
        public const int RecycleExitCode = 75;
        static readonly List<Process> s_live = new();

        public static int Supervise(string[] args, int workers, string dir, int seed, bool resumeRun = false)
        {
            var procs = new Process[workers];
            var finished = new bool[workers];
            var restarts = new int[workers];
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                lock (s_live)
                    foreach (var p in s_live)
                        try { if (!p.HasExited && !p.WaitForExit(15000)) p.Kill(); } catch { }
            };
            string[] checkpoints = null;
            if (resumeRun)
            {
                int generation = Prisma.Training.TrainingRunResume.Prepare(dir, workers, out checkpoints, out string refusal);
                if (checkpoints == null) { Console.WriteLine("[train] cannot resume: " + refusal); return 2; }
                Console.WriteLine($"[train] resuming the run in {dir} from generation {generation} ({workers} checkpoint(s))");
            }
            for (int w = 0; w < workers; w++) procs[w] = Start(args, w, dir, seed, checkpoints?[w]);

            while (true)
            {
                Thread.Sleep(250);
                int done = 0;
                for (int w = 0; w < workers; w++)
                {
                    if (finished[w]) { done++; continue; }
                    var p = procs[w];
                    if (!p.HasExited) continue;
                    p.WaitForExit(); // drain the output readers
                    int code = p.ExitCode;
                    if (code == RecycleExitCode)
                    {
                        restarts[w]++;
                        Console.WriteLine($"[train] worker {w} recycled (restart {restarts[w]})");
                        procs[w] = Start(args, w, dir, seed, Path.Combine(dir, $"ckpt_w{w}.json"));
                        continue;
                    }
                    if (code != 0)
                    {
                        Console.WriteLine($"[train] worker {w} failed (exit {code}); see {Path.Combine(dir, $"w{w}.log")} — stopping the run");
                        File.WriteAllText(Path.Combine(dir, "done"), "failed");
                        foreach (var q in procs) try { if (!q.HasExited && !q.WaitForExit(15000)) q.Kill(); } catch { }
                        return code;
                    }
                    finished[w] = true;
                    done++;
                }
                if (done == workers) return 0;
            }
        }

        static Process Start(string[] args, int w, string dir, int seed, string resume)
        {
            string self = Environment.ProcessPath;
            string entry = typeof(Program).Assembly.Location;
            bool viaHost = self != null && Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
            var psi = new ProcessStartInfo(self ?? "dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (viaHost) psi.ArgumentList.Add(entry);
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is "--seed" or "--worker" or "--train-dir" or "--resume") { i++; continue; }
                if (args[i] is "--resume-run") continue; // the supervisor's own flag; a worker resumes through --resume
                psi.ArgumentList.Add(args[i]);
            }
            psi.ArgumentList.Add("--worker"); psi.ArgumentList.Add(w.ToString());
            psi.ArgumentList.Add("--seed"); psi.ArgumentList.Add(seed.ToString());
            psi.ArgumentList.Add("--train-dir"); psi.ArgumentList.Add(dir);
            if (resume != null) { psi.ArgumentList.Add("--resume"); psi.ArgumentList.Add(resume); }
            var log = new StreamWriter(Path.Combine(dir, $"w{w}.log"), append: true) { AutoFlush = true };
            bool echo = w == 0;
            var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (_, e) => { if (e.Data == null) return; lock (log) log.WriteLine(e.Data); if (echo) Console.WriteLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data == null) return; lock (log) log.WriteLine(e.Data); if (echo) Console.Error.WriteLine(e.Data); };
            p.Exited += (_, _) => { lock (log) log.Flush(); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            lock (s_live) s_live.Add(p);
            Console.WriteLine($"[train] started worker {w} (pid {p.Id}){(resume != null ? " from checkpoint" : "")}");
            return p;
        }
    }
}
