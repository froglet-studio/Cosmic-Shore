using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// The hierarchy messages (original contract): <c>OnTransformChildrenChanged</c> on a parent whose
    /// child list changed (a child added, removed, destroyed or reordered), and
    /// <c>OnBeforeTransformParentChanged</c> / <c>OnTransformParentChanged</c> on an object that moves
    /// to another parent and on everything under it. uGUI is built on them: a layout group re-lays
    /// out when a card is instantiated into it, a graphic re-dirties the layout it moved into.
    /// Delivered to enabled behaviours on active objects, each guarded like a lifecycle call.
    /// </summary>
    internal static class TransformMessages
    {
        static readonly Func<LifecycleHooks, Action<MonoBehaviour>> s_children = h => h.TransformChildrenChanged;
        static readonly Func<LifecycleHooks, Action<MonoBehaviour>> s_before = h => h.BeforeTransformParentChanged;
        static readonly Func<LifecycleHooks, Action<MonoBehaviour>> s_after = h => h.TransformParentChanged;

        public static void ChildrenChanged(Transform parent) => Send(parent, s_children);
        public static void BeforeParentChanged(Transform moved) => SendSubtree(moved, s_before);
        public static void ParentChanged(Transform moved) => SendSubtree(moved, s_after);

        static void Send(Transform t, Func<LifecycleHooks, Action<MonoBehaviour>> pick)
        {
            if (t is null || t.destroyedFlag) return;
            var go = t.gameObject;
            if (go is null || go.destroyedFlag || !go.activeInHierarchy) return;
            var components = go.ComponentList;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is not MonoBehaviour mb || mb.destroyedFlag || !mb.enabled || mb.hooks is null) continue;
                var hook = pick(mb.hooks);
                if (hook is not null) mb.SendMessageHook(hook);
                if (i >= components.Count) break; // a handler removed components
            }
        }

        static void SendSubtree(Transform t, Func<LifecycleHooks, Action<MonoBehaviour>> pick)
        {
            if (t is null || !t.gameObject.activeInHierarchy) return;
            Send(t, pick);
            var children = t.Children;
            if (children.Count == 0) return;
            foreach (var child in children is Transform[] arr ? arr : System.Linq.Enumerable.ToArray(children))
                SendSubtree(child, pick);
        }
    }
}
