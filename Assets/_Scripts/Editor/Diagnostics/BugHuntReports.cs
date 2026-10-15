using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The diagnostic FILES a play test leaves behind, gathered so "Send to Claude"
    /// (<see cref="BugHuntAutoSend"/>) carries them with the console errors. Three producers, all
    /// already running on every machine:
    ///
    /// <list type="bullet">
    /// <item><b>redlog</b> — every red Console entry of a play session, with context
    /// (<c>Logs/RedConsole/redconsole_*.log</c>, Yash's <c>RedConsoleLog</c>).</item>
    /// <item><b>crash</b> — the Crash Detector's abnormal-exit reports (<c>Logs/CrashDetector/Crash-*.log</c>).</item>
    /// <item><b>perf</b> — the DiagnosticsHUD's JSON reports and captures
    /// (<c>Documents/CosmicShore Diagnostics/*.json</c>).</item>
    /// </list>
    ///
    /// Each lands at <c>BugLedger/shared/reports/&lt;user&gt;/&lt;kind&gt;/&lt;file&gt;</c> on the sender's
    /// <c>bugledger/&lt;user&gt;</c> branch. "New" means not on that branch yet, so nothing is sent twice.
    /// Bounded on purpose — recent files only, a size cap per file, a count cap per send, newest
    /// first: the branch carries evidence, not an archive.
    /// </summary>
    public static class BugHuntReports
    {
        const long MaxFileBytes = 512 * 1024;
        const int MaxFilesPerSend = 12;
        const int MaxAgeDays = 14;

        /// <summary>One file to send: where it is on disk, where it goes in the branch.</summary>
        public readonly struct Report
        {
            public readonly string Source;
            public readonly string RepoPath;
            public readonly string Kind;

            public Report(string source, string repoPath, string kind)
            {
                Source = source;
                RepoPath = repoPath;
                Kind = kind;
            }
        }

        /// <summary>
        /// Main thread only (resolves <c>DiagnosticsHUD.OutputDirectory</c>, which may fall back to
        /// <c>Application.persistentDataPath</c>). Every recent candidate, newest first, uncapped —
        /// the sender drops the ones its branch already has, then applies <see cref="Cap"/>.
        /// Never throws: a missing or unreadable folder contributes nothing.
        /// </summary>
        public static List<Report> Candidates(string reportsRepoDir, string user)
        {
            var candidates = new List<(FileInfo file, string kind)>();
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var cutoff = DateTime.UtcNow.AddDays(-MaxAgeDays);

            Collect(candidates, Path.Combine(projectRoot, "Logs", "RedConsole"), "redconsole_*.log", "redlog", cutoff);
            Collect(candidates, Path.Combine(projectRoot, "Logs", "CrashDetector"), "Crash-*.log", "crash", cutoff);
            string perfDir = null;
            try { perfDir = DiagnosticsHUD.OutputDirectory; } catch { /* no Documents folder */ }
            if (perfDir != null) Collect(candidates, perfDir, "*.json", "perf", cutoff);

            candidates.Sort((a, b) => b.file.LastWriteTimeUtc.CompareTo(a.file.LastWriteTimeUtc));
            var reports = new List<Report>(candidates.Count);
            foreach (var (file, kind) in candidates)
                reports.Add(new Report(file.FullName, $"{reportsRepoDir}/{Slug(user)}/{kind}/{file.Name}", kind));
            return reports;
        }

        /// <summary>The newest <see cref="MaxFilesPerSend"/> of <paramref name="unsent"/>.</summary>
        public static List<Report> Cap(List<Report> unsent)
            => unsent.Count <= MaxFilesPerSend ? unsent : unsent.GetRange(0, MaxFilesPerSend);

        public static string Slug(string s)
        {
            if (string.IsNullOrEmpty(s)) return "unknown";
            var chars = s.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-') chars[i] = '-';
            return new string(chars);
        }

        static void Collect(List<(FileInfo, string)> into, string dir, string pattern, string kind, DateTime cutoff)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var path in Directory.GetFiles(dir, pattern))
                {
                    var info = new FileInfo(path);
                    if (info.Length == 0 || info.Length > MaxFileBytes) continue;
                    if (info.LastWriteTimeUtc < cutoff) continue;
                    into.Add((info, kind));
                }
            }
            catch { /* unreadable folder contributes nothing */ }
        }
    }
}
