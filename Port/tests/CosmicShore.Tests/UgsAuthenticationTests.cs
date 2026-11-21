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
using System.Threading.Tasks;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A stand-in for UGS's Player Authentication endpoints (anonymous and session-token sign-in), in the
    /// documented request and response shape. It never talks to UGS: no player is created anywhere.
    /// </summary>
    sealed class UgsAuthStandIn : IDisposable
    {
        readonly HttpListener _http = new();
        readonly Thread _thread;
        readonly ConcurrentDictionary<string, string> _players = new(); // session token -> player id
        int _next;
        volatile bool _stopped;

        public string BaseUrl { get; }
        public string ProjectId = "00000000-1111-2222-3333-444444444444";
        public int ExpiresIn = 3600;
        public int AnonymousSignIns, SessionSignIns, Refused;
        public readonly ConcurrentQueue<(string path, string projectId, string environment)> Calls = new();

        public UgsAuthStandIn()
        {
            var l = new TcpListener(IPAddress.Loopback, 0); l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
            BaseUrl = $"http://127.0.0.1:{port}";
            _http.Prefixes.Add(BaseUrl + "/");
            _http.Start();
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Start();
        }

        /// <summary>Forgets every player, as if their session tokens had expired.</summary>
        public void ForgetPlayers() => _players.Clear();

        /// <summary>The id token issued to a player: "id-PLAYER-N".</summary>
        public static bool IsIdTokenOf(string token, string player) => token.StartsWith($"id-{player}-", StringComparison.Ordinal);

        void Loop()
        {
            while (!_stopped)
            {
                HttpListenerContext ctx;
                try { ctx = _http.GetContext(); } catch { if (_stopped) return; continue; }
                int status = 200;
                JsonNode body;
                var path = ctx.Request.Url!.AbsolutePath;
                var project = ctx.Request.Headers["ProjectId"];
                Calls.Enqueue((path, project, ctx.Request.Headers["UnityEnvironment"]));
                var text = new StreamReader(ctx.Request.InputStream, Encoding.UTF8).ReadToEnd();
                var req = text.Length > 0 ? JsonNode.Parse(text) : null;
                string player = null;
                if (project != ProjectId) { status = 400; Interlocked.Increment(ref Refused); }
                else if (path == "/v1/authentication/anonymous") { player = $"p{Interlocked.Increment(ref _next)}"; Interlocked.Increment(ref AnonymousSignIns); }
                else if (path == "/v1/authentication/session-token" && _players.TryGetValue((string)req?["sessionToken"] ?? "", out var known)) { player = known; Interlocked.Increment(ref SessionSignIns); }
                else { status = 401; Interlocked.Increment(ref Refused); }
                if (player != null)
                {
                    var session = "st-" + Guid.NewGuid().ToString("N");
                    _players[session] = player;
                    body = new JsonObject
                    {
                        ["expiresIn"] = ExpiresIn, ["idToken"] = $"id-{player}-{Guid.NewGuid():N}", ["sessionToken"] = session,
                        ["user"] = new JsonObject { ["id"] = player, ["disabled"] = false }, ["userId"] = player,
                    };
                }
                else body = new JsonObject { ["title"] = status == 400 ? "BAD_REQUEST" : "INVALID_SESSION_TOKEN", ["status"] = status };
                var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
                try { ctx.Response.StatusCode = status; ctx.Response.ContentType = "application/json"; ctx.Response.OutputStream.Write(bytes); ctx.Response.Close(); } catch { }
            }
        }

        public void Dispose() { _stopped = true; try { _http.Stop(); _http.Close(); } catch { } }
    }

    /// <summary><see cref="UgsAuthentication"/>: the sign-in that gives UGS Relay its bearer token (docs/MULTIPLAYER.md §6.8).</summary>
    public class UgsAuthenticationTests : IDisposable
    {
        readonly UgsAuthStandIn _ugs = new();
        readonly string _dir = Path.Combine(Path.GetTempPath(), "ugsauth-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            _ugs.Dispose();
            try { Directory.Delete(_dir, true); } catch { }
        }

        UgsAuthentication Auth(string tokenFile = null, string environment = null) =>
            new(_ugs.ProjectId, environment, tokenFile == null ? null : Path.Combine(_dir, tokenFile), _ugs.BaseUrl);

        [Fact]
        public async Task TheFirstSignIn_IsAnonymous_AndNamesTheProjectAndEnvironment()
        {
            var auth = Auth(environment: "staging");
            var token = await auth.GetAccessTokenAsync();
            Assert.Equal("anonymous", auth.LastSignIn);
            Assert.True(UgsAuthStandIn.IsIdTokenOf(token, auth.PlayerId), token);
            var call = Assert.Single(_ugs.Calls);
            Assert.Equal(("/v1/authentication/anonymous", _ugs.ProjectId, "staging"), call);
        }

        [Fact]
        public async Task NoEnvironment_SendsNoEnvironmentHeader()
        {
            await Auth().GetAccessTokenAsync();
            Assert.Null(Assert.Single(_ugs.Calls).environment);
        }

        [Fact]
        public async Task TheToken_IsReusedUntilAMinuteBeforeExpiry_ThenRenewedAsTheSamePlayer()
        {
            var now = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
            var auth = Auth();
            auth.UtcNow = () => now;
            var first = await auth.GetAccessTokenAsync();
            var player = auth.PlayerId;
            now = now.AddMinutes(58);
            Assert.Equal(first, await auth.GetAccessTokenAsync());
            Assert.Equal(1, _ugs.AnonymousSignIns + _ugs.SessionSignIns);
            now = now.AddSeconds(61); // inside the last minute of 3600 s
            var second = await auth.GetAccessTokenAsync();
            Assert.NotEqual(first, second);
            Assert.Equal("session-token", auth.LastSignIn);
            Assert.Equal(player, auth.PlayerId);
            Assert.Equal(1, _ugs.AnonymousSignIns);
        }

        [Fact]
        public async Task TheSessionToken_OutlivesTheProcess_SoAProfileStaysOnePlayer()
        {
            var a = Auth("profile.token");
            await a.GetAccessTokenAsync();
            var b = Auth("profile.token"); // the next run of the same profile
            await b.GetAccessTokenAsync();
            Assert.Equal("session-token", b.LastSignIn);
            Assert.Equal(a.PlayerId, b.PlayerId);
            Assert.Equal(1, _ugs.AnonymousSignIns);
            var other = Auth("other-profile.token");
            await other.GetAccessTokenAsync();
            Assert.NotEqual(a.PlayerId, other.PlayerId);
        }

        [Fact]
        public async Task ARefusedSessionToken_FallsBackToANewAnonymousPlayer()
        {
            var a = Auth("profile.token");
            await a.GetAccessTokenAsync();
            _ugs.ForgetPlayers();
            var b = Auth("profile.token");
            var token = await b.GetAccessTokenAsync();
            Assert.Equal("anonymous", b.LastSignIn);
            Assert.NotEqual(a.PlayerId, b.PlayerId);
            Assert.True(UgsAuthStandIn.IsIdTokenOf(token, b.PlayerId));
            Assert.Equal(1, _ugs.Refused);
        }

        [Fact]
        public async Task ConcurrentCallers_ShareOneSignIn()
        {
            var auth = Auth();
            var tokens = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(auth.GetAccessTokenAsync)));
            Assert.Single(tokens.Distinct());
            Assert.Equal(1, _ugs.AnonymousSignIns);
        }

        [Fact]
        public async Task AWrongProject_IsAServiceErrorWithItsStatus()
        {
            var auth = new UgsAuthentication("not-the-project", null, null, _ugs.BaseUrl);
            var e = await Assert.ThrowsAsync<UgsServiceException>(auth.GetAccessTokenAsync);
            Assert.Equal(400, e.Status);
        }

        [Fact]
        public void TheProjectId_IsReadFromUnitysProjectSettings()
        {
            var root = Path.Combine(_dir, "proj");
            Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
            File.WriteAllText(Path.Combine(root, "ProjectSettings", "ProjectSettings.asset"),
                "PlayerSettings:\n  productName: Cosmic Shore\n  cloudProjectId: 3030fd69-28ab-433f-b4bd-22b9b93c5118\n  projectName: Cosmic Shore\n");
            Assert.Equal("3030fd69-28ab-433f-b4bd-22b9b93c5118", UgsAuthentication.ReadUnityProjectId(root));
            Assert.Null(UgsAuthentication.ReadUnityProjectId(Path.Combine(_dir, "nowhere")));
            Assert.Throws<ArgumentException>(() => new UgsAuthentication(" ", null, null, _ugs.BaseUrl));
        }

        // ── the chain: sign-in -> bearer -> relay allocation ────────

        [Fact]
        public async Task TheRelayClient_SendsTheSignedInPlayersToken()
        {
            var auth = Auth();
            var token = await auth.GetAccessTokenAsync();
            using var relay = FrogletRelayServer.Start();
            relay.Secret = token; // the relay accepts exactly this player's token
            var signedIn = new RelayAllocationClient(relay.BaseUrl, auth.GetAccessTokenAsync);
            var host = await signedIn.AllocateAsync(1);
            Assert.NotEmpty(host.AllocationId);
            var anonymous = new RelayAllocationClient(relay.BaseUrl);
            Assert.Equal(401, (await Assert.ThrowsAsync<RelayServiceException>(() => anonymous.AllocateAsync(1))).Status);
            var wrong = new RelayAllocationClient(relay.BaseUrl, () => Task.FromResult("id-someone-else"));
            Assert.Equal(401, (await Assert.ThrowsAsync<RelayServiceException>(() => wrong.CreateJoinCodeAsync(host))).Status);
        }

        [Fact]
        public async Task ARelayServerWithASecret_StillAnswersHealthWithoutOne()
        {
            using var relay = FrogletRelayServer.Start();
            relay.Secret = "s3cret";
            using var http = new System.Net.Http.HttpClient();
            var health = JsonNode.Parse(await http.GetStringAsync(relay.BaseUrl + "/health"));
            Assert.True((bool)health!["ok"]);
            var withSecret = new RelayAllocationClient(relay.BaseUrl, () => Task.FromResult("s3cret"));
            var a = await withSecret.AllocateAsync(2);
            Assert.Matches("^[6789BCDFGHJKLMNPQRTW]{6}$", await withSecret.CreateJoinCodeAsync(a));
        }

        // ── RelayCheck: the owner's one-command test, against our relay ──

        [Fact]
        public async Task TheRelayCheck_PassesThroughARelay_AndTimesRoundTrips()
        {
            using var relay = FrogletRelayServer.Start();
            var log = new StringWriter();
            var r = await RelayCheck.RunAsync(new RelayAllocationClient(relay.BaseUrl), new RelayAllocationClient(relay.BaseUrl), log, pings: 5);
            Assert.True(r.Passed, log.ToString());
            Assert.Equal(5, r.Pings);
            Assert.True(r.RttMinMs > 0 && r.RttMinMs <= r.RttAverageMs && r.RttAverageMs <= r.RttMaxMs);
            Assert.Matches("^[6789BCDFGHJKLMNPQRTW]{6}$", r.JoinCode);
            Assert.Contains("[relay-check] PASS", log.ToString());
            Assert.True(relay.Forwarded >= 10);
        }

        [Fact]
        public async Task TheRelayCheck_NamesTheStepThatFailed()
        {
            using var a = FrogletRelayServer.Start();
            using var b = FrogletRelayServer.Start(); // the joiner asks a relay that never issued the code
            var log = new StringWriter();
            var r = await RelayCheck.RunAsync(new RelayAllocationClient(a.BaseUrl), new RelayAllocationClient(b.BaseUrl), log, pings: 1);
            Assert.False(r.Passed);
            Assert.Equal("join", r.FailedStep);
            Assert.Contains("FAILED at 'join'", log.ToString());
            Assert.EndsWith("[relay-check] FAIL", log.ToString().TrimEnd());
        }
    }
}
