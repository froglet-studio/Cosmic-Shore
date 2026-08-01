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

        [Tooltip("Raised once when the user has ASKED to quit but the quit has been deferred, " +
                 "giving subscribers a short bounded window to finish outbound network work " +
                 "(e.g. leaving the UGS presence lobby so peers stop seeing this player online). " +
                 "Unlike OnAppQuitting - which fires from OnApplicationQuit, after teardown has " +
                 "begun and too late for anything async - this fires while the app is still fully " +
                 "alive. Do NOT do slow work here; the window is capped.")]
        public ScriptableEventNoParam OnAppQuitRequested;

        [Header("Scene Lifecycle")]
        [Tooltip("Raised when a scene finishes loading. Passes the scene name.")]
        public ScriptableEventString OnSceneLoaded;

        [Tooltip("Raised just before a scene is unloaded. Passes the scene name.")]
        public ScriptableEventString OnSceneUnloading;
    }
}
