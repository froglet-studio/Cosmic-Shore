using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Canvas hit-tester (original contract; REAL as of Arc D). Walks this canvas's
    /// subtree in hierarchy order — which IS draw order, later siblings on top — and
    /// reports every active, enabled, <see cref="Graphic.raycastTarget"/> graphic whose
    /// rect contains the screen point (Arc-A world corners are screen pixels for
    /// screen-space canvases). A nested Canvas owns its own subtree: the walk stops
    /// there, matching the original's per-canvas graphic registry.
    /// </summary>
    public class GraphicRaycaster : BaseRaycaster
    {
        public enum BlockingObjects { None = 0, TwoD = 1, ThreeD = 2, All = 3 }

        [SerializeField] bool m_IgnoreReversedGraphics = true;
        [SerializeField] BlockingObjects m_BlockingObjects = BlockingObjects.None;

        public bool ignoreReversedGraphics { get => m_IgnoreReversedGraphics; set => m_IgnoreReversedGraphics = value; }
        public BlockingObjects blockingObjects { get => m_BlockingObjects; set => m_BlockingObjects = value; }

        Canvas m_Canvas;
        Canvas canvas => m_Canvas ??= gameObject.GetComponent<Canvas>();

        public override int sortOrderPriority => canvas != null ? canvas.sortingOrder : 0;

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (canvas == null) return;

            int drawOrder = 0;
            WalkGraphics(canvas.transform, eventData.position, resultAppendList, ref drawOrder, isRoot: true);
        }

        void WalkGraphics(Transform node, Vector2 screenPoint, List<RaycastResult> results, ref int drawOrder, bool isRoot)
        {
            if (!node.gameObject.activeInHierarchy) return;

            // A nested canvas owns its own graphics (and its own raycaster).
            if (!isRoot && node.gameObject.GetComponent<Canvas>() != null) return;

            // Original rule: a CanvasGroup with blocksRaycasts=false makes its whole
            // subtree invisible to raycasts (the hidden-modal case — alpha 0 overlays
            // must not swallow clicks). ignoreParentGroups re-opt-in is not yet
            // consumed by any ported UI; add it when a consumer arrives.
            var group = node.gameObject.GetComponent<CanvasGroup>();
            if (group != null && group.isActiveAndEnabled && !group.blocksRaycasts) return;

            foreach (var graphic in node.gameObject.GetComponents<Graphic>())
            {
                if (!graphic.isActiveAndEnabled) continue;
                int depth = drawOrder++;
                if (!graphic.raycastTarget) continue;
                if (node is not RectTransform rect) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint)) continue;

                results.Add(new RaycastResult
                {
                    gameObject = node.gameObject,
                    module = this,
                    distance = 0f,
                    index = results.Count,
                    depth = depth,
                    sortingOrder = canvas.sortingOrder,
                    screenPosition = screenPoint,
                });
            }

            for (int i = 0; i < node.childCount; i++)
                WalkGraphics(node.GetChild(i), screenPoint, results, ref drawOrder, isRoot: false);
        }
    }
}
