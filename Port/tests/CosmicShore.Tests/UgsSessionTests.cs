using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Online;

namespace CosmicShore.Tests
{
    /// <summary>
    /// UGS sign-in reuse: the first run signs in anonymously and caches the session token in the instance's
    /// data folder; later runs sign the SAME player back in with it, so Prisma stops minting a new anonymous
    /// player per run. Driven through the real <see cref="UgsClient"/> over a fake HTTP handler.
    /// </summary>
    public class UgsSessionTests : IDisposable
    {
        readonly string dir = Path.Combine(Path.GetTempPath(), "ugs-session-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose() { try { Directory.Delete(dir, true); } catch { } }

        /// <summary>The player-auth service: mints players, accepts session tokens it issued (until revoked).</summary>
        sealed class FakeAuth : HttpMessageHandler
        {
            public readonly List<string> Calls = new();
            public readonly HashSet<string> Revoked = new();
            public int ExpiresIn = 3599;
            public bool RotateTokens = true;
            readonly Dictionary<string, string> _tokenToPlayer = new();
            int _players, _tokens;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            {
                string path = req.RequestUri.AbsolutePath;
                Calls.Add(path);
                Assert.Equal("proj-1", req.Headers.GetValues("ProjectId").Single());
                Assert.Equal("env-1", req.Headers.GetValues("UnityEnvironment").Single());
                var body = JsonNode.Parse(await req.Content.ReadAsStringAsync(ct));
                string player;
                if (path.EndsWith("/anonymous")) player = $"player-{++_players}";
                else if (path.EndsWith("/session-token"))
                {
                    var token = (string)body["sessionToken"];
                    if (Revoked.Contains(token) || !_tokenToPlayer.TryGetValue(token, out player))
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                        { Content = new StringContent("{\"title\":\"Unauthorized\",\"detail\":\"invalid session token\",\"code\":0}") };
                    if (!RotateTokens)
                        return Ok(player, null);
                }
                else return new HttpResponseMessage(HttpStatusCode.NotFound);
                var issued = $"session-{++_tokens}";
                _tokenToPlayer[issued] = player;
                return Ok(player, issued);
            }

            HttpResponseMessage Ok(string player, string sessionToken)
            {
                var j = new JsonObject { ["userId"] = player, ["idToken"] = "id-" + Guid.NewGuid(), ["expiresIn"] = ExpiresIn };
                if (sessionToken != null) j["sessionToken"] = sessionToken;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(j.ToJsonString(), Encoding.UTF8, "application/json") };
            }
        }

        UgsSession NewRun(FakeAuth auth, string env = "env-1") => new(new UgsClient("proj-1", env, auth), "proj-1", env, dir);

        [Fact]
        public async Task TheFirstRunSignsInAnonymously_AndLaterRunsReuseThatPlayer()
        {
            var auth = new FakeAuth();
            var first = await NewRun(auth).GetPlayerAsync();
            Assert.Equal("player-1", first.PlayerId);
            Assert.True(File.Exists(Path.Combine(dir, UgsSession.CacheFileName)));

            for (int run = 0; run < 3; run++)
            {
                var session = NewRun(auth);           // a new process
                var p = await session.GetPlayerAsync();
                Assert.Equal("player-1", p.PlayerId);   // the same player every time
                Assert.Equal("cached", session.LastSignIn);
            }
            Assert.Equal(1, auth.Calls.Count(c => c.EndsWith("/anonymous")));
            Assert.Equal(3, auth.Calls.Count(c => c.EndsWith("/session-token")));
        }

        [Fact]
        public async Task ARevokedToken_FallsBackToANewAnonymousPlayer_AndCachesIt()
        {
            var auth = new FakeAuth();
            await NewRun(auth).GetPlayerAsync();
            foreach (var t in Enumerable.Range(1, 10)) auth.Revoked.Add($"session-{t}");
            var session = NewRun(auth);
            var p = await session.GetPlayerAsync();
            Assert.Equal("player-2", p.PlayerId);
            Assert.Equal("anonymous", session.LastSignIn);
            auth.Revoked.Clear();
            Assert.Equal("player-2", (await NewRun(auth).GetPlayerAsync()).PlayerId);
        }

        [Fact]
        public async Task TheCacheBelongsToOneProjectAndEnvironment()
        {
            var auth = new FakeAuth();
            await NewRun(auth).GetPlayerAsync();
            var other = await new UgsSession(new UgsClient("proj-1", "env-1", auth), "proj-1", "production", dir).GetPlayerAsync();
            Assert.Equal("player-2", other.PlayerId); // a different environment's cache entry is not reused
        }

        [Fact]
        public async Task AnExpiringIdToken_IsRenewedWithTheSessionToken_ForTheSamePlayer()
        {
            var auth = new FakeAuth { ExpiresIn = 60 }; // inside the 5-minute renewal margin
            var session = NewRun(auth);
            var a = await session.GetPlayerAsync();
            var b = await session.GetPlayerAsync();
            Assert.Equal(a.PlayerId, b.PlayerId);
            Assert.NotEqual(a.IdToken, b.IdToken);
            Assert.Equal(new[] { "/v1/authentication/anonymous", "/v1/authentication/session-token" }, auth.Calls);
        }

        [Fact]
        public async Task AFreshIdToken_IsReusedInProcess()
        {
            var auth = new FakeAuth();
            var session = NewRun(auth);
            var a = await session.GetPlayerAsync();
            var b = await session.GetPlayerAsync();
            Assert.Same(a, b);
            Assert.Single(auth.Calls);
        }

