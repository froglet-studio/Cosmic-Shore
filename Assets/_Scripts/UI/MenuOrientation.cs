using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Lets a phone turn upright in the menus and only there (Docs/MENU_REDESIGN_B.md section 138; Shombith, 2026-11-06:
    /// "we do need support for potrait mode unlocked", menus only). While Menu_Main is <see cref="MainMenuState.Ready"/>
    /// the screen auto-rotates between landscape and portrait, and the canvas lays itself out upright
    /// (<see cref="AdaptiveCanvasScaler"/>, <see cref="PortraitRect"/>). Freestyle, a game launch, the boot states and
    /// leaving the scene lock landscape-left again, the orientation flight, its cameras and the touch controls are built
    /// for (<c>DeviceOrientationHandler</c> locks the same when input starts).
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Menu Orientation")]
    public class MenuOrientation : MonoBehaviour
    {
        [Tooltip("The menu's state machine: portrait is allowed only while it is Ready.")]
        [SerializeField] private MainMenuController menu;

        /// <summary>Whether a menu state lets the phone turn upright.</summary>
        public static bool AllowsPortrait(MainMenuState state) => state == MainMenuState.Ready;

        private void OnEnable()
        {
            if (!menu) return;
            menu.OnStateChanged += OnState;
            OnState(menu.CurrentState);
        }

        private void OnDisable()
        {
            if (menu) menu.OnStateChanged -= OnState;
            LockLandscape();
        }

        private void OnState(MainMenuState state)
        {
            if (AllowsPortrait(state)) AllowPortrait();
            else LockLandscape();
        }

        private static void AllowPortrait()
        {
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            if (Screen.orientation != ScreenOrientation.AutoRotation) Screen.orientation = ScreenOrientation.AutoRotation;
        }

        private static void LockLandscape()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            if (Screen.orientation != ScreenOrientation.LandscapeLeft && Screen.orientation != ScreenOrientation.LandscapeRight)
                Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
    }
}
