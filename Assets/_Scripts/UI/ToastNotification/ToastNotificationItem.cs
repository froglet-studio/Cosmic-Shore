using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Individual toast item view. Slides/fades in, reveals the message with a
    /// typewriter effect, and supports swipe-to-dismiss. Vertical layout is owned
    /// by the container; this component only animates its own horizontal offset.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ToastNotificationItem : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Bindings")]
        [SerializeField] private TMP_Text messageText;

        private CanvasGroup _canvasGroup;
        private RectTransform _rect;
        private Sequence _activeSeq;

        private ToastNotificationSettingsSO _settings;
        private float _dragStartX;
        private float _restX;
        private bool _isDismissing;

        public event Action<ToastNotificationItem> OnDismissed;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rect = GetComponent<RectTransform>();
        }

        /// <summary>
        /// Set the message text, activate, and play the intro (slide + fade + typewriter).
        /// </summary>
        public void Show(string message, ToastNotificationSettingsSO settings)
        {
            _settings = settings;
            _isDismissing = false;

            if (messageText)
            {
                messageText.text = message;
                messageText.maxVisibleCharacters = int.MaxValue;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(true);

            // Let the container layout place us before caching the resting position —
            // it is also what recovers pooled items left off-screen by a slide-out.
            if (transform.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            _restX = _rect.anchoredPosition.x;

            KillSequence();
            _activeSeq = DOTween.Sequence();
            if (settings.useUnscaledTime) _activeSeq.SetUpdate(true);

            float startX = _restX - (_rect.rect.width + settings.offscreenPadding);
            _rect.anchoredPosition = new Vector2(startX, _rect.anchoredPosition.y);
            _activeSeq.Append(_rect.DOAnchorPosX(_restX, settings.slideInDuration).SetEase(settings.slideInEase));
            _activeSeq.Join(_canvasGroup.DOFade(1f, settings.fadeInDuration));

            if (settings.useTypewriterText && messageText)
            {
                messageText.ForceMeshUpdate();
                int charCount = messageText.textInfo.characterCount;
                if (charCount > 0)
                {
                    messageText.maxVisibleCharacters = 0;
                    float revealDuration = Mathf.Min(
                        charCount / Mathf.Max(1f, settings.typewriterCharactersPerSecond),
                        settings.typewriterMaxDuration);
                    _activeSeq.Join(DOTween.To(
                            () => messageText.maxVisibleCharacters,
                            visible => messageText.maxVisibleCharacters = visible,
                            charCount, revealDuration)
                        .SetEase(Ease.Linear));
                }
            }

            _activeSeq.AppendInterval(settings.autoRemoveDelay);
            _activeSeq.AppendCallback(AutoDismiss);
        }

        #region Dismiss

        private void AutoDismiss()
        {
            // Auto-remove exits back out the way it came in (left).
            FadeOutAndDismiss(-1f);
        }

        private void FadeOutAndDismiss(float directionX)
        {
            if (_isDismissing) return;
            _isDismissing = true;

            KillSequence();

            _activeSeq = DOTween.Sequence();
            if (_settings.useUnscaledTime) _activeSeq.SetUpdate(true);

            float exitX = _rect.anchoredPosition.x
                          + directionX * (_rect.rect.width + _settings.offscreenPadding);
            _activeSeq.Append(_rect.DOAnchorPosX(exitX, _settings.slideOutDuration).SetEase(_settings.slideOutEase));
            _activeSeq.Join(_canvasGroup.DOFade(0f, _settings.fadeOutDuration));

            _activeSeq.OnComplete(() =>
            {
                _canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
                OnDismissed?.Invoke(this);
            });
        }

        public void DismissImmediate()
        {
            if (_isDismissing) return;
            _isDismissing = true;

            KillSequence();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            OnDismissed?.Invoke(this);
        }

        #endregion

        #region Drag / Swipe Handling

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isDismissing) return;
            KillSequence();
            if (messageText) messageText.maxVisibleCharacters = int.MaxValue;
            _canvasGroup.alpha = 1f;
            _dragStartX = eventData.position.x;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isDismissing || _settings == null) return;

            float deltaX = eventData.position.x - _dragStartX;
            if (deltaX < 0f) deltaX = 0f;

            _rect.anchoredPosition = new Vector2(_restX + deltaX, _rect.anchoredPosition.y);

            float progress = Mathf.Clamp01(deltaX / (_settings.swipeDismissThreshold * 2f));
            _canvasGroup.alpha = 1f - progress * 0.5f;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isDismissing || _settings == null) return;

            float deltaX = eventData.position.x - _dragStartX;

            if (deltaX >= _settings.swipeDismissThreshold)
            {
                // Swipe continues out to the right.
                FadeOutAndDismiss(1f);
            }
            else
            {
                KillSequence();
                _activeSeq = DOTween.Sequence();
                if (_settings.useUnscaledTime) _activeSeq.SetUpdate(true);

                _activeSeq.Append(_rect.DOAnchorPosX(_restX, _settings.slideInDuration).SetEase(_settings.slideInEase));
                _activeSeq.Join(_canvasGroup.DOFade(1f, _settings.fadeInDuration));
                _activeSeq.AppendInterval(_settings.autoRemoveDelay);
                _activeSeq.AppendCallback(AutoDismiss);
            }
        }

        #endregion

        /// <summary>
        /// Runtime binding hook for the code-built fallback item. Authored prefabs
        /// wire <see cref="messageText"/> in the inspector instead.
        /// </summary>
        internal void BindMessageText(TMP_Text text) => messageText = text;

        private void KillSequence()
        {
            if (_activeSeq != null && _activeSeq.IsActive())
            {
                _activeSeq.Kill();
                _activeSeq = null;
            }
        }

        private void OnDestroy() => KillSequence();
    }
}
