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
        /// The free-pickup CTA at SIGNAL strength — the sibling of
        /// <see cref="GetDomainSignalColor"/>, and needed for the same reason.
        ///
        /// <para><b>A CTA pair is authored for a CRYSTAL, and a crystal composes both halves.</b>
        /// In every crystal shader the composition is <c>lerp(dull, bright, (1-N.V)^4)</c>, so
        /// <see cref="EnvironmentColorSet.DarkCTA"/> paints ~93% of a CTA crystal and
        /// <see cref="EnvironmentColorSet.BrightCTA"/> is a 2.5% hairline rim
        /// (<c>Docs/PALETTE.md §2.2</c>). Anything that is NOT a crystal — a prism, a UI chip —
        /// has no such composition, so painting it with the dull half alone gives it a dark olive
        /// where the player expects the free-pickup lime. The shipped
        /// <c>OriginalColorSetSO</c> authors <c>DarkCTA</c> at (0.28, 0.50, 0.08).</para>
        ///
        /// <para>Returns the CTA hue with its brightest channel driven to 1, exactly as the domain
        /// sibling does. Alpha 0 when the palette does not author a CTA at all (both inactive
        /// palettes author it (0,0,0,0)), so a caller can fall back rather than paint something
        /// black — a colour accessor that can return black can make an element vanish, and a
        /// vanished element reads as "not implemented" rather than as mis-tinted.</para>
        /// </summary>
        public Color GetCtaSignalColor()
        {
            var c = EnvironmentColors != null ? EnvironmentColors.DarkCTA : default;
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (c.a <= 0f || peak <= 0.001f) return new Color(0f, 0f, 0f, 0f);
            return new Color(c.r / peak, c.g / peak, c.b / peak, 1f);
        }

        /// <summary>
        /// The DANGER tier at SIGNAL strength - the third sibling of
        /// <see cref="GetDomainSignalColor"/> and <see cref="GetCtaSignalColor"/>, and needed for
        /// the same reason: a UI surface that wants to say "danger" must not read a prism colour
        /// raw.
        ///
        /// <para><b>The danger tier has no colour fields of its own</b> (see
        /// <see cref="GetPrismKindColors"/>): it is the domain's SHIELDED base face under the
        /// shared, domain-independent <see cref="EnvironmentColorSet.Danger"/> rim, and the RIM is
        /// the half that says dangerous. So this returns that rim - which the shipped
        /// <c>OriginalColorSetSO</c> authors HDR at (1.498, 0.006, 0.007) with <b>alpha 0</b>, the
        /// same trap <see cref="GetCtaSignalColor"/> records. Normalised here to the hue with its
        /// brightest channel driven to 1 and alpha 1.</para>
        ///
        /// <para>Returns alpha 0 when the palette authors no danger colour at all - both inactive
        /// palettes author (0,0,0,0) - so a caller can keep whatever it already had rather than
        /// paint something black. A colour accessor that can return black can make a UI element
        /// vanish, and a vanished element reads as "not implemented" rather than as mis-tinted.</para>
        /// </summary>
        public Color GetDangerSignalColor()
        {
            var c = EnvironmentColors != null ? EnvironmentColors.Danger : default;
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak <= 0.001f) return new Color(0f, 0f, 0f, 0f);
            return new Color(c.r / peak, c.g / peak, c.b / peak, 1f);
        }

        /// <summary>
        /// The domain's SHIELDED base face at SIGNAL strength - the fourth sibling of
        /// <see cref="GetDomainSignalColor"/>, <see cref="GetCtaSignalColor"/> and
        /// <see cref="GetDangerSignalColor"/>, and needed for the same reason a third time over:
        /// a UI surface that has to say "this is SHIELDED mass, in this domain" must not read a
        /// prism colour raw.
        ///
        /// <para>The shielded tier is its rim (<c>ShieldedInsideBlockColor</c>) over its base face
        /// (<c>ShieldedOutsideBlockColor</c>) - see <see cref="GetPrismKindColors"/> - and it is the
        /// BASE FACE that carries the tier's domain hue, which is why this reads that half.</para>
        ///
        /// <para><b>The whole of the work here is ONE conversion, and getting it wrong cost three
        /// rounds</b> (<c>Docs/PALETTE.md</c> §2.9). This project is <b>Linear</b>
        /// (<c>m_ActiveColorSpace: 1</c>) and these fields are <c>[ColorUsage(true, true)]</c>, so
        /// the floats in the asset are <b>linear intensities</b> - §3. A UI <c>Image.color</c> is
        /// <b>gamma</b>: measured off a screenshot, <c>ElementalBarsConfigSO.blueColor</c>
        /// (0.220, 0.510, 1.000) renders as exactly (56, 130, 255) and this accessor's old answer
        /// rendered as exactly (46, 125, 255) - a 1:1 map from float to display byte. So handing a
        /// palette float straight to an Image is a SPACE error, and it is not a small one: Jade's
        /// base face is (22, 60, 123) read as bytes and <b>(83, 134, 185)</b> once converted.</para>
        ///
        /// <para>The proof that the conversion is the right one is the HUE. Converted, Jade lands on
        /// <b>210.3°</b>; Jade shielded prisms MEASURE <b>209.4°</b> on screen (four samples,
        /// (86,167,253) (94,198,254) (106,178,254) (94,161,254)). Under a degree. The previous
        /// answer sat at 217.3° and that 8° gap was written down as an ACES hue shift - it was the
        /// missing conversion.</para>
        ///
        /// <para>Two things this accessor USED to do are therefore gone, and both were compensating
        /// for the space error rather than doing a job. It normalised the brightest channel to 1 on
        /// the stated grounds that the authored colour is "too dark for a UI slot" - <b>it is not
        /// dark, it is linear</b>, and the normalisation is what pushed the result to
        /// (0.179, 0.489, 1.000), hue 217.3°, which is <c>blueColor</c> to within <b>0.3°</b>: the
        /// colour that means <i>two upgrades in</i> on the very row this icon is drawn on. And a
        /// 0.25 lerp toward white modelled bloom + ACES on top of that. Converted honestly, the
        /// value is legible (0.725 brightness), sits 0.230 of saturation clear of that ladder rung,
        /// and needs neither.</para>
        ///
        /// <para>Stated gap: the prisms read BRIGHTER than this (measured value ~1.0 against 0.725)
        /// because a bright HDR rim sits over the base and blooms. This is the BASE FACE's colour,
        /// correctly converted - the hue and the tier are right, the bloom is not reproduced, and
        /// inventing a lift for it is exactly the mistake above.</para>
        ///
        /// <para>Alpha 0 when the domain authors no shielded base at all, so a caller keeps
        /// whatever it already had rather than painting something black - the same contract the
        /// CTA and danger siblings have, for the same reason.</para>
        ///
        /// <para>⚠ The three sibling accessors still normalise a LINEAR value the same way, and are
        /// deliberately left alone: their job is an unmistakable SIGNAL rather than a match to
        /// something in the world, and their shipped appearance was judged by eye. Changing them
        /// would move the Echo Sight, the vessel vision band and every domain-tinted HUD slot at
        /// once. Recorded in §2.9, not fixed here.</para>
        /// </summary>
        public Color GetShieldedSignalColor(Domains domain)
        {
            if (!TryGetColorSetByDomain(domain, out var colorSet)) return new Color(0f, 0f, 0f, 0f);
            var c = colorSet.ShieldedOutsideBlockColor;
            if (c.a <= 0f || Mathf.Max(c.r, Mathf.Max(c.g, c.b)) <= 0.001f)
                return new Color(0f, 0f, 0f, 0f);

            // Unity's own linear -> gamma transfer, so this cannot drift from what the engine does.
            var g = c.gamma;
            return new Color(g.r, g.g, g.b, 1f);
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
