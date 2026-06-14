using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Persistent brain for a Tournament session. Pure C# DI singleton (created eagerly by
    /// <c>AppManager</c>, so it is alive from bootstrap and survives every Single scene load).
    /// A static <see cref="Instance"/> is exposed so scene MonoBehaviours (the Scoreboard's
    /// Continue button, the Tournament scene view) can reach it without DI injection — mirroring
    /// <c>PartyInviteController.Instance</c> / <c>CameraManager.Instance</c>.
    ///
    /// Design (see Docs/TournamentSystem/ARCHITECTURE.md):
    ///   • Sequential <c>LoadSceneMode.Single</c> loads — the network session / Relay / Player
    ///     objects already persist across them. The host drives every scene load; clients follow
    ///     via Netcode. No additive loading, no new NetworkBehaviour.
    ///   • Standings are network-free: on <c>OnMiniGameEnd</c> EVERY peer folds the already-synced
    ///     <see cref="GameDataSO.Results"/> into <see cref="TournamentDataSO"/> identically.
    ///   • Phase is driven by scene loads (deterministic on every peer): the lobby scene starts
    ///     the tournament, each queued game scene advances the index, Menu_Main ends it.
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
        public bool IsLastGame => _tournament != null && _tournament.IsLastGame;

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

            // The lobby scene load is the per-peer "tournament started" signal (covers both
            // first start and Play-Again restart, which reloads the lobby).
            if (scene.name == _tournament.LobbySceneName)
            {
                StartTournament();
                return;
            }

            // A queued game scene loaded — keep the index in lock-step with the scene on
            // every peer (the same value the Scoreboard reads for IsLastGame).
            int idx = _tournament.IndexOfSceneName(scene.name);
            if (idx >= 0 && _tournament.IsActive)
            {
                _tournament.CurrentGameIndex = idx;
                _stateMachine.TransitionTo(TournamentPhase.InGame);
            }
        }

        void HandleMiniGameEnd()
        {
            if (_tournament == null || !_tournament.IsActive) return;

            // Fold this game's ranked, synced results into the cumulative standings. Runs on
            // every peer with identical input, BEFORE the next Single load clears Results.
            _tournament.RecordResults(_gameData.Results);

            if (_tournament.IsLastGame)
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
            _gameData.IsTournamentMode = true;
            _stateMachine.ResetToIdle();
            _stateMachine.TransitionTo(TournamentPhase.Lobby);
            _tournament.OnTournamentStarted.Raise();
        }

        /// <summary>Host loads the first game in the lineup (called from the lobby scene view).</summary>
        public void BeginFirstGame()
        {
            if (!IsHost) return;
            LoadGameAtIndex(0);
        }

        /// <summary>
        /// Host advances to the next game (the Scoreboard's host-only Continue button). The
        /// party follows the Single load via Netcode.
        /// </summary>
        public void AdvanceToNextGame()
        {
            if (!IsHost) return;
            if (_tournament == null || !_tournament.IsActive) return;
            LoadGameAtIndex(_tournament.CurrentGameIndex + 1);
        }

        /// <summary>
        /// Host restarts the whole tournament (the final Scoreboard's Play Again). Reloads the
        /// lobby so every peer resets its standings via <see cref="StartTournament"/>, then the
        /// lobby view advances to game 1.
        /// </summary>
        public void RestartTournament()
        {
            if (!IsHost) return;
            LoadLobby();
        }

        /// <summary>Clears tournament state on every peer (Menu_Main return / exit).</summary>
        public void EndTournament()
        {
            _tournament.IsActive = false;
            _gameData.IsTournamentMode = false;
            _stateMachine.ResetToIdle();
        }

        // ── Host-only scene loads (reuse the proven SceneLoader path) ─────────────

        void LoadGameAtIndex(int index)
        {
            if (_tournament == null) return;
            if (index < 0 || index >= _tournament.GameCount) return;

            var game = _tournament.GameQueue[index];
            if (game == null) return;

            // SyncFromArcadeGame sets SceneName/GameMode/IsMultiplayerMode for this game.
            // Player count / intensity / AI backfill / domain count were set by the configure
            // modal at tournament launch and persist across the Single loads (ResetRuntimeData
            // does not touch them), so each game inherits the same lobby config automatically.
            _gameData.SyncFromArcadeGame(game);
            _gameData.IsTournamentMode = true;       // SyncFromArcadeGame doesn't set it; keep it on.
            _gameData.InvokeGameLaunch();            // → SceneLoader.LaunchGame (host loads; clients follow)
        }

        void LoadLobby()
        {
            _gameData.SceneName = _tournament.LobbySceneName;
            _gameData.GameMode = CosmicShore.Data.GameModes.Tournament;
            _gameData.IsMultiplayerMode = true;
            _gameData.IsTournamentMode = true;
            _gameData.InvokeGameLaunch();
        }
    }
}
