using System;
using System.Linq;
using System.Threading;
using CosmicShore.Engine.Networking;
using CosmicShore.Tests;

// The relay and Lobby stand-ins for run.sh. Prints the two URLs the pilots read
// (COSMIC_SHORE_UGS_RELAY_URL, COSMIC_SHORE_UGS_LOBBY_URL), then a counter line every 30 s and at exit.
static class PartyStandIns
{
    static int Main()
    {
        using var relay = FrogletRelayServer.Start(0, 0);
        using var lobby = new UgsLobbyStandIn();
        Console.WriteLine($"[standins] COSMIC_SHORE_UGS_RELAY_URL={relay.BaseUrl}");
        Console.WriteLine($"[standins] COSMIC_SHORE_UGS_LOBBY_URL={lobby.BaseUrl}");
        var stop = new ManualResetEventSlim();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Set();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        void Report() => Console.WriteLine(
            $"[standins] relay: {relay.AllocationCount} allocations, {relay.BindsAccepted} binds accepted, {relay.BindsRejected} rejected, " +
            $"{relay.Forwarded} forwarded, {relay.Refused} refused; lobby: {lobby.Calls.Count} calls, " +
            $"{lobby.Heartbeats.Values.Sum()} heartbeats ({lobby.HeartbeatsRefused} refused)");
        while (!stop.Wait(TimeSpan.FromSeconds(30))) Report();
        Report();
        return 0;
    }
}
