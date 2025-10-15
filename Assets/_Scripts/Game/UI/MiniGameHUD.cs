using CosmicShore.SOAP;
using CosmicShore.Utilities;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    [RequireComponent(typeof(MiniGameHUDView))]
    public class MiniGameHUD : MonoBehaviour, IMiniGameHUDController
    {
        public MiniGameHUDView View => view;

        [SerializeField]
        GameDataSO gameData;
        
        [Header("View")]
        [SerializeField] private MiniGameHUDView view;

        [Header("Event Channels")]

        // [SerializeField] private IntEventChannelSO onMoundDroneSpawned;
        [SerializeField] private ScriptableEventInt onMoundDroneSpawned;
        // [SerializeField] private IntEventChannelSO onQueenDroneSpawned;
        [SerializeField] private ScriptableEventInt onQueenDroneSpawned;
        // [SerializeField] private BoolEventChannelSO onBottomEdgeButtonsEnabled;
        [SerializeField] protected ScriptableEventBool onBottomEdgeButtonsEnabled;
        
        // [SerializeField] private SilhouetteEventChannelSO onSilhouetteInitialized;
        [SerializeField] private ScriptableEventSilhouetteData onSilhouetteInitialized;
        
        [SerializeField] ScriptableEventNoParam OnResetForReplay;
        
        private void OnValidate()
        {
            // auto-assign the view if omitted
            view = GetComponent<MiniGameHUDView>();
        }

        private void OnEnable()
        {
            ToggleReadyButton(false);
            UpdateTurnMonitorDisplay(string.Empty);
            
            gameData.OnClientReady += OnClientReady;
            OnResetForReplay.OnRaised += ResetForReplay;
            
            // SO ? Controller
            onMoundDroneSpawned.OnRaised += OnMoundDroneSpawned;
            onQueenDroneSpawned.OnRaised += OnQueenDroneSpawned;
            // onBottomEdgeButtonsEnabled.OnEventRaised += OnBottomEdgeButtonsEnabled;
            onBottomEdgeButtonsEnabled.OnRaised += OnBottomEdgeButtonsEnabled;
            onSilhouetteInitialized.OnRaised += OnSilhouetteInitialized;

            // View ? Controller
            view.Initialize(this);
        }

        private void OnDisable()
        {
            gameData.OnClientReady -= OnClientReady;
            OnResetForReplay.OnRaised -= ResetForReplay;
            
            onMoundDroneSpawned.OnRaised -= OnMoundDroneSpawned;
            onQueenDroneSpawned.OnRaised -= OnQueenDroneSpawned;
            // onBottomEdgeButtonsEnabled.OnEventRaised -= OnBottomEdgeButtonsEnabled;
            onBottomEdgeButtonsEnabled.OnRaised -= OnBottomEdgeButtonsEnabled;
            onSilhouetteInitialized.OnRaised -= OnSilhouetteInitialized;
        }

        // IMiniGameHUDController
        public void OnButtonPressed(int buttonNumber)
        {
            // switch (buttonNumber)
            // {
            //     case 1:
            //         onButton1Pressed.RaiseEvent(InputEvents.Button1Action);
            //         break;
            //     case 2:
            //         onButton2Pressed.RaiseEvent(InputEvents.Button2Action);
            //         break;
            //     case 3:
            //         onButton3Pressed.RaiseEvent(InputEvents.Button3Action);
            //         break;
            // }
        }

        public void OnButtonReleased(int buttonNumber)
        {
            // switch (buttonNumber)
            // {
            //     case 1:
            //         onButton1Released.RaiseEvent(InputEvents.Button1Action);
            //         break;
            //     case 2:
            //         onButton2Released.RaiseEvent(InputEvents.Button2Action);
            //         break;
            //     case 3:
            //         onButton3Released.RaiseEvent(InputEvents.Button3Action);
            //         break;
            // }
        }

        // � SO event handlers call into the view �D

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

        private void OnBottomEdgeButtonsEnabled(bool enabled)
        {
            view.ButtonPanel.PositionButtons(enabled);
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
            data.Sender.SetSilhouetteReference(sil.transform, trail.transform);
        }
        
        private void OnClientReady() => ResetForReplay();
        
        void ResetForReplay()
        {
            ToggleReadyButton(true);
            UpdateTurnMonitorDisplay(string.Empty);
        }

        // Public methods you may call externally:
        public void Show() => view.gameObject.SetActive(true);
        public void Hide() => view.gameObject.SetActive(false);

        public void ToggleReadyButton(bool toggle) => view.ReadyButton.gameObject.SetActive(toggle);
        public void UpdateTurnMonitorDisplay(string message) => view.RoundTimeDisplay.text = message;
        
    }
}
