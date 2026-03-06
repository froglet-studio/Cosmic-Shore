using CosmicShore.Data;
using CosmicShore.UI;
using DG.Tweening;
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

        [Header("Connecting Panel")]
        [SerializeField] private ConnectingPanel connectingPanel;

        [Header("Connecting Panel Animations")]
        [SerializeField] private DoTweenTypewriterAnimator hackerTextAnimator;
        [SerializeField] private ConnectingDotsAnimator dotsAnimator;

        [Header("Player/AI Score Cards")]
        [SerializeField] private Transform playerScoreContainer;
        [SerializeField] private PlayerScoreCard playerScoreCardPrefab;
        [SerializeField] private List<DomainColorDef> domainColors;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        public Transform PlayerScoreContainer => playerScoreContainer;
        public PlayerScoreCard PlayerScoreCardPrefab => playerScoreCardPrefab;

        private Tween _viewFadeTween;
        private Tween _connectingFadeTween;

        private void Awake()
        {
            // Auto-discover connecting panel components when not assigned in Inspector
            if (connectingPanelCanvasGroup != null)
            {
                var panelGO = connectingPanelCanvasGroup.gameObject;
                if (connectingPanel == null)
                    connectingPanel = panelGO.GetComponent<ConnectingPanel>();
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
            _viewFadeTween?.Kill();

            float duration = animSettings ? animSettings.hudFadeDuration : 0.25f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            if (active)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                var ease = animSettings ? animSettings.hudFadeInEase : Ease.OutQuad;
                _viewFadeTween = canvasGroup.DOFade(1f, duration).SetEase(ease).SetUpdate(unscaled);
            }
            else
            {
                var ease = animSettings ? animSettings.hudFadeOutEase : Ease.InQuad;
                _viewFadeTween = canvasGroup.DOFade(0f, duration).SetEase(ease).SetUpdate(unscaled)
                    .OnComplete(() =>
                    {
                        canvasGroup.interactable = false;
                        canvasGroup.blocksRaycasts = false;
                    });
            }
        }

        public void ToggleConnectingPanel(bool active)
        {
            if (!connectingPanelCanvasGroup) return;

            _connectingFadeTween?.Kill();

            float duration = animSettings ? animSettings.connectingFadeDuration : 0.3f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            if (active)
            {
                // Enable/disable the ConnectingPanel component so OnEnable picks a random sprite
                if (connectingPanel != null)
                    connectingPanel.enabled = true;

                connectingPanelCanvasGroup.interactable = true;
                connectingPanelCanvasGroup.blocksRaycasts = true;
                _connectingFadeTween = connectingPanelCanvasGroup.DOFade(1f, duration).SetUpdate(unscaled);

                StartConnectingAnimations();
            }
            else
            {
                StopConnectingAnimations();

                _connectingFadeTween = connectingPanelCanvasGroup.DOFade(0f, duration).SetUpdate(unscaled)
                    .OnComplete(() =>
                    {
                        connectingPanelCanvasGroup.interactable = false;
                        connectingPanelCanvasGroup.blocksRaycasts = false;

                        if (connectingPanel != null)
                            connectingPanel.enabled = false;
                    });
            }
        }

        private System.Threading.CancellationTokenSource _hackerCts;

        private void StartConnectingAnimations()
        {
            // Start hacker text animation using the animator's own baked-in fullText
            if (hackerTextAnimator != null)
            {
                _hackerCts?.Cancel();
                _hackerCts?.Dispose();
                _hackerCts = new System.Threading.CancellationTokenSource();
                hackerTextAnimator.PlayIn(_hackerCts.Token).Forget();
            }

            // Start dots animation
            if (dotsAnimator != null)
            {
                dotsAnimator.BaseText = "CONNECTING TO SHORE";
                dotsAnimator.StartAnimation();
            }
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
            if (playerScoreContainer == null) return;

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
            _viewFadeTween?.Kill();
            _connectingFadeTween?.Kill();
            _hackerCts?.Cancel();
            _hackerCts?.Dispose();
            _hackerCts = null;
        }
    }
}