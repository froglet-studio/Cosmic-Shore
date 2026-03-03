using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// SOAP event container for Menu_Main freestyle/menu state transitions.
    /// Raised by <see cref="Gameplay.MenuCrystalClickHandler"/> when the player
    /// toggles between the menu camera (crystal revolve) and the freestyle vessel camera.
    ///
    /// Consumers (ScreenSwitcher, MainMenuController, MainMenuCameraController,
    /// MenuMiniGameHUD, MenuVesselSelectionPanelController) subscribe to these
    /// events to show/hide their UI without direct coupling to the click handler.
    ///
    /// <b>Transition events</b> (Start/End) bracket each async transition so
    /// subscribers can react at the beginning and end of the camera/UI blend:
    /// <list type="bullet">
    ///   <item><see cref="OnGameStateTransitionStart"/> — fired at the start of menu→freestyle</item>
    ///   <item><see cref="OnGameStateTransitionEnd"/> — fired after menu→freestyle completes</item>
    ///   <item><see cref="OnMenuStateTransitionStart"/> — fired at the start of freestyle→menu</item>
    ///   <item><see cref="OnMenuStateTransitionEnd"/> — fired after freestyle→menu completes</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(
        fileName = "MenuFreestyleEvents",
        menuName = "ScriptableObjects/Data Containers/MenuFreestyleEvents")]
    public class MenuFreestyleEventsContainerSO : ScriptableObject
    {
        [Header("Menu → Freestyle (Game State)")]
        [Tooltip("Raised at the START of a menu→freestyle transition, before fades/blends begin. " +
                 "Use for immediate setup (e.g. showing vessel HUD, starting camera blend).")]
        public ScriptableEventNoParam OnGameStateTransitionStart;

        [Tooltip("Raised at the END of a menu→freestyle transition, after all fades/blends complete.")]
        public ScriptableEventNoParam OnGameStateTransitionEnd;

        [Header("Freestyle → Menu (Menu State)")]
        [Tooltip("Raised at the START of a freestyle→menu transition, before fades/blends begin. " +
                 "Use for immediate teardown (e.g. hiding vessel HUD, starting camera blend).")]
        public ScriptableEventNoParam OnMenuStateTransitionStart;

        [Tooltip("Raised at the END of a freestyle→menu transition, after all fades/blends complete.")]
        public ScriptableEventNoParam OnMenuStateTransitionEnd;
    }
}
