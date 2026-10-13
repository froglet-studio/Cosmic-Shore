using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.Engine.Networking
{
    // The lobby's data and rules, shared by every session backend: the shared-folder one
    // (DirectoryMultiplayerService, one machine) and the online server (OnlineServer, any network).
    // Both keep the same records and change them through the same rules, so a session behaves the
    // same whichever backend holds it - the semantics the UGS Lobby gives the game.

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
        /// <summary>Shared-folder sessions: the host's TCP endpoint a join connects to.</summary>
        public string RelayAddress { get; set; }
        public int RelayPort { get; set; }
        /// <summary>Online sessions: the session carries a Relay network (joins connect through the server).</summary>
        public bool Relay { get; set; }
        public Dictionary<string, PropRecord> Properties { get; set; } = new();
        public List<PlayerRecord> Players { get; set; } = new();
    }

    /// <summary>Why a lobby rule refused: the game classifies these through <see cref="SessionException.Error"/>.</summary>
    internal readonly struct SessionRefusal
    {
        public readonly SessionError Error;
        public readonly string Message;
        public SessionRefusal(SessionError error, string message) { Error = error; Message = message; }
        public SessionException ToException() => new(Error, Message);
    }

    /// <summary>
    /// The lobby's rules over <see cref="SessionRecord"/>s. Liveness mirrors lobby heartbeats: a
    /// player unseen for <see cref="PlayerTimeout"/> is dropped from the roster, a session whose
    /// host is gone is no longer listed or joinable, and a refresh migrates such a session to a
    /// remaining player.
    /// </summary>
    internal static class SessionRules
    {
        public static readonly TimeSpan PlayerTimeout = TimeSpan.FromSeconds(30);

        public static long NowTicks => DateTime.UtcNow.Ticks;

        public static bool Alive(PlayerRecord p, long now) => now - p.LastSeen < PlayerTimeout.Ticks;

        public static void Prune(SessionRecord rec, long now) => rec.Players.RemoveAll(p => !Alive(p, now));

        public static bool HostAlive(SessionRecord rec, long now) => rec.Players.Any(p => p.Id == rec.HostPlayerId && Alive(p, now));

        /// <summary>Every player gone for four timeouts: the service has expired the session.</summary>
        public static bool Expired(SessionRecord rec, long now) => rec.Players.All(p => now - p.LastSeen > PlayerTimeout.Ticks * 4);

        public static Dictionary<string, PropRecord> ToRecords(IDictionary<string, PlayerProperty> props)
        {
            var d = new Dictionary<string, PropRecord>();
            if (props != null)
                foreach (var kv in props) if (kv.Value != null) d[kv.Key] = new PropRecord { Value = kv.Value.Value, Visibility = (int)kv.Value.Visibility };
            return d;
        }

        /// <summary>A new session record for <paramref name="hostId"/>, built from the SDK's create options.</summary>
        public static SessionRecord NewSession(SessionOptions options, string hostId, long now)
        {
            var rec = new SessionRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = NewCode(),
                Created = now,
                HostPlayerId = hostId,
                MaxPlayers = options?.MaxPlayers ?? 0,
                IsLocked = options?.IsLocked ?? false,
                IsPrivate = options?.IsPrivate ?? false,
            };
            if (options?.SessionProperties != null)
                foreach (var kv in options.SessionProperties)
                    rec.Properties[kv.Key] = new PropRecord { Value = kv.Value.Value, Index = (int)kv.Value.Index, Visibility = (int)kv.Value.Visibility };
            rec.Players.Add(new PlayerRecord { Id = hostId, Properties = ToRecords(options?.PlayerProperties), LastSeen = now });
            return rec;
        }

        /// <summary>
        /// Adds <paramref name="playerId"/> to a live session (or refreshes its entry when it is
        /// already in): refused when the session is gone, locked or full.
        /// </summary>
        public static SessionRefusal? Join(SessionRecord rec, string sessionId, string playerId, Dictionary<string, PropRecord> props, long now)
        {
            if (rec == null || !HostAlive(rec, now)) return new SessionRefusal(SessionError.SessionNotFound, $"Session '{sessionId}' not found.");
            Prune(rec, now);
            var self = rec.Players.FirstOrDefault(p => p.Id == playerId);
            if (self == null)
            {
                if (rec.IsLocked) return new SessionRefusal(SessionError.Unknown, "Session is locked.");
                if (rec.MaxPlayers > 0 && rec.Players.Count >= rec.MaxPlayers) return new SessionRefusal(SessionError.Unknown, "Session is full.");
                self = new PlayerRecord { Id = playerId };
                rec.Players.Add(self);
            }
            self.Properties = props ?? new Dictionary<string, PropRecord>();
            self.LastSeen = now;
            return null;
        }

        /// <summary>Listed by a query: the host is alive, the session is public and open, and every filter matches.</summary>
        public static bool Listed(SessionRecord rec, IEnumerable<FilterOption> filters, long now)
        {
            if (!HostAlive(rec, now)) return false;
            if (rec.IsPrivate || rec.IsLocked) return false;
            return filters == null || filters.All(f => Matches(rec, f));
        }

        public static bool Matches(SessionRecord rec, FilterOption f)
        {
            int index = (int)f.Field;
            string value = rec.Properties.Values.FirstOrDefault(p => p.Index == index)?.Value;
            int cmp = string.CompareOrdinal(value ?? "", f.Value ?? "");
            return f.Operation switch
            {
                FilterOperation.Equal => cmp == 0,
                FilterOperation.NotEqual => cmp != 0,
                FilterOperation.Greater => cmp > 0,
                FilterOperation.Less => cmp < 0,
                _ => true,
            };
        }

        /// <summary>
        /// A refresh by <paramref name="playerId"/>: drops players gone silent, migrates a session
        /// whose host left to a remaining player, and stamps the caller as seen. Returns false when
        /// the caller is no longer in the session.
        /// </summary>
        public static bool Refresh(SessionRecord rec, string playerId, long now)
        {
            Prune(rec, now);
            if (rec.Players.Count > 0 && rec.Players.All(p => p.Id != rec.HostPlayerId)) rec.HostPlayerId = rec.Players[0].Id;
            var me = rec.Players.FirstOrDefault(p => p.Id == playerId);
            if (me == null) return false;
            me.LastSeen = now;
            return true;
        }

        /// <summary>
        /// The caller leaves: returns true when the session is now empty (and should be removed);
        /// a host leaving hands the session to the next player.
        /// </summary>
        public static bool Leave(SessionRecord rec, string playerId)
        {
            rec.Players.RemoveAll(p => p.Id == playerId);
            if (rec.Players.Count == 0) return true;
            if (rec.HostPlayerId == playerId) rec.HostPlayerId = rec.Players[0].Id;
            return false;
        }

        public static SessionInfo Info(SessionRecord r) => new(r);

        static readonly System.Random s_rng = new();

        public static string NewCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            lock (s_rng) return new string(Enumerable.Range(0, 6).Select(_ => alphabet[s_rng.Next(alphabet.Length)]).ToArray());
        }
    }

    /// <summary>A query result: discovery-level metadata of one listed session.</summary>
    internal sealed class SessionInfo : ISessionInfo
    {
        public SessionInfo(SessionRecord r)
        {
            Id = r.Id;
            Created = new DateTime(r.Created, DateTimeKind.Utc);
            MaxPlayers = r.MaxPlayers;
            AvailableSlots = r.MaxPlayers > 0 ? Math.Max(0, r.MaxPlayers - r.Players.Count) : int.MaxValue;
            IsLocked = r.IsLocked;
        }

        public string Id { get; }
        public DateTime Created { get; }
        public int MaxPlayers { get; }
        public int AvailableSlots { get; }
        public bool IsLocked { get; }
        public bool HasPassword => false;
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
}
