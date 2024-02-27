using CosmicShore.Core;
using CosmicShore.Game.Arcade;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class MiniGameHUD : MonoBehaviour
    {
        public TMP_Text ScoreDisplay;
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
        [HideInInspector] public Ship ship;

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

        public void PositionButtonPanel(bool bottomEdge) // TODO: this should be conbined with SetButtonActive to make a configuration method
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
            ship.PerformShipControllerActions(InputEvents.Button1Action);
        }

        public void releaseButton1()
        {
            ship.StopShipControllerActions(InputEvents.Button1Action);
        }

        public void PressButton2()
        {
            ship.PerformShipControllerActions(InputEvents.Button2Action);
        }

        public void releaseButton2()
        {
            ship.StopShipControllerActions(InputEvents.Button2Action);
        }

        public void PressButton3()
        {
            ship.PerformShipControllerActions(InputEvents.Button3Action);
        }

        public void releaseButton3()
        {
            ship.StopShipControllerActions(InputEvents.Button3Action);
        }
    }
}