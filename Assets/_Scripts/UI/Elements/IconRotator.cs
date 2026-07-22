using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drop-anywhere continuous spinner for 2D icons: rotates its own
    /// transform around Z every frame. Paste it on any UI Image (or world
    /// sprite) and it works - no references to wire. Speed and direction
    /// come from one signed degrees-per-second value; runs on unscaled
    /// time by default so it keeps spinning on loading screens and while
    /// paused.
    /// </summary>
    public class IconRotator : MonoBehaviour
    {
        [Header("Rotation")]
        [Tooltip("Degrees per second around Z. Negative = clockwise, positive = counter-clockwise. -360 = one full clockwise turn per second.")]
        [SerializeField] private float degreesPerSecond = -180f;

        [Tooltip("Use unscaled time so the spin keeps running while timeScale is 0 (loading screens, pause).")]
        [SerializeField] private bool useUnscaledTime = true;

        [Tooltip("Snap back to the authored rotation when the component is disabled.")]
        [SerializeField] private bool resetOnDisable = true;

        /// <summary>Runtime speed control - sign flips direction, 0 holds still.</summary>
        public float DegreesPerSecond
        {
            get => degreesPerSecond;
            set => degreesPerSecond = value;
        }

        private Quaternion _restRotation;

        void Awake() => _restRotation = transform.localRotation;

        void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            transform.Rotate(0f, 0f, degreesPerSecond * dt);
        }

        void OnDisable()
        {
            if (resetOnDisable)
                transform.localRotation = _restRotation;
        }
    }
}
