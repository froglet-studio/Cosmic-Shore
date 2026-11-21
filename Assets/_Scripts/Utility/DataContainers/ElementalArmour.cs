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

        /// <summary>A shield's semi-axis as a multiple of its prism's FULL size along each local axis: the
        /// octahedron circumscribing the box is built from the HALF-extents x
        /// <see cref="OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE"/>, so 1.5.</summary>
        public const float ShieldReach = OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE * 0.5f;

        /// <summary>The clear radius an ARMOURED (Mass) heart needs around it: the Mass crystal's depth prisms at
        /// full reach, 11.85 u at the one crystal size (Docs/ECOSYSTEM.md §64.8), x the 5% margin the Mass fauna were
        /// shipped with. Measured offline off the mesh; <c>Tools/Build/mass_armour_lib.py</c> asserts the material and
        /// the crystal size it was measured at, so a retune of either fails every fit instead of quietly invalidating
        /// this.</summary>
        public const float HeartSwingClearance = 11.85f * 1.05f;

        /// <summary>The clear radius any other heart needs from a prism: the crystal's own seat, the figure the
        /// quasicrystal's heart seat is proven against (<c>Tools/Build/fit_quasicrystal_strut_sizes.py</c> CRYSTAL_NEED:
        /// above 0.6 x the one crystal size, the crystal's apparent radius).</summary>
        public const float HeartSeatClearance = 2.55f;

        /// <summary>How far a prism of <paramref name="element"/> must keep from its heart: the swing on an armoured
        /// lifeform (measured to its OCTAHEDRA), the crystal's seat on every other (measured to its box).</summary>
        public static float HeartClearance(Element element) => Shields(element) ? HeartSwingClearance : HeartSeatClearance;

        /// <summary>How far a prism of <paramref name="element"/> reaches from its centre along each local axis, per
        /// unit of its full size: its shield on an armoured lifeform (<see cref="ShieldReach"/>), its box (0.5) on every
        /// other. The box with these half-extents CONTAINS the shield, so a clearance proven against it is
        /// conservative.</summary>
        public static float PrismReach(Element element) => Shields(element) ? ShieldReach : 0.5f;

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
