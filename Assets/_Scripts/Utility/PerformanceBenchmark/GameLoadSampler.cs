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

            // Every live prism owns a PrismScaleAnimator, so the scale manager's registered
            // count is a faithful "active prisms" reading without a scene scan.
            var scaleManager = PrismScaleManager.Instance;
            if (scaleManager != null)
                metrics.activePrisms = scaleManager.RegisteredAnimatorCount;

            var effectsManager = PrismEffectsManager.Instance;
            if (effectsManager != null)
            {
                metrics.activeExplosions = effectsManager.ActiveExplosionCount;
                metrics.activeImplosions = effectsManager.ActiveImplosionCount;
            }

            if (gameData != null)
            {
                metrics.activeVessels = gameData.Vessels?.Count ?? 0;
                metrics.activePlayers = gameData.Players?.Count ?? 0;
            }

            return metrics;
        }
    }
}
