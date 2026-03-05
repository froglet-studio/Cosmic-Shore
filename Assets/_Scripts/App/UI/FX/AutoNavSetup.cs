using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.App.UI.FX
{
    /// <summary>
    /// Auto-configures Unity Selectable navigation on all child Selectables
    /// so gamepad DPad navigation works without manual explicit wiring.
    /// Attach to screen root panels, modal roots, or any container with buttons.
    /// </summary>
    public class AutoNavSetup : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("First element to select when this panel becomes active. " +
                 "Leave null to auto-pick the first interactable child.")]
        [SerializeField] private Selectable firstSelected;

        [Tooltip("Navigation mode to apply to child Selectables. " +
                 "Automatic works well for most grid/list layouts.")]
        [SerializeField] private Navigation.Mode navigationMode = Navigation.Mode.Automatic;

        [Tooltip("If true, selects the firstSelected element when this GO becomes active.")]
        [SerializeField] private bool selectOnEnable = true;

        void OnEnable()
        {
            ConfigureNavigation();

            if (selectOnEnable)
                SelectFirst();
        }

        /// <summary>
        /// Sets Navigation.Mode on all child Selectables that currently use None.
        /// Does not override explicitly configured navigation.
        /// </summary>
        public void ConfigureNavigation()
        {
            var selectables = GetComponentsInChildren<Selectable>(true);
            foreach (var selectable in selectables)
            {
                if (selectable.navigation.mode == Navigation.Mode.None)
                {
                    var nav = selectable.navigation;
                    nav.mode = navigationMode;
                    selectable.navigation = nav;
                }
            }
        }

        /// <summary>
        /// Selects the firstSelected element, or the first interactable child if null.
        /// Only acts when a gamepad is connected.
        /// </summary>
        public void SelectFirst()
        {
            if (UnityEngine.InputSystem.Gamepad.current == null) return;
            if (EventSystem.current == null) return;

            if (firstSelected != null && firstSelected.gameObject.activeInHierarchy && firstSelected.interactable)
            {
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
                return;
            }

            var selectables = GetComponentsInChildren<Selectable>(false);
            foreach (var s in selectables)
            {
                if (s.interactable && s.navigation.mode != Navigation.Mode.None)
                {
                    EventSystem.current.SetSelectedGameObject(s.gameObject);
                    return;
                }
            }
        }
    }
}
