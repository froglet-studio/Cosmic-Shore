using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Party-services ring 2 — LobbyRefreshScheduler cadence/boost, InviteService
// per-player invite-slot payloads, LobbyPropertyWriter mutex→refresh→set→save
// flow, and AcceptanceSignalService's lobby scan + publish, all live against
// the engine session placeholders (zero deviations in the ring).
// ─────────────────────────────────────────────────────────────────────────────

sealed class RingStubPlayer : IReadOnlyPlayer
{
    public string Id { get; init; }
    public Dictionary<string, PlayerProperty> Props { get; } = new();
    public IReadOnlyDictionary<string, PlayerProperty> Properties => Props;
}

sealed class RingStubCurrentPlayer : CosmicShore.Engine.Networking.IPlayer
{
    public string Id { get; init; } = "local";
    public Dictionary<string, PlayerProperty> Props { get; } = new();
    public IReadOnlyDictionary<string, PlayerProperty> Properties => Props;
    public void SetProperty(string key, PlayerProperty property) => Props[key] = property;
}

sealed class RingStubLobby : IHostSession
{
    public string Id => "lobby";
    public string Code => "CODE";
    public bool IsHost => true;
    public int MaxPlayers => 100;
    public int PlayerCount => Roster.Count;
    public event Action Deleted { add { } remove { } }
    public event Action<string> PlayerLeaving { add { } remove { } }
    public List<IReadOnlyPlayer> Roster { get; } = new();
    public IReadOnlyList<IReadOnlyPlayer> Players => Roster;
    public RingStubCurrentPlayer Local { get; } = new();
    public CosmicShore.Engine.Networking.IPlayer CurrentPlayer => Local;
    public int Refreshes; public int Saves;
    public System.Threading.Tasks.Task RefreshAsync() { Refreshes++; return System.Threading.Tasks.Task.CompletedTask; }
    public System.Threading.Tasks.Task SaveCurrentPlayerDataAsync() { Saves++; return System.Threading.Tasks.Task.CompletedTask; }
    public System.Threading.Tasks.Task LeaveAsync() { Left++; return System.Threading.Tasks.Task.CompletedTask; }
    public int Left; public int Deleted2;
    public IHostSession AsHost() => this;
    public System.Threading.Tasks.Task DeleteAsync() { Deleted2++; return System.Threading.Tasks.Task.CompletedTask; }
    public System.Threading.Tasks.Task RemovePlayerAsync(string playerId) => System.Threading.Tasks.Task.CompletedTask;
}

public class LobbyRefreshSchedulerTests : IDisposable
{
    readonly GameLoop loop = new(nameof(LobbyRefreshSchedulerTests));
    public void Dispose() => loop.Dispose();

    [Fact]
    public void FiresOncePerInterval_AndResets()
    {
        var s = new LobbyRefreshScheduler(defaultIntervalSeconds: 3f);
        Assert.False(s.ShouldFireNow(1f));
        Assert.False(s.ShouldFireNow(1f));
        Assert.True(s.ShouldFireNow(1.1f));   // 3.1s accumulated → fire + auto-reset
        Assert.False(s.ShouldFireNow(1f));    // accumulator restarted

        s.Reset();
        Assert.False(s.ShouldFireNow(2.9f));
        Assert.True(s.ShouldFireNow(0.2f));
    }

    [Fact]
    public void Boost_TightensTheInterval_ForTheBoostWindow()
    {
        var s = new LobbyRefreshScheduler(defaultIntervalSeconds: 3f);
        loop.Tick(1f / 60f); // give Time.unscaledTime a defined base
        s.Boost();
        Assert.True(s.IsBoosted);
        Assert.True(s.ShouldFireNow(LobbyRefreshScheduler.BOOSTED_INTERVAL_SECONDS + 0.01f));

        // Advance unscaled time past the boost window — cadence returns to default.
        float t = 0f;
        while (t < LobbyRefreshScheduler.BOOST_WINDOW_SECONDS + 0.5f) { loop.Tick(0.25f); t += 0.25f; }
        Assert.False(s.IsBoosted);
        Assert.False(s.ShouldFireNow(1f));
    }

