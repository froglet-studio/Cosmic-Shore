using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.CloudCode;

namespace CosmicShore.Core
{
    /// <summary>
    /// Whether weekly-challenge times go through the server (fair play, Threat Board T2; Docs/AntiCheat/README.md §4.2).
    /// Off until the <c>weekly-score</c> Cloud Code script is deployed AND the Access Control policy that stops players
    /// writing leaderboards is applied (Tools/UGS/README.md). Turn both on together: the policy without the gate makes
    /// every submit fail, and the gate without the policy leaves the old direct write open to cheaters.
    /// </summary>
    public static class ServerScoreGate
    {
        public static readonly bool Enabled = false;
    }

    /// <summary>The client half of the server-checked leaderboard: one Cloud Code endpoint that refuses impossible times.</summary>
    public static class ServerScores
    {
        public const string WeeklyEndpoint = "weekly-score";

        [Serializable]
        public class Result
        {
            public bool ok;
            public string reason;
        }

        /// <summary>Posts a weekly-challenge completion time; returns the server's verdict, or null if it could not be reached.</summary>
        public static async UniTask<Result> SubmitWeeklyAsync(string leaderboardId, double seconds, string periodKey, int avatarId)
        {
            var args = new Dictionary<string, object>
            {
                { "leaderboardId", leaderboardId },
                { "seconds", seconds },
                { "periodKey", periodKey ?? string.Empty },
            };
            if (avatarId >= 0) args["avatarId"] = avatarId;

            try
            {
                return await CloudCodeService.Instance.CallEndpointAsync<Result>(WeeklyEndpoint, args).AsMainThread();
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[ServerScores] Weekly time for '{leaderboardId}' could not reach the server: {e.Message}");
                return null;
            }
        }
    }
}
