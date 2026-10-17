using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The configure window's flight states (Docs/MENU_REDESIGN_B.md section 39). The player flies IN the
    /// framed preview: the settings stay where they are, and "START or Esc to stop flying" with STOP and
    /// FULL SCREEN appears. FULL SCREEN is the option (the owner, 2026-10-17: "you should be just able to
    /// fly in the little window, not go full screen - there should be an option"): the frame grows to
    /// the whole window and the settings fade out; pressing it again, or handing the stick back,
    /// returns the frame.
    ///
    /// <para>Reads <see cref="ModePreviewWindow.HasFocus"/> rather than its events: focus is granted by
    /// the preview session and released by several paths; the state is what they all agree on. Settles
    /// after each move, so a resting window costs a bool compare a frame.</para>
    ///
    /// <para>Full screen scales the preview's texture, which is sized to the frame when the preview
    /// starts (a live texture is never swapped), so it reads softer than the frame.</para>
    /// </summary>
    public class ConfigureCinema : MonoBehaviour
    {
        [SerializeField, Tooltip("The framed live preview this window flies.")]
        private ModePreviewWindow preview;

        [SerializeField, Tooltip("The preview's frame: grows to the window in full screen.")]
        private RectTransform previewRect;

        [SerializeField, Tooltip("Everything around the preview: cards, briefing, header. Faded out in full screen only.")]
        private CanvasGroup chrome;

        [SerializeField, Tooltip("Shown only while flying: how to hand control back, STOP and FULL SCREEN.")]
        private CanvasGroup flyingHint;

        [SerializeField, Min(0.01f), Tooltip("Seconds for full screen to open or close.")]
        private float fadeSeconds = 0.35f;

        [Header("Full screen")]
        [SerializeField, Tooltip("The frame's anchored offsets in full screen (under the nav).")]
        private Vector2 fullOffsetMin = new(0f, 0f);
        [SerializeField] private Vector2 fullOffsetMax = new(0f, -84f);

        float _hint;      // 0 hidden .. 1 shown
        float _full;      // 0 framed .. 1 full screen
        bool _flying, _wantFull, _captured;
        Vector2 _amin, _amax, _omin, _omax;

        void OnEnable()
        {
            Capture();
            _flying = preview && preview.HasFocus;
            _wantFull = false;
            _hint = _flying ? 1f : 0f;
            _full = 0f;
            Apply();
        }

        void Capture()
        {
            if (_captured || !previewRect) return;
            _captured = true;
            _amin = previewRect.anchorMin; _amax = previewRect.anchorMax;
            _omin = previewRect.offsetMin; _omax = previewRect.offsetMax;
        }

        /// <summary>FULL SCREEN: the frame grows to the window (or back). Only while flying.</summary>
        public void ToggleFullScreen()
        {
            if (!_flying) return;
            _wantFull = !_wantFull;
        }

        void Update()
        {
            bool flying = preview && preview.HasFocus;
            if (!flying) _wantFull = false;             // handing the stick back returns the frame
            float hintTarget = flying ? 1f : 0f, fullTarget = _wantFull ? 1f : 0f;
            if (flying == _flying && Mathf.Approximately(_hint, hintTarget) && Mathf.Approximately(_full, fullTarget)) return;
            _flying = flying;
            float step = Time.unscaledDeltaTime / fadeSeconds;
            _hint = Mathf.MoveTowards(_hint, hintTarget, step);
            _full = Mathf.MoveTowards(_full, fullTarget, step);
            Apply();
        }

        void Apply()
        {
            float f = _full * _full * (3f - 2f * _full);       // smoothstep
            if (chrome)
            {
                chrome.alpha = 1f - f;
                bool live = f < 0.5f;
                chrome.interactable = live && !_flying;          // the pad belongs to the vessel while flying
                chrome.blocksRaycasts = live;
            }
            if (previewRect && _captured)
            {
                previewRect.anchorMin = Vector2.Lerp(_amin, Vector2.zero, f);
                previewRect.anchorMax = Vector2.Lerp(_amax, Vector2.one, f);
                previewRect.offsetMin = Vector2.Lerp(_omin, fullOffsetMin, f);
                previewRect.offsetMax = Vector2.Lerp(_omax, fullOffsetMax, f);
            }
            if (flyingHint)
            {
                flyingHint.alpha = _hint;
                flyingHint.interactable = _flying;
                flyingHint.blocksRaycasts = _flying;
            }
        }
    }
}
