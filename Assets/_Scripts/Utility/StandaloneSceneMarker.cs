using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Declares the scene it sits in STANDALONE: <see cref="CosmicShore.Core.AppManager"/>
    /// installs its DI bindings as usual but does not run the application boot chain —
    /// no network monitor, no UGS authentication, and no load of the Authentication scene.
    ///
    /// WHY THIS IS NEEDED. AppManager is a Reflex ROOT SCOPE
    /// (<c>Assets/Resources/ReflexSettings.asset</c>), so it reaches every play session in
    /// every scene, and <c>AppManager.Start()</c> had no scene guard at all: a bare test
    /// scene started UGS auth and then, after <c>MinimumSplashDuration</c>, replaced itself
    /// with Authentication. A measurement scene cannot measure anything under those terms.
    ///
    /// The decision is not new — <c>AppManager.AutoCreateBootstrapFlow</c> (the OTHER way
    /// AppManager can reach a scene) has always opened with
    /// <c>if (activeScene.buildIndex != 0) return;</c>. "Do not bootstrap outside Bootstrap"
    /// was settled for one path and never applied to the Reflex path. This finishes it.
    ///
    /// INSTALLING IS NOT BOOTING. <c>InstallBindings</c> runs at Reflex container
    /// construction, BEFORE any <c>Start</c>, so GameDataSO / CameraManager / ThemeManager
    /// and every other binding stay available to a standalone scene — only the boot
    /// SEQUENCE stands down. That split is what lets a detached scene still spawn a vessel.
    ///
    /// A MARKER, NOT A SCENE-NAME LIST. It lives in the scene, shows in the Hierarchy,
    /// survives a rename, and is copied when the scene is duplicated. One authority for one
    /// fact: a name list beside a marker would be two, and two authorities for one fact is
    /// how a setting comes to be accepted in one place and silently overridden in another.
    ///
    /// Deliberately carries NO compilation guard. It is a handful of lines, and a
    /// <c>#if UNITY_EDITOR</c> here would put the type out of reach of AppManager's
    /// unguarded reference in a release build — precisely the breakage
    /// Docs/CONDITIONAL_COMPILATION.md exists to prevent.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public class StandaloneSceneMarker : MonoBehaviour
    {
        /// <summary>
        /// True while a scene carrying this marker is loaded. Read by AppManager.Start().
        /// </summary>
        public static bool IsActive { get; private set; }

        /// <summary>
        /// A static holding RUNTIME state must be reset at construction, or in the editor it
        /// starts a session with whatever the last one left in it — the same rule the SOAP
        /// variables carry, and the same failure (ApplicationStateMachine spent its whole
        /// life refusing transitions because ShuttingDown survived a play-mode exit).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => IsActive = false;

        // Awake, not Start: Unity runs every scene object's Awake before ANY Start, and
        // AppManager reads this from Start. The execution order above is belt and braces.
        void Awake()
        {
            IsActive = true;

            // Reported on the HUD rather than the console: it is a standing condition a
            // developer needs to be able to SEE (otherwise "why is nothing connecting?" is a
            // mystery), not an event worth a log line. SetStat is a no-op outside the editor
            // and development builds.
            CosmicShore.Utility.PerformanceBenchmark.DiagnosticsHUD.SetStat(
                "Scene", "bootstrap", "stood down — standalone scene");
        }

        void OnDestroy()
        {
            // Clear on the way out so a scene loaded AFTER this one is not silently detached
            // too. AppManager.Start runs once per session, so this is correctness rather than
            // a live path, but a latched global that nothing clears is a trap for later.
            IsActive = false;
        }
    }
}
