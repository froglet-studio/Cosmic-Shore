using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The pure rule behind the fauna-birth omni-crystal morph (Angler, Jacks:
    /// <c>R_VesselActions/FAUNA_BIRTH_CRYSTAL_MORPH.md</c>): which heart of a newborn creature the
    /// crystal becomes. Kept free of any scene object so the headless harness
    /// (<c>Tools/Build/crystal_morph_harness</c>) runs it.
    ///
    /// Every creature carries its element in one embedded crystal, its heart. A colony (the worm)
    /// is heartless at the root and carries one heart per segment, so a birth can offer several;
    /// the crystal becomes the one NEAREST where it was collected, so it travels the least.
    /// </summary>
    public static class FaunaHeartMorphGeometry
    {
        /// <summary>The index of the heart nearest <paramref name="crystal"/>, or -1 for none. Ties
        /// keep the first, so every peer that lists the same hearts in the same order agrees.</summary>
        public static int NearestHeart(IReadOnlyList<Vector3> hearts, Vector3 crystal)
        {
            if (hearts == null) return -1;
            int best = -1;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < hearts.Count; i++)
            {
                float sq = (hearts[i] - crystal).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            return best;
        }
    }
}
