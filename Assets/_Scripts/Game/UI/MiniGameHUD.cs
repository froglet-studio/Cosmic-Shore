using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CosmicShore.Soap;
using CosmicShore.Utilities;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    [RequireComponent(typeof(MiniGameHUDView))]
    public class MiniGameHUD : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] protected GameDataSO gameData;

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

        [Header("Intro / Connecting")]
        [SerializeField] private float minConnectingSeconds = 5f;

        [Header("AI Tracking")]
        [SerializeField] protected bool isAIAvailable;

        protected IRoundStats localRoundStats;
        protected Dictionary<string, PlayerScoreCard> _aiCards = new();
        private Dictionary<IRoundStats, Action> _aiScoreHandlers = new();

        private CancellationTokenSource _connectingCts;
        private bool _clientReady;

        protected virtual bool RequireClientReady => false;

        private void OnValidate()
        {
            if (view == null) view = GetComponent<MiniGameHUDView>();
        }

        protected virtual void OnEnable()
        {
            _clientReady = false;

            _connectingCts?.Cancel();
            _connectingCts?.Dispose();
            _connectingCts = new CancellationTokenSource();

            SubscribeToEvents();
            CleanupUI();
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromEvents();

            _connectingCts?.Cancel();
            _connectingCts?.Dispose();
            _connectingCts = null;
        }

        protected virtual void SubscribeToEvents()
        {
            if (gameData != null)
            {
                gameData.OnClientReady += OnClientReady;
                gameData.OnMiniGameTurnStarted.OnRaised += OnMiniGameTurnStarted;
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
                gameData.OnClientReady -= OnClientReady;
                gameData.OnMiniGameTurnStarted.OnRaised -= OnMiniGameTurnStarted;
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
            _clientReady = true;
            ResetForReplay();
        }

        protected virtual void OnMiniGameTurnStarted()
        {
            localRoundStats = gameData.LocalRoundStats;
            if (localRoundStats != null)
                localRoundStats.OnScoreChanged += UpdateScoreUI;

            if (isAIAvailable) SetupAICards();
        }

        protected virtual void OnMiniGameTurnEnd()
        {
            if (localRoundStats != null)
                localRoundStats.OnScoreChanged -= UpdateScoreUI;

            if (isAIAvailable) CleanupAICards();

            UpdateTurnMonitorDisplay(string.Empty);
            UpdateLifeformCounterDisplay(string.Empty);
        }

        private void SetupAICards()
        {
            view.ClearPlayerList();
            _aiCards.Clear();
            _aiScoreHandlers.Clear();

            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == localRoundStats) continue;

                var card = Instantiate(view.PlayerScoreCardPrefab, view.PlayerScoreContainer);
                var teamColor = view.GetColorForDomain(stats.Domain);
                card.Setup(stats.Name, (int)stats.Score, teamColor, false);
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

        private void ResetForReplay()
        {
            Show();
            CleanupUI();

            UpdateTurnMonitorDisplay(string.Empty);
            UpdateLifeformCounterDisplay("0");
            view.UpdateScoreUI("0");

            view.ToggleConnectingPanel(true);
            ToggleReadyButton(false);

            RunConnectingMinimum().Forget();
        }

        private async UniTaskVoid RunConnectingMinimum()
        {
            var ct = _connectingCts?.Token ?? CancellationToken.None;

            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(minConnectingSeconds),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.PreUpdate,
                    ct);

                if (RequireClientReady)
                {
                    while (!_clientReady)
                        await UniTask.Yield(PlayerLoopTiming.PreUpdate, ct);
                }

                view.ToggleConnectingPanel(false);
                ToggleReadyButton(true);
            }
            catch (OperationCanceledException) { }
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
        }

        public void OnPipInitialized(PipData data)
        {
            view.Pip.SetActive(data.IsActive);
            view.Pip.GetComponent<PipUI>().SetMirrored(data.IsMirrored);
        }

        private void CleanupUI()
        {
            UpdateTurnMonitorDisplay(string.Empty);
            UpdateLifeformCounterDisplay(string.Empty);
            view.UpdateScoreUI("0");
            if (isAIAvailable) view.ClearPlayerList();
        }

        public void Show() => view.ToggleView(true);
        public void Hide() => view.ToggleView(false);
        public void ToggleReadyButton(bool toggle) => view.ReadyButton.gameObject.SetActive(toggle);
        public void UpdateTurnMonitorDisplay(string message) => view.UpdateCountdownTimer(message);
        public void UpdateLifeformCounterDisplay(string message) => view.UpdateLifeFormCounter(message);
    }
}