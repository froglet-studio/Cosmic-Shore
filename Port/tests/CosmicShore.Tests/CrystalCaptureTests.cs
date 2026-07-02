using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Cli;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Crystal Capture arc — the crystal race on the ported controller chain.
//
// Unit layers: CrystalCaptureScoringRuleSO (domain-aggregated end condition over
// CrystalsCollected sums, points scoring: Score = own crystal count, descending
// results, no golf) and the NetworkCrystalCollisionTurnMonitor target resolution
// through the real EndConditionOverrides tool asset keyed by GameMode
// (CRYSTAL_CAPTURE.md §End Conditions).
//
// Integration layer: CrystalCaptureRound (CosmicShore.Cli) runs the WHOLE round
// through the real chain — ready → countdown → crystal-seek AI → trigger-pass
// claims onto RoundStats → monitor end condition → OnTurnEndedCustom →
// SyncFinalScores → GameDataSO.Results — and these tests assert
// CRYSTAL_CAPTURE.md's objective + end-flow semantics on its result.
//
// Rounds run sequentially (assembly-wide parallelization is disabled — the
// GameLoop/Time are process-global), every spawned NetworkBehaviour is
// despawned and every monitor stopped before its loop is disposed (async-void
// discipline), and the EndConditionOverrides static cache + Resources
// registration are reset by every test that touches them.
// ─────────────────────────────────────────────────────────────────────────────
public class CrystalCaptureTests
{
    const float Dt = 1f / 60f;

