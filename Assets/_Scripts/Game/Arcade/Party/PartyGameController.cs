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
    ///
    /// Flow:
    ///   Enter → PartyGame_Components enabled, ready button disabled
    ///   → Cinematic → free flight (vessels active)
    ///   → Lobby fills → Randomizing → "Round 1: Joust"
    ///   → WaitingForReady → READY button (1st ready — accept the game)
    ///   → "Loading..." → activate env → position players
    ///   → MiniGameReady → READY button (2nd ready — start playing)
    ///   → Hide panel → 3-2-1-GO countdown → SetPlayersActive + StartTurn
    ///   → Playing → game ends → RoundResults → ready → next round...
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

        [Header("Party Components")]
        [Tooltip("Root GameObject holding all party-related objects (canvas, panel, etc.). " +
                 "Stays enabled at start; disabled during active gameplay; re-enabled between rounds.")]
        [SerializeField] GameObject partyComponentsRoot;

        [Header("Mini-Game Environments")]
        [Tooltip("Root GameObjects for each mini-game environment. Index must match AvailableMiniGames order in config.")]
        [SerializeField] List<GameObject> miniGameEnvironments = new();

        // --- Network state ---
        readonly NetworkVariable<int> _netCurrentRound = new(0);
        readonly NetworkVariable<int> _netPhase = new((int)PartyPhase.Lobby);
        readonly NetworkVariable<int> _netSelectedMiniGameIndex = new(-1);

        // --- Local state ---
        readonly List<PartyRoundResult> _roundResults = new();
        readonly List<PartyPlayerState> _playerStates = new();
        readonly List<GameModes> _recentMiniGames = new();
        int _activeMiniGameIndex = -1;
        CancellationTokenSource _lobbyCts;
        CancellationTokenSource _roundCts;

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

            gameData.OnWinnerCalculated += OnMiniGameWinnerCalculated;
            gameData.OnMiniGameEnd += OnMiniGameEnded;
            gameData.OnPlayerAdded += HandlePlayerAdded;

            // Initialize round results
            _roundResults.Clear();
            for (int i = 0; i < config.TotalRounds; i++)
                _roundResults.Add(new PartyRoundResult { RoundIndex = i });

            // Disable all mini-game environments at start
            foreach (var env in miniGameEnvironments)
                if (env) env.SetActive(false);

            // Keep party components enabled (user's scene has them on by default)
            if (partyComponentsRoot)
                partyComponentsRoot.SetActive(true);

            // Initialize the party panel with round tabs (ready button disabled)
            if (partyPausePanel)
            {
                partyPausePanel.Initialize(config.TotalRounds, _playerStates);
                partyPausePanel.ForceShow();
            }

            // Manually fire the Lobby phase callback — NetworkVariable.OnValueChanged
            // does NOT fire when the initial value matches the default (Lobby = 0).
            OnNetPhaseChanged(-1, (int)PartyPhase.Lobby);

            if (IsServer)
            {
                if (IsSoloWithAI)
                    StartSoloLobby().Forget();
                else
                    StartLobbyTimer().Forget();
            }

            CSDebug.Log($"[PartyGame] OnNetworkSpawn complete. IsServer={IsServer}, IsSolo={IsSoloWithAI}, Envs={miniGameEnvironments.Count}");
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

            base.OnNetworkDespawn();
        }

        #endregion

        #region Phase Management

        void SetPhase(PartyPhase phase)
        {
            if (!IsServer) return;

            int newVal = (int)phase;
            int oldVal = _netPhase.Value;

            // NetworkVariable.OnValueChanged only fires when the value actually changes.
            // If we're setting the same value (e.g., re-entering WaitingForReady),
            // manually invoke the callback so the UI updates.
            if (oldVal == newVal)
            {
                CSDebug.Log($"[PartyGame] SetPhase: re-entering {phase}, forcing callback");
                OnNetPhaseChanged(oldVal, newVal);
            }
            else
            {
                _netPhase.Value = newVal;
            }
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

        void HandlePlayerAdded(string playerName, Domains domain)
        {
            if (!IsServer) return;

            bool isAI = false;
            var player = gameData.Players.FirstOrDefault(p => p.Name == playerName);
            if (player != null)
                isAI = player.IsInitializedAsAI;

            CSDebug.Log($"[PartyGame] HandlePlayerAdded: '{playerName}', domain={domain}, isAI={isAI}");
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

                if (CurrentPhase == PartyPhase.Lobby)
                {
                    FillWithAI();
                    ReinitializePanelWithPlayers_ClientRpc();
                    EnableFreeFlightForLobby_ClientRpc();
                    StartNextRound().Forget();
                }
            }
            catch (OperationCanceledException) { }
        }

        async UniTaskVoid StartSoloLobby()
        {
            _lobbyCts?.Cancel();
            _lobbyCts = new CancellationTokenSource();
            var ct = _lobbyCts.Token;

            try
            {
                BroadcastGameStateText_ClientRpc("Setting up party...");

                // Wait for ServerPlayerVesselInitializer to spawn human + AI.
                // Players arrive via HandlePlayerAdded → OnPlayerJoined.
                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.SoloLobbyWaitSeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                if (_playerStates.Count < config.MaxPlayers)
                    FillWithAI();

                CSDebug.Log($"[PartyGame] Solo lobby complete. Players: {_playerStates.Count}");
                foreach (var ps in _playerStates)
                    CSDebug.Log($"  - {ps.PlayerName} (AI={ps.IsAIReplacement}, Domain={ps.Domain})");

                // Re-initialize panel now that we have all players
                ReinitializePanelWithPlayers_ClientRpc();

                if (CurrentPhase == PartyPhase.Lobby)
                {
                    EnableFreeFlightForLobby_ClientRpc();
                    StartNextRound().Forget();
                }
            }
            catch (OperationCanceledException) { }
        }

        public void OnPlayerJoined(string playerName, Domains domain, bool isAI)
        {
            if (_playerStates.Any(p => p.PlayerName == playerName))
            {
                CSDebug.Log($"[PartyGame] Player '{playerName}' already tracked, skipping.");
                return;
            }

            // Enforce max player limit
            if (_playerStates.Count >= config.MaxPlayers)
            {
                CSDebug.Log($"[PartyGame] Max players ({config.MaxPlayers}) reached. Ignoring '{playerName}'.");
                return;
            }

            _playerStates.Add(new PartyPlayerState
            {
                PlayerName = playerName,
                Domain = domain,
                GamesWon = 0,
                IsAIReplacement = isAI,
                IsReady = isAI,
            });

            SyncPlayerJoined_ClientRpc(playerName, (int)domain, isAI);
            CSDebug.Log($"[PartyGame] Player joined: '{playerName}' (AI={isAI}). Total: {_playerStates.Count}/{config.MaxPlayers}");

            // In solo mode, StartSoloLobby manages the lobby flow with a short timer
            if (IsSoloWithAI) return;

            // Multiplayer: check if lobby is full
            int humanCount = _playerStates.Count(p => !p.IsAIReplacement);
            int totalCount = _playerStates.Count;

            if (totalCount >= config.MaxPlayers ||
                (humanCount >= config.MinPlayers && totalCount < config.MaxPlayers))
            {
                if (totalCount < config.MaxPlayers)
                    FillWithAI();

                _lobbyCts?.Cancel();
                ReinitializePanelWithPlayers_ClientRpc();
                EnableFreeFlightForLobby_ClientRpc();
                StartNextRound().Forget();
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
                CSDebug.Log($"[PartyGame] Filled slot {i} with AI: '{aiName}' ({aiDomain})");
            }
        }

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

        public void OnPlayerLeft(string playerName)
        {
            if (!IsServer) return;

            var state = _playerStates.FirstOrDefault(p => p.PlayerName == playerName);
            if (state == null) return;

            state.IsAIReplacement = true;
            state.IsReady = true;

            SyncPlayerLeft_ClientRpc(playerName);
            CSDebug.Log($"[PartyGame] Player '{playerName}' left. Replaced by AI.");

            if (CurrentPhase == PartyPhase.WaitingForReady ||
                CurrentPhase == PartyPhase.RoundResults ||
                CurrentPhase == PartyPhase.MiniGameReady)
                CheckAllPlayersReady();
        }

        #endregion

        #region Ready System

        /// <summary>
        /// Called when a player clicks the Ready button in the party panel.
        /// </summary>
        public void OnLocalPlayerReady()
        {
            if (gameData.LocalPlayer == null)
            {
                CSDebug.LogWarning("[PartyGame] OnLocalPlayerReady: gameData.LocalPlayer is null! Cannot send ready.");
                return;
            }

            string name = gameData.LocalPlayer.Name;
            CSDebug.Log($"[PartyGame] OnLocalPlayerReady: sending ready for '{name}', phase={CurrentPhase}");
            OnPlayerReady_ServerRpc(name);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnPlayerReady_ServerRpc(FixedString128Bytes playerName)
        {
            string name = playerName.ToString();
            var state = _playerStates.FirstOrDefault(p => p.PlayerName == name);
            if (state == null)
            {
                CSDebug.LogWarning($"[PartyGame] OnPlayerReady_ServerRpc: player '{name}' not found in _playerStates! " +
                                   $"Count={_playerStates.Count}, Names=[{string.Join(", ", _playerStates.Select(p => p.PlayerName))}]");
                return;
            }

            if (state.IsReady)
            {
                CSDebug.Log($"[PartyGame] Player '{name}' was already ready, ignoring duplicate.");
                return;
            }

            state.IsReady = true;

            int readyCount = _playerStates.Count(p => p.IsReady);
            BroadcastPlayerReady_ClientRpc(new FixedString128Bytes(name), readyCount, _playerStates.Count);
            CSDebug.Log($"[PartyGame] Player '{name}' ready. ({readyCount}/{_playerStates.Count}), phase={CurrentPhase}");

            CheckAllPlayersReady();
        }

        void CheckAllPlayersReady()
        {
            bool allReady = _playerStates.Count > 0 && _playerStates.All(p => p.IsReady);
            CSDebug.Log($"[PartyGame] CheckAllPlayersReady: allReady={allReady}, phase={CurrentPhase}");

            if (!allReady) return;

            switch (CurrentPhase)
            {
                case PartyPhase.WaitingForReady:
                    CSDebug.Log("[PartyGame] All ready during WaitingForReady → LoadMiniGameEnvironment");
                    LoadMiniGameEnvironment().Forget();
                    break;

                case PartyPhase.MiniGameReady:
                    CSDebug.Log("[PartyGame] All ready during MiniGameReady → BeginMiniGamePlay");
                    BeginMiniGamePlay().Forget();
                    break;

                case PartyPhase.RoundResults:
                    CSDebug.Log("[PartyGame] All ready during RoundResults → StartNextRound");
                    StartNextRound().Forget();
                    break;

                default:
                    CSDebug.Log($"[PartyGame] All ready but phase is {CurrentPhase}, no action taken.");
                    break;
            }
        }

        void ResetReadyStates()
        {
            foreach (var state in _playerStates)
                state.IsReady = state.IsAIReplacement; // AI is always ready

            int readyCount = _playerStates.Count(p => p.IsReady);
            CSDebug.Log($"[PartyGame] ResetReadyStates: {readyCount}/{_playerStates.Count} ready (AI pre-ready)");

            // Sync the reset to clients so the UI updates
            ResetReadyStates_ClientRpc();
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
                CSDebug.Log($"[PartyGame] StartNextRound: round {roundIndex + 1}/{config.TotalRounds}");

                // Phase: Randomizing
                SetPhase(PartyPhase.Randomizing);
                BroadcastGameStateText_ClientRpc("Randomizing game...");

                int miniGameIndex = PickRandomMiniGame();
                _netSelectedMiniGameIndex.Value = miniGameIndex;
                var selectedMode = config.AvailableMiniGames[miniGameIndex];
                _roundResults[roundIndex].MiniGameMode = selectedMode;

                await UniTask.Delay(TimeSpan.FromSeconds(1.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                string modeName = GetMiniGameDisplayName(selectedMode);
                BroadcastGameStateText_ClientRpc($"Round {roundIndex + 1}: {modeName}");

                await UniTask.Delay(TimeSpan.FromSeconds(1.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                // Phase: WaitingForReady — 1st ready (accept the game)
                SetPhase(PartyPhase.WaitingForReady);
                ResetReadyStates();
                ForceShowPartyPanel_ClientRpc();
                BroadcastGameStateText_ClientRpc($"Round {roundIndex + 1}: {modeName} — Ready up!");
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// After 1st ready: "Loading..." → activate env → position players → MiniGameReady
        /// </summary>
        async UniTaskVoid LoadMiniGameEnvironment()
        {
            if (!IsServer) return;

            _roundCts?.Cancel();
            _roundCts = new CancellationTokenSource();
            var ct = _roundCts.Token;

            try
            {
                int miniGameIndex = _netSelectedMiniGameIndex.Value;
                int roundIndex = _netCurrentRound.Value;
                var selectedMode = config.AvailableMiniGames[miniGameIndex];

                CSDebug.Log($"[PartyGame] LoadMiniGameEnvironment: envIndex={miniGameIndex}, mode={selectedMode}");

                BroadcastGameStateText_ClientRpc("Loading...");

                // Set the game mode on gameData
                gameData.GameMode = selectedMode;

                // Activate the mini-game environment on all clients
                ActivateMiniGameEnvironment_ClientRpc(miniGameIndex);
                ResetGameDataForRound_ClientRpc((int)selectedMode);

                OnRoundStarting?.Invoke(roundIndex, selectedMode);

                // Wait for environment activation to settle
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                // Phase: MiniGameReady — 2nd ready (start playing)
                SetPhase(PartyPhase.MiniGameReady);
                ResetReadyStates();
                ForceShowPartyPanel_ClientRpc();

                string modeName = GetMiniGameDisplayName(selectedMode);
                BroadcastGameStateText_ClientRpc($"{modeName} — Ready to play!");
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// After 2nd ready: hide panel → disable party components → 3-2-1-GO → SetPlayersActive + StartTurn
        /// </summary>
        async UniTaskVoid BeginMiniGamePlay()
        {
            if (!IsServer) return;

            _roundCts?.Cancel();
            _roundCts = new CancellationTokenSource();
            var ct = _roundCts.Token;

            try
            {
                CSDebug.Log("[PartyGame] BeginMiniGamePlay: hiding panel, starting gameplay");

                // Hide the party panel and disable party components during gameplay
                HidePartyUI_ClientRpc();

                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.PostCountdownDelaySeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                // Phase: Playing
                SetPhase(PartyPhase.Playing);
                StartGameplay_ClientRpc();

                // Fallback round timer
                RunRoundTimer(ct).Forget();
            }
            catch (OperationCanceledException) { }
        }

        int PickRandomMiniGame()
        {
            var available = config.AvailableMiniGames;
            if (available.Count == 0) return 0;

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

        async UniTaskVoid RunRoundTimer(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.RoundDurationSeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

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

            int roundIndex = _netCurrentRound.Value;
            var result = _roundResults[roundIndex];

            if (gameData.RoundStatsList.Count > 0)
            {
                result.WinnerName = gameData.RoundStatsList[0].Name;
                result.WinnerDomain = gameData.RoundStatsList[0].Domain;
            }

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

            var winnerState = _playerStates.FirstOrDefault(p => p.PlayerName == result.WinnerName);
            if (winnerState != null && !winnerState.IsAIReplacement)
                winnerState.GamesWon++;

            CSDebug.Log($"[PartyGame] Round {roundIndex + 1} winner: {result.WinnerName}");
        }

        void OnMiniGameEnded()
        {
            if (!IsServer) return;
            if (CurrentPhase != PartyPhase.Playing) return;

            CSDebug.Log("[PartyGame] OnMiniGameEnded → CompleteRound");
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

                // Wait for end-game cinematic to play
                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.PostRoundDelaySeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                // Deactivate mini-game environment, re-enable party components
                DeactivateAllMiniGameEnvironments_ClientRpc();
                ShowPartyUI_ClientRpc();

                // Sync round results
                SyncRoundResult(roundIndex, result);

                if (roundIndex + 1 >= config.TotalRounds)
                {
                    SetPhase(PartyPhase.FinalResults);
                    BroadcastGameStateText_ClientRpc("Party Complete!");
                    SyncFinalResults();
                    OnPartyCompleted?.Invoke();
                }
                else
                {
                    _netCurrentRound.Value = roundIndex + 1;
                    SetPhase(PartyPhase.RoundResults);
                    ResetReadyStates();

                    string winnerText = string.IsNullOrEmpty(result.WinnerName)
                        ? "Round complete!"
                        : $"{result.WinnerName} wins!";
                    BroadcastGameStateText_ClientRpc(winnerText);

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
                new FixedString64Bytes(result.WinnerName ?? ""),
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
        void BroadcastPlayerReady_ClientRpc(FixedString128Bytes playerName, int readyCount, int totalCount)
        {
            string name = playerName.ToString();
            var state = _playerStates.FirstOrDefault(p => p.PlayerName == name);
            if (state != null) state.IsReady = true;

            if (partyPausePanel)
                partyPausePanel.OnPlayerReadyChanged(name, true, readyCount, totalCount);
        }

        [ClientRpc]
        void ResetReadyStates_ClientRpc()
        {
            foreach (var state in _playerStates)
                state.IsReady = state.IsAIReplacement;

            if (partyPausePanel)
                partyPausePanel.OnReadyStatesReset();
        }

        [ClientRpc]
        void SyncPlayerJoined_ClientRpc(FixedString128Bytes playerName, int domain, bool isAI)
        {
            if (IsServer) return;

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
        void ReinitializePanelWithPlayers_ClientRpc()
        {
            if (partyPausePanel)
                partyPausePanel.Initialize(config.TotalRounds, _playerStates);
        }

        [ClientRpc]
        void EnableFreeFlightForLobby_ClientRpc()
        {
            PauseSystem.TogglePauseGame(false);
            gameData.SetPlayersActive();
        }

        [ClientRpc]
        void ForceShowPartyPanel_ClientRpc()
        {
            if (partyPausePanel)
                partyPausePanel.ForceShow();
        }

        /// <summary>
        /// Hide the party panel and disable party components during active gameplay.
        /// </summary>
        [ClientRpc]
        void HidePartyUI_ClientRpc()
        {
            if (partyPausePanel)
                partyPausePanel.Hide();

            // Disable party components root so it doesn't interfere during gameplay
            if (partyComponentsRoot)
                partyComponentsRoot.SetActive(false);
        }

        /// <summary>
        /// Re-enable party components and show the panel between rounds.
        /// </summary>
        [ClientRpc]
        void ShowPartyUI_ClientRpc()
        {
            if (partyComponentsRoot)
                partyComponentsRoot.SetActive(true);

            if (partyPausePanel)
                partyPausePanel.ForceShow();
        }

        [ClientRpc]
        void StartGameplay_ClientRpc()
        {
            // Ensure the game is unpaused
            PauseSystem.TogglePauseGame(false);

            gameData.InitializeGame();

            // Use the active mini-game environment's CountdownTimer for 3-2-1-GO
            var countdownTimer = FindActiveCountdownTimer();
            if (countdownTimer)
            {
                CSDebug.Log("[PartyGame] StartGameplay: running countdown timer");
                countdownTimer.BeginCountdown(() =>
                {
                    gameData.SetPlayersActive();
                    gameData.StartTurn();
                });
            }
            else
            {
                CSDebug.Log("[PartyGame] StartGameplay: no countdown timer, starting immediately");
                gameData.SetPlayersActive();
                gameData.StartTurn();
            }
        }

        CountdownTimer FindActiveCountdownTimer()
        {
            if (_activeMiniGameIndex < 0 || _activeMiniGameIndex >= miniGameEnvironments.Count)
                return null;

            var env = miniGameEnvironments[_activeMiniGameIndex];
            if (!env || !env.activeSelf) return null;

            return env.GetComponentInChildren<CountdownTimer>();
        }

        [ClientRpc]
        void ActivateMiniGameEnvironment_ClientRpc(int miniGameIndex)
        {
            DeactivateAllEnvironments();

            _activeMiniGameIndex = miniGameIndex;

            if (miniGameIndex < 0 || miniGameIndex >= miniGameEnvironments.Count)
            {
                CSDebug.LogWarning($"[PartyGame] ActivateEnv: index {miniGameIndex} out of range (count={miniGameEnvironments.Count})");
                return;
            }

            var env = miniGameEnvironments[miniGameIndex];
            if (!env)
            {
                CSDebug.LogWarning($"[PartyGame] ActivateEnv: environment at index {miniGameIndex} is null");
                return;
            }

            env.SetActive(true);
            CSDebug.Log($"[PartyGame] Activated environment: '{env.name}'");

            var spawner = env.GetComponentInChildren<SegmentSpawner>();
            if (spawner) spawner.Initialize();

            UpdateSpawnPositionsFromEnv(env);
        }

        void UpdateSpawnPositionsFromEnv(GameObject env)
        {
            var spvi = env.GetComponentInChildren<ServerPlayerVesselInitializer>(true);
            if (spvi && spvi.PlayerOrigins is { Length: > 0 })
            {
                gameData.SetSpawnPositions(spvi.PlayerOrigins);
                gameData.ResetPlayers();
                CSDebug.Log($"[PartyGame] Updated spawn positions from '{env.name}' ({spvi.PlayerOrigins.Length} origins)");
            }
            else
            {
                CSDebug.LogWarning($"[PartyGame] No ServerPlayerVesselInitializer with origins found in '{env.name}'");
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

            // Only reset stats if they exist (first round won't have any yet)
            if (gameData.RoundStatsList != null && gameData.RoundStatsList.Count > 0)
                gameData.ResetStatsDataForReplay();
        }

        void DeactivateAllEnvironments()
        {
            _activeMiniGameIndex = -1;
            foreach (var env in miniGameEnvironments)
                if (env) env.SetActive(false);
        }

        #endregion

        #region Public API for UI

        public void TogglePartyPanel()
        {
            if (!partyPausePanel) return;

            if (partyPausePanel.IsVisible)
                partyPausePanel.Hide();
            else
                partyPausePanel.ForceShow();
        }

        public void ShowPartyPanel()
        {
            if (partyPausePanel)
                partyPausePanel.ForceShow();
        }

        public void OnQuitParty()
        {
            if (multiplayerSetup)
                multiplayerSetup.LeaveSession().Forget();
        }

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
