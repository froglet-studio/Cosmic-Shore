using CosmicShore.Data;

namespace CosmicShore.Utility
{
    // A LEVEL spread (LifeformLevelSpread: Enabled / MinLevel / MaxLevel / RarityFalloff) used
    // to live here, rolling each spawn a level in 1..5 with higher levels rarer. It is RETIRED
    // (Docs/ECOSYSTEM.md §33, superseding the level half of §17): every lifeform is now born at
    // level 1 and EARNS the rest — a plant by reproducing, a creature by feeding a significant
    // amount — so a rolled spawn level would hand a lifeform the record of a life it has not
    // lived, which is the same class of mistake as a scripted fitness function. Do not
    // reintroduce it. The ELEMENT half of the spread is untouched and still lives on the two
    // config SOs: an element is an identity a lifeform is born with, not an achievement.

    /// <summary>
    /// What one spawn of a species actually is: which element it carries, which variant block
    /// expresses that element, and what level it seeds at. Rolled once per spawn from the
    /// species config and then INHERITED by offspring — a lineage keeps its element rather than
    /// re-rolling a new identity every birth.
    ///
    /// <para><see cref="Level"/> is no longer rolled: it is the config's <c>InitialLevel</c>
    /// verbatim, which is 1 in every shipped asset (the Lifeform Matrix bench is the one caller
    /// that sets it higher). It rides along here because it is applied on the same spawn path,
    /// and because an inherited pick must NOT carry a parent's earned level — acquired growth is
    /// not heritable. See Docs/ECOSYSTEM.md §33.</para>
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
