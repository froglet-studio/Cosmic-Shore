using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// SA1 coverage: the scoring family (ScoringRuleSO + ScoringMetrics + GolfScoreSentinels +
// ScoreResultBuilder + HexRaceScoringRuleSO + the BaseScoring listeners + the live
// composite scoring modes) and the arcade SO chain (SO_Game / SO_ArcadeGame / SO_Vessel),
// plus the restored GameDataSO surface (ScoringRule member, SyncFromArcadeGame).
// Per CLAUDE.md: HexRace is golf-ruled and DOMAIN-aggregated — IsObjectiveReached fires
// on ScoringMetrics.SumByDomain reaching the target, teammates win/lose together, and
// ties break by ActiveDomains order (Jade → Ruby → Gold).
public class ScoringArcadeTests
{
    // ── test doubles / helpers ──────────────────────────────────────────────

    class StubScoreTracker : IScoreTracker
    {
        public readonly List<string> Calls = new();
        public void CalculateTotalScore(string playerName) => Calls.Add(playerName);
    }

    class StubScoringMode : BaseScoringMode
    {
        readonly float _calculate;
        readonly float _endTurn;

        public StubScoringMode(float calculate, float endTurn) : base(1f)
        {
            _calculate = calculate;
            _endTurn = endTurn;
        }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
            => currentScore + _calculate;

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
            => currentScore + _endTurn;
    }

    static GameDataSO MakeGameData()
    {
        NetworkManager.Singleton = null; // offline / single-process default
        return ScriptableObject.CreateInstance<GameDataSO>();
    }

    static RoundStats MakeStats(string name, Domains domain, int crystals = 0, float score = 0f)
        => new RoundStats { Name = name, Domain = domain, CrystalsCollected = crystals, Score = score };

    static HexRaceScoringRuleSO MakeHexRule()
        => ScriptableObject.CreateInstance<HexRaceScoringRuleSO>();

    // ── ScoringMetrics ──────────────────────────────────────────────────────

    [Fact]
    public void ScoringMetrics_Read_MapsEachMetricToItsStat()
    {
        var stats = new RoundStats
        {
            CrystalsCollected = 1,
            OmniCrystalsCollected = 2,
            ElementalCrystalsCollected = 3,
            JoustCollisions = 4,
        };

        Assert.Equal(1, ScoringMetrics.Read(stats, ScoringMetric.Crystals));
        Assert.Equal(2, ScoringMetrics.Read(stats, ScoringMetric.OmniCrystals));
        Assert.Equal(3, ScoringMetrics.Read(stats, ScoringMetric.ElementalCrystals));
        Assert.Equal(4, ScoringMetrics.Read(stats, ScoringMetric.Jousts));
        Assert.Equal(0, ScoringMetrics.Read(stats, (ScoringMetric)99)); // unknown → 0
    }

