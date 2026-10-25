using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One of Home's docked cards (rev 3: the weekly challenge and the current quest move to the left
    /// under the pilot chip, and "can go away and be minimized"). The – button folds the card to a strip
    /// (its art slice and eyebrow) and back, remembered on this device; the × closes it for this session.
    ///
    /// <para>The card's data and its show / hide stay with <see cref="HomeUpNext"/>, which asks
    /// <see cref="IsClosed"/> before showing a card again.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Home Dock Card")]
    public class HomeDockCard : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The card's own group (shared with HomeUpNext, which shows it when it has data).")]
        [SerializeField] private CanvasGroup cardGroup;
        [Tooltip("Everything below the eyebrow: hidden while the card is folded.")]
        [SerializeField] private CanvasGroup detailGroup;
        [Tooltip("The card's rect; its height folds.")]
        [SerializeField] private RectTransform card;
        [SerializeField] private Button minimiseButton;
        [SerializeField] private Button closeButton;

        [Header("Fold")]
        [SerializeField, Min(1f)] private float expandedHeight = 132f;
        [SerializeField, Min(1f)] private float foldedHeight = 48f;
        [Tooltip("Seconds the fold takes.")]
        [SerializeField, Min(0f)] private float foldSeconds = 0.18f;
        [Tooltip("PlayerPrefs key that remembers the fold on this device. Empty = always opens unfolded.")]
        [SerializeField] private string foldKey;

        private bool _closed;
        private bool _folded;
        private float _from, _to, _t = 1f;

        public bool IsClosed => _closed;
        public bool IsFolded => _folded;

        private void Awake()
        {
            if (minimiseButton) minimiseButton.onClick.AddListener(ToggleFold);
            if (closeButton) closeButton.onClick.AddListener(Close);
            _folded = !string.IsNullOrEmpty(foldKey) && PlayerPrefs.GetInt(foldKey, 0) == 1;
            ApplyFold(instant: true);
        }

        private void OnDestroy()
        {
            if (minimiseButton) minimiseButton.onClick.RemoveListener(ToggleFold);
            if (closeButton) closeButton.onClick.RemoveListener(Close);
        }

        public void ToggleFold()
        {
            _folded = !_folded;
            if (!string.IsNullOrEmpty(foldKey))
            {
                PlayerPrefs.SetInt(foldKey, _folded ? 1 : 0);
                PlayerPrefs.Save();
            }
            ApplyFold(instant: false);
        }

        public void Close()
        {
            _closed = true;
            KitVisibility.SetShown(cardGroup, false);
        }

        private void ApplyFold(bool instant)
        {
            KitVisibility.SetShown(detailGroup, !_folded);
            if (!card) return;
            _from = card.sizeDelta.y;
            _to = _folded ? foldedHeight : expandedHeight;
            _t = instant || foldSeconds <= 0f ? 1f : 0f;
            if (_t >= 1f) SetHeight(_to);
        }

        private void Update()
        {
            if (_t >= 1f || !card) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / foldSeconds);
            float eased = 1f - (1f - _t) * (1f - _t);
            SetHeight(Mathf.Lerp(_from, _to, eased));
        }

        private void SetHeight(float h)
        {
            var size = card.sizeDelta;
            if (Mathf.Approximately(size.y, h)) return;
            size.y = h;
            card.sizeDelta = size;
            if (card.parent is RectTransform parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
        }
    }
}
