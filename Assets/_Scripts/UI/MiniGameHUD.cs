using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using Reflex.Attributes;
namespace CosmicShore.UI
{
    [RequireComponent(typeof(MiniGameHUDView))]
    public class MiniGameHUD : MonoBehaviour
    {
        [Header("Data")]
        [Inject] protected GameDataSO gameData;

        [Header("View")]
        [SerializeField] protected MiniGameHUDView view;

        [Header("Related UI Components")]
        [SerializeField] private Scoreboard scoreboard;

        [Header("Event Channels")]
        [SerializeField] private ScriptableEventInt onMoundDroneSpawned;
        [SerializeField] private ScriptableEventInt onQueenDroneSpawned;
        [SerializeField] private ScriptableEventSilhouetteData onSilhouetteInitialized;
        [SerializeField] private ScriptableEventShipHUDData onShipHUDInitialized;
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        [Header("Pre-Game Cinematic")]
        [SerializeField] private PreGameCinematicController preGameCinematic;
        [SerializeField] private Vector3 cinematicLookAtCenter = Vector3.zero;
        [SerializeField] private bool enablePreGameCinematic = true;

        [Header("AI Tracking")]
        [SerializeField] protected bool isAIAvailable;

        [Header("Avatar Icons")]
        [SerializeField] protected SO_ProfileIconList profileIconList;
        [SerializeField] protected SO_AIProfileList aiProfileList;

        protected IRoundStats localRoundStats;
        protected Dictionary<string, PlayerScoreEntry> _aiCards = new();
        private Dictionary<IRoundStats, Action> _aiScoreHandlers = new();
        private PlayerScoreEntry _localPlayerCard;
        private Dictionary<string, AIProfile> _assignedAIProfiles = new();

        private CancellationTokenSource _lifecycleCts;
        private bool _readyButtonUnlocked;

        private void OnValidate()
        {
            if (view == null) view = GetComponent<MiniGameHUDView>();
        }

        private void Awake()
        {
            if (enablePreGameCinematic && preGameCinematic == null)
                EnsurePreGameCinematic();
        }

        /// <summary>
        /// Auto-creates a PreGameCinematicController with a skip button
        /// when none is assigned via Inspector.
        /// </summary>
        private void EnsurePreGameCinematic()
        {
            // Check if one already exists in the scene
            preGameCinematic = FindAnyObjectByType<PreGameCinematicController>();
            if (preGameCinematic != null) return;

            // Create the cinematic controller
            var cinematicGO = new GameObject("PreGameCinematic");
            cinematicGO.transform.SetParent(transform.parent, false);
            preGameCinematic = cinematicGO.AddComponent<PreGameCinematicController>();

            // Create skip button on the HUD canvas
            var skipGO = new GameObject("SkipCinematicButton");
            skipGO.transform.SetParent(transform.parent, false);

            var skipRect = skipGO.AddComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(1f, 0f);
            skipRect.anchorMax = new Vector2(1f, 0f);
            skipRect.pivot = new Vector2(1f, 0f);
            skipRect.anchoredPosition = new Vector2(-30f, 30f);
            skipRect.sizeDelta = new Vector2(120f, 45f);

            var skipImage = skipGO.AddComponent<Image>();
            skipImage.color = new Color(0f, 0f, 0f, 0.5f);

            var skipButton = skipGO.AddComponent<Button>();
            skipButton.targetGraphic = skipImage;

            // Add text label
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(skipGO.transform, false);

            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmpText = textGO.AddComponent<TextMeshProUGUI>();
            tmpText.text = "SKIP >";
            tmpText.fontSize = 18;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;

            var skipCanvasGroup = skipGO.AddComponent<CanvasGroup>();

            preGameCinematic.SetupSkipButton(skipButton, skipCanvasGroup);
        }

        protected virtual void OnEnable()
        {
            _lifecycleCts?.Cancel();
            _lifecycleCts?.Dispose();
            _lifecycleCts = new CancellationTokenSource();

            _readyButtonUnlocked = false;
            CleanupUI();
        }

        protected virtual void Start()
        {
            Debug.Log($"<color=#FFFFFF><b>[FLOW-HUD] [MiniGameHUD] Start — gameData={gameData != null}, enableCinematic={enablePreGameCinematic}</b></color>");
            SubscribeToEvents();

            // If OnClientReady already fired before we subscribed (client race condition:
            // RPCs can resolve in the same frame as scene load, before Start() runs),
            // handle it now. LocalPlayer.Vessel being set means InitializePair() already ran.
            if (gameData?.LocalPlayer?.Vessel != null)
            {
                Debug.Log("<color=#FFFFFF><b>[FLOW-8] [MiniGameHUD] Late subscription — OnClientReady already fired, handling now</b></color>");
                HandleClientReady().Forget();
            }
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromEvents();

            _lifecycleCts?.Cancel();
            _lifecycleCts?.Dispose();
            _lifecycleCts = null;
        }