    [Fact]
    public void ResetDeferred_PushesTheNextFireOut()
    {
        var s = new LobbyRefreshScheduler(defaultIntervalSeconds: 1f);
        s.ResetDeferred(2f);                 // timer = -2 → needs 3s total
        Assert.False(s.ShouldFireNow(2.5f));
        Assert.True(s.ShouldFireNow(0.6f));
    }
}

public class InviteServiceTests : IDisposable
{
    readonly GameLoop loop = new(nameof(InviteServiceTests));
    public void Dispose() => loop.Dispose();

    [Fact]
    public void AddSerialize_UsesThePipeDelimitedPerLineFormat()
    {
        var inv = new InviteService();
        inv.AddOrRefresh("target-1", "PENDING", "me", "Ace", 7, expiresAtUnscaledTime: 999f);
        inv.AddOrRefresh("target-2", "sess-42", "me", "Ace", 7, expiresAtUnscaledTime: 999f);

        Assert.Equal(2, inv.OutgoingCount);
        Assert.True(inv.Contains("target-1"));
        var lines = inv.SerializeAll().Split('\n');
        Assert.Contains("target-1|me|PENDING|Ace|7", lines);
        Assert.Contains("target-2|me|sess-42|Ace|7", lines);
    }

    [Fact]
    public void UpdatePayloadsWithRealSessionId_ReplacesPendingOnly()
    {
        var inv = new InviteService();
        inv.AddOrRefresh("t1", "PENDING", "me", "Ace", 7, 999f);
        inv.AddOrRefresh("t2", "already-real", "me", "Ace", 7, 999f);

        inv.UpdatePayloadsWithRealSessionId("real-99");

        var payload = inv.SerializeAll();
        Assert.Contains("t1|me|real-99|Ace|7", payload);
        Assert.Contains("t2|me|already-real|Ace|7", payload);
    }

    [Fact]
    public void RemoveExpired_DropsOnlyPastDeadlines()
    {
        var inv = new InviteService();
        loop.Tick(1f / 60f); // Time.unscaledTime > 0
        float now = Time.unscaledTime;
        inv.AddOrRefresh("stale", "PENDING", "me", "Ace", 7, expiresAtUnscaledTime: now - 0.001f);
        inv.AddOrRefresh("fresh", "PENDING", "me", "Ace", 7, expiresAtUnscaledTime: now + 60f);

        var expired = inv.RemoveExpired();

        Assert.Equal(new[] { "stale" }, expired);
        Assert.False(inv.Contains("stale"));
        Assert.True(inv.Contains("fresh"));
        Assert.Equal(1, inv.OutgoingCount);
    }
}

public class AcceptanceSignalServiceTests
{
    [Fact]
    public void ScanForSignals_ReturnsTheAccepter_OnlyForOurInvites()
    {
        var svc = new AcceptanceSignalService();
        var lobby = new RingStubLobby();

        var invitedButAcceptedSomeoneElse = new RingStubPlayer { Id = "p-other" };
        invitedButAcceptedSomeoneElse.Props["accepted_invite"] = new PlayerProperty("not-us");
        var notInvited = new RingStubPlayer { Id = "p-random" };
        notInvited.Props["accepted_invite"] = new PlayerProperty("local");
        var accepter = new RingStubPlayer { Id = "p-guest" };
        accepter.Props["accepted_invite"] = new PlayerProperty("local");

        lobby.Roster.Add(invitedButAcceptedSomeoneElse);
        lobby.Roster.Add(notInvited);
        lobby.Roster.Add(accepter);

        var result = svc.ScanForSignals(lobby, "local", new[] { "p-other", "p-guest" });
        Assert.Equal("p-guest", result);

        // No matching signal → null.
        Assert.Null(svc.ScanForSignals(lobby, "someone-else-entirely", new[] { "p-none" }));
    }

    [Fact]
    public async System.Threading.Tasks.Task PublishSignalAsync_WritesAcceptedInvite_ThroughTheWriterMutex()
    {
        var svc = new AcceptanceSignalService();
        var lobby = new RingStubLobby();
        var writer = new LobbyPropertyWriter();

        await svc.PublishSignalAsync(lobby, hostPlayerId: "host-7", writer);

        Assert.True(lobby.Local.Props.TryGetValue("accepted_invite", out var prop));
        Assert.Equal("host-7", prop.Value);
        Assert.True(lobby.Refreshes >= 1);  // mutex → refresh → set → save
        Assert.True(lobby.Saves >= 1);
    }
}
