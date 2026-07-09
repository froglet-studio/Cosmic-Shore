namespace CosmicShore.Engine
{
    /// <summary>
    /// Screen-point ↔ rect helpers (original contract). Headless-first: screen-space
    /// overlay canvases put world corners in pixels (the Arc-A canvas-driven solve),
    /// so a screen point tests directly against the world-corner quad; the camera
    /// parameter exists for signature parity and is unused until a world-space UI
    /// pass needs it.
    /// </summary>
    public static class RectTransformUtility
    {
        static readonly Vector3[] s_Corners = new Vector3[4];

        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint)
            => RectangleContainsScreenPoint(rect, screenPoint, null);

        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam)
        {
            if (rect == null) return false;
            rect.GetWorldCorners(s_Corners); // BL, TL, TR, BR

            // Point-in-quad via same-side cross products — handles rotated/scaled UI,
            // and degenerates safely for the axis-aligned common case.
            return SameSide(s_Corners[0], s_Corners[1], screenPoint)
                && SameSide(s_Corners[1], s_Corners[2], screenPoint)
                && SameSide(s_Corners[2], s_Corners[3], screenPoint)
                && SameSide(s_Corners[3], s_Corners[0], screenPoint);
        }

        static bool SameSide(Vector3 a, Vector3 b, Vector2 point)
        {
            float cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
            // BL→TL→TR→BR winds CLOCKWISE in y-up screen space, so interior points
            // sit on the negative-cross side of every edge (boundary counts as inside).
            return cross <= 0f;
        }

        /// <summary>Converts a screen point into <paramref name="rect"/>'s local space.</summary>
        public static bool ScreenPointToLocalPointInRectangle(
            RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            if (rect == null) return false;

            // World position of the rect's pivot, then inverse-scale the offset into
            // local units (sufficient for the unrotated screen-space UI the menu uses).
            Vector3 pivotWorld = rect.TransformPoint(Vector3.zero);
            Vector3 scale = rect.lossyScale;
            localPoint = new Vector2(
                scale.x != 0f ? (screenPoint.x - pivotWorld.x) / scale.x : 0f,
                scale.y != 0f ? (screenPoint.y - pivotWorld.y) / scale.y : 0f);
            return true;
        }
    }
}
