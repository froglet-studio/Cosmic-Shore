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
        [SerializeField] private CanvasGroup connectingPanelCanvasGroup;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text lifeFormCounter;

        [Header("Connecting Panel")]
        [SerializeField] private ConnectingPanel connectingPanel;

        [Header("Connecting Panel Animations")]
        [SerializeField] private DoTweenTypewriterAnimator hackerTextAnimator;
        [SerializeField] private ConnectingDotsAnimator dotsAnimator;

        [Header("Objective")]
        [Tooltip("Top-left objective readout - the mode's objective glyph beside the count still " +
                 "needed to win. Optional; a HUD without one simply shows no glyph.")]
        [SerializeField] private ObjectiveReadout objectiveReadout;

        [Header("Player/AI Score Entries (in-game)")]
        [SerializeField] private Transform playerScoreContainer;
        [SerializeField] private PlayerScoreEntry playerScoreEntryPrefab;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        [Header("Style (optional)")]
        // Wiring proof only (Docs/STYLE_FOUNDATION.md §11). Nothing reads this yet: the literals
        // in this file and its siblings are still literals, and swapping them for tokens is a
        // separate, reviewed pass. Read it through UITheme.Resolve/Spacing/StaggerFor, which
        // fall back to the authored §11 values when this reference is empty.
        [SerializeField] private UIThemeSO theme;

        public UIThemeSO Theme => theme;

        public ObjectiveReadout ObjectiveReadout => objectiveReadout;

        public Transform PlayerScoreContainer => playerScoreContainer;
        public PlayerScoreEntry PlayerScoreEntryPrefab => playerScoreEntryPrefab;

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

        // scoreDisplay and roundTimeDisplay are OPTIONAL. The domain-grouped top bar retires the
        // per-player score plate outright, and a HUD that shows no objective count wires neither -
        // so both writers guard rather than assuming a reference the layout no longer requires.
        public void UpdateScoreUI(string message)
        {
            if (scoreDisplay) scoreDisplay.text = message;
        }

        public void UpdateCountdownTimer(string message)
        {
            if (roundTimeDisplay) roundTimeDisplay.text = message;
        }

        /// <summary>
        /// The lifeform counter SHOWS ITSELF. Only the two Wildlife Blitz turn monitors ever raise
        /// it, so on every other mode it used to sit in the corner reading a stale "0" - the same
        /// class of clutter the drone counters already solve by activating on a real value
        /// (see MiniGameHUD.OnMoundDroneSpawned). Empty or "0" hides it; anything else shows it.
        /// </summary>
        public void UpdateLifeFormCounter(string message)
        {
            if (!lifeFormCounter) return;
            lifeFormCounter.text = message;

            var root = lifeFormCounter.transform.parent != null
                ? lifeFormCounter.transform.parent.gameObject
                : lifeFormCounter.gameObject;
            bool meaningful = !string.IsNullOrWhiteSpace(message) && message.Trim() != "0";
            if (root.activeSelf != meaningful) root.SetActive(meaningful);
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