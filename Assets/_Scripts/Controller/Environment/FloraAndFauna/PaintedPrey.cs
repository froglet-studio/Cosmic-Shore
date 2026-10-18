using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The prisms an Angler has PAINTED (lit, and not yet eaten or erased), which every creature finds more desirable
    /// than the rest of its food (the designer, 2026-10-16; <c>R_VesselActions/ANGLER.md</c> §1c; <c>Docs/ECOSYSTEM.md</c>
    /// §61.5).
    ///
    /// <para><b>Preference, never permission.</b> A painted prism only RANKS closer in a forager's own food search
    /// (<see cref="Rank"/>, read by <c>LightFauna</c> and <c>Boid</c> where they pick the nearest edible prism). It is still
    /// tested by the creature's own diet, so shielded mass, the nucleus interior and a body prism stay off the menu, and
    /// it is only ever seen inside the creature's own sensing radius. Nothing here moves a goal or feeds anyone.</para>
    ///
    /// <para><b>Who writes it.</b> Every Angler executor, on every peer, from the same replicated trigger input that paints;
    /// so the ecology's sim authority holds the same marks the pilot sees. Each mark is counted per holder, and a holder
    /// removes its own marks when the prism is eaten or erased, when its turn ends and when it is disabled. A pooled prism
    /// forgets its marks on reuse (<see cref="Forget"/>, from <c>Prism.Initialize</c>).</para>
    /// </summary>
    public static class PaintedPrey
    {
        static readonly Dictionary<Prism, int> s_marks = new();

        /// <summary>How many times closer a painted prism ranks than an unpainted one at the same distance (1 = no
        /// preference). Set by the Angler executors from <c>AnglerConfigSO.PaintedPreference</c>.</summary>
        public static float Preference { get; set; } = 1f;

        public static int Count => s_marks.Count;

        public static void Mark(Prism prism)
        {
            if (!prism) return;
            s_marks[prism] = s_marks.TryGetValue(prism, out int n) ? n + 1 : 1;
        }

        public static void Unmark(Prism prism)
        {
            if (prism is null || !s_marks.TryGetValue(prism, out int n)) return;
            if (n <= 1) s_marks.Remove(prism);
            else s_marks[prism] = n - 1;
        }

        /// <summary>A pooled prism starting a new life carries no marks from its last one.</summary>
        public static void Forget(Prism prism)
        {
            if (s_marks.Count > 0 && prism is not null) s_marks.Remove(prism);
        }

        public static bool IsPainted(Prism prism) => s_marks.Count > 0 && prism && !prism.destroyed && s_marks.ContainsKey(prism);

        /// <summary>The squared distance a forager ranks <paramref name="prism"/> by: a painted prism ranks as if
        /// <see cref="Preference"/> times closer. Unpainted prisms (and every prism while nothing is painted) are
        /// ranked by their plain distance, exactly as before.</summary>
        public static float Rank(Prism prism, float sqrDistance)
        {
            if (s_marks.Count == 0 || Preference <= 1f || !IsPainted(prism)) return sqrDistance;
            return sqrDistance / (Preference * Preference);
        }
    }
}
