using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.App.UI.FX
{
    /// <summary>
    /// Ensures gamepad DPad navigation works across all menu screens.
    /// When a gamepad is detected and no UI element is selected,
    /// auto-selects a sensible default on the current screen.
    /// Handles B-button as back/close for modals.
    /// </summary>
    public class MenuDPadNavigator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ScreenSwitcher screenSwitcher;

        [Header("Default Selectables Per Screen")]
        [Tooltip("First selectable to focus when navigating to each screen index. " +
                 "Array index matches ScreenSwitcher screen order.")]
        [SerializeField] private Selectable[] screenDefaults;

        [Header("Settings")]
        [SerializeField] private float inputRepeatDelay = 0.3f;

        private float _lastDPadTime;
        private bool _gamepadWasConnected;

        void Update()
        {
            if (Gamepad.current == null)
            {
                _gamepadWasConnected = false;
                return;
            }

            // When gamepad first detected, ensure something is selected
            if (!_gamepadWasConnected)
            {
                _gamepadWasConnected = true;
                EnsureSelection();
            }

            // If current selection is null or inactive, re-select
            var currentSelected = EventSystem.current?.currentSelectedGameObject;
            if (currentSelected == null || !currentSelected.activeInHierarchy)
            {
                if (AnyDPadPressed())
                    EnsureSelection();
            }

            // B button closes top modal
            if (Gamepad.current.buttonEast.wasPressedThisFrame)
                HandleBackButton();
        }

        /// <summary>
        /// Call this after screen transitions to focus the right element.
        /// </summary>
        public void OnScreenChanged(int screenIndex)
        {
            if (Gamepad.current == null) return;

            if (screenDefaults != null && screenIndex >= 0 && screenIndex < screenDefaults.Length)
            {
                var target = screenDefaults[screenIndex];
                if (target != null && target.gameObject.activeInHierarchy && target.interactable)
                {
                    EventSystem.current?.SetSelectedGameObject(target.gameObject);
                    return;
                }
            }

            EnsureSelection();
        }

        /// <summary>
        /// Select the first interactable Selectable found in the active screen.
        /// </summary>
        public void EnsureSelection()
        {
            if (EventSystem.current == null) return;

            var current = EventSystem.current.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy)
                return;

            // Find first active, interactable selectable in the scene
            var selectables = Selectable.allSelectablesArray;
            for (int i = 0; i < Selectable.allSelectableCount; i++)
            {
                var s = selectables[i];
                if (s != null && s.gameObject.activeInHierarchy && s.interactable && s.navigation.mode != Navigation.Mode.None)
                {
                    EventSystem.current.SetSelectedGameObject(s.gameObject);
                    return;
                }
            }
        }

        private void HandleBackButton()
        {
            if (screenSwitcher == null) return;

            // Try to close the top modal via ScreenSwitcher's modal system
            var modals = screenSwitcher.GetComponentsInChildren<Modals.ModalWindowManager>(true);
            for (int i = modals.Length - 1; i >= 0; i--)
            {
                if (modals[i].gameObject.activeSelf)
                {
                    modals[i].ModalWindowOut();
                    return;
                }
            }
        }

        private static bool AnyDPadPressed()
        {
            if (Gamepad.current == null) return false;
            var dpad = Gamepad.current.dpad;
            return dpad.up.wasPressedThisFrame || dpad.down.wasPressedThisFrame ||
                   dpad.left.wasPressedThisFrame || dpad.right.wasPressedThisFrame;
        }
    }
}
