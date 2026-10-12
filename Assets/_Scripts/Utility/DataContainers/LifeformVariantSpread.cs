using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.Utility
{
    // A LEVEL spread (LifeformLevelSpread: Enabled / MinLevel / MaxLevel / RarityFalloff) used
    // to live here, rolling each spawn a level in 1..5 with higher levels rarer. Level itself is
    // now RETIRED (Docs/ECOSYSTEM.md §40, superseding §33 and the level half of §17): a lifeform
    // is its species and its ELEMENT, and nothing else. The four elemental variations are the
    // whole variation a species has, and each one states everything about itself — body scale,
    // prism shape, tempo, budget, survival numbers and the size of its heart — exactly once.
    //
    // Do not reintroduce either half. A ROLLED level handed a lifeform the record of a life it
    // had not lived; an EARNED one made "how big is this thing" a hidden per-individual history
    // the player could not read off the species, and the three lattice flora could never honour
    // it anyway (two prism sizes cannot tile one lattice). The ELEMENT half of the spread is
    // untouched and still lives on the two config SOs: an element is an identity a lifeform is
    // born with, not an achievement.

    /// <summary>
    /// What one spawn of a species actually is: which element it carries and which variant block
    /// expresses that element. Rolled once per spawn from the species config and then INHERITED
    /// by offspring — a lineage keeps its element rather than re-rolling a new identity every
    /// birth.
    /// </summary>
    public readonly struct LifeformVariantPick<TTuning> where TTuning : class
    {
        public readonly Element Element;
        public readonly TTuning Tuning;

        /// <summary>
        /// The heritable genome riding this pick (Docs/EVOLUTION.md). The founder value (all genes 0) is the
        /// species exactly as authored, which is what every pick carries while a biome's
        /// <c>CellConfigDataSO.Evolution</c> is off. A parent's genome reaches its offspring through the SAME
        /// inherit channel as the element - mutated on the way by <c>Cell.GenomeForOffspring</c> - so there is one
        /// inheritance path, not two (Docs/ECOSYSTEM_MASTERPLAN.md Phase 3).
        /// </summary>
        public readonly LifeformGenome Genome;

        public LifeformVariantPick(Element element, TTuning tuning)
            : this(element, tuning, LifeformGenome.Founder) { }

        public LifeformVariantPick(Element element, TTuning tuning, LifeformGenome genome)
        {
            Element = element;
            Tuning = tuning;
            Genome = genome;
        }

        /// <summary>The same identity carrying a different genome (a founder's roll, or a child's mutation).</summary>
        public LifeformVariantPick<TTuning> WithGenome(LifeformGenome genome) =>
            new LifeformVariantPick<TTuning>(Element, Tuning, genome);
    }
}
