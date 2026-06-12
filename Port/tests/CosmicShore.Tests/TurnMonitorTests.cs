using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// Rung-4 groundwork: TurnMonitor (base) + CrystalCollisionTurnMonitor ported.
// The base runs an async RunLoopAsync — every test STOPS its monitor and ticks the
// loop before returning so the cancellation propagates (async-loop trap: a live
// monitor loop left running hangs the test host).
public class TurnMonitorTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();
    readonly List<TurnMonitor> monitors = new();

    public void Dispose()
    {
        foreach (var monitor in monitors)
            if (monitor) monitor.StopMonitor();
        loop.Tick(0.05f); // let cancelled loop continuations unwind
        loop.Tick(0.05f);
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    // ── doubles ─────────────────────────────────────────────────────────────

    sealed class LocalStubPlayer : IPlayer
    {
        public LocalStubPlayer(string name, Domains domain)
        {
            Name = name;
            RoundStats = new RoundStats { Name = name, Domain = domain };
        }

        public Domains Domain => RoundStats.Domain;
        public string Name { get; }
        public int AvatarId => 0;
        public string PlayerUUID => Name;
        public IVessel Vessel => null;
        public InputController InputController => null;
        public IInputStatus InputStatus => null;
        public IRoundStats RoundStats { get; }
        public bool IsActive => true;
        public bool IsInitializedAsAI => false;
        public bool IsMultiplayerOwner => true;
        public bool IsNetworkOwner => true;
        public bool IsNetworkClient => false;
        public bool IsLocalUser => true;
        public ulong PlayerNetId => 0;
        public ulong VesselNetId => 0;
        public ulong OwnerClientNetId => 0;
        public Transform Transform => null;

        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel) { }
        public void InitializeForMultiplayerMode(IVessel vessel) { }
        public void ToggleGameObject(bool toggle) { }
        public void DestroyPlayer() { }
        public void StartPlayer() { }
        public void ResetForPlay() { }
        public void SetPoseOfVessel(Pose pose) { }
        public void ChangeVessel(IVessel vessel) { }
    }

    /// <summary>Counts OnTurnEnded so the base Update's end-of-turn path is observable.</summary>
    class CountingCrystalMonitor : CrystalCollisionTurnMonitor
    {
        public int TurnEndedCount;
        protected override void OnTurnEnded() => TurnEndedCount++;
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static void SetField(object target, string name, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var field = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{name}' not found on {target.GetType().Name}.");
    }

    (GameDataSO data, LocalStubPlayer player) MakeGameDataWithLocalPlayer()
    {
        NetworkManager.Singleton = null;
        var data = ScriptableObject.CreateInstance<GameDataSO>();
        var player = new LocalStubPlayer("Local", Domains.Jade);
        data.AddPlayer(player); // sets LocalPlayer + RoundStatsList entry
        return (data, player);
    }

    T SpawnMonitor<T>(GameDataSO data, int crystalTarget, ScriptableEventString display = null)
        where T : CrystalCollisionTurnMonitor
    {
        var go = new GameObject("turn-monitor");
        go.SetActive(false);
        spawned.Add(go);
        var monitor = go.AddComponent<T>();
        monitors.Add(monitor);
        SetField(monitor, "gameData", data);
        SetField(monitor, "CrystalCollisions", crystalTarget);
        if (display != null) SetField(monitor, "onUpdateTurnMonitorDisplay", display);
        go.SetActive(true);
        return monitor;
    }

    // ── CrystalCollisionTurnMonitor ─────────────────────────────────────────

    [Fact]
    public void CheckForEndOfTurn_TrueOnlyAtCrystalTarget()
    {
        var (data, player) = MakeGameDataWithLocalPlayer();
        var monitor = SpawnMonitor<CrystalCollisionTurnMonitor>(data, crystalTarget: 3);

        monitor.StartMonitor();
        Assert.False(monitor.CheckForEndOfTurn());

        player.RoundStats.CrystalsCollected = 2;
        Assert.False(monitor.CheckForEndOfTurn());

        player.RoundStats.CrystalsCollected = 3;
        Assert.True(monitor.CheckForEndOfTurn());

        monitor.StopMonitor();
    }

    [Fact]
    public void StartMonitor_ZeroTargetFallsBackTo39()
    {
        var (data, _) = MakeGameDataWithLocalPlayer();
        var monitor = SpawnMonitor<CrystalCollisionTurnMonitor>(data, crystalTarget: 0);

        monitor.StartMonitor();
        Assert.Equal("39", monitor.GetRemainingCrystalsCountToCollect());

        monitor.StopMonitor();
    }

    [Fact]
    public void CrystalChange_RaisesRemainingCountOnDisplayEvent()
    {
        var (data, player) = MakeGameDataWithLocalPlayer();
        var display = ScriptableObject.CreateInstance<ScriptableEventString>();
        var messages = new List<string>();
        display.OnRaised += messages.Add;

        var monitor = SpawnMonitor<CrystalCollisionTurnMonitor>(data, crystalTarget: 5, display);
        monitor.StartMonitor();
        Assert.Equal("5", messages[^1]); // initial display on StartMonitor

        player.RoundStats.CrystalsCollected = 2; // unspawned RoundStats raises locally
        Assert.Equal("3", messages[^1]);

        monitor.StopMonitor();

        player.RoundStats.CrystalsCollected = 4; // unsubscribed after stop
        Assert.Equal("3", messages[^1]);
    }

    [Fact]
    public void Update_FiresOnTurnEndedOnceAndStopsMonitor()
    {
        var (data, player) = MakeGameDataWithLocalPlayer();
        var monitor = SpawnMonitor<CountingCrystalMonitor>(data, crystalTarget: 2);

        monitor.StartMonitor();
        player.RoundStats.CrystalsCollected = 2;

        loop.Tick(0.02f); // base Update: CheckForEndOfTurn → OnTurnEnded → StopMonitor
        Assert.Equal(1, monitor.TurnEndedCount);

        loop.Tick(0.02f); // monitor stopped — no second fire
        Assert.Equal(1, monitor.TurnEndedCount);
    }

    [Fact]
    public void ResetMonitor_WithRestart_ResubscribesAndRuns()
    {
        var (data, player) = MakeGameDataWithLocalPlayer();
        var monitor = SpawnMonitor<CountingCrystalMonitor>(data, crystalTarget: 2);

        monitor.StartMonitor();
        player.RoundStats.CrystalsCollected = 2;
        loop.Tick(0.02f);
        Assert.Equal(1, monitor.TurnEndedCount);

        player.RoundStats.CrystalsCollected = 0;
        monitor.ResetMonitor(restart: true);
        loop.Tick(0.02f);
        Assert.Equal(1, monitor.TurnEndedCount); // below target after reset

        player.RoundStats.CrystalsCollected = 2;
        loop.Tick(0.02f);
        Assert.Equal(2, monitor.TurnEndedCount); // second turn end after restart

        monitor.StopMonitor();
    }
}
