namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// The shared solve for horizontal and vertical layout groups (original contract).
    /// Along the MAIN axis, children are placed sequentially: each child's size
    /// interpolates min→preferred by how much of the group's preferred span fits
    /// (<c>minMaxLerp</c>), then surplus space is shared out by flexible weight — or, with
    /// no flexible children, the whole run is aligned inside the surplus. Along the CROSS
    /// axis every child is solved independently against the group's inner span. With
    /// <c>childControl*</c> off, children keep their own sizeDelta and the group only
    /// positions them (aligned inside the cell their computed size defines);
    /// <c>childForceExpand*</c> gives every child at least flexible weight 1.
    /// </summary>
    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        [SerializeField] protected float m_Spacing;
        [SerializeField] protected bool m_ChildForceExpandWidth = true;
        [SerializeField] protected bool m_ChildForceExpandHeight = true;
        [SerializeField] protected bool m_ChildControlWidth = true;
        [SerializeField] protected bool m_ChildControlHeight = true;
        [SerializeField] protected bool m_ChildScaleWidth;
        [SerializeField] protected bool m_ChildScaleHeight;
        [SerializeField] protected bool m_ReverseArrangement;

        public float spacing { get => m_Spacing; set => SetProperty(ref m_Spacing, value); }
        public bool childForceExpandWidth { get => m_ChildForceExpandWidth; set => SetProperty(ref m_ChildForceExpandWidth, value); }
        public bool childForceExpandHeight { get => m_ChildForceExpandHeight; set => SetProperty(ref m_ChildForceExpandHeight, value); }
        public bool childControlWidth { get => m_ChildControlWidth; set => SetProperty(ref m_ChildControlWidth, value); }
        public bool childControlHeight { get => m_ChildControlHeight; set => SetProperty(ref m_ChildControlHeight, value); }
        /// <summary>Size and place children by their size times their local scale on X.</summary>
        public bool childScaleWidth { get => m_ChildScaleWidth; set => SetProperty(ref m_ChildScaleWidth, value); }
        /// <summary>Size and place children by their size times their local scale on Y.</summary>
        public bool childScaleHeight { get => m_ChildScaleHeight; set => SetProperty(ref m_ChildScaleHeight, value); }
        /// <summary>Lay the children out last-to-first along the main axis.</summary>
        public bool reverseArrangement { get => m_ReverseArrangement; set => SetProperty(ref m_ReverseArrangement, value); }

        /// <summary>The child's local scale on the axis when the group uses child scale there, else 1.</summary>
        float ScaleOf(RectTransform child, int axis)
            => (axis == 0 ? m_ChildScaleWidth : m_ChildScaleHeight) ? (axis == 0 ? child.localScale.x : child.localScale.y) : 1f;

        /// <summary>Accumulates this group's min/preferred/flexible inputs along one axis.</summary>
        protected void CalcAlongAxis(int axis, bool isVertical)
        {
            float combinedPadding = axis == 0 ? padding.horizontal : padding.vertical;
            bool controlSize = axis == 0 ? m_ChildControlWidth : m_ChildControlHeight;
            bool childForceExpandSize = axis == 0 ? m_ChildForceExpandWidth : m_ChildForceExpandHeight;

            float totalMin = combinedPadding;
            float totalPreferred = combinedPadding;
            float totalFlexible = 0f;

            bool alongOtherAxis = isVertical ^ (axis == 1);
            foreach (var child in rectChildren)
            {
                GetChildSizes(child, axis, controlSize, childForceExpandSize,
                    out float min, out float preferred, out float flexible);
                float scale = ScaleOf(child, axis);
                min *= scale; preferred *= scale; flexible *= scale;

                if (alongOtherAxis)
                {
                    totalMin = Mathf.Max(min + combinedPadding, totalMin);
                    totalPreferred = Mathf.Max(preferred + combinedPadding, totalPreferred);
                    totalFlexible = Mathf.Max(flexible, totalFlexible);
                }
                else
                {
                    totalMin += min + spacing;
                    totalPreferred += preferred + spacing;
                    totalFlexible += flexible;
                }
            }

            if (!alongOtherAxis && rectChildren.Count > 0)
            {
                totalMin -= spacing; // no trailing gap
                totalPreferred -= spacing;
            }
            totalPreferred = Mathf.Max(totalMin, totalPreferred);

            SetLayoutInputForAxis(totalMin, totalPreferred, totalFlexible, axis);
        }

        /// <summary>Writes every child's position (and size, when controlled) along one axis.</summary>
        protected void SetChildrenAlongAxis(int axis, bool isVertical)
        {
            float size = axis == 0 ? rectTransform.rect.width : rectTransform.rect.height;
            bool controlSize = axis == 0 ? m_ChildControlWidth : m_ChildControlHeight;
            bool childForceExpandSize = axis == 0 ? m_ChildForceExpandWidth : m_ChildForceExpandHeight;
            float alignmentOnAxis = GetAlignmentOnAxis(axis);

            bool alongOtherAxis = isVertical ^ (axis == 1);
            if (alongOtherAxis)
            {
                float innerSize = size - (axis == 0 ? padding.horizontal : padding.vertical);
                foreach (var child in rectChildren)
                {
                    GetChildSizes(child, axis, controlSize, childForceExpandSize,
                        out float min, out float preferred, out float flexible);
                    float scale = ScaleOf(child, axis);

                    // Sizes here are the space the child occupies (its size x scale).
                    float requiredSpace = Mathf.Clamp(innerSize, min * scale, flexible > 0f ? size : preferred * scale);
                    float startOffset = GetStartOffset(axis, requiredSpace);
                    if (controlSize)
                    {
                        SetChildAlongAxisWithScale(child, axis, startOffset, scale != 0f ? requiredSpace / scale : 0f, scale);
                    }
                    else
                    {
                        float childExtent = (axis == 0 ? child.sizeDelta.x : child.sizeDelta.y) * scale;
                        float offsetInCell = (requiredSpace - childExtent) * alignmentOnAxis;
                        SetChildAlongAxisWithScale(child, axis, startOffset + offsetInCell, scale);
                    }
                }
            }
            else
            {
                float pos = axis == 0 ? padding.left : padding.top;
                float itemFlexibleMultiplier = 0f;
                float surplusSpace = size - GetTotalPreferredSize(axis);
                if (surplusSpace > 0f)
                {
                    if (GetTotalFlexibleSize(axis) == 0f)
                        pos = GetStartOffset(axis,
                            GetTotalPreferredSize(axis) - (axis == 0 ? padding.horizontal : padding.vertical));
                    else
                        itemFlexibleMultiplier = surplusSpace / GetTotalFlexibleSize(axis);
                }

                float minMaxLerp = 0f;
                if (GetTotalMinSize(axis) != GetTotalPreferredSize(axis))
                    minMaxLerp = Mathf.Clamp01(
                        (size - GetTotalMinSize(axis)) / (GetTotalPreferredSize(axis) - GetTotalMinSize(axis)));

                for (int k = 0; k < rectChildren.Count; k++)
                {
                    var child = rectChildren[m_ReverseArrangement ? rectChildren.Count - 1 - k : k];
                    GetChildSizes(child, axis, controlSize, childForceExpandSize,
                        out float min, out float preferred, out float flexible);
                    float scale = ScaleOf(child, axis);

                    // childSize is the space the child occupies along the run (its size x scale).
                    float childSize = Mathf.Lerp(min * scale, preferred * scale, minMaxLerp);
                    childSize += flexible * scale * itemFlexibleMultiplier;
                    if (controlSize)
                    {
                        SetChildAlongAxisWithScale(child, axis, pos, scale != 0f ? childSize / scale : 0f, scale);
                    }
                    else
                    {
                        float childExtent = (axis == 0 ? child.sizeDelta.x : child.sizeDelta.y) * scale;
                        float offsetInCell = (childSize - childExtent) * alignmentOnAxis;
                        SetChildAlongAxisWithScale(child, axis, pos + offsetInCell, scale);
                    }
                    pos += childSize + spacing;
                }
            }
        }

        void GetChildSizes(RectTransform child, int axis, bool controlSize, bool childForceExpand,
            out float min, out float preferred, out float flexible)
        {
            if (!controlSize)
            {
                // The child keeps its own size; the group only reserves and positions.
                min = axis == 0 ? child.sizeDelta.x : child.sizeDelta.y;
                preferred = min;
                flexible = 0f;
            }
            else
            {
                min = LayoutUtility.GetMinSize(child, axis);
                preferred = LayoutUtility.GetPreferredSize(child, axis);
                flexible = LayoutUtility.GetFlexibleSize(child, axis);
            }

            if (childForceExpand)
                flexible = Mathf.Max(flexible, 1f);
        }
    }

    /// <summary>Lays children left→right (original contract).</summary>
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalcAlongAxis(0, isVertical: false);
        }

        public override void CalculateLayoutInputVertical() => CalcAlongAxis(1, isVertical: false);
        public override void SetLayoutHorizontal() => SetChildrenAlongAxis(0, isVertical: false);
        public override void SetLayoutVertical() => SetChildrenAlongAxis(1, isVertical: false);
    }

    /// <summary>Lays children top→bottom (original contract).</summary>
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalcAlongAxis(0, isVertical: true);
        }

        public override void CalculateLayoutInputVertical() => CalcAlongAxis(1, isVertical: true);
        public override void SetLayoutHorizontal() => SetChildrenAlongAxis(0, isVertical: true);
        public override void SetLayoutVertical() => SetChildrenAlongAxis(1, isVertical: true);
    }
}
