using System.Collections.Generic;
using System.Text;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One voyage's roll (Garrett, 2026-10-31: "each hypersea voyage is its own unique experience"): from the escort's
    /// legs, the worlds this voyage sails and in what order (a threat ramp with a little shuffle, the hungriest world
    /// always last), which variant of each world, and 0-2 named modifiers. A pure function of the escort, the difficulty
    /// and the seed, so the card can show the voyage before it sails and every peer of a party (the seed travels in the
    /// escort's course) rolls the same one.
    /// </summary>
    public static class ArkEscortVariety
    {
        public sealed class RolledVoyage
        {
            public int Seed;
            public readonly List<ArkEscortSO.Leg> Legs = new();
            public readonly List<ArkEscortSO.VoyageModifier> Modifiers = new();
            public readonly List<string> WorldNames = new();
            public readonly List<string> ModifierNames = new();

            /// <summary>"Garland Shallows > Scarab Reef > The Maw" and the modifiers, for the card and the banner.</summary>
            public string Describe()
            {
                var sb = new StringBuilder(string.Join(" > ", WorldNames));
                if (ModifierNames.Count > 0) sb.Append("  |  ").Append(string.Join(", ", ModifierNames));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Roll the voyage. <paramref name="forcedModifiers"/>: -1 rolls them; otherwise a bitmask over the escort's
        /// modifier list (bit i = modifier i), 0 = none. <paramref name="worlds"/>: worlds per voyage, -1 = the escort's.
        /// </summary>
        public static RolledVoyage Roll(ArkEscortSO escort, int difficulty, int seed, int forcedModifiers = -1, int worlds = -1)
        {
            var rolled = new RolledVoyage { Seed = seed };
            if (!escort || escort.Legs.Count == 0) return rolled;
            var rng = new System.Random(seed);

            // ── Which worlds, in what order ──
            var all = escort.Legs;
            int count = worlds >= 0 ? worlds : escort.WorldsPerVoyage;
            count = count <= 0 ? all.Count : Mathf.Min(count, all.Count);
            int last = 0;
            for (int i = 1; i < all.Count; i++)
                if (all[i].Threat >= all[last].Threat) last = i;
            var rest = new List<int>();
            for (int i = 0; i < all.Count; i++) if (i != last) rest.Add(i);
            var picked = new List<int>();
            while (picked.Count < count - 1 && rest.Count > 0)
            {
                int k = PickWeighted(rng, rest, i => Mathf.Max(0f, all[i].Weight));
                picked.Add(rest[k]);
                rest.RemoveAt(k);
            }
            float jitter = escort.RampJitter;
            var keys = new Dictionary<int, float>();
            foreach (int i in picked) keys[i] = all[i].Threat + (float)(rng.NextDouble() * 2.0 - 1.0) * jitter;
            picked.Sort((a, b) => keys[a].CompareTo(keys[b]));
            picked.Add(last);

            // ── Each world's variant ──
            foreach (int i in picked)
            {
                var leg = Clone(all[i]);
                if (leg.Variants is { Count: > 0 })
                {
                    var v = leg.Variants[PickWeighted(rng, leg.Variants, x => Mathf.Max(0f, x.Weight))];
                    if (!string.IsNullOrEmpty(v.Name)) leg.Name = v.Name;
                    if (v.World)
                    {
                        leg.World = v.World;
                        leg.Ladder = new List<CosmicShore.Utility.CellConfigDataSO>(v.Ladder ?? new());
                    }
                    leg.Threat *= Mathf.Max(0f, v.ThreatScale);
                }
                rolled.Legs.Add(leg);
                rolled.WorldNames.Add(leg.Name);
            }

            // ── Modifiers ──
            var mods = escort.Modifiers;
            if (forcedModifiers >= 0)
            {
                for (int i = 0; i < mods.Count && i < 31; i++)
                    if ((forcedModifiers & (1 << i)) != 0) rolled.Modifiers.Add(mods[i]);
            }
            else if (mods.Count > 0)
            {
                var w = escort.ModifierCountWeights;
                float total = Mathf.Max(1e-4f, w.x + w.y + w.z);
                double r = rng.NextDouble() * total;
                int want = r < w.x ? 0 : r < w.x + w.y ? 1 : 2;
                var candidates = new List<int>();
                for (int i = 0; i < mods.Count; i++)
                    if (mods[i] != null && mods[i].MinDifficulty <= difficulty && mods[i].Weight > 0f) candidates.Add(i);
                while (rolled.Modifiers.Count < want && candidates.Count > 0)
                {
                    int k = PickWeighted(rng, candidates, i => mods[i].Weight);
                    var m = mods[candidates[k]];
                    candidates.RemoveAt(k);
                    // Never two of one kind (a doubled swarm reads as a bug, not a twist).
                    if (rolled.Modifiers.Exists(x => x.Kind == m.Kind)) continue;
                    rolled.Modifiers.Add(m);
                }
            }
            foreach (var m in rolled.Modifiers) rolled.ModifierNames.Add(m.Name);
            return rolled;
        }

        static int PickWeighted<T>(System.Random rng, List<T> items, System.Func<T, float> weight)
        {
            float total = 0f;
            foreach (var it in items) total += weight(it);
            if (total <= 0f) return rng.Next(items.Count);
            double r = rng.NextDouble() * total;
            for (int i = 0; i < items.Count; i++)
            {
                r -= weight(items[i]);
                if (r < 0) return i;
            }
            return items.Count - 1;
        }

        static ArkEscortSO.Leg Clone(ArkEscortSO.Leg l) => new()
        {
            Name = l.Name,
            World = l.World,
            Ladder = new List<CosmicShore.Utility.CellConfigDataSO>(l.Ladder ?? new()),
            Threat = l.Threat,
            Hostile = l.Hostile,
            HostileFrom = l.HostileFrom,
            Arrival = l.Arrival,
            Weight = l.Weight,
            Variants = new List<ArkEscortSO.WorldVariant>(l.Variants ?? new()),
        };
    }
}