    static void SetField(object target, string field, object value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().Name, field);
    }

    static void ResetEndConditionOverrides()
    {
        typeof(EndConditionOverridesSO)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, null);
        Resources.Register(EndConditionOverridesSO.ResourcePath, null);
    }

    static GameDataSO MakeGameData()
    {
        NetworkManager.Singleton = null;
        return ScriptableObject.CreateInstance<GameDataSO>();
    }

    static RoundStats MakeStats(string name, Domains domain, int crystals)
        => new RoundStats { Name = name, Domain = domain, CrystalsCollected = crystals };

    static CrystalCaptureScoringRuleSO MakeRule()
    {
        var rule = ScriptableObject.CreateInstance<CrystalCaptureScoringRuleSO>();
        SetField(rule, "metric", ScoringMetric.Crystals);
        SetField(rule, "golfRules", false);
        return rule;
    }

    // ── CrystalCaptureScoringRuleSO ─────────────────────────────────────────

    [Fact]
    public void ScoringRule_ObjectiveIsDomainAggregated_TeammatesCaptureTogether()
    {
        var gameData = MakeGameData();
        gameData.RequestedDomainCount = 2;
        gameData.CrystalTargetCount = 20;
        var rule = MakeRule();

        // 12 + 7 Jade vs 15 Ruby — no domain at 20 yet.
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, 12));
        gameData.RoundStatsList.Add(MakeStats("B", Domains.Jade, 7));
        gameData.RoundStatsList.Add(MakeStats("C", Domains.Ruby, 15));
        Assert.False(rule.IsObjectiveReached(gameData, out var winner));
        Assert.Equal(Domains.Blue, winner);

        // Jade's TEAM sum reaches 20 (13 + 7) — the turn ends for the domain even though
        // no individual reached it (CLAUDE.md: domain-aggregated scoring).
        gameData.RoundStatsList[0].CrystalsCollected = 13;
        Assert.True(rule.IsObjectiveReached(gameData, out winner));
        Assert.Equal(Domains.Jade, winner);

        // Remaining is the domain deficit (the HUD's team readout).
        Assert.Equal(0, rule.Remaining(gameData, Domains.Jade));
        Assert.Equal(5, rule.Remaining(gameData, Domains.Ruby));
    }

    [Fact]
    public void ScoringRule_PointsScores_ScoreIsOwnCrystals_ResultsRankDescending()
    {
        var gameData = MakeGameData();
        gameData.RequestedDomainCount = 2;
        var rule = MakeRule();

        var a = MakeStats("A", Domains.Jade, 25);
        var b = MakeStats("B", Domains.Jade, 12);
        var c = MakeStats("C", Domains.Ruby, 18);
        gameData.RoundStatsList.Add(b);
        gameData.RoundStatsList.Add(c);
        gameData.RoundStatsList.Add(a);

        // Winning domain = highest crystal sum (Jade 37 vs Ruby 18).
        Assert.Equal(Domains.Jade, rule.ResolveWinner(gameData));
        gameData.WinnerDomain = Domains.Jade;

        // Score = individual CrystalsCollected for EVERY player (winner and losers alike) —
        // the scoreboard's secondary stat shows individual contribution (CRYSTAL_CAPTURE.md §7).
        rule.AssignScores(gameData, Domains.Jade, 0f);
        Assert.Equal(25f, a.Score);
        Assert.Equal(12f, b.Score);
        Assert.Equal(18f, c.Score);

        // Non-golf: descending scores, rank 1 = most crystals, "N Crystals" text, no secondary.
        var results = rule.BuildResults(gameData);
        Assert.Equal(new[] { 1, 2, 3 }, results.Select(r => r.Rank).ToArray());
        Assert.Equal(new[] { "A", "C", "B" }, results.Select(r => r.Name).ToArray());
        Assert.Equal("25 Crystals", results[0].ScoreText);
        Assert.Null(results[0].Secondary);
        Assert.True(results[0].Score >= results[1].Score && results[1].Score >= results[2].Score);
    }

    // ── the crystal target through the REAL tool surface ────────────────────

    [Fact]
    public void NetworkMonitor_ResolvesCrystalCaptureTargetFromToolAsset_KeyedByGameMode()
    {
        using var loop = new GameLoop(nameof(NetworkMonitor_ResolvesCrystalCaptureTargetFromToolAsset_KeyedByGameMode));
        NetworkManager.Singleton = null;

        ResetEndConditionOverrides();
        var overrides = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
        overrides.crystalCaptureCrystalCount = 7;
        overrides.hexRaceCrystalCount = 55; // the OTHER mode's knob must not bleed over
        Resources.Register(EndConditionOverridesSO.ResourcePath, overrides);

        var gameData = MakeGameData();
        gameData.GameMode = GameModes.MultiplayerCrystalCapture;
        gameData.RequestedDomainCount = 1;
        gameData.ScoringRule = MakeRule();
        // The monitor's Debug-build CSDebug.Log interpolates SelectedIntensity.Value
        // (stripped in Release) — wire the variable like the scene does.
        gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
        gameData.SelectedIntensity.Value = 1;

        var player = new JoustTestsLocalPlayer("Local", Domains.Jade);
        gameData.AddPlayer(player); // local user → LocalPlayer + RoundStats entry

        var go = new GameObject("crystal-monitor");
        go.SetActive(false);
        var monitor = go.AddComponent<NetworkCrystalCollisionTurnMonitor>();
        SetField(monitor, "gameData", gameData);
        SetField(monitor, "onUpdateTurnMonitorDisplay", ScriptableObject.CreateInstance<ScriptableEventString>());
        go.SetActive(true);

        try
        {
            monitor.Spawn(); // IsServer=true — the authoritative end-of-turn check
            monitor.StartMonitor();

            // Target resolved from the Crystal Capture knob (not HexRace's) and published.
            Assert.Equal(7, gameData.CrystalTargetCount);

            player.RoundStats.CrystalsCollected = 6;
            Assert.False(monitor.CheckForEndOfTurn());

            player.RoundStats.CrystalsCollected = 7;
            Assert.True(monitor.CheckForEndOfTurn()); // rule-delegated domain sum ≥ target
        }
        finally
        {
            monitor.StopMonitor();
            monitor.Despawn();
            go.SetActive(false);
            loop.Tick(Dt);
            ResetEndConditionOverrides();
        }
    }

    /// <summary>Minimal local IPlayer for the monitor rig (same shape as the other test stubs).</summary>
    sealed class JoustTestsLocalPlayer : IPlayer
    {
        public JoustTestsLocalPlayer(string name, Domains domain)
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

    // ── the full round through the real chain (CosmicShore.Cli round) ────────

    const int CompactTarget = 4; // small target → short round, full end-flow path

    static CrystalCaptureRoundResult RunRound(int players, int seed)
        => CrystalCaptureRound.Run(new CrystalCaptureRoundOptions
        {
            PlayerCount = players,
            Seed = seed,
            CrystalTarget = CompactTarget,
        });

    [Fact]
    public void Round_Completes_ObjectiveAndEndFlowFollowCrystalCaptureMd()
    {
        var result = RunRound(players: 4, seed: 42);

        Assert.True(result.Finished, "round must end through the monitor → OnTurnEndedCustom → sync flow");
        Assert.Empty(result.EngineErrors); // fail loud on any Error/Exception logged mid-round
        Assert.NotEqual(Domains.Blue, result.WinnerDomain);
        Assert.False(string.IsNullOrEmpty(result.WinnerName));
        Assert.True(result.TotalClaims >= CompactTarget, "claims must flow through the trigger pipeline");

        // Domain-aggregated objective: the winning domain's summed crystals reached the target.
        Assert.Equal(CompactTarget, result.WinnerDomainCrystals);
        int winnerDomainSum = result.Standings
            .Where(s => s.Domain == result.WinnerDomain)
            .Sum(s => s.Crystals);
        Assert.Equal(CompactTarget, winnerDomainSum);

        // One standings row per player, ranked 1..N; winner = best contributor on the
        // winning domain, on top under points ordering.
        Assert.Equal(4, result.Standings.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Standings.Select(s => s.Rank).ToArray());
        Assert.Equal(result.WinnerName, result.Standings[0].Name);
        Assert.Equal(result.WinnerDomain, result.Standings[0].Domain);

        // Points semantics (CRYSTAL_CAPTURE.md §7): Score = individual crystal count,
        // sorted descending — never golf, no loser sentinel.
        foreach (var s in result.Standings)
        {
            Assert.Equal(s.Crystals, (int)s.Score);
            Assert.Equal($"{s.Crystals} Crystals", s.ScoreText);
        }
        var scores = result.Standings.Select(s => s.Score).ToArray();
        Assert.Equal(scores.OrderByDescending(s => s), scores);
    }

    [Fact]
    public void Round_IsAPureFunctionOfItsSeed()
    {
        var first = RunRound(players: 4, seed: 42);
        var second = RunRound(players: 4, seed: 42);

        Assert.True(first.Finished && second.Finished);
        Assert.Equal(first.Transcript, second.Transcript); // claim-by-claim, line-identical
        Assert.Equal(first.WinnerName, second.WinnerName);
        Assert.Equal(first.WinnerDomain, second.WinnerDomain);
        Assert.Equal(first.FramesSimulated, second.FramesSimulated);
        Assert.Equal(
            first.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Crystals, s.Score)),
            second.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Crystals, s.Score)));

        var third = RunRound(players: 4, seed: 1337);
        Assert.NotEqual(first.Transcript, third.Transcript); // the seed is load-bearing
    }
}
