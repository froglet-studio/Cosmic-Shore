using System.Globalization;
using CosmicShore.SOAP;
using CosmicShore.Utilities;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    [RequireComponent(typeof(MiniGameHUDView))]
    public class MiniGameHUD : MonoBehaviour, IMiniGameHUDController
    {
        [SerializeField]
        protected GameDataSO gameData;
        
        [Header("View")]
        [SerializeField] private MiniGameHUDView view;

        [Header("Event Channels")]
        
        [SerializeField] ScriptableEventInt onMoundDroneSpawned;
        [SerializeField] ScriptableEventInt onQueenDroneSpawned;
        [SerializeField] ScriptableEventSilhouetteData onSilhouetteInitialized;
        [SerializeField] ScriptableEventNoParam OnResetForReplay;
        
        IRoundStats localRoundStats;
        
        private void OnValidate()
        {
            // auto-assign the view if omitted
            view = GetComponent<MiniGameHUDView>();
        }

        private void OnEnable()
        {
            ToggleReadyButton(false);
            
            gameData.OnClientReady += OnClientReady;
            gameData.OnMiniGmaeTurnStarted.OnRaised += OnMiniGameTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnMiniGameTurnEnd;
            OnResetForReplay.OnRaised += ResetForReplay;
            
            onMoundDroneSpawned.OnRaised += OnMoundDroneSpawned;
            onQueenDroneSpawned.OnRaised += OnQueenDroneSpawned;
            onSilhouetteInitialized.OnRaised += OnSilhouetteInitialized;

            CleanupUI();
        }

        private void OnDisable()
        {
            gameData.OnClientReady -= OnClientReady;
            gameData.OnMiniGmaeTurnStarted.OnRaised -= OnMiniGameTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnMiniGameTurnEnd;
            OnResetForReplay.OnRaised -= ResetForReplay;
            
            onMoundDroneSpawned.OnRaised -= OnMoundDroneSpawned;
            onQueenDroneSpawned.OnRaised -= OnQueenDroneSpawned;
            onSilhouetteInitialized.OnRaised -= OnSilhouetteInitialized;
        }

        void OnMiniGameTurnStarted()
        {
            localRoundStats = gameData.LocalRoundStats;
            localRoundStats.OnScoreChanged += UpdateScoreUI;
        }

        void OnMiniGameTurnEnd()
        {
            localRoundStats.OnScoreChanged -= UpdateScoreUI;
            UpdateTurnMonitorDisplay(string.Empty);
        }

        void UpdateScoreUI()
        {
            var score = (int)localRoundStats.Score;
            view.UpdateScoreUI(score.ToString(CultureInfo.InvariantCulture));
        }

        public void OnPipInitialized(PipData data)
        {
            view.Pip.SetActive(data.IsActive);
            view.Pip.GetComponent<PipUI>().SetMirrored(data.IsMirrored);
        }

        private void OnMoundDroneSpawned(int count)
        {
            // this will show/hide and set the text
            view.LeftNumberDisplay.transform.parent.parent.gameObject.SetActive(count > 0);
            view.LeftNumberDisplay.text = count.ToString();
        }

        private void OnQueenDroneSpawned(int count)
        {
            view.RightNumberDisplay.transform.parent.parent.gameObject.SetActive(count > 0);
            view.RightNumberDisplay.text = count.ToString();
        }

        /*private void OnBottomEdgeButtonsEnabled(bool enabled)
        {
            view.ButtonPanel.PositionButtons(enabled);
        }*/

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
            //data.Sender.SetSilhouetteReference(sil.transform, trail.transform);
        }
        
        private void OnClientReady() => ResetForReplay();
        
        void ResetForReplay()
        {
            ToggleReadyButton(true);
            CleanupUI();
        }

        // Public methods you may call externally: 
        // TODO -> Use Canvas Group, dont activate/deactivate game objects -> expensive.
        public void Show() => view.gameObject.SetActive(true);
        public void Hide() => view.gameObject.SetActive(false);

        public void ToggleReadyButton(bool toggle) => view.ReadyButton.gameObject.SetActive(toggle);
        public void UpdateTurnMonitorDisplay(string message) => view.UpdateCountdownTimer(message);
        
        void CleanupUI()
        {
            UpdateTurnMonitorDisplay(string.Empty);
            view.UpdateScoreUI("0");
        }
        
    }
}
