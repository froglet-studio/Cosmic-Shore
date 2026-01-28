using System.Globalization;
using CosmicShore.Soap;
using CosmicShore.Utilities;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Main controller for the mini-game HUD system.
    /// Manages the view, score updates, and coordinates with other UI components.
    /// </summary>
    [RequireComponent(typeof(MiniGameHUDView))]
    public class MiniGameHUD : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] protected GameDataSO gameData;
        
        [Header("View")]
        [SerializeField] private MiniGameHUDView view;

        [Header("Related UI Components")]
        [SerializeField] private Scoreboard scoreboard;

        [Header("Event Channels")]
        [SerializeField] private ScriptableEventInt onMoundDroneSpawned;
        [SerializeField] private ScriptableEventInt onQueenDroneSpawned;
        [SerializeField] private ScriptableEventSilhouetteData onSilhouetteInitialized;
        [SerializeField] private ScriptableEventShipHUDData onShipHUDInitialized;
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;
        
        private IRoundStats localRoundStats;
        
        private void OnValidate()
        {
            view = GetComponent<MiniGameHUDView>();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
            CleanupUI();
            ToggleReadyButton(false);
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #region Event Subscription

        private void SubscribeToEvents()
        {
            // Game lifecycle events
            gameData.OnClientReady += OnClientReady;
            gameData.OnMiniGameTurnStarted.OnRaised += OnMiniGameTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnMiniGameTurnEnd;
            OnResetForReplay.OnRaised += ResetForReplay;
            
            // Gameplay events
            onMoundDroneSpawned.OnRaised += OnMoundDroneSpawned;
            onQueenDroneSpawned.OnRaised += OnQueenDroneSpawned;
            onSilhouetteInitialized.OnRaised += OnSilhouetteInitialized;
            
            // Ship HUD events
            onShipHUDInitialized.OnRaised += OnShipHUDInitialized;
        }

        private void UnsubscribeFromEvents()
        {
            // Game lifecycle events
            gameData.OnClientReady -= OnClientReady;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnMiniGameTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnMiniGameTurnEnd;
            OnResetForReplay.OnRaised -= ResetForReplay;
            
            // Gameplay events
            onMoundDroneSpawned.OnRaised -= OnMoundDroneSpawned;
            onQueenDroneSpawned.OnRaised -= OnQueenDroneSpawned;
            onSilhouetteInitialized.OnRaised -= OnSilhouetteInitialized;
            
            // Ship HUD events
            onShipHUDInitialized.OnRaised -= OnShipHUDInitialized;
        }

        #endregion

        #region Game Lifecycle Event Handlers

        private void OnMiniGameTurnStarted()
        {
            localRoundStats = gameData.LocalRoundStats;
            localRoundStats.OnScoreChanged += UpdateScoreUI;
        }

        private void OnMiniGameTurnEnd()
        {
            if (localRoundStats != null)
            {
                localRoundStats.OnScoreChanged -= UpdateScoreUI;
            }
            
            UpdateTurnMonitorDisplay(string.Empty);
            UpdateLifeformCounterDisplay(string.Empty);
        }

        private void OnClientReady() => ResetForReplay();
        
        private void ResetForReplay()
        {
            ToggleReadyButton(true);
            view.ToggleConnectingPanel(false);
            CleanupUI();
        }

        #endregion

        #region Gameplay Event Handlers

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

        /// <summary>
        /// Handles ship HUD initialization by replacing the current HUD with the new one.
        /// This allows for dynamic ship-specific HUDs.
        /// </summary>
        private void OnShipHUDInitialized(ShipHUDData data)
        {
            if (!data.ShipHUD) return;

            // Hide current HUD
            Hide();
            
            // Transfer children to the new HUD's parent (this GameObject's parent)
            foreach (Transform child in data.ShipHUD.GetComponentsInChildren<Transform>(false))
            {
                if (child == data.ShipHUD.transform) continue;
                
                child.SetParent(transform.parent, false);
                child.SetSiblingIndex(0);
            }
            
            // Activate the new ship HUD
            data.ShipHUD.gameObject.SetActive(true);
        }

        #endregion

        #region UI Update Methods

        private void UpdateScoreUI()
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
        }

        #endregion

        #region Public API

        public void Show() => view.ToggleView(true);
        public void Hide() => view.ToggleView(false);
        public void ToggleReadyButton(bool toggle) => view.ReadyButton.gameObject.SetActive(toggle);
        public void UpdateTurnMonitorDisplay(string message) => view.UpdateCountdownTimer(message);
        public void UpdateLifeformCounterDisplay(string message) => view.UpdateLifeFormCounter(message);

        #endregion
    }
}