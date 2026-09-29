using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The FLEET's crystal-morph feel — one asset at <c>Resources/CrystalMorphConfig</c>, read by
    /// every vessel's bespoke omni-crystal retirement.
    ///
    /// It is one asset rather than a field per vessel for the reason
    /// <c>Docs/ECOSYSTEM.md §31</c> already gives about crystal capture: a per-prefab duration is
    /// how that beat drifted to 1 s on two fauna and 3 s on eleven flora, and a pickup should read
    /// the same LENGTH whichever hull took it. What differs per vessel is the SHAPE the crystal
    /// lands on, which is geometry, not feel.
    ///
    /// It is loaded from Resources rather than wired, because the animation is started by the
    /// forged object on EVERY peer — including peers that never ran the effect that minted it — so
    /// there is no inspector reference in reach at the point it is needed.
    /// </summary>
    [CreateAssetMenu(fileName = "CrystalMorphConfig", menuName = "ScriptableObjects/Crystal Morph Config")]
    public class CrystalMorphConfigSO : ScriptableObject
    {
        [Header("Window")]
        [Tooltip("Whole animation, seconds — geometry plus the dissolve tail. Matches the " +
                 "platform's crystal-capture beat (CrystalCaptureConfigSO, 0.44s) so a pickup " +
                 "reads the same length whichever vessel took it and whatever it became.")]
        [Min(0.05f)] public float duration = 0.44f;

        [Tooltip("Fraction of the window the GEOMETRY gets. The remainder is the dissolve that " +
                 "hands the surface over to the real object. The shader is given the geometry " +
                 "half ALONE, so the last staggered solid lands before the hand-off — a stagger " +
                 "is only free if it finishes first.")]
        [Range(0.3f, 1f)] public float morphFraction = 0.85f;

        [Header("Cascade")]
        [Tooltip("How much of the window is spent staggering solids against each other. 0 = the " +
                 "whole cage closes as one; higher = a collapse that travels.")]
        [Range(0f, 0.9f)] public float stagger = 0.35f;

        [Tooltip("Phase of the solid NEAREST the centre. Author this above phaseFar to invert the " +
                 "cascade — the outer cage falling in first rather than last.")]
        [Range(0f, 1f)] public float phaseNear = 1f;

        [Tooltip("Phase of the solid FURTHEST from the centre. Below phaseNear (the default) the " +
                 "outermost struts leave first and the collapse reads as the shell folding in.")]
        [Range(0f, 1f)] public float phaseFar = 0f;

        [Header("Colour")]
        [Tooltip("Fraction of the GEOMETRY half spent carrying the crystal's colour pair onto the " +
                 "target's. Finishing before the hand-off is the point: the two surfaces must " +
                 "already agree when they overlap.")]
        [Range(0.1f, 1f)] public float colourBlendFraction = 0.8f;

        static CrystalMorphConfigSO _instance;

        /// <summary>
        /// The shipped asset, or a defaults instance when it is missing — a morph that cannot find
        /// its tuning still plays at the authored defaults rather than not playing, because the
        /// alternative is a crystal that pops out of existence, which is the law this exists to
        /// keep.
        /// </summary>
        public static CrystalMorphConfigSO Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<CrystalMorphConfigSO>("CrystalMorphConfig");
                if (_instance == null) _instance = CreateInstance<CrystalMorphConfigSO>();
                return _instance;
            }
        }
    }
}
