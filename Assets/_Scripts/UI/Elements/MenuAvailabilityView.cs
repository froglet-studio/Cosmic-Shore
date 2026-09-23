using CosmicShore.Core;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The ONE place a <see cref="MenuAvailability"/> becomes pixels and a response. Every menu
    /// entry that can ship before it is finished — a home-hub entry (<see cref="MenuHubButton"/>),
    /// a nav-bar link for a screen in <c>ScreenSwitcher.disabledScreens</c>, a
    /// <see cref="NavLink"/> tab — carries one of these rather than growing its own locked look.
    ///
    /// <para><b>Why one component and not one method per host.</b> The three hosts do completely
    /// different things when they are Available: open a modal, navigate to a screen, select a view.
    /// Their <i>targets</i> are different types (<c>ScreenSwitcher.ModalWindows</c>,
    /// <c>MenuScreens</c>, a <c>View</c>) and cannot be unified. What CAN be unified — and what the
    /// player actually learns — is the state and how it reads: the same dimming, the same padlock,
    /// the same refusal sting, the same wording. So the shared piece is the state and its
    /// presentation, never the target.</para>
    ///
    /// <para><b>It must read as locked with NO authored art.</b> Overlays and a label are optional,
    /// because the surfaces that need this most are the ones nobody drew a locked state for — the
    /// nav-bar links are two <c>Image</c> children and an <c>EventTrigger</c>, nothing else. When
    /// no overlay and no label are wired, the view falls back to dimming the host's own
    /// <see cref="Graphic"/>s, which is a real visual difference bought with zero authoring. It
    /// <b>tints</b> rather than disables, because an absent graphic does not raycast — switching one
    /// off would silently delete the touch target this entry still needs in order to be pressable
    /// enough to refuse.</para>
    ///
    /// <para>Presentation is applied on enable and on every change, so a host may set availability
    /// before or after this component wakes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuAvailabilityView : MonoBehaviour
    {
        [Header("State")]
        [SerializeField, Tooltip("Available does the entry's job; Locked refuses with a reason; " +
                                 "Unavailable is inert. Flip to Available when the thing behind it " +
                                 "is ready - nothing else has to change.")]
        MenuAvailability availability = MenuAvailability.Available;

        [Header("Authored art (all optional)")]
        [SerializeField, Tooltip("Shown while Locked. A padlock, a dimming veil - whatever the " +
                                 "design says 'not yet' with.")]
        GameObject lockedOverlay;

        [SerializeField, Tooltip("Shown while Unavailable.")]
        GameObject unavailableOverlay;

        [SerializeField, Tooltip("Optional label tinted down while this entry is not Available.")]
        TMP_Text label;

        [SerializeField, Tooltip("Colour the label takes while Locked or Unavailable.")]
        Color unavailableLabelColor = new(1f, 1f, 1f, 0.4f);

        [Header("Fallback when nothing above is authored")]
        [SerializeField, Tooltip("Multiply this entry's own Graphics by the tint below while it is " +
                                 "not Available. This is what makes a link with no locked art still " +
                                 "READ as locked. Off only if a host draws its own state some other way.")]
        bool dimOwnGraphics = true;

        [SerializeField, Tooltip("Multiplied into each Graphic's authored colour while not Available.")]
        Color dimTint = new(1f, 1f, 1f, 0.35f);

        [Header("Refusal")]
        [SerializeField, Tooltip("Toast shown when a Locked entry is pressed. Empty shows none - " +
                                 "the sting alone is the answer.")]
        string lockedMessage = "Not open yet.";

        [Inject] AudioSystem audioSystem;

        Selectable _selectable;
        Graphic[] _graphics;
        Color[] _graphicColors;
        Color _labelColor;
        bool _captured;
        bool _reportedMissingSystem;

        /// <summary>What this entry currently does when pressed.</summary>
        public MenuAvailability Availability => availability;

        /// <summary>True while the entry does its job; false while it is Locked or Unavailable.</summary>
        public bool IsAvailable => availability == MenuAvailability.Available;

        void Awake() => Capture();

        void OnEnable()
        {
            Capture();
            Apply();
        }

        /// <summary>
        /// Change this entry's state at runtime — the seam a progression unlock, or a
        /// <c>disabledScreens</c> list, plugs into. Idempotent.
        /// </summary>
        public void SetAvailability(MenuAvailability value)
        {
            availability = value;
            Apply();
        }

        /// <summary>The reason a Locked press gives. Empty leaves the sting to speak alone.</summary>
        public void SetLockedMessage(string message)
        {
            lockedMessage = message;
        }

        /// <summary>
        /// Ask before acting. Returns true when the caller should go ahead; returns false having
        /// ALREADY presented the refusal, so no host repeats the sting or the wording.
        /// </summary>
        public bool TryPress()
        {
            switch (availability)
            {
                case MenuAvailability.Available:
                    return true;

                case MenuAvailability.Locked:
                    PlayMenuAudio(MenuAudioCategory.Denied);
                    if (!string.IsNullOrWhiteSpace(lockedMessage))
                        ToastNotificationAPI.Show(lockedMessage);
                    return false;

                default:
                    // Unavailable has nothing to say. Silence IS the state - but it is a silence the
                    // player can see, because Apply() has dimmed the entry.
                    return false;
            }
        }

        void Capture()
        {
            if (_captured) return;
            _captured = true;

            _selectable = GetComponent<Selectable>();
            if (label) _labelColor = label.color;

            // Include inactive: a nav link's Active/Inactive icons are toggled with SetActive, so
            // the one that is currently off still has to come back correctly tinted.
            _graphics = GetComponentsInChildren<Graphic>(true);
            _graphicColors = new Color[_graphics.Length];
            for (int i = 0; i < _graphics.Length; i++)
                _graphicColors[i] = _graphics[i] ? _graphics[i].color : Color.white;
        }

        /// <summary>
        /// Re-applies the current state's presentation without re-reading the authored colours.
        /// Call after something else has written this entry's Graphics back to their authored
        /// values — <see cref="NavLink"/>'s crossfade does exactly that on every group selection,
        /// and without this the dim would be wiped the first time a sibling tab was chosen.
        /// </summary>
        public void Reapply() => Apply();

        /// <summary>Re-reads the authored colours. Call after something else re-tints this entry.</summary>
        public void Recapture()
        {
            _captured = false;
            Capture();
            Apply();
        }

        void Apply()
        {
            if (lockedOverlay) lockedOverlay.SetActive(availability == MenuAvailability.Locked);
            if (unavailableOverlay) unavailableOverlay.SetActive(availability == MenuAvailability.Unavailable);

            // A Locked entry stays pressable on purpose: the press is how the player is TOLD it is
            // locked. An Unavailable one has nothing to say, so it does not respond.
            if (_selectable) _selectable.interactable = availability != MenuAvailability.Unavailable;

            if (label) label.color = IsAvailable ? _labelColor : unavailableLabelColor;

            ApplyGraphicDim();
        }

        void ApplyGraphicDim()
        {
            // Only the fallback: if a host authored a locked look, that look IS the answer and
            // dimming on top of it would double the signal.
            if (!dimOwnGraphics || lockedOverlay || unavailableOverlay || label) return;
            if (_graphics == null) return;

            for (int i = 0; i < _graphics.Length; i++)
            {
                var g = _graphics[i];
                if (!g) continue;
                var authored = _graphicColors[i];
                g.color = IsAvailable
                    ? authored
                    : new Color(authored.r * dimTint.r, authored.g * dimTint.g,
                                authored.b * dimTint.b, authored.a * dimTint.a);
            }
        }

        void PlayMenuAudio(MenuAudioCategory category)
        {
            // The static instance is the same object DI hands out; it is the fallback for an object
            // nobody injected, never a replacement for injecting it. This view is ENSURED at runtime
            // on the nav-bar links, which no injector reaches.
            var system = audioSystem ? audioSystem : AudioSystem.Instance;
            if (!system) return;

            if (!audioSystem && !_reportedMissingSystem)
            {
                _reportedMissingSystem = true;
                CSDebug.LogWarningFormat(
                    "{0} on '{1}' was never injected and is falling back to AudioSystem.Instance.",
                    nameof(MenuAvailabilityView), name);
            }

            system.PlayMenuAudio(category);
        }

        /// <summary>
        /// Finds or adds the view on <paramref name="host"/>. Structural rather than authored, so a
        /// surface that has never been opened in the editor — every nav-bar link — still gets its
        /// state, and a screen added to <c>disabledScreens</c> tomorrow needs no scene edit.
        /// </summary>
        public static MenuAvailabilityView Ensure(GameObject host)
        {
            if (!host) return null;
            return host.TryGetComponent(out MenuAvailabilityView view)
                ? view
                : host.AddComponent<MenuAvailabilityView>();
        }
    }
}
