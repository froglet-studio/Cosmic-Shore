using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Reuses AOE explosion instances per prefab, so a detonation stops minting a GameObject tree
    /// (lever L13, Docs/PERFORMANCE_OPTIMIZATION.md §3.3). Measured on Amoebius Skim Race I2
    /// (2026-10-15): the Squirrel's omni-collect ring shell, one <c>Instantiate</c> per collect, was
    /// 6.0 of the 6.2 KB/frame <c>VesselImpactor.AcceptImpactee</c> allocated.
    ///
    /// <para>OPT-IN per class through <see cref="AOEExplosion.Poolable"/>. A class says yes only when
    /// everything one detonation writes on the instance is rewritten by the next
    /// <see cref="AOEExplosion.Initialize"/>; the damage blasts are not there yet (their
    /// <see cref="ExplosionImpactor"/> keeps per-blast hit ledgers and friendly-fire overrides that
    /// assume a fresh instance), so they still Instantiate and Destroy as before.</para>
    ///
    /// <para>A pooled instance is parked INACTIVE at the scene root: <c>OnDisable</c> drops its game
    /// event subscriptions and cancels its token, <c>OnEnable</c> takes them back. Parked instances
    /// die with their scene; the stack then holds destroyed references, which <see cref="Get"/>
    /// skips. At most <see cref="MaxParkedPerPrefab"/> are parked per prefab, the rest are destroyed,
    /// so a burst never becomes a standing cost. Main thread only.</para>
    /// </summary>
    public static class AOEExplosionPool
    {
        public const int MaxParkedPerPrefab = 16;

        static readonly Dictionary<AOEExplosion, Stack<AOEExplosion>> s_parked = new();
        static readonly ProfilerMarker s_reuse = new("AOEPool.Reuse");
        static readonly ProfilerMarker s_miss = new("AOEPool.Miss");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_parked.Clear();

        /// <summary>
        /// An active instance of <paramref name="prefab"/>. <paramref name="fresh"/> is true when it
        /// was just Instantiated (the caller injects it then; a reused one keeps its injection).
        /// A prefab whose class is not <see cref="AOEExplosion.Poolable"/> is always Instantiated.
        /// </summary>
        public static AOEExplosion Get(AOEExplosion prefab, out bool fresh)
        {
            if (prefab.Poolable && s_parked.TryGetValue(prefab, out var stack))
            {
                using (s_reuse.Auto())
                {
                    while (stack.Count > 0)
                    {
                        var parked = stack.Pop();
                        if (!parked) continue;   // died with its scene
                        parked.gameObject.SetActive(true);
                        fresh = false;
                        return parked;
                    }
                }
            }

            using (s_miss.Auto())
            {
                var aoe = Object.Instantiate(prefab);
                if (prefab.Poolable) aoe.PoolPrefab = prefab;
                fresh = true;
                return aoe;
            }
        }

        /// <summary>Parks <paramref name="aoe"/> for reuse, or destroys it when it is not pooled or
        /// its prefab already has <see cref="MaxParkedPerPrefab"/> parked.</summary>
        public static void Release(AOEExplosion aoe)
        {
            if (!aoe) return;
            var prefab = aoe.PoolPrefab;
            if (!prefab)
            {
                Object.Destroy(aoe.gameObject);
                return;
            }
            if (!aoe.gameObject.activeSelf) return;   // already parked: a second release must not push it twice

            if (!s_parked.TryGetValue(prefab, out var stack))
                s_parked[prefab] = stack = new Stack<AOEExplosion>(MaxParkedPerPrefab);
            if (stack.Count >= MaxParkedPerPrefab)
            {
                Object.Destroy(aoe.gameObject);
                return;
            }

            aoe.gameObject.SetActive(false);
            stack.Push(aoe);
        }
    }
}
