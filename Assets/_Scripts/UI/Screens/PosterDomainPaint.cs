using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Paints shapes as a PRISM of one domain in the worn colour set (Rev 94 C, Docs/MENU_REDESIGN_B.md sections
    /// 177.1-177.2): the set's face colour for a body, its rim colour for an edge. These are the two colours the
    /// game paints that domain's plain prisms with (<see cref="SO_ColorSet.TryGetPrismKindColors"/>), read from
    /// <see cref="ColorTrace.Worn"/>, so a band on the splash is the colour of the mass the player will fly through.
    ///
    /// <para>When the worn set changes (<see cref="ColorTrace.Changed"/>: a set worn at startup, previewed or
    /// applied in Profile) the shapes blend to the new set's colours over <see cref="blendSeconds"/>, eased both
    /// ways. Until a set is worn nothing is painted and the shapes keep their serialized colour, which is
    /// transparent. Nothing is written outside a blend.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Poster Domain Paint")]
    public class PosterDomainPaint : MonoBehaviour
    {
        [Tooltip("Whose prism this is.")]
        [SerializeField] Domains domain = Domains.Jade;

        [Header("Shapes")]
        [Tooltip("Painted in the domain's FACE colour: a prism's body.")]
        [SerializeField] KitSlant[] faces = new KitSlant[0];
        [Tooltip("Opacity of the face colour, 0..1.")]
        [SerializeField, Range(0f, 1f)] float faceAlpha = 0.74f;
        [Tooltip("Painted in the domain's RIM colour: a prism's edge.")]
        [SerializeField] KitSlant[] rims = new KitSlant[0];
        [Tooltip("Opacity of the rim colour, 0..1.")]
        [SerializeField, Range(0f, 1f)] float rimAlpha = 1f;

        [Header("When the worn colour set changes")]
        [Tooltip("Seconds the shapes take to blend to the new set's colours.")]
        [SerializeField, Min(0f)] float blendSeconds = 1f;

        Color _faceFrom, _faceTo, _rimFrom, _rimTo, _face, _rim;
        float _clock;
        bool _shown, _blending;

        /// <summary>
        /// A colour-set colour as a canvas shows it: an HDR colour is brought back under 1 on its brightest
        /// channel (its hue and saturation kept), and it takes <paramref name="alpha"/>.
        /// </summary>
        public static Color Display(Color colour, float alpha)
        {
            float peak = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
            float k = peak > 1f ? 1f / peak : 1f;
            return new Color(colour.r * k, colour.g * k, colour.b * k, Mathf.Clamp01(alpha));
        }

        /// <summary>The blend's place at <paramref name="t"/> (0..1), eased both ways: no pop at either end.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        void OnEnable()
        {
            ColorTrace.Changed += Repaint;
            Repaint();
        }

        void OnDisable() => ColorTrace.Changed -= Repaint;

        void Repaint()
        {
            var set = ColorTrace.Worn;
            if (!set || !set.TryGetPrismKindColors(domain, PrismKind.Plain, out var rim, out var face))
                return;

            _faceTo = Display(face, faceAlpha);
            _rimTo = Display(rim, rimAlpha);
            // The first paint rises from clear in the same colours; a later one blends from what is shown.
            _faceFrom = _shown ? _face : new Color(_faceTo.r, _faceTo.g, _faceTo.b, 0f);
            _rimFrom = _shown ? _rim : new Color(_rimTo.r, _rimTo.g, _rimTo.b, 0f);
            _shown = true;
            _clock = 0f;
            _blending = blendSeconds > 0f;
            if (!_blending) Apply(1f);
        }

        void Update()
        {
            if (!_blending) return;
            _clock += Time.unscaledDeltaTime;
            float t = blendSeconds > 0f ? _clock / blendSeconds : 1f;
            Apply(Ease(t));
            if (t >= 1f) _blending = false;
        }

        void Apply(float eased)
        {
            _face = Color.Lerp(_faceFrom, _faceTo, eased);
            _rim = Color.Lerp(_rimFrom, _rimTo, eased);
            for (int i = 0; i < faces.Length; i++)
                if (faces[i]) faces[i].Paint(_face);
            for (int i = 0; i < rims.Length; i++)
                if (rims[i]) rims[i].Paint(_rim);
        }
    }
}
