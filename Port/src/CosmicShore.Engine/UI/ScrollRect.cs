using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Scrollable viewport (original contract: ScrollRect) riding the Arc-D drag
    /// pipeline (IBeginDrag/IDrag/IEndDrag) + IScrollHandler. The used surface is
    /// REAL: drag pans the content (per-axis gates, Clamped bounds), the wheel
    /// scrolls by sensitivity, programmatic <see cref="velocity"/> flings decay in
    /// LateUpdate by decelerationRate (the GameEventFeed's fling), and the
    /// normalized positions map content travel to [0,1]. Headless deviations
    /// (documented): Elastic clamps like Clamped (the overshoot spring is
    /// presentation), and drag-release velocity is not inferred from pointer history
    /// — the project only ever sets velocity programmatically.
    /// </summary>
    public class ScrollRect : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public enum MovementType { Unrestricted = 0, Elastic = 1, Clamped = 2 }
        public enum ScrollbarVisibility { Permanent = 0, AutoHide = 1, AutoHideAndExpandViewport = 2 }

        public RectTransform content;
        public RectTransform viewport;
        public bool horizontal = true;
        public bool vertical = true;
        public MovementType movementType = MovementType.Elastic;
        public float elasticity = 0.1f;
        public bool inertia = true;
        public float decelerationRate = 0.135f;
        public float scrollSensitivity = 1f;
        public ScrollbarVisibility verticalScrollbarVisibility = ScrollbarVisibility.AutoHide;
        public ScrollbarVisibility horizontalScrollbarVisibility = ScrollbarVisibility.AutoHide;

        public UnityEvent<Vector2> onValueChanged = new();

        /// <summary>Content speed in units/second — decays each LateUpdate while flinging.</summary>
        public Vector2 velocity;

        RectTransform viewRect => viewport != null ? viewport : transform as RectTransform;

        Vector2 m_PointerStart;
        Vector2 m_ContentStartPosition;
        bool m_Dragging;

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || content == null) return;
            m_Dragging = true;
            m_PointerStart = eventData.position;
            m_ContentStartPosition = content.anchoredPosition;
            velocity = Vector2.zero;
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!m_Dragging || content == null) return;
            var delta = eventData.position - m_PointerStart;
            SetContentPosition(m_ContentStartPosition + delta);
        }

        public virtual void OnEndDrag(PointerEventData eventData) => m_Dragging = false;

        public virtual void OnScroll(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || content == null) return;
            // Original mapping: wheel Y scrolls the vertical axis (inverted), X the horizontal.
            var delta = new Vector2(
                eventData.scrollDelta.x * scrollSensitivity,
                -eventData.scrollDelta.y * scrollSensitivity);
            SetContentPosition(content.anchoredPosition + delta);
        }

        void LateUpdate()
        {
            if (content == null || m_Dragging) return;
            if (velocity == Vector2.zero) return;

            float dt = Time.deltaTime;
            SetContentPosition(content.anchoredPosition + velocity * dt);

            if (!inertia) { velocity = Vector2.zero; return; }
            velocity *= Mathf.Pow(decelerationRate, dt);
            if (velocity.sqrMagnitude < 1f) velocity = Vector2.zero;
        }

        void SetContentPosition(Vector2 target)
        {
            var current = content.anchoredPosition;
            if (!horizontal) target.x = current.x;
            if (!vertical) target.y = current.y;

            if (movementType != MovementType.Unrestricted)
                target = ClampToBounds(target);

            if (target == current) return;
            content.anchoredPosition = target;
            onValueChanged.Invoke(normalizedPosition);
        }

        /// <summary>
        /// Keeps the viewport covered by content: the content may not pull its leading
        /// edge past the view's, per axis. Content smaller than the view pins to the
        /// view's origin edge.
        /// </summary>
        Vector2 ClampToBounds(Vector2 target)
        {
            var view = viewRect;
            if (view == null) return target;

            Vector2 contentSize = content.rect.size;
            Vector2 viewSize = view.rect.size;
            Vector2 slack = contentSize - viewSize; // ≥0 → scrollable range per axis

            Vector2 offset = target - m_BasePosition();
            offset.x = slack.x > 0f ? Mathf.Clamp(offset.x, -slack.x, 0f) : 0f;
            offset.y = slack.y > 0f ? Mathf.Clamp(offset.y, 0f, slack.y) : 0f;
            return m_BasePosition() + offset;
        }

        // The content's rest position (travel measures from here). Captured lazily so
        // authoring order doesn't matter.
        bool m_HasBase;
        Vector2 m_Base;

        Vector2 m_BasePosition()
        {
            if (!m_HasBase)
            {
                m_Base = content != null ? content.anchoredPosition : Vector2.zero;
                m_HasBase = true;
            }
            return m_Base;
        }

        public Vector2 normalizedPosition
        {
            get => new(horizontalNormalizedPosition, verticalNormalizedPosition);
            set { horizontalNormalizedPosition = value.x; verticalNormalizedPosition = value.y; }
        }

        public float horizontalNormalizedPosition
        {
            get
            {
                float slack = SlackX();
                if (slack <= 0f) return 0f;
                return Mathf.Clamp01(-(content.anchoredPosition.x - m_BasePosition().x) / slack);
            }
            set
            {
                float slack = SlackX();
                if (slack <= 0f || content == null) return;
                var p = content.anchoredPosition;
                SetContentPosition(new Vector2(m_BasePosition().x - Mathf.Clamp01(value) * slack, p.y));
            }
        }

        public float verticalNormalizedPosition
        {
            get
            {
                float slack = SlackY();
                if (slack <= 0f) return 0f;
                return Mathf.Clamp01((content.anchoredPosition.y - m_BasePosition().y) / slack);
            }
            set
            {
                float slack = SlackY();
                if (slack <= 0f || content == null) return;
                var p = content.anchoredPosition;
                SetContentPosition(new Vector2(p.x, m_BasePosition().y + Mathf.Clamp01(value) * slack));
            }
        }

        float SlackX() => content != null && viewRect != null ? Mathf.Max(0f, content.rect.width - viewRect.rect.width) : 0f;
        float SlackY() => content != null && viewRect != null ? Mathf.Max(0f, content.rect.height - viewRect.rect.height) : 0f;
    }
}
