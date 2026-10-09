using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Online
{
    /// <summary>A signed-in UGS player. The tokens stay in memory (and the session token in the cache file); they are never logged.</summary>
    public sealed record UgsPlayer(string PlayerId, string IdToken, string SessionToken, DateTimeOffset ExpiresAt);

    /// <summary>One way to reach a Relay server, as the Allocations service lists it.</summary>
    public sealed record RelayEndpoint(string ConnectionType, string Host, int Port, bool Secure);

    /// <summary>A region's QoS servers ("host:port" endpoints), from the QoS Discovery service.</summary>
    public sealed record QosServer(string Region, IReadOnlyList<string> Endpoints);

    /// <summary>
    /// What the Allocations service returns for /allocate (host) or /join (joining player).
    /// <see cref="HostConnectionData"/> is only set on a join: the host's connection data, which a joiner
    /// puts in its CONNECT_REQUEST.
    /// </summary>
    public sealed record RelayAllocation(
        Guid AllocationId,
        byte[] AllocationIdBytes,
        byte[] ConnectionData,
        byte[] Key,
        byte[] HostConnectionData,
        string Region,
        IReadOnlyList<RelayEndpoint> Endpoints)
    {
        public RelayEndpoint Endpoint(string connectionType)
        {
            foreach (var e in Endpoints)
                if (string.Equals(e.ConnectionType, connectionType, StringComparison.OrdinalIgnoreCase)) return e;
            throw new InvalidOperationException($"the allocation has no '{connectionType}' endpoint");
        }
    }

    /// <summary>
    /// The UGS client APIs Prisma uses, as an interface so tests (and an offline build) can stand in.
    /// Every call takes the player whose id token authorizes it.
    /// </summary>
    public interface IUgsApi
    {
        Task<UgsPlayer> SignInAnonymouslyAsync(CancellationToken ct = default);
        /// <summary>Signs the SAME player back in from a session token an earlier sign-in returned.</summary>
        Task<UgsPlayer> SignInWithSessionTokenAsync(string sessionToken, CancellationToken ct = default);
        Task<IReadOnlyList<string>> ListRegionsAsync(UgsPlayer player, CancellationToken ct = default);
        Task<IReadOnlyList<QosServer>> ListQosServersAsync(UgsPlayer player, CancellationToken ct = default);
        Task<RelayAllocation> AllocateAsync(UgsPlayer player, int maxConnections, string region, CancellationToken ct = default);
        Task<string> CreateJoinCodeAsync(UgsPlayer player, Guid allocationId, CancellationToken ct = default);
        Task<RelayAllocation> JoinAsync(UgsPlayer player, string joinCode, CancellationToken ct = default);
    }

    /// <summary>
    /// The UGS REST client, with no Unity SDK (Unity's public web API docs):
    ///  • Authentication: player-auth.services.api.unity.com/v1/authentication/anonymous and
    ///    /session-token (ProjectId + UnityEnvironment headers) → idToken, the Bearer for the rest.
    ///  • Relay Allocations: relay-allocations.services.api.unity.com/v1/allocate, /joincode, /join, /regions.
    ///  • QoS Discovery: qos-discovery.services.api.unity.com/v1/services/relay/servers.
    /// Docs: https://docs.unity.com/en-us/services-web-apis/client-auth,
    ///       https://docs.unity.com/en-us/oas-relay-allocations/1.0.0, https://docs.unity.com/en-us/oas-qos/1.0.0
    /// </summary>
    public sealed class UgsClient : IUgsApi, IDisposable
    {
        const string AuthBase = "https://player-auth.services.api.unity.com/v1/authentication/";
        const string RelayBase = "https://relay-allocations.services.api.unity.com/v1/";
        const string QosBase = "https://qos-discovery.services.api.unity.com/v1/";

        readonly HttpClient _http;
        public string ProjectId { get; }
        public string Environment { get; }

        /// <param name="handler">Tests pass a fake; null = the real network.</param>
        public UgsClient(string projectId, string environment, HttpMessageHandler handler = null)
        {
            ProjectId = projectId;
            Environment = environment;
            _http = handler != null ? new HttpClient(handler) : new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(20);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Prisma-CosmicShore/1.0");
        }

        public Task<UgsPlayer> SignInAnonymouslyAsync(CancellationToken ct = default)
            => SignInAsync("anonymous", new JsonObject(), ct);

        public Task<UgsPlayer> SignInWithSessionTokenAsync(string sessionToken, CancellationToken ct = default)
            => SignInAsync("session-token", new JsonObject { ["sessionToken"] = sessionToken }, ct);

        async Task<UgsPlayer> SignInAsync(string path, JsonObject body, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, AuthBase + path)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("ProjectId", ProjectId);
            req.Headers.Add("UnityEnvironment", Environment);
            var json = await SendAsync(req, ct).ConfigureAwait(false);
            return new UgsPlayer(
                (string)json["userId"],
                (string)json["idToken"],
                (string)json["sessionToken"],
                DateTimeOffset.UtcNow.AddSeconds((int?)json["expiresIn"] ?? 3600));
        }

        public async Task<IReadOnlyList<string>> ListRegionsAsync(UgsPlayer p, CancellationToken ct = default)
        {
            using var req = Authed(p, HttpMethod.Get, RelayBase + "regions", null);
            var json = await SendAsync(req, ct).ConfigureAwait(false);
            var list = new List<string>();
            foreach (var r in json["data"]?["regions"]?.AsArray() ?? new JsonArray())
                if (r?["id"] is JsonNode id) list.Add((string)id);
            return list;
        }

        public async Task<IReadOnlyList<QosServer>> ListQosServersAsync(UgsPlayer p, CancellationToken ct = default)
        {
            using var req = Authed(p, HttpMethod.Get, QosBase + "services/relay/servers", null);
            var json = await SendAsync(req, ct).ConfigureAwait(false);
            var list = new List<QosServer>();
            foreach (var s in json["data"]?["servers"]?.AsArray() ?? new JsonArray())
            {
                var eps = new List<string>();
                foreach (var e in s?["endpoints"]?.AsArray() ?? new JsonArray()) if (e != null) eps.Add((string)e);
                if (s?["region"] is JsonNode region) list.Add(new QosServer((string)region, eps));
            }
            return list;
        }

        public async Task<RelayAllocation> AllocateAsync(UgsPlayer p, int maxConnections, string region, CancellationToken ct = default)
        {
            var body = new JsonObject { ["maxConnections"] = maxConnections };
            if (!string.IsNullOrEmpty(region)) body["region"] = region;
            using var req = Authed(p, HttpMethod.Post, RelayBase + "allocate", body);
            return ParseAllocation((await SendAsync(req, ct).ConfigureAwait(false))["data"]["allocation"]);
        }

        public async Task<string> CreateJoinCodeAsync(UgsPlayer p, Guid allocationId, CancellationToken ct = default)
        {
            using var req = Authed(p, HttpMethod.Post, RelayBase + "joincode", new JsonObject { ["allocationId"] = allocationId.ToString() });
            return (string)(await SendAsync(req, ct).ConfigureAwait(false))["data"]["joinCode"];
        }

        public async Task<RelayAllocation> JoinAsync(UgsPlayer p, string joinCode, CancellationToken ct = default)
        {
            using var req = Authed(p, HttpMethod.Post, RelayBase + "join", new JsonObject { ["joinCode"] = joinCode });
            return ParseAllocation((await SendAsync(req, ct).ConfigureAwait(false))["data"]["allocation"]);
        }

        internal static RelayAllocation ParseAllocation(JsonNode a)
        {
            var eps = new List<RelayEndpoint>();
            foreach (var e in a["serverEndpoints"]?.AsArray() ?? new JsonArray())
                eps.Add(new RelayEndpoint((string)e["connectionType"], (string)e["host"], (int)e["port"], (bool?)e["secure"] ?? false));
            return new RelayAllocation(
                Guid.Parse((string)a["allocationId"]),
                Convert.FromBase64String((string)a["allocationIdBytes"]),
                Convert.FromBase64String((string)a["connectionData"]),
                Convert.FromBase64String((string)a["key"]),
                a["hostConnectionData"] is JsonNode h ? Convert.FromBase64String((string)h) : null,
                (string)a["region"] ?? "",
                eps);
        }

        static HttpRequestMessage Authed(UgsPlayer p, HttpMethod m, string url, JsonNode body)
        {
            var req = new HttpRequestMessage(m, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", p.IdToken);
            if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            return req;
        }

        async Task<JsonNode> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var text = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                throw new UgsException((int)res.StatusCode, req.RequestUri.AbsolutePath, Describe(text));
            return JsonNode.Parse(text);
        }

        /// <summary>Error bodies are problem+json: keep title/detail/code and drop the rest (nothing secret is echoed).</summary>
        static string Describe(string body)
        {
            try
            {
                var j = JsonNode.Parse(body);
                return $"{j?["title"]} {j?["detail"]} (code {j?["code"]})".Trim();
            }
            catch (JsonException) { return body.Length > 200 ? body[..200] : body; }
        }

        public void Dispose() => _http.Dispose();
    }

    public sealed class UgsException : Exception
    {
        public int Status { get; }
        public UgsException(int status, string path, string detail) : base($"HTTP {status} {path}: {detail}") => Status = status;
    }
}
