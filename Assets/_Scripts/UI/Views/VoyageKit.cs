using System;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The small builders every part of the Voyage window is made with (<see cref="VoyageBoard"/>,
    /// <see cref="VoyageSquadDeck"/>, <see cref="VoyageSquadColumn"/>, <see cref="VoyageSlabPage"/>): a node, a slivered
    /// plate, a soft-ended band, a line of text in the window's face, a label in the style's label face, a keyed button,
    /// an element's flower. One look: every sprite, colour and face is
    /// <see cref="VoyageBoardStyleSO"/>'s, and every colour is read as the worn colour set has it
    /// (ColorTrace, Docs/VesselModels/COLOR_TRACE.md).
    /// </summary>
    public sealed class VoyageKit
    {
        public readonly VoyageBoardStyleSO Style;
        public readonly VoyageLines Lines;
        readonly TMP_Text _font;

        public static readonly Color Clear = new(0f, 0f, 0f, 0f);
        static readonly Color DisabledAuthored = new(0.55f, 0.55f, 0.6f, 0.6f);

        public VoyageKit(VoyageBoardStyleSO style, VoyageLines lines, TMP_Text font)
        {
            Style = style;
            Lines = lines;
            _font = font;
        }

        public Color Light => ColorTrace.Retrace(ColorTrace.Ui, Style.Light);
        public Color Sub => ColorTrace.Retrace(ColorTrace.Ui, Style.Sub);
        public Color Jade => ColorTrace.Retrace(ColorTrace.Ui, Style.Jade);
        public Color Cyan => ColorTrace.Retrace(ColorTrace.Ui, Style.Cyan);
        public Color Outline => ColorTrace.Retrace(ColorTrace.Ui, Style.Outline);
        public Color Popup => ColorTrace.Retrace(ColorTrace.Ui, Style.Popup);
        public Color RowPicked => ColorTrace.Retrace(ColorTrace.Ui, Style.RowPicked);
        public Color CtaFill => ColorTrace.Retrace(ColorTrace.Ui, Style.CtaFill);
        public Color CtaText => ColorTrace.Retrace(ColorTrace.Ui, Style.CtaText);
        public Color Disabled => ColorTrace.Retrace(ColorTrace.Ui, DisabledAuthored);

        /// <summary><paramref name="c"/> at <paramref name="alpha"/> of its own opacity.</summary>
        public static Color Fade(Color c, float alpha) => new(c.r, c.g, c.b, c.a * alpha);

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>A full-width band <paramref name="height"/> tall, <paramref name="y"/> below its parent's top.</summary>
        public static void Top(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -y);
        }

        /// <summary>A full-width band <paramref name="height"/> tall, <paramref name="y"/> above its parent's foot.</summary>
        public static void Foot(RectTransform rt, float y, float height)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        public static LayoutElement Le(Component c) =>
            c.TryGetComponent<LayoutElement>(out var le) ? le : c.gameObject.AddComponent<LayoutElement>();

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleLeft,
            bool expandWidth = false)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = false;
            return h;
        }

        public static Image Sliced(RectTransform rt, Sprite sprite, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A plain coloured block (a bar, a rule, a hit area when clear).</summary>
        public static Image Block(RectTransform rt, Color color, bool hit = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = hit;
            return img;
        }

        /// <summary>A fill solid in the middle and fading to both ends, each <paramref name="soft"/> of its width
        /// (<see cref="VoyageSoftFill"/>): a button, the band behind a group, a hairline.</summary>
        public static VoyageSoftFill Band(RectTransform rt, Color color, float soft = 0.2f, bool hit = false)
        {
            var fill = rt.gameObject.AddComponent<VoyageSoftFill>();
            fill.Set(VoyageSoftFill.Shape.Band, soft);
            fill.color = color;
            fill.raycastTarget = hit;
            return fill;
        }

        /// <summary>A light fading from the middle of <paramref name="rt"/> to its rim (under a picked picture).</summary>
        public static VoyageSoftFill Glow(RectTransform rt, Color color)
        {
            var fill = rt.gameObject.AddComponent<VoyageSoftFill>();
            fill.Set(VoyageSoftFill.Shape.Glow, 0f);
            fill.color = color;
            fill.raycastTarget = false;
            return fill;
        }

        /// <summary>A hairline across <paramref name="parent"/>'s top (<paramref name="top"/>) or foot, fading to both
        /// ends.</summary>
        public static VoyageSoftFill Hairline(RectTransform parent, bool top, Color color, float soft = 0.2f)
        {
            var rt = Node(top ? "LineTop" : "LineFoot", parent);
            Le(rt).ignoreLayout = true;
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            return Band(rt, color, soft);
        }

        /// <summary>A picture kept in its own shape; hidden until it has a sprite (<see cref="Show"/>).</summary>
        public static Image Picture(RectTransform rt)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        public static void Show(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite;
        }

        public static void Set(TMP_Text t, string value)
        {
            if (t) t.text = value ?? string.Empty;
        }

        public TMP_Text Text(Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var rt = Node("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font)
            {
                t.font = _font.font;
                t.fontSharedMaterial = _font.fontSharedMaterial;
            }
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A label: the style's label face, in capitals, spaced (the style guide's eyebrow).</summary>
        public TMP_Text Label(Transform parent, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var t = Text(parent, size, color, align);
            if (Style.LabelFont)
            {
                t.font = Style.LabelFont;
                t.fontSharedMaterial = Style.LabelFont.material;
            }
            t.fontStyle = FontStyles.UpperCase;
            t.characterSpacing = 14f;
            return t;
        }

        /// <summary>A line of text over a 2 unit line as wide as the text (the picked mark: light the line with
        /// <see cref="Cyan"/>, clear it otherwise), set to <paramref name="align"/> in its parent.</summary>
        public (TMP_Text text, Image bar) Underlined(Transform parent, float size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var row = Node("NameRow", parent);
            Row(row, 0f, align);
            var name = Node("Name", row);
            Column(name, 6f, TextAnchor.UpperCenter);
            var text = Text(name, size, Light, TextAlignmentOptions.Center);
            var bar = Node("Bar", name);
            Le(bar).preferredHeight = 2f;
            return (text, Block(bar, Cyan));
        }

        /// <summary>A button over a slivered plate; <paramref name="edge"/> draws its outline.</summary>
        public Button Plated(RectTransform rt, Color fill, bool edge, out Image plate, out Image outline)
        {
            plate = Sliced(rt, Style.Sliver10Fill, fill);
            plate.raycastTarget = true;
            outline = null;
            if (edge)
            {
                var e = Node("Edge", rt);
                Le(e).ignoreLayout = true;
                Stretch(e);
                outline = Sliced(e, Style.Sliver10Line, Outline);
            }
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = plate;
            return b;
        }

        /// <summary>
        /// A button's words in the label face with its keycap before them (an outlined box, the style guide's <c>.kc</c>),
        /// centred in <paramref name="rt"/>. An empty key shows no keycap.
        /// </summary>
        public TMP_Text KeyedLabel(RectTransform rt, string key, string words, Color color)
        {
            var row = Node("Label", rt);
            Stretch(row);
            Row(row, 12f, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(key))
            {
                var cap = Node("Key", row);
                var le = Le(cap);
                le.minWidth = le.preferredWidth = 28f;
                le.minHeight = le.preferredHeight = 24f;
                Sliced(cap, Style.Sliver10Line, color);
                var k = Label(cap, 14, color, TextAlignmentOptions.Center);
                Stretch(k.rectTransform);
                k.characterSpacing = 0f;
                k.text = key;
            }
            var t = Label(row, 18, color, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            t.text = words ?? string.Empty;
            return t;
        }

        /// <summary>The screen's one call to action (style guide section 5, Lime): as wide as <paramref name="rt"/>, its
        /// keycap before its words.</summary>
        public (Button button, TMP_Text words) Cta(RectTransform rt, string key, string words, Action onClick)
        {
            // Soft hard soft: solid across the middle, fading out to both ends; no box and no cut corners.
            var fill = Band(rt, CtaFill, 0.2f, hit: true);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            var colors = b.colors;
            colors.disabledColor = Disabled;
            b.colors = colors;
            var t = KeyedLabel(rt, key, words, CtaText);
            b.onClick.AddListener(() => onClick?.Invoke());
            return (b, t);
        }

        /// <summary>A quiet button (the default kind): the ground between two hairlines, all three fading out to both
        /// ends, its keycap before its words.</summary>
        public Button Quiet(RectTransform rt, string key, string words, Action onClick) => Quiet(rt, key, words, onClick, out _);

        public Button Quiet(RectTransform rt, string key, string words, Action onClick, out TMP_Text label)
        {
            var fill = Band(rt, Fade(RowPicked, 1f), 0.2f, hit: true);
            Hairline(rt, true, Fade(Light, 0.8f));
            Hairline(rt, false, Fade(Light, 0.8f));
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            var colors = b.colors;
            colors.disabledColor = Disabled;
            b.colors = colors;
            label = KeyedLabel(rt, key, words, Light);
            b.onClick.AddListener(() => onClick?.Invoke());
            return b;
        }

        /// <summary>The element's mark: the HUD's flower, five copies of the element's petal turned 72 degrees about the
        /// centre (<see cref="ElementalBarsView.ConfigurePetal"/>, Resources/ElementPetals), in <paramref name="color"/>.
        /// Elements carry no colour of their own.</summary>
        public static RectTransform Flower(Transform parent, CosmicShore.Data.Element element, Color color)
        {
            var root = Node("Flower", parent);
            var sprite = Resources.Load<Sprite>("ElementPetals/" + element.ToString().ToLowerInvariant() + "_petal");
            for (int i = 0; i < 5; i++)
            {
                var petal = Node("Petal" + i, root);
                var img = petal.gameObject.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                ElementalBarsView.ConfigurePetal(img, sprite, i);
                img.enabled = sprite;
            }
            return root;
        }

        /// <summary>Tint a <see cref="Flower"/>.</summary>
        public static void TintFlower(RectTransform flower, Color color)
        {
            foreach (var img in flower.GetComponentsInChildren<Image>(true)) img.color = color;
        }

        /// <summary>
        /// A stepper: its label over a plate holding a step back, the value and a step on. The step marks are the
        /// content's (<see cref="VoyageLines.BoardStepBack"/>, <see cref="VoyageLines.BoardStepOn"/>).
        /// </summary>
        public Stepper NewStepper(Transform parent)
        {
            var s = new Stepper();
            s.Root = Node("Stepper", parent);
            Column(s.Root, 2f);
            s.Label = Label(s.Root, 13, Sub);
            var box = Node("Box", s.Root);
            Le(box).preferredHeight = 44f;
            Sliced(box, Style.Sliver10Fill, Popup);
            var edge = Node("Edge", box);
            Stretch(edge);
            Sliced(edge, Style.Sliver10Line, Outline);
            s.Back = Step(box, 0f, Lines?.BoardStepBack);
            s.On = Step(box, 1f, Lines?.BoardStepOn);
            s.Value = Text(box, 15, Light, TextAlignmentOptions.Center);
            var vrt = s.Value.rectTransform;
            Stretch(vrt);
            vrt.offsetMin = new Vector2(44f, 0f);
            vrt.offsetMax = new Vector2(-44f, 0f);
            return s;
        }

        Button Step(RectTransform box, float side, string mark)
        {
            var rt = Node(side < 0.5f ? "Back" : "On", box);
            rt.anchorMin = new Vector2(side, 0f);
            rt.anchorMax = new Vector2(side, 1f);
            rt.pivot = new Vector2(side, 0.5f);
            rt.sizeDelta = new Vector2(44f, 0f);
            rt.anchoredPosition = Vector2.zero;
            var hit = Block(rt, Clear, hit: true);
            var t = Text(rt, 18, Light, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            t.richText = false;
            t.text = mark ?? string.Empty;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            return b;
        }

        public sealed class Stepper
        {
            public RectTransform Root;
            public TMP_Text Label, Value;
            public Button Back, On;

            /// <summary>Show <paramref name="label"/> and <paramref name="value"/>; the steps call <paramref name="step"/>
            /// with -1 or +1.</summary>
            public void Show(string label, string value, Action<int> step)
            {
                Set(Label, label);
                Set(Value, value);
                Back.onClick.RemoveAllListeners();
                On.onClick.RemoveAllListeners();
                Back.onClick.AddListener(() => step?.Invoke(-1));
                On.onClick.AddListener(() => step?.Invoke(+1));
            }
        }
    }
}
