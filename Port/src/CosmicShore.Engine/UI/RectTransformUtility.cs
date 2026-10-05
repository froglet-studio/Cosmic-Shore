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

        /// <summary>Screen position of a world point: through the camera for camera/world canvases, identity for overlay (null camera).</summary>
        public static Vector2 WorldToScreenPoint(Camera cam, Vector3 worldPoint)
            => cam == null ? new Vector2(worldPoint.x, worldPoint.y) : (Vector2)cam.WorldToScreenPoint(worldPoint);

        public static Ray ScreenPointToRay(Camera cam, Vector2 screenPos)
            => cam != null ? cam.ScreenPointToRay(screenPos) : new Ray(new Vector3(screenPos.x, screenPos.y, -100f), Vector3.forward);

        public static bool ScreenPointToWorldPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            if (rect == null) return false;
            var ray = ScreenPointToRay(cam, screenPoint);
            var plane = new Plane(rect.rotation * Vector3.back, rect.position);
            if (!plane.Raycast(ray, out float dist)) return false;
            worldPoint = ray.GetPoint(dist);
            return true;
        }

        public static Vector2 PixelAdjustPoint(Vector2 point, Transform elementTransform, Canvas canvas)
            => canvas != null && canvas.pixelPerfect ? new Vector2(Mathf.Round(point.x), Mathf.Round(point.y)) : point;

        public static Rect PixelAdjustRect(RectTransform rectTransform, Canvas canvas) => rectTransform != null ? rectTransform.rect : default;

        /// <summary>
        /// Bounds of every active RectTransform under <paramref name="child"/>, expressed in
        /// <paramref name="root"/>'s local space (original contract).
        /// </summary>
        public static Bounds CalculateRelativeRectTransformBounds(Transform root, Transform child)
        {
            if (child == null) return new Bounds(Vector3.zero, Vector3.zero);
            var rects = child.GetComponentsInChildren<RectTransform>(false);
            if (rects.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);

            var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var toLocal = root != null ? root.worldToLocalMatrix : Matrix4x4.identity;
            foreach (var rt in rects)
            {
                rt.GetWorldCorners(s_Corners);
                for (int j = 0; j < 4; j++)
                {
                    var v = toLocal.MultiplyPoint3x4(s_Corners[j]);
                    vMin = Vector3.Min(v, vMin);
                    vMax = Vector3.Max(v, vMax);
                }
            }
            var b = new Bounds(vMin, Vector3.zero);
            b.Encapsulate(vMax);
            return b;
        }

        public static Bounds CalculateRelativeRectTransformBounds(Transform trans) => CalculateRelativeRectTransformBounds(trans, trans);

        public static void FlipLayoutOnAxis(RectTransform rect, int axis, bool keepPositioning, bool recursive)
        {
            if (rect == null) return;
            if (recursive)
                for (int i = 0; i < rect.childCount; i++)
                    if (rect.GetChild(i) is RectTransform c) FlipLayoutOnAxis(c, axis, false, true);
            var pivot = rect.pivot; pivot[axis] = 1f - pivot[axis]; rect.pivot = pivot;
            if (keepPositioning) return;
            var ap = rect.anchoredPosition; ap[axis] = -ap[axis]; rect.anchoredPosition = ap;
            var min = rect.anchorMin; var max = rect.anchorMax;
            float t = min[axis]; min[axis] = 1f - max[axis]; max[axis] = 1f - t;
            rect.anchorMin = min; rect.anchorMax = max;
        }
    }
}
