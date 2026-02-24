using CosmicShore.App.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CosmicShore.Game.UI.Party;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade.Party
{
    /// <summary>
    /// Orchestrates a full Party Game session: lobby, 5 randomized mini-game rounds,
    /// scoring, and final results. Lives in the party scene for the entire session.
    /// Mini-game environments are child GameObjects that get enabled/disabled per round.
    /// </summary>
    public class PartyGameController : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] PartyGameConfigSO config;
        [SerializeField] GameDataSO gameData;

        [Header("Multiplayer")]
        [SerializeField] MultiplayerSetup multiplayerSetup;

        [Header("UI")]
        [SerializeField] PartyPausePanel partyPausePanel;

        [Header("Mini-Game Environments")]
        [Tooltip("Root GameObjects for each mini-game environment. Index must match AvailableMiniGames order in config.")]
        [SerializeField] List<GameObject> miniGameEnvironments = new();

        // --- Network state ---
        readonly NetworkVariable<int> _netCurrentRound = new(0);
        readonly NetworkVariable<int> _netPhase = new((int)PartyPhase.Lobby);
        readonly NetworkVariable<int> _netSelectedMiniGameIndex = new(-1);
        readonly NetworkVariable<float> _netLobbyStartTime = new(0f);

        // --- Local state ---
        readonly List<PartyRoundResult> _roundResults = new();
        readonly List<PartyPlayerState> _playerStates = new();
        readonly List<GameModes> _recentMiniGames = new();
        readonly List<MiniGameControllerBase> _miniGameControllers = new();
        int _readyPlayerCount;
        CancellationTokenSource _lobbyCts;
        CancellationTokenSource _roundCts;
        GameObject _activeEnvironmentInstance;

        // --- Derived state ---
        bool IsSoloWithAI => !gameData.IsMultiplayerMode;

        // --- Public API ---
        public PartyPhase CurrentPhase => (PartyPhase)_netPhase.Value;
        public int CurrentRound => _netCurrentRound.Value;
        public int TotalRounds => config.TotalRounds;
        public IReadOnlyList<PartyRoundResult> RoundResults => _roundResults;
        public IReadOnlyList<PartyPlayerState> PlayerStates => _playerStates;

        // --- Events ---
        public event Action<PartyPhase> OnPhaseChanged;
        public event Action<int, GameModes> OnRoundStarting;
        public event Action<int, PartyRoundResult> OnRoundCompleted;
        public event Action OnPartyCompleted;
        public event Action<string> OnGameStateTextChanged;

        #region Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _netPhase.OnValueChanged += OnNetPhaseChanged;
            _netCurrentRound.OnValueChanged += OnNetRoundChanged;
            _netSelectedMiniGameIndex.OnValueChanged += OnNetMiniGameChanged;

            // Subscribe to game data events to capture round results
            gameData.OnWinnerCalculated += OnMiniGameWinnerCalculated;
            gameData.OnMiniGameEnd += OnMiniGameEnded;

            // Subscribe to player join events so we track who's in the party
            gameData.OnPlayerAdded += HandlePlayerAdded;

            // Auto-discover mini-game controllers from environment GameObjects
            DiscoverMiniGameControllers();

            // Initialize round results
            _roundResults.Clear();
            for (int i = 0; i < config.TotalRounds; i++)
                _roundResults.Add(new PartyRoundResult { RoundIndex = i });

            // Disable all mini-game environments at start
            foreach (var env in miniGameEnvironments)
                if (env) env.SetActive(false);

            if (IsServer)
            {
                _netLobbyStartTime.Value = Time.realtimeSinceStartup;
                SetPhase(PartyPhase.Lobby);

                if (IsSoloWithAI)
                    StartSoloLobby().Forget();
                else
                    StartLobbyTimer().Forget();
            }

            // Initialize the party panel with round tabs
            if (partyPausePanel)
            {
                partyPausePanel.Initialize(config.TotalRounds, _playerStates);
                partyPausePanel.Show();
            }
        }

        public override void OnNetworkDespawn()
        {
            _netPhase.OnValueChanged -= OnNetPhaseChanged;
            _netCurrentRound.OnValueChanged -= OnNetRoundChanged;
            _netSelectedMiniGameIndex.OnValueChanged -= OnNetMiniGameChanged;

            gameData.OnWinnerCalculated -= OnMiniGameWinnerCalculated;
            gameData.OnMiniGameEnd -= OnMiniGameEnded;
            gameData.OnPlayerAdded -= HandlePlayerAdded;

            _lobbyCts?.Cancel();
            _lobbyCts?.Dispose();
            _roundCts?.Cancel();
            _roundCts?.Dispose();

            DestroyEnvironmentInstance();

            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Auto-discovers MiniGameControllerBase components from the environment GameObjects.
        /// Each environment root should have the controller as a component.
        /// </summary>
        void DiscoverMiniGameControllers()
        {
            _miniGameControllers.Clear();
            foreach (var env in miniGameEnvironments)
            {
                if (!env)
                {
                    _miniGameControllers.Add(null);
                    continue;
                }

                var controller = env.GetComponent<MiniGameControllerBase>();
                _miniGameControllers.Add(controller);

                if (!controller)
                    CSDebug.Log($"[PartyGame] No MiniGameControllerBase on '{env.name}'. PartyGameController will drive game flow.");
            }
        }

        #endregion

        #region Phase Management

        void SetPhase(PartyPhase phase)
        {
            if (!IsServer) return;
            _netPhase.Value = (int)phase;
        }

        void OnNetPhaseChanged(int previous, int current)
        {
            var phase = (PartyPhase)current;
            CSDebug.Log($"[PartyGame] Phase: {(PartyPhase)previous} -> {phase}");
            OnPhaseChanged?.Invoke(phase);

            if (partyPausePanel)
                partyPausePanel.OnPhaseChanged(phase);
        }

        void OnNetRoundChanged(int previous, int current)
        {
            CSDebug.Log($"[PartyGame] Round: {previous} -> {current}");
        }

        void OnNetMiniGameChanged(int previous, int current)
        {
            if (current < 0 || current >= config.AvailableMiniGames.Count) return;
            var mode = config.AvailableMiniGames[current];
            CSDebug.Log($"[PartyGame] Selected mini-game: {mode}");
        }

        #endregion

        #region Lobby & Player Management

        /// <summary>
        /// Bridge from GameDataSO.OnPlayerAdded event to our player tracking.
        /// Called on the server when a new player is added to game data.
        /// </summary>
        void HandlePlayerAdded(string playerName, Domains domain)
        {
            if (!IsServer) return;

            bool isAI = false;
            // Check if this player is AI by looking at the game data
            var player = gameData.Players.FirstOrDefault(p => p.Name == playerName);
            if (player != null)
                isAI = player.IsInitializedAsAI;

            OnPlayerJoined(playerName, domain, isAI);
        }

        async UniTaskVoid StartLobbyTimer()
        {
            _lobbyCts?.Cancel();
            _lobbyCts = new CancellationTokenSource();
            var ct = _lobbyCts.Token;

            try
            {
                BroadcastGameStateText_ClientRpc("Waiting for players...");

                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.LobbyWaitTimeSeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                // Timeout reached — fill with AI and move to ready phase
                if (CurrentPhase == PartyPhase.Lobby)
                {
                    FillWithAI();
                    SetPhase(PartyPhase.WaitingForReady);
                    BroadcastGameStateText_ClientRpc("All players joined. Ready up!");
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Solo mode (1 player selected): wait for ServerPlayerVesselInitializer to
        /// spawn players via HandlePlayerAdded, then use a short lobby timer so the
        /// player can see the lobby before readying up.
        /// </summary>
        async UniTaskVoid StartSoloLobby()
        {
            _lobbyCts?.Cancel();
            _lobbyCts = new CancellationTokenSource();
            var ct = _lobbyCts.Token;

            try
            {
                BroadcastGameStateText_ClientRpc("Setting up party...");

                // Wait for the ServerPlayerVesselInitializer to spawn human + AI
                // Players arrive via HandlePlayerAdded → OnPlayerJoined
                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.SoloLobbyWaitSeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                // If players still haven't filled (e.g., initializer didn't add enough), fill now
                if (_playerStates.Count < config.MaxPlayers)
                    FillWithAI();

                // Re-initialize the panel now that we have all players
                if (partyPausePanel)
                    partyPausePanel.Initialize(config.TotalRounds, _playerStates);

                if (CurrentPhase == PartyPhase.Lobby)
                {
                    SetPhase(PartyPhase.WaitingForReady);
                    BroadcastGameStateText_ClientRpc("All players joined. Ready up!");
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Called by the server player initializer when a new player joins.
        /// </summary>
        public void OnPlayerJoined(string playerName, Domains domain, bool isAI)
        {
            if (_playerStates.Any(p => p.PlayerName == playerName)) return;

            _playerStates.Add(new PartyPlayerState
            {
                PlayerName = playerName,
                Domain = domain,
                GamesWon = 0,
                IsAIReplacement = isAI,
                IsReady = isAI, // AI is always ready
            });

            SyncPlayerJoined_ClientRpc(playerName, (int)domain, isAI);

            // In solo mode, StartSoloLobby manages the lobby flow with a short timer
            if (IsSoloWithAI) return;

            // Check if we have enough players
            int humanCount = _playerStates.Count(p => !p.IsAIReplacement);
            int totalCount = _playerStates.Count;

            if (totalCount >= config.MaxPlayers ||
                (humanCount >= config.MinPlayers && totalCount < config.MaxPlayers))
            {
                // Fill remaining slots with AI
                if (totalCount < config.MaxPlayers)
                    FillWithAI();

                _lobbyCts?.Cancel();
                SetPhase(PartyPhase.WaitingForReady);
                BroadcastGameStateText_ClientRpc("All players joined. Ready up!");
            }
        }

        void FillWithAI()
        {
            int currentCount = _playerStates.Count;
            for (int i = currentCount; i < config.MaxPlayers; i++)
            {
                var aiDomain = PickUnusedDomain();
                string aiName = $"AI Pilot {i + 1}";

                _playerStates.Add(new PartyPlayerState
                {
                    PlayerName = aiName,
                    Domain = aiDomain,
                    GamesWon = 0,
                    IsAIReplacement = true,
                    IsReady = true,
                });

                SyncPlayerJoined_ClientRpc(aiName, (int)aiDomain, true);
            }
        }

        /// <summary>
        /// Picks a domain not already used by existing players.
        /// Avoids drawing from the DomainAssigner pool which may already
        /// be exhausted by the ServerPlayerVesselInitializer.
        /// </summary>
        Domains PickUnusedDomain()
        {
            var usedDomains = new HashSet<Domains>(_playerStates.Select(p => p.Domain));
            Domains[] candidates = { Domains.Jade, Domains.Ruby, Domains.Gold };
            foreach (var d in candidates)
            {
                if (!usedDomains.Contains(d))
                    return d;
            }
            return Domains.Unassigned;
        }

        /// <summary>
        /// Called when a player leaves mid-party. Replaces them with AI.
        /// AI replacement scores are not recorded.
        /// </summary>
        public void OnPlayerLeft(string playerName)
        {
            if (!IsServer) return;

            var state = _playerStates.FirstOrDefault(p => p.PlayerName == playerName);
            if (state == null) return;

            state.IsAIReplacement = true;
            state.IsReady = true;

            SyncPlayerLeft_ClientRpc(playerName);
            CSDebug.Log($"[PartyGame] Player '{playerName}' left. Replaced by AI.");

            // If we were waiting for ready and this was the last holdout, check ready state
            if (CurrentPhase == PartyPhase.WaitingForReady || CurrentPhase == PartyPhase.RoundResults)
                CheckAllPlayersReady();
        }

        #endregion

        #region Ready System

        /// <summary>
        /// Called when a player clicks the Ready button in the party panel.
        /// </summary>
        public void OnLocalPlayerReady()
        {
            if (gameData.LocalPlayer == null) return;
            OnPlayerReady_ServerRpc(gameData.LocalPlayer.Name);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnPlayerReady_ServerRpc(FixedString128Bytes playerName)
        {
            string name = playerName.ToString();
            var state = _playerStates.FirstOrDefault(p => p.PlayerName == name);
            if (state == null) return;

            state.IsReady = true;
            _readyPlayerCount++;

            BroadcastPlayerReady_ClientRpc(new FixedString128Bytes(name));
            CSDebug.Log($"[PartyGame] Player '{name}' ready. ({_readyPlayerCount}/{_playerStates.Count})");

            CheckAllPlayersReady();
        }

        void CheckAllPlayersReady()
        {
            if (!_playerStates.All(p => p.IsReady)) return;

            if (CurrentPhase == PartyPhase.WaitingForReady || CurrentPhase == PartyPhase.RoundResults)
            {
                StartNextRound().Forget();
            }
        }

        void ResetReadyStates()
        {
            _readyPlayerCount = 0;
            foreach (var state in _playerStates)
            {
                state.IsReady = state.IsAIReplacement; // AI is always ready
            }
        }

        #endregion

        #region Round Flow

        async UniTaskVoid StartNextRound()
        {
            if (!IsServer) return;

            _roundCts?.Cancel();
            _roundCts = new CancellationTokenSource();
            var ct = _roundCts.Token;

            try
            {
                int roundIndex = _netCurrentRound.Value;

                // Phase: Randomizing
                SetPhase(PartyPhase.Randomizing);
                BroadcastGameStateText_ClientRpc("Randomizing game...");

                // Randomize mini-game selection
                int miniGameIndex = PickRandomMiniGame();
                _netSelectedMiniGameIndex.Value = miniGameIndex;
                var selectedMode = config.AvailableMiniGames[miniGameIndex];

                _roundResults[roundIndex].MiniGameMode = selectedMode;

                await UniTask.Delay(TimeSpan.FromSeconds(1.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                string modeName = GetMiniGameDisplayName(selectedMode);
                BroadcastGameStateText_ClientRpc($"Round {roundIndex + 1}: {modeName}");

                await UniTask.Delay(TimeSpan.FromSeconds(1.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                // Phase: Countdown
                SetPhase(PartyPhase.Countdown);
                ForceShowPartyPanel_ClientRpc();

                for (int i = (int)config.PreRoundCountdownSeconds; i > 0; i--)
                {
                    BroadcastGameStateText_ClientRpc($"Starting in {i}...");
                    await UniTask.Delay(TimeSpan.FromSeconds(1f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
                }

                // Hide HUD, small delay, then start
                HideMiniGameHUD_ClientRpc();
                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.PostCountdownDelaySeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                // Phase: Playing — activate the mini-game environment
                SetPhase(PartyPhase.Playing);
                ActivateMiniGameEnvironment_ClientRpc(miniGameIndex);

                // Notify listeners
                OnRoundStarting?.Invoke(roundIndex, selectedMode);

                // Reset game data for the round and start gameplay
                gameData.GameMode = selectedMode;
                ResetGameDataForRound_ClientRpc((int)selectedMode);

                // Small delay for environment activation to settle
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                // Start the actual gameplay — this is what the standalone controllers do
                StartGameplay_ClientRpc();

                // Start a round timer as a fallback end condition
                RunRoundTimer(ct).Forget();
            }
            catch (OperationCanceledException) { }
        }

        int PickRandomMiniGame()
        {
            var available = config.AvailableMiniGames;
            if (available.Count == 0) return 0;

            // Avoid repeating the last game if possible
            var candidates = new List<int>();
            for (int i = 0; i < available.Count; i++)
            {
                if (_recentMiniGames.Count > 0 && _recentMiniGames.Last() == available[i] && available.Count > 1)
                    continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0)
                candidates.AddRange(Enumerable.Range(0, available.Count));

            int chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            _recentMiniGames.Add(available[chosen]);

            return chosen;
        }

        /// <summary>
        /// Fallback round timer. If the game-specific end condition doesn't fire
        /// within the configured duration, the server forces the round to end.
        /// </summary>
        async UniTaskVoid RunRoundTimer(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.RoundDurationSeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                // Timer expired and we're still playing — force end
                if (IsServer && CurrentPhase == PartyPhase.Playing)
                {
                    CSDebug.Log("[PartyGame] Round timer expired. Forcing round end.");
                    ForceEndRound_ClientRpc();
                }
            }
            catch (OperationCanceledException) { }
        }

        [ClientRpc]
        void ForceEndRound_ClientRpc()
        {
            gameData.InvokeGameTurnConditionsMet();
            gameData.SortRoundStats(false);
            gameData.CalculateDomainStats(false);
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        void OnMiniGameWinnerCalculated()
        {
            if (!IsServer) return;
            if (CurrentPhase != PartyPhase.Playing) return;

            // Capture results from gameData
            int roundIndex = _netCurrentRound.Value;
            var result = _roundResults[roundIndex];

            // Determine winner (first in sorted stats)
            if (gameData.RoundStatsList.Count > 0)
            {
                result.WinnerName = gameData.RoundStatsList[0].Name;
                result.WinnerDomain = gameData.RoundStatsList[0].Domain;
            }

            // Capture per-player scores
            result.PlayerScores.Clear();
            foreach (var stats in gameData.RoundStatsList)
            {
                var playerState = _playerStates.FirstOrDefault(p => p.PlayerName == stats.Name);
                result.PlayerScores.Add(new PartyRoundPlayerScore
                {
                    PlayerName = stats.Name,
                    Domain = stats.Domain,
                    Score = stats.Score,
                    IsAIReplacement = playerState?.IsAIReplacement ?? false,
                });
            }

            // Update wins — only count non-AI-replacement players
            var winnerState = _playerStates.FirstOrDefault(p => p.PlayerName == result.WinnerName);
            if (winnerState != null && !winnerState.IsAIReplacement)
                winnerState.GamesWon++;

            CSDebug.Log($"[PartyGame] Round {roundIndex + 1} winner: {result.WinnerName}");
        }

        void OnMiniGameEnded()
        {
            if (!IsServer) return;
            if (CurrentPhase != PartyPhase.Playing) return;

            CompleteRound().Forget();
        }

        async UniTaskVoid CompleteRound()
        {
            if (!IsServer) return;

            _roundCts?.Cancel();
            _roundCts = new CancellationTokenSource();
            var ct = _roundCts.Token;

            try
            {
                int roundIndex = _netCurrentRound.Value;
                var result = _roundResults[roundIndex];

                // Wait for end-game cinematic to play (but we skip the scoreboard)
                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.PostRoundDelaySeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                // Deactivate mini-game environment
                DeactivateAllMiniGameEnvironments_ClientRpc();

                // Sync round results to all clients
                SyncRoundResult(roundIndex, result);

                // Check if party is complete
                if (roundIndex + 1 >= config.TotalRounds)
                {
                    SetPhase(PartyPhase.FinalResults);
                    BroadcastGameStateText_ClientRpc("Party Complete!");
                    SyncFinalResults();
                    OnPartyCompleted?.Invoke();
                }
                else
                {
                    // Move to next round
                    _netCurrentRound.Value = roundIndex + 1;
                    SetPhase(PartyPhase.RoundResults);
                    ResetReadyStates();

                    string winnerText = string.IsNullOrEmpty(result.WinnerName)
                        ? "Round complete!"
                        : $"{result.WinnerName} wins!";
                    BroadcastGameStateText_ClientRpc(winnerText);

                    // PartyEndGameHandler shows the party panel after the cinematic finishes

                    OnRoundCompleted?.Invoke(roundIndex, result);
                }
            }
            catch (OperationCanceledException) { }
        }

        #endregion

        #region Score Sync

        void SyncRoundResult(int roundIndex, PartyRoundResult result)
        {
            int playerCount = result.PlayerScores.Count;
            var names = new FixedString64Bytes[playerCount];
            var scores = new float[playerCount];
            var domains = new int[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                names[i] = new FixedString64Bytes(result.PlayerScores[i].PlayerName);
                scores[i] = result.PlayerScores[i].Score;
                domains[i] = (int)result.PlayerScores[i].Domain;
            }

            SyncRoundResult_ClientRpc(
                roundIndex,
                (int)result.MiniGameMode,
                new FixedString64Bytes(result.WinnerName),
                (int)result.WinnerDomain,
                names,
                scores,
                domains);
        }

        [ClientRpc]
        void SyncRoundResult_ClientRpc(
            int roundIndex,
            int miniGameMode,
            FixedString64Bytes winnerName,
            int winnerDomain,
            FixedString64Bytes[] playerNames,
            float[] playerScores,
            int[] playerDomains)
        {
            if (roundIndex < 0 || roundIndex >= _roundResults.Count) return;

            var result = _roundResults[roundIndex];
            result.MiniGameMode = (GameModes)miniGameMode;
            result.WinnerName = winnerName.ToString();
            result.WinnerDomain = (Domains)winnerDomain;
            result.PlayerScores.Clear();

            for (int i = 0; i < playerNames.Length; i++)
            {
                result.PlayerScores.Add(new PartyRoundPlayerScore
                {
                    PlayerName = playerNames[i].ToString(),
                    Score = playerScores[i],
                    Domain = (Domains)playerDomains[i],
                });
            }

            if (partyPausePanel)
                partyPausePanel.UpdateRoundResult(roundIndex, result);

            OnRoundCompleted?.Invoke(roundIndex, result);
        }

        void SyncFinalResults()
        {
            int count = _playerStates.Count;
            var names = new FixedString64Bytes[count];
            var wins = new int[count];
            var domains = new int[count];

            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(_playerStates[i].PlayerName);
                wins[i] = _playerStates[i].GamesWon;
                domains[i] = (int)_playerStates[i].Domain;
            }

            SyncFinalResults_ClientRpc(names, wins, domains);
        }

        [ClientRpc]
        void SyncFinalResults_ClientRpc(
            FixedString64Bytes[] names,
            int[] wins,
            int[] domains)
        {
            _playerStates.Clear();
            for (int i = 0; i < names.Length; i++)
            {
                _playerStates.Add(new PartyPlayerState
                {
                    PlayerName = names[i].ToString(),
                    GamesWon = wins[i],
                    Domain = (Domains)domains[i],
                });
            }

            // Sort by wins descending
            _playerStates.Sort((a, b) => b.GamesWon.CompareTo(a.GamesWon));

            if (partyPausePanel)
                partyPausePanel.OnFinalResults(_playerStates);

            OnPartyCompleted?.Invoke();
        }

        #endregion

        #region Client RPCs — UI & Environment

        [ClientRpc]
        void BroadcastGameStateText_ClientRpc(FixedString128Bytes text)
        {
            string msg = text.ToString();
            OnGameStateTextChanged?.Invoke(msg);

            if (partyPausePanel)
                partyPausePanel.SetGameStateText(msg);
        }

        [ClientRpc]
        void BroadcastPlayerReady_ClientRpc(FixedString128Bytes playerName)
        {
            string name = playerName.ToString();
            var state = _playerStates.FirstOrDefault(p => p.PlayerName == name);
            if (state != null) state.IsReady = true;

            if (partyPausePanel)
                partyPausePanel.OnPlayerReadyChanged(name, true);
        }

        [ClientRpc]
        void SyncPlayerJoined_ClientRpc(FixedString128Bytes playerName, int domain, bool isAI)
        {
            if (IsServer) return; // Server already has this state

            string name = playerName.ToString();
            if (_playerStates.Any(p => p.PlayerName == name)) return;

            _playerStates.Add(new PartyPlayerState
            {
                PlayerName = name,
                Domain = (Domains)domain,
                GamesWon = 0,
                IsAIReplacement = isAI,
                IsReady = isAI,
            });

            if (partyPausePanel)
                partyPausePanel.OnPlayerJoined(name, (Domains)domain, isAI);
        }

        [ClientRpc]
        void SyncPlayerLeft_ClientRpc(FixedString128Bytes playerName)
        {
            string name = playerName.ToString();
            var state = _playerStates.FirstOrDefault(p => p.PlayerName == name);
            if (state != null)
            {
                state.IsAIReplacement = true;
                state.IsReady = true;
            }

            if (partyPausePanel)
                partyPausePanel.OnPlayerLeft(name);
        }

        [ClientRpc]
        void ForceShowPartyPanel_ClientRpc()
        {
            if (partyPausePanel)
                partyPausePanel.ForceShow();
        }

        [ClientRpc]
        void HideMiniGameHUD_ClientRpc()
        {
            // The party scene should subscribe to this via the panel
            if (partyPausePanel)
                partyPausePanel.Hide();
        }

        [ClientRpc]
        void StartGameplay_ClientRpc()
        {
            // Ensure the game is unpaused — PauseSystem is static and may carry
            // over a paused state from menu navigation (ScreenSwitcher).
            // Also restores Time.timeScale = 1 so physics/movement work.
            PauseSystem.TogglePauseGame(false);

            gameData.InitializeGame();
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        [ClientRpc]
        void ActivateMiniGameEnvironment_ClientRpc(int miniGameIndex)
        {
            DeactivateAllEnvironments();

            Transform envParent = null;
            if (miniGameIndex >= 0 && miniGameIndex < miniGameEnvironments.Count)
            {
                var env = miniGameEnvironments[miniGameIndex];
                if (env)
                {
                    env.SetActive(true);
                    envParent = env.transform;

                    // Initialize SegmentSpawners each round (Start only fires once,
                    // so subsequent rounds need an explicit Initialize call).
                    var spawner = env.GetComponentInChildren<SegmentSpawner>();
                    if (spawner) spawner.Initialize();
                }
            }

            // Instantiate the visual environment prefab (Nucleus/Cell) under the env
            if (miniGameIndex >= 0 && miniGameIndex < config.AvailableMiniGames.Count)
            {
                var mode = config.AvailableMiniGames[miniGameIndex];
                SpawnEnvironmentPrefab(mode, envParent);
            }
        }

        [ClientRpc]
        void DeactivateAllMiniGameEnvironments_ClientRpc()
        {
            DeactivateAllEnvironments();
        }

        [ClientRpc]
        void ResetGameDataForRound_ClientRpc(int gameMode)
        {
            gameData.GameMode = (GameModes)gameMode;
            gameData.ResetStatsDataForReplay();
        }

        void SpawnEnvironmentPrefab(GameModes mode, Transform parent = null)
        {
            DestroyEnvironmentInstance();

            var entry = config.EnvironmentPrefabs.Find(e => e.gameMode == mode);
            if (entry?.environmentPrefab == null) return;

            _activeEnvironmentInstance = parent
                ? Instantiate(entry.environmentPrefab, parent)
                : Instantiate(entry.environmentPrefab);
            _activeEnvironmentInstance.name = $"PartyEnv_{mode}";
        }

        void DestroyEnvironmentInstance()
        {
            if (!_activeEnvironmentInstance) return;
            Destroy(_activeEnvironmentInstance);
            _activeEnvironmentInstance = null;
        }

        void DeactivateAllEnvironments()
        {
            DestroyEnvironmentInstance();
            foreach (var env in miniGameEnvironments)
                if (env) env.SetActive(false);
        }

        #endregion

        #region Public API for UI

        /// <summary>
        /// Called from the Quit button on the party pause panel.
        /// </summary>
        public void OnQuitParty()
        {
            if (multiplayerSetup)
                multiplayerSetup.LeaveSession().Forget();
        }

        /// <summary>
        /// Gets the display name for a mini-game mode.
        /// </summary>
        public static string GetMiniGameDisplayName(GameModes mode)
        {
            return mode switch
            {
                GameModes.MultiplayerCrystalCapture => "Crystal Capture",
                GameModes.HexRace => "Hex Race",
                GameModes.MultiplayerJoust => "Joust",
                _ => mode.ToString(),
            };
        }

        /// <summary>
        /// Gets the overall party winner (most games won).
        /// </summary>
        public PartyPlayerState GetPartyWinner()
        {
            return _playerStates
                .Where(p => !p.IsAIReplacement)
                .OrderByDescending(p => p.GamesWon)
                .FirstOrDefault();
        }

        #endregion
    }
}
