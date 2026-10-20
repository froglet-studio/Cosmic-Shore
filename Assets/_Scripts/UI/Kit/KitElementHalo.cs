using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A soft glow behind an element glyph that breathes in the element's colour and drifts a little
    /// toward white at its peak - the rev-3 "glyphs with glowing animations" (the Hangar's Abilities and
    /// Upgrades rows; the in-match ability row's colours, AbilityLockupStyleSO). The page that owns the
    /// row hands it the colour (<see cref="SetColour"/>); a <see cref="Flash"/> lifts it for a beat when
    /// the ability is fired.
    ///
    /// <para><b>Cheap by construction.</b> It writes one Image colour a frame, and only while it can be
    /// seen (<see cref="KitSightline"/>: a closed tab or a hidden screen costs nothing). Its host page
    /// carries its own nested Canvas, so that write re-batches the page, never Menu_Main's whole
    /// canvas.</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class KitElementHalo : MonoBehaviour
    {
        [SerializeField, Min(0.2f), Tooltip("Seconds per breath.")]
        private float period = 2.6f;
        [SerializeField, Range(0f, 1f), Tooltip("Where in the breath this halo starts (rows are offset so they do not pulse in step).")]
        private float phase;
        [SerializeField, Tooltip("Alpha at the bottom and the top of a breath.")]
        private Vector2 alphaRange = new(0.18f, 0.6f);
        [SerializeField, Range(0f, 1f), Tooltip("How far toward white the colour drifts at the top of a breath.")]
        private float whiteDrift = 0.3f;
        [SerializeField, Min(0.05f), Tooltip("Seconds a Flash takes to fade.")]
        private float flashSeconds = 0.6f;

        Image _image;
        KitSightline _sight;
        Color _colour = Color.white;
        float _flash;
        bool _hasColour;

        void Awake()
        {
            _image = GetComponent<Image>();
            _sight = new KitSightline(transform, null, _image.canvasRenderer);
        }

        /// <summary>The element's colour; clear hides the halo (an open slot).</summary>
        public void SetColour(Color colour)
        {
            _colour = colour;
            _hasColour = colour.a > 0f;
            if (_image && !_hasColour) _image.color = Color.clear;
        }

        /// <summary>Lift the halo to full for a beat (the ability was just fired).</summary>
        public void Flash() => _flash = 1f;

        void Update()
        {
            if (!_hasColour || !_image || !_sight.Visible) return;
            float t = Time.unscaledTime / period + phase;
            float breath = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
            if (_flash > 0f) _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime / flashSeconds);
            float lift = Mathf.Max(breath, _flash);
            var c = Color.Lerp(_colour, Color.white, whiteDrift * lift);
            c.a = Mathf.Lerp(alphaRange.x, alphaRange.y, lift);
            _image.color = c;
        }
    }
}
