using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace CosmicShore.UI
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

        [Header("Player/AI Score Entries (in-game)")]
        [SerializeField] private Transform playerScoreContainer;
        [SerializeField] private PlayerScoreEntry playerScoreEntryPrefab;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        public Transform PlayerScoreContainer => playerScoreContainer;
        public PlayerScoreEntry PlayerScoreEntryPrefab => playerScoreEntryPrefab;

        private Tween _viewFadeTween;
        private Tween _connectingFadeTween;

        private void Awake()
        {
            // Auto-discover the connecting-panel child components from the authored panel
            // so only the connectingPanelCanvasGroup needs wiring in the inspector.
            if (connectingPanelCanvasGroup != null)
            {
                var panelGO = connectingPanelCanvasGroup.gameObject;
                if (connectingPanel == null)
                    connectingPanel = panelGO.GetComponent<ConnectingPanel>();
                if (hackerTextAnimator == null)
                    hackerTextAnimator = panelGO.GetComponentInChildren<DoTweenTypewriterAnimator>(true);
                if (dotsAnimator == null)
                    dotsAnimator = panelGO.GetComponentInChildren<ConnectingDotsAnimator>(true);
            }
        }

        /// <summary>True when a connecting panel is authored/wired in this scene or prefab.</summary>
        public bool HasConnectingPanel => connectingPanelCanvasGroup != null;

        /// <summary>
        /// Sets the game-details line(s) shown on the connecting panel (mode name,
        /// intensity, players in the room). No-op if no panel is present.
        /// </summary>
        public void SetConnectingDetails(string details)
        {
            if (connectingPanel != null)
                connectingPanel.SetDetails(details);
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