using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CosmicShore.Player
{
    /// <summary>
    /// Starts the other training workers: the same player, the same arguments, plus
    /// --worker I and the shared run directory. Each writes its console to wI.log there;
    /// they exit when worker 0 marks the run done.
    /// </summary>
    static class TrainingWorkers
    {
        static readonly List<Process> s_children = new();

        public static void Spawn(string[] args, int workers, string dir, int seed)
        {
            string self = Environment.ProcessPath;
            string entry = typeof(Program).Assembly.Location;
            bool viaHost = self != null && Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
            for (int w = 1; w < workers; w++)
            {
                var psi = new ProcessStartInfo(self ?? "dotnet")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                if (viaHost) psi.ArgumentList.Add(entry);
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] is "--seed" or "--worker" or "--train-dir") { i++; continue; }
                    psi.ArgumentList.Add(args[i]);
                }
                psi.ArgumentList.Add("--worker"); psi.ArgumentList.Add(w.ToString());
                psi.ArgumentList.Add("--seed"); psi.ArgumentList.Add(seed.ToString());
                psi.ArgumentList.Add("--train-dir"); psi.ArgumentList.Add(dir);
                var log = new StreamWriter(Path.Combine(dir, $"w{w}.log")) { AutoFlush = true };
                var p = new Process { StartInfo = psi };
                p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                s_children.Add(p);
                Console.WriteLine($"[train] started worker {w} (pid {p.Id})");
            }
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                foreach (var p in s_children)
                {
                    try { if (!p.WaitForExit(15000)) p.Kill(); } catch { }
                }
            };
        }
    }
}
