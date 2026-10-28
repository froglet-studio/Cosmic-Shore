using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// THE FOUR ELEMENTAL IDENTITIES OF A PLANT, as one rule (Docs/ECOSYSTEM.md §51).
    ///
    /// <para>Every element expresses itself in the food web through the shape of the mass it
    /// grows, and each of the four says exactly ONE thing:</para>
    ///
    /// <list type="bullet">
    /// <item><b>CHARGE is dangerous.</b> Every prism it lays is a danger prism (<c>Flora.ArmLeaf</c>,
    /// <see cref="ElementalArmour"/>, Docs/ECOSYSTEM.md §65). Charge is silent in this file - its identity is a state,
    /// not a shape.</item>
    /// <item><b>MASS is ARMOURED, and its volume is its SHIELD's.</b> Its leaf keeps the species' own SHAPE at 0.64x
    /// the neutral one on every axis - fitted so its shields (3x the box) clear inside Space's span - and its prisms are
    /// shielded (<c>Flora.ResolveShieldPeriod</c>), so it SHOWS 1.18x the neutral volume (Docs/ECOSYSTEM.md §65). It was
    /// the most cumulative and most CUBIC leaf, the "chunky" plate, until 2026-10-28.</item>
    /// <item><b>SPACE is the opposite: the highest ASPECT RATIO.</b> Its long axis costs it
    /// cumulative prism volume and buys it the BOUNDING VOLUME of the assembly - the same
    /// plant, spent as a wide skeleton of needles instead of a compact block.</item>
    /// <item><b>TIME is the fastest clock.</b> It grows fastest and it reproduces fastest
    /// (<see cref="FloraReproductionRules.ReproductionRateFor"/>). Time is silent about
    /// shape - its identity is tempo.</item>
    /// </list>
    ///
    /// <para><b>CHARGE, TIME AND SPACE ARE A REDISTRIBUTION OF ONE SPECIES' FORM, NEVER AN INFLATION OF
    /// IT</b> (Mass left the average in §65: its volume is its shield's, and its box is lighter). §51's four values averaged to exactly 1, and
    /// <see cref="ShapeLeaf"/>'s anisotropy term is volume-EXACT by construction (raising a
    /// unit-volume shape vector to any power leaves its product at 1). So a mixed-element
    /// forest holds the same mass it held before this law existed, and <b>no cell's volume
    /// phase ladder moves</b> - which is what makes a fleet-wide leaf law shippable at all
    /// (Docs/ECOSYSTEM.md §4.6: volume is the spine). What changes is that a Mass plant and a
    /// Space plant in the same cell are now visibly different plants.</para>
    ///
    /// <para><b>It CANNOT be authored per config.</b> The leaf is authored per CONFIG while
    /// the element is ROLLED per plant (<c>FloraConfigurationSO.SpreadElements</c>) - a config
    /// with an empty element palette applies its OWN variant block to whatever it rolled, so
    /// no asset field could express this. Same argument, same choke point and same scoping as
    /// <c>Flora.ResolveShieldPeriod</c> and <c>Flora.ResolveGrowthPerOffspring</c>: resolved at
    /// <c>LifeForm.Initialize</c>, and scoped to <c>Flora</c> so no creature inherits a rule
    /// written about plants.</para>
    ///
    /// <para><b>The constants are MEASURED, not invented.</b> Three shipped species already
    /// state this law in their own fitted data - the gyroid, Schwarz P and quasicrystal
    /// lattices each author four leaves per element - and the eight Hesperides phyllotactics
    /// share one authored cross-section ladder that states the volume half of it. Each FAMILY
    /// gets one vote (the eight phyllotactics share one table, so they are one decision, not
    /// eight), and the law is the geometric mean of the two family medians, taken against
    /// TIME as the neutral form. Measured per-element against Time:
    /// volume Mass x2.40 / x6.14 / x1.86 (lattice) and x1.82 (phyllotactic);
    /// Space x0.87 / x0.98 / x0.54 and x0.25. Anisotropy exponent Mass 0.39 / 0.52 / 0.45,
    /// Space 2.11 / 1.34 / 2.20 - three independent authoring decisions agreeing on both the
    /// direction and, for the aspect, closely on the magnitude.
    /// <c>Tools/Build/measure_flora_elemental_form.py</c> re-derives every one of these
    /// numbers from the shipped assets and fails the build when a species contradicts the
    /// direction.</para>
    ///
    /// <para><b>A species whose prism size is dictated by its growth rule is EXEMPT and states
    /// the law in its own data instead</b> (<c>Flora.PrismSizeFixedByGrowthRule</c>): a lattice
    /// bonds at offsets measured in absolute units, so a transformed leaf lays prisms the bond
    /// table no longer describes (Docs/ECOSYSTEM.md §34.8). Those species are CHECKED against
    /// the law offline rather than transformed at runtime - and a Mass leaf re-fitted for its
    /// armour (<c>Tools/Build/fit_shield_clearance.py</c>) is fitted against what the species
    /// authors, so the two cannot fight.</para>
    ///
    /// <para>Kept static and engine-free (bar <see cref="Mathf"/>) so the edit-mode tests can
    /// pin it without a Unity runtime - the same shape as
    /// <see cref="FloraReproductionRules"/>.</para>
    /// </summary>
    public static class FloraElementalForm
    {
        // ── VOLUME: how much prism a plant of this element spends per leaf ───────────────
        //
        // Charge, Time and Space are normalised as they always were (Space + 2 x neutral + the Mass leaf of §51 averaged
        // to 1). MASS no longer joins that average: since Docs/ECOSYSTEM.md §65 its VOLUME is its SHIELD's.

        /// <summary>Per-leaf BOX volume multiplier for a MASS plant: <see cref="NeutralLeafVolume"/> x
        /// <see cref="MassAxisScale"/>^3 = 0.2301 (0.64^3 of the neutral leaf). A Mass plant's volume is carried by its
        /// SHIELD (Garrett, 2026-10-28: "the shielding should increase the volume of mass"): the octahedron is 4.5x its
        /// box, so a Mass leaf SHOWS 1.18x the neutral one while its prism is 0.26x it. It was 1.8347 (the heaviest box,
        /// x2.09 the neutral) until Mass was fitted under Space (§65: "space should always have greater span but
        /// significantly less volume"). So a Mass plant is lighter FOOD than it looks - its box is what a second pass
        /// eats - and a cell's prism-volume ladder reads less of it than it did.</summary>
        public const float MassLeafVolume = 0.2301f;

        /// <summary>Per-leaf VOLUME multiplier for a SPACE plant - the lightest of the four.</summary>
        public const float SpaceLeafVolume = 0.4097f;

        /// <summary>
        /// Per-leaf VOLUME multiplier for the two elements whose identity is not shape -
        /// CHARGE (danger) and TIME (tempo). They take the species' own authored form, scaled
        /// only by the normaliser that keeps the four-element mean at 1.
        /// </summary>
        public const float NeutralLeafVolume = 0.8778f;

        // ── ASPECT: how far apart this element's leaf axes sit ──────────────────────────
        //
        // An exponent on the leaf's unit-volume SHAPE vector, so it is exactly
        // volume-preserving: <1 pulls every axis toward the geometric mean (toward a cube),
        // >1 drives them apart (a needle gets longer AND thinner). 1 is the authored shape.

        /// <summary>Anisotropy exponent for a MASS plant: 1, the species' own shape. It was 0.45 (pulling x, y and z
        /// toward a cube, the "chunky" Mass plate) until Mass took the shield and size on 2026-10-28 (Docs/ECOSYSTEM.md
        /// §65): a shield is drawn as the octahedron circumscribing the prism, so a cube-ward plate is not what shows.</summary>
        public const float MassLeafAnisotropy = 1f;

        /// <summary>Anisotropy exponent for a SPACE plant - drives the long axis out.</summary>
        public const float SpaceLeafAnisotropy = 2.11f;

        /// <summary>This element's per-leaf volume multiplier.</summary>
        public static float LeafVolumeScale(Element element) => element switch
        {
            Element.Mass => MassLeafVolume,
            Element.Space => SpaceLeafVolume,
            Element.Charge or Element.Time => NeutralLeafVolume,
            // Element.None means "no crystal resolved yet" and Omni is not one of the four.
            // A plant that reaches growth without an element keeps behaving exactly as it did.
            _ => 1f,
        };

        /// <summary>This element's leaf anisotropy exponent.</summary>
        public static float LeafAnisotropy(Element element) => element switch
        {
            Element.Mass => MassLeafAnisotropy,
            Element.Space => SpaceLeafAnisotropy,
            _ => 1f,
        };

        /// <summary>
        /// How far this element's plant REACHES, relative to the species' authored extent -
        /// the half of the law that lands on the assembly rather than on one prism.
        ///
        /// <para>SPACE and the two neutral elements spend a fixed amount of material: their reach is
        /// <c>LeafVolumeScale^(-1/3)</c>, so thinning the leaf buys extent - "Space's long axis reduces
        /// cumulative prism volume and increases the bounding volume of the assembly" (Space x1.35, Charge and
        /// Time x1.04).</para>
        ///
        /// <para><b>MASS is the neutral plant scaled by its leaf's axis scale</b> (Docs/ECOSYSTEM.md §65):
        /// <see cref="MassAxisScale"/> = x0.64, every position and prism of the Time plant, the shape kept - a SIMILARITY,
        /// so a Mass plant always spans less than a Space one (x1.35). Its shields (3x a prism's reach) are what make it
        /// read heavy. It was the Time plant grown x1.2786 for a few hours on 2026-10-28 ("mass has size"), and before
        /// that it drew IN to x0.82 with the chunky plate.</para>
        ///
        /// <para>A family honours this on whatever field carries its extent
        /// (<c>PhyllotacticFlora.segmentLength</c>, <c>BranchingFlora.branchingScaleFactor</c>).
        /// A family whose leaf IS its strut needs nothing: the anisotropy already lengthened
        /// it.</para>
        /// </summary>
        public static float ReachScale(Element element)
        {
            if (element == Element.Mass)
                return Mathf.Pow(NeutralLeafVolume, -1f / 3f) * MassAxisScale;
            float volume = LeafVolumeScale(element);
            return volume <= 0f ? 1f : Mathf.Pow(volume, -1f / 3f);
        }

        /// <summary>A Mass plant against the neutral (Time) one on every axis: <c>(MassLeafVolume / NeutralLeafVolume)^(1/3)</c>
        /// = 0.64 (it was 1.2786). FITTED, not chosen: the largest plate whose SHIELDS (3x its reach) clear on a flush
        /// tiling spread no wider than SPACE's narrowest span - 2x the anchor's, the Borromean membrane and the
        /// quasicrystal lattice - with a 3% margin, 2 / (3 x 1.03) = 0.647, taken down to 0.64 so the spread stays
        /// strictly inside Space's. "Space should always have greater span" (Garrett, 2026-10-28) is what sets it. A Mass
        /// leaf is the Time leaf times this (<see cref="MassLeafAnisotropy"/> is 1) and a growth-rule Mass plant is the
        /// Time plant scaled by it (<see cref="ReachScale"/>); its shields are what make it read heavier.</summary>
        public static float MassAxisScale => Mathf.Pow(MassLeafVolume / NeutralLeafVolume, 1f / 3f);

        /// <summary>
        /// The element's leaf: the species' authored <paramref name="authored"/> size
        /// redistributed into this element's volume and aspect.
        ///
        /// <para>Decomposes the leaf into a SIZE (its geometric mean) and a unit-volume SHAPE,
        /// scales the size by <see cref="LeafVolumeScale"/> and raises the shape to
        /// <see cref="LeafAnisotropy"/>. The shape term is volume-exact - the product of a
        /// unit-volume vector's components is 1, and 1 to any power is 1 - so aspect and
        /// volume are independent dials and the species' own axis ORDER (which axis is its
        /// long one) is preserved, never chosen here.</para>
        ///
        /// <para>A leaf with a zero or negative component is returned UNCHANGED: that is a
        /// sentinel ("keep the prefab's size") or a degenerate prism, not a shape, and the
        /// decomposition is undefined on it. See the skill's sentinel rule.</para>
        /// </summary>
        public static Vector3 ShapeLeaf(Vector3 authored, Element element)
        {
            if (authored.x <= 0f || authored.y <= 0f || authored.z <= 0f) return authored;

            float volume = LeafVolumeScale(element);
            float anisotropy = LeafAnisotropy(element);
            if (Mathf.Approximately(volume, 1f) && Mathf.Approximately(anisotropy, 1f))
                return authored;

            float mean = Mathf.Pow(authored.x * authored.y * authored.z, 1f / 3f);
            if (mean <= 0f) return authored;

            float size = mean * Mathf.Pow(volume, 1f / 3f);
            return new Vector3(
                size * Mathf.Pow(authored.x / mean, anisotropy),
                size * Mathf.Pow(authored.y / mean, anisotropy),
                size * Mathf.Pow(authored.z / mean, anisotropy));
        }

        /// <summary>
        /// THE TIME LAW'S THIRD CLOCK. A plant owns three clocks and they are all the same
        /// constant: how fast it lays a prism (<c>Flora.ResolveGrowPeriod</c>), how much growth
        /// a child costs it (<c>Flora.ResolveGrowthPerOffspring</c>), and how often a lattice
        /// COLONY births (<c>AssembledFlora.ColonyCyclePeriod</c>). Every one of them is a
        /// "cost per thing" measured in different units, so every one of them divides by
        /// <see cref="FloraReproductionRules.ReproductionRateFor"/> - which is why "Time grows
        /// and regenerates the fastest" is one number rather than three that can drift.
        ///
        /// <para>It composes on top of whatever tempo the SPECIES authored rather than
        /// replacing it: the eight Hesperides phyllotactics already ship a per-element grow
        /// ladder, and this scales it, exactly as the growth quota is scaled on top of the
        /// number <c>author_flora_populations.py</c> wrote.</para>
        /// </summary>
        public static float ScaleGrowPeriod(float authoredSeconds, Element element)
            => FloraReproductionRules.ScaleCostPerChild(
                authoredSeconds, FloraReproductionRules.ReproductionRateFor(element));
    }
}
