using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// The UGS Lobby client API over REST, without Unity's SDK (docs/MULTIPLAYER.md §6.10), written from
    /// Unity's public API reference (https://docs.unity.com/legacy-services-docs/lobby/v1/). Every call is
    /// made as the signed-in player: <c>Authorization: Bearer</c> the <see cref="UgsAuthentication"/> id token.
    /// A refusal is a <see cref="UgsServiceException"/> carrying the HTTP status and the service's title/detail.
    /// </summary>
    public sealed class UgsLobbyClient
    {
        public const string UgsBaseUrl = "https://lobby.services.api.unity.com/v1";

        readonly HttpClient _http;
        readonly Func<Task<string>> _token;

        public UgsLobbyClient(Func<Task<string>> accessToken, string baseUrl = UgsBaseUrl, HttpClient http = null)
        {
            _token = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
            _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _http.BaseAddress = new Uri((baseUrl ?? UgsBaseUrl).TrimEnd('/') + "/");
        }

        public Task<JsonNode> CreateAsync(JsonObject lobby) => Send(HttpMethod.Post, "create", lobby);
        public Task<JsonNode> QueryAsync(JsonObject query) => Send(HttpMethod.Post, "query", query);
        public Task<JsonNode> JoinByIdAsync(string lobbyId, JsonObject player) => Send(HttpMethod.Post, $"{Esc(lobbyId)}/join", new JsonObject { ["player"] = player });
        public Task<JsonNode> GetAsync(string lobbyId) => Send(HttpMethod.Get, Esc(lobbyId), null);
        public Task<JsonNode> UpdateAsync(string lobbyId, JsonObject changes) => Send(HttpMethod.Post, Esc(lobbyId), changes);
        public Task<JsonNode> UpdatePlayerAsync(string lobbyId, string playerId, JsonObject changes) => Send(HttpMethod.Post, $"{Esc(lobbyId)}/players/{Esc(playerId)}", changes);
        public Task RemovePlayerAsync(string lobbyId, string playerId) => Send(HttpMethod.Delete, $"{Esc(lobbyId)}/players/{Esc(playerId)}", null);
        public Task DeleteAsync(string lobbyId) => Send(HttpMethod.Delete, Esc(lobbyId), null);
        /// <summary>A host keeps its lobby active with one of these at least every 30 s.</summary>
        public Task HeartbeatAsync(string lobbyId) => Send(HttpMethod.Post, $"{Esc(lobbyId)}/heartbeat", new JsonObject());

        static string Esc(string s) => Uri.EscapeDataString(s ?? "");

        async Task<JsonNode> Send(HttpMethod method, string path, JsonObject body)
        {
            var token = await _token();
            using var req = new HttpRequestMessage(method, path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            HttpResponseMessage resp;
            try { resp = await _http.SendAsync(req); }
            catch (TaskCanceledException e) { throw new UgsServiceException(0, $"lobby {method} {path}: timed out", e); }
            catch (HttpRequestException e) { throw new UgsServiceException(0, $"lobby {method} {path}: {e.Message}", e); }
            using (resp)
            {
                var text = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new UgsServiceException((int)resp.StatusCode, $"lobby {method} {path}: {(int)resp.StatusCode} {Describe(text)}");
                return text.Length == 0 ? null : JsonNode.Parse(text);
            }
        }

        /// <summary>The service's error title and detail ("Lobby is full: ..."), or the raw body.</summary>
        static string Describe(string text)
        {
            try
            {
                if (JsonNode.Parse(text) is JsonObject o)
                {
                    var parts = new StringBuilder();
                    if ((string)o["title"] is { Length: > 0 } title) parts.Append(title);
                    if ((string)o["detail"] is { Length: > 0 } detail) parts.Append(parts.Length > 0 ? ": " : "").Append(detail);
                    if (o["details"] is JsonArray list)
                        foreach (var d in list)
                            if ((string)d?["message"] is { Length: > 0 } m) parts.Append(parts.Length > 0 ? "; " : "").Append(m);
                    if (parts.Length > 0) return parts.ToString();
                }
            }
            catch (System.Text.Json.JsonException) { }
            return text.Length > 300 ? text[..300] + "..." : text;
        }
    }
}
