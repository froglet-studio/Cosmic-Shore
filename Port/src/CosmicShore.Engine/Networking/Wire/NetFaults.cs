using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Session-service faults for testing the game's error paths without a real outage
    /// (docs/MULTIPLAYER.md §6.3): <c>UgsRequestPolicy</c>'s retries and classification, the offline
    /// fallback, the party's "That party is full." message. Unity has no equivalent: an MPPM run
    /// can only meet these when UGS itself misbehaves.
    ///
    /// Set at launch with <c>COSMIC_SHORE_NET_FAULT=SPEC</c> or live with <c>do netfault SPEC</c> (MCP
    /// <c>net_fault</c>). SPEC tokens, applied in order:
    ///   off                 clear every fault
    ///   full[=N]            the next N joins (default 1) find the session full
    ///   ratelimit[=N]       the next N calls of any kind fail with a 429 (RateLimitExceeded)
    ///   relayfail[=N]       the next N creates or joins fail to set up the relay (NetworkSetupFailed)
    ///   down | up           every call fails as a service outage until "up"
    ///   slow=MS             every call waits MS first (0 = off)
    ///
    /// The error SHAPES are the stand-in's best reading of the SDK's (codes by name; messages are
    /// not matched by the game). Real UGS shapes are confirmed only by an MPPM run against UGS.
    /// </summary>
    public static class NetFaults
    {
        public static int FullJoins, RateLimited, RelayFailures, SlowMs;
        public static bool Down;

        /// <summary>Faults raised since launch, by kind (read by <c>do netfault</c> and the tests).</summary>
        public static readonly Dictionary<string, int> Raised = new();

        static NetFaults()
        {
            var spec = Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_FAULT");
            if (!string.IsNullOrWhiteSpace(spec)) Console.WriteLine(Apply(spec));
        }

        public static bool Any => FullJoins > 0 || RateLimited > 0 || RelayFailures > 0 || SlowMs > 0 || Down;

        public static string Describe()
        {
            if (!Any) return "off";
            var parts = new List<string>();
            if (Down) parts.Add("DOWN");
            if (FullJoins > 0) parts.Add($"full={FullJoins}");
            if (RateLimited > 0) parts.Add($"ratelimit={RateLimited}");
            if (RelayFailures > 0) parts.Add($"relayfail={RelayFailures}");
            if (SlowMs > 0) parts.Add($"slow={SlowMs}");
            return string.Join(" ", parts);
        }

        public static void Clear()
        {
            FullJoins = RateLimited = RelayFailures = SlowMs = 0;
            Down = false;
        }

        /// <summary>Applies SPEC. Returns the line to print: the faults now armed, or the error (nothing changes on an error).</summary>
        public static string Apply(string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return $"[netfault] {Describe()} · raised {RaisedText()}";
            var tokens = spec.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            // Validate everything first, so a bad token changes nothing.
            var ops = new List<Action>();
            foreach (var raw in tokens)
            {
                var t = raw.Trim().ToLowerInvariant();
                int eq = t.IndexOf('=');
                string key = eq < 0 ? t : t[..eq];
                int n = 1;
                if (eq >= 0 && (!int.TryParse(t[(eq + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 0))
                    return $"[netfault] error: '{raw}': the value must be a whole number >= 0";
                switch (key)
                {
                    case "off": case "none": case "clear": ops.Add(Clear); break;
                    case "down": ops.Add(() => Down = true); break;
                    case "up": ops.Add(() => Down = false); break;
                    case "full": { int v = n; ops.Add(() => FullJoins = v); break; }
                    case "ratelimit": { int v = n; ops.Add(() => RateLimited = v); break; }
                    case "relayfail": { int v = n; ops.Add(() => RelayFailures = v); break; }
                    case "slow" when eq >= 0: { int v = n; ops.Add(() => SlowMs = v); break; }
                    default:
                        return $"[netfault] error: unknown token '{raw}' (off, full[=N], ratelimit[=N], relayfail[=N], down, up, slow=MS)";
                }
            }
            foreach (var op in ops) op();
            return $"[netfault] {Describe()}";
        }

        static string RaisedText()
        {
            if (Raised.Count == 0) return "none";
            var parts = new List<string>();
            foreach (var kv in Raised) parts.Add($"{kv.Key} {kv.Value}");
            return string.Join(", ", parts);
        }

        static Exception Raise(string kind, SessionError code, string message)
        {
            Raised[kind] = Raised.TryGetValue(kind, out var c) ? c + 1 : 1;
            Console.WriteLine($"[netfault] raising {kind}: {message}");
            return new SessionException(message, code);
        }

        public enum Call { Create, Join, Query, Refresh, Save, Leave, Delete, Remove }

        /// <summary>Throws the fault armed for this call, if any (consuming a counted one).</summary>
        internal static void Check(Call call)
        {
            if (Down) throw Raise("down", SessionError.Unknown, "Service Unavailable (simulated outage).");
            if (RateLimited > 0) { RateLimited--; throw Raise("ratelimit", SessionError.RateLimitExceeded, "Too Many Requests (simulated)."); }
            if ((call == Call.Create || call == Call.Join) && RelayFailures > 0)
            {
                RelayFailures--;
                throw Raise("relayfail", SessionError.NetworkSetupFailed, "Relay allocation failed (simulated).");
            }
            if (call == Call.Join && FullJoins > 0)
            {
                FullJoins--;
                // The same shape as the stand-in's own full session, so the game's full-party path runs.
                throw Raise("full", SessionError.Unknown, "Session is full.");
            }
        }

        internal static async Task Gate(Call call)
        {
            if (SlowMs > 0) await Task.Delay(SlowMs);
            Check(call);
        }

        /// <summary>Wraps the active session service once, so every call can meet an armed fault.</summary>
        public static void Install()
        {
            if (MultiplayerService.Instance is not FaultInjectingMultiplayerService)
                MultiplayerService.Instance = new FaultInjectingMultiplayerService(MultiplayerService.Instance);
        }
    }

    /// <summary>A pass-through <see cref="IMultiplayerService"/> that consults <see cref="NetFaults"/> before every call.</summary>
    internal sealed class FaultInjectingMultiplayerService : IMultiplayerService
    {
        readonly IMultiplayerService _inner;
        public FaultInjectingMultiplayerService(IMultiplayerService inner) => _inner = inner;
        public IMultiplayerService Inner => _inner;

        public async Task<ISession> CreateSessionAsync(SessionOptions options)
        {
            await NetFaults.Gate(NetFaults.Call.Create);
            return Wrap(await _inner.CreateSessionAsync(options));
        }

        public async Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null)
        {
            await NetFaults.Gate(NetFaults.Call.Join);
            return Wrap(await _inner.JoinSessionByIdAsync(sessionId, options));
        }

        public async Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions options)
        {
            await NetFaults.Gate(NetFaults.Call.Query);
            return await _inner.QuerySessionsAsync(options);
        }

        static ISession Wrap(ISession s) => s == null ? null : new FaultySession(s);
    }

    /// <summary>A session whose service calls can meet an armed fault; everything else is the inner session's.</summary>
    internal sealed class FaultySession : IHostSession
    {
        readonly ISession _inner;
        public FaultySession(ISession inner) => _inner = inner;

        public string Id => _inner.Id;
        public string Code => _inner.Code;
        public bool IsHost => _inner.IsHost;
        public int MaxPlayers => _inner.MaxPlayers;
        public int PlayerCount => _inner.PlayerCount;
        public event Action Deleted { add => _inner.Deleted += value; remove => _inner.Deleted -= value; }
        public event Action<string> PlayerLeaving { add => _inner.PlayerLeaving += value; remove => _inner.PlayerLeaving -= value; }
        public IReadOnlyList<IReadOnlyPlayer> Players => _inner.Players;
        public IPlayer CurrentPlayer => _inner.CurrentPlayer;

        public async Task RefreshAsync() { await NetFaults.Gate(NetFaults.Call.Refresh); await _inner.RefreshAsync(); }
        public async Task SaveCurrentPlayerDataAsync() { await NetFaults.Gate(NetFaults.Call.Save); await _inner.SaveCurrentPlayerDataAsync(); }
        // Leaving and deleting still go through in an outage: a stuck session would outlive the test.
        public Task LeaveAsync() => _inner.LeaveAsync();
        public IHostSession AsHost() => _inner.AsHost() == null ? null : this;
        public Task DeleteAsync() => _inner.AsHost().DeleteAsync();
        public async Task RemovePlayerAsync(string playerId) { await NetFaults.Gate(NetFaults.Call.Remove); await _inner.AsHost().RemovePlayerAsync(playerId); }
    }
}
