namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Explicit layout inputs for one element (original contract): −1 means "no opinion",
    /// any other value overrides what siblings report at lower priority.
    /// <see cref="ignoreLayout"/> opts the element out of its parent group entirely.
    /// </summary>
    public class LayoutElement : MonoBehaviour, ILayoutElement, ILayoutIgnorer
    {
        [SerializeField] bool m_IgnoreLayout;
        [SerializeField] float m_MinWidth = -1f;
        [SerializeField] float m_MinHeight = -1f;
        [SerializeField] float m_PreferredWidth = -1f;
        [SerializeField] float m_PreferredHeight = -1f;
        [SerializeField] float m_FlexibleWidth = -1f;
        [SerializeField] float m_FlexibleHeight = -1f;
        [SerializeField] int m_LayoutPriority = 1;

        public virtual bool ignoreLayout { get => m_IgnoreLayout; set => SetProperty(ref m_IgnoreLayout, value); }
        public virtual float minWidth { get => m_MinWidth; set => SetProperty(ref m_MinWidth, value); }
        public virtual float minHeight { get => m_MinHeight; set => SetProperty(ref m_MinHeight, value); }
        public virtual float preferredWidth { get => m_PreferredWidth; set => SetProperty(ref m_PreferredWidth, value); }
        public virtual float preferredHeight { get => m_PreferredHeight; set => SetProperty(ref m_PreferredHeight, value); }
        public virtual float flexibleWidth { get => m_FlexibleWidth; set => SetProperty(ref m_FlexibleWidth, value); }
        public virtual float flexibleHeight { get => m_FlexibleHeight; set => SetProperty(ref m_FlexibleHeight, value); }
        public virtual int layoutPriority { get => m_LayoutPriority; set => SetProperty(ref m_LayoutPriority, value); }

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }

        void SetProperty<T>(ref T field, T value)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            SetDirty();
        }

        void OnEnable() => SetDirty();
        void OnDisable() => SetDirty();

        /// <summary>
        /// The parent group re-solves when this element's opinion changes. Marks even while
        /// disabling — the group above must re-solve WITHOUT this element's contribution.
        /// </summary>
        protected void SetDirty()
        {
            if (transform is RectTransform rect)
                LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
    }
}
