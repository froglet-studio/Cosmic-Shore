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

        [Tooltip("Optional: line background.")]
        [SerializeField] private Image background;

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
            start.x += settings.SlideInOffset;
            _rect.anchoredPosition = start;
            _canvasGroup.alpha = 0f;

            _sequence = DOTween.Sequence().SetLink(gameObject);
            if (settings.UseUnscaledTime)
                _sequence.SetUpdate(true);

            _sequence.Join(_rect.DOAnchorPosX(targetX, settings.SlideInDuration)
                .SetEase(settings.SlideInEase));
            _sequence.Join(_canvasGroup.DOFade(_baseAlpha, settings.SlideInDuration));

            if (settings.AgedAlpha < 1f && settings.AgeAfterSeconds > 0f)
            {
                _sequence.AppendInterval(settings.AgeAfterSeconds);
                _sequence.Append(_canvasGroup.DOFade(settings.AgedAlpha * _baseAlpha,
                    settings.AgeFadeDuration));
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
