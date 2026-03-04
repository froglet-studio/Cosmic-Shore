using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.App.UI.ToastNotification
{
    /// <summary>
    /// Individual toast item view. Handles its own slide/fade animations and swipe-to-dismiss gesture.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ToastNotificationItem : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Bindings")]
        [SerializeField] private TMP_Text messageText;

        // Cached references
        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private Sequence _activeSeq;

        // Runtime state
        private ToastNotificationSettingsSO _settings;
        private Vector2 _showPosition;
        private float _dragStartX;
        private bool _isDismissing;

        /// <summary>Fired when the toast finishes its dismiss animation and should be recycled.</summary>
        public event Action<ToastNotificationItem> OnDismissed;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// Initialise and play the slide-in + fade-in animation.
        /// </summary>
        public void Show(string message, Vector2 showPosition, ToastNotificationSettingsSO settings)
        {
            _settings = settings;
            _showPosition = showPosition;
            _isDismissing = false;

            if (messageText) messageText.text = message;

            // Anchor bottom-left, pivot bottom-left
            _rect.anchorMin = new Vector2(0f, 0f);
            _rect.anchorMax = new Vector2(1f, 0f);
            _rect.pivot = new Vector2(0.5f, 0f);

            // Start off-screen to the left
            float offscreenX = -((_rect.rect.width > 0 ? _rect.rect.width : 600f) + settings.offscreenPadding);
            _rect.anchoredPosition = new Vector2(offscreenX, showPosition.y);
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(true);

            KillSequence();

            _activeSeq = DOTween.Sequence();
            if (settings.useUnscaledTime) _activeSeq.SetUpdate(true);

            // Slide in from left
            _activeSeq.Join(
                _rect.DOAnchorPosX(showPosition.x, settings.slideInDuration)
                    .SetEase(settings.slideInEase));

            // Fade in
            _activeSeq.Join(
                _canvasGroup.DOFade(1f, settings.fadeInDuration));

            // Hold
            _activeSeq.AppendInterval(settings.autoRemoveDelay);

            // Auto-dismiss after hold
            _activeSeq.AppendCallback(AutoDismiss);
        }

        /// <summary>
        /// Slide the toast to a new Y position (used when other toasts are dismissed and stack shifts).
        /// </summary>
        public void AnimateToY(float newY)
        {
            if (_isDismissing || _settings == null) return;
            _showPosition = new Vector2(_showPosition.x, newY);

            _rect.DOAnchorPosY(newY, _settings.slideInDuration * 0.5f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(_settings.useUnscaledTime);
        }

        #region Dismiss

        private void AutoDismiss()
        {
            if (_isDismissing) return;
            SlideOutRight();
        }

        /// <summary>
        /// Plays the dismiss animation: slide right and fade out.
        /// </summary>
        private void SlideOutRight()
        {
            if (_isDismissing) return;
            _isDismissing = true;

            KillSequence();

            float exitX = (_rect.rect.width > 0 ? _rect.rect.width : 600f) + _settings.offscreenPadding;

            _activeSeq = DOTween.Sequence();
            if (_settings.useUnscaledTime) _activeSeq.SetUpdate(true);

            _activeSeq.Join(
                _rect.DOAnchorPosX(_showPosition.x + exitX, _settings.slideOutDuration)
                    .SetEase(_settings.slideOutEase));

            _activeSeq.Join(
                _canvasGroup.DOFade(0f, _settings.fadeOutDuration));

            _activeSeq.OnComplete(() =>
            {
                _canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
                OnDismissed?.Invoke(this);
            });
        }

        /// <summary>
        /// Immediately dismiss without animation (used when capacity is exceeded).
        /// </summary>
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

            // Kill the auto-dismiss timer when user starts dragging
            KillSequence();
            _dragStartX = eventData.position.x;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isDismissing || _settings == null) return;

            float deltaX = eventData.position.x - _dragStartX;

            // Only allow dragging to the right (positive direction)
            if (deltaX < 0f) deltaX = 0f;

            _rect.anchoredPosition = new Vector2(_showPosition.x + deltaX, _rect.anchoredPosition.y);

            // Fade proportionally as the user drags further right
            float progress = Mathf.Clamp01(deltaX / (_settings.swipeDismissThreshold * 2f));
            _canvasGroup.alpha = 1f - progress * 0.5f;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isDismissing || _settings == null) return;

            float deltaX = eventData.position.x - _dragStartX;

            if (deltaX >= _settings.swipeDismissThreshold)
            {
                // Swipe threshold met — dismiss
                SlideOutRight();
            }
            else
            {
                // Snap back and restart auto-dismiss timer
                _canvasGroup.alpha = 1f;

                KillSequence();
                _activeSeq = DOTween.Sequence();
                if (_settings.useUnscaledTime) _activeSeq.SetUpdate(true);

                _activeSeq.Join(
                    _rect.DOAnchorPosX(_showPosition.x, _settings.slideInDuration * 0.5f)
                        .SetEase(Ease.OutCubic));

                _activeSeq.AppendInterval(_settings.autoRemoveDelay);
                _activeSeq.AppendCallback(AutoDismiss);
            }
        }

        #endregion

        private void KillSequence()
        {
            if (_activeSeq != null && _activeSeq.IsActive())
            {
                _activeSeq.Kill();
                _activeSeq = null;
            }
        }

        private void OnDestroy()
        {
            KillSequence();
        }
    }
}
