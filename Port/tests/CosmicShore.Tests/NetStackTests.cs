using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// How a player's networking is put together (<see cref="NetStack"/>), and that the player does it. The merge of
    /// the online server into Ys-bleeding-edge (61da694263) dropped the transport choice, the relay, the network
    /// simulator and session faults from PlayerBoot while every engine test stayed green: each layer was tested
    /// alone, nothing tested the composition. These do; UgsMultiplayerTests plays a match through the whole stack.
    /// </summary>
    [Collection(NetDriverStaticsCollection.Name)]
    public class NetStackTests : IDisposable
    {
        UgsMultiplayerService _ugs;

        public NetStackTests()
        {
            NetworkManager.Singleton = null;
            RelaySessions.Reset();
            NetDriver.TransportFactory = new TcpTransportFactory();
        }

        public void Dispose()
        {
            _ugs?.Dispose();
            NetDriver.TransportFactory = new TcpTransportFactory();
            RelaySessions.Reset();
            MultiplayerService.Instance = null;
            NetworkManager.Singleton = null;
        }

        void InstallUgs() => MultiplayerService.Instance = _ugs = new UgsMultiplayerService(
            new UgsLobbyClient(() => Task.FromResult("id-alice-1"), "http://127.0.0.1:9/v1"), () => "alice");

        [Fact]
        public void TheOrder_TransportThenRelayThenSessions_ThenSimulatorAndFaultsAroundThem()
        {
            NetStack.Install("udp", () => RelaySessions.Install(new RelayAllocationClient("http://127.0.0.1:9")), InstallUgs);
            Assert.Equal("simulated > relay > udp", NetStack.Describe());
            Assert.Equal("faults > ugs", NetStack.DescribeSessions());
        }

        [Fact]
        public void TheTransportChoice_IsKept_AndInstallingTwiceDoesNotStackLayers()
        {
            NetStack.Install("tcp", null, InstallUgs);
            Assert.Equal("simulated > tcp", NetStack.Describe());
            Assert.Equal("faults > ugs", NetStack.DescribeSessions());
            _ugs.Dispose();
            NetStack.Install("", null, InstallUgs);
            Assert.Equal("simulated > udp", NetStack.Describe()); // UDP is the default
            Assert.Equal("faults > ugs", NetStack.DescribeSessions());
        }

        [Fact]
        public void ThePlayer_GoesOnlineThroughUgs_AndPutsItsNetworkingTogetherThroughNetStack()
        {
            var player = Path.Combine(PortRoot(), "src", "CosmicShore.Player");
            var boot = File.ReadAllText(Path.Combine(player, "PlayerBoot.cs"));
            var ugs = File.ReadAllText(Path.Combine(player, "UgsSetup.cs"));
            Assert.Contains("UgsSetup.InstallOnline(", boot);
            Assert.Contains("NetStack.Install(", ugs);
            Assert.Contains("COSMIC_SHORE_NET_TRANSPORT", ugs);
            Assert.Contains("RelaySessions.Install(", ugs);
            Assert.Contains("new UgsMultiplayerService(", ugs);
            // Nothing else in the player may pick the transport or the simulator/faults on its own: that is how they were lost.
            var stray = Regex.Matches(boot + ugs, @"NetTransports\.Select|NetSimulator\.Install|NetFaults\.Install|TransportFactory\s*=").Select(m => m.Value).ToList();
            Assert.True(stray.Count == 0, "the player sets networking layers outside NetStack: " + string.Join(", ", stray));
        }

        static string PortRoot()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (Directory.Exists(Path.Combine(d.FullName, "src", "CosmicShore.Player"))) return d.FullName;
            throw new DirectoryNotFoundException("Port/ not found above " + AppContext.BaseDirectory);
        }
    }
}
