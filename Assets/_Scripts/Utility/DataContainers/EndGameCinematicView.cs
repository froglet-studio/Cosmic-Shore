using System;
using System.Collections;
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
            
            scoreRevealToastText.text = message;
            
            var rectTransform = scoreRevealToastText.GetComponent<RectTransform>();
            if (!rectTransform) yield break;
            
            Vector2 originalPos = rectTransform.anchoredPosition;
            Vector2 targetPos = originalPos + new Vector2(0, settings.yOffset);
            
            // Reset
            scoreRevealToastCanvasGroup.alpha = 0f;
            rectTransform.anchoredPosition = originalPos;
            rectTransform.localScale = Vector3.one * 0.8f;
            
            if (settings.delay > 0f)
                yield return new WaitForSeconds(settings.delay);
            
            Sequence toastSequence = DOTween.Sequence();
            
            // Animate
            toastSequence.Append(scoreRevealToastCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad));
            toastSequence.Join(rectTransform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack));
            toastSequence.Join(rectTransform.DOAnchorPos(targetPos, 0.4f).SetEase(Ease.OutQuad));
            
            float holdDuration = Mathf.Max(0.1f, settings.duration - 0.6f);
            toastSequence.AppendInterval(holdDuration);
            
            toastSequence.Append(scoreRevealToastCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
            toastSequence.Join(rectTransform.DOScale(0.8f, 0.3f).SetEase(Ease.InQuad));
            
            toastSequence.Play();
            yield return toastSequence.WaitForCompletion();
            
            rectTransform.anchoredPosition = originalPos;
            rectTransform.localScale = Vector3.one;
        }
        #endregion

        #region Score Reveal Animation
        
        /// <summary>
        /// Play the complete score reveal animation sequence
        /// Added formatAsTime parameter to switch between integer score and HH:MM:SS
        /// </summary>
        public IEnumerator PlayScoreRevealAnimation(
            string cinematicText,
            int score,
            ScoreRevealAnimationSettings settings,
            bool formatAsTime = false)
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
            
            DisplayVesselImages();

            // Run casino counter animation with formatting flag
            yield return StartCoroutine(PlayCasinoCounterCoroutine(score, settings.casinoCounterDuration, formatAsTime));
        }

        IEnumerator PlayCasinoCounterCoroutine(int targetScore, float duration, bool formatAsTime)
        {
            var task = UniTask.WhenAll(
                AnimateSingleCounter(bestScoreText, targetScore, duration, _cts.Token, formatAsTime),
                AnimateSingleCounter(highScoreText, targetScore, duration, _cts.Token, formatAsTime)
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

        async UniTask AnimateSingleCounter(TMP_Text textField, int target, float duration, CancellationToken ct, bool formatAsTime)
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
                    
                    // [Visual Note] Format logic applied during animation loop
                    textField.text = formatAsTime ? FormatTime(randomDisplay) : randomDisplay.ToString("000");

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // Final Value
                textField.text = formatAsTime ? FormatTime(target) : target.ToString("000");
            }
            catch (System.OperationCanceledException)
            {
                if (textField)
                    textField.text = formatAsTime ? FormatTime(target) : target.ToString("000");
            }
        }

        void DisplayVesselImages()
        {
            vesselDisplayManager?.DisplayVessels();
        }

        /// <summary>
        /// Helper to format seconds into HH:MM:SS
        /// </summary>
        private string FormatTime(int totalSeconds)
        {
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            return $"{hours:00}:{minutes:00}:{seconds:00}";
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