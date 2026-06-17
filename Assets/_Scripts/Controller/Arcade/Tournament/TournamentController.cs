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
    /// Continue button, the Tournament scene view) can reach it without DI injection — mirroring
    /// <c>PartyInviteController.Instance</c> / <c>CameraManager.Instance</c>.
    ///
    /// Design (see Docs/TournamentSystem/ARCHITECTURE.md):
    ///   • Sequential <c>LoadSceneMode.Single</c> loads — the network session / Relay / Player
    ///     objects already persist across them. The host drives every scene load; clients follow
    ///     via Netcode. No additive loading, no new NetworkBehaviour.
    ///   • <b>Randomized lineup</b> (the "Shuffle" card): each game the host draws a random pool mode
    ///     + a random intensity in [1..ceiling] and launches it. Clients learn the mode from the
    ///     loaded scene and the intensity from the existing config sync — no shared RNG seed needed.
    ///   • <b>Race to 6</b>: standings are network-free — on <c>OnMiniGameEnd</c> EVERY peer folds the
    ///     already-synced <see cref="GameDataSO.Results"/> into per-domain crystals identically and
    ///     evaluates <see cref="TournamentDataSO.IsShuffleComplete"/> (a domain hit the target, or the
    ///     game cap). The host then advances to the next random game or the summary.
    ///   • Phase is driven by scene loads (deterministic on every peer): the lobby scene starts the
    ///     session, each pool game scene marks it in-game, Menu_Main ends it.
    /// </summary>
    public class TournamentController
    {
        public static TournamentController Instance { get; private set; }

        readonly GameDataSO _gameData;
        readonly TournamentDataSO _tournament;
        readonly SceneNameListSO _sceneNames;
        readonly TournamentStateMachine _stateMachine = new();

        public TournamentPhase Phase => _stateMachine.Current;
        public bool IsActive => _tournament != null && _tournament.IsActive;

        /// <summary>True while the Tournament results screen is up (after the last game). Read by the scene view.</summary>
        public bool IsShowingSummary => _stateMachine.Current == TournamentPhase.Summary;

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

            // The Tournament scene is BOTH the intro and the end-of-tournament results screen.
            // Distinguish them by phase (deterministic on every peer): if the last game just
            // finished (Complete), this load is the SUMMARY — show results, keep standings.
            // Otherwise it's a fresh start — reset + lobby.
            if (scene.name == _tournament.LobbySceneName)
            {
                if (_stateMachine.Current == TournamentPhase.Complete)
                    _stateMachine.TransitionTo(TournamentPhase.Summary);
                else
                    StartTournament();
                return;
            }

            // A pool game scene loaded.
            int idx = _tournament.IndexOfSceneName(scene.name);
            if (idx >= 0 && _tournament.IsActive)
            {
                // Play Again from the summary: ANY pool game loading while still in Summary phase is a
                // fresh shuffle — reset standings on every peer (phase is Summary on all peers after the
                // summary scene loaded, so the wipe is deterministic). With the randomized first game we
                // can no longer key this on a fixed index (it used to be idx == 0).
                if (_stateMachine.Current == TournamentPhase.Summary)
                {
                    _tournament.ResetRuntime();
                    _tournament.IsActive = true;
                    _gameData.IsTournamentMode = true;
                }
                _tournament.CurrentGameIndex = idx;   // which pool mode is loaded (for repeat-avoidance)
                _stateMachine.TransitionTo(TournamentPhase.InGame);
            }
        }

        void HandleMiniGameEnd()
        {
            if (_tournament == null || !_tournament.IsActive) return;

            // Fold this game's ranked, synced results into the cumulative per-domain standings (and
            // bump GamesPlayed). Runs on every peer with identical input, BEFORE the next Single load
            // clears Results.
            _tournament.RecordResults(_gameData.Results);

            // Race to 6 (or the game cap): once the shuffle is decided, the next Continue loads the
            // summary instead of another game. Evaluated identically on every peer from synced state.
            if (_tournament.IsShuffleComplete)
            {
                _stateMachine.TransitionTo(TournamentPhase.Complete);
                _tournament.OnTournamentCompleted.Raise();
            }
        }

        // ── Session control ──────────────────────────────────────────────────────

        /// <summary>
        /// Initializes (or re-initializes) a tournament. Called on every peer when the lobby
        /// scene loads, so standings reset identically across the party.
        /// </summary>
        public void StartTournament()
        {
            _tournament.ResetRuntime();
            _tournament.IsActive = true;
            // Capture the lobby-chosen intensity as the per-game CEILING (X); each game then draws a
            // random intensity in [1..X]. Set AFTER ResetRuntime (which preserves the ceiling) so a
            // fresh start from the lobby re-captures the player's current choice.
            _tournament.IntensityCeiling = _gameData.SelectedIntensity != null
                ? Mathf.Max(1, _gameData.SelectedIntensity.Value)
                : 1;
            _gameData.IsTournamentMode = true;
            _stateMachine.ResetToIdle();
            _stateMachine.TransitionTo(TournamentPhase.Lobby);
            _tournament.OnTournamentStarted.Raise();
        }

        /// <summary>Host loads the first (random) game in the lineup (called from the lobby scene view).</summary>
        public void BeginFirstGame()
        {
            if (!IsHost) return;
            LoadRandomGame();
        }

        /// <summary>
        /// Host advances on the Scoreboard's host-only Continue button. If the shuffle is decided
        /// (race target reached or game cap hit) it loads the Tournament scene, which shows the results
        /// summary (phase Complete → Summary on load); otherwise it draws and loads the next random
        /// game. The party follows the Single load.
        /// </summary>
        public void AdvanceToNextGame()
        {
            if (!IsHost) return;
            if (_tournament == null || !_tournament.IsActive) return;

            if (_tournament.IsShuffleComplete)
                LoadTournamentScene();   // → Summary results screen
            else
                LoadRandomGame();
        }

        /// <summary>
        /// Host restarts the whole tournament (the summary screen's Play Again). Loads a fresh random
        /// game 1; the reset runs on every peer when that game loads while still in Summary phase
        /// (see <see cref="HandleSceneLoaded"/>), so standings clear consistently across the party.
        /// </summary>
        public void RestartTournament()
        {
            if (!IsHost) return;
            LoadRandomGame();
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
        // the shuffle is decided — the scene view picks the layout from the phase).
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
