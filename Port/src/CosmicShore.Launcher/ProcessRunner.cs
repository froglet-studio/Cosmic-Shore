using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>One line of launcher output, tagged by where it came from.</summary>
    public readonly record struct LogLine(DateTime Time, LogKind Kind, string Text);

    public enum LogKind { Info, Command, Output, Error, Success, Warn }

    /// <summary>
    /// A bounded, thread-safe log every job writes into and the console page draws from.
    /// Background jobs append from worker threads; the UI copies a snapshot once per frame.
    /// </summary>
    public sealed class LogBuffer
    {
        const int Capacity = 6000;
        readonly List<LogLine> _lines = new();
        readonly object _gate = new();
        public int Version { get; private set; }
        /// <summary>Also print every line to stdout (scripted runs and CI).</summary>
        public bool Echo { get; set; }

        public void Add(LogKind kind, string text)
        {
            if (text == null) return;
            lock (_gate)
            {
                foreach (var part in text.Replace("\r\n", "\n").Split('\n'))
                {
                    if (part.Length == 0 && kind == LogKind.Output) continue;
                    _lines.Add(new LogLine(DateTime.Now, kind, part.TrimEnd('\r')));
                    if (Echo) Console.WriteLine($"[{kind}] {part.TrimEnd('\r')}");
                }
                if (_lines.Count > Capacity) _lines.RemoveRange(0, _lines.Count - Capacity);
                Version++;
            }
        }

        public void CopyTo(List<LogLine> target)
        {
            lock (_gate) { target.Clear(); target.AddRange(_lines); }
        }

        /// <summary>The last <paramref name="n"/> lines' text.</summary>
        public List<string> Tail(int n) { lock (_gate) return _lines.Skip(Math.Max(0, _lines.Count - n)).Select(l => l.Text).ToList(); }

        public string LastLine
        {
            get { lock (_gate) return _lines.Count == 0 ? "" : _lines[^1].Text; }
        }

        public void Clear() { lock (_gate) { _lines.Clear(); Version++; } }

        public string AllText()
        {
            var sb = new StringBuilder();
            lock (_gate) foreach (var l in _lines) sb.Append(l.Time.ToString("HH:mm:ss")).Append("  ").AppendLine(l.Text);
            return sb.ToString();
        }
    }

    /// <summary>Runs external tools (git, dotnet, the player) and streams their output into the log.</summary>
    public static class ProcessRunner
    {
        static readonly System.Text.RegularExpressions.Regex GitProgress =
            new(@"^(remote: )?[A-Za-z ]+:\s+\d{1,2}% \(", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>git's percentage lines drive the progress bar and compiler warnings are not a tester's business; neither goes to the log.</summary>
        static bool Noise(string line) => GitProgress.IsMatch(line) || line.Contains(": warning ");

        public sealed record Result(int ExitCode, string StdOut, string StdErr);

        public static async Task<Result> Run(string file, IEnumerable<string> args, string? workDir, LogBuffer? log,
            CancellationToken ct, IDictionary<string, string>? env = null, Action<string>? onLine = null, bool quiet = false, bool closeStdin = false)
        {
            var psi = new ProcessStartInfo(file)
            {
                RedirectStandardInput = closeStdin,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            if (!string.IsNullOrEmpty(workDir)) psi.WorkingDirectory = workDir;
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            psi.Environment["DOTNET_NOLOGO"] = "1";
            if (env != null) foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;

            if (!quiet) log?.Add(LogKind.Command, "> " + Describe(file, psi.ArgumentList));

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                stdout.AppendLine(e.Data);
                if (!quiet && !Noise(e.Data)) log?.Add(LogKind.Output, e.Data);
                onLine?.Invoke(e.Data);
            };
            p.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                stderr.AppendLine(e.Data);
                if (!quiet && !Noise(e.Data)) log?.Add(LogKind.Output, e.Data);
                onLine?.Invoke(e.Data);
            };
            try { p.Start(); }
            catch (Exception ex)
            {
                if (!quiet) log?.Add(LogKind.Error, $"could not start {file}: {ex.Message}");
                return new Result(-1, "", ex.Message);
            }
            if (closeStdin) p.StandardInput.Close(); // nothing to read: a prompt fails instead of waiting forever
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            try { await p.WaitForExitAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
                throw;
            }
            p.WaitForExit(); // flush the async readers
            return new Result(p.ExitCode, stdout.ToString(), stderr.ToString());
        }

        /// <summary>Runs a tool quietly and returns trimmed stdout, or null on any failure.</summary>
        public static string? Capture(string file, params string[] args)
        {
            try
            {
                var r = Run(file, args, null, null, CancellationToken.None, quiet: true).GetAwaiter().GetResult();
                return r.ExitCode == 0 ? r.StdOut.Trim() : null;
            }
            catch { return null; }
        }

        static string Describe(string file, IEnumerable<string> args)
        {
            var sb = new StringBuilder(System.IO.Path.GetFileName(file));
            foreach (var a in args) sb.Append(' ').Append(a.Contains(' ') ? $"\"{a}\"" : a);
            return sb.ToString();
        }
    }
}
