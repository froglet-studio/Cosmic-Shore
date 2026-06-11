using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.App.UI.ToastNotification
{
    /// <summary>
    /// Individual toast item view. Sets text, fades in/out, supports swipe-to-dismiss.
    /// Never modifies its own anchors, pivot, size, or position — layout is owned by the container.
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
        /// Set the message text, activate, and fade in. Does not touch layout.
        /// </summary>
        public void Show(string message, ToastNotificationSettingsSO settings)
        {
            _settings = settings;
            _isDismissing = false;

            if (messageText) messageText.text = message;

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(true);

            // Cache resting X so swipe-dismiss can restore it
            _restX = _rect.anchoredPosition.x;

            KillSequence();

            _activeSeq = DOTween.Sequence();
            if (settings.useUnscaledTime) _activeSeq.SetUpdate(true);

            _activeSeq.Join(_canvasGroup.DOFade(1f, settings.fadeInDuration));
            _activeSeq.AppendInterval(settings.autoRemoveDelay);
            _activeSeq.AppendCallback(AutoDismiss);
        }

        // Legacy overload kept for compatibility — ignores position parameter.
        public void Show(string message, Vector2 _, ToastNotificationSettingsSO settings)
            => Show(message, settings);

        // Legacy — no-op, layout handles positioning.
        public void AnimateToY(float _) { }

        #region Dismiss

        private void AutoDismiss()
        {
            if (_isDismissing) return;
            FadeOutAndDismiss();
        }

        private void FadeOutAndDismiss()
        {
            if (_isDismissing) return;
            _isDismissing = true;

            KillSequence();

            _activeSeq = DOTween.Sequence();
            if (_settings.useUnscaledTime) _activeSeq.SetUpdate(true);

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
                FadeOutAndDismiss();
            }
            else
            {
                _canvasGroup.alpha = 1f;
                _rect.anchoredPosition = new Vector2(_restX, _rect.anchoredPosition.y);

                KillSequence();
                _activeSeq = DOTween.Sequence();
                if (_settings.useUnscaledTime) _activeSeq.SetUpdate(true);
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

        private void OnDestroy() => KillSequence();
    }
}
