using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The microgame coach: one panel along the bottom of the preview window showing what the
    /// <see cref="DrillRunner"/> is saying (Docs/ModePreview/TRAINING_PLAN.md §4.5).
    ///
    /// <para><b>Generated, not authored.</b> The preview window lives inside the arcade modal's
    /// prefab and the Maelstrom hub's scene rect alike; a coach that had to be placed in both is
    /// a coach one of them would be missing. It is built under the window's own RectTransform on
    /// first use and draws nothing it was not told - every word comes from the runner, which
    /// reads it from <c>DrillLibrary.asset</c>. Font and size are taken from the window's own
    /// status label so the coach reads as part of the same panel.</para>
    ///
    /// <para><b>Event-driven</b>: it redraws on <see cref="DrillRunner.OnChanged"/> and has no
    /// Update. Raycasts are off everywhere except the two buttons, so the panel never eats the
    /// tap that gives the window focus.</para>
    /// </summary>
    public sealed class DrillCoachView : MonoBehaviour
    {
        const float PanelHeightFraction = 0.24f;
        const float PipSize = 10f;

        DrillRunner _runner;
        RectTransform _panel;
        TMP_Text _title;
        TMP_Text _line;
        TMP_Text _chipLabel;
        Image _chipGlyph;
        RectTransform _pips;
        Button _skip;
        TMP_Text _skipLabel;
        Button _next;
        TMP_Text _nextLabel;
        CanvasGroup _group;

        /// <summary>Attach to a preview window's rect, creating the view the first time.</summary>
        public static DrillCoachView Ensure(RectTransform host)
        {
            if (!host) return null;
            var view = host.GetComponentInChildren<DrillCoachView>(true);
            if (view) return view;

            var go = new GameObject("DrillCoach", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(host, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            view = go.AddComponent<DrillCoachView>();
            view.Build(host);
            return view;
        }

        /// <summary>Show this runner's state. Null hides the coach.</summary>
        public void Bind(DrillRunner runner)
        {
            if (_runner == runner) { Redraw(); return; }
            if (_runner) _runner.OnChanged -= Redraw;
            _runner = runner;
            if (_runner) _runner.OnChanged += Redraw;
            Redraw();
        }

        void OnDestroy()
        {
            if (_runner) _runner.OnChanged -= Redraw;
        }

        // ── Drawing ──────────────────────────────────────────────────────

        void Redraw()
        {
            if (!_panel) return;

            bool visible = _runner && _runner.Phase != DrillPhase.Idle &&
                           (!string.IsNullOrEmpty(_runner.Line) || _runner.Phase == DrillPhase.Lesson);
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            if (!visible) return;

            var strings = _runner.Library ? _runner.Library.Strings : null;
            _title.text = _runner.Title;
            _line.text = _runner.Line;

            _skip.gameObject.SetActive(_runner.SkipAvailable && !string.IsNullOrEmpty(strings?.SkipLabel));
            _skipLabel.text = strings?.SkipLabel ?? string.Empty;
            _next.gameObject.SetActive(_runner.NextAvailable && !string.IsNullOrEmpty(strings?.NextLabel));
            _nextLabel.text = strings?.NextLabel ?? string.Empty;

            DrawChip();
            DrawPips();
        }

        /// <summary>
        /// The control chip, drawn the way the ability lockup draws it: the pad's artwork from the
        /// fleet's one glyph set, the keyboard's label from the same row. Blank when the line is
        /// not about an ability or the control has no art - honest, never a guessed glyph.
        /// </summary>
        void DrawChip()
        {
            Sprite sprite = null;
            string label = null;
            var element = _runner.LineElement;
            if (element != Element.None && _runner.Facts != null && _runner.Facts.AbilityHasInput(element))
            {
                if (_runner.Keyboard)
                    _runner.Facts.TryAbilityControlLabel(element, out label);
                else
                    sprite = PadGlyph(element);
            }

            _chipGlyph.sprite = sprite;
            _chipGlyph.enabled = sprite;
            _chipLabel.text = label ?? string.Empty;
            _chipLabel.enabled = !string.IsNullOrEmpty(label);
        }

        Sprite PadGlyph(Element element)
        {
            var glyphs = Resources.Load<ControlGlyphSetSO>("ControlGlyphSet");
            var map = _runner.Facts != null ? ElementalAbilityMapSO.LoadFor(_runner.Facts.Vessel) : null;
            var entry = map ? map.GetEntry(element) : null;
            if (!glyphs || entry == null) return null;
            var binding = InputHintBindingMap.BindingFor(entry.Input, keyboard: false);
            return glyphs.For(binding)?.padGlyph;
        }

        void DrawPips()
        {
            bool lesson = _runner.Phase == DrillPhase.Lesson;
            int count = lesson ? _runner.StepCount : 0;

            while (_pips.childCount < count) MakePip();
            for (int i = 0; i < _pips.childCount; i++)
            {
                var pip = _pips.GetChild(i);
                pip.gameObject.SetActive(i < count);
                if (i >= count) continue;
                var image = pip.GetComponent<Image>();
                image.color = i < _runner.StepIndex ? new Color(1f, 1f, 1f, 0.9f)
                            : i == _runner.StepIndex ? new Color(1f, 1f, 1f, 0.6f)
                            : new Color(1f, 1f, 1f, 0.2f);
            }
        }

        // ── Building ─────────────────────────────────────────────────────

        void Build(RectTransform host)
        {
            var reference = host.GetComponentInChildren<TMP_Text>(true);
            var font = reference ? reference.font : TMP_Settings.defaultFontAsset;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = true;

            _panel = MakeRect("Panel", transform);
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = new Vector2(1f, PanelHeightFraction);
            _panel.offsetMin = _panel.offsetMax = Vector2.zero;
            var bg = _panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            _title = MakeText("Title", _panel, font, 14f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.TopLeft);
            Place(_title.rectTransform, new Vector2(0.03f, 0.66f), new Vector2(0.7f, 0.96f));

            _line = MakeText("Line", _panel, font, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
            _line.enableAutoSizing = true;
            _line.fontSizeMin = 12f;
            _line.fontSizeMax = 24f;
            Place(_line.rectTransform, new Vector2(0.14f, 0.08f), new Vector2(0.78f, 0.68f));

            var chip = MakeRect("Chip", _panel);
            Place(chip, new Vector2(0.03f, 0.12f), new Vector2(0.12f, 0.64f));
            _chipGlyph = chip.gameObject.AddComponent<Image>();
            _chipGlyph.preserveAspect = true;
            _chipGlyph.raycastTarget = false;
            _chipGlyph.enabled = false;
            _chipLabel = MakeText("ChipLabel", chip, font, 16f, Color.white, TextAlignmentOptions.Center);
            Place(_chipLabel.rectTransform, Vector2.zero, Vector2.one);

            _pips = MakeRect("Pips", _panel);
            Place(_pips, new Vector2(0.7f, 0.72f), new Vector2(0.97f, 0.94f));
            var layout = _pips.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            (_skip, _skipLabel) = MakeButton("Skip", font, new Vector2(0.8f, 0.12f), new Vector2(0.97f, 0.62f));
            _skip.onClick.AddListener(() => { if (_runner) _runner.Skip(); });
            (_next, _nextLabel) = MakeButton("Next", font, new Vector2(0.8f, 0.12f), new Vector2(0.97f, 0.62f));
            _next.onClick.AddListener(() => { if (_runner) _runner.NextTip(); });
        }

        (Button, TMP_Text) MakeButton(string name, TMP_FontAsset font, Vector2 min, Vector2 max)
        {
            var rect = MakeRect(name, _panel);
            Place(rect, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.15f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = MakeText("Label", rect, font, 16f, Color.white, TextAlignmentOptions.Center);
            Place(label.rectTransform, Vector2.zero, Vector2.one);
            rect.gameObject.SetActive(false);
            return (button, label);
        }

        void MakePip()
        {
            var rect = MakeRect("Pip", _pips);
            rect.sizeDelta = new Vector2(PipSize, PipSize);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
        }

        static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font, float size, Color color,
                                 TextAlignmentOptions alignment)
        {
            var rect = MakeRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        static void Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
