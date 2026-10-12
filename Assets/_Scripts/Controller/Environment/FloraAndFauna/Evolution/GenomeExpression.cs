// Genotype -> phenotype (Docs/EVOLUTION.md §2). Pure C#; exact port in Tools/Evolution/sim.js.
//
// The map from a gene to a multiplier is deliberately NOT R^g: pow() is not bit-identical across runtimes and the lab's
// parity gate holds this file to its JavaScript port exactly. It is the piecewise-rational
//     f(g) =  1 + (R - 1) g           for g >= 0
//     f(g) =  1 / (1 + (R - 1)(-g))   for g <  0
// which uses only +, -, *, / and has the three properties the design needs:
//   - f(0) = 1 (gene 0 is the authored species), f(+1) = R, f(-1) = 1/R;
//   - f(-g) = 1 / f(g) exactly, so a lean one way multiplies by what the same lean the other way divides by;
//   - it is C1 at 0 (both one-sided slopes are R - 1) and monotonic for every R >= 1, so a small step in the gene is a
//     small step in the trait at every point of the band - the "smooth, non-lethal mutational pathway" the masterplan
//     asks for (Ray's anti-brittleness lesson).
using System;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What one genome DOES to the creature that carries it, computed once at lineage bind. Every member is a
    /// multiplier on an authored number (1 = as authored), except <see cref="Provision"/>, which is the fraction of a
    /// full stomach an offspring is born with (1 = born full, the shipped rule).
    /// </summary>
    public readonly struct LifeformPhenotype
    {
        /// <summary>Speed band multiplier (Boid min/max speed, LightFauna speed).</summary>
        public readonly float Pace;
        /// <summary>Graze / interaction radius multiplier.</summary>
        public readonly float Reach;
        /// <summary>Metabolic upkeep multiplier: the stomach drains this much faster (starvation clock divided by it).</summary>
        public readonly float Upkeep;
        /// <summary>Births per feed multiplier: feeds-per-offspring is DIVIDED by this.</summary>
        public readonly float Fecundity;
        /// <summary>Fraction of a full stomach an offspring is born with, in (0, 1].</summary>
        public readonly float Provision;
        /// <summary>Cohesion radius multiplier.</summary>
        public readonly float Cohesion;

        public LifeformPhenotype(float pace, float reach, float upkeep, float fecundity, float provision, float cohesion)
        {
            Pace = pace;
            Reach = reach;
            Upkeep = upkeep;
            Fecundity = fecundity;
            Provision = provision;
            Cohesion = cohesion;
        }

        /// <summary>The authored species: every multiplier 1, born full.</summary>
        public static LifeformPhenotype Neutral => new LifeformPhenotype(1f, 1f, 1f, 1f, 1f, 1f);

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "pace x{0:0.00} reach x{1:0.00} upkeep x{2:0.00} fecundity x{3:0.00} provision {4:0.00} cohesion x{5:0.00}",
                Pace, Reach, Upkeep, Fecundity, Provision, Cohesion);
    }

    public static class GenomeExpression
    {
        /// <summary>The lowest fraction of a stomach an offspring can be born with - a child is never born starving.</summary>
        public const float MinProvision = 0.1f;

        /// <summary>
        /// The gene -> multiplier map (file header). <paramref name="range"/> is the multiplier at gene +1; a range
        /// below 1 reads as 1 (inert). Computed in double and rounded once, so the JS port rounds at the same point.
        /// </summary>
        public static float Multiplier(float gene, float range)
        {
            double r = EvolutionSettings.SafeRange(range);
            double g = LifeformGenome.Clamp(gene);
            double k = r - 1.0;
            double f = g >= 0.0 ? 1.0 + k * g : 1.0 / (1.0 + k * (-g));
            return (float)f;
        }

        /// <summary>The phenotype a genome expresses under a biome's settings.</summary>
        public static LifeformPhenotype Express(in LifeformGenome genome, EvolutionSettings s)
        {
            if (s == null || !s.Enabled) return LifeformPhenotype.Neutral;

            float pace = Multiplier(genome.Tempo, s.TempoPaceRange);
            float reach = Multiplier(genome.Reach, s.ReachRadiusRange);
            // Upkeep is the product of each trait's own cost map: a creature that leans fast AND far pays both.
            // Computed in double from the two cost multipliers and rounded once (the JS port does the same).
            double upkeepD = (double)Multiplier(genome.Tempo, s.TempoUpkeepRange)
                           * (double)Multiplier(genome.Reach, s.ReachUpkeepRange);
            float upkeep = (float)upkeepD;
            float fecundity = Multiplier(genome.Fecundity, s.FecundityRange);
            // Provisioning is one-sided: an r-lean (gene > 0) births hungrier children; a K-lean keeps them born full.
            float provision = genome.Fecundity > 0f
                ? (float)(1.0 / (double)Multiplier(genome.Fecundity, s.FecundityProvisionRange))
                : 1f;
            if (provision < MinProvision) provision = MinProvision;
            if (provision > 1f) provision = 1f;
            float cohesion = Multiplier(genome.Cohesion, s.CohesionRange);

            return new LifeformPhenotype(pace, reach, upkeep, fecundity, provision, cohesion);
        }

        /// <summary>
        /// The feeds an individual needs per birth under its phenotype: the authored count divided by its fecundity,
        /// never below 1 (a birth always costs at least one feed) and 0 stays 0 (the species does not reproduce).
        /// </summary>
        public static int FeedsPerOffspring(int authored, in LifeformPhenotype p)
        {
            if (authored <= 0) return authored;
            double scaled = Math.Round(authored / Math.Max(1e-6, (double)p.Fecundity), MidpointRounding.AwayFromZero);
            return scaled < 1 ? 1 : (int)scaled;
        }

        /// <summary>
        /// The starvation clock an individual actually runs: the authored seconds divided by its upkeep. 0 (never
        /// starves) stays 0 - a clock the species does not run cannot be bought or sold by a gene.
        /// </summary>
        public static float StarvationSeconds(float authored, in LifeformPhenotype p)
        {
            if (!(authored > 0f)) return authored;
            return (float)(authored / Math.Max(1e-6, (double)p.Upkeep));
        }
    }
}
