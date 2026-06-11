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
