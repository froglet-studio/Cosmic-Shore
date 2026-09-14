using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The three source weights of a chimera — human, clade A, clade B — summing to 1 with each
    /// held inside [<see cref="Min"/>, <see cref="Max"/>], so every source is always present and
    /// none can dominate. Equal thirds is the centroid of that region, not the rule.
    /// A pure human control is NOT a weight triplet: it is a genome with no clades
    /// (<see cref="CharacterGenome.IsPureHuman"/>), and this struct is never asked about it.
    /// </summary>
    [Serializable]
    public struct CharacterWeights
    {
        public const float Min = 0.20f;
        public const float Max = 0.60f;
        const float SumTolerance = 1e-4f;

        public float Human;
        public float CladeA;
        public float CladeB;

        public static CharacterWeights EqualThirds => new CharacterWeights { Human = 1f / 3f, CladeA = 1f / 3f, CladeB = 1f / 3f };

        public float this[int source] => source == 0 ? Human : (source == 1 ? CladeA : CladeB);

        public bool IsValid =>
            InRange(Human) && InRange(CladeA) && InRange(CladeB)
            && Mathf.Abs(Human + CladeA + CladeB - 1f) <= SumTolerance;

        static bool InRange(float w) => w >= Min - 1e-5f && w <= Max + 1e-5f;

        /// <summary>
        /// Project an arbitrary triplet onto the valid region: clamp, renormalise, and repeat
        /// until it settles (the region is convex, so alternating the two projections converges
        /// in a handful of passes). This is what the inspector sliders route through, so a slider
        /// can never leave the triplet somewhere the resolver refuses.
        /// </summary>
        public static CharacterWeights Constrain(float human, float a, float b)
        {
            var w = new CharacterWeights { Human = human, CladeA = a, CladeB = b };
            for (int pass = 0; pass < 12; pass++)
            {
                w.Human = Mathf.Clamp(w.Human, Min, Max);
                w.CladeA = Mathf.Clamp(w.CladeA, Min, Max);
                w.CladeB = Mathf.Clamp(w.CladeB, Min, Max);
                float sum = w.Human + w.CladeA + w.CladeB;
                if (sum <= 0f) return EqualThirds;
                w.Human /= sum; w.CladeA /= sum; w.CladeB /= sum;
                if (w.IsValid) return w;
            }
            // Not reachable for finite inputs; the centroid is the honest fallback.
            return w.IsValid ? w : EqualThirds;
        }

        /// <summary>
        /// Constrain while HOLDING one source at the value the user just set (index 0 = human,
        /// 1 = A, 2 = B). The other two share the remainder in their current ratio. This is what
        /// makes dragging one slider feel like it owns that number.
        /// </summary>
        public static CharacterWeights ConstrainHolding(CharacterWeights current, int held, float value)
        {
            value = Mathf.Clamp(value, Min, Max);
            float remainder = 1f - value;
            int i = (held + 1) % 3, j = (held + 2) % 3;
            float wi = current[i], wj = current[j];
            float ratio = (wi + wj) > 1e-6f ? wi / (wi + wj) : 0.5f;
            float vi = remainder * ratio, vj = remainder - vi;
            // The remainder must be splittable inside [Min, Max]²: push the pair back in.
            vi = Mathf.Clamp(vi, Min, Max); vj = remainder - vi;
            vj = Mathf.Clamp(vj, Min, Max); vi = remainder - vj;
            float[] v = new float[3];
            v[held] = value; v[i] = vi; v[j] = vj;
            return Constrain(v[0], v[1], v[2]);
        }

        /// <summary>Uniform over the valid region by rejection on the simplex.</summary>
        public static CharacterWeights Roll(ref CharacterRandom rng)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                float h = rng.Range(Min, Max);
                float a = rng.Range(Min, Max);
                float b = 1f - h - a;
                if (b >= Min && b <= Max) return new CharacterWeights { Human = h, CladeA = a, CladeB = b };
            }
            return EqualThirds;
        }

        public override string ToString() => $"H{Human:0.00} A{CladeA:0.00} B{CladeB:0.00}";
    }
}
