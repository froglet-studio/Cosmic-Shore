using CosmicShore.Data;

namespace CosmicShore.Utility
{
    /// <summary>
    /// What an element does to a lifeform's PRISMS, for plants and creatures alike (Docs/ECOSYSTEM.md §65; Garrett,
    /// 2026-10-28: "shift the shielding affect from charge to mass ... then the new charge affect will be to make all of
    /// its prisms danger prism, not just the select ones the other elements have").
    ///
    /// <list type="bullet">
    /// <item><b>MASS is ARMOURED.</b> Its prisms are shielded, and re-armoured one every <see cref="ShieldPeriod"/>
    /// seconds forever: a shielded prism sheds its shield instead of being eaten or destroyed, so Mass takes two passes.
    /// A shield is the octahedron circumscribing the prism at 3x its reach, so a Mass body is FITTED for its octahedra
    /// (and, having size, grown until its heart's swing clears them).</item>
    /// <item><b>CHARGE is DANGEROUS.</b> Every one of its prisms is a danger prism. A Charge lifeform never shields:
    /// danger and shield are mutually exclusive by locked design (a shield clears danger), so armouring it would undo
    /// its identity.</item>
    /// <item>Every element keeps its own AUTHORED danger prisms (a gyroid's octagon ring, a shark's teeth). On a Mass
    /// lifeform those stay danger and are not shielded - the regen skips them.</item>
    /// </list>
    ///
    /// <para>A law rather than a number on every asset because the element is ROLLED per lifeform while a cadence is
    /// authored per config (Docs/ECOSYSTEM.md §35.1). Pure and Unity-free.</para>
    /// </summary>
    public static class ElementalArmour
    {
        /// <summary>Seconds between shield refreshes on an armoured (Mass) lifeform's prisms.</summary>
        public const float ShieldPeriod = 1f;

        /// <summary>True for the element whose prisms are shielded.</summary>
        public static bool Shields(Element element) => element == Element.Mass;

        /// <summary>True for the element whose every prism is a danger prism.</summary>
        public static bool ArmsEveryPrism(Element element) => element == Element.Charge;

        /// <summary>The shield cadence a lifeform of <paramref name="element"/> runs, given what its config resolved to.
        /// Mass is floored at <see cref="ShieldPeriod"/> (an authored cadence still wins: faster or slower, never off);
        /// Charge is 0 whatever is authored (its prisms are danger, and a shield would clear it); every other element
        /// keeps its authored cadence.</summary>
        public static float ResolveShieldPeriod(Element element, float authored)
        {
            if (ArmsEveryPrism(element)) return 0f;
            if (Shields(element) && authored <= 0f) return ShieldPeriod;
            return authored;
        }
    }
}
