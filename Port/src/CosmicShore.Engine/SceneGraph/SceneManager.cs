using System;

namespace CosmicShore.Engine.SceneManagement
{
    /// <summary>
    /// Load mode for a scene transition (original UnityEngine.SceneManagement numeric values —
    /// the Tournament meta chains sequential Single loads; Additive is never used there).
    /// </summary>
    public enum LoadSceneMode
    {
        Single = 0,
        Additive = 1,
    }

    /// <summary>
    /// Engine addition (Tournament arc): the scene-load announcement surface ported code
    /// subscribes to (original contract: <c>UnityEngine.SceneManagement.SceneManager.sceneLoaded</c>).
    /// Substitution: <c>using UnityEngine.SceneManagement;</c> →
    /// <c>using CosmicShore.Engine.SceneManagement;</c>.
    ///
    /// The port has no scene transitions yet (one GameLoop owns one Scene — see
    /// <see cref="Scene"/>), so nothing raises <see cref="sceneLoaded"/> automatically.
    /// Harnesses (CLI rounds, tests) announce loads via <see cref="NotifySceneLoaded(string, LoadSceneMode)"/>;
    /// when real scene management lands it becomes the single raiser. Subscribers written
    /// against the original API (e.g. <c>TournamentController</c>) port verbatim.
    /// </summary>
    public static class SceneManager
    {
        /// <summary>Raised after a scene load completes (port surface: raised by <see cref="NotifySceneLoaded(Scene, LoadSceneMode)"/>).</summary>
        public static event Action<Scene, LoadSceneMode> sceneLoaded;

        /// <summary>Announce a completed scene load to all subscribers.</summary>
        public static void NotifySceneLoaded(Scene scene, LoadSceneMode mode = LoadSceneMode.Single)
            => sceneLoaded?.Invoke(scene, mode);

        /// <summary>Announce a completed scene load by name (constructs the Scene handle).</summary>
        public static void NotifySceneLoaded(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
            => NotifySceneLoaded(new Scene(sceneName), mode);

        /// <summary>
        /// Port surface (test/harness hygiene): drops every <see cref="sceneLoaded"/> subscriber.
        /// Persistent singletons (e.g. <c>TournamentController</c>) subscribe for the app lifetime
        /// and expose no unsubscribe; without this, controllers constructed by earlier tests keep
        /// reacting to later tests' scene notifications.
        /// </summary>
        public static void ResetSceneLoadedSubscribers() => sceneLoaded = null;
    }
}
