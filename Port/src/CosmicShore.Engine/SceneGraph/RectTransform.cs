namespace CosmicShore.Engine
{
    /// <summary>
    /// The UI geometry core (original contract: RectTransform) — a Transform whose position
    /// and size derive from anchor state relative to the parent's rect:
    ///
    ///   • <see cref="anchorMin"/>/<see cref="anchorMax"/> — normalized [0,1]² corners of the
    ///     anchor region inside the parent rect. Together (min == max) the element has a fixed
    ///     size; apart, the element stretches with the parent.
    ///   • <see cref="pivot"/> — normalized point in the element's own rect that
    ///     <see cref="anchoredPosition"/> positions (and rotation/scale pivot around).
    ///   • <see cref="anchoredPosition"/> — offset of the pivot from the anchor reference
    ///     point (the pivot-lerped point of the anchor region).
    ///   • <see cref="sizeDelta"/> — size relative to the anchor region: with anchors
    ///     together it IS the size; with anchors apart it is the total margin
    ///     (offsetMax − offsetMin).
    ///
    /// The solve is PULL-BASED: <see cref="rect"/> and <see cref="localPosition"/> compute
    /// from the live anchor state and parent chain on every read, so geometry is always
    /// internally consistent with no dirty-tracking or update ordering — the headless
    /// determinism the original engine only guarantees after its layout pass. A root
    /// screen-space <see cref="Canvas"/> on the same GameObject DRIVES this rect: size =
    /// screen / scaleFactor, pose = screen centre, scale = scaleFactor (writes to driven
    /// properties are stored but overridden while the canvas drives, matching the original
    /// "driven by Canvas" behaviour).
    /// </summary>
    public class RectTransform : Transform
    {
        public enum Axis { Horizontal = 0, Vertical = 1 }
        public enum Edge { Left = 0, Right = 1, Top = 2, Bottom = 3 }

        public Vector2 anchorMin = new(0.5f, 0.5f);
        public Vector2 anchorMax = new(0.5f, 0.5f);
        public Vector2 pivot = new(0.5f, 0.5f);
        public Vector2 anchoredPosition = Vector2.zero;
        public Vector2 sizeDelta = Vector2.zero;

        Vector3 _storedLocalPosition; // z always; xy kept as the last written value while driven
        Vector3 _storedLocalScale = Vector3.one;

        // ── The rect solve ───────────────────────────────────────────────────

        /// <summary>The parent's rect in parent-local space, or zero when the parent is not a RectTransform.</summary>
        Rect ParentRect => parent is RectTransform parentRect ? parentRect.rect : Rect.zero;

        /// <summary>
        /// The root screen-space Canvas on this GameObject, when it drives this rect
        /// (size = screen / scaleFactor, pose = screen centre, scale = scaleFactor).
        /// </summary>
        Canvas DrivingCanvas
        {
            get
            {
                var canvas = gameObject?.GetComponent<Canvas>();
                if (canvas == null) return null;
                return canvas.renderMode != RenderMode.WorldSpace && canvas.isRootCanvas ? canvas : null;
            }
        }

        /// <summary>The pivot's rest point (anchor reference) in parent-local space.</summary>
        Vector2 AnchorReferencePoint
        {
            get
            {
                Rect parentRect = ParentRect;
                Vector2 refMin = parentRect.min + Vector2.Scale(anchorMin, parentRect.size);
                Vector2 refMax = parentRect.min + Vector2.Scale(anchorMax, parentRect.size);
                return refMin + Vector2.Scale(refMax - refMin, pivot);
            }
        }

        /// <summary>
        /// The element's rect in its own local space, relative to the pivot
        /// (min = −pivot·size). Computed from the live anchor state and parent chain.
        /// </summary>
        public Rect rect
        {
            get
            {
                Vector2 size;
                var driver = DrivingCanvas;
                if (driver != null)
                {
                    float scale = driver.scaleFactor;
                    if (scale <= 0f) scale = 1f;
                    size = new Vector2(Screen.width / scale, Screen.height / scale);
                }
                else
                {
                    Rect parentRect = ParentRect;
                    size = new Vector2(
                        (anchorMax.x - anchorMin.x) * parentRect.width + sizeDelta.x,
                        (anchorMax.y - anchorMin.y) * parentRect.height + sizeDelta.y);
                }
                return new Rect(-pivot.x * size.x, -pivot.y * size.y, size.x, size.y);
            }
        }

        public override Vector3 localPosition
        {
            get
            {
                var driver = DrivingCanvas;
                if (driver != null)
                    return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, _storedLocalPosition.z);
                Vector2 xy = AnchorReferencePoint + anchoredPosition;
                return new Vector3(xy.x, xy.y, _storedLocalPosition.z);
            }
            set
            {
                _storedLocalPosition = value;
                // Back-solve against the REAL parent rect so the pose lands exactly —
                // this is what makes reparenting (worldPositionStays) and Transform→
                // RectTransform conversion preserve world position.
                anchoredPosition = new Vector2(value.x, value.y) - AnchorReferencePoint;
            }
        }

        public override Vector3 localScale
        {
            get
            {
                var driver = DrivingCanvas;
                if (driver != null)
                {
                    float scale = driver.scaleFactor;
                    return new Vector3(scale, scale, scale);
                }
                return _storedLocalScale;
            }
            set => _storedLocalScale = value;
        }

        // ── Derived views over the anchor state ─────────────────────────────

        /// <summary>Offset of the rect's min corner from the anchor region's min corner.</summary>
        public Vector2 offsetMin
        {
            get => anchoredPosition - Vector2.Scale(sizeDelta, pivot);
            set
            {
                Vector2 max = offsetMax;
                sizeDelta = max - value;
                anchoredPosition = value + Vector2.Scale(sizeDelta, pivot);
            }
        }

        /// <summary>Offset of the rect's max corner from the anchor region's max corner.</summary>
        public Vector2 offsetMax
        {
            get => anchoredPosition + Vector2.Scale(sizeDelta, Vector2.one - pivot);
            set
            {
                Vector2 min = offsetMin;
                sizeDelta = value - min;
                anchoredPosition = min + Vector2.Scale(sizeDelta, pivot);
            }
        }

        public Vector3 anchoredPosition3D
        {
            get => new(anchoredPosition.x, anchoredPosition.y, _storedLocalPosition.z);
            set
            {
                anchoredPosition = new Vector2(value.x, value.y);
                _storedLocalPosition.z = value.z;
            }
        }

        // ── Corner queries ───────────────────────────────────────────────────

        /// <summary>
        /// The rect's four corners in local space. Original contract: index order is
        /// bottom-left, top-left, top-right, bottom-right.
        /// </summary>
        public void GetLocalCorners(Vector3[] fourCornersArray)
        {
            Rect r = rect;
            fourCornersArray[0] = new Vector3(r.xMin, r.yMin, 0f);
            fourCornersArray[1] = new Vector3(r.xMin, r.yMax, 0f);
            fourCornersArray[2] = new Vector3(r.xMax, r.yMax, 0f);
            fourCornersArray[3] = new Vector3(r.xMax, r.yMin, 0f);
        }

        /// <summary>Local corners through the world transform (same index order).</summary>
        public void GetWorldCorners(Vector3[] fourCornersArray)
        {
            GetLocalCorners(fourCornersArray);
            for (int i = 0; i < 4; i++)
                fourCornersArray[i] = TransformPoint(fourCornersArray[i]);
        }

        // ── Sizing helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Makes the rect <paramref name="size"/> units along <paramref name="axis"/> without
        /// moving the pivot (adjusts sizeDelta for the current anchor span).
        /// </summary>
        public void SetSizeWithCurrentAnchors(Axis axis, float size)
        {
            Rect parentRect = ParentRect;
            if (axis == Axis.Horizontal)
                sizeDelta.x = size - (anchorMax.x - anchorMin.x) * parentRect.width;
            else
                sizeDelta.y = size - (anchorMax.y - anchorMin.y) * parentRect.height;
        }

        /// <summary>
        /// Anchors this axis to a parent edge and places the rect <paramref name="inset"/>
        /// units in from it at the given <paramref name="size"/> (original contract).
        /// </summary>
        public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size)
        {
            bool horizontal = edge is Edge.Left or Edge.Right;
            bool fromMin = edge is Edge.Left or Edge.Bottom;
            float anchor = fromMin ? 0f : 1f;
            float pivotAlong = horizontal ? pivot.x : pivot.y;
            float position = fromMin
                ? inset + size * pivotAlong
                : -inset - size * (1f - pivotAlong);

            if (horizontal)
            {
                anchorMin.x = anchor;
                anchorMax.x = anchor;
                sizeDelta.x = size;
                anchoredPosition.x = position;
            }
            else
            {
                anchorMin.y = anchor;
                anchorMax.y = anchor;
                sizeDelta.y = size;
                anchoredPosition.y = position;
            }
        }
    }
}
