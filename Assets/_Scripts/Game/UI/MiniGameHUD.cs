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

        [Header("View")]
        [SerializeField] private MiniGameHUDView view;

        [Header("Event Channels")]

        [SerializeField] private IntEventChannelSO onMoundDroneSpawned;
        [SerializeField] private IntEventChannelSO onQueenDroneSpawned;
        // [SerializeField] private BoolEventChannelSO onBottomEdgeButtonsEnabled;
        [SerializeField] protected ScriptableEventBool onBottomEdgeButtonsEnabled;
        [SerializeField] private InputEventsEventChannelSO onButton1Pressed;
        [SerializeField] private InputEventsEventChannelSO onButton1Released;
        [SerializeField] private InputEventsEventChannelSO onButton2Pressed;
        [SerializeField] private InputEventsEventChannelSO onButton2Released;
        [SerializeField] private InputEventsEventChannelSO onButton3Pressed;
        [SerializeField] private InputEventsEventChannelSO onButton3Released;
        [SerializeField] private SilhouetteEventChannelSO onSilhouetteInitialized;
        
        private void Reset()
        {
            // auto-assign the view if omitted
            view = GetComponent<MiniGameHUDView>();
        }

        private void OnEnable()
        {
            // SO ? Controller
            onMoundDroneSpawned.OnEventRaised += OnMoundDroneSpawned;
            onQueenDroneSpawned.OnEventRaised += OnQueenDroneSpawned;
            // onBottomEdgeButtonsEnabled.OnEventRaised += OnBottomEdgeButtonsEnabled;
            onBottomEdgeButtonsEnabled.OnRaised += OnBottomEdgeButtonsEnabled;
            onSilhouetteInitialized.OnEventRaised += OnSilhouetteInitialized;

            // View ? Controller
            view.Initialize(this);
        }

        private void OnDisable()
        {
            onMoundDroneSpawned.OnEventRaised -= OnMoundDroneSpawned;
            onQueenDroneSpawned.OnEventRaised -= OnQueenDroneSpawned;
            // onBottomEdgeButtonsEnabled.OnEventRaised -= OnBottomEdgeButtonsEnabled;
            onBottomEdgeButtonsEnabled.OnRaised -= OnBottomEdgeButtonsEnabled;
            onSilhouetteInitialized.OnEventRaised -= OnSilhouetteInitialized;
        }

        // IMiniGameHUDController
        public void OnButtonPressed(int buttonNumber)
        {
            switch (buttonNumber)
            {
                case 1:
                    onButton1Pressed.RaiseEvent(InputEvents.Button1Action);
                    break;
                case 2:
                    onButton2Pressed.RaiseEvent(InputEvents.Button2Action);
                    break;
                case 3:
                    onButton3Pressed.RaiseEvent(InputEvents.Button3Action);
                    break;
            }
        }

        public void OnButtonReleased(int buttonNumber)
        {
            switch (buttonNumber)
            {
                case 1:
                    onButton1Released.RaiseEvent(InputEvents.Button1Action);
                    break;
                case 2:
                    onButton2Released.RaiseEvent(InputEvents.Button2Action);
                    break;
                case 3:
                    onButton3Released.RaiseEvent(InputEvents.Button3Action);
                    break;
            }
        }

        // � SO event handlers call into the view �

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

        // Public methods you may call externally:
        public void Show() => view.gameObject.SetActive(true);
        public void Hide() => view.gameObject.SetActive(false);

        public void ToggleReadyButton(bool toggle) => view.ReadyButton.gameObject.SetActive(toggle);
        public void UpdateTurnMonitorDisplay(string message) => view.RoundTimeDisplay.text = message;
        
    }
}
