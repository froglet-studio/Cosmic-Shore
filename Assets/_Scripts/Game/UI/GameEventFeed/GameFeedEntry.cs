using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class GameFeedEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text textComponent;

        private CanvasGroup _canvasGroup;
        private RectTransform _rect;
        private Sequence _seq;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rect = GetComponent<RectTransform>();
        }

        public void Setup(string message, Color color, bool isRichText = false)
        {
            if (textComponent == null)
                textComponent = GetComponentInChildren<TMP_Text>();

            if (textComponent == null) return;

            textComponent.text = message;
            textComponent.richText = true;

            if (!isRichText)
                textComponent.color = color;
            else
                textComponent.color = Color.white;
        }

        /// <summary>
        /// Start the slide-in / hold / fade-out animation.
        /// Must be called AFTER layout rebuild so anchoredPosition is correct.
        /// Only animates X so VerticalLayoutGroup can reposition Y when new entries arrive.
        /// </summary>
        public void AnimateIn(GameFeedSettingsSO settings)
        {
            var targetX = _rect.anchoredPosition.x;

            // Offset X for slide-in start
            var pos = _rect.anchoredPosition;
            pos.x += settings.slideInOffset;
            _rect.anchoredPosition = pos;
            _canvasGroup.alpha = 0f;

            _seq = DOTween.Sequence();
            if (settings.useUnscaledTime) _seq.SetUpdate(true);

            // IN: slide X from right + fade in (Y stays under layout control)
            _seq.Join(_rect.DOAnchorPosX(targetX, settings.slideInDuration).SetEase(settings.slideInEase));
            _seq.Join(_canvasGroup.DOFade(1f, settings.slideInDuration));

            // HOLD
            _seq.AppendInterval(settings.holdDuration);

            // OUT: fade out
            _seq.Append(_canvasGroup.DOFade(0f, settings.fadeOutDuration));

            _seq.OnComplete(() =>
            {
                if (gameObject != null)
                    Destroy(gameObject);
            });
        }

        private void OnDestroy()
        {
            if (_seq != null && _seq.IsActive())
            {
                _seq.Kill();
                _seq = null;
            }
        }

        /// <summary>
        /// Creates a GameFeedEntry programmatically without needing a prefab asset.
        /// </summary>
        public static GameFeedEntry CreateEntry(Transform parent)
        {
            var go = new GameObject("FeedEntry", typeof(RectTransform), typeof(CanvasGroup), typeof(GameFeedEntry));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1f, 0f);

            // Add LayoutElement for height control
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 20f;
            le.flexibleWidth = 1f;

            // Add ContentSizeFitter to auto-size height
            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Child text object
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
            textGo.transform.SetParent(go.transform, false);
            textGo.layer = go.layer;

            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 14f;
            tmp.alignment = TextAlignmentOptions.Right;
            tmp.richText = true;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.enableWordWrapping = true;

            var entry = go.GetComponent<GameFeedEntry>();
            entry.textComponent = tmp;

            return entry;
        }
    }
}
