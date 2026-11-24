using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A pilot's avatar as Frogma Rev 127 D ("Ripple", picked by Shombith 2026-11-23,
    /// <c>Docs/MENU_REDESIGN_B.md</c> section 217): the art cut to a circle, a ring in the pilot's domain colour on that
    /// circle's edge, and a thin wave the ring sends outward that fades exactly at the domain halo's rim, two waves
    /// half a period apart.
    ///
    /// <para><b>One geometry, so the border and the glow line up.</b> The halo behind an avatar used to be a round glow
    /// behind SQUARE art, whose corners reached past it. Here the avatar, its ring, the waves and the halo share one
    /// centre: the ring sits on the avatar's edge, the halo is <see cref="haloScale"/> times the avatar's size and at
    /// full colour from the ring outward, and a wave grows from 1 to exactly that scale.</para>
    ///
    /// <para><b>Structural, not authored.</b> <see cref="EnsureFor"/> builds it around an existing avatar Image the
    /// first time it is asked for, the way the retired PartySlotDomainGlow built its halo: a holder takes the
    /// avatar's place (its rect, its layout slot), and the avatar moves inside a round mask in it. The avatar Image
    /// keeps its identity, so every reference to it (sprite swaps, tints, the slot's own field) still works.</para>
    ///
    /// <para><b>It draws only what it knows.</b> A pilot whose domain is not known yet gets no halo and no waves,
    /// only a quiet line ring in <see cref="unknownRingToken"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarRippleBorder : MonoBehaviour
    {
        /// <summary>Name of the generated holder that takes the avatar's place.</summary>
        public const string HolderName = "AvatarRippleBorder";

        [Header("Geometry (Rev 127 D)")]
        [Tooltip("Halo diameter as a multiple of the avatar's. A wave fades out at exactly this size.")]
        [SerializeField, Min(1f)] float haloScale = 1.3f;

        [Tooltip("Ring width as a fraction of the avatar's diameter, plus Ring Width Px.")]
        [SerializeField, Min(0f)] float ringWidthPerDiameter = 0.016f;
        [SerializeField, Min(0f)] float ringWidthPx = 1.6f;

        [Tooltip("Wave width as a fraction of the avatar's diameter, plus Wave Width Px.")]
        [SerializeField, Min(0f)] float waveWidthPerDiameter = 0.008f;
        [SerializeField, Min(0f)] float waveWidthPx = 1f;

        [Tooltip("Ring width for a pilot whose domain is not known yet, in px.")]
        [SerializeField, Min(0f)] float unknownRingWidthPx = 2f;

        [Header("Motion (Rev 127 D)")]
        [Tooltip("Seconds one wave takes to travel from the ring to the halo's rim. A second wave runs half a period behind.")]
        [SerializeField, Min(0.1f)] float wavePeriod = 2.4f;

        [Tooltip("A wave's alpha as it leaves the ring; it fades to zero at the halo's rim.")]
        [SerializeField, Range(0f, 1f)] float waveStartAlpha = 0.9f;

        [Tooltip("The halo's alpha. Constant: on this border the waves carry the motion.")]
        [SerializeField, Range(0f, 1f)] float haloAlpha = 0.45f;

        [Tooltip("How fast the border eases to a new domain colour and fades in and out (an exponential rate).")]
        [SerializeField] float easeRate = 7f;

        [Header("Colours")]
        [Tooltip("The chrome theme, for the not-known-yet ring. Empty: the style guide's authored value.")]
        [SerializeField] UIThemeSO theme;

        [Tooltip("The ring's colour while the pilot's domain is not known yet.")]
        [SerializeField] UIColorToken unknownRingToken = UIColorToken.SurfaceLight;

        [Tooltip("The colour of the player's own avatar in a menu, where no match has given them a domain yet.")]
        [SerializeField] UIColorToken selfToken = UIColorToken.ControlActive;

        RectTransform _square;
        Image _halo, _ring;
        Image[] _waves;
        Image _avatar;

        Color _current = Color.white, _target = Color.white;
        bool _hasColour, _known;
        float _visibility, _targetVisibility;
        float _phase;          // own clock in 0..1 (Time.unscaledTime loses precision over a long session)
        Vector2 _builtForSize;
        bool _ringBuiltKnown;

        /// <summary>The avatar this border was built around.</summary>
        public Image Avatar => _avatar;

        // ─────────────────────────────────────────────────────────────────────
        // Build
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the border around <paramref name="avatar"/>, building it on first use. Safe to call repeatedly:
        /// an avatar already inside a border returns that border.
        /// </summary>
        public static AvatarRippleBorder EnsureFor(Image avatar)
        {
            if (avatar == null) return null;
            var existing = avatar.GetComponentInParent<AvatarRippleBorder>(true);
            if (existing != null && existing._avatar == avatar) return existing;

            var avatarRect = avatar.rectTransform;
            var parent = avatarRect.parent as RectTransform;
            if (parent == null) return null;

            // The holder takes the avatar's place: its rect, its sibling slot and its layout slot.
            var holderGo = new GameObject(HolderName, typeof(RectTransform), typeof(LayoutElement));
            var holder = (RectTransform)holderGo.transform;
            holder.SetParent(parent, false);
            holder.SetSiblingIndex(avatarRect.GetSiblingIndex());
            holder.anchorMin = avatarRect.anchorMin;
            holder.anchorMax = avatarRect.anchorMax;
            holder.pivot = avatarRect.pivot;
            holder.anchoredPosition = avatarRect.anchoredPosition;
            holder.sizeDelta = avatarRect.sizeDelta;
            holder.localScale = avatarRect.localScale;
            holder.localRotation = avatarRect.localRotation;

            var layout = holderGo.GetComponent<LayoutElement>();
            if (avatar.TryGetComponent<LayoutElement>(out var avatarLayout))
            {
                layout.ignoreLayout = avatarLayout.ignoreLayout;
                layout.minWidth = avatarLayout.minWidth;
                layout.minHeight = avatarLayout.minHeight;
                layout.preferredWidth = avatarLayout.preferredWidth;
                layout.preferredHeight = avatarLayout.preferredHeight;
                layout.flexibleWidth = avatarLayout.flexibleWidth;
                layout.flexibleHeight = avatarLayout.flexibleHeight;
                avatarLayout.ignoreLayout = true;
            }
            else
            {
                var size = avatarRect.rect.size;
                layout.preferredWidth = size.x;
                layout.preferredHeight = size.y;
            }

            var border = holderGo.AddComponent<AvatarRippleBorder>();
            border._avatar = avatar;

            // The circle is the largest square that fits the avatar's rect, centred: a portrait laid out in a
            // non-square region (a Configure seat's) still gets a round cut, not an ellipse.
            var squareGo = new GameObject("Square", typeof(RectTransform), typeof(AspectRatioFitter));
            var square = (RectTransform)squareGo.transform;
            square.SetParent(holder, false);
            square.anchorMin = Vector2.zero;
            square.anchorMax = Vector2.one;
            square.offsetMin = square.offsetMax = Vector2.zero;
            var fitter = squareGo.GetComponent<AspectRatioFitter>();
            fitter.aspectRatio = 1f;
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            border._square = square;

            // Back to front: halo, the two waves, the round mask holding the avatar and the ring.
            border._halo = Layer(square, "Halo", border.haloScale);
            border._waves = new[] { Layer(square, "Wave1", 1f), Layer(square, "Wave2", 1f) };

            var round = Layer(square, "Round", 1f);
            round.sprite = Disc();
            var mask = round.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            avatarRect.SetParent(round.rectTransform, false);
            avatarRect.anchorMin = Vector2.zero;
            avatarRect.anchorMax = Vector2.one;
            avatarRect.pivot = new Vector2(0.5f, 0.5f);
            avatarRect.anchoredPosition = Vector2.zero;
            avatarRect.sizeDelta = Vector2.zero;
            avatarRect.localScale = Vector3.one;
            avatarRect.localRotation = Quaternion.identity;
            avatar.preserveAspect = false;   // the circle crops square art; a letterbox would leave a gap at the ring

            border._ring = Layer(round.rectTransform, "Ring", 1f);
            border._ring.transform.SetAsLastSibling();

            border._halo.sprite = HaloSprite(border.haloScale);
            border.SetAlpha(0f);
            return border;
        }

        static Image Layer(RectTransform parent, string name, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            float pad = (scale - 1f) * 0.5f;
            rect.anchorMin = new Vector2(-pad, -pad);
            rect.anchorMax = new Vector2(1f + pad, 1f + pad);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;   // decoration never eats the avatar's clicks
            image.type = Image.Type.Simple;
            return image;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drive
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Shows the border in <paramref name="domainColour"/>. A repeat of the colour showing is a no-op.</summary>
        public void Show(Color domainColour)
        {
            domainColour.a = 1f;
            _target = domainColour;
            if (!_hasColour || !_known)
            {
                // The first colour snaps: easing from nothing would show the pilot briefly on the wrong side.
                _current = domainColour;
                _hasColour = true;
            }
            _known = true;
            _targetVisibility = 1f;
        }

        /// <summary>
        /// Shows the border in a theme token's colour: for an avatar that is the player themself in a menu, where no
        /// match has given them a domain yet (the Home pilot card, the profile, the friends orb, the avatar picker).
        /// </summary>
        public void ShowToken(UIColorToken token) => Show(UITheme.Resolve(theme, token));

        /// <summary>The player's own avatar in a menu: <see cref="ShowToken"/> in <see cref="selfToken"/>.</summary>
        public void ShowSelf() => ShowToken(selfToken);

        /// <summary>The pilot's domain is not known yet: no halo, no waves, the quiet line ring.</summary>
        public void ShowUnknown()
        {
            _known = false;
            _hasColour = false;
            _targetVisibility = 1f;
        }

        /// <summary>Fades the whole border out (an empty seat). The avatar's round cut stays.</summary>
        public void Hide()
        {
            _targetVisibility = 0f;
            _hasColour = false;
        }

        void Update()
        {
            if (_square == null || _ring == null) return;
            RebuildSpritesIfResized();

            float dt = Time.unscaledDeltaTime;   // menus run at a modified timeScale during camera transitions
            float ease = 1f - Mathf.Exp(-easeRate * dt);
            _visibility = Mathf.Lerp(_visibility, _targetVisibility, ease);
            _current = Color.Lerp(_current, _target, ease);
            if (_targetVisibility <= 0f && _visibility < 0.004f) _visibility = 0f;

            _phase += dt / Mathf.Max(0.1f, wavePeriod);
            if (_phase >= 1f) _phase -= Mathf.Floor(_phase);

            if (!_known)
            {
                var unknown = UITheme.Resolve(theme, unknownRingToken);
                _ring.color = WithAlpha(unknown, unknown.a * _visibility);
                _halo.enabled = false;
                for (int i = 0; i < _waves.Length; i++) _waves[i].enabled = false;
                return;
            }

            _halo.enabled = true;
            _ring.color = WithAlpha(_current, _visibility);
            _halo.color = WithAlpha(_current, haloAlpha * _visibility);
            for (int i = 0; i < _waves.Length; i++)
            {
                float t = Mathf.Repeat(_phase + (float)i / _waves.Length, 1f);
                WaveAt(t, haloScale, waveStartAlpha, out float scale, out float alpha);
                var wave = _waves[i];
                wave.enabled = alpha * _visibility > 0.002f;
                wave.color = WithAlpha(_current, alpha * _visibility);
                var s = new Vector3(scale, scale, 1f);
                if (wave.rectTransform.localScale != s) wave.rectTransform.localScale = s;
            }
        }

        void SetAlpha(float a)
        {
            _ring.color = WithAlpha(_ring.color, a);
            _halo.color = WithAlpha(_halo.color, a);
            foreach (var w in _waves) w.color = WithAlpha(w.color, a);
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        /// <summary>
        /// One wave at <paramref name="t"/> (0..1 of its period): it grows from the ring (scale 1) to the halo's rim
        /// (<paramref name="haloScale"/>) and fades from <paramref name="startAlpha"/> to 0, both on the board's
        /// ease-out (cubic-bezier .22,1,.36,1, approximated by 1 - (1 - t)^3).
        /// </summary>
        public static void WaveAt(float t, float haloScale, float startAlpha, out float scale, out float alpha)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            float e = 1f - inv * inv * inv;
            scale = Mathf.Lerp(1f, haloScale, e);
            alpha = startAlpha * (1f - e);
        }

        /// <summary>A band's width as a fraction of the avatar's radius: <c>(diameter * perDiameter + px) / radius</c>.</summary>
        public static float BandFraction(float diameter, float perDiameter, float px) =>
            diameter <= 0f ? 0f : Mathf.Clamp01((diameter * perDiameter + px) / (diameter * 0.5f));

        void RebuildSpritesIfResized()
        {
            var size = _square.rect.size;
            // The ring is narrower for a pilot whose domain is not known yet, so a flip of that rebuilds it too.
            if ((size - _builtForSize).sqrMagnitude < 0.25f && _ringBuiltKnown == _known) return;
            _builtForSize = size;
            _ringBuiltKnown = _known;
            float d = Mathf.Min(size.x, size.y);
            if (d <= 0f) return;

            if (_known)
                _ring.sprite = Ring(BandFraction(d, ringWidthPerDiameter, ringWidthPx));
            else
                _ring.sprite = Ring(BandFraction(d, 0f, unknownRingWidthPx));
            var wave = Ring(BandFraction(d, waveWidthPerDiameter, waveWidthPx));
            foreach (var w in _waves) w.sprite = wave;
        }


        // ─────────────────────────────────────────────────────────────────────
        // Shared generated sprites (white, tinted at run time)
        // ─────────────────────────────────────────────────────────────────────

        const int SIZE = 256;
        static Sprite _disc;
        static readonly Dictionary<int, Sprite> Rings = new();
        static readonly Dictionary<int, Sprite> Halos = new();

        static Sprite Disc() => _disc != null ? _disc : _disc = Make("AvatarRipple_Disc", r => Coverage(1f - r));

        /// <summary>An annulus on the texture's outer edge, <paramref name="fraction"/> of the radius wide.</summary>
        static Sprite Ring(float fraction)
        {
            int key = Mathf.Clamp(Mathf.RoundToInt(fraction * 512f), 1, 512);
            if (Rings.TryGetValue(key, out var s) && s != null) return s;
            float w = key / 512f;
            s = Make($"AvatarRipple_Ring{key}", r => Mathf.Min(Coverage(1f - r), Coverage(r - (1f - w))));
            Rings[key] = s;
            return s;
        }

        /// <summary>The halo: full from the avatar's edge (1 / scale of the texture's radius) fading out to its rim.</summary>
        static Sprite HaloSprite(float scale)
        {
            int key = Mathf.RoundToInt(scale * 1000f);
            if (Halos.TryGetValue(key, out var s) && s != null) return s;
            float core = 1f / Mathf.Max(1f, scale);
            s = Make($"AvatarRipple_Halo{key}", r =>
            {
                float t = Mathf.Clamp01((r - core) / Mathf.Max(1e-4f, 1f - core));
                float a = 1f - t * t * (3f - 2f * t);
                return a * a;
            });
            Halos[key] = s;
            return s;
        }

        // Anti-aliased coverage of a signed distance given in radius units (one texel wide).
        static float Coverage(float signed) => Mathf.Clamp01(signed * (SIZE * 0.5f) + 0.5f);

        static Sprite Make(string name, System.Func<float, float> alphaAtRadius)
        {
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false)
            {
                name = name, hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[SIZE * SIZE];
            float half = SIZE * 0.5f;
            for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float a = Mathf.Clamp01(alphaAtRadius(Mathf.Sqrt(dx * dx + dy * dy)));
                px[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
