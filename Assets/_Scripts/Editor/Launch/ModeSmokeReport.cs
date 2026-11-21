using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// What one mode's smoke run saw. Facts only: the verdict is derived from them by <see cref="Evaluate"/> (and again,
    /// independently, by <c>Tools/Launch/smoke_report.py</c>), so a result cannot claim a pass its facts do not support.
    /// Serialised with <see cref="JsonUtility"/>; the field names are the JSON keys smoke_report.py reads.
    /// </summary>
    [Serializable]
    public class ModeSmokeResult
    {
        public const string Passed = "passed";
        public const string Failed = "failed";
        public const string Skipped = "skipped";
        public const string NotRun = "not_run";

        public string mode;
        public int modeId;
        public string scene;
        public string vessel;
        public int intensity;
        public int players;
        /// <summary><see cref="Passed"/>, <see cref="Failed"/>, <see cref="Skipped"/> or <see cref="NotRun"/>.</summary>
        public string status = NotRun;
        public string skipReason = "";
        /// <summary>The first check that failed, in words ("" when it passed).</summary>
        public string failure = "";

        // The facts the verdict reads.
        public bool inBuild = true;
        public bool reachedMenu;
        public bool sceneLoaded;
        public bool turnStarted;
        public int seats;
        public int aiSeats;
        public int movingSeats;
        public bool gameEnded;
        public int errors;
        public int exceptions;
        public string harnessFault = "";

        // Context for a person reading a failure.
        public int turnsEnded;
        public bool winnerCalculated;
        public float gameSeconds;
        public float wallSeconds;
        public float matchBudgetSeconds;
        public string phaseReached = "";
        public string notes = "";
        public int errorsAtBoot;
        public int errorsInMatch;
        public int errorsAtExit;
        public List<string> firstErrors = new();

        /// <summary>Sets <see cref="status"/> and <see cref="failure"/> from the facts. Skipped results stay skipped.</summary>
        public void Evaluate()
        {
            if (status == Skipped) { failure = ""; return; }
            failure = FirstFailure();
            status = string.IsNullOrEmpty(failure) ? Passed : Failed;
        }

        string FirstFailure()
        {
            if (!inBuild) return $"the scene '{scene}' is not an enabled scene in the build settings";
            if (!string.IsNullOrEmpty(harnessFault)) return "the run stopped: " + harnessFault;
            if (!reachedMenu) return "the boot never reached the menu with a local pilot";
            if (!sceneLoaded) return $"the scene '{scene}' never loaded";
            if (seats <= 0) return "no seat had a vessel";
            if (!turnStarted) return "the first turn never started (Ready was pressed)";
            if (aiSeats < seats) return $"{seats - aiSeats} of {seats} seats were not flown by the AI";
            if (movingSeats < 1) return "no AI vessel moved";
            if (!gameEnded) return $"the game-over event never fired within {matchBudgetSeconds:0} game seconds";
            if (exceptions > 0) return $"{exceptions} exception(s) logged";
            if (errors > 0) return $"{errors} error(s) logged";
            return "";
        }

        /// <summary>The launch-status component id for <paramref name="modeName"/>: "smoke-" + kebab-case ("SkimRace" -> "smoke-skim-race").</summary>
        public static string ComponentId(string modeName)
        {
            var sb = new StringBuilder("smoke-");
            for (int i = 0; i < modeName.Length; i++)
            {
                char c = modeName[i];
                if (char.IsUpper(c) && i > 0 && (char.IsLower(modeName[i - 1]) || char.IsDigit(modeName[i - 1])))
                    sb.Append('-');
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
            }
            return sb.ToString();
        }
    }

    /// <summary>A whole run: every planned mode, in order, rewritten after each one so a crash leaves what finished.</summary>
    [Serializable]
    public class ModeSmokeSummary
    {
        public int schema = 1;
        public string startedAt = "";
        public string finishedAt = "";
        public string commit = "";
        public string unityVersion = "";
        public float timeScale;
        public string aiDifficulty = "";
        public bool batchMode;
        /// <summary>False when the editor's Live end-condition counts differ from the Build baseline a player build ships.</summary>
        public bool endConditionsMatchBuild = true;
        public List<ModeSmokeResult> modes = new();
        /// <summary>Enabled build scenes no planned mode loads (unlisted modes): not smoked, named so nobody assumes they were.</summary>
        public List<string> uncoveredBuildScenes = new();

        public int Count(string status)
        {
            int n = 0;
            foreach (var m in modes) if (m.status == status) n++;
            return n;
        }

        /// <summary>0 when every mode that was meant to run passed; 1 when any failed or never ran.</summary>
        public int ExitCode => Count(ModeSmokeResult.Failed) + Count(ModeSmokeResult.NotRun) > 0 ? 1 : 0;
    }

    /// <summary>Writes a run's files: summary.json, one JSON per mode, and a JUnit XML for CI test reporters.</summary>
    public static class ModeSmokeReport
    {
        const int MaxKeptErrors = 8;
        const int MaxErrorChars = 300;

        // The same shapes launch_status.py refuses: a long unbroken token, and a URL's query string. A game log
        // could carry either; the run's files are uploaded as CI artifacts on a public repository.
        static readonly Regex LongToken = new(@"[A-Za-z0-9+/=_-]{40,}", RegexOptions.Compiled);
        static readonly Regex UrlQuery = new(@"(https?://[^\s?]+)\?\S*", RegexOptions.Compiled);

        /// <summary>One line of a logged error, shortened and with anything token-shaped removed.</summary>
        public static string Redact(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            string line = message.Split('\n')[0].Trim();
            line = UrlQuery.Replace(line, "$1?[query removed]");
            line = LongToken.Replace(line, "[redacted]");
            return line.Length > MaxErrorChars ? line.Substring(0, MaxErrorChars) + "..." : line;
        }

        /// <summary>Keeps the first few distinct errors of a mode, redacted.</summary>
        public static void KeepError(ModeSmokeResult result, string phase, string message)
        {
            if (result.firstErrors.Count >= MaxKeptErrors) return;
            string line = $"[{phase}] {Redact(message)}";
            if (!result.firstErrors.Contains(line)) result.firstErrors.Add(line);
        }

        public static void Write(ModeSmokeSummary summary, string directory)
        {
            Directory.CreateDirectory(Path.Combine(directory, "modes"));
            File.WriteAllText(Path.Combine(directory, "summary.json"), JsonUtility.ToJson(summary, true));
            foreach (var m in summary.modes)
                if (m.status != ModeSmokeResult.NotRun)
                    File.WriteAllText(Path.Combine(directory, "modes", m.mode + ".json"), JsonUtility.ToJson(m, true));
            File.WriteAllText(Path.Combine(directory, "junit.xml"), JUnit(summary));
        }

        public static string JUnit(ModeSmokeSummary summary)
        {
            var inv = CultureInfo.InvariantCulture;
            int failures = summary.Count(ModeSmokeResult.Failed) + summary.Count(ModeSmokeResult.NotRun);
            int skipped = summary.Count(ModeSmokeResult.Skipped);
            float total = 0f;
            foreach (var m in summary.modes) total += m.wallSeconds;

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append("<testsuites name=\"ModeSmoke\" tests=\"").Append(summary.modes.Count)
              .Append("\" failures=\"").Append(failures).Append("\" skipped=\"").Append(skipped)
              .Append("\" time=\"").Append(total.ToString("F1", inv)).Append("\">\n");
            sb.Append("  <testsuite name=\"ModeSmoke\" tests=\"").Append(summary.modes.Count)
              .Append("\" failures=\"").Append(failures).Append("\" errors=\"0\" skipped=\"").Append(skipped)
              .Append("\" time=\"").Append(total.ToString("F1", inv))
              .Append("\" timestamp=\"").Append(Xml(summary.startedAt)).Append("\">\n");
            sb.Append("    <properties>\n");
            Property(sb, "commit", summary.commit);
            Property(sb, "unity", summary.unityVersion);
            Property(sb, "timeScale", summary.timeScale.ToString("0.##", inv));
            Property(sb, "aiDifficulty", summary.aiDifficulty);
            Property(sb, "endConditionsMatchBuild", summary.endConditionsMatchBuild ? "true" : "false");
            sb.Append("    </properties>\n");

            foreach (var m in summary.modes)
            {
                sb.Append("    <testcase classname=\"ModeSmoke\" name=\"").Append(Xml(m.mode))
                  .Append("\" time=\"").Append(m.wallSeconds.ToString("F1", inv)).Append("\">\n");
                if (m.status == ModeSmokeResult.Skipped)
                    sb.Append("      <skipped message=\"").Append(Xml(m.skipReason)).Append("\"/>\n");
                else if (m.status == ModeSmokeResult.NotRun)
                    sb.Append("      <failure message=\"never ran (the run stopped before this mode)\"/>\n");
                else if (m.status == ModeSmokeResult.Failed)
                    sb.Append("      <failure message=\"").Append(Xml(m.failure)).Append("\">")
                      .Append(Xml(Details(m))).Append("</failure>\n");
                sb.Append("      <system-out>").Append(Xml(Details(m))).Append("</system-out>\n");
                sb.Append("    </testcase>\n");
            }
            sb.Append("  </testsuite>\n</testsuites>\n");
            return sb.ToString();
        }

        static string Details(ModeSmokeResult m)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("scene=").Append(m.scene).Append(" vessel=").Append(m.vessel)
              .Append(" intensity=").Append(m.intensity).Append(" players=").Append(m.players)
              .Append(" phase=").Append(m.phaseReached).Append('\n');
            sb.Append("seats=").Append(m.seats).Append(" ai=").Append(m.aiSeats).Append(" moving=").Append(m.movingSeats)
              .Append(" gameEnded=").Append(m.gameEnded ? "yes" : "no").Append(" turnsEnded=").Append(m.turnsEnded)
              .Append(" gameSeconds=").Append(m.gameSeconds.ToString("F1", inv))
              .Append(" wallSeconds=").Append(m.wallSeconds.ToString("F1", inv)).Append('\n');
            sb.Append("errors=").Append(m.errors).Append(" exceptions=").Append(m.exceptions)
              .Append(" (boot ").Append(m.errorsAtBoot).Append(", match ").Append(m.errorsInMatch)
              .Append(", exit ").Append(m.errorsAtExit).Append(")\n");
            if (!string.IsNullOrEmpty(m.notes)) sb.Append("notes: ").Append(m.notes).Append('\n');
            foreach (var e in m.firstErrors) sb.Append(e).Append('\n');
            return sb.ToString();
        }

        static void Property(StringBuilder sb, string name, string value) =>
            sb.Append("      <property name=\"").Append(name).Append("\" value=\"").Append(Xml(value)).Append("\"/>\n");

        /// <summary>Escapes text for an XML attribute or element, dropping characters XML 1.0 cannot hold.</summary>
        public static string Xml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default:
                        if (c == '\n' || c == '\t' || c == '\r' || c >= 0x20) sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
