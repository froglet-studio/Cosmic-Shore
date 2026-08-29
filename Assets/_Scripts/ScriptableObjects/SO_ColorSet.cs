using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Color Set", menuName = "ScriptableObjects/ColorSet")]
    [System.Serializable]
    public class SO_ColorSet : ScriptableObject
    {
        [SerializeField] public DomainColorSet JadeColors;
        [SerializeField] public DomainColorSet RubyColors;
        [SerializeField] public DomainColorSet GoldColors;
        [SerializeField] public DomainColorSet BlueColors;
        [SerializeField] public EnvironmentColorSet EnvironmentColors;

        public bool TryGetColorSetByDomain(Domains domain,  out DomainColorSet colorSet)
        {
            colorSet = domain switch
            {
                Domains.Jade => JadeColors,
                Domains.Ruby => RubyColors,
                Domains.Gold => GoldColors,
                Domains.Blue => BlueColors,
                _ => null
            };

            if (colorSet != null)
                return true;

            return false;
        }

        /// <summary>
        /// The single representative domain color for flat UI surfaces (scoreboard banner,
        /// player score cards, in-game HUD entries). Returns the domain's
        /// <see cref="DomainColorSet.TrailHighlightColor"/> - the same vivid color players
        /// see on that domain's vessel trails - so UI matches what's on the field. Neutral
        /// gray for domains with no color set.
        /// </summary>
        public Color GetDomainUIColor(Domains domain) =>
            TryGetColorSetByDomain(domain, out var colorSet) ? colorSet.TrailHighlightColor : Color.gray;

        /// <summary>
        /// The domain's colour pushed to FULL BRIGHTNESS — hue and saturation preserved, the
        /// brightest channel driven to 1. For a SIGNAL that has to be unmistakable: a HUD slot
        /// announcing which team owns something, or a highlight that must separate a vessel from lit
        /// mass around it.
        ///
        /// <para><b>Do not reach for a crystal colour to say "this belongs to domain X".</b>
        /// <c>DullCrystalColor</c> is authored (0,0,0) on Jade, Ruby AND Gold in the shipped
        /// <c>OriginalColorSetSO</c> — the domain crystals are deliberately near-black bodies with a
        /// bright fresnel rim, which is right on a faceted crystal in the world and renders as a
        /// black square in a UI slot. <c>BrightCrystalColor</c> tops out at 0.75 value. The domain UI
        /// colour is the palette's answer to "what colour is this team", and this is that colour at
        /// full strength.</para>
        ///
        /// Returns white for an unauthored domain, so a signal can never silently become invisible.
        /// </summary>
        public Color GetDomainSignalColor(Domains domain)
        {
            var c = GetDomainUIColor(domain);
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak <= 0.001f) return Color.white;
            return new Color(c.r / peak, c.g / peak, c.b / peak, 1f);
        }

        /// <summary>
        /// The per-domain accent for translucent flat-UI card tints (Maelstrom round/player/summary
        /// cards, Connecting-panel domain rank) - deliberately brighter than
        /// <see cref="DomainColorSet.TrailHighlightColor"/> and alpha-tinted so card backgrounds stay
        /// translucent over the scene. Falls back to <see cref="GetDomainUIColor"/> when the accent is
        /// unauthored (alpha 0), so color sets without accents keep the unified domain UI color.
        /// </summary>
        public Color GetDomainUIAccentColor(Domains domain) =>
            TryGetColorSetByDomain(domain, out var colorSet) && colorSet.UIAccentColor.a > 0f
                ? colorSet.UIAccentColor
                : GetDomainUIColor(domain);

        /// <summary>
        /// THE definition of what a prism of a given <see cref="PrismKind"/> is painted with:
        /// its fresnel rim (<c>_BrightColor</c>) and base face (<c>_DarkColor</c>). Note
        /// "Inside/Outside" in the field names are legacy misnomers for rim / base face -
        /// see Docs/PALETTE.md section 2.
        ///
        /// This is the SINGLE source of the tier composition. <c>ThemeManager</c> paints the
        /// live block materials from it and <c>PrismFactory</c> tints the death debris from
        /// it, so debris can never drift from the mass it came from. The danger tier in
        /// particular has no colour fields of its own - it is composed here out of the
        /// domain's SHIELDED base and the shared, domain-independent
        /// <see cref="EnvironmentColorSet.Danger"/> rim (Docs/PALETTE.md section 4.3).
        /// </summary>
        public void GetPrismKindColors(DomainColorSet colorSet, PrismKind kind,
                                       out Color bright, out Color dark)
        {
            switch (kind)
            {
                case PrismKind.Danger:
                    // Domain-independent hot rim (what says "dangerous") over the domain's
                    // shielded base (what says "whose"). Deliberately NOT the plain base.
                    bright = EnvironmentColors.Danger;
                    dark = colorSet.ShieldedOutsideBlockColor;
                    break;
                case PrismKind.Shielded:
                    bright = colorSet.ShieldedInsideBlockColor;
                    dark = colorSet.ShieldedOutsideBlockColor;
                    break;
                case PrismKind.SuperShielded:
                    bright = colorSet.SuperShieldedInsideBlockColor;
                    dark = colorSet.SuperShieldedOutsideBlockColor;
                    break;
                default:
                    bright = colorSet.InsideBlockColor;
                    dark = colorSet.OutsideBlockColor;
                    break;
            }
        }

        /// <summary>
        /// Domain-keyed convenience wrapper over <see cref="GetPrismKindColors"/>. False when
        /// the domain has no colour set authored (the caller keeps whatever it already had).
        /// </summary>
        public bool TryGetPrismKindColors(Domains domain, PrismKind kind,
                                          out Color bright, out Color dark)
        {
            if (!TryGetColorSetByDomain(domain, out var colorSet))
            {
                bright = Color.white;
                dark = Color.black;
                return false;
            }

            GetPrismKindColors(colorSet, kind, out bright, out dark);
            return true;
        }
    }

    [System.Serializable]
    public class DomainColorSet
    {
        [ColorUsage(true, true)] [SerializeField] public Color ShipColor1;
        [ColorUsage(true, true)] [SerializeField] public Color ShipColor2;
        [ColorUsage(true, true)] [SerializeField] public Color OutsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color ShieldedOutsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color SuperShieldedOutsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color InsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color ShieldedInsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color SuperShieldedInsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color AOETextureColor;
        [ColorUsage(true, true)] [SerializeField] public Color AOEFresnelColor;
        [ColorUsage(true, true)] [SerializeField] public Color AOEConicColor;
        [ColorUsage(true, true)] [SerializeField] public Color AOEConicEdgeColor;
        [ColorUsage(true, true)] [SerializeField] public Color SpikeLightColor;
        [ColorUsage(true, true)] [SerializeField] public Color SpikeDarkColor;
        [ColorUsage(true, true)] [SerializeField] public Color SkimmerColor;
        [ColorUsage(true, true)] [SerializeField] public Color DullCrystalColor;
        [ColorUsage(true, true)] [SerializeField] public Color BrightCrystalColor;
        [ColorUsage(true, true)] [SerializeField] public Color TrailHighlightColor;
        [ColorUsage(true, true)] [SerializeField] public Color TrailCoreColor;
        [Tooltip("Translucent flat-UI accent (Maelstrom cards, Connecting-panel rank). Brighter than " +
                 "TrailHighlightColor, alpha-tinted for card backgrounds. Alpha 0 = unauthored, falls " +
                 "back to the unified domain UI color (TrailHighlightColor).")]
        [SerializeField] public Color UIAccentColor;
    }

    [System.Serializable]
    public class EnvironmentColorSet
    {
        [ColorUsage(true, true)] [SerializeField] public Color SkyColor;
        [ColorUsage(true, true)] [SerializeField] public Color LightColor;
        [ColorUsage(true, true)] [SerializeField] public Color DarkColor;
        [Tooltip("The free-pickup lime. DarkCTA paints the crystal BODY and BrightCTA only its " +
                 "fresnel rim (Blend(Base=Dull, Blend=Bright, Opacity=(1-N.V)^4) in every crystal " +
                 "shader), and that rim is ~2.5% of the silhouette - so DarkCTA is what ~93% of the " +
                 "crystal, and therefore almost all of its BLOOM, is made of. Both are sized against " +
                 "the gameplay Bloom CLAMP (0.5): bloom saturates at max channel 0.5, so DarkCTA's " +
                 "max channel sits exactly there and pushing either higher buys nothing.")]
        [ColorUsage(true, true)] [SerializeField] public Color BrightCTA;
        [ColorUsage(true, true)] [SerializeField] public Color DarkCTA;

        [Tooltip("Scales the CTA pair for the four ELEMENTAL crystals so the omni reads as the hero " +
                 "pickup. A pure scalar, deliberately: it cannot move the hue, so all five crystals " +
                 "stay in one lime family by construction - which a second authored colour pair " +
                 "could silently break. It also dims correctly whichever role each colour plays " +
                 "(TimeCrystalGraph swaps body and rim relative to the other graphs).")]
        [Range(0f, 1f)] [SerializeField] public float ElementalCrystalDimming = 0.45f;

        [ColorUsage(true, true)] [SerializeField] public Color Danger;
    }

    public static class EnvironmentColorSetExtensions
    {
        /// <summary>
        /// Scales RGB and leaves ALPHA alone. Unity's Color operator* also scales alpha, which on a
        /// crystal tint would quietly fade the mesh out instead of dimming it.
        /// </summary>
        public static Color ScaleRGB(this Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);
    }
}
