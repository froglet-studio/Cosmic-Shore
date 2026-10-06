using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Who owns which STOLEN prism (Docs/BUILDERS_AND_THIEVES.md §2.3). Integer addressing - a structure prism is keyed
    /// by <c>(colonyId, Vector3Int site)</c>, like <c>SchwarzPTileRegistry</c> / <c>GyroidOctagonRegistry</c> - plus the
    /// set of prisms some worker or thief is carrying right now. One registry for every colony in the scene, so the
    /// shared predicate (<c>IsStealableForMe</c>) can say "never built, never carried" across colonies: a fortress never
    /// strips a thief's hoard and two colonies never fight over one prism in mid-air.
    ///
    /// Nothing here moves, creates or destroys a prism. It is bookkeeping over prisms that already exist, so a built
    /// structure costs no collider of its own (the prisms keep theirs).
    /// </summary>
    public static class BuilderRegistry
    {
        static readonly Dictionary<Prism, (int colony, Vector3Int site)> s_built = new();
        static readonly Dictionary<(int colony, Vector3Int site), Prism> s_bySite = new();
        static readonly HashSet<Prism> s_carried = new();
        static int s_nextColony;

        // Enter Play Mode without a domain reload keeps statics: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_built.Clear();
            s_bySite.Clear();
            s_carried.Clear();
            s_nextColony = 0;
        }

        /// <summary>A fresh colony id (each colony's sites live in their own integer space).</summary>
        public static int NewColonyId() => ++s_nextColony;

        /// <summary>Part of SOME colony's structure (a wall brick, a hoarded prism).</summary>
        public static bool IsBuilt(Prism prism) => prism && s_built.ContainsKey(prism);

        /// <summary>In some worker's or thief's grip right now.</summary>
        public static bool IsCarried(Prism prism) => prism && s_carried.Contains(prism);

        /// <summary>Built structures, for a reader that wants the count (HUD, QA).</summary>
        public static int BuiltCount => s_built.Count;

        public static bool TryGetSite(Prism prism, out int colony, out Vector3Int site)
        {
            if (prism && s_built.TryGetValue(prism, out var e)) { colony = e.colony; site = e.site; return true; }
            colony = 0; site = default;
            return false;
        }

        public static Prism AtSite(int colony, Vector3Int site) =>
            s_bySite.TryGetValue((colony, site), out var p) ? p : null;

        public static void Register(Prism prism, int colony, Vector3Int site)
        {
            if (!prism) return;
            Unregister(prism);
            s_built[prism] = (colony, site);
            s_bySite[(colony, site)] = prism;
            s_carried.Remove(prism);
        }

        public static void Unregister(Prism prism)
        {
            if (!prism || !s_built.TryGetValue(prism, out var e)) return;
            s_built.Remove(prism);
            if (s_bySite.TryGetValue((e.colony, e.site), out var at) && at == prism) s_bySite.Remove((e.colony, e.site));
        }

        public static void MarkCarried(Prism prism, bool carried)
        {
            if (!prism) return;
            if (carried) s_carried.Add(prism);
            else s_carried.Remove(prism);
        }

        /// <summary>A colony went away (its anchor was destroyed): its structure stays in the world as ordinary loose mass -
        /// nothing pops - and stops being anyone's.</summary>
        public static void ReleaseColony(int colony)
        {
            var drop = new List<Prism>();
            foreach (var kv in s_built) if (kv.Value.colony == colony) drop.Add(kv.Key);
            foreach (var p in drop) Unregister(p);
        }
    }
}
