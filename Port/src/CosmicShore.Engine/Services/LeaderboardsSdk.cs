// ─────────────────────────────────────────────────────────────────────────────
// LeaderboardsSdk.cs — engine placeholder surface for the UGS Leaderboards SDK
// (original contract: Unity.Services.Leaderboards — LeaderboardsService.Instance.
// AddPlayerScoreAsync). Grown per the CloudSaveSdk / MultiplayerSdk precedent so
// UGSStatsManager's leaderboard submission lane ports FULLY LIVE: the default
// instance is an honest single-process store — per-leaderboard submission log +
// last score per board, nothing pre-exists on a fresh process. Tests read the
// log through the public seams; the real SDK binding replaces the local service
// at the services phase.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Services.Leaderboards
{
    /// <summary>A submitted score row (original contract: Unity.Services.Leaderboards.Models.LeaderboardEntry, the subset callers read).</summary>
    public class LeaderboardEntry
    {
        public string PlayerId;
        public double Score;
        public int Rank;
    }

    public interface ILeaderboardsService
    {
        Task<LeaderboardEntry> AddPlayerScoreAsync(string leaderboardId, double score);
    }

    /// <summary>
    /// Honest local leaderboard store: every submission is appended to
    /// <see cref="Submissions"/> and becomes the board's latest entry. No
    /// ranking simulation — rank is always 1 in a single-process world.
    /// </summary>
    public class LocalLeaderboardsService : ILeaderboardsService
    {
        /// <summary>Every submission in order (port-only observability).</summary>
        public readonly List<(string LeaderboardId, double Score)> Submissions = new();

        /// <summary>Latest score per leaderboard id.</summary>
        public readonly Dictionary<string, double> LatestScores = new();

        public Task<LeaderboardEntry> AddPlayerScoreAsync(string leaderboardId, double score)
        {
            if (string.IsNullOrEmpty(leaderboardId))
                throw new System.ArgumentException("Leaderboard id is null or empty.", nameof(leaderboardId));

            Submissions.Add((leaderboardId, score));
            LatestScores[leaderboardId] = score;
            return Task.FromResult(new LeaderboardEntry { PlayerId = "local", Score = score, Rank = 1 });
        }
    }

    /// <summary>Static access point (original contract: Unity.Services.Leaderboards.LeaderboardsService).</summary>
    public static class LeaderboardsService
    {
        public static ILeaderboardsService Instance { get; set; } = new LocalLeaderboardsService();

        /// <summary>Swap in a fresh local store (test isolation).</summary>
        public static LocalLeaderboardsService Reset()
        {
            var local = new LocalLeaderboardsService();
            Instance = local;
            return local;
        }
    }
}
