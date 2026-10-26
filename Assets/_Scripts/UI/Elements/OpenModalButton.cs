using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A button that opens one modal by TYPE through <see cref="ScreenSwitcher.OpenModal"/> - the
    /// Arcade header's Replays button is the first. The <see cref="MenuHubButton"/> rule without the
    /// hub's availability states: the button names a type and the switcher finds the window, so the
    /// switcher stays the one authority over the modal stack (gamepad B, the close sweeps). The window
    /// plays its own open sound, so the button plays none.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class OpenModalButton : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("Which modal this button opens. Its ModalWindowManager must be in the " +
                                 "ScreenSwitcher's Modals list - that is how it is found.")]
        ScreenSwitcher.ModalWindows target = ScreenSwitcher.ModalWindows.REPLAYS;

        [SerializeField, Tooltip("Leave empty to find the one in the scene at Start.")]
        ScreenSwitcher screenSwitcher;

        Button _button;

        public ScreenSwitcher.ModalWindows Target => target;

        void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(HandleClick);
        }

        void Start()
        {
            if (!screenSwitcher)
                screenSwitcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
        }

        void OnDestroy()
        {
            if (_button) _button.onClick.RemoveListener(HandleClick);
        }

        void HandleClick()
        {
            if (!screenSwitcher)
            {
                CSDebug.LogWarning($"[OpenModalButton] '{name}' has no ScreenSwitcher - '{target}' cannot be opened.");
                return;
            }

            screenSwitcher.OpenModal(target);
        }
    }
}
