using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The rev-3 configure window's cinema mode. The live preview fills the whole window behind the
    /// settings (Docs/MENU_REDESIGN_B.md section 33); while the player is FLYING it, the settings get
    /// out of the way - the step cards, briefing and header fade to nothing and stop taking input -
    /// and a "how to stop" hint with a STOP button (touch has no START / Esc) fades in. Releasing
    /// focus brings them back.
    ///
    /// <para>Reads <see cref="ModePreviewWindow.HasFocus"/> rather than its events: focus is granted
    /// by the preview session, not by the window, and released by several paths; the state is the
    /// one thing all of them agree on. Settles after each fade, so a resting window costs one bool
    /// compare a frame.</para>
    /// </summary>
    public class ConfigureCinema : MonoBehaviour
    {
        [SerializeField, Tooltip("The full-bleed live preview this window flies.")]
        private ModePreviewWindow preview;

        [SerializeField, Tooltip("Everything drawn over the preview: cards, briefing, header. Faded out while flying.")]
        private CanvasGroup chrome;

        [SerializeField, Tooltip("Shown only while flying: how to hand control back (START / Esc, and a STOP button for touch).")]
        private CanvasGroup flyingHint;

        [SerializeField, Min(0.01f), Tooltip("Seconds for the settings to fade out or back in.")]
        private float fadeSeconds = 0.35f;

        float _t;          // 0 = settings shown, 1 = flying
        bool _flying;

        void OnEnable()
        {
            _flying = preview && preview.HasFocus;
            _t = _flying ? 1f : 0f;
            Apply();
        }

        void Update()
        {
            bool flying = preview && preview.HasFocus;
            float target = flying ? 1f : 0f;
            if (flying == _flying && Mathf.Approximately(_t, target)) return;
            _flying = flying;
            _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime / fadeSeconds);
            Apply();
        }

        void Apply()
        {
            float e = _t * _t * (3f - 2f * _t);          // smoothstep
            if (chrome)
            {
                chrome.alpha = 1f - e;
                bool live = !_flying;
                chrome.interactable = live;
                chrome.blocksRaycasts = live;
            }
            if (flyingHint)
            {
                // Pressable only while flying: its STOP button is touch's way back.
                flyingHint.alpha = e;
                flyingHint.interactable = _flying;
                flyingHint.blocksRaycasts = _flying;
            }
        }
    }
}
