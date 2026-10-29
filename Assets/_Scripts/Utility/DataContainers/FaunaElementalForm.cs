using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// What a creature's ELEMENT says about it, beyond its heart (Docs/ECOSYSTEM.md §64.8-64.9).
    ///
    /// <list type="bullet">
    /// <item><b>MASS has SIZE.</b> A Mass variant's body is grown until its heart's swing (the Mass crystal's
    /// depth prisms rising 2.37x) clears it - authored per species as
    /// <see cref="FaunaVariantTuning.BaseBodyScale"/>, because each body's cavity is its own measurement.</item>
    /// <item><b>SPACE takes ROOM.</b> Its school keeps the widest spacing (<see cref="Spread"/>) and it senses and
    /// strikes from furthest (<see cref="Reach"/>) - the plants' own Space reach (<see cref="FloraElementalForm.ReachScale"/>,
    /// x1.35), so "Space reaches further" is one law in the cell. Its body is the LONGEST and the most SPINDLY: scaled
    /// whole to <see cref="SpaceLengthOverMass"/> the same species' Mass body, then its spindles ERODED
    /// (<see cref="FaunaVariantTuning.ErodeSpindle"/>, <see cref="ErodedGirth"/>) so its thickest section is no thicker
    /// than the Mass body's, its prisms the leaf law's needles fitted to their eroded sockets.</item>
    /// </list>
    ///
    /// <para>Pure and Unity-free apart from the element enum and Mathf, like <see cref="FloraElementalForm"/>. A dial is
    /// multiplied where it is READ, never written back, so a pooled body that comes back as another element cannot
    /// compound it.</para>
    /// </summary>
    public static class FaunaElementalForm
    {
        /// <summary>How much further apart a school of this element keeps (separation and cohesion).</summary>
        public static float Spread(Element element) => element == Element.Space ? FloraElementalForm.ReachScale(Element.Space) : 1f;

        /// <summary>How much further this element senses and strikes (detection, attack and feeding reach).</summary>
        public static float Reach(Element element) => element == Element.Space ? FloraElementalForm.ReachScale(Element.Space) : 1f;

        /// <summary>How much LONGER a Space body is than the same species' Mass body: 1.647, the plants' Space:Mass reach
        /// as Docs/ECOSYSTEM.md §51 set it ((1.8347 / 0.4097)^(1/3) - Space reached 1.35x where Mass drew in to 0.82x), the
        /// length the Space shark was approved at (§64.9). It is a creature constant now: the plants' Mass leaf left that
        /// ratio in §65 (its volume is its shield's, so a Mass PLANT spans less still and Space out-spans it 2.0x), while a
        /// Mass CREATURE kept the size its heart's swing needs (§64.8), so Space still out-spans it by this.</summary>
        public const float SpaceLengthOverMass = 1.6477f;

        /// <summary>The lattice flora's fitting margin: a fitted plate is the largest that clears, less 3%.</summary>
        public const float FitClearance = 0.97f;

        /// <summary>The exponent a Space spindle's section widths are drawn in by: the inverse of the lattice's Space
        /// leaf anisotropy (1/2.11). Where a Space leaf drives the ratio of its long axis out by that power, its body draws
        /// the ratio of a thick section to a thin one IN by the same power - the thick parts lose the most.</summary>
        public static float SpindleGirthExponent => 1f / FloraElementalForm.SpaceLeafAnisotropy;

        /// <summary>The girth factor of an eroded body's THICKEST section: 1/<see cref="SpaceLengthOverMass"/>. A Space
        /// body is scaled whole to 1.647x the Mass body, so its thickest section comes back to the Mass body's thickness:
        /// what Space spends on length it takes from girth.</summary>
        public static float ThickestGirth => 1f / SpaceLengthOverMass;

        /// <summary>The section half-width below which a spindle is not eroded: the width from which
        /// <see cref="ErodedGirth"/> reaches <see cref="ThickestGirth"/> at the body's <paramref name="thickest"/>.</summary>
        public static float ErosionReference(float thickest) =>
            thickest * Mathf.Pow(ThickestGirth, 1f / (1f - SpindleGirthExponent));

        /// <summary>The girth factor of a section <paramref name="width"/> wide: (width / reference)^(exponent - 1)
        /// above the reference, 1 at or below it (a thin tail or fin keeps its whole size).</summary>
        public static float ErodedGirth(float width, float reference) =>
            reference > 0f && width > reference ? Mathf.Pow(width / reference, SpindleGirthExponent - 1f) : 1f;
    }
}
