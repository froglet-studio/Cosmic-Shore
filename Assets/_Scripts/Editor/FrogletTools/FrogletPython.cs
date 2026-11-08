using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>Outcome of one python run.</summary>
    public readonly struct FrogletPythonResult
    {
        public readonly int ExitCode;
        public readonly string StdOut;
        public readonly string StdErr;
        public readonly bool Started;

        public FrogletPythonResult(bool started, int exitCode, string stdOut, string stdErr)
        {
            Started = started;
            ExitCode = exitCode;
            StdOut = stdOut ?? string.Empty;
            StdErr = stdErr ?? string.Empty;
        }

        public bool Ok => Started && ExitCode == 0;
    }

    /// <summary>
    /// Runs the repo's python tools (<c>Tools/Build/*.py</c>) from an editor window, so a rule lives once, in
    /// python, and the window only draws its answer. The lookup is the QA Session window's: the first of
    /// <c>python3</c>, <c>python</c>, <c>py -3</c> that really reports Python 3 (Windows ships stub
    /// <c>python.exe</c> aliases that start and only print a Microsoft Store hint).
    ///
    /// <para>Safe to call off the main thread once <see cref="FrogletGit.TopLevel"/> has been read on it
    /// (scripts run from the repo root).</para>
    /// </summary>
    public static class FrogletPython
    {
        public const string DownloadUrl = "https://www.python.org/downloads/";

        static bool _searched;
        static string _exe;
        static string _prefix;
        static string _version;

        /// <summary>The interpreter, or null when none was found.</summary>
        public static string Executable { get { Find(); return _exe; } }

        /// <summary>"Python 3.x.y", or null.</summary>
        public static string Version { get { Find(); return _version; } }

        public static bool IsAvailable => Executable != null;

        /// <summary>Forget the lookup (after the person installs Python).</summary>
        public static void Reset() => _searched = false;

        static void Find()
        {
            if (_searched) return;
            _searched = true;
            _exe = _prefix = _version = null;
            foreach (var candidate in new[] { "python3|", "python|", "py|-3" })
            {
                var parts = candidate.Split('|');
                var r = Start(parts[0], (parts[1] + " --version").Trim(), 20000, null, null);
                if (!r.Ok) continue;
                var reported = (r.StdOut + " " + r.StdErr).Trim();
                if (!reported.StartsWith("Python 3.", StringComparison.Ordinal)) continue;
                _exe = parts[0];
                _prefix = parts[1];
                _version = reported.Split('\n')[0].Trim();
                return;
            }
        }

        /// <summary>
        /// Run a repo script (repo-relative path) with arguments. <paramref name="onLine"/>, when given, is called
        /// for each stdout line as it arrives, on a worker thread.
        /// </summary>
        public static FrogletPythonResult RunScript(string script, IEnumerable<string> args, int timeoutMs,
                                                    Action<string> onLine = null)
        {
            Find();
            if (_exe == null)
                return new FrogletPythonResult(false, -1, string.Empty, "Python 3 is not installed (" + DownloadUrl + ").");

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_prefix)) sb.Append(_prefix).Append(' ');
            sb.Append(Quote(script));
            if (args != null)
                foreach (var a in args)
                    sb.Append(' ').Append(Quote(a));
            return Start(_exe, sb.ToString(), timeoutMs, FrogletGit.TopLevel, onLine);
        }

        static FrogletPythonResult Start(string exe, string args, int timeoutMs, string cwd, Action<string> onLine)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                if (!string.IsNullOrEmpty(cwd)) psi.WorkingDirectory = cwd;
                // Both ends of the pipe agree on UTF-8 (python on Windows otherwise writes the ANSI codepage).
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["PYTHONUTF8"] = "1";
                psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                using var p = new Process { StartInfo = psi };
                var so = new StringBuilder();
                var se = new StringBuilder();
                p.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    lock (so) so.AppendLine(e.Data);
                    onLine?.Invoke(e.Data);
                };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (se) se.AppendLine(e.Data); };
                if (!p.Start()) return new FrogletPythonResult(false, -1, string.Empty, exe + " did not start.");
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch (Exception) { /* already gone */ }
                    lock (so) lock (se)
                        return new FrogletPythonResult(true, -1, so.ToString(), se + "\ntimed out after " + timeoutMs / 1000 + "s.");
                }
                p.WaitForExit(); // flushes the async readers
                lock (so) lock (se)
                    return new FrogletPythonResult(true, p.ExitCode, so.ToString(), se.ToString());
            }
            catch (Exception e)
            {
                return new FrogletPythonResult(false, -1, string.Empty, e.Message);
            }
        }

        static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
            return "\"" + a.Replace("\"", "\\\"") + "\"";
        }
    }
}
