using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.CloudCode;

namespace CosmicShore.Core
{
    /// <summary>
    /// Whether every player reports each match to the server (fair play, Threat Board T4 and T9;
    /// Docs/AntiCheat/README.md §4.2, §4.4). Off until the <c>match-report</c> Cloud Code script is deployed
    /// (Tools/UGS/README.md). While off, <see cref="CosmicShore.Gameplay.MatchReporter"/> is never attached.
    /// </summary>
    public static class MatchReportGate
    {
        public static readonly bool Enabled = false;
    }

    /// <summary>The client half of the match reports: <c>start</c> when a match begins, <c>end</c> when it finishes.</summary>
    public static class ServerMatchReports
    {
        public const string Endpoint = "match-report";

        [Serializable]
        public class Result
        {
            public bool ok;
            public string reason;
            public int matchesStarted;
            public int matchesLeft;
            public float leaveRate;
        }

        /// <summary>One final score as this player's game received it.</summary>
        [Serializable]
        public struct Score
        {
            public string id;
            public float score;
        }

        public static UniTask<Result> StartAsync(string matchId, string mode, string hostId, List<string> players) =>
            CallAsync(new Dictionary<string, object>
            {
                { "action", "start" }, { "matchId", matchId }, { "mode", mode }, { "hostId", hostId }, { "players", players },
            });

        public static UniTask<Result> EndAsync(string matchId, string mode, string hostId, List<string> players,
            List<Score> scores, string winnerId) =>
            CallAsync(new Dictionary<string, object>
            {
                { "action", "end" }, { "matchId", matchId }, { "mode", mode }, { "hostId", hostId }, { "players", players },
                { "scores", scores }, { "winner", winnerId ?? string.Empty },
            });

        static async UniTask<Result> CallAsync(Dictionary<string, object> args)
        {
            try
            {
                var result = await CloudCodeService.Instance.CallEndpointAsync<Result>(Endpoint, args).AsMainThread();
                if (result is { ok: false })
                    CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[MatchReport] {args["action"]} refused: {result.reason}");
                return result;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[MatchReport] {args["action"]} could not reach the server: {e.Message}");
                return null;
            }
        }
    }
}
