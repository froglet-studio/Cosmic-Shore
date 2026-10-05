using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Every live component of one type, in creation order, for per-frame consumers that would
    /// otherwise sweep the whole scene graph (the render backend's sun and canvas lookups).
    /// Registration happens in <see cref="GameObject.AddComponent(System.Type)"/>, so clones are
    /// covered; destroyed entries are pruned as they are found. Activity is the caller's to
    /// test, exactly as with <see cref="Renderer.CollectLive"/>.
    /// </summary>
    public static class LiveComponents<T> where T : Component
    {
        static readonly List<T> s_live = new();

        internal static void Register(T component) => s_live.Add(component);

        /// <summary>
        /// Every registered component that is active in a scene (not part of a prefab asset),
        /// the same set <c>FindObjectsByType&lt;T&gt;</c> answers, in creation order.
        /// </summary>
        public static void CollectActive(List<T> into)
        {
            into.Clear();
            int w = 0;
            for (int i = 0; i < s_live.Count; i++)
            {
                var c = s_live[i];
                var go = c.gameObject;
                if (c.destroyedFlag || go == null || go.destroyedFlag) continue;
                s_live[w++] = c;
                if (!go.activeInHierarchy || go.transform.root.gameObject.isPrefabAsset) continue;
                into.Add(c);
            }
            s_live.RemoveRange(w, s_live.Count - w);
        }
    }
}
