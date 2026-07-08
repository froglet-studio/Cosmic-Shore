using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// A collection of root GameObjects owned by a <see cref="GameLoop"/>. Multi-scene
    /// support (additive loads, scene transitions) arrives with the content phase; until
    /// then one loop owns one scene.
    /// </summary>
    public sealed class Scene
    {
        readonly List<GameObject> _roots = new();

        public string name { get; set; }

        /// <summary>
        /// Original contract: the scene's index in build settings. The port has no build
        /// list; the single loop-owned scene reads 0 (the Bootstrap slot) until the full
        /// loader lands. Settable so harnesses can model non-bootstrap scenes.
        /// </summary>
        public int buildIndex { get; set; }

        /// <summary>
        /// Original engine contract: true once the scene's content is loaded. The single
        /// loop-owned scene is loaded for as long as its GameLoop is alive; objects probe
        /// this during teardown to skip work on an unloading scene.
        /// </summary>
        public bool isLoaded { get; internal set; } = true;

        internal Scene(string name) { this.name = name; }

        public IReadOnlyList<GameObject> GetRootGameObjects() => _roots;

        public int rootCount => _roots.Count;

        internal void AddRoot(GameObject go)
        {
            if (!_roots.Contains(go)) _roots.Add(go);
        }

        internal void RemoveRoot(GameObject go) => _roots.Remove(go);

        /// <summary>First component of type T found anywhere in the scene (slow — avoid in hot paths).</summary>
        public T FindObjectOfType<T>(bool includeInactive = false) where T : class
        {
            foreach (var root in _roots.ToArray())
            {
                var found = root.GetComponentInChildren<T>(includeInactive);
                if (found != null) return found;
            }
            return null;
        }
    }
}
