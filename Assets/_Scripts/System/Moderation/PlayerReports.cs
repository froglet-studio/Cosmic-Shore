using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.Moderation;
using Unity.Services.Moderation.Models;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>Why a player is reported. Mapped to the UGS Moderation reason in <see cref="PlayerReports"/>.</summary>
    public enum PlayerReportReason
    {
        Cheating = 0,
        OffensiveName = 1,
        Trolling = 2,
        // A faction message in the Port (Docs/MENU_REDESIGN_B.md section 90). With Vivox in the project, the report also
        // carries the faction channel both players were in.
        OffensiveMessage = 3,
    }

    /// <summary>
    /// Sends a player report to UGS Moderation (fair play, Threat Board T8; Docs/AntiCheat/README.md §4.4), where it is
    /// reviewed in the dashboard. Apple guideline 1.2 and Google Play's UGC policy require a way to report players
    /// once they can see each other's content (names). One report per reported player per cooldown
    /// (<see cref="PlayerReportConfigSO.ReportCooldownSeconds"/>), so a press cannot spam the queue.
    /// </summary>
    public static class PlayerReports
    {
        static readonly Dictionary<string, float> s_lastReportAt = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_lastReportAt.Clear();

        /// <summary>True when a report on <paramref name="playerId"/> was already sent within the cooldown.</summary>
        public static bool RecentlyReported(string playerId) =>
            !string.IsNullOrEmpty(playerId) && s_lastReportAt.TryGetValue(playerId, out float at) &&
            Time.realtimeSinceStartup - at < PlayerReportConfigSO.LoadOrDefault().ReportCooldownSeconds;

        /// <summary>Sends the report; true when the service accepted it (or one was already sent within the cooldown).</summary>
        public static async UniTask<bool> ReportAsync(string playerId, PlayerReportReason reason)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            if (RecentlyReported(playerId)) return true;

            try
            {
                var report = Moderation.Instance.NewReport(playerId, new ReportReason(ModerationReason(reason)));
                await Moderation.Instance.ReportPlayer(report).AsMainThread();
                s_lastReportAt[playerId] = Time.realtimeSinceStartup;
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PlayerReports] Reported {playerId} for {reason}.");
                return true;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[PlayerReports] Report on {playerId} ({reason}) could not be sent: {e.Message}");
                return false;
            }
        }

        static string ModerationReason(PlayerReportReason reason) => reason switch
        {
            PlayerReportReason.Cheating => ReportReason.Hacking,
            PlayerReportReason.OffensiveName => ReportReason.InappropriatePlayerName,
            PlayerReportReason.Trolling => ReportReason.Sabotage,
            PlayerReportReason.OffensiveMessage => ReportReason.VerbalAbuse,
            _ => ReportReason.Hacking,
        };
    }
}
