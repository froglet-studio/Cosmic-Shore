using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Shared <see cref="WaitForSeconds"/> instances, so a coroutine that loops on a timer stops allocating one per
    /// tick (the per-frame allocation scan's "yield return new ... inside a loop", 63 sites on 2026-10-16; the hot
    /// ones are per creature and per vessel). A <see cref="WaitForSeconds"/> carries only its duration and Unity
    /// lets any number of coroutines yield the same instance, so sharing it is safe. Main thread only, like every
    /// coroutine.
    ///
    /// <para><see cref="Seconds"/> keys on the exact duration: timing is unchanged, for anything a player feels
    /// (fire rate, ability ticks, AI timers) and any cadence read from config. <see cref="About"/> rounds to
    /// <see cref="Step"/> (10 ms) first, for cadences that are randomised per tick or per creature (a jittered
    /// behaviour tick), where an exact key would make every tick a new entry. Both stop adding entries at
    /// <see cref="MaxEntries"/> and fall back to a fresh instance, so a caller passing unbounded values is still
    /// correct, only no longer free.</para>
    ///
    /// <para><see cref="WaitForSecondsRealtime"/> is NOT cached here: it keeps per-use state, so one instance
    /// cannot be shared between coroutines.</para>
    /// </summary>
    public static class WaitCache
    {
        public const float Step = 0.01f;
        public const int MaxEntries = 256;

        static readonly Dictionary<float, WaitForSeconds> s_exact = new();
        static readonly Dictionary<int, WaitForSeconds> s_rounded = new();

        /// <summary>A shared wait of exactly <paramref name="seconds"/>.</summary>
        public static WaitForSeconds Seconds(float seconds)
        {
            if (s_exact.TryGetValue(seconds, out var wait)) return wait;
            wait = new WaitForSeconds(seconds);
            if (s_exact.Count < MaxEntries) s_exact[seconds] = wait;
            return wait;
        }

        /// <summary>A shared wait of <paramref name="seconds"/> rounded to the nearest <see cref="Step"/> (at most
        /// 5 ms off). For randomised cadences only.</summary>
        public static WaitForSeconds About(float seconds)
        {
            int key = Mathf.Max(0, Mathf.RoundToInt(seconds / Step));
            if (s_rounded.TryGetValue(key, out var wait)) return wait;
            wait = new WaitForSeconds(key * Step);
            if (s_rounded.Count < MaxEntries) s_rounded[key] = wait;
            return wait;
        }
    }
}
