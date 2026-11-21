using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    // A session as the game's handle sees it: what UGS Lobby returns, read into records
    // (UgsLobbyMapping.ToRecord) and shown through SessionView, the roster with the local player's own
    // writable entry. UGS keeps the rules (seats, host, liveness); these only hold its answers.

    /// <summary>One property: its value, its visibility and (session properties only) its query index.</summary>
    internal sealed class PropRecord
    {
        public string Value { get; set; }
        public int Index { get; set; }
        public int Visibility { get; set; }
    }

    /// <summary>A player in a session: id, the per-player properties (the game's invite channel) and when it was last heard from.</summary>
    internal sealed class PlayerRecord
    {
        public string Id { get; set; }
        public Dictionary<string, PropRecord> Properties { get; set; } = new();
        public long LastSeen { get; set; }
    }

    /// <summary>A session as the lobby holds it.</summary>
    internal sealed class SessionRecord
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public long Created { get; set; }
        public string HostPlayerId { get; set; }
        public int MaxPlayers { get; set; }
        public bool IsLocked { get; set; }
        public bool IsPrivate { get; set; }
        /// <summary>The host's UGS Relay join code, when the session carries a Relay network: joiners join by it, over UDP.</summary>
        public string RelayJoinCode { get; set; }
        public Dictionary<string, PropRecord> Properties { get; set; } = new();
        public List<PlayerRecord> Players { get; set; } = new();
    }

    internal static class SessionRules
    {
        public static Dictionary<string, PropRecord> ToRecords(IDictionary<string, PlayerProperty> props)
        {
            var d = new Dictionary<string, PropRecord>();
            if (props != null)
                foreach (var kv in props) if (kv.Value != null) d[kv.Key] = new PropRecord { Value = kv.Value.Value, Visibility = (int)kv.Value.Visibility };
            return d;
        }
    }

    /// <summary>A roster entry as a session handle exposes it; the local player's is writable.</summary>
    internal sealed class RosterPlayer : IPlayer
    {
        readonly Dictionary<string, PlayerProperty> _props = new();

        public RosterPlayer(string id, Dictionary<string, PropRecord> props)
        {
            Id = id;
            if (props != null)
                foreach (var kv in props) _props[kv.Key] = new PlayerProperty(kv.Value.Value, (VisibilityPropertyOptions)kv.Value.Visibility);
        }

        public string Id { get; }
        public IReadOnlyDictionary<string, PlayerProperty> Properties => _props;

        public void SetProperty(string key, PlayerProperty property)
        {
            if (property == null) _props.Remove(key);
            else _props[key] = property;
        }

        internal Dictionary<string, PropRecord> Snapshot() => SessionRules.ToRecords(_props);
    }

    /// <summary>
    /// What a session handle shows of the last record it read: the roster (the local player's own
    /// writable entry in it), the host and the session properties. Applying a newer record raises
    /// PlayerLeaving for every player who is gone from it.
    /// </summary>
    internal sealed class SessionView
    {
        readonly string _selfId;

        public SessionView(string selfId, Dictionary<string, PropRecord> selfProps)
        {
            _selfId = selfId;
            Self = new RosterPlayer(selfId, selfProps);
        }

        public RosterPlayer Self { get; }
        public string HostId { get; private set; }
        public List<IReadOnlyPlayer> Players { get; private set; } = new();
        public Dictionary<string, SessionProperty> Properties { get; } = new();

        /// <summary>Applies <paramref name="rec"/>; returns the ids of the players who left since the last one.</summary>
        public List<string> Apply(SessionRecord rec)
        {
            HostId = rec.HostPlayerId;
            var before = new HashSet<string>(Players.Select(p => p.Id));
            var list = new List<IReadOnlyPlayer>();
            foreach (var p in rec.Players)
                list.Add(p.Id == _selfId ? Self : new RosterPlayer(p.Id, p.Properties));
            Players = list;
            Properties.Clear();
            foreach (var kv in rec.Properties)
                Properties[kv.Key] = new SessionProperty(kv.Value.Value, (VisibilityPropertyOptions)kv.Value.Visibility, (PropertyIndex)kv.Value.Index);
            var left = new List<string>();
            foreach (var id in before)
                if (list.All(p => p.Id != id)) left.Add(id);
            return left;
        }

        public void Clear() => Players = new List<IReadOnlyPlayer>();
    }

    /// <summary>
    /// A session's network through UGS Relay (docs/MULTIPLAYER.md §6.7-6.10): the host allocates and gets a join
    /// code BEFORE it starts listening (the relay transport binds the allocation on Listen), a joiner joins by that code.
    /// </summary>
    internal static class RelayedSessions
    {
        /// <summary>A relay is configured: sessions with a network go through it.</summary>
        public static bool Active => RelaySessions.Allocator != null;

        /// <summary>Hosts through the relay; the host's allocation (with its join code), or null when the NetworkManager was already running.</summary>
        public static async Task<RelayAllocation> HostAsync(NetworkManager nm, int maxPlayers)
        {
            if (nm.IsListening) return null;
            var relay = RelaySessions.Allocator;
            var alloc = await relay.AllocateAsync(Math.Max(1, (maxPlayers > 0 ? maxPlayers : 4) - 1), RelaySessions.Region);
            await relay.CreateJoinCodeAsync(alloc);
            RelaySessions.HostWith(alloc);
            nm.StartHost();
            return NetDriver.IsServer && alloc.JoinCode is { Length: > 0 } ? alloc : null;
        }

        /// <summary>Prepares the next client start to join <paramref name="joinCode"/>; the transport then connects to <see cref="RelaySessions.RelayAddress"/>.</summary>
        public static async Task<RelayAllocation> JoinAsync(string joinCode)
        {
            var relay = RelaySessions.Allocator
                ?? throw new SessionException("This session is reached through UGS Relay, and this player has no relay (not signed in to UGS).", SessionError.NetworkSetupFailed);
            var join = await relay.JoinAsync(joinCode);
            RelaySessions.JoinWith(join);
            return join;
        }
    }
}
