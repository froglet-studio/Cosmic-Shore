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
using Reflex.Core;
using Reflex.Injectors;
namespace CosmicShore.UI
{
    [RequireComponent(typeof(MiniGameHUDView))]
    public class MiniGameHUD : MonoBehaviour
    {
        [Header("Data")]
        [Inject] protected GameDataSO gameData;
        [Inject] protected Container _diContainer;

        [Header("Objective Indicator")]
        [Tooltip("Auto-creates an off-screen objective indicator at runtime when the game mode supports one (HexRace, Joust). Disable to wire your own indicator manually in the scene.")]
        [SerializeField] protected bool autoCreateObjectiveIndicator = true;
        ObjectiveIndicator _autoCreatedIndicator;

        [Header("View")]
        [SerializeField] protected MiniGameHUDView view;

        [Header("Related UI Components")]
        [SerializeField] private Scoreboard scoreboard;

        [Header("Volume / Pause Button (universal domain-volume gauge)")]
        [Tooltip("The in-game pause button. The domain-volume hex gauge is auto-attached to it so the SAME gauge trains as the pause button across every gameplay scene. Leave null to auto-find a button named \"Volume / Pause Button\" under the HUD canvas.")]
        [SerializeField] protected UnityEngine.UI.Button volumePauseButton;
        [Tooltip("Optional pre-placed indicator. Leave null to attach/create one on the volume-pause button at runtime.")]
        [SerializeField] protected DomainVolumeIndicator volumeIndicator;

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
            SubscribeToEvents();

            EnsureObjectiveIndicator();
            EnsureVolumeIndicator();

            // If OnClientReady already fired before we subscribed (client race condition:
            // RPCs can resolve in the same frame as scene load, before Start() runs),
            // handle it now. LocalPlayer.Vessel being set means InitializePair() already ran.
            if (gameData?.LocalPlayer?.Vessel != null)
            {
                HandleClientReady().Forget();
            }
        }

        /// <summary>
        /// Auto-creates an off-screen objective indicator and the matching
        /// provider for the current game mode, if the scene doesn't already
        /// have one. The indicator is created even without a matching provider
        /// so the UI element is always visible in debug mode — useful for
        /// verifying that rendering works before wiring up provider logic.
        /// </summary>
        protected virtual void EnsureObjectiveIndicator()
        {
            if (!autoCreateObjectiveIndicator) return;
            if (FindAnyObjectByType<ObjectiveIndicator>() != null) return;

            IObjectiveProvider provider = gameData != null
                ? CreateObjectiveProviderForGameMode(gameData.GameMode)
                : null;

            Transform canvasRoot = transform.parent != null ? transform.parent : transform;
            _autoCreatedIndicator = ObjectiveIndicator.CreateRuntime(canvasRoot, provider);
        }

        /// <summary>
        /// Attaches the universal domain-volume hex gauge to this scene's "Volume /
        /// Pause Button" so the same gauge trains players as the in-game pause button
        /// everywhere (it mirrors MenuMiniGameHUD.EnsureDomainVolumeIndicator for the
        /// menu). The button keeps its authored onClick (open PauseMenu) — the gauge
        /// only replaces the button's face. Runs in Start so the injected gameData can
        /// be handed to the runtime-added component (which never gets Reflex injection).
        /// </summary>
        protected virtual void EnsureVolumeIndicator()
        {
            var button = ResolveVolumePauseButton();
            if (!button) return;

            if (!volumeIndicator)
                volumeIndicator = button.GetComponent<DomainVolumeIndicator>();
            if (!volumeIndicator)
                volumeIndicator = button.gameObject.AddComponent<DomainVolumeIndicator>();

            volumeIndicator.SetGameData(gameData);
        }

        /// <summary>
        /// Returns the serialized volume-pause button, or auto-finds one named
        /// "Volume / Pause Button" under the HUD canvas so the gauge appears without
        /// per-prefab wiring (the GameCanvas prefabs already carry that button).
        /// </summary>
        UnityEngine.UI.Button ResolveVolumePauseButton()
        {
            if (volumePauseButton) return volumePauseButton;

            var canvas = GetComponentInParent<Canvas>();
            Transform root = canvas ? canvas.transform : transform.root;
            foreach (var btn in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (btn && btn.name == "Volume / Pause Button")
                {
                    volumePauseButton = btn;
                    return btn;
                }
            }
            return null;
        }

        IObjectiveProvider CreateObjectiveProviderForGameMode(GameModes mode)
        {
            switch (mode)
            {
                case GameModes.HexRace:
                    return CreateProviderComponent<HexRaceObjectiveProvider>("ObjectiveProvider_HexRace");
                case GameModes.MultiplayerJoust:
                    return CreateProviderComponent<JoustObjectiveProvider>("ObjectiveProvider_Joust");
                default:
                    return null;
            }
        }

        T CreateProviderComponent<T>(string objectName) where T : MonoBehaviour, IObjectiveProvider
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var component = go.AddComponent<T>();
            if (_diContainer != null)
                GameObjectInjector.InjectRecursive(go, _diContainer);
            return component;
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
            HandleClientReady().Forget();
        }

        private async UniTaskVoid HandleClientReady()
        {
            var ct = _lifecycleCts?.Token ?? CancellationToken.None;

            try
            {
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
                    if (playerTarget != null)
                    {
                        bool cinematicDone = false;
                        preGameCinematic.OnCinematicFinished += () => cinematicDone = true;
                        preGameCinematic.Play(cinematicLookAtCenter, playerTarget);

                        while (!cinematicDone)
                            await UniTask.Yield(PlayerLoopTiming.PreUpdate, ct);
                    }
                }

                _readyButtonUnlocked = true;
                ToggleReadyButton(true);
            }
            catch (OperationCanceledException)
            {
                // cancelled — nothing to do
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
            var teamColor = ResolveDomainColor(localPlayer.RoundStats?.Domain ?? Domains.Jade);
            card.Setup(localPlayer.Name, 0, teamColor, true, 0);

            var sprite = ResolveAvatarSprite(localPlayer.AvatarId);
            card.SetAvatar(sprite);

            _localPlayerCard = card;
        }

        /// <summary>
        /// Single source of truth for a domain's UI color — the same ColorSet the vessels
        /// and prisms read from (ThemeManagerData.ColorSet -> TrailHighlightColor). Neutral
        /// gray if the theme isn't available. See Docs/ScoringSystem/REFACTOR.md R5.
        /// </summary>
        protected Color ResolveDomainColor(Domains domain) =>
            gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIColor(domain)
                : Color.gray;

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
                var teamColor = ResolveDomainColor(stats.Domain);
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
