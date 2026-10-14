using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>The loop a <see cref="KitAmbient"/> plays.</summary>
    public enum KitAmbientMode
    {
        /// <summary>Bobs up and down by Amplitude canvas units (a hull, a portrait, a formed crystal).</summary>
        Float = 0,
        /// <summary>Pulses its CanvasGroup alpha between Amplitude and 1 (a lamp, a hint, a state glow).</summary>
        Breathe = 1,
        /// <summary>Turns a full revolution every Period seconds (the settings gear).</summary>
        Spin = 2,
        /// <summary>Rocks Amplitude degrees either side (a toy preview standing in for its orbit).</summary>
        Sway = 3,
        /// <summary>Drifts and slowly zooms by Amplitude (card art, a preview still) - put it on the art under a mask.</summary>
        Pan = 4,
    }

    /// <summary>
    /// The ambient layer of Docs/MENU_REDESIGN_B.md §13.4 (decision Q1): a slow, endless loop that
    /// carries no state of its own. Each instance is separable - disable the component and the object
    /// returns to rest. Runs on unscaled time and from a phase offset, so neighbours never move in step.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Ambient")]
    [DisallowMultipleComponent]
    public class KitAmbient : MonoBehaviour
    {
        [Tooltip("Which loop.")]
        [SerializeField] private KitAmbientMode mode = KitAmbientMode.Float;
        [Tooltip("Seconds per cycle.")]
        [SerializeField, Min(0.1f)] private float period = 3.2f;
        [Tooltip("Float: canvas units. Breathe: lowest alpha (0-1). Sway: degrees. Pan: extra zoom (0.05 = 5 %). Spin: unused.")]
        [SerializeField] private float amplitude = 6f;
        [Tooltip("Seconds into the cycle this instance starts at, so neighbours drift apart.")]
        [SerializeField, Min(0f)] private float phase;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _restPosition;
        private Vector3 _restScale = Vector3.one;
        private Quaternion _restRotation = Quaternion.identity;
        private float _clock;

        private void Awake()
        {
            _rect = transform as RectTransform;
            TryGetComponent(out _group);
        }

        private void OnEnable()
        {
            if (!_rect) return;
            _restPosition = _rect.anchoredPosition;
            _restScale = _rect.localScale;
            _restRotation = _rect.localRotation;
            _clock = phase;
        }

        private void OnDisable()
        {
            if (!_rect) return;
            _rect.anchoredPosition = _restPosition;
            _rect.localScale = _restScale;
            _rect.localRotation = _restRotation;
            if (_group && mode == KitAmbientMode.Breathe) _group.alpha = 1f;
        }

        private void Update()
        {
            if (!_rect) return;
            _clock += Time.unscaledDeltaTime;
            float cycle = _clock / period;
            float wave = Mathf.Sin(cycle * Mathf.PI * 2f);
            switch (mode)
            {
                case KitAmbientMode.Float:
                    _rect.anchoredPosition = _restPosition + Vector2.up * (wave * amplitude * 0.5f);
                    break;
                case KitAmbientMode.Breathe:
                    if (_group) _group.alpha = Mathf.Lerp(Mathf.Clamp01(amplitude), 1f, wave * 0.5f + 0.5f);
                    break;
                case KitAmbientMode.Spin:
                    _rect.localRotation = _restRotation * Quaternion.Euler(0f, 0f, -360f * (cycle % 1f));
                    break;
                case KitAmbientMode.Sway:
                    _rect.localRotation = _restRotation * Quaternion.Euler(0f, 0f, wave * amplitude);
                    break;
                case KitAmbientMode.Pan:
                {
                    float p = Mathf.PingPong(cycle * 2f, 1f);
                    float s = 1f + amplitude * (0.8f + 1.0f * KitMotion.Ease(KitEase.InOutSine, p));
                    _rect.localScale = _restScale * s;
                    _rect.anchoredPosition = _restPosition + new Vector2(Mathf.Lerp(-1f, 1f, p), -p) * (_rect.rect.width * 0.01f);
                    break;
                }
            }
        }
    }
}
