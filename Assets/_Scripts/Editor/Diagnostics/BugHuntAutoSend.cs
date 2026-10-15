using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The whole Bug Hunt UI: ONE question after you stop Play.
    ///
    /// <para>If the play test that just ended logged a red error Claude does not have yet, a dialog
    /// asks "Send to Claude?". <b>Send to Claude</b> pushes this machine's errors (plus any new
    /// redlog, crash and perf files) to <c>bugledger/&lt;user&gt;</c> through
    /// <see cref="BugLedgerSender"/>, which never touches your working tree or branch. <b>Not now</b>
    /// keeps them on this machine; they go with the next send. Either way the same error is not
    /// asked about again — only a NEW error, or one that came back after a fix, asks again.</para>
    ///
    /// <para>Everything after the click is automatic: Claude fixes from that branch and marks the
    /// issue fixed there; once you pull the fix, your next clean play runs close it (the ledger
    /// does this silently); if the error returns it reopens and the next play test asks again.
    /// At editor load the fixes Claude published are picked up in the background.</para>
    ///
    /// <para>This replaced the Bug Ledger window, its Stage &amp; Push tab and the daily check-in
    /// reminder (2026-10-15, Yash: "no complex UI toolboxes in Unity... just auto send consoles").
    /// Never prompts in batch mode.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class BugHuntAutoSend
    {
        const int MaxTitlesShown = 3;

        static BugHuntAutoSend()
        {
            if (Application.isBatchMode) return;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += AskAfterPlay;
            };
            EditorApplication.delayCall += BugLedgerSender.SyncFixesInBackground;
        }

        static void AskAfterPlay()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                var settings = BugLedgerSettings.instance;
                var asked = new HashSet<string>(
                    settings.SentFingerprints.Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

                var fresh = new List<BugLedgerIssue>();
                foreach (var issue in BugLedger.CopyLive())
                {
                    if (!issue.IsOpen || !BugLedger.LastPlaySeen.Contains(issue.Id)) continue;
                    if (asked.Add(Fingerprint(issue))) fresh.Add(issue);
                }

                // A performance capture is deliberate (someone pressed Capture), and /perf-hunt can
                // only judge captures that reach a branch — so a new one asks too, even on a run
                // with no new error. Redlog and crash files only ride along: a redlog is written
                // every session that has reds, and asking for those would ask every run.
                // An AI training record (HumanFlightRecorder, automatic in every mode) asks too: the AIs can only learn from
                // records that reach a branch (learn_from_flights.py). The question names each vessel and mode.
                int newCaptures = 0, newFlights = 0;
                var records = new List<string>();
                foreach (var report in BugHuntReports.Candidates("", Environment.UserName))
                {
                    if (report.Kind == "perf" && asked.Add("perf:" + Path.GetFileName(report.Source)))
                        newCaptures++;
                    else if (report.Kind == "flights" && asked.Add("flights:" + Path.GetFileName(report.Source)))
                    {
                        newFlights++;
                        string what = DescribeRecord(Path.GetFileName(report.Source));
                        if (!records.Contains(what)) records.Add(what);
                    }
                }

                if (fresh.Count == 0 && newCaptures == 0 && newFlights == 0) return;

                // Remember them whatever the answer: "Not now" must not become "ask every run".
                settings.SentFingerprints = string.Join("\n", asked);
                settings.SaveNow();

                var body = new StringBuilder();
                if (fresh.Count > 0)
                {
                    body.Append(fresh.Count == 1
                        ? "This play test logged a new console error:\n\n"
                        : $"This play test logged {fresh.Count} new console errors:\n\n");
                    for (int i = 0; i < fresh.Count && i < MaxTitlesShown; i++)
                        body.Append("• ").Append(Shorten(fresh[i].Title)).Append('\n');
                    if (fresh.Count > MaxTitlesShown) body.Append($"• and {fresh.Count - MaxTitlesShown} more\n");
                    if (newCaptures > 0) body.Append($"\nPlus {newCaptures} new performance capture(s).\n");
                    if (newFlights > 0)
                        body.Append($"\nPlus {newFlights} new AI training record(s): {string.Join(", ", records)} " +
                                    $"({HumanFlightRecorder.TrainingType}).\n");
                    body.Append("\nSend them to Claude to fix? Nothing in your project or branch is changed.");
                }
                else if (newCaptures == 0)
                {
                    // Only AI training records: the question is about storing them, not about bugs.
                    body.Append(newFlights == 1 ? "New AI training record:\n\n" : $"{newFlights} new AI training records:\n\n");
                    for (int i = 0; i < records.Count && i < MaxTitlesShown; i++)
                        body.Append("• ").Append(records[i]).Append('\n');
                    if (records.Count > MaxTitlesShown) body.Append($"• and {records.Count - MaxTitlesShown} more\n");
                    body.Append($"\nTraining type: {HumanFlightRecorder.TrainingType}.\n");
                    body.Append("\nStore it so the AI learns from how you fly? Nothing in your project or branch is changed.");
                    if (EditorUtility.DisplayDialog("Store AI training record?", body.ToString(), "Store AI record", "Not now"))
                        BugLedgerSender.SendInBackground();
                    return;
                }
                else
                {
                    body.Append(newCaptures == 1
                        ? "You made a new performance capture."
                        : $"You made {newCaptures} new performance captures.");
                    body.Append("\n\nSend to Claude to check for slowdowns? Nothing in your project or branch is changed.");
                }

                if (EditorUtility.DisplayDialog("Send to Claude?", body.ToString(), "Send to Claude", "Not now"))
                    BugLedgerSender.SendInBackground();
            }
            catch (Exception e)
            {
                // The question must never break the editor (same doctrine as the ledger itself).
                Debug.LogWarning($"[BugLedger] After-play send prompt failed: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>"Squirrel in Regatta" from "flight_Regatta_Squirrel_20261016_023117.json".</summary>
        static string DescribeRecord(string file)
        {
            var parts = Path.GetFileNameWithoutExtension(file).Split('_');
            return parts.Length >= 3 ? $"{parts[2]} in {parts[1]}" : file;
        }

        static string Fingerprint(BugLedgerIssue issue) => issue.Id + ":" + issue.Regressions;

        static string Shorten(string s) => s.Length > 90 ? s.Substring(0, 87) + "..." : s;
    }
}
