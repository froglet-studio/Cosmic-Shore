using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Saga and Episodes, locked, at the foot of the Voyages page (Frogma Rev 103 A, narrowed; Shombith, 2026-11-13:
    /// "Lock both saga and episodes for now. I do not think they should be different panels rather in the same maybe in
    /// the bottom showing as locked and coming soon", and 2026-11-14 on A: "make them smaller in the X axis").
    ///
    /// <para>Two cards side by side, centred: each a dimmed picture (the saga's first part's card art; the galaxy for
    /// Episodes), its kicker, its name, a lock and "Coming soon". A press opens nothing: the card's line says it is not
    /// open yet, and a second press puts "Coming soon" back. No idle motion, no glow, no call to action. Every word is a
    /// <see cref="VoyageLines"/> field (the <c>Strip*</c> lines). The Saga and Episodes tabs are dropped from the Voyage
    /// window's row (<see cref="VoyageWindowTabs.Drop"/>), so this strip is where both are seen.</para>
    /// </summary>
    public sealed class VoyageLockedStrip : MonoBehaviour
    {
        /// <summary>The strip's height, and the gap the page keeps above it for the board.</summary>
        public const float Height = 120f, Lift = Height + 24f;
        const float CardWidth = 520f, CardGap = 24f, PictureWidth = 150f, TextLeft = 176f, TextRight = 24f, LockSize = 18f;
        const float PictureAlpha = 0.45f, NameAlpha = 0.78f;

        sealed class Card
        {
            public TMP_Text Kicker, Name, Line;
            public Image Plate, Edge, Lock, Art;
            public RawImage Galaxy;
            public string KickerWords, NameWords;
        }

        VoyageKit _kit;
        Card _saga, _episodes;
        Card _pressed;
        Sprite _sagaArt, _lockSprite;

        /// <summary>Build the strip at <paramref name="host"/>'s foot, centred. <paramref name="sagaArt"/> is the saga's
        /// first part's card art (null: the galaxy); <paramref name="lockSprite"/> is the lock the tabs used.</summary>
        public static VoyageLockedStrip Create(RectTransform host, VoyageKit kit, Sprite sagaArt, Sprite lockSprite)
        {
            var rt = VoyageKit.Node("LockedStrip", host);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(2f * CardWidth + CardGap, Height);
            rt.anchoredPosition = Vector2.zero;
            var strip = rt.gameObject.AddComponent<VoyageLockedStrip>();
            strip._kit = kit;
            strip._sagaArt = sagaArt;
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

            // The plate is the press: a locked card answers, it does not open.
            var button = _kit.Plated(root, _kit.Popup, true, out c.Plate, out c.Edge);
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Press(c));

            var picture = VoyageKit.Node("Picture", root);
            picture.anchorMin = Vector2.zero;
            picture.anchorMax = new Vector2(0f, 1f);
            picture.pivot = new Vector2(0f, 0.5f);
            picture.sizeDelta = new Vector2(PictureWidth, 0f);
            picture.anchoredPosition = Vector2.zero;
            picture.gameObject.AddComponent<RectMask2D>();
            var art = VoyageKit.Node("Art", picture);
            VoyageKit.Stretch(art);
            c.Art = art.gameObject.AddComponent<Image>();
            c.Art.raycastTarget = false;
            art.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            var galaxy = VoyageKit.Node("Galaxy", picture);
            VoyageKit.Stretch(galaxy);
            c.Galaxy = galaxy.gameObject.AddComponent<RawImage>();
            c.Galaxy.raycastTarget = false;
            var rule = VoyageKit.Node("Rule", picture);
            rule.anchorMin = new Vector2(1f, 0f);
            rule.anchorMax = Vector2.one;
            rule.pivot = new Vector2(1f, 0.5f);
            rule.sizeDelta = new Vector2(1f, 0f);
            rule.anchoredPosition = Vector2.zero;
            VoyageKit.Block(rule, _kit.Outline);

            var words = VoyageKit.Node("Words", root);
            words.anchorMin = Vector2.zero;
            words.anchorMax = Vector2.one;
            words.offsetMin = new Vector2(TextLeft, 0f);
            words.offsetMax = new Vector2(-TextRight, 0f);

            c.Kicker = _kit.Label(words, 14f, _kit.Sub);
            VoyageKit.Top(c.Kicker.rectTransform, 20f, 20f);
            c.Kicker.rectTransform.offsetMax = new Vector2(-(LockSize + 8f), c.Kicker.rectTransform.offsetMax.y);

            var lockRt = VoyageKit.Node("Lock", words);
            lockRt.anchorMin = lockRt.anchorMax = new Vector2(1f, 1f);
            lockRt.pivot = new Vector2(1f, 1f);
            lockRt.sizeDelta = new Vector2(LockSize, LockSize);
            lockRt.anchoredPosition = new Vector2(0f, -21f);
            c.Lock = lockRt.gameObject.AddComponent<Image>();
            c.Lock.preserveAspect = true;
            c.Lock.raycastTarget = false;

            c.Name = _kit.Text(words, 22f, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Top(c.Name.rectTransform, 46f, 30f);

            c.Line = _kit.Label(words, 13f, _kit.Sub);
            VoyageKit.Top(c.Line.rectTransform, 86f, 18f);
            return c;
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
            Paint(_saga, lines?.StripSagaKicker, lines?.StripSagaName, _sagaArt);
            Paint(_episodes, lines?.StripEpisodesKicker, lines?.StripEpisodesName, null);
        }

        void Paint(Card c, string kicker, string name, Sprite art)
        {
            var lines = _kit.Lines;
            c.Plate.color = _kit.Popup;
            if (c.Edge) c.Edge.color = _kit.Outline;
            VoyageKit.Set(c.Kicker, kicker);
            VoyageKit.Set(c.Name, name);
            c.Name.color = VoyageKit.Fade(_kit.Light, NameAlpha);
            VoyageKit.Set(c.Line, _pressed == c ? lines?.StripNotOpen : lines?.StripSoon);
            c.Kicker.color = c.Line.color = _kit.Sub;
            c.Lock.sprite = _lockSprite;
            c.Lock.enabled = _lockSprite;
            c.Lock.color = _kit.Sub;

            // The picture, dimmed: the saga's art, else the galaxy (Episodes has none of its own).
            var dim = VoyageKit.Fade(_kit.Light, PictureAlpha);
            bool hasArt = art;
            c.Art.enabled = hasArt;
            c.Galaxy.enabled = !hasArt;
            if (hasArt)
            {
                c.Art.sprite = art;
                c.Art.color = dim;
                if (c.Art.TryGetComponent<AspectRatioFitter>(out var fit) && art.rect.height > 0f)
                    fit.aspectRatio = art.rect.width / art.rect.height;
            }
            else
            {
                c.Galaxy.texture = VoyageGalaxyArt.Texture();
                c.Galaxy.color = dim;
                // The galaxy's middle, cut to the picture's shape (as the locked tables zoom it, 1.9).
                const float zoom = 1.9f;
                float shape = PictureWidth / Height;
                float w = Mathf.Min(1f, shape / VoyageGalaxyArt.Aspect) / zoom, h = 1f / zoom;
                c.Galaxy.uvRect = new Rect(0.5f - w / 2f, 0.5f - h / 2f, w, h);
            }
        }
    }
}
