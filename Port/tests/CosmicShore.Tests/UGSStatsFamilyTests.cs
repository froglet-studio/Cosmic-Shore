using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Services.Leaderboards;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Stats-reporter family (2026-07-10) — the REAL UGSStatsManager (replacing the
// 14L shell) + the reporters over it. Covers: the readiness lane (profiles
// cached from the UGSDataService repos); ReportJoustStats (finish time updates
// the per-key best AND submits to the leaderboard through the LeaderboardConfigSO
// mapping + the engine's local Leaderboards store; a DNF-sentinel score records
// nothing); GetEvaluatedHighScore's golf-min vs high-score-max lanes (a DNF
// session falls back to the cloud best); ReportVesselTelemetry rolling Squirrel
// telemetry into per-vessel lifetime stats (best-keeping + counters);
// TrackPlayAgain reaching the analytics facade; and JoustStatsReporter
// end-to-end — OnMiniGameEnd reports ONLY when the local player is the winner.
// ─────────────────────────────────────────────────────────────────────────────

public class UGSStatsFamilyTests : IDisposable
{
    readonly GameLoop loop = new(nameof(UGSStatsFamilyTests));

    readonly UGSDataService ugs;
    readonly UGSStatsManager manager;
    readonly AnalyticsServiceFacade analytics = new();
    readonly LocalLeaderboardsService leaderboards;
    readonly GameDataSO gameData;

    public UGSStatsFamilyTests()
    {
        ClearStatics();
        leaderboards = LeaderboardsService.Reset();

        // A live UGSDataService whose repos exist (Awake) — readiness flipped by
        // reflection so the manager's Start takes the immediate ready path
        // (the GameModeProgressionServiceTests rig).
        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        var vesselList = ScriptableObject.CreateInstance<SO_VesselList>();
        vesselList.VesselList = new List<SO_Vessel>();
        var ugsGo = new GameObject("ugs-data-service");
        ugsGo.SetActive(false);
        ugs = ugsGo.AddComponent<UGSDataService>();
        Set(ugs, "vesselList", vesselList);
        Set(ugs, "_authData", authVar);
        ugsGo.SetActive(true);
        typeof(UGSDataService).GetProperty("IsInitialized")!.SetValue(ugs, true);

        var config = ScriptableObject.CreateInstance<LeaderboardConfigSO>();
        Set(config, "leaderboardMappings", new List<LeaderboardConfigSO.LeaderboardMapping>
        {
            new() { GameMode = GameModes.MultiplayerJoust, Intensity = 2, LeaderboardId = "joust-i2" },
            new() { GameMode = GameModes.WildlifeBlitz, Intensity = 1, LeaderboardId = "blitz-i1" },
        });

        var managerGo = new GameObject("UGSStatsManager");
        managerGo.SetActive(false);
        manager = managerGo.AddComponent<UGSStatsManager>();
        Set(manager, "leaderboardConfig", config);
        Set(manager, "_ugsDataService", ugs);
        Set(manager, "_analytics", analytics);
        managerGo.SetActive(true); // Awake: Instance
        loop.Tick(1f / 60f);       // Start: ready path → profiles cached from repos

        gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnMiniGameEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
        gameData.SelectedIntensity.Value = 2;
    }

    public void Dispose()
    {
        ClearStatics();
        LeaderboardsService.Reset();
        loop.Dispose();
    }

    static void ClearStatics()
        => typeof(UGSStatsManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);

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

    PlayerStatsProfile Profile => ugs.StatsRepo.Data;

    [Fact]
    public void ReportJoustStats_FinishTime_UpdatesBestAndSubmits_DnfRecordsNothing()
    {
        manager.ReportJoustStats(GameModes.MultiplayerJoust, 2, joustsWon: 3, raceTime: 42.5f);

        Assert.Equal(42.5f, Profile.JoustStats.BestRaceTimes["MultiplayerJoust_2"]);
        var submission = Assert.Single(leaderboards.Submissions);
        Assert.Equal("joust-i2", submission.LeaderboardId);
        Assert.Equal(42.5, submission.Score, 3);

        // A slower finish doesn't regress the best (golf rules), but still submits.
        manager.ReportJoustStats(GameModes.MultiplayerJoust, 2, joustsWon: 1, raceTime: 60f);
        Assert.Equal(42.5f, Profile.JoustStats.BestRaceTimes["MultiplayerJoust_2"]);
        Assert.Equal(2, leaderboards.Submissions.Count);

        // DNF sentinel: neither best nor leaderboard moves.
        manager.ReportJoustStats(GameModes.MultiplayerJoust, 2, joustsWon: 0,
            raceTime: GolfScoreSentinels.DnfThreshold + 5f);
        Assert.Equal(42.5f, Profile.JoustStats.BestRaceTimes["MultiplayerJoust_2"]);
        Assert.Equal(2, leaderboards.Submissions.Count);
    }

    [Fact]
    public void GetEvaluatedHighScore_GolfTakesMin_DnfFallsBackToCloud_BlitzTakesMax()
    {
        Profile.JoustStats.BestRaceTimes["MultiplayerJoust_2"] = 40f;
        Assert.Equal(35f, manager.GetEvaluatedHighScore(GameModes.MultiplayerJoust, 2, 35f)); // session beats cloud
        Assert.Equal(40f, manager.GetEvaluatedHighScore(GameModes.MultiplayerJoust, 2, 55f)); // cloud best wins
        Assert.Equal(40f, manager.GetEvaluatedHighScore(GameModes.MultiplayerJoust, 2,
            GolfScoreSentinels.DnfThreshold + 3f));                                            // DNF → cloud best

        Profile.BlitzStats.HighScores["WildlifeBlitz_1"] = 100;
        Assert.Equal(150f, manager.GetEvaluatedHighScore(GameModes.WildlifeBlitz, 1, 150f));   // high score: max
        Assert.Equal(100f, manager.GetEvaluatedHighScore(GameModes.WildlifeBlitz, 1, 80f));
    }

    [Fact]
    public void ReportVesselTelemetry_RollsSquirrelStatsIntoLifetimeBests()
    {
        var telemetryGo = new GameObject("telemetry");
        telemetryGo.SetActive(false); // component only — the effect-event lanes are carried deviations
        var telemetry = telemetryGo.AddComponent<SquirrelVesselTelemetry>();
        typeof(VesselTelemetry).GetProperty("MaxDriftTime")!.SetValue(telemetry, 7.5f);
        typeof(VesselTelemetry).GetProperty("MaxBoostTime")!.SetValue(telemetry, 3.25f);
        typeof(SquirrelVesselTelemetry).GetProperty("JoustsWon")!.SetValue(telemetry, 4);

        manager.ReportVesselTelemetry(telemetry, "Squirrel");

        var stats = ugs.VesselStatsRepo.Data.GetOrCreate("Squirrel");
        Assert.Equal(1, stats.GamesPlayed);
        Assert.Equal(7.5f, stats.BestDriftTime);
        Assert.Equal(3.25f, stats.BestBoostTime);
        Assert.Equal(4, stats.Counters["JoustsWon"]);

        // A weaker second game keeps the bests, accumulates the counter.
        typeof(VesselTelemetry).GetProperty("MaxDriftTime")!.SetValue(telemetry, 2f);
        manager.ReportVesselTelemetry(telemetry, "Squirrel");
        Assert.Equal(2, stats.GamesPlayed);
        Assert.Equal(7.5f, stats.BestDriftTime);
        Assert.Equal(8, stats.Counters["JoustsWon"]);
    }

    [Fact]
    public void TrackPlayAgain_ReachesTheAnalyticsFacade()
    {
        manager.TrackPlayAgain();
        manager.TrackPlayAgain();
        Assert.Equal(2, analytics.PlayAgainCount);
    }

    [Fact]
    public void JoustStatsReporter_ReportsOnGameEnd_OnlyWhenLocalPlayerWins()
    {
        var winner = new RoundStats { Name = "ToyPilot", Domain = Domains.Jade, Score = 33f, JoustCollisions = 5 };
        gameData.RoundStatsList.Add(winner);
        var player = new ToyStubPlayer(); // Name => "ToyPilot" 
        typeof(GameDataSO).GetProperty("LocalPlayer")!.SetValue(gameData, player);

        var reporterGo = new GameObject("joust-reporter");
        reporterGo.SetActive(false);
        var reporter = reporterGo.AddComponent<JoustStatsReporter>();
        Set(reporter, "gameData", gameData);
        Set(reporter, "ugsStatsManager", manager);
        reporterGo.SetActive(true); // OnEnable: subscribe to OnMiniGameEnd

        // Someone else won: nothing reported.
        gameData.WinnerName = "AI-2";
        gameData.OnMiniGameEnd.Raise();
        Assert.Empty(leaderboards.Submissions);

        // Local player won: best time + leaderboard submission land.
        gameData.WinnerName = "ToyPilot";
        gameData.OnMiniGameEnd.Raise();
        Assert.Equal(33f, Profile.JoustStats.BestRaceTimes[$"MultiplayerJoust_{gameData.SelectedIntensity.Value}"]);
        Assert.Single(leaderboards.Submissions);
    }
}
