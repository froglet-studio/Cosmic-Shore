using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The intro's screen layer: letterbox bars, a white flash, and the mode's title, tagline,
    /// objective tag and lower third. Built at runtime under the HUD canvas, just below the Skip
    /// button so Skip stays on top. Draws only what <see cref="PreGameCinematicController"/> sets
    /// each frame; every word comes from <see cref="IntroModeWords"/>, never from code.
    ///
    /// <para>Laid out as the Rev 33 cell-gate board draws it (Docs/MENU_REDESIGN_B.md section 99, board A): the
    /// title, tagline and objective chip on one column 128 units in from the left, low on the frame, the title
    /// rising out of its own mask; the lower third a framed panel on the same margin at the right. Positions are in
    /// the HUD canvas's 1920 x 1080 reference units.</para>
    /// </summary>
    public sealed class IntroCameraOverlay : MonoBehaviour
    {
        // Board A's geometry (reference units from the bottom-left corner).
        const float Column = 128f;
        const float TitleBottom = 306f, TitleHeight = 128f, TitleWidth = 1100f;
        const float TaglineBottom = 262f, TaglineHeight = 34f;
        const float ObjectiveBottom = 198f, ObjectiveHeight = 44f;
        const float LowerBottom = 200f, LowerHeight = 102f, LowerWidth = 560f;
        const float TitleSpacing = 5f, ObjectiveSpacing = 14f, LowerTitleSpacing = 6f, LowerLineSpacing = 16f;

        static readonly Color BarColour = new(0f, 0.008f, 0.059f, 1f);
        static readonly Color FrameColour = new(0.169f, 0.196f, 0.4f, 1f);
        static readonly Color GlassColour = new(0.012f, 0.027f, 0.133f, 0.95f);
        static readonly Color TaglineColour = new(0.863f, 0.878f, 0.98f, 1f);

        RectTransform _top, _bottom;
        Image _flash, _lowerBar;
        TextMeshProUGUI _title, _tagline, _objective, _lowerTitle, _lowerLine;
        RectTransform _titleRect, _taglineRect, _objectiveRect, _lowerRect;
        CanvasGroup _group, _objectiveGroup, _lowerGroup;
        float _letterbox;

        public static IntroCameraOverlay Create(Transform parent, int siblingIndex, IntroCameraLibrarySO style, Color accent)
        {
            var go = new GameObject("IntroCameraOverlay", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (siblingIndex >= 0) go.transform.SetSiblingIndex(siblingIndex);
            Stretch((RectTransform)go.transform);
            var overlay = go.AddComponent<IntroCameraOverlay>();
            overlay.Build(style, accent);
            return overlay;
        }

        void Build(IntroCameraLibrarySO style, Color accent)
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _letterbox = style ? style.Letterbox : 0.1222f;
            var font = style && style.CaptionFont ? style.CaptionFont : TMP_Settings.defaultFontAsset;
            var detail = style && style.DetailFont ? style.DetailFont : font;

            _flash = MakeImage(transform, "Flash", Color.white);
            Stretch(_flash.rectTransform);
            _top = MakeImage(transform, "LetterboxTop", BarColour).rectTransform;
            Bar(_top, top: true);
            _bottom = MakeImage(transform, "LetterboxBottom", BarColour).rectTransform;
            Bar(_bottom, top: false);

            // The title rises out of its own box, so it never slides over the frame below it.
            var titleClip = Node(transform, "TitleClip");
            Place(titleClip, new Vector2(0f, 0f), new Vector2(Column - 6f, TitleBottom), new Vector2(TitleWidth, TitleHeight));
            titleClip.gameObject.AddComponent<RectMask2D>();
            _title = MakeText(titleClip, "Title", font, style ? style.TitleFontSize : 118f, FontStyles.UpperCase,
                              TextAlignmentOptions.Left, Color.white);
            _title.characterSpacing = TitleSpacing;
            FitLine(_title, 56f);
            _titleRect = _title.rectTransform;
            Stretch(_titleRect);

            _tagline = MakeText(transform, "Tagline", detail, style ? style.TaglineFontSize : 26f, FontStyles.Normal,
                                TextAlignmentOptions.TopLeft, TaglineColour);
            _taglineRect = _tagline.rectTransform;
            Place(_taglineRect, Vector2.zero, new Vector2(Column, TaglineBottom), new Vector2(TitleWidth, TaglineHeight));

            // The objective in its chip: a frame, the glass inside it, the words; as wide as the words.
            var chip = MakeImage(transform, "Objective", FrameColour);
            _objectiveRect = chip.rectTransform;
            Place(_objectiveRect, Vector2.zero, new Vector2(Column, ObjectiveBottom), new Vector2(0f, ObjectiveHeight));
            Row(chip.gameObject, new RectOffset(1, 1, 1, 1));
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _objectiveGroup = chip.gameObject.AddComponent<CanvasGroup>();
            var glass = MakeImage(chip.transform, "Glass", GlassColour);
            Row(glass.gameObject, new RectOffset(18, 22, 0, 0));
            _objective = MakeText(glass.transform, "Text", detail, style ? style.ObjectiveFontSize : 15f, FontStyles.UpperCase,
                                  TextAlignmentOptions.Left, Color.white);
            _objective.characterSpacing = ObjectiveSpacing;
            _objective.textWrappingMode = TextWrappingModes.NoWrap;
            _objective.rectTransform.sizeDelta = new Vector2(0f, ObjectiveHeight - 2f);

            // The lower third: a framed panel at the right margin, a bar in the pilot's colour on its left edge.
            var lower = MakeImage(transform, "LowerThird", FrameColour);
            _lowerRect = lower.rectTransform;
            Place(_lowerRect, new Vector2(1f, 0f), new Vector2(-Column, LowerBottom), new Vector2(LowerWidth, LowerHeight));
            _lowerGroup = lower.gameObject.AddComponent<CanvasGroup>();
            var lowerGlass = MakeImage(lower.transform, "Glass", GlassColour);
            Inset(lowerGlass.rectTransform, 1f);
            _lowerBar = MakeImage(lower.transform, "Bar", accent);
            var bar = _lowerBar.rectTransform;
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.offsetMin = new Vector2(0f, 16f);
            bar.offsetMax = new Vector2(3f, -16f);
            _lowerTitle = MakeText(lower.transform, "LowerTitle", font, style ? style.LowerTitleFontSize : 34f,
                                   FontStyles.UpperCase, TextAlignmentOptions.BottomLeft, Color.white);
            _lowerTitle.characterSpacing = LowerTitleSpacing;
            FitLine(_lowerTitle, 20f);
            Band(_lowerTitle.rectTransform, 0.46f, 0.86f);
            _lowerLine = MakeText(lower.transform, "LowerLine", detail, style ? style.LowerLineFontSize : 13f,
                                  FontStyles.UpperCase, TextAlignmentOptions.TopLeft, accent);
            _lowerLine.characterSpacing = LowerLineSpacing;
            Band(_lowerLine.rectTransform, 0.14f, 0.4f);

            Clear();
        }

        /// <summary>The local pilot's colour on the lower third's line and bar. The overlay is kept between
        /// matches, and the next match's pilot may fly another domain.</summary>
        public void SetAccent(Color accent)
        {
            if (!_lowerLine) return;
            var line = accent;
            line.a = _lowerLine.color.a;
            _lowerLine.color = line;
            if (_lowerBar) _lowerBar.color = accent;
        }

        /// <summary>Hides every element; the bars and flash go to zero.</summary>
        public void Clear()
        {
            SetLetterbox(0f);
            SetFlash(0f);
            SetCaption(IntroCaptionSlot.Title, null, 0f, 1f);
            SetCaption(IntroCaptionSlot.Tagline, null, 0f, 1f);
            SetCaption(IntroCaptionSlot.Objective, null, 0f, 1f);
            SetLowerThird(null, null, 0f, 1f);
        }

        /// <param name="amount">0 = no bars, 1 = full letterbox.</param>
        public void SetLetterbox(float amount)
        {
            float h = Mathf.Clamp01(amount) * _letterbox;
            _top.anchorMin = new Vector2(0f, 1f - h);
            _bottom.anchorMax = new Vector2(1f, h);
        }

        public void SetFlash(float alpha) => _flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

        /// <param name="enter">0 to 1, how far its entrance animation has run.</param>
        public void SetCaption(IntroCaptionSlot slot, string text, float alpha, float enter)
        {
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(enter), 3f);
            switch (slot)
            {
                case IntroCaptionSlot.Title:
                    Show(_title, text, alpha);
                    _titleRect.anchoredPosition = new Vector2(0f, -(1f - e) * TitleHeight * 1.05f);
                    break;
                case IntroCaptionSlot.Tagline:
                    Show(_tagline, text, alpha);
                    _taglineRect.anchoredPosition = new Vector2(Column, TaglineBottom - (1f - e) * 12f);
                    break;
                default:
                    bool on = Show(_objective, text, alpha);
                    if (_objectiveGroup.gameObject.activeSelf != on) _objectiveGroup.gameObject.SetActive(on);
                    _objectiveGroup.alpha = Mathf.Clamp01(alpha);
                    _objectiveRect.anchoredPosition = new Vector2(Column, ObjectiveBottom - (1f - e) * 14f);
                    break;
            }
        }

        public void SetLowerThird(string title, string line, float alpha, float enter)
        {
            bool on = Show(_lowerTitle, title, alpha) | Show(_lowerLine, line, alpha);
            if (_lowerGroup.gameObject.activeSelf != on) _lowerGroup.gameObject.SetActive(on);
            _lowerGroup.alpha = Mathf.Clamp01(alpha);
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(enter), 3f);
            _lowerRect.anchoredPosition = new Vector2(-Column + (1f - e) * 40f, LowerBottom);
        }

        static bool Show(TextMeshProUGUI label, string text, float alpha)
        {
            bool on = !string.IsNullOrEmpty(text) && alpha > 0.001f;
            if (label.enabled != on) label.enabled = on;
            if (!on) return false;
            if (label.text != text) label.text = text;
            var c = label.color;
            c.a = Mathf.Clamp01(alpha);
            label.color = c;
            return true;
        }

        static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            var img = Node(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style,
                                        TextAlignmentOptions align, Color color)
        {
            var t = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        // A rect placed by its bottom-left (anchor x 0) or bottom-right (anchor x 1) corner, in reference units.
        static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        // A horizontal layout at its children's widths (the chip hugs its words; only its root carries the fitter).
        static void Row(GameObject go, RectOffset padding)
        {
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = padding;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
        }

        // One line that shrinks to fit its box rather than wrapping out of it (a long mode name).
        static void FitLine(TextMeshProUGUI t, float minSize)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = Mathf.Min(minSize, t.fontSize);
        }

        static void Inset(RectTransform r, float by)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(by, by);
            r.offsetMax = new Vector2(-by, -by);
        }

        // A full-width band of the lower third, between two heights (0-1), clear of the bar.
        static void Band(RectTransform r, float from, float to)
        {
            r.anchorMin = new Vector2(0f, from);
            r.anchorMax = new Vector2(1f, to);
            r.offsetMin = new Vector2(24f, 0f);
            r.offsetMax = new Vector2(-24f, 0f);
        }

        static void Bar(RectTransform r, bool top)
        {
            r.anchorMin = top ? new Vector2(0f, 1f) : Vector2.zero;
            r.anchorMax = top ? Vector2.one : new Vector2(1f, 0f);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
