using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The pointer-hover lift for a kit surface that is NOT a <see cref="KitButton"/>: a top-nav tab
    /// (pressed through an EventTrigger) or a keycap sitting on a plain Button. While the pointer is
    /// over this object, every target wears <see cref="hoverToken"/>; on exit, and on disable, each
    /// target re-applies its own token, so nothing is remembered and a theme edit is never fought.
    ///
    /// Pointer only. Gamepad focus belongs to a Selectable (<see cref="KitSelectableBase"/>): a top-nav tab's
    /// is its <see cref="KitTopNavTab"/> focus ring, and a keycap's plain Button shows none. Leave a target out (or disable this component) when
    /// something else drives its colour - a locked nav tab's label belongs to its MenuAvailabilityView.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Hover Tint")]
    public class KitHoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Theme")]
        [Tooltip("The chrome theme (Docs/STYLE_FOUNDATION.md §11). Unwired falls back to the authored §11 values.")]
        [SerializeField] private UIThemeSO theme;
        [Tooltip("The §11 colour role every target wears while the pointer is over this object.")]
        [SerializeField] private UIColorToken hoverToken = UIColorToken.TextLight;

        [Header("Targets")]
        [Tooltip("Labels lifted on hover. Each returns to its own KitText token on exit.")]
        [SerializeField] private KitText[] texts;
        [Tooltip("Chrome lifted on hover (a keycap border). Each returns to its own token on exit.")]
        [SerializeField] private KitThemedGraphic[] graphics;

        private bool _hovered;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            var c = UITheme.Resolve(theme, hoverToken);
            if (texts != null)
                foreach (var t in texts)
                    if (t && t.TryGetComponent(out TMP_Text tmp)) tmp.color = c;
            if (graphics != null)
                foreach (var g in graphics)
                    if (g && g.TryGetComponent(out Graphic graphic)) graphic.color = c;
        }

        public void OnPointerExit(PointerEventData eventData) => Restore();

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (!_hovered) return;
            _hovered = false;
            if (texts != null)
                foreach (var t in texts)
                    if (t) t.Apply();
            if (graphics != null)
                foreach (var g in graphics)
                    if (g) g.Apply();
        }
    }
}
