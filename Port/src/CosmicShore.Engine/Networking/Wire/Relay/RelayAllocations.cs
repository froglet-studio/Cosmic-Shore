using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// One relay allocation: what a player needs to BIND to a relay server and be reached through it.
    /// The shape is the Relay Allocations API's (<c>data.allocation</c>), which Froglet's own relay server
    /// answers in too (docs/MULTIPLAYER.md §6.7).
    /// </summary>
    public sealed class RelayAllocation
    {
        public string AllocationId = "";
        /// <summary>The 16 bytes every relay message names this player by.</summary>
        public byte[] AllocationIdBytes = new byte[RelayProtocol.AllocationIdLength];
        public byte[] ConnectionData = Array.Empty<byte>();
        /// <summary>A joiner's: the host's connection data, which its CONNECT_REQUEST names. Empty for a host.</summary>
        public byte[] HostConnectionData = Array.Empty<byte>();
        /// <summary>The BIND HMAC key.</summary>
        public byte[] Key = Array.Empty<byte>();
        public string ServerHost = "";
        public int ServerPort;
        public string Region = "";
        /// <summary>The code other players join with (a host's, once created).</summary>
        public string JoinCode = "";

        public bool IsJoin => HostConnectionData.Length > 0;
        public string LinkKey => RelayProtocol.Key(AllocationIdBytes);

        /// <summary>Reads <c>data.allocation</c> (or the allocation object itself).</summary>
        public static RelayAllocation FromJson(JsonNode node)
        {
            var a = node?["data"]?["allocation"] ?? node?["allocation"] ?? node ?? throw new FormatException("no allocation in the response");
            var r = new RelayAllocation
            {
                AllocationId = (string)a["allocationId"] ?? "",
                ConnectionData = B64(a["connectionData"]),
                HostConnectionData = B64(a["hostConnectionData"]),
                Key = B64(a["key"]),
                Region = (string)a["region"] ?? "",
            };
            var idBytes = B64(a["allocationIdBytes"]);
            if (idBytes.Length == RelayProtocol.AllocationIdLength) r.AllocationIdBytes = idBytes;
            else if (Guid.TryParse(r.AllocationId, out var g)) r.AllocationIdBytes = g.ToByteArray(bigEndian: true); // RFC 4122 order
            else throw new FormatException("the allocation has no usable id");
            // A plain UDP endpoint: Prisma's transport does its own reliability; DTLS and WebSocket are not used.
            var udp = (a["serverEndpoints"] as JsonArray)?.FirstOrDefault(e => (string)e?["connectionType"] == "udp");
            if (udp != null) { r.ServerHost = (string)udp["host"] ?? ""; r.ServerPort = (int?)udp["port"] ?? 0; }
            else { r.ServerHost = (string)a["relayServer"]?["ipV4"] ?? ""; r.ServerPort = (int?)a["relayServer"]?["port"] ?? 0; }
            if (r.ServerHost.Length == 0 || r.ServerPort <= 0) throw new FormatException("the allocation names no UDP relay endpoint");
            return r;
        }

        static byte[] B64(JsonNode n) => n is null ? Array.Empty<byte>() : Convert.FromBase64String((string)n);

        public JsonObject ToJson() => new()
        {
            ["allocationId"] = AllocationId,
            ["allocationIdBytes"] = Convert.ToBase64String(AllocationIdBytes),
            ["relayServer"] = new JsonObject { ["ipV4"] = ServerHost, ["port"] = ServerPort },
            ["key"] = Convert.ToBase64String(Key),
            ["region"] = Region,
            ["connectionData"] = Convert.ToBase64String(ConnectionData),
            ["serverEndpoints"] = new JsonArray(new JsonObject
            {
                ["connectionType"] = "udp", ["host"] = ServerHost, ["port"] = ServerPort, ["network"] = "udp", ["secure"] = false, ["reliable"] = false,
            }),
        };
    }

    /// <summary>Allocates relay slots: create a host allocation, give it a join code, join one by code.</summary>
    public interface IRelayAllocator
    {
        Task<RelayAllocation> AllocateAsync(int maxConnections, string region = null);
        Task<string> CreateJoinCodeAsync(RelayAllocation hostAllocation);
        Task<RelayAllocation> JoinAsync(string joinCode);
    }

    /// <summary>
    /// The Relay Allocations REST API client: <c>POST /v1/allocate</c>, <c>/v1/joincode</c>, <c>/v1/join</c>.
    /// Against UGS the base URL is <see cref="UgsBaseUrl"/> and every call carries the signed-in player's
    /// token; against Froglet's relay server (<see cref="FrogletRelayServer"/>) no token is needed.
    /// </summary>
    public sealed class RelayAllocationClient : IRelayAllocator
    {
        public const string UgsBaseUrl = "https://relay-allocations.services.api.unity.com";

        readonly HttpClient _http;
        readonly Func<Task<string>> _token;

        public RelayAllocationClient(string baseUrl, Func<Task<string>> bearerToken = null, HttpClient http = null)
        {
            _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            _token = bearerToken;
        }

        public string BaseUrl => _http.BaseAddress?.ToString();

        async Task<JsonNode> Post(string path, JsonObject body)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
            if (_token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _token());
            using var resp = await _http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new RelayServiceException((int)resp.StatusCode, $"relay {path}: {(int)resp.StatusCode} {Trim(text)}");
            return JsonNode.Parse(text);
        }

        static string Trim(string s) => s.Length > 300 ? s[..300] + "..." : s;

        public async Task<RelayAllocation> AllocateAsync(int maxConnections, string region = null)
        {
            var body = new JsonObject { ["maxConnections"] = Math.Max(1, maxConnections) };
            if (!string.IsNullOrEmpty(region)) body["region"] = region;
            return RelayAllocation.FromJson(await Post("v1/allocate", body));
        }

        public async Task<string> CreateJoinCodeAsync(RelayAllocation hostAllocation)
        {
            var r = await Post("v1/joincode", new JsonObject { ["allocationId"] = hostAllocation.AllocationId });
            var code = (string)r?["data"]?["joinCode"] ?? throw new FormatException("no joinCode in the response");
            hostAllocation.JoinCode = code;
            return code;
        }

        public async Task<RelayAllocation> JoinAsync(string joinCode)
        {
            var a = RelayAllocation.FromJson(await Post("v1/join", new JsonObject { ["joinCode"] = joinCode }));
            a.JoinCode = joinCode;
            return a;
        }
    }

    /// <summary>A relay service refused a call; <see cref="Status"/> is the HTTP status (404 unknown join code, 429 rate limited ...).</summary>
    public sealed class RelayServiceException : Exception
    {
        public int Status { get; }
        public RelayServiceException(int status, string message) : base(message) => Status = status;
    }
}
