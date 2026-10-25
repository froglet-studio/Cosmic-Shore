using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Individual toast item view. Sets text, fades in/out, supports swipe-to-dismiss.
    /// Never modifies its own anchors, pivot, size, or position - layout is owned by the container.
    ///
    /// <para><b>Style-B parts (all optional, Docs/MENU_REDESIGN_B.md section 20).</b> With a
    /// <see cref="LayoutElement"/> wired the toast HUGS its text: it measures the message and asks the
    /// container for 360 - 720 wide, so it no longer renders as a 100 x 100 square. A
    /// <see cref="body"/> child (everything visible) drops in from above while the root, which the
    /// container lays out, stays put. A leading glyph names the <see cref="ToastKind"/>, and a drain
    /// bar empties over the toast's hold.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ToastNotificationItem : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Bindings")]
        [SerializeField] private TMP_Text messageText;

        [Header("Style-B sizing (optional)")]
        [SerializeField, Tooltip("The root's LayoutElement. When wired, its preferred size is set from the measured message.")]
        private LayoutElement sizing;
        [SerializeField, Tooltip("The message's LayoutElement inside the body row: its preferred width is the measured text width.")]
        private LayoutElement messageSizing;
        [SerializeField, Min(0f)] private float minWidth = 360f;
        [SerializeField, Min(0f)] private float maxWidth = 720f;
        [SerializeField, Tooltip("Left, right, top, bottom padding around the row - match the body's layout group.")]
        private Vector4 padding = new(22f, 22f, 14f, 16f);
        [SerializeField, Tooltip("Glyph width plus the gap after it.")]
        private float glyphAdvance = 32f;

        [Header("Style-B parts (optional)")]
        [SerializeField, Tooltip("Everything visible. Drops in from above; the root stays where the container put it.")]
        private RectTransform body;
        [SerializeField, Tooltip("The bar along the bottom edge, emptied (x scale 1 to 0) over the hold.")]
        private RectTransform drainBar;
        [SerializeField] private Image kindIcon;
        [SerializeField] private KitThemedGraphic kindPaint;
        [SerializeField, Tooltip("Shows / hides the glyph (KitVisibility).")]
        private CanvasGroup kindGroup;
        [SerializeField, Tooltip("The glyph's LayoutElement: out of the row while the toast has no kind.")]
        private LayoutElement kindSizing;
        [SerializeField] private Sprite refusalIcon;
        [SerializeField] private Sprite successIcon;
        [SerializeField] private Sprite errorIcon;
        [SerializeField] private Sprite goalIcon;

        private CanvasGroup _canvasGroup;
        private RectTransform _rect;
        private Sequence _activeSeq;

        private ToastNotificationSettingsSO _settings;
        private float _dragStartX;
        private float _restX;
        private bool _isDismissing;

        public event Action<ToastNotificationItem> OnDismissed;

        /// <summary>Binds the label of an item built in code (the manager's default prefab).</summary>
        internal void SetMessageText(TMP_Text text) => messageText = text;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rect = GetComponent<RectTransform>();
        }

        /// <summary>
        /// Set the message text, activate, and fade in. Does not touch layout.
        /// </summary>
        public void Show(string message, ToastNotificationSettingsSO settings)
            => Show(message, ToastKind.None, settings);

        /// <summary>
        /// Set the message and its kind's glyph, size to the text, activate, and fade (and drop) in.
        /// </summary>
        public void Show(string message, ToastKind kind, ToastNotificationSettingsSO settings)
        {
            // Show can arrive before Awake (item instantiated from an inactive prefab / pooled
            // inactive) — resolve the required components lazily instead of NRE'ing.
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_rect == null) _rect = GetComponent<RectTransform>();

            _settings = settings;
            _isDismissing = false;

            if (messageText) messageText.text = message;
            bool hasGlyph = DrawKind(kind);
            Measure(message, hasGlyph);

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(true);

            // Cache resting X so swipe-dismiss can restore it
            _restX = _rect.anchoredPosition.x;

            KillSequence();

            _activeSeq = DOTween.Sequence();
            if (settings.useUnscaledTime) _activeSeq.SetUpdate(true);

            _activeSeq.Join(_canvasGroup.DOFade(1f, settings.fadeInDuration));
            if (body && settings.dropDistance > 0f)
            {
                body.anchoredPosition = new Vector2(0f, settings.dropDistance);
                _activeSeq.Join(body.DOAnchorPosY(0f, settings.fadeInDuration).SetEase(Ease.OutQuint));
            }
            AppendHold(settings);
        }

        /// <summary>The hold, with the drain bar emptying across it, then the auto-dismiss.</summary>
        private void AppendHold(ToastNotificationSettingsSO settings)
        {
            if (drainBar)
            {
                drainBar.localScale = Vector3.one;
                _activeSeq.Append(drainBar.DOScaleX(0f, settings.autoRemoveDelay).SetEase(Ease.Linear));
            }
            else
            {
                _activeSeq.AppendInterval(settings.autoRemoveDelay);
            }
            _activeSeq.AppendCallback(AutoDismiss);
        }

        /// <summary>The leading glyph for <paramref name="kind"/>; false when the toast draws none.</summary>
        private bool DrawKind(ToastKind kind)
        {
            Sprite sprite = kind switch
            {
                ToastKind.Refusal => refusalIcon,
                ToastKind.Success => successIcon,
                ToastKind.Error => errorIcon,
                ToastKind.Goal => goalIcon,
                _ => null,
            };
            bool shown = sprite != null && kindIcon != null;
            if (shown)
            {
                kindIcon.sprite = sprite;
                if (kindPaint)
                    kindPaint.SetToken(kind switch
                    {
                        ToastKind.Success => UIColorToken.Cta,
                        ToastKind.Error => UIColorToken.Danger,
                        ToastKind.Goal => UIColorToken.NeutralLightest,
                        _ => UIColorToken.TextLight,
                    });
            }
            if (kindGroup) KitVisibility.SetShown(kindGroup, shown);
            if (kindSizing) kindSizing.ignoreLayout = !shown;
            return shown;
        }

        /// <summary>
        /// Hug the text: the narrowest width that fits the message on one line, capped at
        /// <see cref="maxWidth"/> (where it wraps) and floored at <see cref="minWidth"/>.
        /// </summary>
        private void Measure(string message, bool hasGlyph)
        {
            if (!sizing || !messageText) return;
            float glyph = hasGlyph ? glyphAdvance : 0f;
            float maxText = Mathf.Max(1f, maxWidth - padding.x - padding.y - glyph);
            float textWidth = Mathf.Min(messageText.GetPreferredValues(message).x, maxText);
            float textHeight = messageText.GetPreferredValues(message, textWidth, 0f).y;
            if (messageSizing) messageSizing.preferredWidth = textWidth;
            sizing.preferredWidth = Mathf.Clamp(textWidth + padding.x + padding.y + glyph, minWidth, maxWidth);
            sizing.preferredHeight = textHeight + padding.z + padding.w;
        }

        // Legacy overload kept for compatibility - ignores position parameter.
        public void Show(string message, Vector2 _, ToastNotificationSettingsSO settings)
            => Show(message, settings);

        // Legacy - no-op, layout handles positioning.
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
                AppendHold(_settings);
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
