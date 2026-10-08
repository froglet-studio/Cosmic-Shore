using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Drives the two-phase layout solve (original contract). Per axis (horizontal fully
    /// before vertical, since heights may depend on solved widths):
    ///
    ///   1. INPUT pass, bottom-up — every <see cref="ILayoutElement"/> computes its
    ///      min/preferred/flexible sizes, children before parents so a group's inputs
    ///      aggregate already-computed child inputs.
    ///   2. CONTROL pass, top-down — every <see cref="ILayoutController"/> applies
    ///      geometry, self-controllers (fitters) before group controllers on the same
    ///      node, parents before children so a group hands each child its cell before
    ///      the child's own group subdivides it.
    ///
    /// <see cref="MarkLayoutForRebuild"/> queues a rect's LAYOUT ROOT (the topmost
    /// ancestor chain of active layout groups) for a rebuild;
    /// <see cref="FlushQueuedRebuilds"/> runs after LateUpdate each tick (the engine
    /// loop's canvas-update slot — where the original engine rebuilds before rendering).
    /// Headless tests can also call <see cref="ForceRebuildLayoutImmediate"/> directly.
    /// </summary>
    public static class LayoutRebuilder
    {
        static readonly List<RectTransform> s_Queued = new();

        /// <summary>Fresh-world reset — a new GameLoop drops marks queued by the old world.</summary>
        internal static void ResetQueue() { s_Queued.Clear(); s_ScreenW = s_ScreenH = -1; }

        static int s_ScreenW = -1, s_ScreenH = -1;

        /// <summary>
        /// A new screen size changes every screen-space root canvas's rect (and its scaler's
        /// factor), so every rect under it that stretches changes size - the case the original
        /// engine catches per rect as a dimensions change. Rects here are computed on read, so
        /// only the layout-driven ones go stale: queue every active layout controller.
        /// </summary>
        static void MarkAllIfScreenChanged()
        {
            if (Screen.width == s_ScreenW && Screen.height == s_ScreenH) return;
            bool first = s_ScreenW < 0;
            s_ScreenW = Screen.width; s_ScreenH = Screen.height;
            if (first) return; // the first tick's layouts are already queued by their OnEnable
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is ILayoutController && mb.isActiveAndEnabled && mb.transform is RectTransform rt)
                    MarkLayoutForRebuild(rt);
        }

        /// <summary>Solves the whole subtree now (both axes, both passes).</summary>
        public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot)
        {
            if (layoutRoot == null) return;
            PerformLayoutCalculation(layoutRoot, static e => e.CalculateLayoutInputHorizontal());
            PerformLayoutControl(layoutRoot, static c => c.SetLayoutHorizontal());
            PerformLayoutCalculation(layoutRoot, static e => e.CalculateLayoutInputVertical());
            PerformLayoutControl(layoutRoot, static c => c.SetLayoutVertical());
        }

        /// <summary>
        /// Queues the layout ROOT above <paramref name="rect"/> for an end-of-tick rebuild:
        /// walks up while the parent hosts an active layout group, so one rebuild covers
        /// the whole nested arrangement.
        /// </summary>
        public static void MarkLayoutForRebuild(RectTransform rect)
        {
            if (rect == null) return;

            var layoutRoot = rect;
            var parent = layoutRoot.parent as RectTransform;
            while (parent != null && HasActiveLayoutGroup(parent))
            {
                layoutRoot = parent;
                parent = parent.parent as RectTransform;
            }

            if (!s_Queued.Contains(layoutRoot))
                s_Queued.Add(layoutRoot);
        }

        /// <summary>Rebuilds every queued root. Called by the GameLoop after LateUpdate.</summary>
        public static void FlushQueuedRebuilds()
        {
            MarkAllIfScreenChanged();
            if (s_Queued.Count == 0) return;

            // Snapshot: rebuilds can mark again (e.g. a group resizing a child group);
            // those marks land in the NEXT tick's flush, preventing rebuild storms.
            var roots = s_Queued.ToArray();
            s_Queued.Clear();
            foreach (var root in roots)
                if (root != null && !root.destroyedFlag && root.gameObject is { IsDestroyed: false })
                    ForceRebuildLayoutImmediate(root);
        }

        static bool HasActiveLayoutGroup(RectTransform rect)
        {
            foreach (var group in rect.gameObject.GetComponents<ILayoutGroup>())
                if (group is not Behaviour { isActiveAndEnabled: false })
                    return true;
            return false;
        }

        static void PerformLayoutCalculation(RectTransform rect, Action<ILayoutElement> action)
        {
            var elements = ActiveComponents<ILayoutElement>(rect);
            if (elements.Count == 0 && !HasLayoutController(rect)) return;

            // Children FIRST: a parent group's inputs aggregate child inputs.
            for (int i = 0; i < rect.childCount; i++)
                if (rect.GetChild(i) is RectTransform child)
                    PerformLayoutCalculation(child, action);

            foreach (var element in elements)
                action(element);
        }

        static void PerformLayoutControl(RectTransform rect, Action<ILayoutController> action)
        {
            var controllers = ActiveComponents<ILayoutController>(rect);
            if (controllers.Count == 0) return;

            // Self-controllers size this node first; then group controllers lay out the
            // children within it; then each child subdivides its own cell.
            foreach (var controller in controllers)
                if (controller is ILayoutSelfController)
                    action(controller);
            foreach (var controller in controllers)
                if (controller is not ILayoutSelfController)
                    action(controller);

            for (int i = 0; i < rect.childCount; i++)
                if (rect.GetChild(i) is RectTransform child)
                    PerformLayoutControl(child, action);
        }

        static bool HasLayoutController(RectTransform rect)
            => ActiveComponents<ILayoutController>(rect).Count > 0;

        static List<T> ActiveComponents<T>(RectTransform rect) where T : class
        {
            var results = new List<T>();
            foreach (var component in rect.gameObject.GetComponents<T>())
                if (component is not Behaviour { isActiveAndEnabled: false })
                    results.Add(component);
            return results;
        }
    }
}
