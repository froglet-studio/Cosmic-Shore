using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Saga and Episodes, locked, at the foot of the Voyages page (Frogma Rev 103, narrowed; Shombith, 2026-11-13:
    /// "Lock both saga and episodes for now. I do not think they should be different panels rather in the same maybe in
    /// the bottom showing as locked and coming soon", 2026-11-14 on A: "make them smaller in the X axis", and 2026-11-16:
    /// "make them smaller like the line would be better for now").
    ///
    /// <para>Two single-line strips side by side, centred, together as wide as the table (1216): each a lock, its kicker,
    /// its name and "Coming soon" at its right end, on a quiet plate. No picture. A press opens nothing: the name gives
    /// way to the line that says it is not open yet, and a second press puts the name back. No idle motion, no glow, no call to action. Every word is a <see cref="VoyageLines"/>
    /// field (the <c>Strip*</c> lines). The Saga and Episodes tabs are dropped from the Voyage window's row
    /// (<see cref="VoyageWindowTabs.Drop"/>), so this strip is where both are seen.</para>
    /// </summary>
    public sealed class VoyageLockedStrip : MonoBehaviour
    {
        /// <summary>The strip's height, and the gap the page keeps above it for the board.</summary>
        public const float Height = 44f, Lift = Height + 16f;
        const float CardWidth = 600f, CardGap = 16f, Pad = 16f, LockSize = 14f, KickerWidth = 96f, LineWidth = 132f;
        const float NameAlpha = 0.78f;

        sealed class Card
        {
            public TMP_Text Kicker, Name, Line;
            public Image Plate, Edge, Lock;
        }

        VoyageKit _kit;
        Card _saga, _episodes;
        Card _pressed;
        Sprite _lockSprite;

        /// <summary>Build the strip at <paramref name="host"/>'s foot, centred. <paramref name="lockSprite"/> is the lock
        /// the tabs used.</summary>
        public static VoyageLockedStrip Create(RectTransform host, VoyageKit kit, Sprite lockSprite)
        {
            var rt = VoyageKit.Node("LockedStrip", host);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(2f * CardWidth + CardGap, Height);
            rt.anchoredPosition = Vector2.zero;
            var strip = rt.gameObject.AddComponent<VoyageLockedStrip>();
            strip._kit = kit;
            strip._lockSprite = lockSprite;
            strip._saga = strip.BuildCard(rt, "Saga", 0f);
            strip._episodes = strip.BuildCard(rt, "Episodes", CardWidth + CardGap);
            strip.Draw();
            return strip;
        }

        void OnEnable() => ColorTrace.Changed += Draw;
        void OnDisable() => ColorTrace.Changed -= Draw;

        Card BuildCard(RectTransform strip, string name, float left)
        {
            var c = new Card();
            var root = VoyageKit.Node(name, strip);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = Vector2.zero;
            root.sizeDelta = new Vector2(CardWidth, Height);
            root.anchoredPosition = new Vector2(left, 0f);

            // The plate is the press: a locked strip answers, it does not open.
            var button = _kit.Plated(root, _kit.Popup, true, out c.Plate, out c.Edge);
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Press(c));

            // One line, left to right: the lock, the kicker, the name; "Coming soon" at the right end.
            var lockRt = VoyageKit.Node("Lock", root);
            lockRt.anchorMin = lockRt.anchorMax = new Vector2(0f, 0.5f);
            lockRt.pivot = new Vector2(0f, 0.5f);
            lockRt.sizeDelta = new Vector2(LockSize, LockSize);
            lockRt.anchoredPosition = new Vector2(Pad, 0f);
            c.Lock = lockRt.gameObject.AddComponent<Image>();
            c.Lock.preserveAspect = true;
            c.Lock.raycastTarget = false;

            float kickerX = Pad + LockSize + 10f;
            c.Kicker = _kit.Label(root, 12f, _kit.Sub);
            Span(c.Kicker.rectTransform, kickerX, KickerWidth, false);

            c.Line = _kit.Label(root, 12f, _kit.Sub, TextAlignmentOptions.Right);
            Span(c.Line.rectTransform, Pad, LineWidth, true);

            c.Name = _kit.Text(root, 16f, _kit.Light, TextAlignmentOptions.Left);
            var nameRt = c.Name.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(kickerX + KickerWidth + 8f, 0f);
            nameRt.offsetMax = new Vector2(-(Pad + LineWidth + 8f), 0f);
            return c;
        }

        /// <summary>A full-height span <paramref name="width"/> wide, <paramref name="inset"/> from the strip's left end
        /// (or its right end, <paramref name="fromRight"/>).</summary>
        static void Span(RectTransform rt, float inset, float width, bool fromRight)
        {
            float side = fromRight ? 1f : 0f;
            rt.anchorMin = new Vector2(side, 0f);
            rt.anchorMax = new Vector2(side, 1f);
            rt.pivot = new Vector2(side, 0.5f);
            rt.sizeDelta = new Vector2(width, 0f);
            rt.anchoredPosition = new Vector2(fromRight ? -inset : inset, 0f);
        }

        void Press(Card c)
        {
            _pressed = _pressed == c ? null : c;
            Draw();
        }

        void Draw()
        {
            if (_kit == null || _saga == null) return;
            var lines = _kit.Lines;
            Paint(_saga, lines?.StripSagaKicker, lines?.StripSagaName);
            Paint(_episodes, lines?.StripEpisodesKicker, lines?.StripEpisodesName);
        }

        void Paint(Card c, string kicker, string name)
        {
            var lines = _kit.Lines;
            c.Plate.color = _kit.Popup;
            if (c.Edge) c.Edge.color = _kit.Outline;
            VoyageKit.Set(c.Kicker, kicker);
            bool pressed = _pressed == c;
            VoyageKit.Set(c.Name, pressed ? lines?.StripNotOpen : name);
            c.Name.color = pressed ? _kit.Sub : VoyageKit.Fade(_kit.Light, NameAlpha);
            VoyageKit.Set(c.Line, lines?.StripSoon);
            c.Kicker.color = c.Line.color = _kit.Sub;
            c.Lock.sprite = _lockSprite;
            c.Lock.enabled = _lockSprite;
            c.Lock.color = _kit.Sub;
        }
    }
}
