using CosmicShore.Game.Arcade;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class MiniGameHUDView : MonoBehaviour, IMiniGameHUDView
    {
        [Header("Common Elements")]
        [SerializeField] private TMP_Text scoreDisplay;
        [SerializeField] private TMP_Text leftNumberDisplay;
        [SerializeField] private TMP_Text rightNumberDisplay;
        [SerializeField] private TMP_Text roundTimeDisplay;
        [SerializeField] private Image countdownDisplay;
        [SerializeField] private Button readyButton;
        [SerializeField] private GameObject pip;
        [SerializeField] private GameObject silhouette;
        [SerializeField] private GameObject trailDisplay;
        [SerializeField] private CanvasGroup connectingPanelCanvasGroup; 
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text lifeFormCounter;
        
        public void UpdateScoreUI(string message) => scoreDisplay.text = message;
        public void UpdateCountdownTimer(string message) => roundTimeDisplay.text = message;
        public void UpdateLifeFormCounter(string message) 
        {
            if (lifeFormCounter)
                lifeFormCounter.text = message;
        }
        
        public void ToggleView(bool active)
        {
            canvasGroup.alpha = active ? 1 : 0;
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
        }

        public void ToggleConnectingPanel(bool active)
        {
            connectingPanelCanvasGroup.alpha = active ? 1 : 0;
            connectingPanelCanvasGroup.interactable = active;
            connectingPanelCanvasGroup.blocksRaycasts = active;
        }
        
        public TMP_Text LeftNumberDisplay => leftNumberDisplay;
        public TMP_Text RightNumberDisplay => rightNumberDisplay;
        public Button ReadyButton => readyButton;
        public GameObject Pip => pip;
        public GameObject Silhouette => silhouette;
        public GameObject TrailDisplay => trailDisplay;
    }
}
