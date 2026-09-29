using System;
using System.Threading.Tasks;
using CosmicShore.Engine.Tasks;

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

        /// <summary>
        /// Raised when a scene is unloaded (original contract:
        /// <c>SceneManager.sceneUnloaded</c>). Nothing raises it automatically until the
        /// full loader lands; harnesses announce via <see cref="NotifySceneUnloaded"/>.
        /// </summary>
        public static event Action<Scene> sceneUnloaded;

        /// <summary>Raised when the active scene changes (original: activeSceneChanged(previous, next)).</summary>
        public static event Action<Scene, Scene> activeSceneChanged;

        /// <summary>Announce a completed scene unload to all subscribers.</summary>
        public static void NotifySceneUnloaded(Scene scene) => sceneUnloaded?.Invoke(scene);

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

        static readonly Scene s_noScene = new("");

        /// <summary>
        /// Arc E: the content backend that performs real loads (unload unmarked roots,
        /// instantiate the scene's serialized content). Null keeps the original minimal
        /// semantic (re-designate the loop scene + announce) that headless harnesses use.
        /// </summary>
        public static ISceneContentBackend Backend { get; set; }

        /// <summary>
        /// Engine hook: raised just before a Single load unloads the outgoing scene's roots, so
        /// systems that keep objects alive across loads (Netcode's DestroyWithScene=false objects)
        /// can move them out first.
        /// </summary>
        public static event System.Action<Scene> BeforeSingleLoadUnload;

        static void Perform(string sceneName, LoadSceneMode mode)
        {
            var scene = GameLoop.Current?.Scene;
            if (Backend != null && scene != null)
            {
                if (mode == LoadSceneMode.Single)
                {
                    BeforeSingleLoadUnload?.Invoke(scene);
                    NotifySceneUnloaded(scene);
                    foreach (var root in new System.Collections.Generic.List<GameObject>(scene.GetRootGameObjects()))
                        if (!root.IsDontDestroyOnLoad) Object.DestroyImmediate(root);
                    scene.name = sceneName;
                    scene.RenewHandle();
                    activeSceneChanged?.Invoke(scene, scene);
                }
                Backend.Load(sceneName, mode, scene);
                NotifySceneLoaded(scene, mode);
                return;
            }
            if (scene != null) scene.name = sceneName;
            NotifySceneLoaded(scene ?? new Scene(sceneName), mode);
        }

        /// <summary>
        /// The active scene (original contract: <c>UnityEngine.SceneManagement.SceneManager
        /// .GetActiveScene()</c>). One GameLoop owns one Scene, so the current loop's scene
        /// IS the active scene; with no loop running this returns an unnamed placeholder
        /// (mirroring the original's invalid-scene return, whose <c>.name</c> reads empty)
        /// so <c>GetActiveScene().name</c> is always null-safe.
        /// </summary>
        public static Scene GetActiveScene() => GameLoop.Current?.Scene ?? s_noScene;

        /// <summary>
        /// Minimal scene load (original contract: <c>UnityEngine.SceneManagement.SceneManager
        /// .LoadSceneAsync</c>, whose AsyncOperation ported call sites await —
        /// <c>.ToUniTask(ct)</c> → <c>Task.WaitAsync(ct)</c>). The port has no scene assets
        /// to instantiate yet, so the minimal semantic preserves the two observables ported
        /// code depends on: after completion (next PlayerLoop tick, matching the original's
        /// async apply) <see cref="GetActiveScene"/> reads the new scene name, and
        /// <see cref="sceneLoaded"/> fires with the active scene. Content teardown +
        /// instantiation arrive with the full loader in the content phase — this does NOT
        /// destroy or create objects, it re-designates the loop-owned scene.
        /// </summary>
        public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            var op = new AsyncOperation { progress = 0f };
            _ = Run(op, sceneName, mode);
            return op;
        }

        public static AsyncOperation LoadSceneAsync(int sceneBuildIndex, LoadSceneMode mode = LoadSceneMode.Single)
            => LoadSceneAsync(BuildIndexToName?.Invoke(sceneBuildIndex) ?? sceneBuildIndex.ToString(), mode);

        static async Task Run(AsyncOperation op, string sceneName, LoadSceneMode mode)
        {
            try
            {
                await GameTask.Yield();
                op.progress = 0.9f;
                if (!op.allowSceneActivation)
                {
                    var gate = new TaskCompletionSource<bool>();
                    op.ActivationAllowed += () => gate.TrySetResult(true);
                    if (!op.allowSceneActivation) await gate.Task;
                }
                Perform(sceneName, mode);
                op.Complete();
            }
            catch (Exception e) { Debug.LogException(e); op.Fail(e); }
        }

        /// <summary>Unloads an additive scene's objects (single-scene port: raises sceneUnloaded).</summary>
        public static AsyncOperation UnloadSceneAsync(Scene scene)
        {
            NotifySceneUnloaded(scene);
            return AsyncOperation.Completed();
        }

        public static AsyncOperation UnloadSceneAsync(string sceneName) => UnloadSceneAsync(GetSceneByName(sceneName));

        /// <summary>Build-list name lookup (installed by the content runtime).</summary>
        public static Func<int, string> BuildIndexToName { get; set; }

        public static Scene GetSceneByName(string name)
        {
            var active = GetActiveScene();
            return active != null && string.Equals(active.name, name, StringComparison.OrdinalIgnoreCase) ? active : null;
        }

        public static int sceneCount => GetActiveScene() != null ? 1 : 0;
        public static int loadedSceneCount => sceneCount;
        public static int sceneCountInBuildSettings => BuildSceneCount?.Invoke() ?? 0;
        public static Func<int> BuildSceneCount { get; set; }
        public static Scene GetSceneAt(int index) => index == 0 ? GetActiveScene() : null;
        public static bool SetActiveScene(Scene scene) => scene != null && ReferenceEquals(scene, GetActiveScene());
        public static void MoveGameObjectToScene(GameObject go, Scene scene) { }

        /// <summary>
        /// Synchronous variant (original contract: <c>SceneManager.LoadScene</c> — the
        /// defensive fallback ported loaders take when the async path is unavailable).
        /// Same minimal semantic as <see cref="LoadSceneAsync"/>, applied immediately.
        /// </summary>
        public static void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
            => Perform(sceneName, mode);

        public static void LoadScene(int sceneBuildIndex, LoadSceneMode mode = LoadSceneMode.Single)
            => Perform(BuildIndexToName?.Invoke(sceneBuildIndex) ?? sceneBuildIndex.ToString(), mode);
    }

    /// <summary>Arc E: performs real scene content loads for <see cref="SceneManager"/>.</summary>
    public interface ISceneContentBackend
    {
        /// <summary>Instantiates <paramref name="sceneName"/>'s serialized content into <paramref name="scene"/>.</summary>
        void Load(string sceneName, LoadSceneMode mode, Scene scene);
    }
}
