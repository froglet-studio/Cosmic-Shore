using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The pure geometry behind the hull-absorb omni-crystal morph (<c>R_VesselActions/VESSEL_ABSORB_CRYSTAL_MORPH.md</c>):
    /// the shell around a vessel that a collected omni crystal folds onto and sinks through, on every hull whose omni ability
    /// puts nothing in the world (Brittle Star, Sparrow, Stoat, Orca, Wrasse). Kept free of any scene object so the headless
    /// harness (<c>Tools/Build/crystal_morph_harness</c>) runs it.
    ///
    /// The shell is the ellipsoid inscribed in the hull's own bounds, pulled in by <see cref="Sink"/>, so the plates land
    /// just inside the skin and dissolve there: the vessel takes the crystal in.
    /// </summary>
    public static class VesselAbsorbMorphGeometry
    {
        /// <summary>How far inside the hull's bounds the shell sits, as a fraction of each half-extent.</summary>
        public const float Sink = 0.8f;

        /// <summary>The smallest shell semi-axis (u): a flat or degenerate bound still gives the cast a volume.</summary>
        public const float MinSemiAxis = 0.25f;

        /// <summary>Maps a unit-radius sphere onto the shell, in the frame <paramref name="hullBounds"/> is measured in.</summary>
        public static Matrix4x4 UnitSphereToShell(Bounds hullBounds)
        {
            Vector3 e = hullBounds.extents * Sink;
            e = new Vector3(Mathf.Max(MinSemiAxis, e.x), Mathf.Max(MinSemiAxis, e.y), Mathf.Max(MinSemiAxis, e.z));
            return Matrix4x4.TRS(hullBounds.center, Quaternion.identity, e);
        }

        /// <summary>The bounds of <paramref name="source"/> carried through <paramref name="sourceToFrame"/> (its eight
        /// corners), merged into <paramref name="into"/>; <paramref name="any"/> says whether it already holds a box.</summary>
        public static void Encapsulate(ref Bounds into, ref bool any, Bounds source, Matrix4x4 sourceToFrame)
        {
            Vector3 c = source.center, x = source.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -x.x : x.x, (i & 2) == 0 ? -x.y : x.y, (i & 4) == 0 ? -x.z : x.z);
                var p = sourceToFrame.MultiplyPoint3x4(corner);
                if (!any) { into = new Bounds(p, Vector3.zero); any = true; }
                else into.Encapsulate(p);
            }
        }
    }
}
