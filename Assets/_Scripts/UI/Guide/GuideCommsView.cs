using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The captain's comms window on a guided path (Rev 36 B, Docs/MENU_REDESIGN_B.md section 102.1): a tall portrait window
    /// docks on the screen edge AWAY from the one lit control, her voice bars run while her line types, the prompt (the
    /// keycap and the action) comes in when the line is done, and a lime line runs from the window to the control.
    ///
    /// <para><see cref="MenuSpotlight"/> owns it: it instantiates the prefab under its own overlay and calls
    /// <see cref="Present"/> and <see cref="Place"/> every frame with the step's words and the cut-out. A new line replays
    /// the entrance; the same line only re-places. It never presses anything (the guided-path rule): every graphic is
    /// raycast-off, so a press goes to the dim, which passes it only to the lit control.</para>
    ///
    /// <para>Built by <c>Tools/Build/author_guide_comms.py</c>, at the 1080 reference size; the spotlight's overlay is in
    /// pixels, so the view scales itself by the screen height.</para>
    /// </summary>
    public class GuideCommsView : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("The window (everything that slides in). Its size is the window's size at 1080.")]
        [SerializeField] RectTransform window;
        [Tooltip("The speaker's portrait, in the window's top band.")]
        [SerializeField] Image portrait;
        [SerializeField] TMP_Text eyebrowText;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text titleText;
        [Tooltip("The line, revealed a character at a time.")]
        [SerializeField] TMP_Text lineText;
        [Tooltip("The prompt row (keycap and action); it fades in once the line is typed.")]
        [SerializeField] CanvasGroup prompt;
        [SerializeField] TMP_Text keycapText;
        [SerializeField] TMP_Text actionText;
        [Tooltip("The voice bars: they move while the line types and rest after.")]
        [SerializeField] RectTransform[] voiceBars = { };
        [Tooltip("The lime line from the window to the lit control. Pivot at its left end.")]
        [SerializeField] RectTransform pointer;

        [Header("Placement (1080 reference pixels)")]
        [Tooltip("Gap between the window and the screen edge it docks on.")]
        [SerializeField] float edgeMargin = 40f;
        [Tooltip("Height of the window's bottom above the screen's bottom edge.")]
        [SerializeField] float bottomMargin = 120f;
        [Tooltip("Height of the window's top below the screen's top edge, when it must sit high to clear the control.")]
        [SerializeField] float topMargin = 110f;

        GuideSpeakerSO _speaker;
        string _line;
        float _time;
        bool _dockLeft;

        /// <summary>Show <paramref name="line"/> in <paramref name="speaker"/>'s voice. A changed line replays the entrance.</summary>
        public void Present(GuideSpeakerSO speaker, string line, string action, string keycap)
        {
            if (speaker != _speaker)
            {
                _speaker = speaker;
                if (portrait) { portrait.sprite = speaker.portrait; portrait.enabled = speaker.portrait; }
                SetText(eyebrowText, speaker.eyebrow);
                SetText(nameText, speaker.speakerName);
                SetText(titleText, speaker.speakerTitle);
                _line = null;
            }
            if (line != _line)
            {
                _line = line;
                _time = 0f;
                SetText(lineText, line);
            }
            SetText(actionText, action);
            SetText(keycapText, keycap);
            if (keycapText) keycapText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(keycap) && KitInputDevice.ShowsControlHints);
        }

        /// <summary>
        /// Dock the window on the side away from <paramref name="hole"/> (the cut-out, in the overlay's pixels) and run the
        /// pointer to it. <paramref name="scale"/> is the overlay's height over 1080.
        /// </summary>
        public void Place(Rect hole, Rect bounds, float scale, float deltaTime)
        {
            if (!window || _speaker == null) return;
            _time += deltaTime;
            var rt = (RectTransform)transform;
            rt.localScale = new Vector3(scale, scale, 1f);

            var size = window.rect.size;
            float w = size.x * scale, h = size.y * scale;
            bool left = hole.center.x > bounds.center.x;
            if (_time <= deltaTime) _dockLeft = left;           // a new line picks its side; it does not hop mid-line
            left = _dockLeft;

            float x = left ? bounds.xMin + edgeMargin * scale : bounds.xMax - edgeMargin * scale - w;
            float y = bounds.yMin + bottomMargin * scale;
            var rest = new Rect(x, y, w, h);
            if (rest.Overlaps(Inflate(hole, 24f * scale)))
                rest.y = bounds.yMax - topMargin * scale - h;

            // Dock in from the edge (eased out), then rest.
            float slide = _speaker.slideSeconds > 0f ? Mathf.Clamp01(_time / _speaker.slideSeconds) : 1f;
            float ease = 1f - Mathf.Pow(1f - slide, 3f);
            float off = (1f - ease) * (w + edgeMargin * scale + 20f) * (left ? -1f : 1f);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(rest.x + off, rest.y);

            // The line types once the window has docked; the voice runs while it types.
            float typeStart = _speaker.slideSeconds * 0.8f;
            int total = string.IsNullOrEmpty(_line) ? 0 : _line.Length;
            int shown = Mathf.Clamp(Mathf.FloorToInt((_time - typeStart) * _speaker.charactersPerSecond), 0, total);
            if (lineText && lineText.maxVisibleCharacters != shown) lineText.maxVisibleCharacters = shown;
            bool talking = shown < total && _time > typeStart;
            for (int i = 0; i < voiceBars.Length; i++)
            {
                if (!voiceBars[i]) continue;
                float k = talking ? 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(_time * (7f + (i % 4) * 1.7f) + i * 0.9f)) : 0.25f;
                voiceBars[i].localScale = new Vector3(1f, k, 1f);
            }
            if (prompt) prompt.alpha = shown >= total ? Mathf.MoveTowards(prompt.alpha, 1f, deltaTime / 0.3f) : 0f;

            PlacePointer(new Rect(rest.x, rest.y, w, h), hole, scale, ease);
        }

        void PlacePointer(Rect win, Rect hole, float scale, float ease)
        {
            if (!pointer) return;
            bool left = _dockLeft;
            var from = new Vector2(left ? win.xMax : win.xMin, win.yMin + 60f * scale);
            var to = new Vector2(left ? hole.xMin : hole.xMax, hole.center.y);
            if (left ? to.x <= from.x : to.x >= from.x) to.x = hole.center.x;   // the control is beside the window
            var d = to - from;
            float length = d.magnitude;
            var rt = (RectTransform)transform;
            // The pointer is a child of this (scaled) root: work in its local units.
            pointer.anchorMin = pointer.anchorMax = Vector2.zero;
            pointer.pivot = new Vector2(0f, 0.5f);
            pointer.anchoredPosition = (from - rt.anchoredPosition) / scale;
            pointer.sizeDelta = new Vector2(length / scale * ease, 2f);
            pointer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        static Rect Inflate(Rect r, float by) => new(r.xMin - by, r.yMin - by, r.width + 2f * by, r.height + 2f * by);

        static void SetText(TMP_Text t, string s)
        {
            if (!t) return;
            if (t.text != s) t.text = s ?? string.Empty;
            bool show = !string.IsNullOrEmpty(s);
            if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
        }
    }
}
