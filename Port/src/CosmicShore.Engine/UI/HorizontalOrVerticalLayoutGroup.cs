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

        public float spacing { get => m_Spacing; set => SetProperty(ref m_Spacing, value); }
        public bool childForceExpandWidth { get => m_ChildForceExpandWidth; set => SetProperty(ref m_ChildForceExpandWidth, value); }
        public bool childForceExpandHeight { get => m_ChildForceExpandHeight; set => SetProperty(ref m_ChildForceExpandHeight, value); }
        public bool childControlWidth { get => m_ChildControlWidth; set => SetProperty(ref m_ChildControlWidth, value); }
        public bool childControlHeight { get => m_ChildControlHeight; set => SetProperty(ref m_ChildControlHeight, value); }

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

                    float requiredSpace = Mathf.Clamp(innerSize, min, flexible > 0f ? size : preferred);
                    float startOffset = GetStartOffset(axis, requiredSpace);
                    if (controlSize)
                    {
                        SetChildAlongAxis(child, axis, startOffset, requiredSpace);
                    }
                    else
                    {
                        float childExtent = axis == 0 ? child.sizeDelta.x : child.sizeDelta.y;
                        float offsetInCell = (requiredSpace - childExtent) * alignmentOnAxis;
                        SetChildAlongAxis(child, axis, startOffset + offsetInCell);
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

                foreach (var child in rectChildren)
                {
                    GetChildSizes(child, axis, controlSize, childForceExpandSize,
                        out float min, out float preferred, out float flexible);

                    float childSize = Mathf.Lerp(min, preferred, minMaxLerp);
                    childSize += flexible * itemFlexibleMultiplier;
                    if (controlSize)
                    {
                        SetChildAlongAxis(child, axis, pos, childSize);
                    }
                    else
                    {
                        float childExtent = axis == 0 ? child.sizeDelta.x : child.sizeDelta.y;
                        float offsetInCell = (childSize - childExtent) * alignmentOnAxis;
                        SetChildAlongAxis(child, axis, pos + offsetInCell);
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
