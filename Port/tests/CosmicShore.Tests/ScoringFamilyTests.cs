using System;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Scoring family (2026-07-10) — BaseScoreTracker + the concrete scorings that
// complete upstream's CreateScoring switch, restoring HexRaceScoreTracker.
// Covers the SOAP-driven lifecycle end-to-end: OnInitializeGame builds the
// configured scoring array (CreateScoring by mode + multiplier); the turn start
// subscribes every scoring to its RoundStats stat events, so a stat write flows
// stat-setter → scoring.UpdateScore → tracker.CalculateTotalScore →
// RoundStats.Score (summed across all configured scorings); the turn end
// unsubscribes (later stat writes leave Score untouched); OnMiniGameEnd sorts
// the roster and raises OnWinnerCalculated; GetScoring<T> finds a configured
// instance; and the restored HexRaceScoreTracker exposes its telemetry stats
// (empty when no vessel telemetry was captured — the pre-race posture).
// ─────────────────────────────────────────────────────────────────────────────

public class ScoringFamilyTests : IDisposable
{
    readonly GameLoop loop = new(nameof(ScoringFamilyTests));

    // Minimal concrete tracker: BaseScoreTracker's lifecycle is subclass-driven
    // (upstream trackers call SubscribeEvents from their own Start).
    sealed class TestScoreTracker : BaseScoreTracker
    {
        void Start() => SubscribeEvents();
        public new void OnDestroy() => UnsubscribeEvents();
    }

    readonly GameDataSO gameData;
    readonly TestScoreTracker tracker;
    readonly RoundStats alice;
    readonly RoundStats bob;

    public ScoringFamilyTests()
    {
        gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameTurnStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameTurnEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnWinnerCalculated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        alice = new RoundStats { Name = "Alice", Domain = Domains.Jade };
        bob = new RoundStats { Name = "Bob", Domain = Domains.Ruby };
        gameData.RoundStatsList.Add(alice);
        gameData.RoundStatsList.Add(bob);

        var go = new GameObject("score-tracker");
        go.SetActive(false);
        tracker = go.AddComponent<TestScoreTracker>();
        Set(tracker, "gameData", gameData);
        Set(tracker, "OnClickToMainMenu", ScriptableObject.CreateInstance<ScriptableEventNoParam>());
        Set(tracker, "scoringConfigs", new[]
        {
            new ScoringConfig { Mode = ScoringModes.CrystalsCollected, Multiplier = 10f },
            new ScoringConfig { Mode = ScoringModes.VolumeCreated, Multiplier = 2f },
        });
        go.SetActive(true);
        loop.Tick(1f / 60f); // Start: SubscribeEvents
    }

    public void Dispose() => loop.Dispose();

    static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    void StartGameAndTurn()
    {
        gameData.OnInitializeGame.Raise();      // CreateScoring per config
        gameData.OnMiniGameTurnStarted.Raise(); // scorings subscribe to stat events
    }

    [Fact]
    public void InitializeGame_BuildsTheConfiguredScorings()
    {
        StartGameAndTurn();

        Assert.NotNull(tracker.GetScoring<CrystalsCollectedScoring>());
        Assert.NotNull(tracker.GetScoring<VolumeCreatedScoring>());
        Assert.Null(tracker.GetScoring<TurnsPlayedScoring>()); // not configured
    }

    [Fact]
    public void StatWrites_FlowThroughScorings_IntoRoundStatsScore()
    {
        StartGameAndTurn();

        // Crystal lane: 3 crystals × 10 = 30.
        alice.CrystalsCollected = 3;
        Assert.Equal(30f, alice.Score);

        // Volume lane joins the sum: 3×10 + 5×2 = 40.
        alice.VolumeCreated = 5f;
        Assert.Equal(40f, alice.Score);

        // Upstream semantics, pinned: each scoring instance holds ONE shared
        // accumulator (BaseScoring.Score), overwritten by whichever player's
        // stat changed last. Bob's crystal write sets the crystal scoring to
        // 1×10, but the volume scoring still holds Alice's 5×2 — his roll-up
        // sums both (10 + 10). Alice's already-written Score is untouched.
        bob.CrystalsCollected = 1;
        Assert.Equal(20f, bob.Score);
        Assert.Equal(40f, alice.Score);
    }

    [Fact]
    public void TurnEnd_Unsubscribes_LaterWritesLeaveScoreUntouched()
    {
        StartGameAndTurn();
        alice.CrystalsCollected = 2;
        Assert.Equal(20f, alice.Score);

        gameData.OnMiniGameTurnEnd.Raise();

        alice.CrystalsCollected = 9; // no subscribed scoring — Score frozen
        Assert.Equal(20f, alice.Score);
    }

    [Fact]
    public void GameEnd_SortsTheRoster_AndRaisesWinnerCalculated()
    {
        StartGameAndTurn();
        alice.CrystalsCollected = 5; // 50
        bob.CrystalsCollected = 9;   // 90

        int winnerRaises = 0;
        gameData.OnWinnerCalculated.OnRaised += () => winnerRaises++;

        gameData.OnMiniGameEnd.Raise();

        Assert.Equal(1, winnerRaises);
        Assert.Same(bob, gameData.RoundStatsList[0]); // sorted descending (golfRules false)
    }

    [Fact]
    public void RestoredHexRaceScoreTracker_ExposesTelemetryStats()
    {
        var go = new GameObject("hexrace-tracker");
        go.SetActive(false); // component only — full lifecycle needs the round world
        var hexTracker = go.AddComponent<HexRaceScoreTracker>();

        // Pre-race posture: no vessel telemetry captured yet → empty stat set.
        Assert.Empty(hexTracker.GetExposedStats());
    }
}
