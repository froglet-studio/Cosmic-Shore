using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// A value slider (original contract: Slider). The value drives the fill and handle: the fill
    /// rect's anchors grow along the slider's axis to the normalized value (or a Filled image's
    /// fillAmount does), and the handle's anchors sit at it. Unity saves those driven anchors as
    /// zero, so a scene's fill is invisible until the slider sets them, which it does on enable,
    /// on every value, range or direction change, and when its rect changes size, enabled or not.
    /// Pointer down on the track jumps the value there, dragging follows the pointer, and
    /// navigation on the slider's own axis steps it (a tenth of the range, or 1 for whole numbers).
    /// </summary>
    public class Slider : Selectable, IDragHandler, IInitializePotentialDragHandler, ICanvasElement
    {
        public enum Direction { LeftToRight = 0, RightToLeft = 1, BottomToTop = 2, TopToBottom = 3 }

        [System.Serializable] public class SliderEvent : UnityEvent<float> { }

        [SerializeField] RectTransform m_FillRect;
        [SerializeField] RectTransform m_HandleRect;
        [SerializeField] Direction m_Direction = Direction.LeftToRight;
        [SerializeField] float m_MinValue;
        [SerializeField] float m_MaxValue = 1f;
        [SerializeField] bool m_WholeNumbers;
        [SerializeField] protected float m_Value;
        [SerializeField] SliderEvent m_OnValueChanged = new();

        [System.NonSerialized] Image m_FillImage;
        [System.NonSerialized] RectTransform m_FillContainerRect;
        [System.NonSerialized] RectTransform m_HandleContainerRect;
        [System.NonSerialized] Vector2 m_Offset;
        [System.NonSerialized] bool m_ReferencesCached;

        public RectTransform fillRect
        {
            get => m_FillRect;
            set { if (m_FillRect == value) return; m_FillRect = value; UpdateCachedReferences(); UpdateVisuals(); }
        }

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

        public float minValue
        {
            get => m_MinValue;
            set { if (m_MinValue == value) return; m_MinValue = value; Set(m_Value); UpdateVisuals(); }
        }

        public float maxValue
        {
            get => m_MaxValue;
            set { if (m_MaxValue == value) return; m_MaxValue = value; Set(m_Value); UpdateVisuals(); }
        }

        public bool wholeNumbers
        {
            get => m_WholeNumbers;
            set { if (m_WholeNumbers == value) return; m_WholeNumbers = value; Set(m_Value); UpdateVisuals(); }
        }

        public virtual float value
        {
            get => wholeNumbers ? Mathf.Round(m_Value) : m_Value;
            set => Set(value);
        }

        /// <summary>Value write without firing onValueChanged (UI sync paths).</summary>
        public virtual void SetValueWithoutNotify(float input) => Set(input, false);

        public float normalizedValue
        {
            get => Mathf.Approximately(minValue, maxValue) ? 0f : Mathf.InverseLerp(minValue, maxValue, value);
            set => this.value = Mathf.Lerp(minValue, maxValue, value);
        }

        public SliderEvent onValueChanged { get => m_OnValueChanged; set => m_OnValueChanged = value; }

        float stepSize => wholeNumbers ? 1f : (maxValue - minValue) * 0.1f;

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

        void UpdateCachedReferences()
        {
            m_ReferencesCached = true;
            if (m_FillRect && m_FillRect != (RectTransform)transform)
            {
                m_FillImage = m_FillRect.GetComponent<Image>();
                m_FillContainerRect = m_FillRect.parent as RectTransform;
            }
            else
            {
                m_FillRect = null;
                m_FillContainerRect = null;
                m_FillImage = null;
            }

            if (m_HandleRect && m_HandleRect != (RectTransform)transform)
                m_HandleContainerRect = m_HandleRect.parent as RectTransform;
            else
            {
                m_HandleRect = null;
                m_HandleContainerRect = null;
            }
        }

        float ClampValue(float input)
        {
            float v = Mathf.Clamp(input, minValue, maxValue);
            if (wholeNumbers) v = Mathf.Round(v);
            return v;
        }

        protected virtual void Set(float input, bool sendCallback = true)
        {
            float newValue = ClampValue(input);
            if (m_Value == newValue) return;
            m_Value = newValue;
            UpdateVisuals();
            if (sendCallback) m_OnValueChanged.Invoke(newValue);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (!IsActive()) return;
            UpdateVisuals();
        }

        /// <summary>Drives the fill and handle anchors from the value.</summary>
        void UpdateVisuals()
        {
            // Scene load runs OnEnable in no fixed order across objects, so a script can disable a
            // slider before the slider's own OnEnable ran (the quest track's display-only bar). Its
            // value still drives the bar: resolve the fill and handle on first use.
            if (!m_ReferencesCached) UpdateCachedReferences();

            if (m_FillContainerRect != null)
            {
                Vector2 anchorMin = Vector2.zero, anchorMax = Vector2.one;
                if (m_FillImage != null && m_FillImage.type == Image.Type.Filled)
                    m_FillImage.fillAmount = normalizedValue;
                else if (reverseValue)
                    anchorMin[(int)axis] = 1f - normalizedValue;
                else
                    anchorMax[(int)axis] = normalizedValue;
                m_FillRect.anchorMin = anchorMin;
                m_FillRect.anchorMax = anchorMax;
            }

            if (m_HandleContainerRect != null)
            {
                Vector2 anchorMin = Vector2.zero, anchorMax = Vector2.one;
                anchorMin[(int)axis] = anchorMax[(int)axis] = reverseValue ? 1f - normalizedValue : normalizedValue;
                m_HandleRect.anchorMin = anchorMin;
                m_HandleRect.anchorMax = anchorMax;
            }
        }

        void UpdateDrag(PointerEventData eventData, Camera cam)
        {
            var clickRect = m_HandleContainerRect ?? m_FillContainerRect;
            if (clickRect == null || clickRect.rect.size[(int)axis] <= 0f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(clickRect, eventData.position, cam, out var localCursor)) return;
            localCursor -= clickRect.rect.position;
            float val = Mathf.Clamp01((localCursor - m_Offset)[(int)axis] / clickRect.rect.size[(int)axis]);
            normalizedValue = reverseValue ? 1f - val : val;
        }

        bool MayDrag(PointerEventData eventData)
            => IsActive() && IsInteractable() && eventData.button == PointerEventData.InputButton.Left;

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!MayDrag(eventData)) return;
            base.OnPointerDown(eventData);
            m_Offset = Vector2.zero;
            var pressAt = eventData.pointerPressRaycast.screenPosition;
            if (m_HandleContainerRect != null && RectTransformUtility.RectangleContainsScreenPoint(m_HandleRect, pressAt, eventData.enterEventCamera))
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_HandleRect, pressAt, eventData.pressEventCamera, out var localMousePos))
                    m_Offset = localMousePos;
            }
            else
            {
                // Outside the handle: jump to the press.
                UpdateDrag(eventData, eventData.pressEventCamera);
            }
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!MayDrag(eventData)) return;
            UpdateDrag(eventData, eventData.pressEventCamera);
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
                    if (axis == Axis.Horizontal && FindSelectableOnLeft() == null) Set(reverseValue ? value + stepSize : value - stepSize);
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Right:
                    if (axis == Axis.Horizontal && FindSelectableOnRight() == null) Set(reverseValue ? value - stepSize : value + stepSize);
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Up:
                    if (axis == Axis.Vertical && FindSelectableOnUp() == null) Set(reverseValue ? value - stepSize : value + stepSize);
                    else base.OnMove(eventData);
                    break;
                case MoveDirection.Down:
                    if (axis == Axis.Vertical && FindSelectableOnDown() == null) Set(reverseValue ? value + stepSize : value - stepSize);
                    else base.OnMove(eventData);
                    break;
            }
        }

        // Automatic navigation leaves the slider's own axis to the slider.
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
