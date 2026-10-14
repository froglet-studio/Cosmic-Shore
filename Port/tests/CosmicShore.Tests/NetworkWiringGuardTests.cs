using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CosmicShore.Tests;

/// <summary>
/// The multiplayer stack is wired at boot, where nothing fails when a line goes missing: the relay, the UDP
/// default, the simulator and the faults simply stop applying. That happened on 2026-10-14 when a merge took
/// one side of PlayerBoot whole: every tool kept compiling and every unit test kept passing. The online
/// server and the session folder have since been removed (START ONLINE is UGS Lobby + Relay), and the stack
/// is built in one place, <c>NetStack.Install</c> (order held by <c>NetStackTests</c>). These checks name the
/// calls that must survive a merge (docs/MULTIPLAYER.md §6.9).
/// </summary>
public class NetworkWiringGuardTests
{
    static string Source(string relative)
    {
        var f = Path.Combine(ContentYamlTests.ProjectRoot!, "Port", "src", relative);
        return Regex.Replace(File.ReadAllText(f), @"//[^\n]*|/\*.*?\*/", "", RegexOptions.Singleline);
    }

    [Fact]
    public void TheBoot_BuildsTheNetworkThroughNetStack()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        Assert.True(Source(Path.Combine("CosmicShore.Player", "PlayerBoot.cs")).Contains("UgsSetup.InstallOnline("),
            "PlayerBoot no longer calls UgsSetup.InstallOnline: START ONLINE silently plays offline");
        Assert.True(Source(Path.Combine("CosmicShore.Player", "UgsSetup.cs")).Contains("NetStack.Install("),
            "UgsSetup no longer builds the stack through NetStack.Install: the transport, relay, simulator and faults stop applying");
        var stack = Source(Path.Combine("CosmicShore.Engine", "Networking", "Wire", "NetStack.cs"));
        foreach (var call in new[] { "NetTransports.Select(", "NetSimulator.Install()", "NetFaults.Install()" })
            Assert.True(stack.Contains(call), $"NetStack no longer calls {call}: its switch silently stops applying (docs/MULTIPLAYER.md §6)");
    }

    [Fact]
    public void TheTransportContract_CoversEveryTransport_AndEachOnABadLine()
    {
        var names = NetTransportContractTests.TransportNames.ToHashSet();
        foreach (var t in new[] { "tcp", "udp", "relay", "loopback" })
        {
            Assert.Contains(t, names);
            Assert.Contains("sim-" + t, names);
        }
    }
}
