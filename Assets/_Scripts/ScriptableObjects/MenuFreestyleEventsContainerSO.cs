using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// SOAP event container for Menu_Main freestyle/menu state transitions.
    /// Raised by <see cref="Gameplay.MenuCrystalClickHandler"/> when the player
    /// toggles between the menu camera (crystal revolve) and the freestyle vessel camera.
    ///
    /// Consumers (ScreenSwitcher, NavBar, Freestyle HUD) subscribe to these events
    /// to show/hide their UI without direct coupling to the click handler.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MenuFreestyleEvents",
        menuName = "ScriptableObjects/SOAP/Data Containers/MenuFreestyleEvents")]
    public class MenuFreestyleEventsContainerSO : ScriptableObject
    {
        [Header("State Transitions")]
        [Tooltip("Raised when the player taps a crystal and enters freestyle mode. " +
                 "Menu UI should hide, freestyle UI should appear, camera follows vessel.")]
        public ScriptableEventNoParam OnEnterFreestyle;

        [Tooltip("Raised when the player taps center-screen to return to menu mode. " +
                 "Freestyle UI should hide, menu UI should appear, camera returns to crystal revolve.")]
        public ScriptableEventNoParam OnExitFreestyle;
    }
}