    [Fact]
    public void ScoringMetrics_SumByDomain_SumsOnlyThatDomain_AndSkipsNulls()
    {
        var gameData = MakeGameData();
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 10));
        gameData.RoundStatsList.Add(MakeStats("B", Domains.Jade, crystals: 5));
        gameData.RoundStatsList.Add(MakeStats("C", Domains.Ruby, crystals: 7));
        gameData.RoundStatsList.Add(null);

        Assert.Equal(15, ScoringMetrics.SumByDomain(gameData, ScoringMetric.Crystals, Domains.Jade));
        Assert.Equal(7, ScoringMetrics.SumByDomain(gameData, ScoringMetric.Crystals, Domains.Ruby));
        Assert.Equal(0, ScoringMetrics.SumByDomain(gameData, ScoringMetric.Crystals, Domains.Gold));
    }

    // ── HexRaceScoringRuleSO — end condition (domain-aggregated) ────────────

    [Fact]
    public void HexRace_IsObjectiveReached_AggregatesTeammatesOnOneDomain()
    {
        var gameData = MakeGameData();
        gameData.CrystalTargetCount = 30;
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 16));
        gameData.RoundStatsList.Add(MakeStats("B", Domains.Jade, crystals: 14)); // team sum = 30
        gameData.RoundStatsList.Add(MakeStats("C", Domains.Ruby, crystals: 25));

        var rule = MakeHexRule();
        Assert.True(rule.IsObjectiveReached(gameData, out var winner));
        Assert.Equal(Domains.Jade, winner);
    }

    [Fact]
    public void HexRace_IsObjectiveReached_FalseAndBlue_WhenNoDomainAtTarget()
    {
        var gameData = MakeGameData();
        gameData.CrystalTargetCount = 30;
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 16));
        gameData.RoundStatsList.Add(MakeStats("C", Domains.Ruby, crystals: 25));

        var rule = MakeHexRule();
        Assert.False(rule.IsObjectiveReached(gameData, out var winner));
        Assert.Equal(Domains.Blue, winner); // "no team" sentinel
    }

    [Fact]
    public void HexRace_TargetCount_FallsBackTo39_WhenUnconfigured()
    {
        var gameData = MakeGameData(); // CrystalTargetCount = 0
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 38));

        var rule = MakeHexRule();
        Assert.False(rule.IsObjectiveReached(gameData, out _));
        Assert.Equal(1, rule.Remaining(gameData, Domains.Jade));

        gameData.RoundStatsList.Add(MakeStats("B", Domains.Jade, crystals: 1)); // sum = 39
        Assert.True(rule.IsObjectiveReached(gameData, out var winner));
        Assert.Equal(Domains.Jade, winner);
    }

    [Fact]
    public void HexRace_IsObjectiveReached_IgnoresDomainsOutsideRequestedCount()
    {
        var gameData = MakeGameData();
        gameData.CrystalTargetCount = 10;
        gameData.RequestedDomainCount = 2; // Jade + Ruby only
        gameData.RoundStatsList.Add(MakeStats("G", Domains.Gold, crystals: 100));

        var rule = MakeHexRule();
        Assert.False(rule.IsObjectiveReached(gameData, out var winner));
        Assert.Equal(Domains.Blue, winner);
    }

    [Fact]
    public void Remaining_ClampsAtZero_AndTracksConfiguredTarget()
    {
        var gameData = MakeGameData();
        gameData.CrystalTargetCount = 10;
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 4));
        gameData.RoundStatsList.Add(MakeStats("C", Domains.Ruby, crystals: 12));

        var rule = MakeHexRule();
        Assert.Equal(6, rule.Remaining(gameData, Domains.Jade));
        Assert.Equal(0, rule.Remaining(gameData, Domains.Ruby)); // past target → clamped
        Assert.Equal(10, rule.Remaining(gameData, Domains.Gold));
    }

    // ── ScoringRuleSO.ResolveWinner (deterministic tie-break) ───────────────

    [Fact]
    public void ResolveWinner_PicksHighestDomainSum_TieBreaksByActiveDomainOrder()
    {
        var gameData = MakeGameData();
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 5));
        gameData.RoundStatsList.Add(MakeStats("C", Domains.Ruby, crystals: 5)); // tie with Jade
        gameData.RoundStatsList.Add(MakeStats("G", Domains.Gold, crystals: 4));

        var rule = MakeHexRule();
        Assert.Equal(Domains.Jade, rule.ResolveWinner(gameData)); // Jade first in ActiveDomains

        gameData.RoundStatsList.Add(MakeStats("D", Domains.Ruby, crystals: 1)); // Ruby = 6
        Assert.Equal(Domains.Ruby, rule.ResolveWinner(gameData));
    }

    // ── HexRace golf scoring (AssignScores / sentinels) ─────────────────────

    static GameDataSO MakeFinishedHexRace(out HexRaceScoringRuleSO rule)
    {
        var gameData = MakeGameData(); // target falls back to 39
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 20));
        gameData.RoundStatsList.Add(MakeStats("B", Domains.Jade, crystals: 19)); // Jade = 39 → wins
        gameData.RoundStatsList.Add(MakeStats("C", Domains.Ruby, crystals: 10));
        gameData.RoundStatsList.Add(MakeStats("D", Domains.Ruby, crystals: 5)); // Ruby = 15 → 24 left
        rule = MakeHexRule();
        return gameData;
    }

    [Fact]
    public void HexRace_AssignScores_WinnersGetFinishTime_LosersTieOnTeamDeficitSentinel()
    {
        var gameData = MakeFinishedHexRace(out var rule);

        rule.AssignScores(gameData, Domains.Jade, finishTime: 83.5f);

        var byName = gameData.RoundStatsList.ToDictionary(s => s.Name);
        Assert.Equal(83.5f, byName["A"].Score);
        Assert.Equal(83.5f, byName["B"].Score); // every winning-domain player scores the time
        Assert.Equal(GolfScoreSentinels.DnfThreshold + 24, byName["C"].Score);
        Assert.Equal(byName["C"].Score, byName["D"].Score); // losing teammates tie on Score
        Assert.True(rule.GolfRules); // golf: lower = better — sentinel always ranks behind
    }

    [Fact]
    public void GolfScoreSentinels_EncodeDecode_RoundTrips()
    {
        Assert.Equal(10024f, GolfScoreSentinels.EncodeHexRaceLoserScore(24));
        Assert.Equal(24, GolfScoreSentinels.DecodeHexRaceCrystalsLeft(10024f));
        Assert.Equal(10000f, GolfScoreSentinels.EncodeHexRaceLoserScore(-3)); // clamps to base

        Assert.True(GolfScoreSentinels.IsFinishTime(83.5f));
        Assert.False(GolfScoreSentinels.IsFinishTime(10024f));
        Assert.True(GolfScoreSentinels.IsHexRaceLoserScore(10024f));
        Assert.False(GolfScoreSentinels.IsHexRaceLoserScore(83.5f));
        Assert.True(GolfScoreSentinels.IsJoustLoserScore(99999f));
        Assert.False(GolfScoreSentinels.IsJoustLoserScore(10024f));
    }

    // ── HexRace results / reveal formatting ─────────────────────────────────

    [Fact]
    public void HexRace_BuildResults_RanksGolfAscending_FormatsTimesAndDeficits()
    {
        var gameData = MakeFinishedHexRace(out var rule);
        rule.AssignScores(gameData, Domains.Jade, finishTime: 84.3f);

        var results = rule.BuildResults(gameData);

        Assert.Equal(4, results.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, results.Select(r => r.Rank).ToArray());

        // Winners first (golf ascending); equal scores tie-break by crystals descending.
        Assert.Equal("A", results[0].Name);
        Assert.Equal("B", results[1].Name);
        Assert.Equal(ScoreResultBuilder.FormatTime(84.3f), results[0].ScoreText);
        Assert.Equal("20 Crystals", results[0].Secondary);

        // Losers show the TEAM deficit (24 left for both Ruby players).
        Assert.Equal("C", results[2].Name);
        Assert.Equal("24 Crystals Left", results[2].ScoreText);
        Assert.Equal("24 Crystals Left", results[3].ScoreText);
        Assert.Equal(Domains.Ruby, results[3].Domain);
    }

    [Fact]
    public void HexRace_BuildReveal_VictoryShowsTime_DefeatShowsTeamCrystalsLeft()
    {
        var gameData = MakeFinishedHexRace(out var rule);
        rule.AssignScores(gameData, Domains.Jade, finishTime: 84.3f);
        var byName = gameData.RoundStatsList.ToDictionary(s => s.Name);

        var win = rule.BuildReveal(gameData, byName["A"], didWin: true);
        Assert.Equal("VICTORY", win.Header);
        Assert.Equal("RACE TIME", win.Label);
        Assert.Equal(84, win.Value);
        Assert.True(win.FormatAsTime);

        var loss = rule.BuildReveal(gameData, byName["D"], didWin: false);
        Assert.Equal("DEFEAT", loss.Header);
        Assert.Equal("CRYSTALS LEFT", loss.Label);
        Assert.Equal(24, loss.Value); // team deficit, not D's personal count
        Assert.False(loss.FormatAsTime);
    }

    // ── ScoreResultBuilder ──────────────────────────────────────────────────

    [Fact]
    public void ScoreResultBuilder_Build_GolfAscending_PointsDescending_StableTies()
    {
        var rows = new[]
        {
            new ScoreResultBuilder.Row("A", Domains.Jade, 5f, "5", null),
            new ScoreResultBuilder.Row("B", Domains.Ruby, 5f, "5", null),
            new ScoreResultBuilder.Row("C", Domains.Gold, 1f, "1", null),
        };

        var golf = ScoreResultBuilder.Build(rows, golfRules: true);
        Assert.Equal(new[] { "C", "A", "B" }, golf.Select(r => r.Name).ToArray()); // stable tie A→B
        Assert.Equal(new[] { 1, 2, 3 }, golf.Select(r => r.Rank).ToArray());

        var points = ScoreResultBuilder.Build(rows, golfRules: false);
        Assert.Equal(new[] { "A", "B", "C" }, points.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void ScoreResultBuilder_FormatTime_IsMmSsCs()
    {
        Assert.Equal("01:24:30", ScoreResultBuilder.FormatTime(84.3f));
        Assert.Equal("00:00:00", ScoreResultBuilder.FormatTime(0f));
    }

    // ── Composite scoring ───────────────────────────────────────────────────

    [Fact]
    public void CompositeScoringMode_SumsChildModes_OnTopOfCurrentScore()
    {
        var composite = new CompositeScoringMode(new BaseScoringMode[]
        {
            new StubScoringMode(calculate: 2f, endTurn: 10f),
            new StubScoringMode(calculate: 3f, endTurn: 20f),
        });

        Assert.Equal(2, composite.GetScoringModeCount());
        Assert.Equal(105f, composite.CalculateScore("any", 100f, 0f));
        Assert.Equal(130f, composite.EndTurnScore("any", 100f, 0f));
    }

    [Fact]
    public void CompositeScoringMode_AddRemoveContains_GuardsDuplicatesAndNulls()
    {
        var composite = new CompositeScoringMode(Array.Empty<BaseScoringMode>());
        var mode = new StubScoringMode(1f, 1f);

        Assert.Equal(7f, composite.CalculateScore("any", 7f, 0f)); // empty → passthrough

        composite.AddScoringMode(mode);
        composite.AddScoringMode(mode); // duplicate ignored
        composite.AddScoringMode(null); // null ignored
        Assert.Equal(1, composite.GetScoringModeCount());
        Assert.True(composite.ContainsScoringMode(mode));

        composite.RemoveScoringMode(mode);
        Assert.Equal(0, composite.GetScoringModeCount());
        Assert.False(composite.ContainsScoringMode(mode));
    }

    // ── BaseScoring listeners (RoundStats-event-driven) ─────────────────────

    [Fact]
    public void FriendlyVolumeDestroyedScoring_PenalizesAndRecomputesTotal_UntilUnsubscribed()
    {
        var gameData = MakeGameData();
        var stats = MakeStats("A", Domains.Jade);
        gameData.RoundStatsList.Add(stats);
        var tracker = new StubScoreTracker();
        var scoring = new FriendlyVolumeDestroyedScoring(tracker, gameData, scoreMultiplier: 10f);

        scoring.Subscribe();
        stats.FriendlyVolumeDestroyed = 2f;

        Assert.Equal(-20f, scoring.Score); // penalty: negative
        Assert.Equal(new[] { "A" }, tracker.Calls.ToArray());

        scoring.Unsubscribe();
        stats.FriendlyVolumeDestroyed = 5f;
        Assert.Equal(-20f, scoring.Score); // no longer listening
        Assert.Single(tracker.Calls);
    }

    [Fact]
    public void LifeFormsKilledScoring_SubscribeResetsCount_AndExposesMultiplier()
    {
        var gameData = MakeGameData();
        var scoring = new LifeFormsKilledScoring(new StubScoreTracker(), gameData, multiplier: 25f);

        scoring.Subscribe();

        Assert.Equal(0, scoring.GetTotalLifeFormsKilled());
        Assert.Equal(25f, scoring.ScorePerKill);
        Assert.Equal(0f, scoring.Score);
        scoring.Unsubscribe();
    }

    // ── TimePlayedScoring (GameTask loop) ───────────────────────────────────

    [Fact]
    public void TimePlayedScoring_Offline_AccumulatesElapsedGameTime_ForEveryPlayer()
    {
        using var loop = new GameLoop();
        NetworkManager.Singleton = null;

        var gameData = MakeGameData();
        var s1 = MakeStats("A", Domains.Jade);
        var s2 = MakeStats("B", Domains.Ruby);
        gameData.RoundStatsList.Add(s1);
        gameData.RoundStatsList.Add(s2);

        var scoring = new TimePlayedScoring(new StubScoreTracker(), gameData, scoreMultiplier: 2f, intervalSeconds: 0.25f);
        scoring.Subscribe();
        loop.Run(70, 1f / 60f); // ~1.17s of game time → ticks at 0.25/0.5/0.75/1.0
        scoring.Unsubscribe();

        Assert.InRange(s1.Score, 1.8f, 2.2f); // ≈ 1.0s elapsed × 2
        Assert.Equal(s1.Score, s2.Score); // time accrues identically for every player

        var frozen = s1.Score;
        loop.Run(30, 1f / 60f); // cancelled loop must not keep scoring
        Assert.Equal(frozen, s1.Score);
    }

    [Fact]
    public void TimePlayedScoring_Networked_UsesServerClockDelta()
    {
        using var loop = new GameLoop();
        try
        {
            var nm = new GameObject("nm").AddComponent<NetworkManager>();
            nm.ServerTime = new NetworkTime(100.0);
            NetworkManager.Singleton = nm;

            var gameData = ScriptableObject.CreateInstance<GameDataSO>();
            var stats = MakeStats("A", Domains.Jade);
            gameData.RoundStatsList.Add(stats);

            var scoring = new TimePlayedScoring(new StubScoreTracker(), gameData, scoreMultiplier: 2f, intervalSeconds: 0.25f);
            scoring.Subscribe(); // start = server 100.0; first (synchronous) tick sees dt 0
            nm.ServerTime = new NetworkTime(103.0); // server clock advances 3s
            loop.Run(20, 1f / 60f); // one 0.25s interval elapses → reads server delta
            scoring.Unsubscribe();

            Assert.Equal(6f, stats.Score, 3); // 3s × 2 — from the server clock, not Time
        }
        finally
        {
            NetworkManager.Singleton = null;
        }
    }

    // ── Restored GameDataSO surface ─────────────────────────────────────────

    [Fact]
    public void GameDataSO_ScoringRule_IsPublishable_AndSurvivesRuntimeResets()
    {
        var gameData = MakeGameData();
        var rule = MakeHexRule();

        gameData.ScoringRule = rule;
        gameData.ResetRuntimeData();

        // Transient but intentionally NOT cleared by resets — re-published on every (re)spawn.
        Assert.Same(rule, gameData.ScoringRule);
    }

    [Fact]
    public void GameDataSO_ScoringRule_DrivesTurnMonitorShapedQuery()
    {
        var gameData = MakeGameData();
        gameData.CrystalTargetCount = 5;
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 5));
        gameData.ScoringRule = MakeHexRule();

        // The NetworkCrystalCollisionTurnMonitor call shape per CLAUDE.md.
        Assert.True(gameData.ScoringRule.IsObjectiveReached(gameData, out _));
    }

    [Fact]
    public void SyncFromArcadeGame_CopiesIdentityFields()
    {
        var gameData = MakeGameData();
        var game = ScriptableObject.CreateInstance<SO_ArcadeGame>();
        game.SceneName = "MinigameHexRace";
        game.Mode = GameModes.HexRace;
        game.IsMultiplayer = true;

        gameData.SyncFromArcadeGame(game);

        Assert.Equal("MinigameHexRace", gameData.SceneName);
        Assert.Equal(GameModes.HexRace, gameData.GameMode);
        Assert.True(gameData.IsMultiplayerMode);
    }

    [Fact]
    public void SyncFromArcadeGame_NullGame_LogsAndLeavesFieldsUntouched()
    {
        var gameData = MakeGameData();
        gameData.SceneName = "Menu_Main";
        gameData.GameMode = GameModes.Random;
        gameData.IsMultiplayerMode = false;

        gameData.SyncFromArcadeGame(null);

        Assert.Equal("Menu_Main", gameData.SceneName);
        Assert.Equal(GameModes.Random, gameData.GameMode);
        Assert.False(gameData.IsMultiplayerMode);
    }

    // ── Arcade SO chain ─────────────────────────────────────────────────────

    [Fact]
    public void SO_ArcadeGame_Defaults_MatchUnitySerializedDefaults()
    {
        var game = ScriptableObject.CreateInstance<SO_ArcadeGame>();

        Assert.Equal(1, game.MinPlayersAllowed);
        Assert.Equal(2, game.MaxPlayersAllowed);
        Assert.Equal(1, game.MinIntensity);
        Assert.Equal(4, game.MaxIntensity);
        Assert.False(game.IsMultiplayer);
        Assert.False(game.GolfScoring);
        Assert.IsAssignableFrom<SO_Game>(game); // chain: SO_ArcadeGame : SO_Game : ScriptableObject
    }

    [Fact]
    public void SO_Vessel_DefaultsAndUnlockToggle()
    {
        var vessel = ScriptableObject.CreateInstance<SO_Vessel>();

        Assert.Equal(100, vessel.UnlockCost);
        Assert.False(vessel.IsLocked);
        Assert.Equal("Casual", vessel.gameplayParameter1.LeftHandLabel);
        Assert.Equal("Challenging", vessel.gameplayParameter1.RightHandLabel);
        Assert.Equal(0.5f, vessel.gameplayParameter3.Value);
        Assert.Equal("Social", vessel.gameplayParameter3.RightHandLabel);

        vessel.Lock();
        Assert.True(vessel.IsLocked);
        vessel.Unlock();
        Assert.False(vessel.IsLocked);
    }

    [Fact]
    public void SO_ArcadeGame_CanCarryVesselRoster()
    {
        var game = ScriptableObject.CreateInstance<SO_ArcadeGame>();
        var vessel = ScriptableObject.CreateInstance<SO_Vessel>();
        vessel.Class = VesselClassType.Squirrel;
        game.Vessels = new List<SO_Vessel> { vessel };

        Assert.Single(game.Vessels);
        Assert.Equal(VesselClassType.Squirrel, game.Vessels[0].Class);
    }

    [Fact]
    public void SO_Element_GetIcon_ValidatesLevelRange()
    {
        var element = ScriptableObject.CreateInstance<SO_Element>();

        Assert.Throws<ArgumentException>(() => element.GetIcon(-1, true));
        Assert.Throws<ArgumentException>(() => element.GetIcon(6, false));
    }
}
