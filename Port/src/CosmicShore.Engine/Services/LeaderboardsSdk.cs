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
        public string PlayerName;
        public double Score;
        public int Rank;
        public string Tier;
        public System.DateTime Updated;
        public string Metadata;
    }

    public class AddPlayerScoreOptions { public object Metadata { get; set; } public string VersionId { get; set; } }
    public class GetScoresOptions { public int? Offset { get; set; } public int? Limit { get; set; } public bool IncludeMetadata { get; set; } public string VersionId { get; set; } }
    public class GetScoresByPlayerIdsOptions { public bool IncludeMetadata { get; set; } public string VersionId { get; set; } }
    public class GetPlayerScoreOptions { public bool IncludeMetadata { get; set; } public string VersionId { get; set; } }
    public class GetPlayerRangeOptions { public int? RangeLimit { get; set; } public bool IncludeMetadata { get; set; } }

    /// <summary>One page of scores (original: Models.LeaderboardScoresPage).</summary>
    public class LeaderboardScoresPage
    {
        public int Offset;
        public int Limit;
        public int Total;
        public List<LeaderboardEntry> Results = new();
    }

    public class LeaderboardScores { public List<LeaderboardEntry> Results = new(); }

    public interface ILeaderboardsService
    {
        Task<LeaderboardEntry> AddPlayerScoreAsync(string leaderboardId, double score);

        // Defaults keep hand-written fakes compiling; the local service overrides them.
        Task<LeaderboardEntry> AddPlayerScoreAsync(string leaderboardId, double score, AddPlayerScoreOptions options)
            => AddPlayerScoreAsync(leaderboardId, score);
        Task<LeaderboardScoresPage> GetScoresAsync(string leaderboardId, GetScoresOptions options = null)
            => Task.FromResult(new LeaderboardScoresPage());
        Task<LeaderboardScores> GetScoresByPlayerIdsAsync(string leaderboardId, List<string> playerIds, GetScoresByPlayerIdsOptions options = null)
            => Task.FromResult(new LeaderboardScores());
        Task<LeaderboardEntry> GetPlayerScoreAsync(string leaderboardId, GetPlayerScoreOptions options = null)
            => Task.FromResult<LeaderboardEntry>(null);
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

        readonly Dictionary<string, string> _metadata = new();

        public Task<LeaderboardEntry> AddPlayerScoreAsync(string leaderboardId, double score, AddPlayerScoreOptions options)
        {
            if (options?.Metadata != null)
                _metadata[leaderboardId] = System.Text.Json.JsonSerializer.Serialize(options.Metadata, options.Metadata.GetType());
            return AddPlayerScoreAsync(leaderboardId, score);
        }

        LeaderboardEntry Entry(string id, bool meta) => new()
        {
            PlayerId = Engine.Services.AuthenticationService.Instance?.PlayerId ?? "local",
            PlayerName = Engine.Services.AuthenticationService.Instance?.PlayerName ?? "Player",
            Score = LatestScores[id], Rank = 0, Updated = System.DateTime.UtcNow,
            Metadata = meta && _metadata.TryGetValue(id, out var m) ? m : null,
        };

        /// <summary>Offline the board holds only the local player's own score (rank 0, the first row).</summary>
        public Task<LeaderboardScoresPage> GetScoresAsync(string leaderboardId, GetScoresOptions options = null)
        {
            var page = new LeaderboardScoresPage { Offset = options?.Offset ?? 0, Limit = options?.Limit ?? 10 };
            if (LatestScores.ContainsKey(leaderboardId) && page.Offset == 0) page.Results.Add(Entry(leaderboardId, options?.IncludeMetadata ?? false));
            page.Total = page.Results.Count;
            return Task.FromResult(page);
        }

        public Task<LeaderboardScores> GetScoresByPlayerIdsAsync(string leaderboardId, List<string> playerIds, GetScoresByPlayerIdsOptions options = null)
        {
            var r = new LeaderboardScores();
            if (LatestScores.ContainsKey(leaderboardId) && playerIds != null
                && playerIds.Contains(Engine.Services.AuthenticationService.Instance?.PlayerId ?? "local"))
                r.Results.Add(Entry(leaderboardId, options?.IncludeMetadata ?? false));
            return Task.FromResult(r);
        }

        public Task<LeaderboardEntry> GetPlayerScoreAsync(string leaderboardId, GetPlayerScoreOptions options = null)
            => Task.FromResult(LatestScores.ContainsKey(leaderboardId) ? Entry(leaderboardId, options?.IncludeMetadata ?? false) : null);
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
