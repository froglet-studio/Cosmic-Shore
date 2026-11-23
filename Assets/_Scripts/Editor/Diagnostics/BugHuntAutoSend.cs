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
    /// The Bug Hunt's after-play send, with no question (Yash, 2026-10-16: "make send to Claude automatic too,
    /// no question").
    ///
    /// <para>When a play test ends having logged a red error Claude does not have yet, or made a new performance
    /// capture (<see cref="PerfAutoCapture"/>), AI training record or multiplayer test log, this pushes this
    /// machine's errors plus those files to <c>bugledger/&lt;user&gt;</c> through <see cref="BugLedgerSender"/>,
    /// which never touches your working tree or branch, and logs one console line saying what went. The same
    /// error is not sent again — only a NEW error, or one that came back after a fix (its fingerprint
    /// <c>id:regressions</c>). Until 2026-10-16 this was a "Send to Claude?" dialog; since 2026-10-17 a small
    /// confirmation (<see cref="BugHuntSendConfirmation"/>) says what went and opens into the Project Record.</para>
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
        /// <summary>What the last automatic send carried, for the Scene view badge ("" before the first).</summary>
        public static string LastSent { get; private set; } = "";

        static BugHuntAutoSend()
        {
            if (Application.isBatchMode) return;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += AskWhenPeersLanded;
            };
            EditorApplication.delayCall += BugLedgerSender.SyncFixesInBackground;
        }

        /// <summary>
        /// After a multiplayer test the virtual players stop Play a moment after this editor and write their
        /// logs then (<see cref="NetTestLogs"/>); wait for them so the question and the send see the whole test.
        /// </summary>
        static void AskWhenPeersLanded()
        {
            if (!NetTestLogs.WroteThisPlay || EditorApplication.timeSinceStartup >= NetTestLogs.AskNotBefore)
            {
                AskAfterPlay();
                return;
            }
            void Poll()
            {
                if (EditorApplication.timeSinceStartup < NetTestLogs.AskNotBefore && !EditorApplication.isPlayingOrWillChangePlaymode) return;
                EditorApplication.update -= Poll;
                AskAfterPlay();
            }
            EditorApplication.update += Poll;
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
                // A multiplayer test's logs (NetTestLogs) ask too, from the main editor only: the virtual players'
                // files land in its folder, and one question per test is the point.
                // A Load Time Insights report asks too: online play records every load by itself (B36), and the
                // multiplayer sweep reads them beside the test's net logs (net_logs.py show).
                int newCaptures = 0, newFlights = 0, newNetLogs = 0, newLoads = 0;
                var records = new List<string>();
                var peers = new List<string>();
                bool clone = NetTestLogs.IsClone;
                foreach (var report in BugHuntReports.Candidates("", Environment.UserName))
                {
                    if (report.Kind == "perf" && asked.Add("perf:" + Path.GetFileName(report.Source)))
                        newCaptures++;
                    else if (report.Kind == "net" && !clone && asked.Add("net:" + Path.GetFileName(report.Source)))
                    {
                        newNetLogs++;
                        string peer = DescribePeer(Path.GetFileName(report.Source));
                        if (!peers.Contains(peer)) peers.Add(peer);
                    }
                    else if (report.Kind == "load" && asked.Add("load:" + Path.GetFileName(report.Source)))
                        newLoads++;
                    else if (report.Kind == "flights" && asked.Add("flights:" + Path.GetFileName(report.Source)))
                    {
                        newFlights++;
                        string what = DescribeRecord(Path.GetFileName(report.Source));
                        if (!records.Contains(what)) records.Add(what);
                    }
                }

                if (fresh.Count == 0 && newCaptures == 0 && newFlights == 0 && newNetLogs == 0 && newLoads == 0)
                {
                    // Nothing to ask about, but a clean run counted toward a fix: tell Claude without
                    // asking (issues and the validation trail only), so "is it fixed?" has a true answer.
                    if (BugLedger.LastPlayCountedFix) BugLedgerSender.SendInBackground(withReports: false);
                    return;
                }

                // Remember them now: each new error, capture or log is sent once, never on every run.
                settings.SentFingerprints = string.Join("\n", asked);
                settings.SaveNow();

                // No question (Yash, 2026-10-16: "make send to Claude automatic too, no question"): whatever is new goes
                // to bugledger/<user> at once. BugLedgerSender never touches the working tree or the current branch, and
                // pushes nothing when the tree is unchanged, so an automatic send costs the person nothing.
                var sent = new List<string>();
                if (fresh.Count > 0) sent.Add(fresh.Count == 1 ? "1 new console error" : $"{fresh.Count} new console errors");
                if (newCaptures > 0) sent.Add($"{newCaptures} performance capture(s)");
                if (newNetLogs > 0) sent.Add($"the multiplayer test's logs from {Players(peers)}");
                if (newFlights > 0) sent.Add($"{newFlights} AI training record(s): {string.Join(", ", records)}");
                if (newLoads > 0) sent.Add($"{newLoads} load timing report(s)");
                LastSent = string.Join("; ", sent);
                BugLedgerSender.SendInBackground();
                // A confirmation, never a question (Yash, 2026-10-17): it says what went, whether it arrived, and opens
                // into the Project Record.
                BugHuntSendConfirmation.ShowAfterSend(LastSent);
                Debug.Log($"[BugLedger] Sent to Claude automatically: {LastSent}. Nothing in your project or branch changed.");
            }
            catch (Exception e)
            {
                // The question must never break the editor (same doctrine as the ledger itself).
                Debug.LogWarning($"[BugLedger] After-play send prompt failed: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>"P2" from "net_20261015_101500_p2.json"; "main" for the main editor's.</summary>
        static string DescribePeer(string file)
        {
            var parts = Path.GetFileNameWithoutExtension(file).Split('_');
            return parts.Length >= 4 ? string.Join("_", parts, 3, parts.Length - 3) : file;
        }

        /// <summary>"main, p2 and p3".</summary>
        static string Players(List<string> peers)
        {
            if (peers.Count == 0) return "this test";
            if (peers.Count == 1) return peers[0];
            return string.Join(", ", peers.GetRange(0, peers.Count - 1)) + " and " + peers[peers.Count - 1];
        }

        /// <summary>"Squirrel in Regatta" from "flight_Regatta_Squirrel_20261016_023117.json".</summary>
        static string DescribeRecord(string file)
        {
            var parts = Path.GetFileNameWithoutExtension(file).Split('_');
            return parts.Length >= 3 ? $"{parts[2]} in {parts[1]}" : file;
        }

        static string Fingerprint(BugLedgerIssue issue) => issue.Id + ":" + issue.Regressions;
    }
}
