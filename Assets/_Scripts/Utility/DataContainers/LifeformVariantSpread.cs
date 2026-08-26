using CosmicShore.Data;

namespace CosmicShore.Utility
{
    // A LEVEL spread (LifeformLevelSpread: Enabled / MinLevel / MaxLevel / RarityFalloff) used
    // to live here, rolling each spawn a level in 1..5 with higher levels rarer. Level itself is
    // now RETIRED (Docs/ECOSYSTEM.md §39, superseding §33 and the level half of §17): a lifeform
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

        public LifeformVariantPick(Element element, TTuning tuning)
        {
            Element = element;
            Tuning = tuning;
        }
    }
}
