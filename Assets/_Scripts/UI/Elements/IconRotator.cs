using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drop-anywhere spinner for 2D icons: rotates its own transform a full
    /// 360 around Z (default one turn per second - 6 per frame at 60 fps)
    /// while smoothly cycling the icon's tint through a color palette.
    /// Paste it on any UI Image or world sprite and it works - the tint
    /// target (Graphic or SpriteRenderer) is auto-detected, nothing to wire.
    /// Runs on unscaled time by default so it keeps animating on loading
    /// screens and while paused.
    /// </summary>
    public class IconRotator : MonoBehaviour
    {
        [Header("Rotation")]
        [Tooltip("Degrees per second around Z. Negative = clockwise. -360 = one full clockwise turn per second (6 per frame at 60 fps).")]
        [SerializeField] private float degreesPerSecond = -360f;

        [Header("Color Cycle")]
        [Tooltip("Colors the icon smoothly blends through in a loop. Fewer than 2 entries keeps the authored color.")]
        [SerializeField] private Color[] cycleColors =
        {
            Color.white,
            new Color(0.25f, 0.95f, 1f),  // cyan
            new Color(1f, 0.30f, 0.85f),  // magenta
            new Color(0.65f, 0.45f, 1f),  // violet
        };
        [Tooltip("Seconds for one full trip through the palette.")]
        [SerializeField, Min(0.1f)] private float colorCycleSeconds = 3f;

        [Header("Timing")]
        [Tooltip("Use unscaled time so the spin keeps running while timeScale is 0 (loading screens, pause).")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("Snap back to the authored rotation and color when the component is disabled.")]
        [SerializeField] private bool resetOnDisable = true;

        /// <summary>Runtime speed control - sign flips direction, 0 holds still.</summary>
        public float DegreesPerSecond
        {
            get => degreesPerSecond;
            set => degreesPerSecond = value;
        }

        private Graphic _graphic;
        private SpriteRenderer _spriteRenderer;
        private Quaternion _restRotation;
        private Color _restColor = Color.white;
        private float _colorElapsed;

        void Awake()
        {
            _restRotation = transform.localRotation;

            // Tint whichever 2D surface this sits on - UI first, then sprite.
            if (TryGetComponent(out _graphic))
                _restColor = _graphic.color;
            else if (TryGetComponent(out _spriteRenderer))
                _restColor = _spriteRenderer.color;
        }

        void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            transform.Rotate(0f, 0f, degreesPerSecond * dt);

            if (cycleColors is { Length: > 1 })
            {
                _colorElapsed = (_colorElapsed + dt) % colorCycleSeconds;
                ApplyColor(SamplePalette(_colorElapsed / colorCycleSeconds));
            }
        }

        void OnDisable()
        {
            if (!resetOnDisable)
                return;

            transform.localRotation = _restRotation;
            _colorElapsed = 0f;
            ApplyColor(_restColor);
        }

        /// <summary>Loops through the palette, blending linearly between neighbors.</summary>
        private Color SamplePalette(float t01)
        {
            float scaled = t01 * cycleColors.Length;
            int from = Mathf.FloorToInt(scaled) % cycleColors.Length;
            int to = (from + 1) % cycleColors.Length;

            Color color = Color.Lerp(cycleColors[from], cycleColors[to], scaled - Mathf.Floor(scaled));
            color.a = _restColor.a;
            return color;
        }

        private void ApplyColor(Color color)
        {
            if (_graphic != null)
                _graphic.color = color;
            else if (_spriteRenderer != null)
                _spriteRenderer.color = color;
        }
    }
}
