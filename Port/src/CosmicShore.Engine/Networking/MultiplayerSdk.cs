// ─────────────────────────────────────────────────────────────────────────────
// MultiplayerSdk.cs — engine placeholder surface for the UGS Multiplayer SDK
// (original contract: Unity.Services.Multiplayer — MultiplayerService,
// SessionOptions/JoinSessionOptions/QuerySessionsOptions, ISessionInfo,
// SessionProperty + PropertyIndex, the filter types). Grown per the
// Friends-SDK precedent so MultiplayerSetup's session-management flow ports
// FULLY LIVE.
//
// The default <see cref="MultiplayerService.Instance"/> is a
// <see cref="LocalMultiplayerService"/>: in the single-process port a
// "multiplayer session" IS a local session (the NetworkSceneManager
// precedent) — queries see no remote sessions, joins-by-id fail with
// SessionNotFound (there is no wire to cross), and creation returns an
// in-process IHostSession so the matchmaking flow converges on
// host-a-fresh-session with real observable behavior (deterministic ids, no
// clock/RNG). Tests swap fakes into the settable Instance; the real SDK
// binding replaces the local service at the services phase.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>Indexed session-property slot (original contract: Unity.Services.Multiplayer.PropertyIndex).</summary>
    public enum PropertyIndex
    {
        String1 = 0,
        String2 = 1,
        String3 = 2,
        String4 = 3,
        String5 = 4,
    }

    /// <summary>Queryable indexed field (original contract: Unity.Services.Multiplayer.FilterField).</summary>
    public enum FilterField
    {
        StringIndex1 = 0,
        StringIndex2 = 1,
        StringIndex3 = 2,
        StringIndex4 = 3,
        StringIndex5 = 4,
    }

    /// <summary>Original contract: Unity.Services.Multiplayer.FilterOperation.</summary>
    public enum FilterOperation
    {
        Equal = 0,
        NotEqual = 1,
        Greater = 2,
        Less = 3,
    }

    /// <summary>A session-level property (original contract: Unity.Services.Multiplayer.SessionProperty).</summary>
    public class SessionProperty
    {
        public string Value { get; }
        public VisibilityPropertyOptions Visibility { get; }
        public PropertyIndex Index { get; }

        public SessionProperty(
            string value,
            VisibilityPropertyOptions visibility = VisibilityPropertyOptions.Public,
            PropertyIndex index = PropertyIndex.String1)
        {
            Value = value;
            Visibility = visibility;
            Index = index;
        }
    }

    /// <summary>One query filter (original contract: Unity.Services.Multiplayer.FilterOption).</summary>
    public class FilterOption
    {
        public FilterField Field { get; }
        public string Value { get; }
        public FilterOperation Operation { get; }

        public FilterOption(FilterField field, string value, FilterOperation operation)
        {
            Field = field;
            Value = value;
            Operation = operation;
        }
    }

    /// <summary>Original contract: Unity.Services.Multiplayer.QuerySessionsOptions.</summary>
    public class QuerySessionsOptions
    {
        public List<FilterOption> FilterOptions { get; } = new();
    }

    /// <summary>Original contract: Unity.Services.Multiplayer.QuerySessionsResults.</summary>
    public class QuerySessionsResults
    {
        public IList<ISessionInfo> Sessions { get; }
        public QuerySessionsResults(IList<ISessionInfo> sessions)
            => Sessions = sessions ?? new List<ISessionInfo>();
    }

    /// <summary>Discovery-level session metadata (original contract: Unity.Services.Multiplayer.ISessionInfo).</summary>
    public interface ISessionInfo
    {
        string Id { get; }
        DateTime Created { get; }
        int MaxPlayers { get; }
        int AvailableSlots { get; }
        bool IsLocked { get; }
        bool HasPassword { get; }
    }

    /// <summary>Original contract: Unity.Services.Multiplayer.SessionOptions (the slice the codebase sets).</summary>
    public class SessionOptions
    {
        public int MaxPlayers { get; set; }
        public bool IsLocked { get; set; }
        public bool IsPrivate { get; set; }
        public Dictionary<string, PlayerProperty> PlayerProperties { get; set; }
        public Dictionary<string, SessionProperty> SessionProperties { get; set; }

        /// <summary>True after <see cref="SessionOptionsExtensions.WithRelayNetwork"/> — the transport request marker.</summary>
        public bool UseRelay { get; internal set; }
    }

    /// <summary>Original contract: the Unity.Services.Multiplayer SessionOptions network extensions.</summary>
    public static class SessionOptionsExtensions
    {
        /// <summary>Request Relay transport for the session (placeholder: records the intent; the transport phase acts on it).</summary>
        public static SessionOptions WithRelayNetwork(this SessionOptions options, string region = null)
        {
            options.UseRelay = true;
            return options;
        }
    }

    /// <summary>Original contract: Unity.Services.Multiplayer.JoinSessionOptions (the slice the codebase sets).</summary>
    public class JoinSessionOptions
    {
        public Dictionary<string, PlayerProperty> PlayerProperties { get; set; }
    }

    /// <summary>The session-service surface the game consumes (original contract: Unity.Services.Multiplayer.IMultiplayerService).</summary>
    public interface IMultiplayerService
    {
        Task<ISession> CreateSessionAsync(SessionOptions options);
        Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null);
        Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions options);
    }

    /// <summary>
    /// Static access point (original contract: Unity.Services.Multiplayer.MultiplayerService).
    /// Defaults to the in-process <see cref="LocalMultiplayerService"/>; tests swap fakes in
    /// and call <see cref="Reset"/> in teardown.
    /// </summary>
    public static class MultiplayerService
    {
        public static IMultiplayerService Instance { get; set; } = new LocalMultiplayerService();

        /// <summary>Restore the local default (test isolation helper). Also resets the local id counter.</summary>
        public static void Reset()
        {
            LocalMultiplayerService.ResetIds();
            Instance = new LocalMultiplayerService();
        }
    }

    /// <summary>
    /// The single-process session service: creation returns an in-process host session
    /// (deterministic ids), discovery sees no remote sessions, and cross-process joins
    /// fail with <see cref="SessionError.SessionNotFound"/> — honest local semantics
    /// that let the matchmaking flow converge on hosting fresh.
    /// </summary>
    public sealed class LocalMultiplayerService : IMultiplayerService
    {
        static int s_nextId;

        internal static void ResetIds() => s_nextId = 0;

        public Task<ISession> CreateSessionAsync(SessionOptions options)
        {
            var session = new LocalSession(
                $"local-session-{++s_nextId}",
                options?.MaxPlayers ?? 0);
            return Task.FromResult<ISession>(session);
        }

        public Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null)
            => throw new SessionException(
                SessionError.SessionNotFound,
                $"Session '{sessionId}' not found (no remote sessions exist in the single-process port).");

        public Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions options)
            => Task.FromResult(new QuerySessionsResults(new List<ISessionInfo>()));

        sealed class LocalSession : IHostSession
        {
            readonly List<IReadOnlyPlayer> _players = new();

            public LocalSession(string id, int maxPlayers)
            {
                Id = id;
                MaxPlayers = maxPlayers;
            }

            public string Id { get; }
            public string Code => "LOCAL";
            public bool IsHost => true;
            public int MaxPlayers { get; }
            public int PlayerCount => _players.Count;
            public event Action Deleted { add { } remove { } }
            public event Action<string> PlayerLeaving { add { } remove { } }
            public IReadOnlyList<IReadOnlyPlayer> Players => _players;
            public IPlayer CurrentPlayer => null;
            public Task RefreshAsync() => Task.CompletedTask;
            public Task SaveCurrentPlayerDataAsync() => Task.CompletedTask;
            public Task LeaveAsync() => Task.CompletedTask;
            public IHostSession AsHost() => this;
            public Task DeleteAsync() => Task.CompletedTask;
            public Task RemovePlayerAsync(string playerId) => Task.CompletedTask;
        }
    }
}
