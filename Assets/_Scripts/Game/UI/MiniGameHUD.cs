using CosmicShore.Game.Arcade;
using CosmicShore.Utilities;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace CosmicShore.Game.UI
{
    public class MiniGameHUD : MonoBehaviour
    {
        public TMP_Text ScoreDisplay;
        public TMP_Text LeftNumberDisplay;
        public TMP_Text RightNumberDisplay;

        public TMP_Text RoundTimeDisplay;
        public Image CountdownDisplay;
        public Button ReadyButton;
        public CountdownTimer CountdownTimer;
        [SerializeField] GameObject pip;
        [SerializeField] GameObject silhouette;
        [SerializeField] GameObject trailDisplay;
        [SerializeField] ButtonPanel buttonPanel;
        [SerializeField] GameObject button1;
        [SerializeField] GameObject button2;
        [SerializeField] GameObject button3;

        [SerializeField]
        PipEventChannelSO OnPipInitializedEventChannel;

        [SerializeField]
        IntEventChannelSO onMoundDroneSpawned;

        [SerializeField]
        IntEventChannelSO onQueenDroneSpawned;

        [SerializeField]
        BoolEventChannelSO onBottomEdgeButtonsEnabled;

        [SerializeField]
        InputEventsEventChannelSO OnButton1Pressed;

        [SerializeField]
        InputEventsEventChannelSO OnButton1Released;

        [SerializeField]
        InputEventsEventChannelSO OnButton2Pressed;

        [SerializeField]
        InputEventsEventChannelSO OnButton2Released;

        [SerializeField]
        InputEventsEventChannelSO OnButton3Pressed;

        [SerializeField]
        InputEventsEventChannelSO OnButton3Released;

        [SerializeField]
        SilhouetteEventChannelSO OnSilhouetteInitialized;

        // public IShip Ship { get; set; }

        private void OnEnable()
        {
            OnPipInitializedEventChannel.OnEventRaised += OnPipInitialized;
            onMoundDroneSpawned.OnEventRaised += SetLeftNumberDisplay;
            onQueenDroneSpawned.OnEventRaised += SetRightNumberDisplay;
            onBottomEdgeButtonsEnabled.OnEventRaised += PositionButtonPanel;
            OnSilhouetteInitialized.OnEventRaised += SetupForSilhouette;
        }

        private void OnDisable()
        {
            OnPipInitializedEventChannel.OnEventRaised -= OnPipInitialized;
            onMoundDroneSpawned.OnEventRaised -= SetLeftNumberDisplay;
            onQueenDroneSpawned.OnEventRaised -= SetRightNumberDisplay;
            onBottomEdgeButtonsEnabled.OnEventRaised -= PositionButtonPanel;
            OnSilhouetteInitialized.OnEventRaised -= SetupForSilhouette;
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        void OnPipInitialized(PipEventData data) =>
            SetPipActive(data.IsActive, data.IsMirrored);

        public void SetPipActive(bool active, bool mirrored)
        {
            pip.SetActive(active);
            pip.GetComponent<PipUI>().SetMirrored(mirrored);
        }

        public GameObject SetSilhouetteActive(bool active)
        {
            silhouette.SetActive(active);
            return silhouette;
        }

        public GameObject SetTrailDisplayActive(bool active)
        {
            trailDisplay.SetActive(active);
            return trailDisplay;
        }

        public void SetLeftNumberDisplay(int number)
        {
            if (number == 0)
            {
                LeftNumberDisplay.transform.parent.parent.gameObject.SetActive(false);
            }
            else
            {
                LeftNumberDisplay.transform.parent.parent.gameObject.SetActive(true);
                LeftNumberDisplay.text = number.ToString();
            }
        }

        public void SetRightNumberDisplay(int number)
        {
            if (number == 0)
            {
                RightNumberDisplay.transform.parent.parent.gameObject.SetActive(false);
            }
            else
            {
                RightNumberDisplay.transform.parent.parent.gameObject.SetActive(true);
                RightNumberDisplay.text = number.ToString();
            }
        }

        public void PositionButtonPanel(bool bottomEdge) // TODO: this should be combined with SetButtonActive to make a configuration method
        {
            buttonPanel.PositionButtons(bottomEdge);
        }

        public void SetButtonActive(bool active, int number) // TODO: this should move to ButtonPanel
        {
            switch (number)
            {
                case 1:
                    button1.SetActive(active);
                    break;
                case 2:
                    button2.SetActive(active);
                    break;
                case 3:
                    button3.SetActive(active);
                    break;
            }
        }

        public void PressButton1() // TODO: this should move to ButtonPanel
        {
            // Ship.PerformShipControllerActions(InputEvents.Button1Action);
            OnButton1Pressed.RaiseEvent(InputEvents.Button1Action);
        }

        public void releaseButton1()
        {
            // Ship.StopShipControllerActions(InputEvents.Button1Action);
            OnButton1Released.RaiseEvent(InputEvents.Button1Action);
        }

        public void PressButton2()
        {
            // Ship.PerformShipControllerActions(InputEvents.Button2Action);
            OnButton2Pressed.RaiseEvent(InputEvents.Button2Action);
        }

        public void releaseButton2()
        {
            // Ship.StopShipControllerActions(InputEvents.Button2Action);
            OnButton2Released.RaiseEvent(InputEvents.Button2Action);
        }

        public void PressButton3()
        {
            // Ship.PerformShipControllerActions(InputEvents.Button3Action);
            OnButton3Pressed.RaiseEvent(InputEvents.Button3Action);
        }

        public void releaseButton3()
        {
            // Ship.StopShipControllerActions(InputEvents.Button3Action);
            OnButton3Released.RaiseEvent(InputEvents.Button3Action);
        }

        public void SetupForSilhouette(SilhouetteData data)
        {
            var silhouetteContainer = SetSilhouetteActive(data.IsSilhouetteActive).transform;
            var trailDisplayContainer = SetTrailDisplayActive(data.IsTrailDisplayActive).transform;
            foreach (var part in data.Silhouettes)
            {
                part.transform.SetParent(silhouetteContainer.transform, false);
                part.SetActive(true);
            }
            data.Sender.SetSilhouetteReference(silhouetteContainer, trailDisplayContainer);
        }
    }
}