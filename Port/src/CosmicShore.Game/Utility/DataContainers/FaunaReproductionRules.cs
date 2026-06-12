using CosmicShore.Engine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Pure decision rules for the prey-linked fauna population pipeline
    /// (Docs/ECOSYSTEM.md §6): reproduction (well-fed fauna birth offspring — the
    /// population driver) and seeding (the spawner only tops a species back up to
    /// its seed floor — bootstrap + extinction recovery, NOT the population driver).
    /// Kept static and engine-free so the edit-mode tests can pin the exact
    /// Lotka–Volterra gating without a Unity runtime.
    /// </summary>
    public static class FaunaReproductionRules
    {
        /// <summary>
        /// True when a fauna that just fed should birth offspring.
        /// </summary>
        /// <param name="feedsSinceBirth">Feeds accumulated since the last birth (or spawn).</param>
        /// <param name="feedsPerOffspring">Feeds required per birth; &lt;= 0 disables reproduction for the species.</param>
        /// <param name="secondsSinceLastBirth">Time since this individual last gave birth.</param>
        /// <param name="cooldownSeconds">Minimum seconds between births per individual.</param>
        /// <param name="livePopulation">Species' live population in the cell.</param>
        /// <param name="maxPopulation">Hard per-cell cap (performance backstop); &lt;= 0 = uncapped.</param>
        public static bool ShouldBirth(
            int feedsSinceBirth, int feedsPerOffspring,
            float secondsSinceLastBirth, float cooldownSeconds,
            int livePopulation, int maxPopulation)
        {
            if (feedsPerOffspring <= 0) return false;
            if (feedsSinceBirth < feedsPerOffspring) return false;
            if (secondsSinceLastBirth < cooldownSeconds) return false;
            if (maxPopulation > 0 && livePopulation >= maxPopulation) return false;
            return true;
        }

        /// <summary>
        /// How many fauna the periodic seeder should spawn this tick: the deficit
        /// below the species' seed floor, clamped so seeding never pushes the live
        /// population over the hard cap. Returns 0 while the food web sustains the
        /// population at or above the floor — the seeder only matters at bootstrap
        /// and after a crash (starvation / predation wiped the species).
        /// </summary>
        public static int SeedSpawnCount(int livePopulation, int seedFloor, int maxPopulation)
        {
            int deficit = Mathf.Max(0, seedFloor - livePopulation);
            if (maxPopulation > 0)
                deficit = Mathf.Min(deficit, Mathf.Max(0, maxPopulation - livePopulation));
            return deficit;
        }
    }
}
