using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>An element the canvas update rebuilds in phases (original contract: ICanvasElement).</summary>
    public interface ICanvasElement
    {
        void Rebuild(CanvasUpdate executing);
        Transform transform { get; }
        void LayoutComplete();
        void GraphicUpdateComplete();
        bool IsDestroyed();
    }

    /// <summary>
    /// The canvas update's two queues (original contract: CanvasUpdateRegistry). Elements in the
    /// layout queue are rebuilt in the Prelayout, Layout and PostLayout phases around the layout
    /// solve, shallowest first; elements in the graphic queue in PreRender and LatePreRender after
    /// it. <see cref="LayoutRebuilder.FlushQueuedRebuilds"/> runs both, once a tick after
    /// LateUpdate. As in Unity, an element cannot be queued for layout while the layout phases
    /// run: the Try* forms answer false, and the plain forms do nothing.
    /// </summary>
    public class CanvasUpdateRegistry
    {
        static CanvasUpdateRegistry s_Instance;
        public static CanvasUpdateRegistry instance => s_Instance ??= new CanvasUpdateRegistry();

        readonly List<ICanvasElement> m_LayoutRebuildQueue = new();
        readonly List<ICanvasElement> m_GraphicRebuildQueue = new();
        bool m_PerformingLayoutUpdate;
        bool m_PerformingGraphicUpdate;

        internal static void Reset() => s_Instance = null;

        public static void RegisterCanvasElementForLayoutRebuild(ICanvasElement element) => instance.InternalRegisterLayout(element);
        public static bool TryRegisterCanvasElementForLayoutRebuild(ICanvasElement element) => instance.InternalRegisterLayout(element);
        public static void RegisterCanvasElementForGraphicRebuild(ICanvasElement element) => instance.InternalRegisterGraphic(element);
        public static bool TryRegisterCanvasElementForGraphicRebuild(ICanvasElement element) => instance.InternalRegisterGraphic(element);

        public static void UnRegisterCanvasElementForRebuild(ICanvasElement element)
        {
            var r = instance;
            r.m_LayoutRebuildQueue.Remove(element);
            r.m_GraphicRebuildQueue.Remove(element);
        }

        /// <summary>Drops a disabled element from both queues (original: it is skipped until re-registered).</summary>
        public static void DisableCanvasElementForRebuild(ICanvasElement element) => UnRegisterCanvasElementForRebuild(element);

        public static bool IsRebuildingLayout() => instance.m_PerformingLayoutUpdate;
        public static bool IsRebuildingGraphics() => instance.m_PerformingGraphicUpdate;

        bool InternalRegisterLayout(ICanvasElement element)
        {
            if (element == null || m_PerformingLayoutUpdate) return false;
            if (!m_LayoutRebuildQueue.Contains(element)) m_LayoutRebuildQueue.Add(element);
            return true;
        }

        bool InternalRegisterGraphic(ICanvasElement element)
        {
            if (element == null || m_PerformingGraphicUpdate) return false;
            if (!m_GraphicRebuildQueue.Contains(element)) m_GraphicRebuildQueue.Add(element);
            return true;
        }

        static bool Valid(ICanvasElement e) => e != null && !e.IsDestroyed();

        static int Depth(Transform t)
        {
            int n = 0;
            for (var p = t?.parent; p is not null; p = p.parent) n++;
            return n;
        }

        /// <summary>The layout phases, with <paramref name="solveLayout"/> (the queued layout roots) between Prelayout and PostLayout.</summary>
        internal void PerformLayoutUpdate(System.Action solveLayout)
        {
            var elements = m_LayoutRebuildQueue.ToArray();
            m_LayoutRebuildQueue.Clear();
            System.Array.Sort(elements, (a, b) => Depth(Valid(a) ? a.transform : null).CompareTo(Depth(Valid(b) ? b.transform : null)));
            m_PerformingLayoutUpdate = true;
            try
            {
                foreach (var e in elements) if (Valid(e)) e.Rebuild(CanvasUpdate.Prelayout);
                foreach (var e in elements) if (Valid(e)) e.Rebuild(CanvasUpdate.Layout);
                solveLayout();
                foreach (var e in elements) if (Valid(e)) e.Rebuild(CanvasUpdate.PostLayout);
                foreach (var e in elements) if (Valid(e)) e.LayoutComplete();
            }
            finally { m_PerformingLayoutUpdate = false; }
        }

        internal void PerformGraphicUpdate()
        {
            if (m_GraphicRebuildQueue.Count == 0) return;
            var elements = m_GraphicRebuildQueue.ToArray();
            m_GraphicRebuildQueue.Clear();
            m_PerformingGraphicUpdate = true;
            try
            {
                foreach (var e in elements) if (Valid(e)) e.Rebuild(CanvasUpdate.PreRender);
                foreach (var e in elements) if (Valid(e)) e.Rebuild(CanvasUpdate.LatePreRender);
                foreach (var e in elements) if (Valid(e)) e.GraphicUpdateComplete();
            }
            finally { m_PerformingGraphicUpdate = false; }
        }
    }
}
