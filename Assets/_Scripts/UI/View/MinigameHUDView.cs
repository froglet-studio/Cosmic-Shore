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
            // Auto-discover connecting panel components when assigned in Inspector
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
            else
            {
                // No authored connecting panel in this scene/prefab — build one at runtime
                // so the mandatory pre-game connecting screen always shows.
                EnsureConnectingPanel();
            }
        }

        /// <summary>
        /// Builds a full-screen connecting panel at runtime when none is wired in the
        /// prefab/scene: a dark overlay with the game-details text and an animated
        /// "CONNECTING TO SHORE..." line. Mirrors the runtime-built skip button pattern in
        /// <see cref="MiniGameHUD"/>. No-op if a panel is already authored.
        /// </summary>
        public void EnsureConnectingPanel()
        {
            if (connectingPanelCanvasGroup != null) return;

            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas ? canvas.transform : transform;

            // Root overlay
            var rootGO = new GameObject("ConnectingPanel (Runtime)", typeof(RectTransform));
            var rootRect = rootGO.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            StretchFull(rootRect);
            rootRect.SetAsLastSibling();

            var bg = rootGO.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.06f, 0.96f);
            bg.raycastTarget = true;

            var cg = rootGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            var panel = rootGO.AddComponent<ConnectingPanel>();
            panel.enabled = false;

            // Game details (mode name, intensity, players)
            var detailsText = CreateText("Details", rootRect,
                new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.72f),
                TextAlignmentOptions.Center, 20f, 80f, Color.white);

            // Animated "connecting" line
            var connText = CreateText("ConnectingText", rootRect,
                new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.42f),
                TextAlignmentOptions.Center, 14f, 44f, new Color(1f, 1f, 1f, 0.85f));
            connText.text = "CONNECTING TO SHORE";

            var dots = connText.gameObject.AddComponent<ConnectingDotsAnimator>();
            dots.InitializeRuntime(connText);

            panel.InitializeRuntime(bg, detailsText);

            connectingPanelCanvasGroup = cg;
            connectingPanel = panel;
            dotsAnimator = dots;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static TMP_Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            TextAlignmentOptions alignment, float minSize, float maxSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.fontSizeMin = minSize;
            text.fontSizeMax = maxSize;
            text.color = color;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

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