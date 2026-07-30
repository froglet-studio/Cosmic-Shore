using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The LEVEL half of the lifeform elemental contract's spread: instead of a species config
    /// seeding every spawn at one authored <c>InitialLevel</c>, a cell can spawn the whole
    /// 1..5 band. Level is a pure scale curve (body + heart crystal), so spreading it adds
    /// NO colliders — the same creature count, a mix of sizes, and the level-5 rarity makes
    /// its bigger crystal drop worth hunting.
    ///
    /// Higher levels are rarer by <see cref="RarityFalloff"/>: the weight of level n is
    /// falloff^-(n - MinLevel), so the default 2 makes each step half as likely as the one
    /// below it (1 → 16/31, 5 → 1/31). Falloff 1 = uniform across the band.
    /// </summary>
    [System.Serializable]
    public class LifeformLevelSpread
    {
        [Tooltip("Master switch — off means every spawn seeds at the config's InitialLevel (legacy).")]
        public bool Enabled = false;

        [Tooltip("Lowest level this species spawns at.")]
        [Range(1, 5)] public int MinLevel = 1;

        [Tooltip("Highest level this species spawns at. Level scales the body AND the elemental " +
                 "crystal it drops on death, so a high band makes the cell read heavier and its " +
                 "kills more rewarding.")]
        [Range(1, 5)] public int MaxLevel = 5;

        [Tooltip("How much rarer each level is than the one below it. 2 = each step half as " +
                 "likely (level 5 is ~1 in 31); 1 = uniform across the band.")]
        [Min(1f)] public float RarityFalloff = 2f;

        /// <summary>
        /// The level a fresh spawn seeds at: a falloff-weighted roll inside the band when
        /// enabled, otherwise the config's authored <paramref name="authoredLevel"/>.
        /// </summary>
        public int Roll(int authoredLevel)
        {
            if (!Enabled) return authoredLevel;

            int min = Mathf.Clamp(Mathf.Min(MinLevel, MaxLevel), 1, 5);
            int max = Mathf.Clamp(Mathf.Max(MinLevel, MaxLevel), 1, 5);
            if (min == max) return min;

            float falloff = Mathf.Max(1f, RarityFalloff);

            float total = 0f;
            for (int level = min; level <= max; level++)
                total += Mathf.Pow(falloff, -(level - min));

            float roll = Random.value * total;
            float cumulative = 0f;
            for (int level = min; level <= max; level++)
            {
                cumulative += Mathf.Pow(falloff, -(level - min));
                if (roll <= cumulative) return level;
            }
            return max;
        }
    }

    /// <summary>
    /// What one spawn of a species actually is: which element it carries, which variant block
    /// expresses that element, and what level it seeds at. Rolled once per spawn from the
    /// species config and then INHERITED by offspring — a lineage keeps its element (and its
    /// spawn level) rather than re-rolling a new identity every birth.
    /// </summary>
    public readonly struct LifeformVariantPick<TTuning> where TTuning : class
    {
        public readonly Element Element;
        public readonly TTuning Tuning;
        public readonly int Level;

        public LifeformVariantPick(Element element, TTuning tuning, int level)
        {
            Element = element;
            Tuning = tuning;
            Level = level;
        }
    }
}
