// The heritable genome (Docs/EVOLUTION.md; Docs/ECOSYSTEM_MASTERPLAN.md Phase 3 -> 4). Pure C#: no UnityEngine, so the
// evolution harness (Tools/Build/evolution_harness) compiles and RUNS it headless against netstandard2.1, and the Darwin
// Lab (Tools/Evolution/sim.js) carries an exact port that a parity gate holds to this file.
//
// A genome is a SMALL FIXED VECTOR of genes, each in [-1, 1]. Gene 0 is the species exactly as authored; the sign says
// which way the trait leans and the magnitude how far. It rides the inheritance channel that already exists
// (LifeformVariantPick, Docs/ECOSYSTEM.md §17): a parent's pick goes to its offspring, and the genome inside it is
// MUTATED on the way (GenomeMutation). Nothing else assigns a gene: there is no fitness function, no scripted outcome -
// the economy (starvation, predation, reproduction) decides who leaves descendants, which is the whole of the
// "endogenous selection only" invariant.
//
// The genome does NOT touch a lifeform's SIZE or its HEART. Docs/ECOSYSTEM.md §40 retired every per-individual size axis
// ("a lifeform is its species and its element"), and this file keeps that: every locus below is behaviour or
// metabolism, read off the creature's motion, never off its silhouette.
using System;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The loci of <see cref="LifeformGenome"/>. Static numeric values: a locus index is the gene's slot in the struct,
    /// its byte in the network payload (<see cref="LifeformGenome.Pack"/>) and its column in every ledger export, so a
    /// renumbering would silently re-label every recorded trajectory. Add new loci at the END and raise
    /// <see cref="LifeformGenome.LocusCount"/>.
    /// </summary>
    public enum GenomeLocus
    {
        /// <summary>Pace against upkeep: a faster creature reaches food sooner and burns its stomach faster.</summary>
        Tempo = 0,
        /// <summary>Sensing against upkeep: a longer graze / interaction radius finds more mass and costs more to run.</summary>
        Reach = 1,
        /// <summary>r against K: fewer feeds per birth, and each offspring born with a smaller stomach.</summary>
        Fecundity = 2,
        /// <summary>Flock tightness: how close a creature keeps to its pack (cohesion radius against goal pull).</summary>
        Cohesion = 3,
    }

    /// <summary>
    /// One individual's genes: <see cref="LocusCount"/> values in [-1, 1], stored as floats. Immutable; every edit
    /// returns a new genome (<see cref="With"/>), which is what makes a parent's genome safe to hand to many offspring.
    /// </summary>
    public readonly struct LifeformGenome : IEquatable<LifeformGenome>
    {
        /// <summary>Number of loci. Must equal the count of <see cref="GenomeLocus"/> members (asserted in the tests).</summary>
        public const int LocusCount = 4;

        /// <summary>A gene never leaves this band; mutation clamps to it.</summary>
        public const float GeneMin = -1f, GeneMax = 1f;

        readonly float _g0, _g1, _g2, _g3;

        public LifeformGenome(float tempo, float reach, float fecundity, float cohesion)
        {
            _g0 = Clamp(tempo);
            _g1 = Clamp(reach);
            _g2 = Clamp(fecundity);
            _g3 = Clamp(cohesion);
        }

        /// <summary>The species exactly as authored: every gene 0. <c>default(LifeformGenome)</c> is the same value.</summary>
        public static LifeformGenome Founder => default;

        /// <summary>True when every gene is exactly 0 - the authored species, untouched by any mutation.</summary>
        public bool IsFounder => _g0 == 0f && _g1 == 0f && _g2 == 0f && _g3 == 0f;

        public float this[GenomeLocus locus] => this[(int)locus];

        public float this[int locus]
        {
            get
            {
                switch (locus)
                {
                    case 0: return _g0;
                    case 1: return _g1;
                    case 2: return _g2;
                    case 3: return _g3;
                    default: throw new ArgumentOutOfRangeException(nameof(locus), locus, "no such locus");
                }
            }
        }

        public float Tempo => _g0;
        public float Reach => _g1;
        public float Fecundity => _g2;
        public float Cohesion => _g3;

        /// <summary>A copy with one gene replaced (clamped to the band).</summary>
        public LifeformGenome With(GenomeLocus locus, float value)
        {
            switch (locus)
            {
                case GenomeLocus.Tempo: return new LifeformGenome(value, _g1, _g2, _g3);
                case GenomeLocus.Reach: return new LifeformGenome(_g0, value, _g2, _g3);
                case GenomeLocus.Fecundity: return new LifeformGenome(_g0, _g1, value, _g3);
                case GenomeLocus.Cohesion: return new LifeformGenome(_g0, _g1, _g2, value);
                default: throw new ArgumentOutOfRangeException(nameof(locus), locus, "no such locus");
            }
        }

        /// <summary>Copies the genes into <paramref name="dst"/> (length at least <see cref="LocusCount"/>).</summary>
        public void CopyTo(float[] dst, int offset = 0)
        {
            dst[offset + 0] = _g0;
            dst[offset + 1] = _g1;
            dst[offset + 2] = _g2;
            dst[offset + 3] = _g3;
        }

        /// <summary>Euclidean distance between two genomes in gene units (0 = identical, at most 2*sqrt(LocusCount)).</summary>
        public static float Distance(in LifeformGenome a, in LifeformGenome b)
        {
            double d0 = a._g0 - b._g0, d1 = a._g1 - b._g1, d2 = a._g2 - b._g2, d3 = a._g3 - b._g3;
            return (float)Math.Sqrt(d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3);
        }

        // ------------------------------------------------------------------------------------------------------------
        //  Wire format - one uint, one signed byte per locus. 127 steps per unit of gene is ~0.008 resolution, finer
        //  than any mutation sigma a biome authors (EvolutionSettings.MutationSigma, 0.08 default), so a creature
        //  rebuilt from the wire expresses the same phenotype to within float noise. Quantization is round-to-nearest
        //  so the founder (0) is exactly preserved and the band ends (+-1) map to +-127.
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>Quantizes a gene to a signed byte in [-127, 127].</summary>
        public static sbyte Quantize(float gene)
        {
            double q = Math.Round(Clamp(gene) * 127.0, MidpointRounding.AwayFromZero);
            if (q > 127) q = 127;
            if (q < -127) q = -127;
            return (sbyte)q;
        }

        /// <summary>The gene a quantized byte stands for.</summary>
        public static float Dequantize(sbyte q) => Clamp((float)(q / 127.0));

        /// <summary>Packs the genome into one uint: locus i is byte i (little end first).</summary>
        public uint Pack()
        {
            uint b0 = (byte)Quantize(_g0), b1 = (byte)Quantize(_g1), b2 = (byte)Quantize(_g2), b3 = (byte)Quantize(_g3);
            return b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
        }

        /// <summary>The inverse of <see cref="Pack"/>.</summary>
        public static LifeformGenome Unpack(uint packed) =>
            new LifeformGenome(
                Dequantize((sbyte)(byte)(packed & 0xFF)),
                Dequantize((sbyte)(byte)((packed >> 8) & 0xFF)),
                Dequantize((sbyte)(byte)((packed >> 16) & 0xFF)),
                Dequantize((sbyte)(byte)((packed >> 24) & 0xFF)));

        /// <summary>The genome as it survives a round trip over the wire (what every peer actually expresses).</summary>
        public LifeformGenome Quantized() => Unpack(Pack());

        public static float Clamp(float g)
        {
            if (float.IsNaN(g)) return 0f;
            return g < GeneMin ? GeneMin : g > GeneMax ? GeneMax : g;
        }

        public bool Equals(LifeformGenome other) =>
            _g0 == other._g0 && _g1 == other._g1 && _g2 == other._g2 && _g3 == other._g3;

        public override bool Equals(object obj) => obj is LifeformGenome g && Equals(g);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = _g0.GetHashCode();
                h = (h * 397) ^ _g1.GetHashCode();
                h = (h * 397) ^ _g2.GetHashCode();
                h = (h * 397) ^ _g3.GetHashCode();
                return h;
            }
        }

        public static bool operator ==(LifeformGenome a, LifeformGenome b) => a.Equals(b);
        public static bool operator !=(LifeformGenome a, LifeformGenome b) => !a.Equals(b);

        /// <summary>Short, log-friendly: <c>T+0.12 R-0.30 F+0.05 C+0.00</c>.</summary>
        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "T{0:+0.00;-0.00} R{1:+0.00;-0.00} F{2:+0.00;-0.00} C{3:+0.00;-0.00}", _g0, _g1, _g2, _g3);

        /// <summary>The short label the ledger and the lab use for a locus column.</summary>
        public static string LocusLabel(GenomeLocus locus)
        {
            switch (locus)
            {
                case GenomeLocus.Tempo: return "tempo";
                case GenomeLocus.Reach: return "reach";
                case GenomeLocus.Fecundity: return "fecundity";
                case GenomeLocus.Cohesion: return "cohesion";
                default: return locus.ToString();
            }
        }
    }
}
