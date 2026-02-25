using CosmicShore.App.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CosmicShore.Game.UI;
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
    /// Orchestrates a Party Game session: lobby, randomized mini-game rounds,
    /// scoring, and final results.
    ///
    /// KEY DESIGN:
    /// - Mini-game controllers have IsPartyMode = true → suppresses autonomous lifecycle,
    ///   but gameplay mechanics (collisions, race finish, crystals) still work.
    /// - PartyVesselSpawner (on always-active PartyGameManager) handles vessel spawning
    ///   so ClientRpcs are never called on NetworkBehaviours from disabled GameObjects.
    /// - Environment SPVIs are always InertMode — they only provide spawn origin data.
    /// - Environment GameCanvas stays enabled for HUD; IsPartyMode suppresses mini-game UI.
    /// - Scene camera is disabled during gameplay, re-enabled between rounds.
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

        [Header("Vessel Spawning")]
        [Tooltip("Lives on PartyGameManager (always active). Handles vessel spawning so RPCs are safe.")]
        [SerializeField] PartyVesselSpawner vesselSpawner;

        [Header("Camera")]
        [Tooltip("The scene-level camera (Mini Game Main Camera). Disabled during gameplay so the vessel camera takes over.")]
        [SerializeField] Camera sceneCamera;

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
        MultiplayerMiniGameControllerBase _activeMiniGameController;
        bool _vesselsSpawned; // True after first round spawns vessels
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

            _roundResults.Clear();
            for (int i = 0; i < config.TotalRounds; i++)
                _roundResults.Add(new PartyRoundResult { RoundIndex = i });

            _vesselsSpawned = false;

            foreach (var env in miniGameEnvironments)
            {
                if (!env) continue;
                SetPartyModeOnEnvironment(env, ServerPlayerVesselInitializer.PartyModeState.InertMode);
                env.SetActive(false);
            }

            // Scene camera stays on during lobby (shows skybox/nucleus)
            if (sceneCamera) sceneCamera.enabled = true;

            if (partyPausePanel)
            {
                partyPausePanel.Initialize(config.TotalRounds, _playerStates);
                partyPausePanel.ForceShow();
            }

            OnNetPhaseChanged(-1, (int)PartyPhase.Lobby);

            if (IsServer)
            {
                if (IsSoloWithAI)
                    StartSoloLobby().Forget();
                else
                    StartLobbyTimer().Forget();
            }

            CSDebug.Log($"[PartyGame] OnNetworkSpawn. IsServer={IsServer}, IsSolo={IsSoloWithAI}, Envs={miniGameEnvironments.Count}");
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

                    SetPhase(PartyPhase.WaitingForReady);
                    ResetReadyStates();
                    ShowPanel_ClientRpc();
                    BroadcastGameStateText_ClientRpc("Ready to party!");
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

                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.SoloLobbyWaitSeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                if (!_playerStates.Any(p => !p.IsAIReplacement))
                {
                    string humanName = gameData.LocalPlayer?.Name ?? "Player 1";
                    var domain = Domains.Jade;
                    OnPlayerJoined(humanName, domain, false);
                    CSDebug.Log($"[PartyGame] Registered human player directly: '{humanName}'");
                }

                if (_playerStates.Count < config.MaxPlayers)
                    FillWithAI();

                CSDebug.Log($"[PartyGame] Solo lobby complete. Players: {_playerStates.Count}");

                ReinitializePanelWithPlayers_ClientRpc();

                SetPhase(PartyPhase.WaitingForReady);
                ResetReadyStates();
                ShowPanel_ClientRpc();
                BroadcastGameStateText_ClientRpc("Ready to party!");
            }
            catch (OperationCanceledException) { }
        }

        public void OnPlayerJoined(string playerName, Domains domain, bool isAI)
        {
            if (_playerStates.Any(p => p.PlayerName == playerName)) return;

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

            if (IsSoloWithAI) return;

            int humanCount = _playerStates.Count(p => !p.IsAIReplacement);
            int totalCount = _playerStates.Count;

            if (totalCount >= config.MaxPlayers ||
                (humanCount >= config.MinPlayers && totalCount < config.MaxPlayers))
            {
                if (totalCount < config.MaxPlayers)
                    FillWithAI();

                _lobbyCts?.Cancel();
                ReinitializePanelWithPlayers_ClientRpc();
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

        public void OnLocalPlayerReady()
        {
            string name = gameData.LocalPlayer?.Name;

            if (string.IsNullOrEmpty(name))
            {
                var human = _playerStates.FirstOrDefault(p => !p.IsAIReplacement);
                name = human?.PlayerName;
            }

            if (string.IsNullOrEmpty(name))
            {
                CSDebug.LogWarning("[PartyGame] OnLocalPlayerReady: no player found!");
                return;
            }

            CSDebug.Log($"[PartyGame] OnLocalPlayerReady: '{name}', phase={CurrentPhase}");
            OnPlayerReady_ServerRpc(name);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnPlayerReady_ServerRpc(FixedString128Bytes playerName)
        {
            string name = playerName.ToString();
            var state = _playerStates.FirstOrDefault(p => p.PlayerName == name);
            if (state == null)
            {
                CSDebug.LogWarning($"[PartyGame] OnPlayerReady_ServerRpc: '{name}' not found!");
                return;
            }

            if (state.IsReady) return;

            state.IsReady = true;
            int readyCount = _playerStates.Count(p => p.IsReady);
            BroadcastPlayerReady_ClientRpc(new FixedString128Bytes(name), readyCount, _playerStates.Count);
            CSDebug.Log($"[PartyGame] '{name}' ready ({readyCount}/{_playerStates.Count}), phase={CurrentPhase}");

            CheckAllPlayersReady();
        }

        void CheckAllPlayersReady()
        {
            bool allReady = _playerStates.Count > 0 && _playerStates.All(p => p.IsReady);
            if (!allReady) return;

            switch (CurrentPhase)
            {
                case PartyPhase.WaitingForReady:
                    if (_netSelectedMiniGameIndex.Value < 0)
                        StartNextRound().Forget();
                    else
                        LoadMiniGameEnvironment().Forget();
                    break;
                // MiniGameReady phase is no longer used — LoadMiniGameEnvironment
                // now proceeds directly to gameplay. Kept as safety fallback.
                case PartyPhase.MiniGameReady:
                    BeginMiniGamePlay().Forget();
                    break;
                case PartyPhase.RoundResults:
                    StartNextRound().Forget();
                    break;
            }
        }

        void ResetReadyStates()
        {
            foreach (var state in _playerStates)
                state.IsReady = state.IsAIReplacement;

            ResetReadyStates_ClientRpc();
            CheckAllPlayersReady();
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
                CSDebug.Log($"[PartyGame] StartNextRound: {roundIndex + 1}/{config.TotalRounds}");

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

                SetPhase(PartyPhase.WaitingForReady);
                ResetReadyStates();
                ShowPanel_ClientRpc();
                BroadcastGameStateText_ClientRpc($"Round {roundIndex + 1}: {modeName} — Ready up!");
            }
            catch (OperationCanceledException) { }
        }

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

                CSDebug.Log($"[PartyGame] LoadMiniGameEnvironment: envIndex={miniGameIndex}, mode={selectedMode}, vesselsExist={_vesselsSpawned}");

                BroadcastGameStateText_ClientRpc("Loading...");
                gameData.GameMode = selectedMode;

                // SPVI is always InertMode — PartyVesselSpawner handles spawning
                bool isFirstRound = !_vesselsSpawned;
                var spviMode = ServerPlayerVesselInitializer.PartyModeState.InertMode;

                ActivateMiniGameEnvironment_ClientRpc(miniGameIndex, (int)spviMode);
                ResetGameDataForRound_ClientRpc((int)selectedMode);
                OnRoundStarting?.Invoke(roundIndex, selectedMode);

                if (isFirstRound)
                {
                    // Spawn vessels via PartyVesselSpawner (on always-active GameObject — safe RPCs)
                    var spawnOrigins = GetSpawnOriginsFromEnv(miniGameIndex);
                    if (vesselSpawner && spawnOrigins is { Length: > 0 })
                    {
                        vesselSpawner.SpawnVesselsForParty(spawnOrigins);
                    }
                    else
                    {
                        CSDebug.LogError($"[PartyGame] Cannot spawn vessels — vesselSpawner={vesselSpawner != null}, origins={spawnOrigins?.Length ?? 0}");
                    }

                    BroadcastGameStateText_ClientRpc("Spawning vessels...");
                    await UniTask.Delay(TimeSpan.FromSeconds(3f), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                    _vesselsSpawned = true;
                    SetVesselsSpawned_ClientRpc();
                }
                else
                {
                    // Vessels already exist — reposition via PartyVesselSpawner
                    RepositionPlayersViaSpawner_ClientRpc(miniGameIndex);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.5f), DelayType.UnscaledDeltaTime, cancellationToken: ct);
                }

                string modeName = GetMiniGameDisplayName(selectedMode);
                BroadcastGameStateText_ClientRpc($"{modeName} — Starting...");

                // Hide panel and go straight to gameplay — no extra ready step
                HidePanel_ClientRpc();
                DisableSceneCamera_ClientRpc();

                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.PostCountdownDelaySeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                SetPhase(PartyPhase.Playing);
                StartGameplay_ClientRpc();
                RunRoundTimer(ct).Forget();
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Returns spawn origin transforms from the SPVI on the given environment.
        /// </summary>
        Transform[] GetSpawnOriginsFromEnv(int envIndex)
        {
            if (envIndex < 0 || envIndex >= miniGameEnvironments.Count) return null;
            var env = miniGameEnvironments[envIndex];
            if (!env) return null;

            var spvi = env.GetComponentInChildren<ServerPlayerVesselInitializer>(true);
            return spvi != null ? spvi.PlayerOrigins : null;
        }

        async UniTaskVoid BeginMiniGamePlay()
        {
            if (!IsServer) return;

            _roundCts?.Cancel();
            _roundCts = new CancellationTokenSource();
            var ct = _roundCts.Token;

            try
            {
                CSDebug.Log("[PartyGame] BeginMiniGamePlay");
                HidePanel_ClientRpc();
                DisableSceneCamera_ClientRpc();

                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.PostCountdownDelaySeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                SetPhase(PartyPhase.Playing);
                StartGameplay_ClientRpc();
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
                    CSDebug.Log("[PartyGame] Round timer expired.");
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
            if (winnerState != null)
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

                await UniTask.Delay(
                    TimeSpan.FromSeconds(config.PostRoundDelaySeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);

                // Deactivate mini-game controller, then environment, then re-enable scene camera
                DeactivateCurrentMiniGame_ClientRpc();
                DeactivateAllEnvironments_ClientRpc();
                EnableSceneCamera_ClientRpc();
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
                    _netSelectedMiniGameIndex.Value = -1;
                    SetPhase(PartyPhase.RoundResults);
                    ResetReadyStates();

                    string winnerText = string.IsNullOrEmpty(result.WinnerName)
                        ? "Round complete!"
                        : $"{result.WinnerName} wins!";
                    BroadcastGameStateText_ClientRpc(winnerText);
                    OnRoundCompleted?.Invoke(roundIndex, result);
                }

                ShowPanel_ClientRpc();
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
                names, scores, domains);
        }

        [ClientRpc]
        void SyncRoundResult_ClientRpc(
            int roundIndex, int miniGameMode,
            FixedString64Bytes winnerName, int winnerDomain,
            FixedString64Bytes[] playerNames, float[] playerScores, int[] playerDomains)
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
        void SyncFinalResults_ClientRpc(FixedString64Bytes[] names, int[] wins, int[] domains)
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

        #region Client RPCs

        [ClientRpc]
        void BroadcastGameStateText_ClientRpc(FixedString128Bytes text)
        {
            string msg = text.ToString();
            OnGameStateTextChanged?.Invoke(msg);
            if (partyPausePanel) partyPausePanel.SetGameStateText(msg);
        }

        [ClientRpc]
        void BroadcastPlayerReady_ClientRpc(FixedString128Bytes playerName, int readyCount, int totalCount)
        {
            string name = playerName.ToString();
            var state = _playerStates.FirstOrDefault(p => p.PlayerName == name);
            if (state != null) state.IsReady = true;
            if (partyPausePanel) partyPausePanel.OnPlayerReadyChanged(name, true, readyCount, totalCount);
        }

        [ClientRpc]
        void ResetReadyStates_ClientRpc()
        {
            foreach (var state in _playerStates)
                state.IsReady = state.IsAIReplacement;
            if (partyPausePanel) partyPausePanel.OnReadyStatesReset();
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

            if (partyPausePanel) partyPausePanel.OnPlayerJoined(name, (Domains)domain, isAI);
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
            if (partyPausePanel) partyPausePanel.OnPlayerLeft(name);
        }

        [ClientRpc]
        void ReinitializePanelWithPlayers_ClientRpc()
        {
            if (partyPausePanel) partyPausePanel.Initialize(config.TotalRounds, _playerStates);
        }

        [ClientRpc]
        void ShowPanel_ClientRpc()
        {
            if (partyPausePanel) partyPausePanel.ForceShow();
        }

        [ClientRpc]
        void HidePanel_ClientRpc()
        {
            if (partyPausePanel) partyPausePanel.Hide();
        }

        [ClientRpc]
        void DisableSceneCamera_ClientRpc()
        {
            if (sceneCamera) sceneCamera.enabled = false;
        }

        [ClientRpc]
        void EnableSceneCamera_ClientRpc()
        {
            if (sceneCamera) sceneCamera.enabled = true;
        }

        [ClientRpc]
        void SetVesselsSpawned_ClientRpc()
        {
            _vesselsSpawned = true;
        }

        [ClientRpc]
        void StartGameplay_ClientRpc()
        {
            PauseSystem.TogglePauseGame(false);
            gameData.InitializeGame();

            var countdownTimer = FindActiveCountdownTimer();
            if (countdownTimer)
            {
                countdownTimer.BeginCountdown(() =>
                {
                    gameData.SetPlayersActive();
                    gameData.StartTurn();
                });
            }
            else
            {
                gameData.SetPlayersActive();
                gameData.StartTurn();
            }
        }

        [ClientRpc]
        void ActivateMiniGameEnvironment_ClientRpc(int miniGameIndex, int spviMode)
        {
            DeactivateAllEnvironments();
            _activeMiniGameIndex = miniGameIndex;

            if (miniGameIndex < 0 || miniGameIndex >= miniGameEnvironments.Count)
            {
                CSDebug.LogWarning($"[PartyGame] ActivateEnv: index {miniGameIndex} out of range");
                return;
            }

            var env = miniGameEnvironments[miniGameIndex];
            if (!env) return;

            // Set party mode BEFORE activation triggers OnNetworkSpawn
            var partyModeState = (ServerPlayerVesselInitializer.PartyModeState)spviMode;
            SetPartyModeOnEnvironment(env, partyModeState);

            env.SetActive(true);
            EnableGameCanvas(env);
            CSDebug.Log($"[PartyGame] Activated: '{env.name}' (SPVI={partyModeState}, GameCanvas enabled)");

            // Initialize segment spawner if present
            var spawner = env.GetComponentInChildren<SegmentSpawner>();
            if (spawner) spawner.Initialize();

            UpdateSpawnPositionsFromEnv(env);

            // Tell the mini-game controller it's time to set up for gameplay
            _activeMiniGameController = env.GetComponentInChildren<MultiplayerMiniGameControllerBase>(true);
            if (_activeMiniGameController != null)
            {
                _activeMiniGameController.PartyMode_Activate();
                CSDebug.Log($"[PartyGame] PartyMode_Activate on {_activeMiniGameController.GetType().Name}");
            }
        }

        [ClientRpc]
        void DeactivateCurrentMiniGame_ClientRpc()
        {
            if (_activeMiniGameController != null)
            {
                _activeMiniGameController.PartyMode_Deactivate();
                CSDebug.Log($"[PartyGame] PartyMode_Deactivate on {_activeMiniGameController.GetType().Name}");
                _activeMiniGameController = null;
            }
        }

        [ClientRpc]
        void DeactivateAllEnvironments_ClientRpc()
        {
            DeactivateAllEnvironments();
        }

        [ClientRpc]
        void ResetGameDataForRound_ClientRpc(int gameMode)
        {
            gameData.GameMode = (GameModes)gameMode;
            if (gameData.RoundStatsList != null && gameData.RoundStatsList.Count > 0)
                gameData.ResetStatsDataForReplay();
        }

        /// <summary>
        /// Repositions existing player vessels to the spawn points of the given environment.
        /// Uses PartyVesselSpawner which is on an always-active GameObject (safe RPCs).
        /// </summary>
        [ClientRpc]
        void RepositionPlayersViaSpawner_ClientRpc(int envIndex)
        {
            if (envIndex < 0 || envIndex >= miniGameEnvironments.Count) return;
            var env = miniGameEnvironments[envIndex];
            if (!env) return;

            var spvi = env.GetComponentInChildren<ServerPlayerVesselInitializer>(true);
            if (spvi == null || spvi.PlayerOrigins == null || spvi.PlayerOrigins.Length == 0)
            {
                CSDebug.LogWarning($"[PartyGame] No spawn origins on '{env.name}' for repositioning.");
                return;
            }

            if (vesselSpawner)
            {
                vesselSpawner.RepositionForNewRound(spvi.PlayerOrigins);
            }
            else
            {
                // Fallback: do it inline if spawner not assigned
                gameData.SetSpawnPositions(spvi.PlayerOrigins);
                gameData.ResetPlayers();
                if (CameraManager.Instance)
                    CameraManager.Instance.SnapPlayerCameraToTarget();
            }

            CSDebug.Log($"[PartyGame] Repositioned players to '{env.name}' spawn points.");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Sets IsPartyMode on all controllers and PartyMode on all SPVIs within an environment.
        /// Called BEFORE env.SetActive(true) so OnNetworkSpawn sees the flags.
        /// </summary>
        void SetPartyModeOnEnvironment(GameObject env, ServerPlayerVesselInitializer.PartyModeState spviMode)
        {
            var controllers = env.GetComponentsInChildren<MiniGameControllerBase>(true);
            foreach (var ctrl in controllers)
            {
                ctrl.IsPartyMode = true;
            }

            var spvis = env.GetComponentsInChildren<ServerPlayerVesselInitializer>(true);
            foreach (var spvi in spvis)
            {
                spvi.PartyMode = spviMode;
                CSDebug.Log($"[PartyGame] Set SPVI PartyMode={spviMode} on '{env.name}'");
            }
        }

        /// <summary>
        /// Re-enables the GameCanvas in the environment so the MiniGameHUD, countdown timer,
        /// and gameplay UI are visible. The IsPartyMode flag on the controller already
        /// suppresses the mini-game's own ready button and lifecycle UI.
        /// </summary>
        void EnableGameCanvas(GameObject env)
        {
            var gameCanvas = env.GetComponentInChildren<GameCanvas>(true);
            if (gameCanvas)
            {
                var canvas = gameCanvas.GetComponent<Canvas>();
                if (canvas) canvas.enabled = true;
                gameCanvas.gameObject.SetActive(true);
                CSDebug.Log($"[PartyGame] GameCanvas enabled on '{env.name}'");
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

        void UpdateSpawnPositionsFromEnv(GameObject env)
        {
            var spvi = env.GetComponentInChildren<ServerPlayerVesselInitializer>(true);
            if (spvi && spvi.PlayerOrigins is { Length: > 0 })
            {
                gameData.SetSpawnPositions(spvi.PlayerOrigins);
                CSDebug.Log($"[PartyGame] Spawn positions from '{env.name}' ({spvi.PlayerOrigins.Length} origins)");
            }
            else
            {
                CSDebug.LogWarning($"[PartyGame] No PlayerOrigins found in '{env.name}'");
            }
        }

        void DeactivateAllEnvironments()
        {
            _activeMiniGameIndex = -1;
            _activeMiniGameController = null;
            foreach (var env in miniGameEnvironments)
                if (env) env.SetActive(false);
        }

        #endregion

        #region Public API

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