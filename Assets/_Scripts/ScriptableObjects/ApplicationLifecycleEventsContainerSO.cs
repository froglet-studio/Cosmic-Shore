using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// SOAP event container for application lifecycle events.
    /// Raised by <see cref="Core.ApplicationLifecycleManager"/> and consumable
    /// by any system via inspector-wired EventListeners or code subscription.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ApplicationLifecycleEvents",
        menuName = "ScriptableObjects/Data Containers/ApplicationLifecycleEvents")]
    public class ApplicationLifecycleEventsContainerSO : ScriptableObject
    {
        [Header("App State")]
        [Tooltip("Raised when the app is paused (true) or resumed (false). Mobile: backgrounding/foregrounding.")]
        public ScriptableEventBool OnAppPaused;

        [Tooltip("Raised when the app gains (true) or loses (false) focus. Desktop: alt-tab, overlay windows.")]
        public ScriptableEventBool OnAppFocusChanged;

        [Tooltip("Raised once when the application is about to quit.")]
        public ScriptableEventNoParam OnAppQuitting;

        [Header("Scene Lifecycle")]
        [Tooltip("Raised when a scene finishes loading. Passes the scene name.")]
        public ScriptableEventString OnSceneLoaded;

        [Tooltip("Raised just before a scene is unloaded. Passes the scene name.")]
        public ScriptableEventString OnSceneUnloading;
    }
}
