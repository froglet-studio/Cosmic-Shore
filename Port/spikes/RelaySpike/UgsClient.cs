using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Prisma.RelaySpike
{
    /// <summary>A signed-in UGS player. Tokens stay in memory and are never logged.</summary>
    public sealed record UgsPlayer(string PlayerId, string IdToken, string SessionToken, DateTimeOffset ExpiresAt);

    /// <summary>One way to reach a Relay server, as the Allocations service lists it.</summary>
    public sealed record RelayEndpoint(string ConnectionType, string Host, int Port, bool Secure);

    /// <summary>
    /// What the Allocations service returns for /allocate (host) or /join (joining player).
    /// <see cref="HostConnectionData"/> is only set on a join: it is the host's encrypted
    /// connection data, which a joiner puts in its CONNECT_REQUEST.
    /// </summary>
    public sealed record RelayAllocation(
        Guid AllocationId,
        byte[] AllocationIdBytes,
        byte[] ConnectionData,
        byte[] Key,
        byte[]? HostConnectionData,
        string Region,
        IReadOnlyList<RelayEndpoint> Endpoints)
    {
        public RelayEndpoint Endpoint(string connectionType)
        {
            foreach (var e in Endpoints)
                if (string.Equals(e.ConnectionType, connectionType, StringComparison.OrdinalIgnoreCase)) return e;
            throw new InvalidOperationException($"allocation has no '{connectionType}' endpoint");
        }
    }

    /// <summary>
    /// The UGS client REST APIs this spike needs, with no Unity SDK:
    ///  • Authentication: POST player-auth.services.api.unity.com/v1/authentication/anonymous
    ///    (ProjectId + UnityEnvironment headers) → idToken (Bearer for every client API).
    ///  • Relay Allocations: /v1/allocate, /v1/joincode, /v1/join, /v1/regions.
    /// Docs: https://docs.unity.com/en-us/services-web-apis/client-auth,
    ///       https://docs.unity.com/en-us/oas-relay-allocations/1.0.0
    /// </summary>
    public sealed class UgsClient : IDisposable
    {
        const string AuthBase = "https://player-auth.services.api.unity.com/v1/authentication/";
        const string RelayBase = "https://relay-allocations.services.api.unity.com/v1/";

        readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        public string ProjectId { get; }
        public string Environment { get; }

        public UgsClient(string projectId, string environment)
        {
            ProjectId = projectId;
            Environment = environment;
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("PrismaRelaySpike/0.1");
        }

        public async Task<UgsPlayer> SignInAnonymouslyAsync(CancellationToken ct = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, AuthBase + "anonymous")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("ProjectId", ProjectId);
            req.Headers.Add("UnityEnvironment", Environment);
            var json = await SendAsync(req, ct);
            return new UgsPlayer(
                (string)json["userId"]!,
                (string)json["idToken"]!,
                (string)json["sessionToken"]!,
                DateTimeOffset.UtcNow.AddSeconds((int?)json["expiresIn"] ?? 3600));
        }

        public async Task<IReadOnlyList<string>> ListRegionsAsync(UgsPlayer p, CancellationToken ct = default)
        {
            using var req = Authed(p, HttpMethod.Get, RelayBase + "regions", null);
            var json = await SendAsync(req, ct);
            var list = new List<string>();
            foreach (var r in json["data"]?["regions"]?.AsArray() ?? new JsonArray())
                if (r?["id"] is JsonNode id) list.Add((string)id!);
            return list;
        }

        public async Task<RelayAllocation> AllocateAsync(UgsPlayer p, int maxConnections, string? region = null, CancellationToken ct = default)
        {
            var body = new JsonObject { ["maxConnections"] = maxConnections };
            if (!string.IsNullOrEmpty(region)) body["region"] = region;
            using var req = Authed(p, HttpMethod.Post, RelayBase + "allocate", body);
            return ParseAllocation((await SendAsync(req, ct))["data"]!["allocation"]!);
        }

        public async Task<string> CreateJoinCodeAsync(UgsPlayer p, Guid allocationId, CancellationToken ct = default)
        {
            using var req = Authed(p, HttpMethod.Post, RelayBase + "joincode", new JsonObject { ["allocationId"] = allocationId.ToString() });
            return (string)(await SendAsync(req, ct))["data"]!["joinCode"]!;
        }

        public async Task<RelayAllocation> JoinAsync(UgsPlayer p, string joinCode, CancellationToken ct = default)
        {
            using var req = Authed(p, HttpMethod.Post, RelayBase + "join", new JsonObject { ["joinCode"] = joinCode });
            return ParseAllocation((await SendAsync(req, ct))["data"]!["allocation"]!);
        }

        static RelayAllocation ParseAllocation(JsonNode a)
        {
            var eps = new List<RelayEndpoint>();
            foreach (var e in a["serverEndpoints"]?.AsArray() ?? new JsonArray())
                eps.Add(new RelayEndpoint((string)e!["connectionType"]!, (string)e["host"]!, (int)e["port"]!, (bool?)e["secure"] ?? false));
            return new RelayAllocation(
                Guid.Parse((string)a["allocationId"]!),
                Convert.FromBase64String((string)a["allocationIdBytes"]!),
                Convert.FromBase64String((string)a["connectionData"]!),
                Convert.FromBase64String((string)a["key"]!),
                a["hostConnectionData"] is JsonNode h ? Convert.FromBase64String((string)h!) : null,
                (string?)a["region"] ?? "",
                eps);
        }

        HttpRequestMessage Authed(UgsPlayer p, HttpMethod m, string url, JsonNode? body)
        {
            var req = new HttpRequestMessage(m, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", p.IdToken);
            if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            return req;
        }

        async Task<JsonNode> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            using var res = await _http.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new UgsException((int)res.StatusCode, req.RequestUri!.AbsolutePath, Describe(text));
            return JsonNode.Parse(text)!;
        }

        /// <summary>Error bodies are problem+json; keep title/detail/code, drop anything else.</summary>
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
