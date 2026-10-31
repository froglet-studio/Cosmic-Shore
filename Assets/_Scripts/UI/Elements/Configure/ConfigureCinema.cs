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
    ///
    /// <para><b>As a tray</b> (<see cref="extendAsTray"/>; Shombith, 2026-10-31: "when we press fullscreen it goes actual
    /// full screen rather the play area should extend as a tray and the other parts of the UI (right side) should get
    /// hidden"): FULL SCREEN slides the preview's right edge out by <see cref="trayExtend"/> over the settings column,
    /// which fades out (<see cref="rightSide"/>); the header, the briefing and the fly row stay. The preview renders a
    /// texture that wide from the start and shows its centre (<see cref="ModePreviewWindow"/>'s textureWidthScale), so the
    /// tray reveals more of the arena, never a stretched one.</para>
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

        [Header("Full screen as a tray (Docs/MENU_REDESIGN_B.md section 108)")]
        [SerializeField, Tooltip("FULL SCREEN extends the preview sideways over the settings column instead of filling " +
                                 "the window: only Right Side fades, the rest of the chrome stays.")]
        private bool extendAsTray;
        [SerializeField, Tooltip("How far the preview's right edge moves out (canvas units).")]
        private float trayExtend = 700f;
        [SerializeField, Tooltip("The settings column the tray slides over: faded out as it opens.")]
        private CanvasGroup[] rightSide = { };
        [SerializeField, Tooltip("Parts pinned to the preview's right edge (the objective bar): they move out with it.")]
        private RectTransform[] followTray = { };

        float _hint;      // 0 hidden .. 1 shown
        float _full;      // 0 framed .. 1 full screen
        bool _flying, _wantFull, _captured;
        Vector2 _amin, _amax, _omin, _omax;
        Vector2[] _followBase;
        bool _trayOut;      // the right side has been faded by the tray and is not fully back yet

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
            _followBase = new Vector2[followTray?.Length ?? 0];
            for (int i = 0; i < _followBase.Length; i++)
                if (followTray[i]) _followBase[i] = followTray[i].anchoredPosition;
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
            if (extendAsTray)
            {
                ApplyTray(f);
                return;
            }
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
            ApplyHint();
        }

        void ApplyHint()
        {
            if (!flyingHint) return;
            flyingHint.alpha = _hint;
            flyingHint.interactable = _flying;
            flyingHint.blocksRaycasts = _flying;
        }

        /// <summary>The tray: the preview's right edge moves out, the right side fades, the chrome stays.</summary>
        void ApplyTray(float f)
        {
            if (chrome)
            {
                chrome.alpha = 1f;
                chrome.interactable = !_flying;                  // the pad belongs to the vessel while flying
                chrome.blocksRaycasts = true;
            }
            // The right side is touched only while the tray is out or closing: at rest its groups belong to their own
            // entrances (KitEntrance fades them in when the window opens), which a write of alpha 1 here would cut short.
            if (f > 0f || _trayOut)
            {
                foreach (var group in rightSide)
                {
                    if (!group) continue;
                    group.alpha = 1f - f;
                    group.interactable = f < 0.5f;
                    group.blocksRaycasts = f < 0.5f;
                }
                _trayOut = f > 0f;
            }
            if (previewRect && _captured)
                previewRect.offsetMax = new Vector2(_omax.x + trayExtend * f, _omax.y);
            for (int i = 0; _followBase != null && i < _followBase.Length && i < followTray.Length; i++)
                if (followTray[i]) followTray[i].anchoredPosition = _followBase[i] + new Vector2(trayExtend * f, 0f);
            ApplyHint();
        }
    }
}
