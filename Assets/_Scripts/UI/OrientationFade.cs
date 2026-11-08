using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Turns the phone without a visible break (Shombith, 2026-11-08: "any time a player changes their orientation in the
    /// app shell UI from landscape to portrait there should be a smooth transition with almost no breaking"). A turn swaps
    /// the canvas's reference and every <see cref="PortraitRect"/> pose in one frame, and the PC blocks fitted to the width
    /// settle a frame later, once the canvas rect has its new size. So the canvas's UI goes out the frame the switch
    /// starts (<see cref="AdaptiveCanvasScaler.PortraitLayoutChanging"/>), stays out for <see cref="holdFrames"/> while
    /// the layout settles, then fades back in over <see cref="fadeSeconds"/>, with input held off until it is back. The
    /// world behind the UI (the menu's cell, a vessel in flight) keeps drawing throughout, so the turn reads as the UI
    /// re-forming over a live scene, not as a cut.
    ///
    /// <para>Writes only its own <see cref="CanvasGroup"/> on the root canvas, which nothing else drives; the windows'
    /// own groups and entrances multiply under it untouched. Authored by Tools/Build/author_menu_portrait.py (Menu_Main
    /// and GameCanvas).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AdaptiveCanvasScaler))]
    [AddComponentMenu("Cosmic Shore/UI/Orientation Fade")]
    public class OrientationFade : MonoBehaviour
    {
        [Tooltip("The root canvas's own CanvasGroup, faded on a turn. Nothing else may drive it.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("Frames the UI stays hidden after a turn while the layout settles (the PC blocks fitted to the width " +
                 "re-fit the frame after the canvas takes its new size).")]
        [SerializeField, Min(0)] private int holdFrames = 2;

        [Tooltip("Seconds the UI takes to fade back in once the layout has settled.")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.28f;

        [Tooltip("The fade's curve.")]
        [SerializeField] private KitEase ease = KitEase.OutCubic;

        private AdaptiveCanvasScaler _scaler;
        private int _hold = -1;      // frames left hidden; -1 at rest
        private float _clock = -1f;  // seconds into the fade; -1 at rest

        /// <summary>Whether a turn is being smoothed right now.</summary>
        public bool Fading => _hold >= 0 || _clock >= 0f;

        /// <summary>The UI's alpha <paramref name="elapsed"/> seconds into a fade of <paramref name="seconds"/>.</summary>
        public static float AlphaAt(float elapsed, float seconds, KitEase ease) =>
            seconds <= 0f ? 1f : KitMotion.Ease(ease, Mathf.Clamp01(elapsed / seconds));

        private void Awake() => TryGetComponent(out _scaler);

        private void OnEnable()
        {
            if (_scaler) _scaler.PortraitLayoutChanging += OnTurning;
        }

        private void OnDisable()
        {
            if (_scaler) _scaler.PortraitLayoutChanging -= OnTurning;
            Settle();
        }

        private void OnTurning(bool upright)
        {
            if (!group) return;
            _hold = holdFrames;
            _clock = -1f;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private void LateUpdate()
        {
            if (!Fading || !group) return;
            if (_hold >= 0)
            {
                if (_hold-- > 0) return;
                _clock = 0f;
            }
            _clock += Time.unscaledDeltaTime;
            float a = AlphaAt(_clock, fadeSeconds, ease);
            group.alpha = a;
            if (a >= 1f) Settle();
        }

        private void Settle()
        {
            _hold = -1;
            _clock = -1f;
            if (!group) return;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        /// <summary>Wire the fade from code (tests and editor tooling).</summary>
        public void SetGroup(CanvasGroup canvasGroup, int frames = 2, float seconds = 0.28f)
        {
            group = canvasGroup; holdFrames = frames; fadeSeconds = seconds;
        }
    }
}
