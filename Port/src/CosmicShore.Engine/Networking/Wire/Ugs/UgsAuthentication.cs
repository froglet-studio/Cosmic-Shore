using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// A UGS player's sign-in over the Player Authentication REST API, without Unity's SDK
    /// (docs/MULTIPLAYER.md §6.8). It gives other UGS calls their bearer token: the
    /// <c>idToken</c> the sign-in returns, which is what Unity's own Relay client sends.
    ///
    ///   - First sign-in: <c>POST /v1/authentication/anonymous</c> with the project's <c>ProjectId</c>
    ///     header (and <c>UnityEnvironment</c> when one is named). UGS creates a player.
    ///   - Every later one: <c>POST /v1/authentication/session-token</c> with the session token the last
    ///     sign-in returned, so a profile stays the same UGS player instead of creating a new one per run.
    ///     The token is kept in <see cref="SessionTokenFile"/>, one per save profile. A token UGS refuses
    ///     (expired, deleted player, another project) falls back to a fresh anonymous sign-in.
    ///   - The access token is reused until a minute before it expires, then the player signs in again.
    /// Calls are serialized, so concurrent callers share one sign-in.
    /// </summary>
    public sealed class UgsAuthentication
    {
        public const string UgsBaseUrl = "https://player-auth.services.api.unity.com";
        /// <summary>How long before expiry a token is replaced.</summary>
        public static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

        readonly HttpClient _http;
        readonly SemaphoreSlim _gate = new(1, 1);
        string _accessToken;
        DateTime _expiresUtc;

        public string ProjectId { get; }
        public string Environment { get; }
        /// <summary>Where the session token is kept between runs; null keeps it in memory only.</summary>
        public string SessionTokenFile { get; }
        /// <summary>The UGS player id of the last sign-in; null before the first.</summary>
        public string PlayerId { get; private set; }
        /// <summary>How the last sign-in went: "anonymous" (a new player) or "session-token" (the same player again).</summary>
        public string LastSignIn { get; private set; }
        /// <summary>The clock token expiry is measured on (tests replace it).</summary>
        public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

        public UgsAuthentication(string projectId, string environment = null, string sessionTokenFile = null, string baseUrl = UgsBaseUrl, HttpClient http = null)
        {
            if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("a UGS project id is required", nameof(projectId));
            ProjectId = projectId.Trim();
            Environment = string.IsNullOrWhiteSpace(environment) ? null : environment.Trim();
            SessionTokenFile = sessionTokenFile;
            _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _http.BaseAddress = new Uri((baseUrl ?? UgsBaseUrl).TrimEnd('/') + "/");
        }

        /// <summary>A valid access token, signing in (or again) when there is none or it is about to expire.</summary>
        public async Task<string> GetAccessTokenAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (_accessToken == null || UtcNow() >= _expiresUtc - RefreshMargin) await SignInAsync();
                return _accessToken;
            }
            finally { _gate.Release(); }
        }

        async Task SignInAsync()
        {
            var stored = ReadSessionToken();
            if (stored != null)
            {
                try
                {
                    Apply(await Post("v1/authentication/session-token", new JsonObject { ["sessionToken"] = stored }), "session-token");
                    return;
                }
                catch (UgsServiceException e) when (e.Status is 400 or 401 or 403 or 404)
                {
                    // Expired, or a player this project no longer has: start over as a new player.
                }
            }
            Apply(await Post("v1/authentication/anonymous", new JsonObject()), "anonymous");
        }

        void Apply(JsonNode r, string how)
        {
            _accessToken = (string)r?["idToken"] ?? throw new FormatException("the sign-in returned no idToken");
            int expiresIn = (int?)r["expiresIn"] ?? 3600;
            _expiresUtc = UtcNow().AddSeconds(Math.Max(1, expiresIn));
            PlayerId = (string)r["userId"] ?? (string)r["user"]?["id"];
            LastSignIn = how;
            if ((string)r["sessionToken"] is { Length: > 0 } session) WriteSessionToken(session);
        }

        async Task<JsonNode> Post(string path, JsonObject body)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
            req.Headers.Add("ProjectId", ProjectId);
            if (Environment != null) req.Headers.Add("UnityEnvironment", Environment);
            using var resp = await _http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new UgsServiceException((int)resp.StatusCode, $"player-auth {path}: {(int)resp.StatusCode} {(text.Length > 300 ? text[..300] + "..." : text)}");
            return JsonNode.Parse(text);
        }

        string _memoryToken;

        string ReadSessionToken()
        {
            if (SessionTokenFile == null) return _memoryToken;
            try { return File.Exists(SessionTokenFile) && File.ReadAllText(SessionTokenFile).Trim() is { Length: > 0 } t ? t : null; }
            catch (IOException) { return null; }
        }

        void WriteSessionToken(string token)
        {
            _memoryToken = token;
            if (SessionTokenFile == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(SessionTokenFile))!);
                File.WriteAllText(SessionTokenFile, token);
            }
            catch (IOException) { } // the next run signs in as a new player; nothing else depends on it
        }

        /// <summary>
        /// The project id a Unity project is linked to (<c>cloudProjectId</c> in ProjectSettings.asset), or null.
        /// </summary>
        public static string ReadUnityProjectId(string projectRoot)
        {
            var f = Path.Combine(projectRoot ?? "", "ProjectSettings", "ProjectSettings.asset");
            if (!File.Exists(f)) return null;
            foreach (var line in File.ReadLines(f))
            {
                var t = line.Trim();
                if (t.StartsWith("cloudProjectId:", StringComparison.Ordinal))
                    return t["cloudProjectId:".Length..].Trim() is { Length: > 0 } id ? id : null;
            }
            return null;
        }
    }

    /// <summary>A UGS service refused a call; <see cref="Status"/> is the HTTP status.</summary>
    public sealed class UgsServiceException : Exception
    {
        public int Status { get; }
        public UgsServiceException(int status, string message) : base(message) => Status = status;
    }
}
