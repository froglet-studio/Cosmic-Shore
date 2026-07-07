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
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// AstroLeague arc — hypersea soccer on the ported controller chain.
//
// Unit layers: AstroLeagueScoringRuleSO (mercy rule over per-domain GoalsScored
// sums, Score = personal goals, winning-domain-first results),
// AstroLeagueMatchMonitor (server clock → OnClockExpired, pause, overtime,
// ForceEnd), and the ball's ballistic core (engine E18 rigidbody integration +
// the spherical-boundary radial reflect) — seeded, bit-identical across runs.
//
// Integration layer: AstroLeagueRound (CosmicShore.Cli) runs the WHOLE match
// through the real chain — ready → countdown → kickoff parking → trigger-pass
// vessel strikes → goal-plane detection → RoundStats.GoalsScored → celebrations
// → mercy/full-time/golden-goal → SyncFinalScores → GameDataSO.Results — and
// these tests assert the milestone exit criteria on its result.
//
// Rounds run sequentially (assembly-wide parallelization is disabled — the
// GameLoop/Time are process-global), and every NetworkBehaviour spawned in a
// test is despawned before its loop is disposed (async-void discipline).
// ─────────────────────────────────────────────────────────────────────────────
public class AstroLeagueTests
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

    static GameDataSO MakeGameData()
    {
        NetworkManager.Singleton = null;
        return ScriptableObject.CreateInstance<GameDataSO>();
    }

    static RoundStats MakeStats(string name, Domains domain, int goals)
        => new RoundStats { Name = name, Domain = domain, GoalsScored = goals };

    static AstroLeagueScoringRuleSO MakeRule()
    {
        var rule = ScriptableObject.CreateInstance<AstroLeagueScoringRuleSO>();
        SetField(rule, "metric", ScoringMetric.Goals);
        SetField(rule, "golfRules", false);
        return rule;
    }

    // ── AstroLeagueScoringRuleSO ────────────────────────────────────────────

    [Fact]
    public void ScoringRule_MercyRule_FiresOnDomainGoalSum_NotIndividuals()
    {
        var gameData = MakeGameData();
        gameData.RequestedDomainCount = 2;
        gameData.GoalTargetCount = 3;
        var rule = MakeRule();

        // 1 + 1 Jade vs 2 Ruby — nobody at 3 yet.
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, 1));
        gameData.RoundStatsList.Add(MakeStats("B", Domains.Jade, 1));
        gameData.RoundStatsList.Add(MakeStats("C", Domains.Ruby, 2));
        Assert.False(rule.IsObjectiveReached(gameData, out var winner));
        Assert.Equal(Domains.Blue, winner);

        // Jade's TEAM sum reaches 3 (2+1) — mercy fires for the domain, no individual has 3.
        gameData.RoundStatsList[0].GoalsScored = 2;
        Assert.True(rule.IsObjectiveReached(gameData, out winner));
        Assert.Equal(Domains.Jade, winner);
    }

    [Fact]
    public void ScoringRule_ZeroTarget_NeverFires()
    {
        var gameData = MakeGameData();
        gameData.GoalTargetCount = 0; // no mercy rule configured
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, 50));
        Assert.False(MakeRule().IsObjectiveReached(gameData, out _));
    }

    [Fact]
    public void ScoringRule_AssignScores_ScoreIsPersonalGoals_AndResultsRankWinnersFirst()
    {
        var gameData = MakeGameData();
        gameData.RequestedDomainCount = 2;
        var rule = MakeRule();

        var a = MakeStats("A", Domains.Jade, 0);
        var b = MakeStats("B", Domains.Jade, 1);
        var c = MakeStats("C", Domains.Ruby, 2);
        gameData.RoundStatsList.Add(a);
        gameData.RoundStatsList.Add(b);
        gameData.RoundStatsList.Add(c);

        // Ruby wins 2–1 despite Jade's B having a goal too.
        Assert.Equal(Domains.Ruby, rule.ResolveWinner(gameData));
        gameData.WinnerDomain = Domains.Ruby;

        rule.AssignScores(gameData, Domains.Ruby, 0f);
        Assert.Equal(0f, a.Score);
        Assert.Equal(1f, b.Score);
        Assert.Equal(2f, c.Score);

        // Winning-domain players rank above losers; ranks are 1..N.
        var results = rule.BuildResults(gameData);
        Assert.Equal(new[] { 1, 2, 3 }, results.Select(r => r.Rank).ToArray());
        Assert.Equal("C", results[0].Name);
        Assert.Equal(Domains.Ruby, results[0].Domain);
        Assert.Equal("2 Goals", results[0].ScoreText);
        Assert.Equal("1 Goal", results[1].ScoreText); // singular form
    }

    [Fact]
    public void ScoringRule_Reveal_ReportsDomainGoalDelta()
    {
        var gameData = MakeGameData();
        gameData.RequestedDomainCount = 2;
        gameData.WinnerDomain = Domains.Ruby;
        var local = MakeStats("C", Domains.Ruby, 2);
        local.Score = 2f;
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, 1));
        gameData.RoundStatsList.Add(local);

        var reveal = MakeRule().BuildReveal(gameData, local, didWin: true);
        Assert.Equal("VICTORY", reveal.Header);
        Assert.Equal("WON BY 1 GOAL", reveal.Label);
    }

    // ── AstroLeagueMatchMonitor (server clock) ──────────────────────────────

    [Fact]
    public void MatchMonitor_ClockExpires_Once_PausesHoldIt_AndForceEndEndsTheTurn()
    {
        using var loop = new GameLoop(nameof(MatchMonitor_ClockExpires_Once_PausesHoldIt_AndForceEndEndsTheTurn));
        NetworkManager.Singleton = null;
        var gameData = MakeGameData();

        var displays = new List<string>();
        var displayChannel = ScriptableObject.CreateInstance<ScriptableEventString>();
        displayChannel.OnRaised += displays.Add;

        var go = new GameObject("match-monitor");
        var monitor = go.AddComponent<AstroLeagueMatchMonitor>();
        SetField(monitor, "gameData", gameData);
        SetField(monitor, "onUpdateTurnMonitorDisplay", displayChannel);

        int expired = 0;
        monitor.OnClockExpired += () => expired++;

        try
        {
            monitor.Spawn(); // IsServer=true — the authoritative clock
            monitor.ConfigureDuration(3f);
            monitor.StartMonitor(); // first RestrictedUpdate runs synchronously (loop's first step)

            // Paused clock: the loop keeps polling but regulation time does not tick down.
            monitor.SetClockPaused(true);
            float pausedRemaining = monitor.RemainingSeconds;
            loop.Run(150, Dt); // 2.5s paused
            Assert.Equal(0, expired);
            Assert.Equal(pausedRemaining, monitor.RemainingSeconds);

            monitor.SetClockPaused(false);
            loop.Run(280, Dt); // > 3s of live clock at the 1s RestrictedUpdate cadence
            Assert.Equal(1, expired);           // fires exactly once
            Assert.False(monitor.CheckForEndOfTurn()); // expiry does NOT end the turn by itself

            Assert.Contains("0:00", displays.Last()); // "M:SS" readout pushed on the shared channel

            monitor.EnterOvertime();
            Assert.Equal("OT", displays.Last());
            loop.Run(120, Dt);
            Assert.Equal(1, expired); // overtime clock is frozen — no re-expiry

            // Only the controller's ForceEnd ends the turn (TurnMonitorController polls this).
            monitor.ForceEnd();
            Assert.True(monitor.CheckForEndOfTurn());
        }
        finally
        {
            monitor.StopMonitor();
            monitor.Despawn();
            loop.Tick(Dt);
        }
    }

    // ── ball physics: seeded determinism + boundary containment ─────────────

    static AstroLeagueBall MakeBall(AstroLeagueSettingsSO settings, out Rigidbody rb)
    {
        var go = new GameObject("ball");
        go.SetActive(false);
        go.transform.localScale = Vector3.one * 14f; // world radius 7 (collider r = 0.5)
        rb = go.AddComponent<Rigidbody>();
        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.5f;
        var ball = go.AddComponent<AstroLeagueBall>();
        SetField(ball, "settings", settings);
        go.SetActive(true);
        ball.Spawn();
        return ball;
    }

    static List<Vector3> RunBallTrace(int frames)
    {
        using var loop = new GameLoop("ball-trace");
        NetworkManager.Singleton = null;
        typeof(Singleton<PrismSpatialIndex>).GetProperty("Instance")!.SetValue(null, null);
        CosmicShore.Engine.Random.InitState(1234);

        var settings = ScriptableObject.CreateInstance<AstroLeagueSettingsSO>();
        var ball = MakeBall(settings, out var rb);
        var trace = new List<Vector3>();
        try
        {
            ball.SetBoundary(new AstroLeagueBoundary(
                AstroLeagueBoundaryShape.Sphere, Vector3.zero, new Vector3(120f, 120f, 120f), 120f));
            ball.SetFrozenServer(false);
            rb.linearVelocity = new Vector3(37f, 11f, 53f);
            rb.angularVelocity = new Vector3(0.5f, 2f, -1f);

            for (int i = 0; i < frames; i++)
            {
                loop.Tick(Dt);
                trace.Add(ball.transform.position);
            }
        }
        finally
        {
            ball.Despawn();
            loop.Tick(Dt);
        }
        return trace;
    }

    [Fact]
    public void Ball_SeededTrace_IsDeterministic_AndStaysInsideTheBoundary()
    {
        var first = RunBallTrace(400);
        var second = RunBallTrace(400);

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
            Assert.Equal(first[i], second[i]); // bit-identical replay — same seed, same trace

        // The radial reflect contains the ball: the containment pass runs in the ball's
        // FixedUpdate BEFORE the step's integration (the original's FixedUpdate-then-solver
        // order), so between passes the center can overshoot by up to one step of travel.
        float speed = new Vector3(37f, 11f, 53f).magnitude;
        float maxCenterDist = 120f - 7f + speed * Time.fixedDeltaTime + 1e-2f;
        foreach (var p in first)
            Assert.True(p.magnitude <= maxCenterDist, $"ball escaped the boundary: |{p}| = {p.magnitude}");

        // And it actually bounced (came back inward) rather than sticking to the wall.
        Assert.Contains(first, p => p.magnitude < 60f);
        Assert.True(first[^1] != first[0], "ball must coast (zero friction, no decay)");
    }

    [Fact]
    public void Ball_FrozenAndHidden_DoNotMove()
    {
        using var loop = new GameLoop(nameof(Ball_FrozenAndHidden_DoNotMove));
        NetworkManager.Singleton = null;
        var settings = ScriptableObject.CreateInstance<AstroLeagueSettingsSO>();
        var ball = MakeBall(settings, out var rb);
        try
        {
            ball.SetBoundary(new AstroLeagueBoundary(
                AstroLeagueBoundaryShape.Sphere, Vector3.zero, new Vector3(120f, 120f, 120f), 120f));
            // Frozen (kickoff count-in): kinematic — velocity writes don't move it.
            Assert.True(ball.IsFrozen);
            rb.linearVelocity = new Vector3(50f, 0f, 0f);
            loop.Run(30, Dt);
            Assert.Equal(Vector3.zero, ball.transform.position);

            // Reset contract: visible, frozen, neutral color again.
            ball.ResetToCenterServer();
            Assert.False(ball.IsHidden);
            Assert.Equal(Domains.Blue, ball.LastHitDomain);
        }
        finally
        {
            ball.Despawn();
            loop.Tick(Dt);
        }
    }

    // ── the full match through the real chain (CosmicShore.Cli round) ────────

    static AstroLeagueRoundResult RunMatch(int players, int seed)
        => AstroLeagueRound.Run(new AstroLeagueRoundOptions { PlayerCount = players, Seed = seed });

    [Fact]
    public void Match_Completes_GoalsLandOnRoundStats_AndResultsRankWinningDomainFirst()
    {
        var result = RunMatch(players: 4, seed: 42);

        Assert.True(result.Finished, "match must finish through the monitor/mercy/golden-goal flow");
        Assert.Empty(result.EngineErrors); // fail loud on any Error/Exception logged mid-match
        Assert.NotEqual(Domains.Blue, result.WinnerDomain);
        Assert.False(string.IsNullOrEmpty(result.WinnerName));
        Assert.True(result.TotalStrikes > 0, "vessel strikes must flow through the trigger pipeline");

        // Standings mirror RoundStats.GoalsScored; the domain sums match the reported score.
        Assert.Equal(4, result.Standings.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Standings.Select(s => s.Rank).ToArray());
        int jade = result.Standings.Where(s => s.Domain == Domains.Jade).Sum(s => s.Goals);
        int ruby = result.Standings.Where(s => s.Domain == Domains.Ruby).Sum(s => s.Goals);
        Assert.Equal(result.JadeGoals, jade);
        Assert.Equal(result.RubyGoals, ruby);
        Assert.True(jade != ruby, "golden goal / mercy rule guarantee an untied result");

        // Winning-domain rows rank above the losers (points ordering, not golf).
        var winnerRows = result.Standings.TakeWhile(s => s.Domain == result.WinnerDomain).Count();
        Assert.True(winnerRows >= 2, "2v2: both winning-domain players rank above the losers");
        Assert.Equal(result.WinnerName, result.Standings[0].Name);

        // A goal was actually scored through RoundStats (never a scoreless 'winner').
        Assert.True(Math.Max(jade, ruby) >= 1);
    }

    [Fact]
    public void Match_TiedRegulation_GoesToGoldenGoal_AndSuddenDeathDecidesIt()
    {
        // Seed chosen for a tied regulation (validated sweep; re-swept 2026-07-07 after the
        // court-boundary drift changed ball trajectories) — full time ties, the monitor
        // reports expiry, the controller enters overtime, and the first overtime goal ends
        // the match immediately (sudden death).
        var result = RunMatch(players: 4, seed: 2);

        Assert.True(result.Finished);
        Assert.Empty(result.EngineErrors);
        Assert.True(result.WentToOvertime, "expected the golden-goal path for this seed");
        Assert.Equal(1, Math.Abs(result.JadeGoals - result.RubyGoals)); // sudden death: one-goal margin
        Assert.Contains(result.Transcript, line => line.Contains("OVERTIME"));
    }

    [Fact]
    public void Match_IsAPureFunctionOfItsSeed()
    {
        var first = RunMatch(players: 4, seed: 42);
        var second = RunMatch(players: 4, seed: 42);

        Assert.Equal(first.Transcript, second.Transcript); // identical play-by-play
        Assert.Equal(first.WinnerDomain, second.WinnerDomain);
        Assert.Equal(first.FramesSimulated, second.FramesSimulated);
        Assert.Equal(first.TotalStrikes, second.TotalStrikes);

        var third = RunMatch(players: 4, seed: 99);
        Assert.NotEqual(first.Transcript, third.Transcript); // the seed is load-bearing
    }
}
