using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A soft glow behind an element glyph. The page that owns the row hands it the colour (<see cref="SetColour"/>);
    /// a <see cref="Flash"/> lifts it for a beat when the ability is fired - that is the halo's motion.
    /// <para><b>Still by default</b> (2026-10-24, Garrett: decorative motion spends the player's attention): it rests at
    /// the low end of <see cref="alphaRange"/> and moves only on a Flash. <see cref="breathe"/> brings back the rev-3
    /// breathing for a surface that earns it. Elements carry no colour in the UI, so its colour is a neutral one.</para>
    ///
    /// <para><b>Cheap by construction.</b> It writes one Image colour a frame, and only while it can be
    /// seen (<see cref="KitSightline"/>: a closed tab or a hidden screen costs nothing). Its host page
    /// carries its own nested Canvas, so that write re-batches the page, never Menu_Main's whole
    /// canvas.</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class KitElementHalo : MonoBehaviour
    {
        [SerializeField, Tooltip("Breathe while idle. Off (the default) holds the halo still; it lights only on a Flash.")]
        private bool breathe;
        [SerializeField, Min(0.2f), Tooltip("Seconds per breath (with Breathe on).")]
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
        bool _resting;   // still and already drawn at rest: nothing to write until a Flash

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
            _resting = false;
            if (_image && !_hasColour) _image.color = Color.clear;
        }

        /// <summary>Lift the halo to full for a beat (the ability was just fired).</summary>
        public void Flash()
        {
            _flash = 1f;
            _resting = false;
        }

        void Update()
        {
            if (!_hasColour || !_image || _resting || !_sight.Visible) return;
            float breath = 0f;
            if (breathe)
            {
                float t = Time.unscaledTime / period + phase;
                breath = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
            }
            if (_flash > 0f) _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime / flashSeconds);
            else if (!breathe) _resting = true;   // this frame draws the rest state; then no writes until a Flash
            float lift = Mathf.Max(breath, _flash);
            var c = Color.Lerp(_colour, Color.white, whiteDrift * lift);
            c.a = Mathf.Lerp(alphaRange.x, alphaRange.y, lift);
            _image.color = c;
        }
    }
}