        protected virtual void SubscribeToEvents()
        {
            if (gameData != null)
            {
                gameData.OnClientReady.OnRaised += OnClientReady;
                gameData.OnMiniGameTurnStarted.OnRaised += OnMiniGameTurnStarted;
                gameData.OnMiniGameTurnStarted.OnRaised += ShowLocalVesselHUD;
                gameData.OnMiniGameTurnEnd.OnRaised += OnMiniGameTurnEnd;

                var resetEvent = OnResetForReplay != null ? OnResetForReplay : gameData.OnResetForReplay;
                if (resetEvent != null) resetEvent.OnRaised += ResetForReplay;
            }

            if (onMoundDroneSpawned != null) onMoundDroneSpawned.OnRaised += OnMoundDroneSpawned;
            if (onQueenDroneSpawned != null) onQueenDroneSpawned.OnRaised += OnQueenDroneSpawned;
            if (onSilhouetteInitialized != null) onSilhouetteInitialized.OnRaised += OnSilhouetteInitialized;
            if (onShipHUDInitialized != null) onShipHUDInitialized.OnRaised += OnShipHUDInitialized;
        }

        protected virtual void UnsubscribeFromEvents()
        {
            if (gameData != null)
            {
                gameData.OnClientReady.OnRaised -= OnClientReady;
                gameData.OnMiniGameTurnStarted.OnRaised -= OnMiniGameTurnStarted;
                gameData.OnMiniGameTurnStarted.OnRaised -= ShowLocalVesselHUD;
                gameData.OnMiniGameTurnEnd.OnRaised -= OnMiniGameTurnEnd;

                var resetEvent = OnResetForReplay != null ? OnResetForReplay : gameData.OnResetForReplay;
                if (resetEvent != null) resetEvent.OnRaised -= ResetForReplay;
            }

            if (onMoundDroneSpawned != null) onMoundDroneSpawned.OnRaised -= OnMoundDroneSpawned;
            if (onQueenDroneSpawned != null) onQueenDroneSpawned.OnRaised -= OnQueenDroneSpawned;
            if (onSilhouetteInitialized != null) onSilhouetteInitialized.OnRaised -= OnSilhouetteInitialized;
            if (onShipHUDInitialized != null) onShipHUDInitialized.OnRaised -= OnShipHUDInitialized;
        }

        private void OnClientReady()
        {
            Debug.Log("<color=#FFFFFF><b>[FLOW-8] [MiniGameHUD] OnClientReady received!</b></color>");
            HandleClientReady().Forget();
        }

        private async UniTaskVoid HandleClientReady()
        {
            var ct = _lifecycleCts?.Token ?? CancellationToken.None;

            try
            {
                Debug.Log($"<color=#FFFFFF><b>[FLOW-8] [MiniGameHUD] HandleClientReady — LocalPlayer={gameData?.LocalPlayer?.Name}, Vessel={gameData?.LocalPlayer?.Vessel != null}</b></color>");
                Show();
                CleanupUI();
                HideLocalVesselHUD();
                UpdateTurnMonitorDisplay(string.Empty);
                UpdateLifeformCounterDisplay("0");
                view.UpdateScoreUI("0");
                ToggleReadyButton(false);

                // Play pre-game cinematic if available
                if (enablePreGameCinematic && preGameCinematic != null)
                {
                    Transform playerTarget = gameData?.LocalPlayer?.Vessel?.Transform;
                    Debug.Log($"<color=#FFFFFF><b>[FLOW-8] [MiniGameHUD] Pre-game cinematic: enabled={enablePreGameCinematic}, controller={preGameCinematic != null}, playerTarget={playerTarget != null}</b></color>");
                    if (playerTarget != null)
                    {
                        bool cinematicDone = false;
                        preGameCinematic.OnCinematicFinished += () => cinematicDone = true;
                        preGameCinematic.Play(cinematicLookAtCenter, playerTarget);

                        while (!cinematicDone)
                            await UniTask.Yield(PlayerLoopTiming.PreUpdate, ct);
                        Debug.Log("<color=#FFFFFF><b>[FLOW-8] [MiniGameHUD] Pre-game cinematic DONE</b></color>");
                    }
                }

                Debug.Log("<color=#FFFFFF><b>[FLOW-8] [MiniGameHUD] ToggleReadyButton(true) — Ready button visible</b></color>");
                _readyButtonUnlocked = true;
                ToggleReadyButton(true);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("<color=#FFA500>[FLOW-8] [MiniGameHUD] HandleClientReady CANCELLED</color>");
            }
        }

