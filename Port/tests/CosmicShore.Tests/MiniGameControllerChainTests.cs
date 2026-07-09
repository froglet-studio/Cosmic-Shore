using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Controller-chain arc — the real MiniGameControllerBase template-method chain,
// headless. A concrete MultiplayerMiniGameControllerBase subclass is Spawn()ed
// host-mode (scene-placed-NetworkBehaviour contract) and driven purely through
// GameDataSO turn events (gameData.InvokeGameTurnConditionsMet — what the turn
// monitors raise): rounds → turns → countdown → gameplay → end, with the ready
// button toggles observed on the SOAP channel. HexRaceController's server-side
// OnTurnEndedCustom is verified against the CLAUDE.md HexRace semantics: the
// first active domain whose summed crystals reach the target wins TOGETHER,
// winners share Score = finishTime, losers get the 10000+deficit golf sentinel
// (tying within a domain), Results ranked, WinnerName = best contributor.
//
// Async-void discipline: every controller is Despawn()ed (unhooking its SOAP
// subscriptions + stopping the domain-sum coroutine) and the single process-wide
// GameLoop is disposed per test — no controller loop outlives its test.
// ─────────────────────────────────────────────────────────────────────────────
public class MiniGameControllerChainTests
{
    const float Dt = 1f / 60f;

    // ── helpers ─────────────────────────────────────────────────────────────

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

    static void SetProperty(object target, string property, object value)
    {
        var p = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(target.GetType().Name, property);
        p.SetValue(target, value);
    }

    static GameDataSO MakeGameData()
    {
        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnSessionStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameRoundStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameRoundEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameTurnStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameTurnEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnWinnerCalculated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
        gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
        gameData.SelectedIntensity.Value = 1;
        gameData.SelectedPlayerCount = ScriptableObject.CreateInstance<IntVariable>();
        return gameData;
    }

    static RoundStats MakeStats(string name, Domains domain, int crystals = 0, float score = 0f)
        => new RoundStats { Name = name, Domain = domain, CrystalsCollected = crystals, Score = score };

    /// <summary>Concrete chain controller with observable template-method hooks.</summary>
    class TestChainController : MultiplayerMiniGameControllerBase
    {
        public int TurnEndedCustomCount;
        public int RoundEndedCustomCount;

        protected override void OnTurnEndedCustom() => TurnEndedCustomCount++;
        protected override void OnRoundEndedCustom() => RoundEndedCustomCount++;
    }

    // ── template method: rounds → turns → end, driven by GameDataSO turn events ──

