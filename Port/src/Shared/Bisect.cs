#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Prisma.Bisect
{
    /// <summary>
    /// How one bisect candidate is judged (ROADMAP C1b). The answer is a <c>git bisect run</c> exit
    /// code: 0 good, 1 bad, 125 skip (the candidate cannot be judged - it does not build, or its
    /// engine predates the check), and anything from 128 up aborts the bisect (the harness itself
    /// broke, so no verdict on any commit is trustworthy).
    /// </summary>
    public static class BisectVerdict
    {
        public const int Good = 0, Bad = 1, Skip = 125, Abort = 128;

        /// <summary>
        /// Whether a problem text carries the regression's signature: <c>/regex/</c> (case-insensitive)
        /// or a plain substring (ordinal).
        /// </summary>
        public static bool Matches(string signature, string text)
        {
            if (signature.Length > 2 && signature[0] == '/' && signature[^1] == '/')
                return Regex.IsMatch(text, signature[1..^1], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return text.Contains(signature, StringComparison.Ordinal);
        }

        /// <summary>
        /// An engine_smoke run. With a signature, bad means some error, exception, assert or crash
        /// (ignored errors excluded) carries it - other noise in an old commit does not count. With
        /// none, bad means the smoke failed.
        /// </summary>
        public static int Smoke(bool buildFailed, bool pass, IEnumerable<string> problems, string signature)
        {
            if (buildFailed) return Skip;
            if (signature.Length == 0) return pass ? Good : Bad;
            return problems.Any(p => Matches(signature, p)) ? Bad : Good;
        }

        /// <summary>
        /// A replay diff. <paramref name="capable"/> is false when the candidate's player predates
        /// the replay flags (--replay, --random-golden): it cannot be judged, so it is skipped.
        /// </summary>
        public static int Parity(bool buildFailed, bool capable, int failedChannels)
        {
            if (buildFailed || !capable) return Skip;
            return failedChannels > 0 ? Bad : Good;
        }

        public static string Name(int code) => code switch { Good => "good", Bad => "bad", Skip => "skip", _ => "abort " + code };
    }

    /// <summary>One judged candidate.</summary>
    public sealed record BisectStep(string Verdict, string Commit, string Subject);

    /// <summary>What a <c>git bisect run</c> found, read back from its output and <c>git bisect log</c>.</summary>
    public sealed class BisectOutcome
    {
        public string? FirstBad { get; init; }
        public string FirstBadSubject { get; init; } = "";
        /// <summary>The bisect stopped among skipped commits: the culprit is one of these.</summary>
        public List<string> Candidates { get; init; } = new();
        public List<BisectStep> Steps { get; init; } = new();
        public IEnumerable<BisectStep> Skipped => Steps.Where(s => s.Verdict == "skip");

        // git 2.55 quotes the term in every message ("# first 'bad' commit:", "<sha> is the first
        // 'bad' commit", "The first 'bad' commit could be any of:"); 2.43 writes it bare. A custom
        // term pair (old/new) is read the same way. The judged lines are keyed by the state word
        // git wrote, so "old"/"new" verdicts are reported as such.
        const string Term = @"'?[A-Za-z0-9_-]+'?";
        static readonly Regex Judged = new(@"^# (good|bad|skip|old|new): \[([0-9a-f]{7,64})\] ?(.*)$");
        static readonly Regex First = new(@"^# first " + Term + @" commit: \[([0-9a-f]{7,64})\] ?(.*)$");
        static readonly Regex FirstInRun = new(@"^([0-9a-f]{40,64}) is the first " + Term + @" commit");
        static readonly Regex AnyOf = new(@"^The first " + Term + @" commit could be any of");

        /// <summary>
        /// The log records the endpoints as comments before <c>git bisect start</c>; every judged
        /// step after it is a comment (verdict, commit, subject) followed by the command.
        /// </summary>
        public static BisectOutcome Parse(string runOutput, string log)
        {
            var steps = new List<BisectStep>();
            string? first = null, firstSubject = "";
            bool started = false;
            foreach (var raw in log.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("git bisect start", StringComparison.Ordinal)) { started = true; steps.Clear(); continue; }
                if (!started) continue;
                var m = Judged.Match(line);
                if (m.Success) { steps.Add(new BisectStep(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)); continue; }
                m = First.Match(line);
                if (m.Success) { first = m.Groups[1].Value; firstSubject = m.Groups[2].Value; }
            }
            var candidates = new List<string>();
            var lines = runOutput.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            if (first == null)
                foreach (var l in lines)
                {
                    var m = FirstInRun.Match(l);
                    if (m.Success) first = m.Groups[1].Value;
                }
            int any = lines.FindIndex(l => AnyOf.IsMatch(l));
            if (any >= 0)
                candidates.AddRange(lines.Skip(any + 1).TakeWhile(l => Regex.IsMatch(l, "^[0-9a-f]{40,64}$")));
            return new BisectOutcome { FirstBad = first, FirstBadSubject = firstSubject ?? "", Candidates = candidates, Steps = steps };
        }
    }
}
