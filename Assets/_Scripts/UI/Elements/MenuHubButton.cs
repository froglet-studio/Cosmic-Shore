using CosmicShore.Core;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
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
    /// <para><b>Availability is a state, not a missing button.</b> A hub entry that is not ready
    /// stays on screen and says so — an entry that simply is not drawn tells the player the game
    /// has three things in it, and the day it ships they have to re-learn the screen. The two
    /// unfinished states differ in what they promise: <see cref="HubAvailability.Locked"/> is
    /// "this exists and you cannot open it yet" (Arena — the modal behind it is real and complete,
    /// gated by this one flag), and <see cref="HubAvailability.Unavailable"/> is "this is not
    /// built" (Mission), which does not respond at all.</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuHubButton : MonoBehaviour
    {
        /// <summary>What this hub entry currently does when pressed.</summary>
        public enum HubAvailability
        {
            /// <summary>Opens its modal.</summary>
            Available = 0,

            /// <summary>Exists but is closed off: refuses with a sting and a reason.</summary>
            Locked = 1,

            /// <summary>Not built yet: the button is inert and reads as such.</summary>
            Unavailable = 2,
        }

        [Header("Target")]
        [SerializeField, Tooltip("Which modal this hub entry opens. Its ModalWindowManager must be " +
                                 "in the ScreenSwitcher's Modals list - that is how it is found.")]
        ScreenSwitcher.ModalWindows target = ScreenSwitcher.ModalWindows.ARCADE;

        [SerializeField, Tooltip("Leave empty to find the one in the scene at Start.")]
        ScreenSwitcher screenSwitcher;

        [Header("Availability")]
        [SerializeField, Tooltip("Available opens the modal; Locked refuses with a reason; " +
                                 "Unavailable is inert. Flip Arena to Available when its mode " +
                                 "is ready - nothing else has to change.")]
        HubAvailability availability = HubAvailability.Available;

        [SerializeField, Tooltip("Shown while Locked. A padlock, a dimming veil - whatever the " +
                                 "design says 'not yet' with.")]
        GameObject lockedOverlay;

        [SerializeField, Tooltip("Shown while Unavailable.")]
        GameObject unavailableOverlay;

        [SerializeField, Tooltip("Optional label tinted down while this entry is not Available.")]
        TMP_Text label;

        [SerializeField, Tooltip("Colour the label takes while Locked or Unavailable.")]
        Color unavailableLabelColor = new(1f, 1f, 1f, 0.4f);

        [SerializeField, Tooltip("Toast shown when a Locked entry is pressed. Empty shows none - " +
                                 "the sting alone is the answer.")]
        string lockedMessage = "Not open yet.";

        [Inject] AudioSystem audioSystem;

        Button _button;
        Color _labelColor;
        bool _capturedLabelColor;

        void Awake()
        {
            _button = GetComponent<Button>();
            if (label)
            {
                _labelColor = label.color;
                _capturedLabelColor = true;
            }
        }

        void Start()
        {
            if (!screenSwitcher)
                screenSwitcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);

            _button.onClick.AddListener(HandleClick);
            Apply();
        }

        void OnDestroy()
        {
            if (_button) _button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>
        /// Change this entry's state at runtime - the seam a progression unlock plugs into, so
        /// opening Arena later needs no new plumbing here.
        /// </summary>
        public void SetAvailability(HubAvailability value)
        {
            availability = value;
            Apply();
        }

        void Apply()
        {
            if (lockedOverlay) lockedOverlay.SetActive(availability == HubAvailability.Locked);
            if (unavailableOverlay) unavailableOverlay.SetActive(availability == HubAvailability.Unavailable);

            // A Locked entry stays pressable on purpose: the press is how the player is TOLD it is
            // locked. An Unavailable one has nothing to say, so it does not respond.
            if (_button) _button.interactable = availability != HubAvailability.Unavailable;

            if (label && _capturedLabelColor)
                label.color = availability == HubAvailability.Available ? _labelColor : unavailableLabelColor;
        }

        void HandleClick()
        {
            switch (availability)
            {
                case HubAvailability.Available:
                    if (!screenSwitcher)
                    {
                        CSDebug.LogWarning($"[MenuHubButton] '{name}' has no ScreenSwitcher - " +
                                           $"'{target}' cannot be opened.");
                        return;
                    }
                    if (audioSystem) audioSystem.PlayMenuAudio(MenuAudioCategory.OpenView);
                    screenSwitcher.OpenModal(target);
                    break;

                case HubAvailability.Locked:
                    if (audioSystem) audioSystem.PlayMenuAudio(MenuAudioCategory.Denied);
                    if (!string.IsNullOrWhiteSpace(lockedMessage))
                        ToastNotificationAPI.Show(lockedMessage);
                    break;

                // Unavailable never gets here - the button is not interactable.
            }
        }
    }
}
