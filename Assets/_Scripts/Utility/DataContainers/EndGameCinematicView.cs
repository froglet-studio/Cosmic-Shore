using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// View component responsible for all UI presentation during end-game cinematics.
    /// Handles animations, display updates, and user input.
    /// Communicates with controller through events.
    /// </summary>
    public class EndGameCinematicView : MonoBehaviour
    {
        [Header("Score Reveal UI")]
        [Tooltip("Parent panel that contains all score reveal UI")]
        [SerializeField] Transform scoreRevealPanel;
        
        [Tooltip("Root transform for slide-in animation")]
        [SerializeField] RectTransform animatedRoot;
        
        [SerializeField] TMP_Text cinematicTextDisplay;
        [SerializeField] TMP_Text bestScoreText;
        [SerializeField] TMP_Text highScoreText;
        [SerializeField] Button continueButton;
        [SerializeField] Image backgroundImage;
        
        [Header("Victory Toast")]
        [Tooltip("Toast message shown during victory lap")]
        [SerializeField] TMP_Text scoreRevealToastText;
        [SerializeField] CanvasGroup scoreRevealToastCanvasGroup;

        [Header("Vessel Podium Display")]
        [Tooltip("Manager for vessel icon displays")]
        [SerializeField] EndGameVesselDisplayManager vesselDisplayManager;

        [Header("Connecting Panel")]
        [SerializeField] SceneTransitionModal connectingPanel;

        private CancellationTokenSource _cts;

        public event Action OnContinuePressed;

        public void Initialize()
        {
            _cts = new CancellationTokenSource();
            
            if (continueButton)
            {
                continueButton.onClick.AddListener(HandleContinueButtonClicked);
            }

            if (scoreRevealPanel)
                scoreRevealPanel.gameObject.SetActive(false);
        }

        public void Cleanup()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (continueButton)
                continueButton.onClick.RemoveListener(HandleContinueButtonClicked);
        }

        #region Panel Visibility
        public void ShowScoreRevealPanel()
        {
            if (scoreRevealPanel)
                scoreRevealPanel.gameObject.SetActive(true);
        }

        public void HideScoreRevealPanel()
        {
            if (scoreRevealPanel)
                scoreRevealPanel.gameObject.SetActive(false);
        }

        public void ShowContinueButton()
        {
            if (continueButton)
                continueButton.gameObject.SetActive(true);
        }

        public void HideContinueButton()
        {
            if (continueButton)
                continueButton.gameObject.SetActive(false);
        }

        public bool IsContinueButtonActive()
        {
            return continueButton && continueButton.gameObject.activeSelf;
        }

        public void ShowConnectingPanel()
        {
            if (connectingPanel)
                connectingPanel.TransitionDoor(true);
        }
        #endregion

        #region Victory Toast Animation
        /// <summary>
        /// Show animated victory toast message during victory lap
        /// </summary>
        public void ShowVictoryToast(string message, ToastAnimationSettings settings)
        {
            StartCoroutine(AnimateVictoryToast(message, settings));
        }

        IEnumerator AnimateVictoryToast(string message, ToastAnimationSettings settings)
        {
            if (!scoreRevealToastText || !scoreRevealToastCanvasGroup)
            {
                Debug.LogWarning("[EndGameView] Victory toast components not assigned!");
                yield break;
            }
            
            Debug.Log($"[EndGameView] Showing victory toast: '{message}'");
            
            scoreRevealToastText.text = message;
            
            var rectTransform = scoreRevealToastText.GetComponent<RectTransform>();
            if (!rectTransform)
            {
                Debug.LogWarning("[EndGameView] Toast text missing RectTransform!");
                yield break;
            }
            
            Vector2 originalPos = rectTransform.anchoredPosition;
            Vector2 targetPos = originalPos + new Vector2(0, settings.yOffset);
            
            // Reset initial state
            scoreRevealToastCanvasGroup.alpha = 0f;
            rectTransform.anchoredPosition = originalPos;
            rectTransform.localScale = Vector3.one * 0.8f;
            
            if (settings.delay > 0f)
                yield return new WaitForSeconds(settings.delay);
            
            Sequence toastSequence = DOTween.Sequence();
            
            // Phase 1: Fade in + Scale up + Pop up
            toastSequence.Append(scoreRevealToastCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad));
            toastSequence.Join(rectTransform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack));
            toastSequence.Join(rectTransform.DOAnchorPos(targetPos, 0.4f).SetEase(Ease.OutQuad));
            
            // Phase 2: Hold visible
            float holdDuration = Mathf.Max(0.1f, settings.duration - 0.6f);
            toastSequence.AppendInterval(holdDuration);
            
            // Phase 3: Fade out + Scale down
            toastSequence.Append(scoreRevealToastCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
            toastSequence.Join(rectTransform.DOScale(0.8f, 0.3f).SetEase(Ease.InQuad));
            
            toastSequence.Play();
            yield return toastSequence.WaitForCompletion();
            
            // Reset
            rectTransform.anchoredPosition = originalPos;
            rectTransform.localScale = Vector3.one;
            
            Debug.Log("[EndGameView] Victory toast animation complete");
        }
        #endregion

        #region Score Reveal Animation
        /// <summary>
        /// Play the complete score reveal animation sequence
        /// </summary>
        public IEnumerator PlayScoreRevealAnimation(
            string cinematicText,
            int score,
            ScoreRevealAnimationSettings settings)
        {
            // Slide in animation
            if (animatedRoot)
            {
                animatedRoot.anchoredPosition = new Vector2(settings.startX, animatedRoot.anchoredPosition.y);

                yield return animatedRoot
                    .DOAnchorPosX(settings.endX, settings.slideDuration)
                    .SetEase(settings.slideEase)
                    .WaitForCompletion();
            }
            
            // Set cinematic text
            if (cinematicTextDisplay)
            {
                cinematicTextDisplay.text = cinematicText;
            }
            
            // Display vessel images
            DisplayVesselImages();

            // Run casino counter animation
            yield return StartCoroutine(PlayCasinoCounterCoroutine(score, settings.casinoCounterDuration));
        }

        IEnumerator PlayCasinoCounterCoroutine(int targetScore, float duration)
        {
            var task = UniTask.WhenAll(
                AnimateSingleCounter(bestScoreText, targetScore, duration, _cts.Token),
                AnimateSingleCounter(highScoreText, targetScore, duration, _cts.Token)
            );

            while (!task.Status.IsCompleted())
            {
                yield return null;
            }

            if (task.Status == UniTaskStatus.Faulted)
            {
                Debug.LogError($"Casino counter animation failed: {task.AsTask().Exception}");
            }
        }

        async UniTask AnimateSingleCounter(TMP_Text textField, int target, float duration, CancellationToken ct)
        {
            if (!textField) return;

            try
            {
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;
                    int randomDisplay = UnityEngine.Random.Range(0, target + 1);
                    textField.text = randomDisplay.ToString("000");

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                textField.text = target.ToString("000");
            }
            catch (System.OperationCanceledException)
            {
                if (textField)
                    textField.text = target.ToString("000");
            }
        }

        void DisplayVesselImages()
        {
            vesselDisplayManager?.DisplayVessels();
        }
        #endregion

        #region Input Handling
        void HandleContinueButtonClicked()
        {
            if (continueButton)
                continueButton.gameObject.SetActive(false);
            
            OnContinuePressed?.Invoke();
        }
        #endregion
    }
}