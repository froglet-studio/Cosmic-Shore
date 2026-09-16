using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The ONE writer of the two colours every creature's spindle is painted with.
    ///
    /// <para><b>A creature is NEUTRAL, and neutral has an authored colour.</b> The shipped
    /// spindle materials carried a hand-authored <c>_BrightColor</c> (0.370, 0.397, 0.956) over
    /// <c>_DullColor</c> (0, 0.028, 1) — an eyeballed approximation of the palette's blue that
    /// nothing could keep in step with it, on materials a species has no business owning a
    /// colour decision in. <see cref="FaunaSpindleGraph"/> reads two UNEXPOSED globals instead,
    /// and this publishes them from the live <see cref="SO_ColorSet"/>.</para>
    ///
    /// <para><b>Which row of the palette, and why:</b> the <see cref="Domains.Blue"/> domain is
    /// the platform's neutral sentinel (never in <c>ActiveDomains</c>, never a team), and the
    /// SHIELDED tier is the row every flora and fauna HEALTH PRISM already wears
    /// (<c>Docs/PALETTE.md §2</c>). Taking the pair from there means a creature's soft tissue and
    /// its conserved mass come out of ONE row rather than two, and it happens to be literally
    /// white-to-blue: rim (1.113, 1.127, 1.260) over base (0, 0, 0.549). It is resolved through
    /// <see cref="SO_ColorSet.GetPrismKindColors"/> — THE single definition of a tier's pair — so
    /// a palette edit moves the creatures with the prisms and no second opinion exists.</para>
    ///
    /// <para><b>Why globals rather than painted materials.</b> <see cref="Spindle"/> mints EIGHT
    /// phase-variant materials per base material at runtime (<c>new Material(baseMat)</c>), which
    /// COPIES whatever colour the base carried at mint time. Painting the base would therefore
    /// work only while <see cref="ThemeManager"/> is guaranteed to run before the first spindle —
    /// an ordering dependency with no enforcement and a silent failure (stale colours on some
    /// creatures and not others). A global has no ordering to get wrong, costs two
    /// <c>SetGlobalColor</c> calls for the whole fleet, and is the same declaration
    /// <c>_PrismClock</c> already uses in that graph.</para>
    /// </summary>
    public static class FaunaNeutralPalette
    {
        static readonly int BrightId = Shader.PropertyToID("_FaunaNeutralBright");
        static readonly int DullId = Shader.PropertyToID("_FaunaNeutralDull");

        /// <summary>
        /// The shipped <c>OriginalColorSetSO</c>'s Blue SHIELDED rim, duplicated here as the
        /// value a scene with no ThemeManager renders with.
        ///
        /// <para>An unexposed Shader Graph property is a plain global: unset, it is ZERO, so
        /// without this every creature in a palette-less scene would render BLACK — which reads
        /// as "the shader is broken", not as "the palette has not loaded". Kept honest by
        /// <c>Tools/Build/check_fauna_neutral_palette.py</c>, which fails the build if these
        /// drift from the asset.</para>
        /// </summary>
        public static readonly Color FallbackBright = new Color(1.1131275f, 1.1268682f, 1.2596959f, 0.9411765f);

        /// <summary>The shipped Blue SHIELDED base face. See <see cref="FallbackBright"/>.</summary>
        public static readonly Color FallbackDull = new Color(0f, 0f, 0.54901963f, 1f);

        /// <summary>The pair currently published, for anything that needs to match it.</summary>
        public static Color Bright { get; private set; } = FallbackBright;

        /// <summary>The pair currently published, for anything that needs to match it.</summary>
        public static Color Dull { get; private set; } = FallbackDull;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PublishFallback() => Publish(FallbackBright, FallbackDull);

        /// <summary>
        /// Publishes the neutral pair from a live palette. Called by <see cref="ThemeManager"/>,
        /// which is where every other consumer of the ColorSet is already served from.
        /// </summary>
        public static void PublishFrom(SO_ColorSet colorSet)
        {
            if (colorSet == null || colorSet.BlueColors == null)
            {
                Publish(FallbackBright, FallbackDull);
                return;
            }

            colorSet.GetPrismKindColors(colorSet.BlueColors, PrismKind.Shielded,
                                        out var bright, out var dull);
            Publish(bright, dull);
        }

        public static void Publish(Color bright, Color dull)
        {
            Bright = bright;
            Dull = dull;
            Shader.SetGlobalColor(BrightId, bright);
            Shader.SetGlobalColor(DullId, dull);
        }
    }
}
