using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Online;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The session layer over a relay backend (docs/RELAY.md): a Relay-networked session's record carries the
    /// host's join code instead of an address, a join resolves the code before it connects, an unreachable
    /// Relay falls back to the LAN, and choosing the "relay" transport installs the Unity Relay backend.
    /// </summary>
    [Collection(NetDriverStaticsCollection.Name)]
    public class RelaySessionTests : IDisposable
    {
        sealed class FakeRelay : INetRelayBackend
        {
            public readonly List<int> Hosts = new();
            public readonly List<string> Joins = new();
            public Exception Fail;
            public Task<string> PrepareHostAsync(int maxConnections, string region = null)
            {
                Hosts.Add(maxConnections);
                return Fail != null ? Task.FromException<string>(Fail) : Task.FromResult("RLY123");
            }
            public Task PrepareJoinAsync(string joinCode)
            {
                Joins.Add(joinCode);
                return Fail != null ? Task.FromException(Fail) : Task.CompletedTask;
            }
            public string Status => "fake";
        }

        readonly string dir = Path.Combine(Path.GetTempPath(), "relay-session-tests-" + Guid.NewGuid().ToString("N"));
        readonly GameLoop loop = new(nameof(RelaySessionTests));
        readonly LoopbackTransportFactory transports = new();
        readonly FakeRelay relay = new();
        readonly NetworkManager nm;
        readonly bool savedEmulate = NetworkManager.EmulateNetcodeLifecycle, savedEnabled = NetDriver.Enabled;

        public RelaySessionTests()
        {
            NetworkManager.EmulateNetcodeLifecycle = true;
            NetDriver.Enabled = true;
            NetDriver.TransportFactory = transports;
            NetRelay.Backend = relay;
            nm = new GameObject("nm").AddComponent<NetworkManager>();
            nm.SetSingleton();
        }

        public void Dispose()
        {
            nm.Shutdown();
            NetDriver.Stop();
            NetDriver.TransportFactory = new TcpTransportFactory();
            NetRelay.Backend = null;
            NetRelay.InUse = false;
            NetRelay.JoinCode = null;
            NetworkManager.EmulateNetcodeLifecycle = savedEmulate;
            NetDriver.Enabled = savedEnabled;
            NetworkManager.Singleton = null;
            loop.Dispose();
            try { Directory.Delete(dir, true); } catch { }
        }

        JsonElement Record(string id) => JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, id + ".json"))).RootElement;

        [Fact]
        public async Task ARelaySession_RecordsTheJoinCode_NotAnAddress()
        {
            var svc = new DirectoryMultiplayerService(dir);
            var session = await svc.CreateSessionAsync(new SessionOptions { MaxPlayers = 4 }.WithRelayNetwork("asia-south1"));
            Assert.True(NetDriver.IsServer);
            Assert.Equal(new[] { 3 }, relay.Hosts); // four players = the host + three joiners
            var rec = Record(session.Id);
            Assert.Equal("RLY123", rec.GetProperty("RelayJoinCode").GetString());
            Assert.Equal(0, rec.GetProperty("RelayPort").GetInt32());
        }

        [Fact]
        public async Task AnUnreachableRelay_HostsOnTheLanInstead()
        {
            relay.Fail = new System.Net.Http.HttpRequestException("no route to host");
            var svc = new DirectoryMultiplayerService(dir);
            var session = await svc.CreateSessionAsync(new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork());
            var rec = Record(session.Id);
            Assert.Equal(JsonValueKind.Null, rec.GetProperty("RelayJoinCode").ValueKind);
            Assert.True(rec.GetProperty("RelayPort").GetInt32() > 0);
        }

        [Fact]
        public async Task JoiningARelaySession_ResolvesTheCode_ThenStartsTheClient()
        {
            var svc = new DirectoryMultiplayerService(dir);
            var session = await svc.CreateSessionAsync(new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork());
            nm.Shutdown(); // this process now plays the joiner
            var joined = await svc.JoinSessionByIdAsync(session.Id);
            Assert.Equal(new[] { "RLY123" }, relay.Joins);
            Assert.True(nm.IsListening && nm.IsClient && !nm.IsServer);
            Assert.True(NetDriver.IsClientOnly);
        }

        [Fact]
        public async Task JoiningARelaySession_WithoutTheRelayTransport_FailsWithAClearMessage()
        {
            var svc = new DirectoryMultiplayerService(dir);
            var session = await svc.CreateSessionAsync(new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork());
            nm.Shutdown();
            NetRelay.Backend = null;
            var e = await Assert.ThrowsAsync<SessionException>(() => svc.JoinSessionByIdAsync(session.Id));
            Assert.Contains("--relay", e.Message);
            Assert.False(nm.IsListening);
        }

        [Fact]
        public async Task WithoutABackend_SessionsStayOnTheLocalStandIn()
        {
            NetRelay.Backend = null;
            var svc = new DirectoryMultiplayerService(dir);
            var session = await svc.CreateSessionAsync(new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork());
            var rec = Record(session.Id);
            Assert.True(rec.GetProperty("RelayPort").GetInt32() > 0);
            Assert.Empty(relay.Hosts);
        }

        [Fact]
        public void SelectingRelay_InstallsTheUnityRelayBackend_WithoutTouchingTheNetwork()
        {
            OnlineBoot.Register(dir, new RelayOptions { Dtls = true });
            Assert.Contains("relay", NetTransports.Names);
            Assert.Equal("relay", NetTransports.Select("RELAY"));
            Assert.IsType<RelayTransportFactory>(NetDriver.TransportFactory);
            Assert.IsType<UnityRelayBackend>(NetRelay.Backend);
            Assert.True(OnlineBoot.Backend.Factory.Dtls);
            Assert.False(File.Exists(Path.Combine(dir, UgsSession.CacheFileName))); // no sign-in until a session needs one
            Assert.Equal("udp", NetTransports.Select("udp"));
        }

        [Fact]
        public void RelayOptions_ComeFromTheEnvironment_WithTheGamesProjectByDefault()
        {
            var names = new[] { "COSMIC_SHORE_UGS_PROJECT", "COSMIC_SHORE_UGS_ENVIRONMENT", "COSMIC_SHORE_RELAY_REGION", "COSMIC_SHORE_RELAY_DTLS" };
            var saved = names.ToDictionary(n => n, Environment.GetEnvironmentVariable);
            try
            {
                foreach (var n in names) Environment.SetEnvironmentVariable(n, null);
                var d = RelayOptions.FromEnvironment();
                Assert.Equal("3030fd69-28ab-433f-b4bd-22b9b93c5118", d.ProjectId);
                Assert.Equal("development", d.Environment);
                Assert.True(d.Dtls);
                Assert.Null(d.Region);
                Environment.SetEnvironmentVariable("COSMIC_SHORE_RELAY_DTLS", "0");
                Environment.SetEnvironmentVariable("COSMIC_SHORE_RELAY_REGION", "asia-south1");
                var o = RelayOptions.FromEnvironment();
                Assert.False(o.Dtls);
                Assert.Equal("asia-south1", o.Region);
            }
            finally { foreach (var kv in saved) Environment.SetEnvironmentVariable(kv.Key, kv.Value); }
        }

        [Fact]
        public void TheWindowTitle_ShowsTheJoinCodeWhileHosting()
        {
            NetDriver.StartServer(nm, "0.0.0.0", 0);
            NetRelay.InUse = true;
            NetRelay.JoinCode = "RLY123";
            NetRelay.Region = "asia-south1";
            Assert.Contains("Relay RLY123 (asia-south1)", NetStats.Title("A"));
        }
    }
}