        private void ResetForReplay()
        {
            Show();
            _readyButtonUnlocked = true;
            CleanupUI();
            HideLocalVesselHUD();

            UpdateTurnMonitorDisplay(string.Empty);
            UpdateLifeformCounterDisplay("0");
            view.UpdateScoreUI("0");
        }

        protected virtual void OnMiniGameTurnStarted()
        {
            localRoundStats = gameData.LocalRoundStats;
            if (localRoundStats != null)
                localRoundStats.OnScoreChanged += UpdateScoreUI;

            SetupLocalPlayerCard();
            if (isAIAvailable) SetupAICards();
        }

        protected virtual void OnMiniGameTurnEnd()
        {
            if (localRoundStats != null)
                localRoundStats.OnScoreChanged -= UpdateScoreUI;

            CleanupLocalPlayerCard();
            if (isAIAvailable) CleanupAICards();

            UpdateTurnMonitorDisplay(string.Empty);
            UpdateLifeformCounterDisplay(string.Empty);
        }

        private void SetupLocalPlayerCard()
        {
            if (gameData.LocalPlayer == null || view.PlayerScoreEntryPrefab == null)
                return;

            // Avoid duplicate — reuse existing card if already created
            if (_localPlayerCard != null)
                return;

            var localPlayer = gameData.LocalPlayer;
            var card = Instantiate(view.PlayerScoreEntryPrefab, view.PlayerScoreContainer);
            var teamColor = view.GetColorForDomain(localPlayer.RoundStats?.Domain ?? Domains.Jade);
            card.Setup(localPlayer.Name, 0, teamColor, true, 0);

            var sprite = ResolveAvatarSprite(localPlayer.AvatarId);
            card.SetAvatar(sprite);

            _localPlayerCard = card;
        }

        private void CleanupLocalPlayerCard()
        {
            if (_localPlayerCard != null)
            {
                Destroy(_localPlayerCard.gameObject);
                _localPlayerCard = null;
            }
        }

        private void SetupAICards()
        {
            // Avoid duplicates — skip if cards already exist
            if (_aiCards.Count > 0)
                return;

            _aiScoreHandlers.Clear();
            AssignAIProfiles();

            int staggerIdx = 1; // 0 is the local player card
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == localRoundStats) continue;

                var card = Instantiate(view.PlayerScoreEntryPrefab, view.PlayerScoreContainer);
                var teamColor = view.GetColorForDomain(stats.Domain);
                card.Setup(stats.Name, (int)stats.Score, teamColor, false, staggerIdx++);

                // Resolve avatar: try AI profile first, then fall back to player AvatarId
                var avatarSprite = ResolveAIAvatarSprite(stats.Name);
                if (avatarSprite == null)
                {
                    var player = gameData.Players.FirstOrDefault(p => p.Name == stats.Name);
                    if (player != null)
                        avatarSprite = ResolveAvatarSprite(player.AvatarId);
                }
                card.SetAvatar(avatarSprite);

                _aiCards[stats.Name] = card;

