using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Pure decision rules for the prey-linked fauna population pipeline
    /// (Docs/ECOSYSTEM.md §6): reproduction (well-fed fauna birth offspring - the
    /// population driver) and seeding (the spawner only tops a species back up to
    /// its seed floor - bootstrap + extinction recovery, NOT the population driver).
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
        /// population at or above the floor - the seeder only matters at bootstrap
        /// and after a crash (starvation / predation wiped the species).
        /// </summary>
        public static int SeedSpawnCount(int livePopulation, int seedFloor, int maxPopulation)
        {
            int deficit = Mathf.Max(0, seedFloor - livePopulation);
            if (maxPopulation > 0)
                deficit = Mathf.Min(deficit, Mathf.Max(0, maxPopulation - livePopulation));
            return deficit;
        }

        /// <summary>
        /// How many fauna a FULL-WAVE tick spawns (SpawnProfileSO.SeedFullWaveEveryTick):
        /// a fresh wave of <paramref name="populationSize"/> every period regardless of the
        /// live count, clamped so the species never exceeds its hard cap. Wave-scored modes
        /// (Brood Rush) use this so every spawn cycle visibly births a brood in the
        /// controlling color; population remains bounded by starvation + the cap.
        /// </summary>
        public static int WaveSpawnCount(int livePopulation, int populationSize, int maxPopulation)
        {
            int wave = Mathf.Max(0, populationSize);
            if (maxPopulation > 0)
                wave = Mathf.Min(wave, Mathf.Max(0, maxPopulation - livePopulation));
            return wave;
        }

        /// <summary>
        /// The prey-linked production gate - no fauna is seeded into famine. A predator needs enough
        /// live herbivores to hunt; a herbivore needs enough opposing ENVIRONMENT volume to graze.
        /// One copy shared by every producer (the cell spawners and the freestyle microscene
        /// conveyor releasing lifeforms into the cell), so the gate can't drift between them.
        /// <paramref name="faunaFoodFloor"/> ≤ 0 means "always produce" (profile-authored).
        /// </summary>
        /// <param name="opposingEnvironmentVolume">Live opposing environment prism volume (NOT fauna bodies).</param>
        /// <param name="faunaFoodFloor">The species' prey floor, authored in nominal prisms.</param>
        public static bool PreyAvailable(bool isPredator, int liveHerbivoreCount, float opposingEnvironmentVolume, int faunaFoodFloor)
        {
            if (faunaFoodFloor <= 0) return true;
            return isPredator
                ? liveHerbivoreCount >= faunaFoodFloor
                : opposingEnvironmentVolume >= faunaFoodFloor * CellPhaseThresholds.NominalPrismVolume;
        }
    }
}
