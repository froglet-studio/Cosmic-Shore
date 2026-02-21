using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

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

        [Header("Connecting Panel Animations")]
        [SerializeField] private DoTweenTypewriterAnimator hackerTextAnimator;
        [SerializeField] private ConnectingDotsAnimator dotsAnimator;
        [SerializeField] private string hackerText = "INITIALIZING COSMIC SHORE";

        [Header("Player/AI Score Cards")]
        [SerializeField] private Transform playerScoreContainer;
        [SerializeField] private PlayerScoreCard playerScoreCardPrefab;
        [SerializeField] private List<DomainColorDef> domainColors;

        public Transform PlayerScoreContainer => playerScoreContainer;
        public PlayerScoreCard PlayerScoreCardPrefab => playerScoreCardPrefab;

        private void Awake()
        {
            // Auto-discover connecting panel animation components when not assigned in Inspector
            if (connectingPanelCanvasGroup != null)
            {
                var panelGO = connectingPanelCanvasGroup.gameObject;
                if (hackerTextAnimator == null)
                    hackerTextAnimator = panelGO.GetComponentInChildren<DoTweenTypewriterAnimator>();
                if (dotsAnimator == null)
                    dotsAnimator = panelGO.GetComponentInChildren<ConnectingDotsAnimator>();
            }
        }

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

            if (active)
                StartConnectingAnimations();
            else
                StopConnectingAnimations();
        }

        private System.Threading.CancellationTokenSource _hackerCts;

        private void StartConnectingAnimations()
        {
            // Start hacker text animation
            if (hackerTextAnimator != null && !string.IsNullOrEmpty(hackerText))
            {
                _hackerCts?.Cancel();
                _hackerCts?.Dispose();
                _hackerCts = new System.Threading.CancellationTokenSource();
                hackerTextAnimator.PlayIn(hackerText, _hackerCts.Token).Forget();
            }

            // Start dots animation
            if (dotsAnimator != null)
                dotsAnimator.StartAnimation();
        }

        private void StopConnectingAnimations()
        {
            // Stop hacker text
            if (_hackerCts != null)
            {
                _hackerCts.Cancel();
                _hackerCts.Dispose();
                _hackerCts = null;
            }
            if (hackerTextAnimator != null)
                hackerTextAnimator.ClearInstant();

            // Stop dots animation
            if (dotsAnimator != null)
                dotsAnimator.StopAnimation();
        }

        public void ClearPlayerList()
        {
            foreach (Transform child in playerScoreContainer)
            {
                Destroy(child.gameObject);
            }
        }

        public Color GetColorForDomain(Domains domain)
        {
            var def = domainColors.FirstOrDefault(d => d.Domain == domain);
            return def.Equals(default(DomainColorDef)) ? Color.white : def.Color;
        }

        [Serializable]
        public struct DomainColorDef
        {
            public Domains Domain;
            public Color Color;
        }
        
        public TMP_Text LeftNumberDisplay => leftNumberDisplay;
        public TMP_Text RightNumberDisplay => rightNumberDisplay;
        public Button ReadyButton => readyButton;
        public GameObject Pip => pip;
        public GameObject Silhouette => silhouette;
        public GameObject TrailDisplay => trailDisplay;

        private void OnDestroy()
        {
            _hackerCts?.Cancel();
            _hackerCts?.Dispose();
            _hackerCts = null;
        }
    }
}