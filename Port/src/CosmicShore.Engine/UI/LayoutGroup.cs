using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Base for components that lay out their children (original contract). Derived groups
    /// compute per-axis layout inputs from the active, non-ignored RectTransform children
    /// (<see cref="rectChildren"/>) and then WRITE each child's geometry via
    /// <see cref="SetChildAlongAxis(RectTransform,int,float,float)"/> — which pins the
    /// child's anchors to the parent's upper-left, the coordinate frame all group maths
    /// use. Groups are themselves <see cref="ILayoutElement"/>s, so nesting composes:
    /// a parent group sizes a child group by the child's own computed inputs.
    /// </summary>
    public abstract class LayoutGroup : MonoBehaviour, ILayoutElement, ILayoutGroup
    {
        [SerializeField] protected RectOffset m_Padding = new();
        [SerializeField] protected TextAnchor m_ChildAlignment = TextAnchor.UpperLeft;

        public RectOffset padding { get => m_Padding; set { m_Padding = value; SetDirty(); } }
        public TextAnchor childAlignment { get => m_ChildAlignment; set => SetProperty(ref m_ChildAlignment, value); }

        RectTransform m_Rect;
        protected RectTransform rectTransform => m_Rect ??= (RectTransform)transform;

        protected readonly List<RectTransform> m_RectChildren = new();
        protected List<RectTransform> rectChildren => m_RectChildren;

        readonly float[] m_TotalMinSize = new float[2];
        readonly float[] m_TotalPreferredSize = new float[2];
        readonly float[] m_TotalFlexibleSize = new float[2];

        // ── ILayoutElement (the group's own inputs, consumed by a PARENT group) ──

        public virtual float minWidth => GetTotalMinSize(0);
        public virtual float preferredWidth => GetTotalPreferredSize(0);
        public virtual float flexibleWidth => GetTotalFlexibleSize(0);
        public virtual float minHeight => GetTotalMinSize(1);
        public virtual float preferredHeight => GetTotalPreferredSize(1);
        public virtual float flexibleHeight => GetTotalFlexibleSize(1);
        public virtual int layoutPriority => 0;

        /// <summary>Collects the layout-participating children: active RectTransforms not opted out via ILayoutIgnorer.</summary>
        public virtual void CalculateLayoutInputHorizontal()
        {
            m_RectChildren.Clear();
            for (int i = 0; i < rectTransform.childCount; i++)
            {
                if (rectTransform.GetChild(i) is not RectTransform rect) continue;
                if (!rect.gameObject.activeInHierarchy) continue;

                bool ignore = false;
                foreach (var ignorer in rect.gameObject.GetComponents<ILayoutIgnorer>())
                {
                    if (ignorer is Behaviour { isActiveAndEnabled: false }) continue;
                    if (ignorer.ignoreLayout) { ignore = true; break; }
                }
                if (!ignore) m_RectChildren.Add(rect);
            }
        }

        public abstract void CalculateLayoutInputVertical();
        public abstract void SetLayoutHorizontal();
        public abstract void SetLayoutVertical();

        // ── Shared group maths ───────────────────────────────────────────────

        protected float GetTotalMinSize(int axis) => m_TotalMinSize[axis];
        protected float GetTotalPreferredSize(int axis) => m_TotalPreferredSize[axis];
        protected float GetTotalFlexibleSize(int axis) => m_TotalFlexibleSize[axis];

        protected void SetLayoutInputForAxis(float totalMin, float totalPreferred, float totalFlexible, int axis)
        {
            m_TotalMinSize[axis] = totalMin;
            m_TotalPreferredSize[axis] = totalPreferred;
            m_TotalFlexibleSize[axis] = totalFlexible;
        }

        /// <summary>0 (min edge) … 1 (max edge) alignment along the axis, from <see cref="childAlignment"/>.</summary>
        protected float GetAlignmentOnAxis(int axis)
            => axis == 0
                ? ((int)childAlignment % 3) * 0.5f
                : ((int)childAlignment / 3) * 0.5f;

        /// <summary>Where content of the given size starts along the axis (padding + aligned surplus).</summary>
        protected float GetStartOffset(int axis, float requiredSpaceWithoutPadding)
        {
            float requiredSpace = requiredSpaceWithoutPadding
                + (axis == 0 ? padding.horizontal : padding.vertical);
            float availableSpace = axis == 0 ? rectTransform.rect.width : rectTransform.rect.height;
            float surplusSpace = availableSpace - requiredSpace;
            return (axis == 0 ? padding.left : padding.top) + surplusSpace * GetAlignmentOnAxis(axis);
        }

        /// <summary>Positions the child at <paramref name="pos"/> from the parent's left/top edge (its size untouched).</summary>
        protected void SetChildAlongAxis(RectTransform rect, int axis, float pos)
        {
            if (rect == null) return;
            PinAnchorsUpperLeft(rect);
            Vector2 anchored = rect.anchoredPosition;
            float extent = axis == 0 ? rect.sizeDelta.x : rect.sizeDelta.y;
            if (axis == 0) anchored.x = pos + extent * rect.pivot.x;
            else anchored.y = -pos - extent * (1f - rect.pivot.y);
            rect.anchoredPosition = anchored;
        }

        /// <summary>Positions AND sizes the child along the axis (distances from the parent's left/top edge).</summary>
        protected void SetChildAlongAxis(RectTransform rect, int axis, float pos, float size)
        {
            if (rect == null) return;
            PinAnchorsUpperLeft(rect);
            Vector2 sizeDelta = rect.sizeDelta;
            if (axis == 0) sizeDelta.x = size; else sizeDelta.y = size;
            rect.sizeDelta = sizeDelta;

            Vector2 anchored = rect.anchoredPosition;
            if (axis == 0) anchored.x = pos + size * rect.pivot.x;
            else anchored.y = -pos - size * (1f - rect.pivot.y);
            rect.anchoredPosition = anchored;
        }

        /// <summary>
        /// Group-controlled children are expressed against the parent's UPPER-LEFT corner
        /// (original contract: the driven-properties tracker forces these anchors).
        /// </summary>
        static void PinAnchorsUpperLeft(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
        }

        // ── Dirtying ─────────────────────────────────────────────────────────

        protected void SetProperty<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            SetDirty();
        }

        void OnEnable() => SetDirty();
        void OnDisable() => SetDirty();

        /// <summary>Queues this group's layout root for a rebuild at the end of the frame.</summary>
        protected void SetDirty()
        {
            if (transform is RectTransform rect)
                LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
    }
}
