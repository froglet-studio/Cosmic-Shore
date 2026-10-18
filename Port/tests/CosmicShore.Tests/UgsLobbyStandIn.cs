using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace CosmicShore.Tests
{
    /// <summary>
    /// UGS Lobby's client API on localhost, from Unity's public reference
    /// (https://docs.unity.com/legacy-services-docs/lobby/v1/), so <c>UgsMultiplayerService</c> is tested without
    /// the live project. The caller is the bearer token's player: <see cref="UgsAuthStandIn"/>'s id tokens
    /// ("id-PLAYER-N"), or <see cref="Token"/>. Refusals carry UGS's error body ({status, title, detail}).
    /// </summary>
    sealed class UgsLobbyStandIn : IDisposable
    {
        sealed class Lobby
        {
            public string Id, Code, HostId, Name;
            public int MaxPlayers;
            public bool IsPrivate, IsLocked;
            public DateTime Created = DateTime.UtcNow;
            public JsonObject Data = new();
            public readonly List<(string id, JsonObject data, string allocationId)> Players = new();
        }

        readonly HttpListener _http = new();
        readonly Thread _thread;
        readonly object _lock = new();
        readonly Dictionary<string, Lobby> _lobbies = new();
        readonly ConcurrentQueue<(int status, string title, string detail)> _faults = new();
        volatile bool _stopped;

        /// <summary>What UgsLobbyClient takes as its base: ".../v1".</summary>
        public string BaseUrl { get; }
        public readonly ConcurrentQueue<string> Calls = new();
        public readonly ConcurrentDictionary<string, int> Heartbeats = new();
        public int HeartbeatsRefused;

        public UgsLobbyStandIn()
        {
            var l = new TcpListener(IPAddress.Loopback, 0); l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
            _http.Prefixes.Add($"http://127.0.0.1:{port}/");
            BaseUrl = $"http://127.0.0.1:{port}/v1";
            _http.Start();
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Start();
        }

        /// <summary>An id token for <paramref name="player"/>, in <see cref="UgsAuthStandIn"/>'s shape.</summary>
        public static string Token(string player) => $"id-{player}-{Guid.NewGuid():N}";

        /// <summary>The next request is refused with this status and error.</summary>
        public void FailNext(int status, string title, string detail = "") => _faults.Enqueue((status, title, detail));

        /// <summary>The lobby's data as the service holds it (member and private keys included).</summary>
        public JsonObject DataOf(string lobbyId) { lock (_lock) return (JsonObject)_lobbies[lobbyId].Data.DeepClone(); }

        public string AllocationOf(string lobbyId, string player)
        {
            lock (_lock) return _lobbies[lobbyId].Players.FirstOrDefault(p => p.id == player).allocationId;
        }

        static string PlayerOf(string auth)
        {
            if (auth == null || !auth.StartsWith("Bearer id-", StringComparison.Ordinal)) return null;
            var t = auth["Bearer id-".Length..];
            int dash = t.LastIndexOf('-');
            return dash > 0 ? t[..dash] : null;
        }

        void Loop()
        {
            while (!_stopped)
            {
                HttpListenerContext ctx;
                try { ctx = _http.GetContext(); } catch { if (_stopped) return; continue; }
                int status;
                JsonNode body;
                try { (status, body) = Handle(ctx); }
                catch (Exception e) { (status, body) = Error(500, "Internal", e.Message); }
                try
                {
                    ctx.Response.StatusCode = status;
                    if (body != null)
                    {
                        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.OutputStream.Write(bytes);
                    }
                    ctx.Response.Close();
                }
                catch { }
            }
        }

        static (int, JsonNode) Error(int status, string title, string detail) =>
            (status, new JsonObject { ["status"] = status, ["title"] = title, ["detail"] = detail, ["details"] = new JsonArray() });

        (int, JsonNode) Handle(HttpListenerContext ctx)
        {
            var method = ctx.Request.HttpMethod;
            var path = ctx.Request.Url!.AbsolutePath;
            Calls.Enqueue($"{method} {path}");
            var text = new StreamReader(ctx.Request.InputStream, Encoding.UTF8).ReadToEnd();
            var req = text.Length > 0 ? JsonNode.Parse(text) as JsonObject : null;
            if (_faults.TryDequeue(out var f)) return Error(f.status, f.title, f.detail);
            var who = PlayerOf(ctx.Request.Headers["Authorization"]);
            if (who == null) return Error(401, "Unauthorized", "no bearer token");
            if (!path.StartsWith("/v1/", StringComparison.Ordinal)) return Error(404, "Not Found", path);
            var parts = path["/v1/".Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();

            lock (_lock)
            {
                if (method == "POST" && parts is ["create"]) return Create(who, req);
                if (method == "POST" && parts is ["query"]) return Query(req);
                if (parts.Length == 0 || !_lobbies.TryGetValue(parts[0], out var lobby)) return Error(404, "Not Found", "lobby not found");
                bool member = lobby.Players.Any(p => p.id == who);
                switch (method, parts.Length > 1 ? parts[1] : null)
                {
                    case ("POST", "join"):
                        if (member) return (200, Json(lobby));
                        if (lobby.IsLocked) return Error(403, "Forbidden", "lobby is locked");
                        if (lobby.Players.Count >= lobby.MaxPlayers) return Error(409, "Conflict", "lobby is full");
                        lobby.Players.Add((who, Data(req?["player"]?["data"]), null));
                        return (200, Json(lobby));
                    case ("GET", null):
                        return member ? (200, Json(lobby)) : Error(403, "Forbidden", "player is not a member of the lobby");
                    case ("POST", "heartbeat"):
                        if (lobby.HostId != who) { Interlocked.Increment(ref HeartbeatsRefused); return Error(403, "Forbidden", "only the host heartbeats"); }
                        Heartbeats.AddOrUpdate(lobby.Id, 1, (_, n) => n + 1);
                        return (204, null);
                    case ("DELETE", null):
                        if (lobby.HostId != who) return Error(403, "Forbidden", "only the host deletes");
                        _lobbies.Remove(lobby.Id);
                        return (204, null);
                    case ("POST", null):
                        if (lobby.HostId != who) return Error(403, "Forbidden", "only the host updates");
                        if (req?["isLocked"] is JsonNode l) lobby.IsLocked = (bool)l;
                        Merge(lobby.Data, req?["data"] as JsonObject);
                        return (200, Json(lobby));
                    case ("POST", "players") when parts.Length == 3:
                    {
                        if (parts[2] != who) return Error(403, "Forbidden", "a player updates only itself");
                        int i = lobby.Players.FindIndex(p => p.id == who);
                        if (i < 0) return Error(404, "Not Found", "player not found");
                        var p = lobby.Players[i];
                        Merge(p.data, req?["data"] as JsonObject);
                        if ((string)req?["allocationId"] is { } alloc) p.allocationId = alloc;
                        lobby.Players[i] = p;
                        return (200, Json(lobby));
                    }
                    case ("DELETE", "players") when parts.Length == 3:
                    {
                        if (parts[2] != who && lobby.HostId != who) return Error(403, "Forbidden", "only the host removes others");
                        if (lobby.Players.RemoveAll(p => p.id == parts[2]) == 0) return Error(404, "Not Found", "player not found");
                        if (lobby.Players.Count == 0) _lobbies.Remove(lobby.Id);
                        else if (lobby.HostId == parts[2]) lobby.HostId = lobby.Players[0].id; // host migration
                        return (204, null);
                    }
                }
                return Error(404, "Not Found", $"{method} {path}");
            }
        }

        (int, JsonNode) Create(string who, JsonObject req)
        {
            if ((string)req?["player"]?["id"] != who) return Error(400, "Bad Request", "player.id must be the caller");
            var lobby = new Lobby
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = new string(Enumerable.Range(0, 6).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[Random.Shared.Next(32)]).ToArray()),
                HostId = who,
                Name = (string)req["name"],
                MaxPlayers = (int?)req["maxPlayers"] ?? 4,
                IsPrivate = (bool?)req["isPrivate"] ?? false,
                IsLocked = (bool?)req["isLocked"] ?? false,
            };
            if (lobby.MaxPlayers < 1 || lobby.MaxPlayers > 100) return Error(400, "Bad Request", "maxPlayers out of range");
            Merge(lobby.Data, req["data"] as JsonObject);
            lobby.Players.Add((who, Data(req["player"]?["data"]), (string)req["player"]?["allocationId"]));
            _lobbies[lobby.Id] = lobby;
            return (200, Json(lobby));
        }

        (int, JsonNode) Query(JsonObject req)
        {
            var results = new JsonArray();
            foreach (var l in _lobbies.Values.Where(l => !l.IsPrivate))
            {
                bool ok = true;
                foreach (var f in req?["filter"] as JsonArray ?? new JsonArray())
                {
                    var field = (string)f["field"];
                    var value = l.Data.Where(kv => (string)kv.Value?["index"] == field).Select(kv => (string)kv.Value["value"]).FirstOrDefault();
                    ok &= (string)f["op"] switch
                    {
                        "EQ" => value == (string)f["value"],
                        "NE" => value != (string)f["value"],
                        _ => true,
                    };
                }
                if (ok) results.Add(Json(l, publicOnly: true));
            }
            return (200, new JsonObject { ["results"] = results });
        }

        static JsonObject Data(JsonNode n) => n is JsonObject o ? (JsonObject)o.DeepClone() : new JsonObject();

        /// <summary>UGS's update: a key sent as null is removed, any other replaces.</summary>
        static void Merge(JsonObject into, JsonObject changes)
        {
            if (changes == null) return;
            foreach (var kv in changes)
                if (kv.Value == null) into.Remove(kv.Key);
                else into[kv.Key] = kv.Value.DeepClone();
        }

        static JsonObject Json(Lobby l, bool publicOnly = false)
        {
            var data = new JsonObject();
            foreach (var kv in l.Data)
                if (!publicOnly || (string)kv.Value?["visibility"] == "public") data[kv.Key] = kv.Value?.DeepClone();
            var players = new JsonArray();
            foreach (var p in l.Players)
                players.Add(new JsonObject { ["id"] = p.id, ["data"] = publicOnly ? new JsonObject() : p.data.DeepClone(), ["allocationId"] = p.allocationId });
            return new JsonObject
            {
                ["id"] = l.Id, ["lobbyCode"] = publicOnly ? null : l.Code, ["hostId"] = l.HostId, ["name"] = l.Name,
                ["maxPlayers"] = l.MaxPlayers, ["availableSlots"] = l.MaxPlayers - l.Players.Count,
                ["isPrivate"] = l.IsPrivate, ["isLocked"] = l.IsLocked, ["created"] = l.Created.ToString("o"),
                ["players"] = players, ["data"] = data,
            };
        }

        public void Dispose() { _stopped = true; try { _http.Stop(); _http.Close(); } catch { } }
    }
}
