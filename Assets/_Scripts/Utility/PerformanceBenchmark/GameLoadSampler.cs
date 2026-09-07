using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Gameplay-object counts sampled for a single frame. These let a benchmark report
    /// correlate frame cost (ms, draw calls, GC) with the actual workload on screen.
    /// </summary>
    public struct GameLoadMetrics
    {
        public int activePrisms;
        public int activeExplosions;
        public int activeImplosions;
        public int activeVessels;
        public int activePlayers;
    }

    /// <summary>
    /// Reads cheap, allocation-free load counts from the gameplay manager singletons.
    /// Every source is null-guarded so the sampler degrades to 0 in scenes (or tooling
    /// contexts) where a given manager or data container is absent.
    ///
    /// This is the one place the benchmark tool reaches into gameplay systems - kept
    /// isolated so the coupling is easy to find, extend, or remove.
    /// </summary>
    public static class GameLoadSampler
    {
        public static GameLoadMetrics Sample(GameDataSO gameData)
        {
            var metrics = new GameLoadMetrics();

            // The spatial index is THE canonical registry of live prism mass
            // (Docs/SPATIAL_INDEX.md) — its O(1) live count is the faithful
            // "active prisms" reading without a scene scan.
            var spatialIndex = CosmicShore.Gameplay.PrismSpatialIndex.Instance;
            if (spatialIndex != null)
                metrics.activePrisms = spatialIndex.LiveCount;

            // Death explosions are batched entities only (D4 — explosion pool is
            // never Get()d). Implosions are batched death suctions PLUS pooled
            // Grow (Sparrow ReverseSuction still uses PrismImplosion).
            metrics.activeExplosions = PrismDebris.LiveDebrisCount;
            metrics.activeImplosions = PrismDebris.LiveImplosionDebrisCount
                + PrismImplosion.EnabledInstances.Count;

            if (gameData != null)
            {
                metrics.activeVessels = gameData.Vessels?.Count ?? 0;
                metrics.activePlayers = gameData.Players?.Count ?? 0;
            }

            return metrics;
        }
    }
}
