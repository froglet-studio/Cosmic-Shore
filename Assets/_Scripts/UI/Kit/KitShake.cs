using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The refusal shake (std token, 240 ms): a short horizontal shudder that tells the player a locked
    /// entry heard the press and said no. It accompanies the Denied sting and the toast; it never
    /// replaces them. Started by whoever refuses - <see cref="Play(GameObject)"/> is a no-op on an
    /// object without one, so a refusal path can call it unconditionally.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Shake")]
    [DisallowMultipleComponent]
    public class KitShake : MonoBehaviour
    {
        [Tooltip("Peak offset in canvas units.")]
        [SerializeField, Min(0f)] private float amplitude = 6f;
        [Tooltip("Seconds.")]
        [SerializeField, Min(0.05f)] private float duration = 0.24f;

        private RectTransform _rect;
        private Vector2 _rest;
        private float _clock = -1f;

        /// <summary>Shake the object's KitShake, if it has one.</summary>
        public static void Play(GameObject target)
        {
            if (target && target.TryGetComponent(out KitShake shake)) shake.Play();
        }

        private void Awake() => _rect = transform as RectTransform;

        public void Play()
        {
            if (!_rect) return;
            if (_clock < 0f) _rest = _rect.anchoredPosition;
            _clock = 0f;
        }

        private void OnDisable() => Stop();

        private void Stop()
        {
            if (_clock < 0f || !_rect) return;
            _clock = -1f;
            _rect.anchoredPosition = _rest;
        }

        private void Update()
        {
            if (_clock < 0f) return;
            _clock += Time.unscaledDeltaTime;
            float t = _clock / duration;
            if (t >= 1f) { Stop(); return; }
            float x = Mathf.Sin(t * Mathf.PI * 5f) * amplitude * (1f - t);
            _rect.anchoredPosition = _rest + new Vector2(x, 0f);
        }
    }
}
