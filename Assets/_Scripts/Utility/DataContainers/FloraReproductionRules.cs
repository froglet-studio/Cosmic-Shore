using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Pure decision rules for the flora population pipeline - the plant-side twin of
    /// <see cref="FaunaReproductionRules"/> (Docs/ECOSYSTEM.md §6.1, §32).
    ///
    /// <para><b>A plant's "feeding" is GROWTH.</b> A creature converts prey into offspring; a
    /// plant converts the space it managed to grow into into offspring. That is the whole
    /// analogy, and it is what keeps the population bounded WITHOUT an imposed death clock: a
    /// full plant has stopped growing, so it has stopped funding children, and it only funds
    /// another one after the food web grazes it and it regrows. Nothing is culled, no prism
    /// ages out, and the down-force is the same food web that bounds fauna (Docs/ECOSYSTEM.md
    /// §0 - mass is conserved).</para>
    ///
    /// <para>Kept static and engine-free (bar <see cref="Mathf"/>) so the edit-mode tests can
    /// pin the gating without a Unity runtime.</para>
    /// </summary>
    public static class FloraReproductionRules
    {
        /// <summary>
        /// True when a plant that just grew should seed an offspring.
        /// </summary>
        /// <param name="growthSinceBirth">Prisms this plant has grown since its last birth (or since it was planted).</param>
        /// <param name="growthPerOffspring">Prisms of growth required per birth; &lt;= 0 disables reproduction for the species.</param>
        /// <param name="secondsSinceLastBirth">Time since this individual last seeded.</param>
        /// <param name="cooldownSeconds">Minimum seconds between births per individual.</param>
        /// <param name="livePrisms">Prisms this plant currently holds.</param>
        /// <param name="prismBudget">This plant's live-prism budget (<c>MaxTotalSpawnedObjects</c>); &lt;= 0 skips the maturity gate.</param>
        /// <param name="maturityFraction">Fraction of its own budget the plant must hold to seed; 0 = no maturity gate.</param>
        /// <param name="livePopulation">Species' live population in the cell.</param>
        /// <param name="maxPopulation">Hard per-cell cap (performance backstop); &lt;= 0 = uncapped.</param>
        public static bool ShouldSeed(
            int growthSinceBirth, int growthPerOffspring,
            float secondsSinceLastBirth, float cooldownSeconds,
            int livePrisms, int prismBudget, float maturityFraction,
            int livePopulation, int maxPopulation)
        {
            if (growthPerOffspring <= 0) return false;
            if (growthSinceBirth < growthPerOffspring) return false;
            if (secondsSinceLastBirth < cooldownSeconds) return false;
            if (maxPopulation > 0 && livePopulation >= maxPopulation) return false;

            // Maturity: a plant grazed down to a stub has still ACCUMULATED its growth quota
            // across several graze-and-regrow cycles, and a near-dead plant seeding a child
            // reads as the wrong thing. Off by default (0), so a species that does not author
            // it behaves purely on the growth quota.
            if (maturityFraction > 0f && prismBudget > 0 &&
                livePrisms < Mathf.FloorToInt(prismBudget * Mathf.Clamp01(maturityFraction)))
                return false;

            return true;
        }

        /// <summary>
        /// How many plants the periodic spawner should seed this tick: the deficit below the
        /// species' seed floor, clamped so seeding never pushes the live population over the
        /// hard cap. Returns 0 while reproduction sustains the species at or above the floor -
        /// the spawner only matters at bootstrap and after a crash (the food web grazed the
        /// species out).
        ///
        /// <para>Deliberately a sibling of <see cref="FaunaReproductionRules.SeedSpawnCount"/>
        /// rather than a call into it: the arithmetic is the same today, but the flora seeder's
        /// inputs (prepared planting sites, per-plant prism budget) are the ones expected to
        /// diverge, and a shared helper would have to grow a flag the day they do.</para>
        /// </summary>
        public static int SeedSpawnCount(int livePopulation, int seedFloor, int maxPopulation)
        {
            int deficit = Mathf.Max(0, seedFloor - livePopulation);
            if (maxPopulation > 0)
                deficit = Mathf.Min(deficit, Mathf.Max(0, maxPopulation - livePopulation));
            return deficit;
        }

        /// <summary>
        /// True when a species has opted into the population model at all. A species that
        /// authors no seed floor keeps the legacy behaviour - the spawner plants one every
        /// plant period, bounded only by the cell's Frenzy gate - so an unauthored or
        /// toy-cloned config is never silently muted.
        /// </summary>
        public static bool HasPopulationModel(int seedFloor) => seedFloor > 0;
    }
}
