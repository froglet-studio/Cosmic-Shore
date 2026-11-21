using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// A scroll bar (original contract: Scrollbar). Its value (0..1, optionally stepped) and size
    /// (the handle's share of the track) drive the handle's anchors inside its parent, the track;
    /// Unity saves those driven anchors as zero, so the handle only appears once the bar sets them.
    /// Dragging the handle moves the value with the pointer (holding the grab offset), pressing the
    /// track pages toward the press every frame until release, and navigation on the bar's axis
    /// steps it. A ScrollRect drives value and size from its content and listens to onValueChanged.
    /// </summary>
    public class Scrollbar : Selectable, IBeginDragHandler, IDragHandler, IInitializePotentialDragHandler, ICanvasElement
    {
        public enum Direction { LeftToRight = 0, RightToLeft = 1, BottomToTop = 2, TopToBottom = 3 }

        [System.Serializable] public class ScrollEvent : UnityEvent<float> { }

        [SerializeField] RectTransform m_HandleRect;
        [SerializeField] Direction m_Direction = Direction.LeftToRight;
        [SerializeField] float m_Value;
        [SerializeField] float m_Size = 0.2f;
        [SerializeField] int m_NumberOfSteps;
        [SerializeField] ScrollEvent m_OnValueChanged = new();

        [System.NonSerialized] RectTransform m_ContainerRect;
        [System.NonSerialized] Vector2 m_Offset;
        [System.NonSerialized] bool isPointerDownAndNotDragging;
        [System.NonSerialized] Vector2 m_RepeatScreenPosition;
        [System.NonSerialized] Camera m_RepeatCamera;

        public RectTransform handleRect
        {
            get => m_HandleRect;
            set { if (m_HandleRect == value) return; m_HandleRect = value; UpdateCachedReferences(); UpdateVisuals(); }
        }

        public Direction direction
        {
            get => m_Direction;
            set { if (m_Direction == value) return; m_Direction = value; UpdateVisuals(); }
        }

        public float value
        {
            get
            {
                float val = m_Value;
                if (m_NumberOfSteps > 1) val = Mathf.Round(val * (m_NumberOfSteps - 1)) / (m_NumberOfSteps - 1);
                return val;
            }
            set => Set(value);
        }

        public virtual void SetValueWithoutNotify(float input) => Set(input, false);

        public float size
        {
            get => m_Size;
            set { float v = Mathf.Clamp01(value); if (m_Size == v) return; m_Size = v; UpdateVisuals(); }
        }

        public int numberOfSteps
        {
            get => m_NumberOfSteps;
            set { if (m_NumberOfSteps == value) return; m_NumberOfSteps = value; Set(m_Value); UpdateVisuals(); }
        }

        public ScrollEvent onValueChanged { get => m_OnValueChanged; set => m_OnValueChanged = value; }

        float stepSize => m_NumberOfSteps > 1 ? 1f / (m_NumberOfSteps - 1) : 0.1f;

        enum Axis { Horizontal = 0, Vertical = 1 }

        Axis axis => m_Direction is Direction.LeftToRight or Direction.RightToLeft ? Axis.Horizontal : Axis.Vertical;
        bool reverseValue => m_Direction is Direction.RightToLeft or Direction.TopToBottom;

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateCachedReferences();
            Set(m_Value, false);
            UpdateVisuals();
        }

        protected override void OnDisable()
        {
            isPointerDownAndNotDragging = false;
            base.OnDisable();
        }

        void UpdateCachedReferences()
            => m_ContainerRect = m_HandleRect && m_HandleRect.parent != null ? m_HandleRect.parent as RectTransform : null;

        void Set(float input, bool sendCallback = true)
        {
            float currentValue = m_Value;
            // Not clamped here: a ScrollRect's elastic overshoot reports values past the ends.
            m_Value = input;
            if (currentValue == value) return;
            UpdateVisuals();
            if (sendCallback) m_OnValueChanged.Invoke(value);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (!IsActive()) return;
            UpdateVisuals();
        }

        /// <summary>Drives the handle's anchors from value and size.</summary>
        void UpdateVisuals()
        {
            if (m_ContainerRect == null) return;
            Vector2 anchorMin = Vector2.zero, anchorMax = Vector2.one;
            float movement = Mathf.Clamp01(value) * (1f - size);
            if (reverseValue)
            {
                anchorMin[(int)axis] = 1f - movement - size;
                anchorMax[(int)axis] = 1f - movement;
            }
            else
            {
                anchorMin[(int)axis] = movement;
                anchorMax[(int)axis] = movement + size;
            }
            m_HandleRect.anchorMin = anchorMin;
            m_HandleRect.anchorMax = anchorMax;
        }

        void UpdateDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || m_ContainerRect == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_ContainerRect, eventData.position, eventData.pressEventCamera, out var localCursor)) return;

            Vector2 handleCenterRelativeToContainerCorner = localCursor - m_Offset - m_ContainerRect.rect.position;
            Vector2 handleCorner = handleCenterRelativeToContainerCorner - (m_HandleRect.rect.size - m_HandleRect.sizeDelta) * 0.5f;
            float parentSize = axis == Axis.Horizontal ? m_ContainerRect.rect.width : m_ContainerRect.rect.height;
            float remainingSize = parentSize * (1f - size);
            if (remainingSize <= 0f) return;

            switch (m_Direction)
            {
                case Direction.LeftToRight: Set(Mathf.Clamp01(handleCorner.x / remainingSize)); break;
                case Direction.RightToLeft: Set(Mathf.Clamp01(1f - handleCorner.x / remainingSize)); break;
                case Direction.BottomToTop: Set(Mathf.Clamp01(handleCorner.y / remainingSize)); break;
                case Direction.TopToBottom: Set(Mathf.Clamp01(1f - handleCorner.y / remainingSize)); break;
            }
        }

        bool MayDrag(PointerEventData eventData)
            => IsActive() && IsInteractable() && eventData.button == PointerEventData.InputButton.Left;

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            isPointerDownAndNotDragging = false;
            if (!MayDrag(eventData) || m_ContainerRect == null) return;

            m_Offset = Vector2.zero;
            var pressAt = eventData.pointerPressRaycast.screenPosition;
            if (RectTransformUtility.RectangleContainsScreenPoint(m_HandleRect, pressAt, eventData.enterEventCamera)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(m_HandleRect, pressAt, eventData.pressEventCamera, out var localMousePos))
                m_Offset = localMousePos - m_HandleRect.rect.center;
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!MayDrag(eventData)) return;
            if (m_ContainerRect != null) UpdateDrag(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!MayDrag(eventData)) return;
            base.OnPointerDown(eventData);
            isPointerDownAndNotDragging = true;
            m_RepeatScreenPosition = eventData.pointerPressRaycast.screenPosition;
            m_RepeatCamera = eventData.enterEventCamera;
            ClickRepeatStep();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            isPointerDownAndNotDragging = false;
        }

        // The original pages once per frame from a coroutine while the press holds outside the handle.
        void Update()
        {
            if (isPointerDownAndNotDragging) ClickRepeatStep();
        }

        void ClickRepeatStep()
        {
            if (m_HandleRect == null || RectTransformUtility.RectangleContainsScreenPoint(m_HandleRect, m_RepeatScreenPosition, m_RepeatCamera)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_HandleRect, m_RepeatScreenPosition, m_RepeatCamera, out var localMousePos)) return;
            float axisCoordinate = axis == Axis.Horizontal ? localMousePos.x : localMousePos.y;
            float change = axisCoordinate < 0 ? size : -size;
            value += reverseValue ? change : -change;
            value = Mathf.Clamp01(value);
            value = Mathf.Round(value * 10000f) / 10000f;
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                base.OnMove(eventData);
                return;
            }

            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    if (axis == Axis.Horizontal && FindSelectableOnLeft() == null) Set(Mathf.Clamp01(reverseValue ? value + stepSize : value - stepSize));
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Right:
                    if (axis == Axis.Horizontal && FindSelectableOnRight() == null) Set(Mathf.Clamp01(reverseValue ? value - stepSize : value + stepSize));
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Up:
                    if (axis == Axis.Vertical && FindSelectableOnUp() == null) Set(Mathf.Clamp01(reverseValue ? value - stepSize : value + stepSize));
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Down:
                    if (axis == Axis.Vertical && FindSelectableOnDown() == null) Set(Mathf.Clamp01(reverseValue ? value + stepSize : value - stepSize));
                    else base.OnMove(eventData);
                    break;
            }
        }

        public override Selectable FindSelectableOnLeft()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Horizontal ? null : base.FindSelectableOnLeft();

        public override Selectable FindSelectableOnRight()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Horizontal ? null : base.FindSelectableOnRight();

        public override Selectable FindSelectableOnUp()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Vertical ? null : base.FindSelectableOnUp();

        public override Selectable FindSelectableOnDown()
            => navigation.mode == Navigation.Mode.Automatic && axis == Axis.Vertical ? null : base.FindSelectableOnDown();

        public virtual void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void SetDirection(Direction direction, bool includeRectLayouts)
        {
            var oldAxis = axis;
            bool oldReverse = reverseValue;
            this.direction = direction;
            if (!includeRectLayouts) return;
            if (axis != oldAxis) RectTransformUtility.FlipLayoutAxes(transform as RectTransform, true, true);
            if (reverseValue != oldReverse) RectTransformUtility.FlipLayoutOnAxis(transform as RectTransform, (int)axis, true, true);
        }

        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }
    }
}
