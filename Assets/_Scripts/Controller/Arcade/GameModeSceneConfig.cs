using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one object you drop into a game-mode scene to say "this scene is THIS mode, configured
    /// like THIS". It holds a single <see cref="GameModeUIConfigSO"/> reference and nothing else.
    ///
    /// <b>The point.</b> Shared prefabs (GameCanvas above all) must stay identical in every scene,
    /// so the per-mode differences need a home that is not a prefab override. That home is the
    /// config asset; this component is just how a scene points at it. One reference, on an object
    /// that is NOT part of any shared prefab, so nothing in the canvas is ever overridden.
    ///
    /// Consumers find it with <see cref="Resolve"/> rather than holding a serialized reference -
    /// that is what keeps "add extra references in the script" off the table.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Cosmic Shore/Game Mode Scene Config")]
    public class GameModeSceneConfig : MonoBehaviour
    {
        [Tooltip("Per-mode UI configuration for this scene. Leave empty and every consumer keeps " +
                 "its current behaviour - the config is purely additive.")]
        [SerializeField] GameModeUIConfigSO config;

        public GameModeUIConfigSO Config => config;

        static GameModeSceneConfig _cached;

        /// <summary>
        /// The active scene's config, or null when the scene has none (menus, tools, and any mode
        /// that has not been migrated yet). Callers MUST treat null as "carry on as before".
        ///
        /// Cached, with the cache validated against Unity's null so a scene change or domain
        /// reload can't hand back a destroyed component.
        /// </summary>
        public static GameModeUIConfigSO Resolve()
        {
            if (_cached == null)
                _cached = FindAnyObjectByType<GameModeSceneConfig>(FindObjectsInactive.Include);

            return _cached != null ? _cached.config : null;
        }

        void OnEnable() => _cached = this;

        void OnDisable()
        {
            if (_cached == this) _cached = null;
        }
    }
}
