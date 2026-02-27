using Reflex.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Ensures every loaded scene has a Reflex <see cref="ContainerScope"/> so
    /// that [Inject] fields on scene MonoBehaviours are populated from the root
    /// DI container.
    ///
    /// The Bootstrap scene already has a ContainerScope (on the AppManager
    /// prefab, which is the Reflex RootScope). For all other scenes this bridge
    /// auto-creates one in the <see cref="SceneManager.sceneLoaded"/> callback,
    /// which fires after Awake/OnEnable but before Start.
    ///
    /// **Consequence**: code that accesses [Inject] fields must use Start() or
    /// later — not Awake()/OnEnable() — in any scene other than Bootstrap.
    /// </summary>
    static class SceneContainerScopeBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Clean up between domain reloads in the editor.
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Skip if the scene already has a ContainerScope (e.g., Bootstrap).
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<ContainerScope>() != null)
                    return;
            }

            // Create a minimal scope that inherits all root container bindings.
            // ContainerScope.Awake() fires immediately, triggering Reflex scene
            // injection before any Start() method runs.
            var go = new GameObject("[SceneScope]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<ContainerScope>();
        }
    }
}
