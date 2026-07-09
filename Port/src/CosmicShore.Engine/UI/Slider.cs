using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Value-range control (original contract: Slider : Selectable). The used surface
    /// — value / min / max / wholeNumbers / normalizedValue / SetValueWithoutNotify /
    /// onValueChanged — is the REAL value model with the original's clamp + rounding.
    /// The handle/fill visual mapping (and pointer-drag value setting, which needs it)
    /// arrives with Arc C; ported code drives the value directly today.
    /// </summary>
    public class Slider : Selectable
    {
        public enum Direction { LeftToRight = 0, RightToLeft = 1, BottomToTop = 2, TopToBottom = 3 }

        [SerializeField] float m_MinValue;
        [SerializeField] float m_MaxValue = 1f;
        [SerializeField] bool m_WholeNumbers;
        [SerializeField] float m_Value;

        public Direction direction = Direction.LeftToRight;
        public RectTransform fillRect;
        public RectTransform handleRect;

        public UnityEvent<float> onValueChanged = new();

        public float minValue
        {
            get => m_MinValue;
            set { m_MinValue = value; Set(m_Value, sendCallback: true); }
        }

        public float maxValue
        {
            get => m_MaxValue;
            set { m_MaxValue = value; Set(m_Value, sendCallback: true); }
        }

        public bool wholeNumbers
        {
            get => m_WholeNumbers;
            set { m_WholeNumbers = value; Set(m_Value, sendCallback: true); }
        }

        public float value
        {
            get => m_WholeNumbers ? Mathf.Round(m_Value) : m_Value;
            set => Set(value, sendCallback: true);
        }

        /// <summary>Value write without firing onValueChanged (UI sync paths).</summary>
        public void SetValueWithoutNotify(float input) => Set(input, sendCallback: false);

        public float normalizedValue
        {
            get => Mathf.Approximately(m_MinValue, m_MaxValue) ? 0f : Mathf.InverseLerp(m_MinValue, m_MaxValue, value);
            set => this.value = Mathf.Lerp(m_MinValue, m_MaxValue, value);
        }

        void Set(float input, bool sendCallback)
        {
            float clamped = Mathf.Clamp(input, m_MinValue, m_MaxValue);
            if (m_WholeNumbers) clamped = Mathf.Round(clamped);
            if (m_Value == clamped) return;
            m_Value = clamped;
            if (sendCallback) onValueChanged.Invoke(clamped);
        }
    }
}
