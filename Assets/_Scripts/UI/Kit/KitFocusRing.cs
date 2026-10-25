using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The pad / keyboard focus ring for a plain Selectable that is not a <see cref="KitSelectableBase"/>: a card's
    /// favourite star, a track's tick box. The ring is the kit's sky-blue Selection, shown while the control has the
    /// pad's or the keyboard's focus and hidden for a pointer (uGUI's :focus-visible, as KitSelectableBase draws it).
    /// Every place in the menu a press can reach is a pad stop (Yash, 2026-10-25: "make sure all the buttons and all
    /// the places where it's clickable can be navigated"; Docs/MENU_REDESIGN_B.md §65), and a stop the player cannot
    /// see is not one.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Focus Ring")]
    public class KitFocusRing : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerDownHandler
    {
        [Tooltip("The chrome theme (Docs/STYLE_FOUNDATION.md §11). Unwired falls back to the authored §11 values.")]
        [SerializeField] private UIThemeSO theme;
        [Tooltip("The ring drawn while this control has the pad's or the keyboard's focus.")]
        [SerializeField] private Graphic ring;
        [Tooltip("The ring's colour.")]
        [SerializeField] private UIColorToken ringToken = UIColorToken.Selection;

        public void OnSelect(BaseEventData eventData) => Show(eventData is not PointerEventData);

        public void OnDeselect(BaseEventData eventData) => Show(false);

        // A click on an already-focused control hides the ring, as KitSelectableBase does.
        public void OnPointerDown(PointerEventData eventData) => Show(false);

        private void OnEnable()
        {
            var es = EventSystem.current;
            Show(es && es.currentSelectedGameObject == gameObject);
        }

        private void OnDisable() => Show(false);

        private void Show(bool on)
        {
            if (!ring) return;
            var c = UITheme.Resolve(theme, ringToken);
            c.a *= on ? 1f : 0f;
            ring.color = c;
        }
    }
}
