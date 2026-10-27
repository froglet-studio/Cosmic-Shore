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
    /// x1.35), so "Space reaches further" is one law in the cell. Its body is the LONGEST: stretched along its spine
    /// (<see cref="FaunaVariantTuning.BodyAspect"/>) until it is <see cref="SpaceLengthOverMass"/> the length of the
    /// same species' Mass body, at the prefab's girth, its prisms the leaf law's needles fitted to their stretched
    /// sockets.</item>
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

        /// <summary>How much LONGER a Space body is than the same species' Mass body: the lattice law's own ratio of
        /// their reaches, (Mass leaf volume / Space leaf volume)^(1/3) = 1.647 - in a plant, Space reaches 1.35x where
        /// Mass draws in to 0.82x. A species' Space <see cref="FaunaVariantTuning.BodyAspect"/> is this x its Mass
        /// body scale over its prefab's.</summary>
        public static float SpaceLengthOverMass =>
            Mathf.Pow(FloraElementalForm.MassLeafVolume / FloraElementalForm.SpaceLeafVolume, 1f / 3f);

        /// <summary>The lattice flora's fitting margin: a fitted plate is the largest that clears, less 3%.</summary>
        public const float FitClearance = 0.97f;
    }
}
