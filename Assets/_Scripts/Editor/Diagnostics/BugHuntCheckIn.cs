using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The Bug Hunt check-in — the editor half of the team's automated bug hunt
    /// (Docs/DIAGNOSTICS.md § "The Bug Hunt check-in").
    ///
    /// <para>Every collaborator's editor already records what goes wrong: the Bug Ledger files every
    /// red error, redlog keeps each play session's reds with context, the Crash Detector writes
    /// abnormal-exit reports and the DiagnosticsHUD writes performance JSONs. None of it leaves the
    /// machine on its own. At most once per <c>CheckInReminderHours</c> (default 24) — on editor load
    /// or after a play test, and only when there is something NEW — this asks the collaborator to share
    /// it. <b>Share now</b> publishes the pending ledger changes plus the new report files
    /// (<see cref="BugHuntReports"/>) through the ledger's own scoped commit: only <c>BugLedger/</c>
    /// paths, the current branch, refused while origin is ahead. <b>Share automatically</b> also turns
    /// on the after-play auto-publish, so the question never needs asking again.</para>
    ///
    /// <para>The daily Claude sweep reads every branch's <c>BugLedger/shared/</c>, fixes what it can
    /// and mirrors the whole picture into the Team Bug Board artifact (<see cref="TeamBoardUrl"/>), where
    /// anyone can read it — and file a bug — without Unity.</para>
    ///
    /// <para>Never prompts in batch mode, while playing, or while a publish is running. A dialog is the
    /// whole UI: one question a day does not need a window.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class BugHuntCheckIn
    {
        /// <summary>The Team Bug Board — the claude.ai artifact the daily sweep keeps current.</summary>
        public const string TeamBoardUrl = "https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno";

        const string ToolName = "Bug Hunt Check-in";

        static BugHuntCheckIn()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () => MaybePrompt(force: false);
            EditorApplication.playModeStateChanged += change =>
            {
                // After the ledger's own EnteredEditMode handling (auto-publish) has had its turn.
                if (change == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += () => MaybePrompt(force: false);
            };
        }

        [MenuItem("FrogletTools/Diagnostics/Bug Hunt Check-in", false, 133)]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 4,
            Description = "Share this machine's console errors, redlog sessions, crash reports and perf JSONs " +
                          "with the team now — the daily Claude bug sweep reads and fixes them.",
            DocPath = "Docs/DIAGNOSTICS.md#the-bug-hunt-check-in")]
        public static void CheckInNow() => MaybePrompt(force: true);

        [MenuItem("FrogletTools/Diagnostics/Open Team Bug Board", false, 134)]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 3,
            Description = "Every collaborator's open bugs, reports and the daily sweep's fixes, in the browser.",
            DocPath = "Docs/DIAGNOSTICS.md#the-bug-hunt-check-in")]
        public static void OpenTeamBoard()
        {
            if (string.IsNullOrEmpty(TeamBoardUrl))
            {
                EditorUtility.DisplayDialog(ToolName,
                    "The Team Bug Board has not been published yet. Ask a Claude session to run /bug-ledger " +
                    "(its daily sweep publishes the board and records its link in BugHuntCheckIn.TeamBoardUrl).",
                    "OK");
                return;
            }
            Application.OpenURL(TeamBoardUrl);
        }

        static void MaybePrompt(bool force)
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || BugLedgerPublisher.IsBusy) return;

                var settings = BugLedgerSettings.instance;
                if (!force)
                {
                    if (settings.CheckInReminderHours <= 0) return;
                    if (TryParseUtc(settings.LastCheckInPromptUtc, out var last)
                        && DateTime.UtcNow - last < TimeSpan.FromHours(settings.CheckInReminderHours))
                        return;
                }

                var pending = BugLedger.ComputePendingChanges();
                BugLedger.HasMaterialChange(pending, out int material);
                var reports = BugHuntReports.Plan();

                if (material == 0 && reports.Count == 0)
                {
                    // Nothing new: no nag, and the clock is not reset, so the next new error is asked
                    // about promptly rather than a day later.
                    if (force)
                        EditorUtility.DisplayDialog(ToolName,
                            "Nothing new to share — no new or changed bugs, and no new redlog, crash or " +
                            "performance files since your last share.", "OK");
                    return;
                }

                settings.LastCheckInPromptUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                settings.SaveNow();

                BugLedgerPublisher.Warm();
                string branch = BugLedgerPublisher.Branch;
                string message =
                    "Your editor caught errors the team should know about:\n\n" +
                    $"• {material} new or changed bug(s)\n" +
                    $"• {BugHuntReports.Describe(reports)}\n\n" +
                    "Press Share with team. That is all you need to do: a daily Claude sweep reads them and " +
                    "fixes what it can. You do not need to press Fix on anything.\n\n" +
                    $"Only BugLedger/ files are committed, to '{branch}'. Nothing else in your project.";

                int choice = EditorUtility.DisplayDialogComplex(ToolName, message,
                    "Share with team", "Not now", "Always share automatically");
                switch (choice)
                {
                    case 0:
                        Share(pending, material, reports);
                        break;
                    case 2:
                        settings.AutoPublishAfterPlay = true;
                        settings.SaveNow();
                        BugLedger.ApplySettings(settings);
                        Share(pending, material, reports);
                        break;
                    // 1: later — the clock was reset above.
                }
            }
            catch (Exception e)
            {
                // A reminder must never break the editor (same doctrine as the ledger itself).
                Debug.LogWarning($"[BugLedger] Bug Hunt check-in failed: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>The Bug Ledger window's <b>Share with team</b> button: everything pending plus the
        /// new report files, one confirm, one scoped publish. Unlike the check-in it ignores the
        /// reminder clock and counts counter-only drift too — the person asked.</summary>
        public static void ShareWithTeam()
        {
            if (BugLedgerPublisher.IsBusy) return;
            var pending = BugLedger.ComputePendingChanges();
            BugLedger.HasMaterialChange(pending, out int material);
            var reports = BugHuntReports.Plan();
            if (pending.Count == 0 && reports.Count == 0)
            {
                EditorUtility.DisplayDialog(ToolName,
                    "Nothing new to share — the team already has every bug and report file from this machine.", "OK");
                return;
            }

            BugLedgerPublisher.Warm();
            if (!EditorUtility.DisplayDialog(ToolName,
                    $"Share {pending.Count} bug change(s) and {BugHuntReports.Describe(reports)} with the team?\n\n" +
                    $"Committed to '{BugLedgerPublisher.Branch}' — only BugLedger/ files, nothing else in your project.",
                    "Share with team", "Cancel"))
                return;
            Share(pending, Math.Max(material, pending.Count), reports);
        }

        static void Share(List<BugLedger.BugLedgerPendingChange> pending, int material,
                          List<BugHuntReports.Copy> reports)
        {
            if (!BugLedgerPublisher.CanPush)
            {
                EditorUtility.DisplayDialog(ToolName,
                    "Git is unavailable or HEAD is detached, so nothing can be pushed from here. Check out a " +
                    "branch and try FrogletTools > Diagnostics > Bug Hunt Check-in again.", "OK");
                return;
            }

            var ids = new List<string>(pending.Count);
            foreach (var change in pending) ids.Add(change.Id);
            BugLedgerPublisher.StartPublish(ids,
                $"bugledger: bug hunt check-in ({material} new/changed issue(s), {BugHuntReports.Describe(reports)})",
                auto: true, reportCopies: reports);
            DiagnosticsWindow.OpenBugLedger();
        }

        static bool TryParseUtc(string text, out DateTime utc)
            => DateTime.TryParse(text, CultureInfo.InvariantCulture,
                                 DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
    }
}
