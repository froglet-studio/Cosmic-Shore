using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One of Home's docked cards under the pilot chip: the Tide strip and the quest chain (Loop Atlas
    /// <c>era.tide</c>, the picked Frogma option C, Garrett 2026-11-21: "lets not x them out"). A card rests
    /// folded to its strip (its eyebrow, its one-line summary and, on the Tide, the clock) and opens to the
    /// full card while the pad's focus is on it or the pointer is over it; it folds again when both leave.
    /// There is no fold or close button. On a touch screen there is no hover, so a tap goes straight to the
    /// card's press (the window it opens).
    ///
    /// <para>The card's data and its show / hide stay with <see cref="HomeUpNext"/>. This component sits on
    /// the card's own GameObject, beside its Button, so the select events reach it.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Home Dock Card")]
    public class HomeDockCard : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Wiring")]
        [Tooltip("Everything the folded strip does not show: hidden while the card is folded.")]
        [SerializeField] private CanvasGroup detailGroup;
        [Tooltip("The card's rect; its height folds.")]
        [SerializeField] private RectTransform card;

        [Header("Fold")]
        [SerializeField, Min(1f)] private float expandedHeight = 132f;
        [SerializeField, Min(1f)] private float foldedHeight = 56f;
        [Tooltip("Seconds the fold takes.")]
        [SerializeField, Min(0f)] private float foldSeconds = 0.18f;

        private bool _selected;
        private bool _hovered;
        private bool _folded = true;
        private float _from, _to, _t = 1f;

        public bool IsFolded => _folded;

        private void Awake() => ApplyFold(instant: true);

        private void OnDisable()
        {
            _selected = _hovered = false;
            _folded = true;
            ApplyFold(instant: true);
        }

        public void OnSelect(BaseEventData eventData) { _selected = true; Refold(); }
        public void OnDeselect(BaseEventData eventData) { _selected = false; Refold(); }
        public void OnPointerEnter(PointerEventData eventData) { _hovered = true; Refold(); }
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; Refold(); }

        private void Refold()
        {
            bool folded = !(_selected || _hovered);
            if (folded == _folded) return;
            _folded = folded;
            ApplyFold(instant: false);
        }

        private void ApplyFold(bool instant)
        {
            if (detailGroup) KitVisibility.SetShown(detailGroup, !_folded);
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
