using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Random = CosmicShore.Engine.Random;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Tournament (Maelstrom) arc — the session-level meta chaining the domain
// minigames. Everything the architecture doc calls load-bearing is exercised
// VERBATIM through the real ported types:
//   • the network-free standings fold: every peer reduces the already-synced
//     GameDataSO.Results into per-domain {2,1,0} placement crystals identically,
//   • the race-to-N (default 6) / MaxGames-cap completion (IsShuffleComplete)
//     and the authoritative summary decision at the Maelstrom scene load
//     (EnterSummary — NOT the transient Complete phase),
//   • the TournamentStateMachine transition table,
//   • the host's randomized lineup draw (mode + intensity ∈ [1..ceiling]),
//     deterministic per seed, with no immediate mode repeat,
//   • the TournamentLobbyNetwork ready-up/countdown (auto-start, all-ready
//     snap, one-shot BeginNextRound).
//
// Scene transitions don't exist in the port yet, so loads are announced through
// the engine's SceneManager.NotifySceneLoaded port surface — the controller's
// sceneLoaded subscription and HandleSceneLoaded body are verbatim.
//
// Hygiene: TournamentController is an app-lifetime singleton with no unsubscribe,
// so every rig resets SceneManager's subscribers on construction AND disposal —
// no controller from one test can react to another test's scene loads. The
// lobby-network test Despawn()s its behaviour and disposes its GameLoop before
// returning (async-void discipline: no loop outlives its test).
// ─────────────────────────────────────────────────────────────────────────────
public class TournamentSystemTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    static ScoreResult Row(int rank, Domains domain, string name = null) =>
        new ScoreResult(rank, name ?? $"P{rank}", domain, rank, $"score-{rank}", null);

    static SO_ArcadeGame MakeCard(string displayName, string sceneName, GameModes mode)
    {
        var card = ScriptableObject.CreateInstance<SO_ArcadeGame>();
        card.DisplayName = displayName;
        card.SceneName = sceneName;
        card.Mode = mode;
        card.IsMultiplayer = true;
        return card;
    }

    /// <summary>
    /// One peer's tournament stack: GameDataSO + TournamentDataSO (+ the 3-card pool) +
    /// SceneNameListSO + the real TournamentController. Constructing the FIRST rig of a test
    /// clears the engine's sceneLoaded subscribers (stale controllers from earlier tests);
    /// additional rigs in the same test (cross-peer determinism) must pass
    /// <c>resetSceneSubscribers: false</c> so they coexist on the shared channel — exactly
    /// like real peers all observing the same Single loads.
    /// </summary>
    sealed class TournamentRig : IDisposable
    {
        public readonly GameDataSO GameData;
        public readonly TournamentDataSO Tournament;
        public readonly SceneNameListSO SceneNames;
        public readonly TournamentController Controller;

        public int StartedRaises, RecordedRaises, StandingsRaises, CompletedRaises, LaunchRaises;

        public TournamentRig(int winTarget = 6, int maxGames = 7, bool resetSceneSubscribers = true)
        {
            if (resetSceneSubscribers)
                SceneManager.ResetSceneLoadedSubscribers();
            NetworkManager.Singleton = null;   // offline / single-process → every rig is the host

            GameData = ScriptableObject.CreateInstance<GameDataSO>();
            GameData.OnMiniGameEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            GameData.OnLaunchGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            GameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
            GameData.SelectedIntensity.Value = 1;
            GameData.OnLaunchGame.OnRaised += () => LaunchRaises++;

            Tournament = ScriptableObject.CreateInstance<TournamentDataSO>();
            Tournament.WinTarget = winTarget;
            Tournament.MaxGames = maxGames;
            Tournament.GameQueue = new List<SO_ArcadeGame>
            {
                MakeCard("Skim Race", "MinigameHexRace", GameModes.HexRace),
                MakeCard("Joust", "MinigameJoust_Gameplay", GameModes.MultiplayerJoust),
                MakeCard("Crystal Capture", "MinigameCrystalCaptureMultiplayer_Gameplay", GameModes.MultiplayerCrystalCapture),
            };
            Tournament.OnTournamentStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            Tournament.OnGameResultRecorded = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            Tournament.OnStandingsChanged = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            Tournament.OnTournamentCompleted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            Tournament.OnTournamentStarted.OnRaised += () => StartedRaises++;
            Tournament.OnGameResultRecorded.OnRaised += () => RecordedRaises++;
            Tournament.OnStandingsChanged.OnRaised += () => StandingsRaises++;
            Tournament.OnTournamentCompleted.OnRaised += () => CompletedRaises++;

            SceneNames = ScriptableObject.CreateInstance<SceneNameListSO>();

            Controller = new TournamentController(GameData, Tournament, SceneNames);
        }

        /// <summary>Announce the Maelstrom (lobby/hub/summary) scene load to every subscribed peer.</summary>
        public void LoadLobbyScene() => SceneManager.NotifySceneLoaded(Tournament.LobbySceneName);

        /// <summary>This peer's copy of one finished game: synced Results land, then OnMiniGameEnd fires.</summary>
        public void EndGame(params ScoreResult[] rows)
        {
            GameData.SetResults(rows);
            GameData.InvokeMiniGameEnd();
        }

        public int Points(Domains d) => Tournament.Standings.Find(s => s.Domain == d)?.TotalPoints ?? 0;

        public void Dispose()
        {
            SceneManager.ResetSceneLoadedSubscribers();
            NetworkManager.Singleton = null;
        }
    }

    static readonly ScoreResult[] JadeRubyGold =
        { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) };
    static readonly ScoreResult[] RubyGoldJade =
        { Row(1, Domains.Ruby), Row(2, Domains.Gold), Row(3, Domains.Jade) };
    static readonly ScoreResult[] GoldJadeRuby =
        { Row(1, Domains.Gold), Row(2, Domains.Jade), Row(3, Domains.Ruby) };

    // ── state machine transition table ──────────────────────────────────────

    [Fact]
    public void StateMachine_WalksTheHappyPath_AndRejectsInvalidTransitions()
    {
        var sm = new TournamentStateMachine();
        var observed = new List<TournamentPhase>();
        sm.OnPhaseChanged += p => observed.Add(p);

        Assert.Equal(TournamentPhase.Idle, sm.Current);
        Assert.False(sm.TransitionTo(TournamentPhase.InGame), "Idle → InGame is not in the table.");
        Assert.False(sm.TransitionTo(TournamentPhase.Summary), "Idle → Summary is not in the table.");
        Assert.Equal(TournamentPhase.Idle, sm.Current);

        Assert.True(sm.TransitionTo(TournamentPhase.Lobby));
        Assert.False(sm.TransitionTo(TournamentPhase.Lobby), "No-op transition returns false without raising.");
        Assert.True(sm.TransitionTo(TournamentPhase.InGame));
        Assert.True(sm.TransitionTo(TournamentPhase.Complete));
        Assert.False(sm.TransitionTo(TournamentPhase.Lobby), "Complete → Lobby is not in the table.");
        Assert.True(sm.TransitionTo(TournamentPhase.Summary));
        Assert.True(sm.TransitionTo(TournamentPhase.InGame), "Summary → InGame (Play Again re-enters play).");

        sm.ResetToIdle();
        Assert.Equal(TournamentPhase.Idle, sm.Current);

        Assert.Equal(new[]
        {
            TournamentPhase.Lobby, TournamentPhase.InGame, TournamentPhase.Complete,
            TournamentPhase.Summary, TournamentPhase.InGame, TournamentPhase.Idle,
        }, observed);
    }

    [Fact]
    public void StateMachine_LobbyToComplete_IsValid_ForTheAuthoritativeSummaryRoute()
    {
        // The race-to-6 fix: a decided shuffle found at a HUB load must still reach the
        // summary — Lobby → Complete → Summary is the EnterSummary route.
        var sm = new TournamentStateMachine();
        Assert.True(sm.TransitionTo(TournamentPhase.Lobby));
        Assert.True(sm.TransitionTo(TournamentPhase.Complete));
        Assert.True(sm.TransitionTo(TournamentPhase.Summary));
    }

    // ── lobby load: fresh start, ceiling capture, flags ─────────────────────

    [Fact]
    public void LobbySceneLoad_FreshStart_ResetsStandings_CapturesCeiling_SetsFlags()
    {
        using var rig = new TournamentRig();
        rig.GameData.SelectedIntensity.Value = 3;

        rig.LoadLobbyScene();

        Assert.True(rig.Tournament.IsActive);
        Assert.True(rig.GameData.IsTournamentMode);
        Assert.Equal(TournamentPhase.Lobby, rig.Controller.Phase);
        Assert.Equal(3, rig.Tournament.IntensityCeiling);
        Assert.Equal(0, rig.Tournament.GamesPlayed);
        Assert.Empty(rig.Tournament.Standings);
        Assert.Equal(1, rig.StartedRaises);
        // No End Game Conditions asset registered → the serialized WinTarget is the resolved target.
        Assert.Equal(rig.Tournament.WinTarget, rig.Tournament.EffectiveWinTarget);
    }

    [Fact]
    public void MenuSceneLoad_EndsTheTournament_OnEveryPeer()
    {
        using var rig = new TournamentRig();
        rig.LoadLobbyScene();
        Assert.True(rig.Tournament.IsActive);

        SceneManager.NotifySceneLoaded(rig.SceneNames.MainMenuScene);

        Assert.False(rig.Tournament.IsActive);
        Assert.False(rig.GameData.IsTournamentMode);
        Assert.Equal(TournamentPhase.Idle, rig.Controller.Phase);
    }

    // ── the standings fold across a simulated 3-game sequence ───────────────

    [Fact]
    public void StandingsFold_ThreeGameSequence_AccumulatesPerDomainPoints_AndHistory()
    {
        using var rig = new TournamentRig();
        rig.LoadLobbyScene();

        // Game 1: draw + game-scene load + synced results + game end.
        rig.Controller.BeginNextRound();
        SceneManager.NotifySceneLoaded(rig.GameData.SceneName);
        Assert.Equal(TournamentPhase.InGame, rig.Controller.Phase);
        rig.EndGame(JadeRubyGold);                       // Jade 2 · Ruby 1 · Gold 0

        rig.Controller.AdvanceToNextGame();              // Continue → Maelstrom hub
        rig.LoadLobbyScene();
        Assert.Equal(TournamentPhase.Lobby, rig.Controller.Phase);

        // Game 2.
        rig.Controller.BeginNextRound();
        SceneManager.NotifySceneLoaded(rig.GameData.SceneName);
        rig.EndGame(RubyGoldJade);                       // Ruby 2 · Gold 1 · Jade 0
        rig.Controller.AdvanceToNextGame();
        rig.LoadLobbyScene();

        // Game 3.
        rig.Controller.BeginNextRound();
        SceneManager.NotifySceneLoaded(rig.GameData.SceneName);
        rig.EndGame(GoldJadeRuby);                       // Gold 2 · Jade 1 · Ruby 0

        Assert.Equal(3, rig.Tournament.GamesPlayed);
        Assert.Equal(3, rig.Tournament.History.Count);
        Assert.Equal(3, rig.RecordedRaises);
        Assert.Equal(3, rig.Points(Domains.Jade));       // 2 + 0 + 1
        Assert.Equal(3, rig.Points(Domains.Ruby));       // 1 + 2 + 0
        Assert.Equal(3, rig.Points(Domains.Gold));       // 0 + 1 + 2

        // All-tie sort: tiebreak best placement (all have a 1st) → enum order Jade→Ruby→Gold.
        var sorted = rig.Tournament.BuildSortedStandings();
        Assert.Equal(new[] { Domains.Jade, Domains.Ruby, Domains.Gold },
            sorted.Select(s => s.Domain).ToArray());

        // Per-round history carries the domain placement order + the ranked player snapshots.
        Assert.Equal(new[] { Domains.Jade, Domains.Ruby, Domains.Gold }, rig.Tournament.History[0].DomainOrder);
        Assert.Equal(new[] { Domains.Ruby, Domains.Gold, Domains.Jade }, rig.Tournament.History[1].DomainOrder);
        Assert.Equal(new[] { Domains.Gold, Domains.Jade, Domains.Ruby }, rig.Tournament.History[2].DomainOrder);
        Assert.Equal("P1", rig.Tournament.History[0].Players[0].Name);

        // 3 games, no domain at 6 → the shuffle is still live (hub, not summary, on the next load).
        Assert.False(rig.Tournament.IsShuffleComplete);
    }

    [Fact]
    public void StandingsFold_IsIdenticalAcrossPeers_FromTheSameSyncedResults()
    {
        // Two peers (host + client) each run their own controller over their own data
        // containers, subscribed to the same scene-load signals — the real topology.
        using var host = new TournamentRig();
        using var client = new TournamentRig(resetSceneSubscribers: false);

        host.LoadLobbyScene();   // one announcement → BOTH controllers start their tournament

        var games = new[] { JadeRubyGold, RubyGoldJade, JadeRubyGold };
        foreach (var results in games)
        {
            host.Controller.BeginNextRound();                     // only the host draws
            SceneManager.NotifySceneLoaded(host.GameData.SceneName); // both peers observe the load
            host.EndGame(results);                                // same synced rows land on each peer
            client.EndGame(results);
            host.Controller.AdvanceToNextGame();
            host.LoadLobbyScene();
        }

        foreach (var domain in GameDataSO.ActiveDomains)
        {
            Assert.Equal(host.Points(domain), client.Points(domain));
            var h = host.Tournament.Standings.Find(s => s.Domain == domain);
            var c = client.Tournament.Standings.Find(s => s.Domain == domain);
            Assert.Equal(h.Placements, c.Placements);
        }
        Assert.Equal(host.Tournament.GamesPlayed, client.Tournament.GamesPlayed);
        Assert.Equal(host.Tournament.IsShuffleComplete, client.Tournament.IsShuffleComplete);
        Assert.Equal(
            TournamentStandingsFormatter.FormatFinal(host.Tournament),
            TournamentStandingsFormatter.FormatFinal(client.Tournament));
    }

    // ── race to 6 / game cap → summary ──────────────────────────────────────

    [Fact]
    public void RaceToSix_ThreeStraightWins_CompletesTheShuffle_AndShowsTheSummary()
    {
        using var rig = new TournamentRig();   // WinTarget 6, {2,1,0} → 3 dominant finishes
        rig.LoadLobbyScene();

        for (int game = 1; game <= 3; game++)
        {
            rig.Controller.BeginNextRound();
            SceneManager.NotifySceneLoaded(rig.GameData.SceneName);
            rig.EndGame(JadeRubyGold);   // Jade +2 each game: 2, 4, 6

            bool decided = game == 3;
            Assert.Equal(decided, rig.Tournament.IsShuffleComplete);
            Assert.Equal(decided ? 1 : 0, rig.CompletedRaises);
            Assert.Equal(decided ? TournamentPhase.Complete : TournamentPhase.InGame, rig.Controller.Phase);

            rig.Controller.AdvanceToNextGame();   // ALWAYS returns to the Maelstrom scene
            rig.LoadLobbyScene();

            if (decided)
            {
                Assert.True(rig.Controller.IsShowingSummary, "Deciding game → the Maelstrom load is the SUMMARY.");
            }
            else
            {
                Assert.Equal(TournamentPhase.Lobby, rig.Controller.Phase);
                Assert.False(rig.Controller.IsShowingSummary, "Mid-run → the Maelstrom load is the standings HUB.");
            }
        }

        Assert.Equal(6, rig.Points(Domains.Jade));
        Assert.Equal(Domains.Jade, rig.Tournament.BuildSortedStandings()[0].Domain);
    }

    [Fact]
    public void GameCap_EndsTheShuffle_EvenWithNoDomainAtTheTarget()
    {
        using var rig = new TournamentRig(winTarget: 100, maxGames: 2);
        rig.LoadLobbyScene();

        for (int game = 1; game <= 2; game++)
        {
            rig.Controller.BeginNextRound();
            SceneManager.NotifySceneLoaded(rig.GameData.SceneName);
            rig.EndGame(game == 1 ? JadeRubyGold : RubyGoldJade);
            rig.Controller.AdvanceToNextGame();
            rig.LoadLobbyScene();
        }

        Assert.True(rig.Tournament.IsShuffleComplete, "MaxGames cap hit → shuffle over.");
        Assert.True(rig.Controller.IsShowingSummary);
    }

    [Fact]
    public void SummaryDecision_IsAuthoritative_NotPhaseDriven_AtTheMaelstromLoad()
    {
        // The race-to-6 regression guard: even when HandleMiniGameEnd never ran on this peer
        // (the Complete phase signal was missed entirely), a decided shuffle found at the
        // Maelstrom load MUST surface as the summary — never the hub.
        using var rig = new TournamentRig();
        rig.LoadLobbyScene();
        Assert.Equal(TournamentPhase.Lobby, rig.Controller.Phase);

        // Fold results straight into the data container (no OnMiniGameEnd → no Complete signal).
        rig.Tournament.RecordResults(JadeRubyGold);
        rig.Tournament.RecordResults(JadeRubyGold);
        rig.Tournament.RecordResults(JadeRubyGold);   // Jade 6 → decided
        Assert.True(rig.Tournament.IsShuffleComplete);
        Assert.Equal(TournamentPhase.Lobby, rig.Controller.Phase);

        rig.LoadLobbyScene();   // EnterSummary: Lobby → Complete → Summary

        Assert.True(rig.Controller.IsShowingSummary, "The win must not be swallowed into another hub visit.");
    }

    // ── restart (Play Again) + dwell window ─────────────────────────────────

    [Fact]
    public void PlayAgain_FromSummary_ResetsStandings_KeepsIntensityCeiling()
    {
        using var rig = new TournamentRig();
        rig.GameData.SelectedIntensity.Value = 4;   // ceiling captured at the fresh start
        rig.LoadLobbyScene();
        Assert.Equal(4, rig.Tournament.IntensityCeiling);

        for (int game = 1; game <= 3; game++)
        {
            rig.Controller.BeginNextRound();        // draws mutate SelectedIntensity per game
            SceneManager.NotifySceneLoaded(rig.GameData.SceneName);
            rig.EndGame(JadeRubyGold);
            rig.Controller.AdvanceToNextGame();
            rig.LoadLobbyScene();
        }
        Assert.True(rig.Controller.IsShowingSummary);

        int launchesBefore = rig.LaunchRaises;
        rig.Controller.RestartTournament();          // host Play Again → loads the Maelstrom scene
        Assert.Equal(launchesBefore + 1, rig.LaunchRaises);
        Assert.Equal(rig.Tournament.LobbySceneName, rig.GameData.SceneName);
        Assert.Equal(GameModes.Tournament, rig.GameData.GameMode);

        rig.LoadLobbyScene();                        // load lands while phase is Summary → RestartFromSummary

        Assert.Equal(TournamentPhase.Lobby, rig.Controller.Phase);
        Assert.True(rig.Tournament.IsActive);
        Assert.Equal(0, rig.Tournament.GamesPlayed);
        Assert.Empty(rig.Tournament.Standings);
        Assert.True(rig.Tournament.IntensityCeiling == 4,
            "Play Again must NOT re-capture the (per-game-rolled) SelectedIntensity as the ceiling.");
    }

    [Fact]
    public void MinLoadSplashDwell_AppliesOnlyMidRun()
    {
        using var rig = new TournamentRig();
        Assert.Equal(0f, rig.Controller.MinLoadSplashDwellSeconds);   // inactive

        rig.LoadLobbyScene();
        Assert.Equal(0f, rig.Controller.MinLoadSplashDwellSeconds);   // first game — never delayed

        rig.Controller.BeginNextRound();
        SceneManager.NotifySceneLoaded(rig.GameData.SceneName);
        rig.EndGame(JadeRubyGold);
        Assert.Equal(rig.Tournament.BetweenGameSummaryDwellSeconds,
            rig.Controller.MinLoadSplashDwellSeconds);                // mid-run → readable dwell

        rig.EndGame(JadeRubyGold);
        rig.EndGame(JadeRubyGold);                                    // Jade 6 → decided
        Assert.Equal(0f, rig.Controller.MinLoadSplashDwellSeconds);   // load into the summary — never delayed
    }

    // ── randomized lineup: seed determinism + repeat avoidance ──────────────

    static List<(string Scene, int Intensity)> DrawSequence(int seed, int draws, int ceiling)
    {
        using var rig = new TournamentRig(winTarget: 1000, maxGames: 1000);
        rig.GameData.SelectedIntensity.Value = ceiling;
        rig.LoadLobbyScene();

        Random.InitState(seed);
        var sequence = new List<(string, int)>();
        for (int i = 0; i < draws; i++)
        {
            rig.Controller.BeginNextRound();
            sequence.Add((rig.GameData.SceneName, rig.GameData.SelectedIntensity.Value));
            SceneManager.NotifySceneLoaded(rig.GameData.SceneName);   // marks CurrentGameIndex (repeat-avoid)
            rig.EndGame(JadeRubyGold);                                 // GamesPlayed++ → avoidance arms
            rig.Controller.AdvanceToNextGame();
            rig.LoadLobbyScene();
        }
        return sequence;
    }

    [Theory]
    [InlineData(42, 2)]
    [InlineData(7, 4)]
    [InlineData(2026, 1)]
    public void RandomizedLineup_IsDeterministicPerSeed_AndNeverImmediatelyRepeats(int seed, int ceiling)
    {
        const int draws = 10;
        var first = DrawSequence(seed, draws, ceiling);
        var second = DrawSequence(seed, draws, ceiling);

        Assert.Equal(first, second);   // identical seed → identical (mode, intensity) experience sequence

        for (int i = 0; i < draws; i++)
        {
            Assert.InRange(first[i].Intensity, 1, ceiling);   // intensity drawn in [1..ceiling]
            if (i > 0)
                Assert.NotEqual(first[i - 1].Scene, first[i].Scene);   // no immediate mode repeat
        }

        // Uniform draw over a 3-mode pool: 10 draws must visit more than one mode.
        Assert.True(first.Select(x => x.Scene).Distinct().Count() > 1);
    }

    [Fact]
    public void RandomizedLineup_DifferentSeeds_DivergeSomewhere()
    {
        var a = DrawSequence(1, 10, 4);
        var b = DrawSequence(2, 10, 4);
        Assert.NotEqual(a, b);
    }

    // ── standings formatter (shared splash/summary text) ────────────────────

    [Fact]
    public void StandingsFormatter_TagsTheLocalDomain_AndOrdersBestFirst()
    {
        using var rig = new TournamentRig();
        rig.LoadLobbyScene();
        rig.Tournament.RecordResults(JadeRubyGold);
        rig.Tournament.NextGameName = "Joust";
        rig.Tournament.NextGameIntensity = 2;

        string running = TournamentStandingsFormatter.FormatRunning(rig.Tournament, Domains.Ruby);
        Assert.Contains("first to 6", running);
        Assert.Contains("Up next: Joust · Intensity 2", running);
        Assert.Contains("Ruby <b>(You)</b>  1", running);
        Assert.True(running.IndexOf("Jade", StringComparison.Ordinal) < running.IndexOf("Ruby", StringComparison.Ordinal),
            "Best-first: the leading domain renders above the local player's domain.");

        string final = TournamentStandingsFormatter.FormatFinal(rig.Tournament, Domains.Gold);
        Assert.Contains("1. Jade — 2 pts", final);
        Assert.Contains("3. Gold <b>(You)</b> — 0 pts", final);
        Assert.Contains("1st: Jade", final);

        // Blue (the no-team sentinel) never tags a row.
        Assert.DoesNotContain("(You)", TournamentStandingsFormatter.FormatRunning(rig.Tournament, Domains.Blue));
    }

    // ── lobby ready-up / countdown (TournamentLobbyNetwork) ─────────────────

    [Fact]
    public void LobbyNetwork_ArmsCountdown_SnapsOnAllReady_FiresBeginNextRoundOnce()
    {
        const float dt = 1f / 60f;
        using var rig = new TournamentRig();
        using var loop = new GameLoop(nameof(LobbyNetwork_ArmsCountdown_SnapsOnAllReady_FiresBeginNextRoundOnce));

        var nmGo = new GameObject("network-manager");
        var nm = nmGo.AddComponent<NetworkManager>();
        nm.IsServer = true;
        NetworkManager.Singleton = nm;   // ConnectedClientsIds = { 0 } — the solo host

        var lobbyGo = new GameObject("tournament-lobby");
        lobbyGo.SetActive(false);        // configure-before-activation
        var lobby = lobbyGo.AddComponent<TournamentLobbyNetwork>();
        lobbyGo.SetActive(true);

        try
        {
            rig.LoadLobbyScene();        // controller phase → Lobby (the arming gate)
            Assert.True(lobby.IsCountingDown);

            lobby.Spawn();               // scene-placed NetworkBehaviour contract (host-mode)

            double time = 0d;
            void Tick()
            {
                nm.ServerTime = new NetworkTime(time);
                loop.Tick(dt);
                time += dt;
            }

            Tick();                       // first server Update arms the 30s auto-start deadline
            Assert.Equal(30, lobby.SecondsRemaining);
            Assert.Equal(0, lobby.ReadyCount);
            Assert.Equal(1, lobby.TotalPlayers);

            lobby.ToggleLocalReady();     // the only connected client readies → deadline snaps to 5s
            Assert.True(lobby.LocalReady);
            Assert.Equal(1, lobby.ReadyCount);
            Assert.True(lobby.SecondsRemaining <= 5,
                $"All-ready must snap the deadline in (got {lobby.SecondsRemaining}s).");

            // Run the clock past the snapped deadline: the host draws + launches EXACTLY once.
            for (int i = 0; i < 6 * 60 && rig.LaunchRaises == 0; i++) Tick();
            Assert.Equal(1, rig.LaunchRaises);
            Assert.True(rig.Tournament.IndexOfSceneName(rig.GameData.SceneName) >= 0,
                "The draw launched one of the pool games.");

            for (int i = 0; i < 30; i++) Tick();   // one-shot: no re-fire while the scene 'loads'
            Assert.Equal(1, rig.LaunchRaises);
        }
        finally
        {
            lobby.Despawn();
            NetworkManager.Singleton = null;
        }
    }
}