    [Fact]
    public void ControllerChain_RunsRoundsAndTurns_ThenEndsGame_ThroughTurnEvents()
    {
        using var loop = new GameLoop(nameof(ControllerChain_RunsRoundsAndTurns_ThenEndsGame_ThroughTurnEvents));
        NetworkManager.Singleton = null;
        var gameData = MakeGameData();

        int roundStarted = 0, roundEnded = 0, gameEnded = 0, winnerCalculated = 0;
        gameData.OnMiniGameRoundStarted.OnRaised += () => roundStarted++;
        gameData.OnMiniGameRoundEnd.OnRaised += () => roundEnded++;
        gameData.OnMiniGameEnd.OnRaised += () => gameEnded++;
        gameData.OnWinnerCalculated.OnRaised += () => winnerCalculated++;

        var readyToggles = new List<bool>();
        var readyChannel = ScriptableObject.CreateInstance<ScriptableEventBool>();
        readyChannel.OnRaised += v => readyToggles.Add(v);

        var go = new GameObject("chain-controller");
        var controller = go.AddComponent<TestChainController>();
        SetField(controller, "gameData", gameData);
        SetField(controller, "numberOfRounds", 2);
        SetField(controller, "numberOfTurnsPerRound", 2);
        SetField(controller, "_onToggleReadyButton", readyChannel);

        // Sorting at game end walks RoundStatsList — give it two rows.
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, score: 10f));
        gameData.RoundStatsList.Add(MakeStats("B", Domains.Ruby, score: 20f));

        try
        {
            controller.Spawn(); // host-mode: IsServer=true — the server-authoritative flow

            // InitializeAfterDelay (1000ms unscaled) → InvokeSessionStarted + SetupNewRound.
            loop.Run(70, Dt);
            Assert.Equal(1, roundStarted);
            Assert.Equal(0, gameData.RoundsPlayed);
            Assert.Contains(true, readyToggles); // SetupNewTurn (server) showed the Ready button

            // Turn 1 of round 1 — the turn-monitor-shaped end signal.
            gameData.InvokeGameTurnConditionsMet();
            Assert.Equal(1, controller.TurnEndedCustomCount);
            Assert.Equal(1, gameData.TurnsTakenThisRound);
            Assert.Equal(0, roundEnded);

            // Turn 2 → round 1 ends, round 2 starts.
            gameData.InvokeGameTurnConditionsMet();
            Assert.Equal(2, controller.TurnEndedCustomCount);
            Assert.Equal(1, controller.RoundEndedCustomCount);
            Assert.Equal(1, gameData.RoundsPlayed);
            Assert.Equal(1, roundEnded);
            Assert.Equal(2, roundStarted);
            Assert.Equal(0, gameData.TurnsTakenThisRound);
            Assert.Equal(0, gameEnded);

            // Round 2's two turns → game end (SyncGameEnd: sort + domain stats + winner + end).
            gameData.InvokeGameTurnConditionsMet();
            gameData.InvokeGameTurnConditionsMet();
            Assert.Equal(4, controller.TurnEndedCustomCount);
            Assert.Equal(2, controller.RoundEndedCustomCount);
            Assert.Equal(2, gameData.RoundsPlayed);
            Assert.Equal(1, winnerCalculated);
            Assert.Equal(1, gameEnded);

            // Default (non-golf) sort: highest score first.
            Assert.Equal("B", gameData.RoundStatsList[0].Name);
            Assert.NotEmpty(gameData.DomainStatsList);
        }
        finally
        {
            controller.Despawn();
            loop.Tick(Dt);
        }
    }

    // ── countdown: Ready → BeginCountdown → OnCountdownTimerEnded after 4 beats ──

    [Fact]
    public void CountdownTimer_FiresOnComplete_AfterFourBeats_AndKillsOnRestart()
    {
        using var loop = new GameLoop(nameof(CountdownTimer_FiresOnComplete_AfterFourBeats_AndKillsOnRestart));
        var go = new GameObject("countdown");
        var timer = go.AddComponent<CountdownTimer>();
        SetField(timer, "countdownDuration", 0.5f); // 4 sprites × 0.5s = 2s total
        // Scene transcription: the real scene wires an Image child as the countdown display.
        var display = new GameObject("CountdownDisplay", typeof(RectTransform))
            .AddComponent<CosmicShore.Engine.UI.Image>();
        display.transform.SetParent(go.transform, false);
        SetField(timer, "countdownDisplay", display);

        int completions = 0;
        timer.BeginCountdown(() => completions++);

        loop.Run(60, Dt); // 1s — mid-count
        Assert.Equal(0, completions);

        // Restart mid-count: the first sequence is killed (DOTween _seq?.Kill() parity).
        timer.BeginCountdown(() => completions++);
        loop.Run(150, Dt); // 2.5s — past the restarted count's 2s
        Assert.Equal(1, completions);

        loop.Run(240, Dt); // no further fires
        Assert.Equal(1, completions);
    }

    // ── HexRaceController: domain-aggregated winner + golf scores (CLAUDE.md semantics) ──

    [Fact]
    public void HexRace_OnTurnEnd_DomainAggregatedWinner_GolfScores_RankedResults()
    {
        using var loop = new GameLoop(nameof(HexRace_OnTurnEnd_DomainAggregatedWinner_GolfScores_RankedResults));
        var nmGo = new GameObject("nm");
        NetworkManager.Singleton = nmGo.AddComponent<NetworkManager>();

        var gameData = MakeGameData();
        var rule = ScriptableObject.CreateInstance<HexRaceScoringRuleSO>();

        int winnerCalculated = 0, gameEnded = 0;
        gameData.OnWinnerCalculated.OnRaised += () => winnerCalculated++;
        gameData.OnMiniGameEnd.OnRaised += () => gameEnded++;

        var go = new GameObject("hexrace-controller");
        go.SetActive(false);
        var controller = go.AddComponent<HexRaceController>();
        SetField(controller, "gameData", gameData);
        SetField(controller, "rule", rule);
        go.SetActive(true);

        try
        {
            controller.Spawn(); // no segmentSpawner wired — SpawnTrackLocally self-skips

            // The race roster: Jade teammates A (3) + B (2) reach the 5-crystal target
            // together; Ruby C (4) trails by 1. A is the local player, elapsed Score 87.25s.
            gameData.CrystalTargetCount = 5;
            var a = MakeStats("A", Domains.Jade, crystals: 3, score: 87.25f);
            var b = MakeStats("B", Domains.Jade, crystals: 2, score: 87.25f);
            var c = MakeStats("C", Domains.Ruby, crystals: 4, score: 87.25f);
            gameData.RoundStatsList.Add(a);
            gameData.RoundStatsList.Add(b);
            gameData.RoundStatsList.Add(c);
            SetProperty(gameData, "LocalRoundStats", a);

            // Let InitializeAfterDelay (1s) + SpawnTrackEarly (1.5s) run out first.
            loop.Run(100, Dt);

            // The turn-monitor end signal → HandleTurnEnd → OnTurnEndedCustom (server).
            gameData.InvokeGameTurnConditionsMet();

            // Winner: Jade (5 ≥ 5 target); representative = best contributor (A, 3 > 2).
            Assert.Equal("A", gameData.WinnerName);
            Assert.Equal(Domains.Jade, gameData.WinnerDomain);
            Assert.Equal(1, winnerCalculated);
            Assert.Equal(1, gameEnded);

            // Winners share Score = finishTime; losers = 10000 + DOMAIN deficit (tie within domain).
            Assert.Equal(87.25f, a.Score, 3);
            Assert.Equal(87.25f, b.Score, 3);
            Assert.Equal(GolfScoreSentinels.EncodeHexRaceLoserScore(1), c.Score, 3);

            // Golf sort: RoundStatsList ascending by Score → both winners above the loser.
            Assert.True(gameData.IsGolfRules);
            Assert.Equal(Domains.Jade, gameData.RoundStatsList[0].Domain);
            Assert.Equal(Domains.Jade, gameData.RoundStatsList[1].Domain);
            Assert.Equal("C", gameData.RoundStatsList[2].Name);

            // Results: single ranked source of truth, 1..N, winner on top.
            Assert.Equal(new[] { 1, 2, 3 }, gameData.Results.Select(r => r.Rank).ToArray());
            Assert.Equal("A", gameData.Results[0].Name);
            Assert.Equal(Domains.Ruby, gameData.Results[2].Domain);

            // Race-over latch: the base flow's SetupNewRound is suppressed (no new round).
            int roundsPlayed = gameData.RoundsPlayed;
            loop.Run(30, Dt);
            Assert.Equal(roundsPlayed, gameData.RoundsPlayed);

            // A second end signal does not double-report (raceEnded latch).
            gameData.InvokeGameTurnConditionsMet();
            Assert.Equal(1, winnerCalculated);
        }
        finally
        {
            controller.Despawn();
            NetworkManager.Singleton = null;
            loop.Tick(Dt);
        }
    }

    [Fact]
    public void HexRace_ObjectiveNotReached_KeepsTheRaceRunning()
    {
        using var loop = new GameLoop(nameof(HexRace_ObjectiveNotReached_KeepsTheRaceRunning));
        NetworkManager.Singleton = null;
        var gameData = MakeGameData();
        var rule = ScriptableObject.CreateInstance<HexRaceScoringRuleSO>();

        int gameEnded = 0;
        gameData.OnMiniGameEnd.OnRaised += () => gameEnded++;

        var go = new GameObject("hexrace-controller");
        go.SetActive(false);
        var controller = go.AddComponent<HexRaceController>();
        SetField(controller, "gameData", gameData);
        SetField(controller, "rule", rule);
        go.SetActive(true);

        try
        {
            controller.Spawn();
            gameData.CrystalTargetCount = 10;
            gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 3));
            loop.Run(100, Dt);

            gameData.InvokeGameTurnConditionsMet(); // 3 < 10 — nobody wins yet

            Assert.Equal("", gameData.WinnerName);
            Assert.Equal(Domains.Blue, gameData.WinnerDomain);
            Assert.Equal(0, gameEnded);
        }
        finally
        {
            controller.Despawn();
            loop.Tick(Dt);
        }
    }
}
