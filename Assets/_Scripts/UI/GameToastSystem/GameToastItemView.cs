using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One line of the in-game toast feed. Lives on the toast item PREFAB (authored in the
    /// editor - never generated from code). Slides in at the bottom of the feed, then stays:
    /// it is pushed up by newer lines and dims with age instead of disappearing. Only X is
    /// animated - the container's VerticalLayoutGroup owns Y.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GameToastItemView : MonoBehaviour
    {
        [Header("References (wire on the prefab)")]
        [SerializeField] private TMP_Text messageText;

        [Tooltip("Optional: thin bar / icon tinted with the toast's domain color.")]
        [SerializeField] private Image accentImage;

        [Tooltip("Optional: the line's plate. A Graphic rather than an Image because it is " +
                 "GENERATED - a TrapezoidGraphic, the same house shape the ability lockup and " +
                 "the goal stack draw with. Held so a future line style can drive it; Setup " +
                 "does not write it today.")]
        [SerializeField] private Graphic background;

        private CanvasGroup _canvasGroup;
        private RectTransform _rect;
        private Sequence _sequence;
        private float _baseAlpha = 1f;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rect = GetComponent<RectTransform>();
        }

        public void Setup(string message, Color textColor, Color accentColor, float baseAlpha)
        {
            _baseAlpha = Mathf.Clamp01(baseAlpha);

            messageText.richText = true;
            messageText.text = message;
            messageText.color = textColor;

            if (accentImage != null)
                accentImage.color = accentColor;
        }

        /// <summary>
        /// Slide-in, then age-dim (no removal). Call AFTER the layout rebuild so the rest
        /// position is correct.
        /// </summary>
        public void AnimateIn(GameToastSettingsSO settings)
        {
            KillTween();

            float targetX = _rect.anchoredPosition.x;
            var start = _rect.anchoredPosition;
            start.x += settings.slideInOffset;
            _rect.anchoredPosition = start;
            _canvasGroup.alpha = 0f;

            _sequence = DOTween.Sequence().SetLink(gameObject);
            if (settings.useUnscaledTime)
                _sequence.SetUpdate(true);

            _sequence.Join(_rect.DOAnchorPosX(targetX, settings.slideInDuration)
                .SetEase(settings.slideInEase));
            _sequence.Join(_canvasGroup.DOFade(_baseAlpha, settings.slideInDuration));

            if (settings.agedAlpha < 1f && settings.ageAfterSeconds > 0f)
            {
                _sequence.AppendInterval(settings.ageAfterSeconds);
                _sequence.Append(_canvasGroup.DOFade(settings.agedAlpha * _baseAlpha,
                    settings.ageFadeDuration));
            }
        }

        private void KillTween()
        {
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill();
            _sequence = null;
        }

        private void OnDestroy() => KillTween();
    }
}
