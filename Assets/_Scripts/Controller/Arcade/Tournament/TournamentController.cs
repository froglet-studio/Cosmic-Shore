using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Persistent brain for a Tournament / Shuffle session. Pure C# DI singleton (created eagerly by
    /// <c>AppManager</c>, so it is alive from bootstrap and survives every Single scene load).
    /// A static <see cref="Instance"/> is exposed so scene MonoBehaviours (the Scoreboard's
    /// Continue button, the Tournament scene view) can reach it without DI injection - mirroring
    /// <c>PartyInviteController.Instance</c> / <c>CameraManager.Instance</c>.
    ///
    /// Design (see Docs/TournamentSystem/ARCHITECTURE.md):
    ///   • Sequential <c>LoadSceneMode.Single</c> loads - the network session / Relay / Player
    ///     objects already persist across them. The host drives every scene load; clients follow
    ///     via Netcode. No additive loading, no new NetworkBehaviour.
    ///   • <b>Randomized lineup</b> (the "Shuffle" card): each game the host draws a random pool mode
    ///     + a random intensity in [1..ceiling] and launches it. Clients learn the mode from the
    ///     loaded scene and the intensity from the existing config sync - no shared RNG seed needed.
    ///   • <b>Race to 6</b>: standings are network-free - on <c>OnMiniGameEnd</c> EVERY peer folds the
    ///     already-synced <see cref="GameDataSO.Results"/> into per-domain crystals identically and
    ///     evaluates <see cref="TournamentDataSO.IsShuffleComplete"/> (a domain hit the target, or the
    ///     game cap). The host then advances to the next random game or the summary.
    ///   • Phase is driven by scene loads (deterministic on every peer): the lobby scene starts the
    ///     session, each pool game scene marks it in-game, Menu_Main ends it.
    /// </summary>
    public class TournamentController
    {
        public static TournamentController Instance { get; private set; }

        // Pure C# Reflex singleton: a new one is constructed per play session, but the old one's
        // SceneManager.sceneLoaded subscription (a Unity static event that also fires in edit
        // mode) survives a domain-reload-free play exit — session N would have N controllers
        // folding standings. Tear the stale instance down before the next session constructs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance?.Teardown();
            Instance = null;
        }

        void Teardown()
        {
            _gameData.OnMiniGameEnd.OnRaised -= HandleMiniGameEnd;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        readonly GameDataSO _gameData;
        readonly TournamentDataSO _tournament;
        readonly SceneNameListSO _sceneNames;
        readonly TournamentStateMachine _stateMachine = new();

        public TournamentPhase Phase => _stateMachine.Current;
        public bool IsActive => _tournament != null && _tournament.IsActive;

        /// <summary>True while the Tournament results screen is up (after the last game). Read by the scene view.</summary>
        public bool IsShowingSummary => _stateMachine.Current == TournamentPhase.Summary;

        /// <summary>
        /// True for the between-game transition whose loading splash shows the running standings - a
        /// tournament is active, a game has been played, and the shuffle isn't decided yet. Mirrors the
        /// exact condition <c>BootStatusBroadcaster.HandleLaunchGame</c> uses to render those standings,
        /// so the dwell below applies precisely when - and because - that summary is on screen.
        /// </summary>
        public bool IsBetweenGamesStandingsShown =>
            _tournament != null && _tournament.IsActive
            && !_tournament.IsShuffleComplete && _tournament.GamesPlayed > 0;

        /// <summary>
        /// Minimum seconds the loading splash should hold before the next scene load begins, so the
        /// between-game running standings are readable. Zero outside that window, so normal game launches,
        /// the first game, and the load into the final summary are never slowed. Read by
        /// <c>SceneLoader.LaunchGame</c> (host path) - holding the host's load holds the whole party's splash.
        /// </summary>
        public float MinLoadSplashDwellSeconds =>
            IsBetweenGamesStandingsShown ? Mathf.Max(0f, _tournament.BetweenGameSummaryDwellSeconds) : 0f;

        static bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

        public TournamentController(GameDataSO gameData, TournamentDataSO tournament, SceneNameListSO sceneNames)
        {
            _gameData = gameData;
            _tournament = tournament;
            _sceneNames = sceneNames;
            Instance = this;

            // OnMiniGameEnd fires on every peer after the mode synced Results; the handler
            // no-ops unless a tournament is active, so non-tournament games are unaffected.
            _gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        // ── Scene-driven lifecycle (runs on every peer) ──────────────────────────

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_tournament == null) return;

            // Returning to the menu ends any running tournament (covers the host's Main Menu
            // press and the clients that follow the load).
            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            if (scene.name == menuScene)
            {
                if (_tournament.IsActive) EndTournament();
                return;
            }

            // The Maelstrom scene serves FOUR roles, decided here (deterministic on every peer):
            //   • Summary phase → restart  (Play Again from the summary → fresh lobby; reset but KEEP the
            //                               intensity ceiling - see RestartFromSummary). Must be checked
            //                               FIRST: standings still read complete here, so it has to win
            //                               over the IsShuffleComplete branch below.
            //   • shuffle decided → SUMMARY (a domain hit WinTarget / the game cap → show final results).
            //   • mid-run        → HUB      (between rounds: IsActive with games played → standings hub;
            //                               do NOT reset - standings/history must persist).
            //   • otherwise      → fresh start (entered from the arcade/menu → reset + capture the ceiling).
            //
            // Summary-vs-hub keys off the AUTHORITATIVE TournamentDataSO.IsShuffleComplete (folded
            // identically on every peer from the synced results), NOT the transient Complete phase that
            // HandleMiniGameEnd sets. That phase is only honored when the deciding game ends in the InGame
            // phase; if it is ever missed, the win MUST still surface as the summary instead of silently
            // routing back to the hub for "one more game" (the race-to-6 regression). See EnterSummary.
            if (scene.name == _tournament.LobbySceneName)
            {
                if (_stateMachine.Current == TournamentPhase.Summary)
                    RestartFromSummary();                                // Play Again → fresh lobby (keep ceiling)
                else if (_tournament.IsActive && _tournament.IsShuffleComplete)
                    EnterSummary();                                      // shuffle decided → results summary
                else if (_tournament.IsActive && _tournament.GamesPlayed > 0)
                    _stateMachine.TransitionTo(TournamentPhase.Lobby);   // between-round hub (no reset)
                else
                    StartTournament();                                   // fresh start from arcade/menu
                return;
            }

            // A pool game scene loaded. Every game is now launched from the lobby/hub (BeginNextRound),
            // so the restart wipe happens at LOBBY load (RestartFromSummary), not here - a game scene
            // only ever loads while already in Lobby/InGame phase.
            int idx = _tournament.IndexOfSceneName(scene.name);
            if (idx >= 0 && _tournament.IsActive)
            {
                _tournament.CurrentGameIndex = idx;   // which pool mode is loaded (for repeat-avoidance)
                _stateMachine.TransitionTo(TournamentPhase.InGame);
            }
        }

        void HandleMiniGameEnd()
        {
            if (_tournament == null || !_tournament.IsActive) return;

            // Fold this game's ranked, synced results into the cumulative per-domain standings (and
            // bump GamesPlayed) + capture a per-round history snapshot. Runs on every peer with
            // identical input, BEFORE the next Single load clears Results / Players.
            //
            // Domain placement comes from the mode rule's TEAM-TOTAL order (summed metric per
            // domain - the same aggregation that ends the turn and picks WinnerDomain), NOT from
            // per-player ranks: rank-derived placement let a losing team outplace the team that
            // out-collected it whenever its best individual tied the top score. RoundStatsList is
            // still populated and synced here (the ClientRpc that raised OnMiniGameEnd updated it),
            // so the order is identical on every peer.
            var placement = _gameData.ScoringRule != null
                ? _gameData.ScoringRule.ResolvePlacementOrder(_gameData)
                : null;
            var snapshots = BuildPlayerSnapshots(_gameData.Results);
            string modeName = _tournament.CurrentGame != null ? _tournament.CurrentGame.DisplayName : null;
            int intensity = _gameData.SelectedIntensity != null ? _gameData.SelectedIntensity.Value : 0;
            _tournament.RecordResults(_gameData.Results, snapshots, modeName, intensity, placement);

            // Race to 6 (or the game cap): once the shuffle is decided, the next Continue loads the
            // summary instead of another game. Evaluated identically on every peer from synced state.
            // The Complete transition is a best-effort signal (it only lands when the game ends in the
            // InGame phase) - it is NOT the source of truth for showing the summary. The authoritative,
            // phase-independent decision is re-made from IsShuffleComplete at the Maelstrom scene load
            // (see HandleSceneLoaded → EnterSummary), so a missed transition here can't swallow the win.
            if (_tournament.IsShuffleComplete)
            {
                _stateMachine.TransitionTo(TournamentPhase.Complete);
                _tournament.OnTournamentCompleted.Raise();
            }
        }

        /// <summary>
        /// Builds enriched per-player history snapshots for the just-finished round by merging the
        /// ranked <paramref name="results"/> with avatar / AI metadata from the still-populated
        /// <c>gameData.Players</c> (matched by Name) - captured before the next Single load clears them.
        /// Runs on every peer; avatar/AI fields are display-only so minor cross-peer differences are harmless.
        /// </summary>
        List<TournamentPlayerSnapshot> BuildPlayerSnapshots(IReadOnlyList<ScoreResult> results)
        {
            var list = new List<TournamentPlayerSnapshot>();
            if (results == null) return list;

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];

                IPlayer player = null;
                var players = _gameData.Players;
                if (players != null)
                {
                    for (int p = 0; p < players.Count; p++)
                    {
                        var candidate = players[p];
                        if (candidate != null && candidate.Name == r.Name) { player = candidate; break; }
                    }
                }

                list.Add(new TournamentPlayerSnapshot
                {
                    Name = r.Name,
                    Domain = r.Domain,
                    Rank = r.Rank,
                    ScoreText = r.ScoreText,
                    Secondary = r.Secondary,
                    AvatarId = player != null ? player.AvatarId : -1,
                    IsAI = player != null && player.IsInitializedAsAI,
                });
            }
            return list;
        }

        // ── Session control ──────────────────────────────────────────────────────

        /// <summary>
        /// Fresh start from the arcade/menu - resets standings AND captures the lobby-chosen intensity
        /// as the per-game ceiling. Called on every peer when the lobby scene loads outside a run, so
        /// standings reset identically across the party.
        /// </summary>
        public void StartTournament() => StartTournamentInternal(captureCeiling: true);

        /// <summary>
        /// Play Again from the summary - same fresh-lobby reset, but PRESERVES the intensity ceiling.
        /// By the summary, <c>gameData.SelectedIntensity</c> holds the last game's rolled value (not the
        /// original ceiling), so re-capturing it would corrupt the ceiling; <see cref="ResetRuntime"/>
        /// already preserves it, so we just skip the re-capture. Runs on every peer (lobby load while
        /// phase is Summary), so the wipe is deterministic across the party.
        /// </summary>
        void RestartFromSummary() => StartTournamentInternal(captureCeiling: false);

        void StartTournamentInternal(bool captureCeiling)
        {
            _tournament.ResetRuntime();   // preserves IntensityCeiling
            _tournament.IsActive = true;

            // Resolve the race-to-N win target from the End Game Conditions tool (Resources/EndConditionOverrides,
            // edited via FrogletTools > Game Modes > End Game Conditions) once per shuffle start. Runs on every peer
            // from the same committed asset, so IsShuffleComplete stays deterministic across the party; falls
            // back to the asset's serialized WinTarget if the tool asset is missing.
            var endConditions = EndConditionOverridesSO.Instance;
            _tournament.ResolveWinTarget(endConditions != null ? endConditions.GetMaelstromWinTarget() : _tournament.WinTarget);

            // Capture the lobby-chosen intensity as the per-game CEILING (X); each game then draws a
            // random intensity in [1..X]. Set AFTER ResetRuntime so a fresh start re-captures the
            // player's current choice; skipped on Play Again so the original ceiling survives.
            if (captureCeiling)
                _tournament.IntensityCeiling = _gameData.SelectedIntensity != null
                    ? Mathf.Max(1, _gameData.SelectedIntensity.Value)
                    : 1;
            _gameData.IsTournamentMode = true;
            _stateMachine.ResetToIdle();
            _stateMachine.TransitionTo(TournamentPhase.Lobby);
            _tournament.OnTournamentStarted.Raise();
        }

        /// <summary>
        /// Drives the state machine to the results <see cref="TournamentPhase.Summary"/> when the shuffle
        /// is decided. Routed through <see cref="TournamentPhase.Complete"/> so the Complete signal is still
        /// observed, but it does NOT depend on <see cref="HandleMiniGameEnd"/> having already set Complete:
        /// the win is authoritative via <see cref="TournamentDataSO.IsShuffleComplete"/>, so the summary
        /// must show even if the per-game-end Complete transition was missed (e.g. the deciding game ended
        /// in a phase other than InGame). Idempotent and safe from Lobby / InGame / Complete (the only
        /// phases that occur at a mid-run Maelstrom load); runs on every peer, so it stays deterministic.
        /// </summary>
        void EnterSummary()
        {
            if (_stateMachine.Current == TournamentPhase.Summary) return;
            if (_stateMachine.Current != TournamentPhase.Complete)
                _stateMachine.TransitionTo(TournamentPhase.Complete);
            _stateMachine.TransitionTo(TournamentPhase.Summary);
        }

        /// <summary>
        /// Host draws + loads the next random game (mode + intensity). Called from the Maelstrom
        /// lobby/hub once the ready-up countdown elapses - so the draw happens at Ready, keeping the
        /// upcoming mode hidden until its connecting panel. The party follows the Single load.
        /// </summary>
        public void BeginNextRound()
        {
            if (!IsHost) return;
            LoadRandomGame();
        }

        /// <summary>Back-compat alias for <see cref="BeginNextRound"/> (the first round is just the
        /// next round from a fresh lobby). Retained for the existing lobby view wiring.</summary>
        public void BeginFirstGame() => BeginNextRound();

        /// <summary>
        /// Host advances on the Scoreboard's host-only Continue button. ALWAYS returns the party to the
        /// Maelstrom scene: mid-run it shows the standings HUB (phase InGame → Lobby on load); once the
        /// shuffle is decided it shows the results SUMMARY (phase Complete → Summary on load). The next
        /// random game is drawn later, from the hub's ready-up (<see cref="BeginNextRound"/>), so the
        /// upcoming mode stays hidden in the hub. The party follows the Single load.
        /// </summary>
        public void AdvanceToNextGame()
        {
            if (!IsHost) return;
            if (_tournament == null || !_tournament.IsActive) return;

            LoadTournamentScene();
        }

        /// <summary>
        /// Host restarts the whole tournament (the summary screen's Play Again). Loads the Maelstrom
        /// scene as a fresh lobby; the reset runs on every peer when it loads while still in Summary
        /// phase (see <see cref="HandleSceneLoaded"/> → <see cref="RestartFromSummary"/>), so standings
        /// clear consistently across the party while keeping the chosen intensity ceiling.
        /// </summary>
        public void RestartTournament()
        {
            if (!IsHost) return;
            LoadTournamentScene();
        }

        /// <summary>Clears tournament state on every peer (Menu_Main return / exit).</summary>
        public void EndTournament()
        {
            _tournament.IsActive = false;
            _gameData.IsTournamentMode = false;
            _stateMachine.ResetToIdle();
        }

        // ── Host-only random draw + scene load (reuse the proven SceneLoader path) ─

        /// <summary>
        /// Draws a random (mode, intensity ∈ [1..ceiling]) "experience" from the pool and launches it.
        /// The host drives the Single load; clients follow it (the mode is the loaded scene, the
        /// intensity rides the existing <c>SyncGameConfigToClients</c> path), so no shared RNG/seed is
        /// needed. Avoids immediately repeating the previous mode when the pool has more than one.
        /// </summary>
        void LoadRandomGame()
        {
            if (_tournament == null || _tournament.GameCount == 0) return;

            // CurrentGameIndex holds the last loaded pool mode (set on scene load); avoid repeating it,
            // except for the very first game of the session.
            int avoid = _tournament.GamesPlayed > 0 ? _tournament.CurrentGameIndex : -1;
            int pick = PickRandomModeIndex(avoid);

            var game = _tournament.GameQueue[pick];
            if (game == null) return;

            int ceiling = Mathf.Clamp(_tournament.IntensityCeiling <= 0 ? 1 : _tournament.IntensityCeiling, 1, 4);
            int intensity = Random.Range(1, ceiling + 1);   // inclusive [1..ceiling]

            // Per-game intensity: set BEFORE SyncFromArcadeGame (which doesn't touch intensity) and
            // before launch, so the game scene's config sync replicates it to clients.
            if (_gameData.SelectedIntensity != null)
                _gameData.SelectedIntensity.Value = intensity;

            // Stamp what's loading so the between-game splash can show "up next: <mode> · Intensity N"
            // (see TournamentStandingsFormatter.FormatRunning). Set before InvokeGameLaunch fires OnLaunchGame.
            _tournament.NextGameName = game.DisplayName;
            _tournament.NextGameIntensity = intensity;

            _gameData.SyncFromArcadeGame(game);        // scene / mode / multiplayer
            _gameData.IsTournamentMode = true;         // SyncFromArcadeGame doesn't set it; keep it on.
            _gameData.InvokeGameLaunch();              // → SceneLoader.LaunchGame (host loads; clients follow)
        }

        // Uniform random pool index, optionally excluding `avoid` (-1 = no exclusion).
        int PickRandomModeIndex(int avoid)
        {
            int count = _tournament.GameCount;
            if (count <= 1) return 0;
            if (avoid < 0 || avoid >= count) return Random.Range(0, count);

            // Pick uniformly among the (count - 1) indices that are not `avoid`.
            int r = Random.Range(0, count - 1);
            return r < avoid ? r : r + 1;
        }

        // Loads the Tournament scene (the intro lobby on a fresh start, the results summary after
        // the shuffle is decided - the scene view picks the layout from the phase).
        void LoadTournamentScene()
        {
            _gameData.SceneName = _tournament.LobbySceneName;
            _gameData.GameMode = CosmicShore.Data.GameModes.Tournament;
            _gameData.IsMultiplayerMode = true;
            _gameData.IsTournamentMode = true;
            _gameData.InvokeGameLaunch();
        }
    }
}
