using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The small kit the runtime-built coverflow pages share (the Quests window's <see cref="QuestCarouselView"/> and the
    /// Voyage window's <see cref="SagaView"/>; Docs/MENU_REDESIGN_B.md sections 155-156): the style guide's tokens, a
    /// rect, a fill, a text in a borrowed font, a coverflow card and the cover crop of a sprite. No words: every word on
    /// those pages is a serialized line on the page.
    /// </summary>
    public static class FlowUi
    {
        // The style guide's tokens (Docs/UI_STYLE_GUIDE.md), as VoyageOpenCard draws them.
        public static readonly Color Light = new(0.902f, 0.914f, 1f, 1f);
        public static readonly Color Soft = new(0.455f, 0.482f, 0.678f, 1f);
        public static readonly Color Line = new(0.263f, 0.298f, 0.537f, 1f);
        public static readonly Color Deep = new(0.039f, 0.047f, 0.110f, 1f);
        public static readonly Color Plate = new(0.075f, 0.086f, 0.180f, 0.92f);
        public static readonly Color Track = new(0.133f, 0.149f, 0.271f, 1f);
        public static readonly Color Selection = new(0f, 0.831f, 1f, 1f);
        public static readonly Color Lime = new(0.6f, 1f, 0.502f, 1f);
        public static readonly Color OnLime = new(0.039f, 0.047f, 0.110f, 1f);

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static Image Fill(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static void Edge(Component on, Color color, float width = 1f)
        {
            var e = on.gameObject.AddComponent<Outline>();
            e.effectColor = color;
            e.effectDistance = new Vector2(width, -width);
        }

        /// <summary>A text in <paramref name="fontFrom"/>'s face (the window's title), one line, ellipsised.</summary>
        public static TMP_Text Text(Transform parent, TMP_Text fontFrom, float size, Color color, TextAlignmentOptions align)
        {
            var rt = Node("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (fontFrom)
            {
                t.font = fontFrom.font;
                t.fontSharedMaterial = fontFrom.fontSharedMaterial;
            }
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A Lime call to action filling <paramref name="rt"/>, its label in the dark text Lime carries.</summary>
        public static (Button button, TMP_Text label) Act(RectTransform rt, TMP_Text fontFrom, Action onClick)
        {
            var fill = Fill(rt, Lime);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            b.onClick.AddListener(() => onClick());
            var t = Text(rt, fontFrom, 20, OnLime, TextAlignmentOptions.Center);
            Anchor(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return (b, t);
        }

        /// <summary>One coverflow card in <paramref name="flow"/>: a deep plate, its art over it and its name at the foot,
        /// posed by <see cref="VoyageFlowCard.Pose"/> (<see cref="VoyageCardRules.FlowPose"/>).</summary>
        public static VoyageFlowCard Card(RectTransform flow, int index, Vector2 size, TMP_Text fontFrom, Action<int> picked)
        {
            var rt = Node("Card" + index, flow);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var bg = Fill(rt, Deep);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var edge = rt.gameObject.AddComponent<Outline>();
            edge.effectDistance = new Vector2(2f, -2f);
            edge.effectColor = Line;
            var a = Node("Art", rt);
            Anchor(a, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var art = a.gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            var name = Text(rt, fontFrom, 28, Light, TextAlignmentOptions.BottomLeft);
            Anchor(name.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            name.margin = new Vector4(24f, 0f, 20f, 22f);
            name.textWrappingMode = TextWrappingModes.Normal;
            var card = rt.gameObject.AddComponent<VoyageFlowCard>();
            card.Init(b, art, name, bg);
            card.Index = index;
            card.Picked = picked;
            return card;
        }

        /// <summary>Show <paramref name="sprite"/> on <paramref name="img"/>, cover-cropped to <paramref name="aspect"/>.</summary>
        public static void Art(RawImage img, Sprite sprite, float aspect)
        {
            img.texture = sprite ? sprite.texture : null;
            img.enabled = sprite;
            if (sprite) img.uvRect = SpriteCover(sprite, aspect);
        }

        /// <summary>The uv rectangle of <paramref name="sprite"/>'s own rect in its texture (an atlas too), cropped centred
        /// to fill an <paramref name="aspect"/>-shaped frame (CSS object-fit: cover).</summary>
        public static Rect SpriteCover(Sprite sprite, float aspect)
        {
            if (!sprite || !sprite.texture) return new Rect(0f, 0f, 1f, 1f);
            var r = sprite.textureRect;
            float tw = sprite.texture.width, th = sprite.texture.height;
            var own = new Rect(r.x / tw, r.y / th, r.width / tw, r.height / th);
            float t = r.height > 0f ? r.width / r.height : 1f;
            Rect crop;
            if (aspect <= 0f) crop = new Rect(0f, 0f, 1f, 1f);
            else if (t > aspect) { float w = aspect / t; crop = new Rect((1f - w) * 0.5f, 0f, w, 1f); }
            else { float h = t / aspect; crop = new Rect(0f, (1f - h) * 0.5f, 1f, h); }
            return new Rect(own.x + crop.x * own.width, own.y + crop.y * own.height, crop.width * own.width, crop.height * own.height);
        }

        /// <summary>Stand <paramref name="cards"/> about <paramref name="picked"/>, the picked one in front.</summary>
        public static void Stand(System.Collections.Generic.List<VoyageFlowCard> cards, int count, int picked, float step)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                bool shown = i < count;
                c.gameObject.SetActive(shown);
                if (!shown) continue;
                c.Index = i;
                int offset = i - picked;
                c.Pose(offset, step);
                if (c.Edge && c.Edge.TryGetComponent(out Outline edge)) edge.effectColor = offset == 0 ? Selection : Line;
            }
            var order = new System.Collections.Generic.List<VoyageFlowCard>();
            for (int i = 0; i < count && i < cards.Count; i++) order.Add(cards[i]);
            order.Sort((a, b) => Mathf.Abs(b.Index - picked).CompareTo(Mathf.Abs(a.Index - picked)));
            foreach (var c in order) c.transform.SetAsLastSibling();
        }
    }
}
