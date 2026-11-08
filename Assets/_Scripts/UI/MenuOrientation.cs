using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Lets a phone turn upright in the menus and only there (Docs/MENU_REDESIGN_B.md section 138; Shombith, 2026-11-06:
    /// "we do need support for potrait mode unlocked", menus only). While Menu_Main is <see cref="MainMenuState.Ready"/>
    /// the screen auto-rotates between landscape and portrait, and the canvas lays itself out upright
    /// (<see cref="AdaptiveCanvasScaler"/>, <see cref="PortraitRect"/>). Freestyle, a game launch, the boot states and
    /// leaving the scene take the menu's allowance away, and the screen locks landscape-left again unless the person flies a
    /// crew seat played upright (<see cref="ScreenOrientationPolicy"/>, the Goby's Pistol).
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
            ScreenOrientationPolicy.Allow(ScreenOrientationPolicy.Source.Menu, false);
        }

        // One policy decides (ScreenOrientationPolicy): a crew seat played upright (the Goby's Pistol in the Toybox) may
        // still allow portrait while the menu does not.
        private static void OnState(MainMenuState state) =>
            ScreenOrientationPolicy.Allow(ScreenOrientationPolicy.Source.Menu, AllowsPortrait(state));
    }
}
