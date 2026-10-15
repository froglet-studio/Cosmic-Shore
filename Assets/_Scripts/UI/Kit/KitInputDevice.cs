using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.UI
{
    /// <summary>
    /// Whether the screen should name controls at all (Docs/MENU_REDESIGN_B.md section 28). A phone
    /// or tablet with no pad connected is played with fingers: a keycap reading "Start", "Y" or
    /// "RShift" there names a control the player does not have, so every keycap and control chip
    /// stands down. Connect a pad and they come back. Presentation only - which device FLIES the
    /// vessel is <c>InputDeviceActuation</c>'s answer, untouched here.
    /// </summary>
    public static class KitInputDevice
    {
        /// <summary>The device is touch-first: a phone, a tablet, or a phone's browser.</summary>
        public static bool IsTouchPrimary =>
            Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;

        /// <summary>True when keycaps and control chips should be drawn.</summary>
        public static bool ShowsControlHints => !IsTouchPrimary || Gamepad.current != null;

        /// <summary>Raised when a pad is connected or disconnected (the answer may have changed).</summary>
        public static event Action Changed;

        static bool _hooked;

        /// <summary>Subscribe to <see cref="Changed"/> through this, so the device hook is installed once.</summary>
        public static void Listen(Action handler)
        {
            if (!_hooked)
            {
                _hooked = true;
                InputSystem.onDeviceChange += (device, change) =>
                {
                    if (device is Gamepad && change is InputDeviceChange.Added or InputDeviceChange.Removed
                            or InputDeviceChange.Reconnected or InputDeviceChange.Disconnected)
                        Changed?.Invoke();
                };
            }
            Changed += handler;
        }

        public static void Unlisten(Action handler) => Changed -= handler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            _hooked = false;
        }
    }
}
