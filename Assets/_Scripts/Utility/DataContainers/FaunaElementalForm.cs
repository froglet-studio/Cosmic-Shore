using CosmicShore.Data;

namespace CosmicShore.Utility
{
    /// <summary>
    /// What a creature's ELEMENT says about it, beyond its heart (Docs/ECOSYSTEM.md §64.8-64.9).
    ///
    /// <list type="bullet">
    /// <item><b>MASS has SIZE.</b> A Mass variant's body is grown until its heart's swing (the Mass crystal's
    /// depth prisms rising 2.37x) clears it - authored per species as
    /// <see cref="FaunaVariantTuning.BaseBodyScale"/>, because each body's cavity is its own measurement.</item>
    /// <item><b>SPACE takes ROOM.</b> Its school keeps the widest spacing (<see cref="Spread"/>), it senses and
    /// strikes from furthest (<see cref="Reach"/>), and its body is the longest and thinnest at the same volume
    /// (<see cref="FaunaVariantTuning.BodyAspect"/>). The number is the plants' own Space reach
    /// (<see cref="FloraElementalForm.ReachScale"/>), so "Space reaches 1.35x further" is one law in the cell.</item>
    /// </list>
    ///
    /// <para>Pure and Unity-free apart from the element enum, like <see cref="FloraElementalForm"/>. A dial is
    /// multiplied where it is READ, never written back, so a pooled body that comes back as another element cannot
    /// compound it.</para>
    /// </summary>
    public static class FaunaElementalForm
    {
        /// <summary>How much further apart a school of this element keeps (separation and cohesion).</summary>
        public static float Spread(Element element) => element == Element.Space ? FloraElementalForm.ReachScale(Element.Space) : 1f;

        /// <summary>How much further this element senses and strikes (detection, attack and feeding reach).</summary>
        public static float Reach(Element element) => element == Element.Space ? FloraElementalForm.ReachScale(Element.Space) : 1f;
    }
}
