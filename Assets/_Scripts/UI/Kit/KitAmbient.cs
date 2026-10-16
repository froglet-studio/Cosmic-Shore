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
    ///
    /// <para>It SLEEPS while nobody can see it (<see cref="KitSightline"/>): under a closed window, or on a
    /// screen the strip has slid off. Every move re-batches the canvas it lives on, so a loop that ran
    /// behind a closed modal cost the whole menu a canvas rebuild every frame for nothing.</para>
    ///
    /// <para>A Breathe modulates the alpha its OWNER set rather than replacing it: a part a script hid
    /// (a KitVisibility owner writing 0) stays hidden, and a dimmed one breathes around its dim.</para>
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
        private bool _scaleCaptured;   // only Pan writes a scale; it captures lazily (check_rest_scale_capture.py)
        private Quaternion _restRotation = Quaternion.identity;
        private float _clock;
        private KitSightline _sight;
        private float _restAlpha = 1f;
        private float _lastAlpha = -1f;

        private void Awake()
        {
            _rect = transform as RectTransform;
            TryGetComponent(out _group);
            _sight = new KitSightline(transform, mode == KitAmbientMode.Breathe ? _group : null,
                                      GetComponentInChildren<CanvasRenderer>(true));
        }

        private void OnTransformParentChanged() => _sight?.Refresh(transform);

        private void OnEnable()
        {
            if (!_rect) return;
            _restPosition = _rect.anchoredPosition;
            _restRotation = _rect.localRotation;
            _clock = phase;
            _lastAlpha = -1f;
        }

        private void OnDisable()
        {
            if (!_rect) return;
            _rect.anchoredPosition = _restPosition;
            if (_scaleCaptured) _rect.localScale = _restScale;
            _scaleCaptured = false;
            _rect.localRotation = _restRotation;
            // Hand the group back at its owner's alpha - but only if the last write was ours.
            if (_group && mode == KitAmbientMode.Breathe && _lastAlpha >= 0f
                && Mathf.Abs(_group.alpha - _lastAlpha) < 0.001f)
                _group.alpha = _restAlpha;
        }

        /// <summary>
        /// The alpha a Breathe breathes around: the owner's value, unless the owner has not written
        /// since our last frame. Same rule as KitEntrance.ReconcileAlpha.
        /// </summary>
        public static float OwnerAlpha(float current, float lastWritten, float rest)
            => lastWritten < 0f || Mathf.Abs(current - lastWritten) > 0.001f ? current : rest;

        private void Update()
        {
            if (!_rect) return;
            if (mode == KitAmbientMode.Breathe && _group)
            {
                _restAlpha = OwnerAlpha(_group.alpha, _lastAlpha, _restAlpha);
                _lastAlpha = _group.alpha;
                if (_restAlpha < KitSightline.HiddenAlpha) return;   // its owner hid it
            }
            if (!_sight.Visible) return;
            _clock += Time.unscaledDeltaTime;
            float cycle = _clock / period;
            float wave = Mathf.Sin(cycle * Mathf.PI * 2f);
            switch (mode)
            {
                case KitAmbientMode.Float:
                    _rect.anchoredPosition = _restPosition + Vector2.up * (wave * amplitude * 0.5f);
                    break;
                case KitAmbientMode.Breathe:
                    if (_group)
                        _group.alpha = _lastAlpha = _restAlpha * Mathf.Lerp(Mathf.Clamp01(amplitude), 1f, wave * 0.5f + 0.5f);
                    break;
                case KitAmbientMode.Spin:
                    _rect.localRotation = _restRotation * Quaternion.Euler(0f, 0f, -360f * (cycle % 1f));
                    break;
                case KitAmbientMode.Sway:
                    _rect.localRotation = _restRotation * Quaternion.Euler(0f, 0f, wave * amplitude);
                    break;
                case KitAmbientMode.Pan:
                {
                    if (!_scaleCaptured) { _restScale = _rect.localScale; _scaleCaptured = true; }
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