        [Fact]
        public async Task ARefreshWithoutANewSessionToken_KeepsTheOldOne()
        {
            var auth = new FakeAuth();
            await NewRun(auth).GetPlayerAsync();
            auth.RotateTokens = false;
            var p = await NewRun(auth).GetPlayerAsync();
            Assert.Equal("session-1", p.SessionToken);
            Assert.Equal("player-1", (await NewRun(auth).GetPlayerAsync()).PlayerId);
        }

        [Fact]
        public async Task TheCacheFile_HoldsNoIdToken_AndIsOwnerOnlyOnUnix()
        {
            var auth = new FakeAuth();
            var p = await NewRun(auth).GetPlayerAsync();
            var path = Path.Combine(dir, UgsSession.CacheFileName);
            var text = File.ReadAllText(path);
            Assert.DoesNotContain(p.IdToken, text);
            Assert.Contains("player-1", text);
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }

        [Fact]
        public void AllocationJson_ParsesLikeTheLiveService()
        {
            var json = JsonNode.Parse(@"{
                ""allocationId"": ""96313be5-f5af-4ed3-b127-d0bf3c095742"",
                ""allocationIdBytes"": ""ljE75fWvTtOxJ9C/PAlXQg=="",
                ""connectionData"": """ + Convert.ToBase64String(new byte[255]) + @""",
                ""key"": """ + Convert.ToBase64String(new byte[64]) + @""",
                ""hostConnectionData"": """ + Convert.ToBase64String(new byte[50]) + @""",
                ""region"": ""asia-south1"",
                ""serverEndpoints"": [
                    { ""connectionType"": ""udp"", ""host"": ""1.2.3.4"", ""port"": 37000, ""secure"": false },
                    { ""connectionType"": ""dtls"", ""host"": ""1.2.3.4"", ""port"": 37001, ""secure"": true } ]
            }");
            var a = UgsClient.ParseAllocation(json);
            Assert.Equal(Guid.Parse("96313be5-f5af-4ed3-b127-d0bf3c095742"), new Guid(a.AllocationIdBytes, bigEndian: true));
            Assert.Equal(255, a.ConnectionData.Length);
            Assert.Equal(50, a.HostConnectionData.Length);
            Assert.Equal(37001, a.Endpoint("dtls").Port);
            Assert.Equal("asia-south1", a.Region);
        }
    }

    public class RelayRegionTests : IDisposable
    {
        readonly Func<string, Task<double>> savedMeasure = RelayRegions.Measure, savedFallback = RelayRegions.Fallback;
        public void Dispose() { RelayRegions.Measure = savedMeasure; RelayRegions.Fallback = savedFallback; }

        sealed class QosOnly : IUgsApi
        {
            public IReadOnlyList<QosServer> Servers;
            public bool Fail;
            public Task<IReadOnlyList<QosServer>> ListQosServersAsync(UgsPlayer p, CancellationToken ct = default)
                => Fail ? throw new UgsException(503, "/v1/services/relay/servers", "down") : Task.FromResult(Servers);
            public Task<UgsPlayer> SignInAnonymouslyAsync(CancellationToken ct = default) => throw new NotSupportedException();
            public Task<UgsPlayer> SignInWithSessionTokenAsync(string t, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<string>> ListRegionsAsync(UgsPlayer p, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<RelayAllocation> AllocateAsync(UgsPlayer p, int m, string r, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<string> CreateJoinCodeAsync(UgsPlayer p, Guid a, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<RelayAllocation> JoinAsync(UgsPlayer p, string c, CancellationToken ct = default) => throw new NotSupportedException();
        }

        static readonly UgsPlayer Player = new("p", "id", "s", DateTimeOffset.UtcNow.AddHours(1));
        static readonly QosServer[] Servers =
        {
            new("us-east1", new[] { "10.0.0.1:7778" }),
            new("asia-south1", new[] { "[2001:db8::2]:7778", "10.0.0.2:7778" }),
            new("europe-west4", new[] { "10.0.0.3:7778" }),
        };

        [Fact]
        public async Task PicksTheRegionWithTheLowestRoundTrip_MeasuringTheIpv4Endpoint()
        {
            var measured = new List<string>();
            RelayRegions.Measure = ep => { lock (measured) measured.Add(ep); return Task.FromResult(ep switch { "10.0.0.2:7778" => 31.0, "10.0.0.1:7778" => 240.0, _ => -1 }); };
            var (region, list) = await RelayRegions.PickAsync(new QosOnly { Servers = Servers }, Player);
            Assert.Equal("asia-south1", region);
            Assert.Equal(new[] { "asia-south1", "us-east1" }, list.Select(m => m.Region));
            Assert.Contains("10.0.0.2:7778", measured);
        }

        [Fact]
        public async Task WhenNoQosServerAnswers_FallsBackToPing_ThenToRelaysOwnChoice()
        {
            RelayRegions.Measure = _ => Task.FromResult(-1.0);
            RelayRegions.Fallback = ep => Task.FromResult(ep == "10.0.0.3:7778" ? 90.0 : -1);
            Assert.Equal("europe-west4", (await RelayRegions.PickAsync(new QosOnly { Servers = Servers }, Player)).region);
            RelayRegions.Fallback = _ => Task.FromResult(-1.0);
            Assert.Null((await RelayRegions.PickAsync(new QosOnly { Servers = Servers }, Player)).region);
        }

        [Fact]
        public async Task ADiscoveryFailure_LeavesTheRegionToRelay()
            => Assert.Null((await RelayRegions.PickAsync(new QosOnly { Fail = true }, Player)).region);
    }
}
