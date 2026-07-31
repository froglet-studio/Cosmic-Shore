using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The single source of truth for the CPU-side epoch of the clock-material
    /// law (Docs/PRISM_ANIMATION.md). Every animation stamp writes
    /// <see cref="Now"/> as its start time; the shader reads the same clock as
    /// _Time.y (Unity: scaled time since level load), so both sides agree by
    /// construction.
    ///
    /// Notes:
    /// - _Time scales with Time.timeScale, so hitstop / pause freeze prism
    ///   animation for free — desired.
    /// - The clock resets on scene load; stamps never outlive their scene
    ///   (prisms are destroyed with it), so no epoch fixup is needed.
    /// - If _Time.y semantics ever drift from Time.timeSinceLevelLoad on some
    ///   platform, the fallback is a single global _PrismClock uniform written
    ///   once per frame by one publisher (O(1), conforming) — change ONLY this
    ///   class and the shader clock input, nothing else.
    /// </summary>
    public static class PrismClock
    {
        /// <summary>Current clock value — stamp this as every animation's start time.</summary>
        public static float Now => Time.timeSinceLevelLoad;

        /// <summary>
        /// A start time far enough in the past that any animation stamped with it
        /// renders fully settled (used behind loading veils where the bloom would
        /// be invisible anyway — the clock equivalent of CompleteGrowthImmediately).
        /// </summary>
        public static float SettledPast => Now - 1000f;
    }
}
