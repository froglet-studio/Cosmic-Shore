// A biome's evolution knobs (Docs/EVOLUTION.md §3). Authored on CellConfigDataSO, so mutation rate and the cost of
// each trait are tuned PER BIOME like every other ecology number - "richness comes from biome x intensity x heritable
// traits, not bespoke code per case" (Docs/ECOSYSTEM_MASTERPLAN.md §1). Plain C# apart from the inspector attributes:
// the evolution harness compiles this file with a two-line stub for UnityEngine's attribute types.
//
// EVERY DEFAULT BELOW IS INERT UNTIL Enabled IS ON. Off, no genome ever leaves the founder value, no phenotype is
// scaled, nothing is recorded and no snapshot runs - the shipped ecology is bit-for-bit what it was.
using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [Serializable]
    public class EvolutionSettings
    {
        [Tooltip("Master switch. OFF (the default every shipped biome carries) = no heredity beyond the element, no " +
                 "mutation, no phenotype scaling, no ledger: the ecology runs exactly as before. ON = offspring inherit " +
                 "their parent's genome with mutation, the genome scales the traits below, and the cell keeps an " +
                 "EvolutionLedger (FrogletTools > Ecology > Evolution Monitor reads it).")]
        public bool Enabled = false;

        [Header("Mutation (what changes between parent and child)")]
        [Tooltip("Probability, per locus per birth, that the gene mutates at all. 1 = every locus drifts a little every " +
                 "birth (the smooth, non-lethal pathway Docs/ECOSYSTEM_MASTERPLAN.md §3 asks for); lower it to make " +
                 "mutation rarer and larger steps meaningful.")]
        [Range(0f, 1f)] public float MutationRate = 1f;

        [Tooltip("Standard deviation of one mutation step, in gene units (a gene spans -1..+1). 0.08 means a child is " +
                 "usually within +-0.1 of its parent on each locus: small genome change, small phenotype change.")]
        [Min(0f)] public float MutationSigma = 0.08f;

        [Tooltip("Standard deviation of a FOUNDER's genes (a creature the seeder spawns, not a birth). 0 = every " +
                 "founder is the authored species and all variation comes by descent; above 0 the population starts " +
                 "with standing variation for selection to act on from the first death.")]
        [Min(0f)] public float FounderSpread = 0.1f;

        [Header("Expression - the trait a gene moves, as the MULTIPLIER at gene +1 (and its reciprocal at -1)")]
        [Tooltip("TEMPO +1: speed band x this. A gene of -1 divides by it. 1 = the locus is inert.")]
        [Min(1f)] public float TempoPaceRange = 1.5f;
        [Tooltip("TEMPO +1: metabolic upkeep x this - the COST of pace. Authored above TempoPaceRange so a faster " +
                 "creature burns more per unit of ground than a slower one and the optimum depends on how dense the " +
                 "food is; 1.84 is 1.5^1.5, roughly kinetic.")]
        [Min(1f)] public float TempoUpkeepRange = 1.84f;

        [Tooltip("REACH +1: graze / interaction radius x this.")]
        [Min(1f)] public float ReachRadiusRange = 1.5f;
        [Tooltip("REACH +1: metabolic upkeep x this - the cost of sensing further.")]
        [Min(1f)] public float ReachUpkeepRange = 1.36f;

        [Tooltip("FECUNDITY +1: feeds per offspring DIVIDED by this (more births per meal).")]
        [Min(1f)] public float FecundityRange = 2f;
        [Tooltip("FECUNDITY +1: each offspring is born with its stomach DIVIDED by this (an r-strategist's many " +
                 "children are born hungrier). Below gene 0 provisioning stays full - the locus only costs there, " +
                 "which is why evolution never leaves the founder on this axis unless more births pay.")]
        [Min(1f)] public float FecundityProvisionRange = 2f;

        [Tooltip("COHESION +1: cohesion radius x this (a looser pack); -1 divides it (a tighter one).")]
        [Min(1f)] public float CohesionRange = 1.6f;

        [Header("Ledger (the evidence - Docs/EVOLUTION.md §6)")]
        [Tooltip("Seconds between trait censuses. Each census is one line per species on the Ecology log channel " +
                 "(off by default) and one row in the ledger the monitor window draws.")]
        [Min(1f)] public float SnapshotIntervalSeconds = 30f;

        [Tooltip("Rows the ledger keeps per species before the oldest are dropped. 2048 rows at 30 s is 17 hours.")]
        [Min(16)] public int MaxSnapshotsPerSpecies = 2048;

        /// <summary>The shipped defaults with the switch ON - what the harness and the lab run as "shipped".</summary>
        public static EvolutionSettings Default(bool enabled = true) => new EvolutionSettings { Enabled = enabled };

        /// <summary>A copy with mutation switched off (the "mutation OFF" control: heredity without variation).</summary>
        public EvolutionSettings WithoutMutation()
        {
            var c = Clone();
            c.MutationRate = 0f;
            c.MutationSigma = 0f;
            return c;
        }

        public EvolutionSettings Clone() => (EvolutionSettings)MemberwiseClone();

        /// <summary>
        /// Every range is a multiplier at gene +1 and must be at least 1; a NaN or a value below 1 is read as 1
        /// (inert) rather than inverting the trait. Called on the way into expression, so an asset edited by hand
        /// cannot flip a locus.
        /// </summary>
        public static float SafeRange(float range) => float.IsNaN(range) || range < 1f ? 1f : range;
    }
}
