using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// A time-left bar that empties from full to nothing over a known lifetime - a toast's 5 s, the 3 s a
    /// party invite stays up. The bar is this object, pivoted on the left; it scales its x from 1 to 0.
    /// It shows time that is really running out, so it is functional motion, not ambient.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Drain")]
    [DisallowMultipleComponent]
    public class KitDrain : MonoBehaviour
    {
        [Tooltip("Seconds from full to empty when played on enable.")]
        [SerializeField, Min(0.1f)] private float duration = 5f;
        [Tooltip("Start draining whenever the object is enabled. Off: only Play() starts it.")]
        [SerializeField] private bool playOnEnable = true;

        private RectTransform _rect;
        private Vector3 _restScale = Vector3.one;
        private bool _scaleCaptured;   // captured on the first Play, not in Awake (check_rest_scale_capture.py)
        private float _clock = -1f;
        private float _length;

        private void Awake()
        {
            _rect = transform as RectTransform;
        }

        private void OnEnable()
        {
            if (playOnEnable) Play(duration);
        }

        private void OnDisable()
        {
            _clock = -1f;
            if (_rect && _scaleCaptured) _rect.localScale = _restScale;
        }

        /// <summary>Refill and drain over <paramref name="seconds"/>.</summary>
        public void Play(float seconds)
        {
            _length = Mathf.Max(0.1f, seconds);
            if (_rect && !_scaleCaptured) { _restScale = _rect.localScale; _scaleCaptured = true; }
            _clock = 0f;
            SetFill(1f);
        }

        private void Update()
        {
            if (_clock < 0f) return;
            _clock += Time.unscaledDeltaTime;
            float left = 1f - _clock / _length;
            if (left <= 0f) { _clock = -1f; left = 0f; }
            SetFill(left);
        }

        private void SetFill(float f)
        {
            if (_rect) _rect.localScale = new Vector3(_restScale.x * f, _restScale.y, _restScale.z);
        }
    }
}
