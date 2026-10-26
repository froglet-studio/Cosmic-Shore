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
    /// </summary>
    public sealed class IntroCameraOverlay : MonoBehaviour
    {
        RectTransform _top, _bottom;
        Image _flash;
        TextMeshProUGUI _title, _tagline, _objective, _lowerTitle, _lowerLine;
        RectTransform _titleRect, _taglineRect, _objectiveRect, _lowerRect;
        CanvasGroup _group;
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
            _letterbox = style ? style.Letterbox : 0.085f;
            var font = style && style.CaptionFont ? style.CaptionFont : TMP_Settings.defaultFontAsset;

            _flash = MakeImage("Flash", Color.white);
            Stretch(_flash.rectTransform);
            _top = MakeImage("LetterboxTop", Color.black).rectTransform;
            Bar(_top, top: true);
            _bottom = MakeImage("LetterboxBottom", Color.black).rectTransform;
            Bar(_bottom, top: false);

            _title = MakeText("Title", font, style ? style.TitleFontSize : 110f, FontStyles.Bold | FontStyles.UpperCase, TextAlignmentOptions.Center, Color.white);
            _titleRect = Place(_title, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.66f));
            _tagline = MakeText("Tagline", font, style ? style.TaglineFontSize : 32f, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
            _taglineRect = Place(_tagline, new Vector2(0.1f, 0.34f), new Vector2(0.9f, 0.46f));
            _objective = MakeText("Objective", font, style ? style.ObjectiveFontSize : 24f, FontStyles.Bold | FontStyles.UpperCase, TextAlignmentOptions.Right, Color.white);
            _objectiveRect = Place(_objective, new Vector2(0.45f, 0.78f), new Vector2(0.96f, 0.86f));
            _lowerTitle = MakeText("LowerTitle", font, style ? style.LowerTitleFontSize : 44f, FontStyles.Bold | FontStyles.UpperCase, TextAlignmentOptions.BottomLeft, Color.white);
            _lowerRect = Place(_lowerTitle, new Vector2(0.04f, 0.17f), new Vector2(0.6f, 0.27f));
            _lowerLine = MakeText("LowerLine", font, style ? style.LowerLineFontSize : 18f, FontStyles.UpperCase, TextAlignmentOptions.TopLeft, accent);
            _lowerLine.rectTransform.SetParent(_lowerRect, false);
            _lowerLine.rectTransform.anchorMin = new Vector2(0f, -0.45f);
            _lowerLine.rectTransform.anchorMax = new Vector2(1f, 0f);
            _lowerLine.rectTransform.offsetMin = _lowerLine.rectTransform.offsetMax = Vector2.zero;
            _lowerLine.characterSpacing = 12f;

            Clear();
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
            var (label, rect) = slot switch
            {
                IntroCaptionSlot.Title => (_title, _titleRect),
                IntroCaptionSlot.Tagline => (_tagline, _taglineRect),
                _ => (_objective, _objectiveRect),
            };
            Show(label, text, alpha);
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(enter), 3f);
            switch (slot)
            {
                case IntroCaptionSlot.Title: rect.localScale = Vector3.one * (1.18f - 0.18f * e); break;
                case IntroCaptionSlot.Tagline: rect.anchoredPosition = new Vector2(0f, -(1f - e) * 12f); break;
                default: rect.anchoredPosition = new Vector2((1f - e) * 24f, 0f); break;
            }
        }

        public void SetLowerThird(string title, string line, float alpha, float enter)
        {
            Show(_lowerTitle, title, alpha);
            Show(_lowerLine, line, alpha);
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(enter), 3f);
            _lowerRect.anchoredPosition = new Vector2(-(1f - e) * 24f, 0f);
        }

        static void Show(TextMeshProUGUI label, string text, float alpha)
        {
            bool on = !string.IsNullOrEmpty(text) && alpha > 0.001f;
            if (label.enabled != on) label.enabled = on;
            if (!on) return;
            if (label.text != text) label.text = text;
            label.alpha = Mathf.Clamp01(alpha);
        }

        Image MakeImage(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI MakeText(string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.outlineWidth = 0.12f;
            t.outlineColor = new Color32(0, 0, 0, 160);
            return t;
        }

        static RectTransform Place(TextMeshProUGUI t, Vector2 min, Vector2 max)
        {
            var r = t.rectTransform;
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
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