                Action handler = () => UpdateAICard(stats);
                _aiScoreHandlers[stats] = handler;
                stats.OnScoreChanged += handler;
            }
        }

        private void UpdateAICard(IRoundStats stats)
        {
            if (_aiCards.TryGetValue(stats.Name, out var card))
                card.UpdateScore((int)stats.Score);
        }

        private void CleanupAICards()
        {
            foreach (var kvp in _aiScoreHandlers)
            {
                kvp.Key.OnScoreChanged -= kvp.Value;
            }
            _aiScoreHandlers.Clear();
            _aiCards.Clear();
            view.ClearPlayerList();
        }

        protected Sprite ResolveAvatarSprite(int avatarId)
        {
            if (profileIconList == null || profileIconList.profileIcons == null)
                return null;

            foreach (var icon in profileIconList.profileIcons)
            {
                if (icon.Id == avatarId)
                    return icon.IconSprite;
            }

            return profileIconList.profileIcons.Count > 0
                ? profileIconList.profileIcons[0].IconSprite
                : null;
        }

        /// <summary>
        /// Assigns random AI profiles from the AI profile list to each AI player.
        /// Cached in _assignedAIProfiles so the same profile is used throughout the game.
        /// Only assigns to actual AI players, not to remote human players in multiplayer.
        /// </summary>
        protected void AssignAIProfiles()
        {
            _assignedAIProfiles.Clear();
            if (aiProfileList == null || aiProfileList.aiProfiles == null || aiProfileList.aiProfiles.Count == 0)
                return;

            // Collect only actual AI players, skipping local player and remote human players
            var aiStatsList = new List<IRoundStats>();
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == localRoundStats) continue;

                var player = gameData.Players.FirstOrDefault(p => p.Name == stats.Name);
                if (player != null && !player.IsInitializedAsAI)
                    continue; // skip remote human players

                aiStatsList.Add(stats);
            }

            var picked = aiProfileList.PickRandom(aiStatsList.Count);
            for (int i = 0; i < aiStatsList.Count && i < picked.Count; i++)
            {
                _assignedAIProfiles[aiStatsList[i].Name] = picked[i];
            }
        }

        /// <summary>
        /// Returns the avatar sprite for an AI player from the assigned AI profile.
        /// Returns null if no AI profile is assigned for this player name.
        /// </summary>
        protected Sprite ResolveAIAvatarSprite(string playerName)
        {
            if (_assignedAIProfiles.TryGetValue(playerName, out var profile))
                return profile.AvatarSprite;
            return null;
        }

        private void OnMoundDroneSpawned(int count)
        {
            view.LeftNumberDisplay.transform.parent.parent.gameObject.SetActive(count > 0);
            view.LeftNumberDisplay.text = count.ToString();
        }

        private void OnQueenDroneSpawned(int count)
        {
            view.RightNumberDisplay.transform.parent.parent.gameObject.SetActive(count > 0);
            view.RightNumberDisplay.text = count.ToString();
        }

        private void OnSilhouetteInitialized(SilhouetteData data)
        {
            var sil = view.Silhouette;
            sil.SetActive(data.IsSilhouetteActive);

            var trail = view.TrailDisplay;
            trail.SetActive(data.IsTrailDisplayActive);

            foreach (var part in data.Silhouettes)
            {
                part.transform.SetParent(sil.transform, false);
                part.SetActive(true);
            }
        }

        private void OnShipHUDInitialized(ShipHUDData data)
        {
            if (!data.ShipHUD) return;

            Hide();

            foreach (Transform child in data.ShipHUD.GetComponentsInChildren<Transform>(false))
            {
                if (child == data.ShipHUD.transform) continue;
                child.SetParent(transform.parent, false);
                child.SetSiblingIndex(0);
            }

            data.ShipHUD.gameObject.SetActive(true);
        }

        protected virtual void UpdateScoreUI()
        {
            if (localRoundStats == null) return;
            var score = (int)localRoundStats.Score;
            view.UpdateScoreUI(score.ToString(CultureInfo.InvariantCulture));

            if (_localPlayerCard != null)
                _localPlayerCard.UpdateScore(score);
        }

        public void OnPipInitialized(PipData data)
        {
            view.Pip.SetActive(data.IsActive);
            view.Pip.GetComponent<PipUI>().SetMirrored(data.IsMirrored);
        }

        private void CleanupUI()
        {
            ToggleReadyButton(false);
            UpdateTurnMonitorDisplay(string.Empty);
            UpdateLifeformCounterDisplay(string.Empty);
            view.UpdateScoreUI("0");
            view.ClearPlayerList();
        }

        public void Show() => view.ToggleView(true);
        public void Hide() => view.ToggleView(false);
        public void ToggleReadyButton(bool toggle)
        {
            // Block all show requests until HandleClientReady explicitly unlocks.
            // This prevents SOAP event listeners, controller SetupNewRound, and
            // any other pathway from showing the button before the cinematic finishes.
            if (toggle && !_readyButtonUnlocked) return;
            view.ReadyButton.gameObject.SetActive(toggle);
        }

        /// <summary>
        /// Resets UI state for re-entering the game flow after shape drawing.
        /// Called externally by SinglePlayerFreestyleController.
        /// </summary>
        public void ShowConnectingFlow() => ResetForReplay();
        public void UpdateTurnMonitorDisplay(string message) => view.UpdateCountdownTimer(message);
        public void UpdateLifeformCounterDisplay(string message) => view.UpdateLifeFormCounter(message);

        private void HideLocalVesselHUD()
        {
            gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselHUDController?.HideHUD();
        }

        private void ShowLocalVesselHUD()
        {
            gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselHUDController?.ShowHUD();
        }
    }
}
