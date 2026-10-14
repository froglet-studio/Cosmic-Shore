using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CosmicShore.Tests;

/// <summary>
/// The multiplayer test tools are wired at boot and in the session service, where nothing fails when a
/// line goes missing: the relay, the UDP default, the simulator and the faults simply stop applying. That
/// happened on 2026-10-14 when a merge took one side of PlayerBoot and DirectoryMultiplayerService
/// whole (the online-server PR against Steps 1-7): every tool kept compiling and every unit test kept
/// passing, while COSMIC_SHORE_RELAY, COSMIC_SHORE_NET_SIM and COSMIC_SHORE_NET_FAULT went silent and the
/// transport contract lost six of its transports. These checks name the lines that must survive a merge
/// (docs/MULTIPLAYER.md §6).
/// </summary>
public class NetworkWiringGuardTests
{
    static string Source(string relative)
    {
        var f = Path.Combine(ContentYamlTests.ProjectRoot!, "Port", "src", relative);
        return Regex.Replace(File.ReadAllText(f), @"//[^\n]*|/\*.*?\*/", "", RegexOptions.Singleline);
    }

    [Fact]
    public void TheBoot_InstallsTheTransportRelaySimulatorAndFaults()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        var boot = Source(Path.Combine("CosmicShore.Player", "PlayerBoot.cs"));
        foreach (var call in new[] { "NetTransports.Select(", "UgsSetup.InstallRelay(", "NetSimulator.Install()", "NetFaults.Install()" })
            Assert.True(boot.Contains(call), $"PlayerBoot no longer calls {call}: its switch silently stops applying (docs/MULTIPLAYER.md §6)");
    }

    [Fact]
    public void TheSessionFolder_HostsAndJoinsThroughARelay()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        var svc = Source(Path.Combine("CosmicShore.Engine", "Networking", "Wire", "DirectoryMultiplayerService.cs"));
        foreach (var piece in new[] { "RelaySessions.Allocator", "RelaySessions.HostWith(", "RelaySessions.JoinWith(", "RelayJoinCode" })
            Assert.True(svc.Contains(piece), $"DirectoryMultiplayerService lost '{piece}': sessions no longer go through COSMIC_SHORE_RELAY (docs/MULTIPLAYER.md §6.7)");
    }

    [Fact]
    public void TheTransportContract_CoversEveryTransport_AndEachOnABadLine()
    {
        var names = NetTransportContractTests.TransportNames.ToHashSet();
        foreach (var t in new[] { "tcp", "udp", "relay", "online-relay", "loopback" })
        {
            Assert.Contains(t, names);
            Assert.Contains("sim-" + t, names);
        }
    }
}
