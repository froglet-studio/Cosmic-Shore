using CosmicShore.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drop this on a Quit Game button and pressing it closes the game - nothing to drag, no
    /// reference to wire. Quitting itself routes through <see cref="DesktopPlatformServices.Quit"/>,
    /// so Unity's normal shutdown path still runs (<c>ApplicationLifecycleManager</c> raises
    /// <c>OnAppQuitting</c>, the state machine reaches <c>ShuttingDown</c>, analytics gets its final
    /// flush) and the editor stops play mode instead of killing the editor.
    ///
    /// This exists because a quit button authored inside a NESTED prefab (the options panel's
    /// GeneralTabContent lives in <c>OptionsMenuContent.prefab</c>) cannot be dragged into the
    /// <c>quitGameButton</c> slot on <see cref="GameSettingsPanelController"/>, which sits on the
    /// parent <c>SettingsModal.prefab</c>. Either path works - use this one OR that slot, not both.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class QuitGameButton : MonoBehaviour
    {
        [SerializeField, Tooltip("Hide this button on platforms where the OS owns app exit (mobile, " +
             "console, WebGL). This is the policy the options panel already follows: a PC player " +
             "expects to close the game from its own UI, and iOS review treats a self-quit control " +
             "as a defect. Turn it off only for a platform that genuinely wants an in-game quit.")]
        bool desktopOnly = true;

        Button _button;

        void Awake()
        {
            _button = GetComponent<Button>();

            if (desktopOnly && !DesktopPlatformServices.IsDesktop)
            {
                gameObject.SetActive(false);
                return;
            }

            _button.onClick.AddListener(Quit);
        }

        void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(Quit);
        }

        /// <summary>
        /// Closes the game (stops play mode in the editor). Public so it can also be driven from a
        /// UnityEvent - calling it twice in one frame is harmless.
        /// </summary>
        public void Quit() => DesktopPlatformServices.Quit();
    }
}
