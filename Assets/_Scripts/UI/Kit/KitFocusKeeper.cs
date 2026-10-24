using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Keeps gamepad focus inside a panel whose rows are rebuilt under it. When the selected row is
    /// destroyed the EventSystem's selection silently becomes null, and with nothing selected the pad
    /// moves nothing until some other code selects again - which, in a list that repopulates on every
    /// party change, reads as a dead controller. This notices that the selection inside the panel was
    /// DESTROYED and puts focus on the panel's first live control instead.
    ///
    /// <para>Only a destroyed selection is replaced. A selection that was cleared while its object
    /// lives on (a click on empty space) or moved out of the panel is left alone.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Focus Keeper")]
    public class KitFocusKeeper : MonoBehaviour
    {
        // The last object selected inside this panel. Compared by reference, so a destroyed object
        // can be told apart from "nothing was selected".
        private GameObject _last;

        private void OnDisable() => _last = null;

        private void LateUpdate()
        {
            var events = EventSystem.current;
            if (!events) return;

            var selected = events.currentSelectedGameObject;
            if (selected)
            {
                _last = selected.transform.IsChildOf(transform) ? selected : null;
                return;
            }

            // Unity's null: the reference is held but the object is gone.
            bool destroyed = !ReferenceEquals(_last, null) && !_last;
            _last = null;
            if (!destroyed) return;

            foreach (var s in GetComponentsInChildren<Selectable>(false))
            {
                if (!s.IsInteractable() || s.navigation.mode == Navigation.Mode.None) continue;
                events.SetSelectedGameObject(s.gameObject);
                return;
            }
        }
    }
}
