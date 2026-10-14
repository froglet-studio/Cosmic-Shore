using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// A scrollable viewport (original contract: ScrollRect), ported rule for rule.
    ///
    /// Bounds: the content's rect is measured in the viewport's space and padded to at least the
    /// viewport's size (toward the content's pivot), so content smaller than the view cannot
    /// scroll. Normalized positions are the view's travel across the content's hidden length.
    ///
    /// Input: a drag pans the content in the viewport's local units (so a scaled canvas pans 1:1
    /// under the pointer), rubber-banding past the edges when Elastic and stopping at them when
    /// Clamped. The wheel scrolls by scrollSensitivity; on a view that scrolls one axis only, the
    /// wheel's larger axis scrolls it (a horizontal track takes the vertical wheel).
    ///
    /// Motion (LateUpdate, unscaled time): while dragging, velocity follows the content's motion;
    /// released, Elastic content springs back inside its bounds (SmoothDamp over elasticity
    /// seconds, three times slower after a wheel scroll), and inertia carries it on, decaying by
    /// decelerationRate per second. Scrollbars take their value and size from the content, and
    /// onValueChanged fires whenever the bounds or the position changed.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ScrollRect : UIBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IEndDragHandler, IDragHandler,
        IScrollHandler, ICanvasElement, ILayoutElement, ILayoutGroup
    {
        public enum MovementType { Unrestricted = 0, Elastic = 1, Clamped = 2 }
        public enum ScrollbarVisibility { Permanent = 0, AutoHide = 1, AutoHideAndExpandViewport = 2 }

        [System.Serializable] public class ScrollRectEvent : UnityEvent<Vector2> { }

        [SerializeField] RectTransform m_Content;
        [SerializeField] bool m_Horizontal = true;
        [SerializeField] bool m_Vertical = true;
        [SerializeField] MovementType m_MovementType = MovementType.Elastic;
        [SerializeField] float m_Elasticity = 0.1f;
        [SerializeField] bool m_Inertia = true;
        [SerializeField] float m_DecelerationRate = 0.135f;
        [SerializeField] float m_ScrollSensitivity = 1f;
        [SerializeField] RectTransform m_Viewport;
        [SerializeField] Scrollbar m_HorizontalScrollbar;
        [SerializeField] Scrollbar m_VerticalScrollbar;
        [SerializeField] ScrollbarVisibility m_HorizontalScrollbarVisibility;
        [SerializeField] ScrollbarVisibility m_VerticalScrollbarVisibility;
        [SerializeField] float m_HorizontalScrollbarSpacing;
        [SerializeField] float m_VerticalScrollbarSpacing;
        [SerializeField] ScrollRectEvent m_OnValueChanged = new();

        public RectTransform content { get => m_Content; set => m_Content = value; }
        public bool horizontal { get => m_Horizontal; set => m_Horizontal = value; }
        public bool vertical { get => m_Vertical; set => m_Vertical = value; }
        public MovementType movementType { get => m_MovementType; set => m_MovementType = value; }
        public float elasticity { get => m_Elasticity; set => m_Elasticity = value; }
        public bool inertia { get => m_Inertia; set => m_Inertia = value; }
        public float decelerationRate { get => m_DecelerationRate; set => m_DecelerationRate = value; }
        public float scrollSensitivity { get => m_ScrollSensitivity; set => m_ScrollSensitivity = value; }
        public RectTransform viewport { get => m_Viewport; set { m_Viewport = value; SetDirtyCaching(); } }
        public ScrollbarVisibility horizontalScrollbarVisibility { get => m_HorizontalScrollbarVisibility; set { m_HorizontalScrollbarVisibility = value; SetDirtyCaching(); } }
        public ScrollbarVisibility verticalScrollbarVisibility { get => m_VerticalScrollbarVisibility; set { m_VerticalScrollbarVisibility = value; SetDirtyCaching(); } }
        public float horizontalScrollbarSpacing { get => m_HorizontalScrollbarSpacing; set { m_HorizontalScrollbarSpacing = value; SetDirty(); } }
        public float verticalScrollbarSpacing { get => m_VerticalScrollbarSpacing; set { m_VerticalScrollbarSpacing = value; SetDirty(); } }
        public ScrollRectEvent onValueChanged { get => m_OnValueChanged; set => m_OnValueChanged = value; }

        public Scrollbar horizontalScrollbar
        {
            get => m_HorizontalScrollbar;
            set
            {
                if (m_HorizontalScrollbar) m_HorizontalScrollbar.onValueChanged.RemoveListener(SetHorizontalNormalizedPosition);
                m_HorizontalScrollbar = value;
                if (m_Horizontal && m_HorizontalScrollbar) m_HorizontalScrollbar.onValueChanged.AddListener(SetHorizontalNormalizedPosition);
                SetDirtyCaching();
            }
        }

        public Scrollbar verticalScrollbar
        {
            get => m_VerticalScrollbar;
            set
            {
                if (m_VerticalScrollbar) m_VerticalScrollbar.onValueChanged.RemoveListener(SetVerticalNormalizedPosition);
                m_VerticalScrollbar = value;
                if (m_Vertical && m_VerticalScrollbar) m_VerticalScrollbar.onValueChanged.AddListener(SetVerticalNormalizedPosition);
                SetDirtyCaching();
            }
        }

        // ── runtime state ─────────────────────────────────────────────────────

        [System.NonSerialized] Vector2 m_PointerStartLocalCursor;
        [System.NonSerialized] protected Vector2 m_ContentStartPosition;
        [System.NonSerialized] RectTransform m_ViewRect;
        [System.NonSerialized] protected Bounds m_ContentBounds;
        [System.NonSerialized] Bounds m_ViewBounds;
        [System.NonSerialized] Vector2 m_Velocity;
        [System.NonSerialized] bool m_Dragging;
        [System.NonSerialized] bool m_Scrolling;
        [System.NonSerialized] Vector2 m_PrevPosition;
        [System.NonSerialized] Bounds m_PrevContentBounds;
        [System.NonSerialized] Bounds m_PrevViewBounds;
        [System.NonSerialized] bool m_HasRebuiltLayout;
        [System.NonSerialized] bool m_HSliderExpand;
        [System.NonSerialized] bool m_VSliderExpand;
        [System.NonSerialized] float m_HSliderHeight;
        [System.NonSerialized] float m_VSliderWidth;
        [System.NonSerialized] RectTransform m_HorizontalScrollbarRect;
        [System.NonSerialized] RectTransform m_VerticalScrollbarRect;
        [System.NonSerialized] readonly Vector3[] m_Corners = new Vector3[4];

        RectTransform rectTransform => (RectTransform)transform;

        protected RectTransform viewRect
        {
            get
            {
                if (m_ViewRect == null) m_ViewRect = m_Viewport;
                if (m_ViewRect == null) m_ViewRect = rectTransform;
                return m_ViewRect;
            }
        }

        /// <summary>Content speed in units per second.</summary>
        public Vector2 velocity { get => m_Velocity; set => m_Velocity = value; }

        // ── canvas update ─────────────────────────────────────────────────────

        public virtual void Rebuild(CanvasUpdate executing)
        {
            if (executing == CanvasUpdate.Prelayout) UpdateCachedData();
            if (executing == CanvasUpdate.PostLayout)
            {
                UpdateBounds();
                UpdateScrollbars(Vector2.zero);
                UpdatePrevData();
                m_HasRebuiltLayout = true;
            }
        }

        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }

        void UpdateCachedData()
        {
            m_HorizontalScrollbarRect = m_HorizontalScrollbar == null ? null : m_HorizontalScrollbar.transform as RectTransform;
            m_VerticalScrollbarRect = m_VerticalScrollbar == null ? null : m_VerticalScrollbar.transform as RectTransform;

            // True when the parts are children, or absent.
            bool viewIsChild = viewRect.parent == transform;
            bool hScrollbarIsChild = !m_HorizontalScrollbarRect || m_HorizontalScrollbarRect.parent == transform;
            bool vScrollbarIsChild = !m_VerticalScrollbarRect || m_VerticalScrollbarRect.parent == transform;
            bool allAreChildren = viewIsChild && hScrollbarIsChild && vScrollbarIsChild;

            m_HSliderExpand = allAreChildren && m_HorizontalScrollbarRect && horizontalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
            m_VSliderExpand = allAreChildren && m_VerticalScrollbarRect && verticalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
            m_HSliderHeight = m_HorizontalScrollbarRect == null ? 0f : m_HorizontalScrollbarRect.rect.height;
            m_VSliderWidth = m_VerticalScrollbarRect == null ? 0f : m_VerticalScrollbarRect.rect.width;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_Horizontal && m_HorizontalScrollbar) m_HorizontalScrollbar.onValueChanged.AddListener(SetHorizontalNormalizedPosition);
            if (m_Vertical && m_VerticalScrollbar) m_VerticalScrollbar.onValueChanged.AddListener(SetVerticalNormalizedPosition);
            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
            SetDirty();
        }

        protected override void OnDisable()
        {
            CanvasUpdateRegistry.DisableCanvasElementForRebuild(this);
            if (m_HorizontalScrollbar) m_HorizontalScrollbar.onValueChanged.RemoveListener(SetHorizontalNormalizedPosition);
            if (m_VerticalScrollbar) m_VerticalScrollbar.onValueChanged.RemoveListener(SetVerticalNormalizedPosition);
            m_Dragging = false;
            m_Scrolling = false;
            m_HasRebuiltLayout = false;
            m_Velocity = Vector2.zero;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            base.OnDisable();
        }

        public override bool IsActive() => base.IsActive() && m_Content != null;

        void EnsureLayoutHasRebuilt()
        {
            if (!m_HasRebuiltLayout && !CanvasUpdateRegistry.IsRebuildingLayout())
                Canvas.ForceUpdateCanvases();
        }

        public virtual void StopMovement() => m_Velocity = Vector2.zero;

        // ── input ─────────────────────────────────────────────────────────────

        public virtual void OnScroll(PointerEventData data)
        {
            if (!IsActive()) return;
            EnsureLayoutHasRebuilt();
            UpdateBounds();

            Vector2 delta = data.scrollDelta;
            // The wheel reports down as negative; up is positive in the UI's space.
            delta.y *= -1;
            if (vertical && !horizontal)
            {
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) delta.y = delta.x;
                delta.x = 0;
            }
            if (horizontal && !vertical)
            {
                if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x)) delta.x = delta.y;
                delta.y = 0;
            }

            if (data.IsScrolling()) m_Scrolling = true;

            Vector2 position = m_Content.anchoredPosition;
            position += delta * m_ScrollSensitivity;
            if (m_MovementType == MovementType.Clamped)
                position += CalculateOffset(position - m_Content.anchoredPosition);

            SetContentAnchoredPosition(position);
            UpdateBounds();
        }

        public virtual void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            m_Velocity = Vector2.zero;
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive()) return;
            UpdateBounds();
            m_PointerStartLocalCursor = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out m_PointerStartLocalCursor);
            m_ContentStartPosition = m_Content.anchoredPosition;
            m_Dragging = true;
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            m_Dragging = false;
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!m_Dragging || eventData.button != PointerEventData.InputButton.Left || !IsActive()) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out var localCursor)) return;

            UpdateBounds();
            var pointerDelta = localCursor - m_PointerStartLocalCursor;
            Vector2 position = m_ContentStartPosition + pointerDelta;

            // Offset to get the content into place in the view.
            Vector2 offset = CalculateOffset(position - m_Content.anchoredPosition);
            position += offset;
            if (m_MovementType == MovementType.Elastic)
            {
                if (offset.x != 0) position.x -= RubberDelta(offset.x, m_ViewBounds.size.x);
                if (offset.y != 0) position.y -= RubberDelta(offset.y, m_ViewBounds.size.y);
            }

            SetContentAnchoredPosition(position);
        }

        protected virtual void SetContentAnchoredPosition(Vector2 position)
        {
            if (!m_Horizontal) position.x = m_Content.anchoredPosition.x;
            if (!m_Vertical) position.y = m_Content.anchoredPosition.y;
            if (position != m_Content.anchoredPosition)
            {
                m_Content.anchoredPosition = position;
                UpdateBounds();
            }
        }

        // ── motion ────────────────────────────────────────────────────────────

        protected virtual void LateUpdate()
        {
            if (!m_Content) return;

            EnsureLayoutHasRebuilt();
            UpdateBounds();
            float deltaTime = Time.unscaledDeltaTime;
            Vector2 offset = CalculateOffset(Vector2.zero);

            // A zero or negative step would divide by zero in the velocity estimate.
            if (deltaTime > 0f)
            {
                if (!m_Dragging && (offset != Vector2.zero || m_Velocity != Vector2.zero))
                {
                    Vector2 position = m_Content.anchoredPosition;
                    for (int axis = 0; axis < 2; axis++)
                    {
                        // Spring back when elastic content is out of bounds.
                        if (m_MovementType == MovementType.Elastic && offset[axis] != 0)
                        {
                            float speed = m_Velocity[axis];
                            float smoothTime = m_Elasticity;
                            if (m_Scrolling) smoothTime *= 3f;
                            position[axis] = Mathf.SmoothDamp(m_Content.anchoredPosition[axis], m_Content.anchoredPosition[axis] + offset[axis],
                                ref speed, smoothTime, Mathf.Infinity, deltaTime);
                            if (Mathf.Abs(speed) < 1) speed = 0;
                            m_Velocity[axis] = speed;
                        }
                        // Otherwise coast on the velocity, decelerating.
                        else if (m_Inertia)
                        {
                            m_Velocity[axis] *= Mathf.Pow(m_DecelerationRate, deltaTime);
                            if (Mathf.Abs(m_Velocity[axis]) < 1) m_Velocity[axis] = 0;
                            position[axis] += m_Velocity[axis] * deltaTime;
                        }
                        // Neither elasticity nor friction: no velocity.
                        else
                        {
                            m_Velocity[axis] = 0;
                        }
                    }

                    if (m_MovementType == MovementType.Clamped)
                    {
                        offset = CalculateOffset(position - m_Content.anchoredPosition);
                        position += offset;
                    }

                    SetContentAnchoredPosition(position);
                }

                if (m_Dragging && m_Inertia)
                {
                    Vector2 newVelocity = (m_Content.anchoredPosition - m_PrevPosition) / deltaTime;
                    m_Velocity = Vector2.Lerp(m_Velocity, newVelocity, deltaTime * 10);
                }
            }

            if (m_ViewBounds != m_PrevViewBounds || m_ContentBounds != m_PrevContentBounds || m_Content.anchoredPosition != m_PrevPosition)
            {
                UpdateScrollbars(offset);
                m_OnValueChanged.Invoke(normalizedPosition);
                UpdatePrevData();
            }
            UpdateScrollbarVisibility();
            m_Scrolling = false;
        }

        protected void UpdatePrevData()
        {
            m_PrevPosition = m_Content == null ? Vector2.zero : m_Content.anchoredPosition;
            m_PrevViewBounds = m_ViewBounds;
            m_PrevContentBounds = m_ContentBounds;
        }

        void UpdateScrollbars(Vector2 offset)
        {
            if (m_HorizontalScrollbar)
            {
                m_HorizontalScrollbar.size = m_ContentBounds.size.x > 0
                    ? Mathf.Clamp01((m_ViewBounds.size.x - Mathf.Abs(offset.x)) / m_ContentBounds.size.x)
                    : 1f;
                m_HorizontalScrollbar.value = horizontalNormalizedPosition;
            }

            if (m_VerticalScrollbar)
            {
                m_VerticalScrollbar.size = m_ContentBounds.size.y > 0
                    ? Mathf.Clamp01((m_ViewBounds.size.y - Mathf.Abs(offset.y)) / m_ContentBounds.size.y)
                    : 1f;
                m_VerticalScrollbar.value = verticalNormalizedPosition;
            }
        }

        // ── normalized positions ──────────────────────────────────────────────

        public Vector2 normalizedPosition
        {
            get => new(horizontalNormalizedPosition, verticalNormalizedPosition);
            set
            {
                SetNormalizedPosition(value.x, 0);
                SetNormalizedPosition(value.y, 1);
            }
        }

        public float horizontalNormalizedPosition
        {
            get
            {
                UpdateBounds();
                if (m_ContentBounds.size.x <= m_ViewBounds.size.x || Mathf.Approximately(m_ContentBounds.size.x, m_ViewBounds.size.x))
                    return m_ViewBounds.min.x > m_ContentBounds.min.x ? 1 : 0;
                return (m_ViewBounds.min.x - m_ContentBounds.min.x) / (m_ContentBounds.size.x - m_ViewBounds.size.x);
            }
            set => SetNormalizedPosition(value, 0);
        }

        public float verticalNormalizedPosition
        {
            get
            {
                UpdateBounds();
                if (m_ContentBounds.size.y <= m_ViewBounds.size.y || Mathf.Approximately(m_ContentBounds.size.y, m_ViewBounds.size.y))
                    return m_ViewBounds.min.y > m_ContentBounds.min.y ? 1 : 0;
                return (m_ViewBounds.min.y - m_ContentBounds.min.y) / (m_ContentBounds.size.y - m_ViewBounds.size.y);
            }
            set => SetNormalizedPosition(value, 1);
        }

        void SetHorizontalNormalizedPosition(float value) => SetNormalizedPosition(value, 0);
        void SetVerticalNormalizedPosition(float value) => SetNormalizedPosition(value, 1);

        protected virtual void SetNormalizedPosition(float value, int axis)
        {
            EnsureLayoutHasRebuilt();
            UpdateBounds();
            // How much the content is larger than the view.
            float hiddenLength = m_ContentBounds.size[axis] - m_ViewBounds.size[axis];
            // Where the content bounds' min corner should be, in the view's space.
            float contentBoundsMinPosition = m_ViewBounds.min[axis] - value * hiddenLength;
            // The content's new anchored position, in the view's space.
            float newAnchoredPosition = m_Content.anchoredPosition[axis] + contentBoundsMinPosition - m_ContentBounds.min[axis];

            Vector2 anchoredPosition = m_Content.anchoredPosition;
            if (Mathf.Abs(anchoredPosition[axis] - newAnchoredPosition) > 0.01f)
            {
                anchoredPosition[axis] = newAnchoredPosition;
                m_Content.anchoredPosition = anchoredPosition;
                m_Velocity[axis] = 0;
                UpdateBounds();
            }
        }

        static float RubberDelta(float overStretching, float viewSize)
            => (1 - 1 / (Mathf.Abs(overStretching) * 0.55f / viewSize + 1)) * viewSize * Mathf.Sign(overStretching);

        protected override void OnRectTransformDimensionsChange() => SetDirty();

        bool hScrollingNeeded => m_ContentBounds.size.x > m_ViewBounds.size.x + 0.01f;
        bool vScrollingNeeded => m_ContentBounds.size.y > m_ViewBounds.size.y + 0.01f;

        // ── layout (ILayoutElement / ILayoutGroup) ────────────────────────────

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }
        public virtual float minWidth => -1;
        public virtual float preferredWidth => -1;
        public virtual float flexibleWidth => -1;
        public virtual float minHeight => -1;
        public virtual float preferredHeight => -1;
        public virtual float flexibleHeight => -1;
        public virtual int layoutPriority => -1;

        public virtual void SetLayoutHorizontal()
        {
            UpdateCachedData();

            if (m_HSliderExpand || m_VSliderExpand)
            {
                // Full-size view, to see whether the content fits without scrollbars.
                viewRect.anchorMin = Vector2.zero;
                viewRect.anchorMax = Vector2.one;
                viewRect.sizeDelta = Vector2.zero;
                viewRect.anchoredPosition = Vector2.zero;
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
                m_ContentBounds = GetBounds();
            }

            // Too tall: show the vertical bar and narrow the view to make room for it.
            if (m_VSliderExpand && vScrollingNeeded)
            {
                viewRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
                m_ContentBounds = GetBounds();
            }

            // Too wide: show the horizontal bar and shorten the view to make room for it.
            if (m_HSliderExpand && hScrollingNeeded)
            {
                viewRect.sizeDelta = new Vector2(viewRect.sizeDelta.x, -(m_HSliderHeight + m_HorizontalScrollbarSpacing));
                m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
                m_ContentBounds = GetBounds();
            }

            // The horizontal bar may now make the content too tall after all.
            if (m_VSliderExpand && vScrollingNeeded && viewRect.sizeDelta.x == 0 && viewRect.sizeDelta.y < 0)
                viewRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);
        }

        public virtual void SetLayoutVertical()
        {
            UpdateScrollbarLayout();
            m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
            m_ContentBounds = GetBounds();
        }

        void UpdateScrollbarVisibility()
        {
            UpdateOneScrollbarVisibility(vScrollingNeeded, m_Vertical, m_VerticalScrollbarVisibility, m_VerticalScrollbar);
            UpdateOneScrollbarVisibility(hScrollingNeeded, m_Horizontal, m_HorizontalScrollbarVisibility, m_HorizontalScrollbar);
        }

        static void UpdateOneScrollbarVisibility(bool scrollingNeeded, bool axisEnabled, ScrollbarVisibility visibility, Scrollbar scrollbar)
        {
            if (!scrollbar) return;
            bool show = visibility == ScrollbarVisibility.Permanent ? axisEnabled : scrollingNeeded;
            if (scrollbar.gameObject.activeSelf != show) scrollbar.gameObject.SetActive(show);
        }

        void UpdateScrollbarLayout()
        {
            if (m_VSliderExpand && m_HorizontalScrollbar)
            {
                m_HorizontalScrollbarRect.anchorMin = new Vector2(0, m_HorizontalScrollbarRect.anchorMin.y);
                m_HorizontalScrollbarRect.anchorMax = new Vector2(1, m_HorizontalScrollbarRect.anchorMax.y);
                m_HorizontalScrollbarRect.anchoredPosition = new Vector2(0, m_HorizontalScrollbarRect.anchoredPosition.y);
                m_HorizontalScrollbarRect.sizeDelta = vScrollingNeeded
                    ? new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), m_HorizontalScrollbarRect.sizeDelta.y)
                    : new Vector2(0, m_HorizontalScrollbarRect.sizeDelta.y);
            }

            if (m_HSliderExpand && m_VerticalScrollbar)
            {
                m_VerticalScrollbarRect.anchorMin = new Vector2(m_VerticalScrollbarRect.anchorMin.x, 0);
                m_VerticalScrollbarRect.anchorMax = new Vector2(m_VerticalScrollbarRect.anchorMax.x, 1);
                m_VerticalScrollbarRect.anchoredPosition = new Vector2(m_VerticalScrollbarRect.anchoredPosition.x, 0);
                m_VerticalScrollbarRect.sizeDelta = hScrollingNeeded
                    ? new Vector2(m_VerticalScrollbarRect.sizeDelta.x, -(m_HSliderHeight + m_HorizontalScrollbarSpacing))
                    : new Vector2(m_VerticalScrollbarRect.sizeDelta.x, 0);
            }
        }

        // ── bounds ────────────────────────────────────────────────────────────

        protected void UpdateBounds()
        {
            m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
            m_ContentBounds = GetBounds();
            if (m_Content == null) return;

            Vector3 contentSize = m_ContentBounds.size;
            Vector3 contentPos = m_ContentBounds.center;
            var contentPivot = m_Content.pivot;
            AdjustBounds(ref m_ViewBounds, ref contentPivot, ref contentSize, ref contentPos);
            m_ContentBounds.size = contentSize;
            m_ContentBounds.center = contentPos;

            if (movementType == MovementType.Clamped)
            {
                // Keep the content covering the view when it has shrunk: its far edge may not come
                // inside the view's far edge, nor its near edge inside the near edge.
                Vector2 delta = Vector2.zero;
                if (m_ViewBounds.max.x > m_ContentBounds.max.x)
                    delta.x = System.Math.Min(m_ViewBounds.min.x - m_ContentBounds.min.x, m_ViewBounds.max.x - m_ContentBounds.max.x);
                else if (m_ViewBounds.min.x < m_ContentBounds.min.x)
                    delta.x = System.Math.Max(m_ViewBounds.min.x - m_ContentBounds.min.x, m_ViewBounds.max.x - m_ContentBounds.max.x);

                if (m_ViewBounds.min.y < m_ContentBounds.min.y)
                    delta.y = System.Math.Max(m_ViewBounds.min.y - m_ContentBounds.min.y, m_ViewBounds.max.y - m_ContentBounds.max.y);
                else if (m_ViewBounds.max.y > m_ContentBounds.max.y)
                    delta.y = System.Math.Min(m_ViewBounds.min.y - m_ContentBounds.min.y, m_ViewBounds.max.y - m_ContentBounds.max.y);

                if (delta.sqrMagnitude > float.Epsilon)
                {
                    contentPos = m_Content.anchoredPosition + delta;
                    if (!m_Horizontal) contentPos.x = m_Content.anchoredPosition.x;
                    if (!m_Vertical) contentPos.y = m_Content.anchoredPosition.y;
                    AdjustBounds(ref m_ViewBounds, ref contentPivot, ref contentSize, ref contentPos);
                }
            }
        }

        /// <summary>
        /// Pads the content's bounds to at least the view's size: scrolling is possible only when the
        /// content is larger than the view. The pivot decides which way they grow (a top pivot grows down).
        /// </summary>
        internal static void AdjustBounds(ref Bounds viewBounds, ref Vector2 contentPivot, ref Vector3 contentSize, ref Vector3 contentPos)
        {
            Vector3 excess = viewBounds.size - contentSize;
            if (excess.x > 0)
            {
                contentPos.x -= excess.x * (contentPivot.x - 0.5f);
                contentSize.x = viewBounds.size.x;
            }
            if (excess.y > 0)
            {
                contentPos.y -= excess.y * (contentPivot.y - 0.5f);
                contentSize.y = viewBounds.size.y;
            }
        }

        Bounds GetBounds()
        {
            if (m_Content == null) return new Bounds();
            m_Content.GetWorldCorners(m_Corners);
            var viewWorldToLocalMatrix = viewRect.worldToLocalMatrix;
            return InternalGetBounds(m_Corners, ref viewWorldToLocalMatrix);
        }

        internal static Bounds InternalGetBounds(Vector3[] corners, ref Matrix4x4 viewWorldToLocalMatrix)
        {
            var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int j = 0; j < 4; j++)
            {
                Vector3 v = viewWorldToLocalMatrix.MultiplyPoint3x4(corners[j]);
                vMin = Vector3.Min(v, vMin);
                vMax = Vector3.Max(v, vMax);
            }
            var bounds = new Bounds(vMin, Vector3.zero);
            bounds.Encapsulate(vMax);
            return bounds;
        }

        Vector2 CalculateOffset(Vector2 delta)
            => InternalCalculateOffset(ref m_ViewBounds, ref m_ContentBounds, m_Horizontal, m_Vertical, m_MovementType, ref delta);

        internal static Vector2 InternalCalculateOffset(ref Bounds viewBounds, ref Bounds contentBounds, bool horizontal, bool vertical,
            MovementType movementType, ref Vector2 delta)
        {
            Vector2 offset = Vector2.zero;
            if (movementType == MovementType.Unrestricted) return offset;

            Vector2 min = contentBounds.min;
            Vector2 max = contentBounds.max;

            // Thresholds rather than zero, so a resting view does not re-solve every frame.
            if (horizontal)
            {
                min.x += delta.x;
                max.x += delta.x;
                float maxOffset = viewBounds.max.x - max.x;
                float minOffset = viewBounds.min.x - min.x;
                if (minOffset < -0.001f) offset.x = minOffset;
                else if (maxOffset > 0.001f) offset.x = maxOffset;
            }

            if (vertical)
            {
                min.y += delta.y;
                max.y += delta.y;
                float maxOffset = viewBounds.max.y - max.y;
                float minOffset = viewBounds.min.y - min.y;
                if (maxOffset > 0.001f) offset.y = maxOffset;
                else if (minOffset < -0.001f) offset.y = minOffset;
            }

            return offset;
        }

        protected void SetDirty()
        {
            if (!IsActive()) return;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        protected void SetDirtyCaching()
        {
            if (!IsActive()) return;
            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            m_ViewRect = null;
        }
    }
}
