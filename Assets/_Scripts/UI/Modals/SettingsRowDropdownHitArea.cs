using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Makes a settings ROW open the dropdown that sits in it, so the whole plate is the hit
    /// target rather than just the control.
    ///
    /// The options rows are authored as a full-width plate (~1765u) carrying a left-hand label and
    /// a dropdown pinned to the right at 384u wide, whose own label is 300u of that. The plate is a
    /// raycast-on <see cref="Image"/> with no click handler, so every click outside that 384u
    /// window - including the dead plate to the RIGHT of the caret, which is wider than the
    /// dropdown itself - was silently absorbed by the row. The control reads as one long button and
    /// behaves like a short one: it opens if you hit the number and does nothing if you hit
    /// anywhere else, with no visual difference between the live part and the dead part.
    ///
    /// Attached at bind time by <see cref="GameSettingsPanelController"/> rather than authored, so
    /// every dropdown on every tab is covered and a row added later inherits it.
    ///
    /// This only ever ADDS hit area. A click that lands inside the dropdown's own rect (its plate,
    /// its label, its caret) is handled by <see cref="TMP_Dropdown"/> itself and never reaches this
    /// component: pointer events stop at the first ancestor that handles them, and the dropdown is
    /// that ancestor for its whole subtree.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsRowDropdownHitArea : MonoBehaviour, IPointerClickHandler
    {
        TMP_Dropdown dropdown;

        /// <summary>
        /// Gives <paramref name="dd"/>'s row the dropdown's click behaviour. No-op when the
        /// dropdown has no parent row, or when that row draws nothing that can be clicked (a row
        /// with no raycast-target <see cref="Graphic"/> receives no pointer events, so there is
        /// nothing to forward).
        /// </summary>
        public static void Attach(TMP_Dropdown dd)
        {
            if (dd == null) return;

            var row = dd.transform.parent;
            if (row == null) return;

            var graphic = row.GetComponent<Graphic>();
            if (graphic == null || !graphic.raycastTarget) return;

            var hitArea = row.GetComponent<SettingsRowDropdownHitArea>();
            if (hitArea == null) hitArea = row.gameObject.AddComponent<SettingsRowDropdownHitArea>();
            hitArea.dropdown = dd;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (dropdown == null || !dropdown.IsActive() || !dropdown.IsInteractable()) return;

            // Show() is itself a no-op while the list is open, so a repeat click can't stack two.
            dropdown.Show();
        }
    }
}
