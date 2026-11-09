using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one place that decides whether a phone may turn upright (Shombith, 2026-11-08: the portrait plan). Two things
    /// can allow it:
    /// <list type="bullet">
    /// <item><b>The menu</b> (<c>MenuOrientation</c>): the app shell while Menu_Main is Ready.</item>
    /// <item><b>A seat</b> (<see cref="CrewOrientation"/>): the local person flies a crew whose seat plays held upright
    /// (<see cref="IUprightSeat"/>; the Goby and Pistol: upright is the Pistol's view), in a game or in the Toybox.</item>
    /// </list>
    /// With neither, the screen is landscape, as the flight cameras and touch controls are built for: every other vessel
    /// turns nothing. A new scene clears the seat's allowance (a crew that went away with its scene leaves no
    /// allowance behind); the menu re-states its own when it wakes.
    ///
    /// <para>The Wrasse's seat still turns the screen itself (<c>SeatOrientation.Portrait</c>, a fixed orientation, not
    /// auto-rotation); this policy only writes when an allowance changes, so it never fights that.</para>
    /// </summary>
    public static class ScreenOrientationPolicy
    {
        public enum Source
        {
            Menu = 0,
            Seat = 1,
        }

        static bool _menu, _seat;

        /// <summary>Whether anything allows the phone to turn upright now.</summary>
        public static bool PortraitAllowed => _menu || _seat;

        /// <summary>Allow or stop portrait for <paramref name="source"/>. The screen is written when the answer changes,
        /// and on every menu call (a menu state change re-states the lock, as <c>MenuOrientation</c> always did).</summary>
        public static void Allow(Source source, bool allow)
        {
            bool before = PortraitAllowed;
            if (source == Source.Menu) _menu = allow; else _seat = allow;
            if (source == Source.Menu || PortraitAllowed != before) Apply();
        }

        /// <summary>Put the allowance back on the screen after something else locked it
        /// (<see cref="DeviceOrientationHandler"/> locks landscape-left when a pilot's input starts).</summary>
        public static void Reassert()
        {
            if (PortraitAllowed) AllowPortrait();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _menu = _seat = false;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        static void OnSceneChanged(Scene from, Scene to)
        {
            if (_seat) Allow(Source.Seat, false);
        }

        static void Apply()
        {
            if (PortraitAllowed) AllowPortrait();
            else LockLandscape();
        }

        static void AllowPortrait()
        {
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            if (Screen.orientation != ScreenOrientation.AutoRotation) Screen.orientation = ScreenOrientation.AutoRotation;
        }

        static void LockLandscape()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            if (Screen.orientation != ScreenOrientation.LandscapeLeft && Screen.orientation != ScreenOrientation.LandscapeRight)
                Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
    }
}
