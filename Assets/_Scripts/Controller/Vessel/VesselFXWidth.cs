using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shared width scaling for a vessel's tail and jets — see <c>Docs/VESSEL_TAIL_AND_JETS.md</c> §3.
    ///
    /// <b>Why this exists:</b> a <see cref="TrailRenderer"/>'s width is a WORLD-space quantity and
    /// is not affected by transform scale, so one authored ribbon width cannot serve this fleet.
    /// The hulls span a 40x range — the Urchin's camera sits 6.67 units back and the Serpent's 250 —
    /// and the shared prefabs are tuned at the Dolphin's 20. Left unscaled, the same ribbon engulfs
    /// the small hulls and disappears on the big ones.
    ///
    /// The scale is authored per vessel as an override on that vessel's own tail/jet instance, and
    /// applied ONCE in Awake by multiplying the prefab's authored width. It is deliberately not a
    /// transform scale (which a TrailRenderer ignores) and not a per-prefab width (which would fork
    /// the shared asset per hull).
    /// </summary>
    static class VesselFXWidth
    {
        static readonly System.Collections.Generic.List<TrailRenderer> Scratch = new();

        /// <summary>
        /// Multiply every TrailRenderer under <paramref name="root"/> by <paramref name="scale"/>.
        /// A scale of 1 is a no-op, which is the common case and costs one comparison.
        /// </summary>
        public static void Apply(Component root, float scale)
        {
            if (Mathf.Approximately(scale, 1f) || scale <= 0f) return;

            Scratch.Clear();
            root.GetComponentsInChildren(true, Scratch);
            for (int i = 0; i < Scratch.Count; i++)
                Scratch[i].widthMultiplier *= scale;
        }
    }
}
