using CosmicShore.Core;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One entry on the home screen's <b>hub</b> — Mission, Toy Box, Arena, Arcade. Each opens its
    /// own modal, and each can be shipped before it is finished.
    ///
    /// <para>The button names a modal <b>TYPE</b> and lets <see cref="ScreenSwitcher"/> find it,
    /// rather than holding a direct reference to the window. That is what keeps one authority over
    /// a modal's lifecycle: the switcher already owns the modal stack, the return-to-modal
    /// PlayerPrefs and the close-everything sweeps, and a button that reached past it to call
    /// <c>ModalWindowIn</c> would be a second one.</para>
    ///
    /// <para><b>Availability is a state, not a missing button</b>, and that state is no longer this
    /// component's to define. It lives in <see cref="MenuAvailability"/> and is drawn by
    /// <see cref="MenuAvailabilityView"/>, which this button ENSURES on itself — the same enum and
    /// the same visual states the nav-bar links and <see cref="NavLink"/> use. This class keeps only
    /// what is genuinely its own: which modal it opens, and what Available means here.
    /// See <c>Docs/HomeHub/ARCHITECTURE.md</c> §2.</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuHubButton : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("Which modal this hub entry opens. Its ModalWindowManager must be " +
                                 "in the ScreenSwitcher's Modals list - that is how it is found.")]
        ScreenSwitcher.ModalWindows target = ScreenSwitcher.ModalWindows.ARCADE;

        [SerializeField, Tooltip("Leave empty to find the one in the scene at Start.")]
        ScreenSwitcher screenSwitcher;

        [Inject] AudioSystem audioSystem;

        Button _button;
        MenuAvailabilityView _availability;

        void Awake()
        {
            _button = GetComponent<Button>();
            // Ensured, not required: a hub entry authored before this split, or added to a scene by
            // a tool, still gets its state rather than silently reading as Available.
            _availability = MenuAvailabilityView.Ensure(gameObject);
        }

        void Start()
        {
            if (!screenSwitcher)
                screenSwitcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);

            _button.onClick.AddListener(HandleClick);
        }

        void OnDestroy()
        {
            if (_button) _button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>
        /// Change this entry's state at runtime - the seam a progression unlock plugs into, so
        /// opening Arena later needs no new plumbing here. Delegates to the shared view; there is
        /// exactly one place the state is stored.
        /// </summary>
        public void SetAvailability(MenuAvailability value)
        {
            if (!_availability) _availability = MenuAvailabilityView.Ensure(gameObject);
            _availability.SetAvailability(value);
        }

        void HandleClick()
        {
            // The view answers Locked and Unavailable itself - the sting, the wording and the
            // silence are the same on every menu surface, so no host repeats them.
            if (_availability && !_availability.TryPress()) return;

            if (!screenSwitcher)
            {
                CSDebug.LogWarning($"[MenuHubButton] '{name}' has no ScreenSwitcher - " +
                                   $"'{target}' cannot be opened.");
                return;
            }

            if (audioSystem) audioSystem.PlayMenuAudio(MenuAudioCategory.OpenView);
            screenSwitcher.OpenModal(target);
        }
    }
}
