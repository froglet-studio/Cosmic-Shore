namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Sizes its OWN RectTransform to its layout inputs (original contract) — the standard
    /// way a panel hugs its content: a layout group computes the content's min/preferred
    /// size, and this self-controller applies it via SetSizeWithCurrentAnchors. Runs before
    /// sibling group controllers in the control pass (ILayoutSelfController ordering).
    /// </summary>
    public class ContentSizeFitter : MonoBehaviour, ILayoutSelfController
    {
        public enum FitMode
        {
            Unconstrained = 0,
            MinSize = 1,
            PreferredSize = 2,
        }

        [SerializeField] protected FitMode m_HorizontalFit = FitMode.Unconstrained;
        [SerializeField] protected FitMode m_VerticalFit = FitMode.Unconstrained;

        public FitMode horizontalFit { get => m_HorizontalFit; set { if (m_HorizontalFit == value) return; m_HorizontalFit = value; SetDirty(); } }
        public FitMode verticalFit { get => m_VerticalFit; set { if (m_VerticalFit == value) return; m_VerticalFit = value; SetDirty(); } }

        RectTransform m_Rect;
        RectTransform rectTransform => m_Rect ??= (RectTransform)transform;

        public virtual void SetLayoutHorizontal() => HandleSelfFittingAlongAxis(0);
        public virtual void SetLayoutVertical() => HandleSelfFittingAlongAxis(1);

        void HandleSelfFittingAlongAxis(int axis)
        {
            FitMode fitting = axis == 0 ? horizontalFit : verticalFit;
            if (fitting == FitMode.Unconstrained) return;

            float size = fitting == FitMode.MinSize
                ? LayoutUtility.GetMinSize(rectTransform, axis)
                : LayoutUtility.GetPreferredSize(rectTransform, axis);
            rectTransform.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, size);
        }

        void OnEnable() => SetDirty();
        void OnDisable() => SetDirty();

        protected void SetDirty()
        {
            if (transform is RectTransform rect)
                LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
    }
}
