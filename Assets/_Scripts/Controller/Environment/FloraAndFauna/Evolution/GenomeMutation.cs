// Mutation and the random stream it draws from (Docs/EVOLUTION.md §3). Pure C#; exact port in Tools/Evolution/sim.js.
//
// Heredity + RNG must stay server-authoritative and reproducible (Docs/ECOSYSTEM_MASTERPLAN.md §8): only the ONE
// simulation rolls a child's genome (Fauna.TryReproduce is IsSimAuthority-gated) and the result rides the spawn
// payload, so peers never roll. The stream is a seedable xoshiro128** (32-bit state, so the JS port is plain uint32
// arithmetic with no BigInt), and the normal variate is Irwin-Hall (a sum of twelve uniforms) rather than Box-Muller,
// because sqrt/log/cos are not bit-identical across runtimes and twelve additions are.
using System;

namespace CosmicShore.Gameplay
{
    /// <summary>A uniform source in [0, 1). The game adapts its own RNG; the harness and the lab use <see cref="GenomeRng"/>.</summary>
    public interface IGenomeRng
    {
        double NextUnit();
    }

    /// <summary>
    /// xoshiro128** seeded through splitmix32. Deterministic, 24-bit-mantissa uniforms (so a double in C# and a
    /// double in JavaScript are the same number), and small enough to live on every Cell.
    /// </summary>
    public sealed class GenomeRng : IGenomeRng
    {
        uint _s0, _s1, _s2, _s3;

        public GenomeRng(uint seed) => Reseed(seed);

        public void Reseed(uint seed)
        {
            // splitmix32 fills the four words; a zero state is impossible because the first output of a
            // splitmix step is never 0 for all four consecutive steps.
            uint x = seed;
            _s0 = SplitMix(ref x);
            _s1 = SplitMix(ref x);
            _s2 = SplitMix(ref x);
            _s3 = SplitMix(ref x);
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 0x9E3779B9u;
        }

        static uint SplitMix(ref uint x)
        {
            unchecked
            {
                x += 0x9E3779B9u;
                uint z = x;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                return z ^ (z >> 16);
            }
        }

        static uint Rotl(uint x, int k) => (x << k) | (x >> (32 - k));

        public uint NextU32()
        {
            unchecked
            {
                uint result = Rotl(_s1 * 5u, 7) * 9u;
                uint t = _s1 << 9;
                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;
                _s2 ^= t;
                _s3 = Rotl(_s3, 11);
                return result;
            }
        }

        /// <summary>A uniform in [0, 1) with 24 significant bits - exactly representable in both C# and JS doubles.</summary>
        public double NextUnit() => (NextU32() >> 8) * (1.0 / 16777216.0);
    }

    public static class GenomeMutation
    {
        /// <summary>
        /// A standard normal variate by Irwin-Hall: twelve uniforms summed, minus 6. Mean 0, variance 1, bounded to
        /// [-6, 6] (a mutation can never be an outlier of more than six sigmas - which is a feature for a pathway that
        /// must stay non-lethal). Exact across runtimes: additions only.
        /// </summary>
        public static double Normal(IGenomeRng rng)
        {
            double sum = 0.0;
            for (int i = 0; i < 12; i++) sum += rng.NextUnit();
            return sum - 6.0;
        }

        /// <summary>
        /// A child's genome from its parent's: per locus, with probability <see cref="EvolutionSettings.MutationRate"/>,
        /// add a normal step of <see cref="EvolutionSettings.MutationSigma"/>; clamp to the band. With the switch off
        /// (or rate / sigma 0) the parent's genome is returned unchanged and NO random draw is made, so a biome that
        /// runs without mutation does not disturb the stream a biome with mutation would see.
        /// </summary>
        public static LifeformGenome Mutate(in LifeformGenome parent, EvolutionSettings s, IGenomeRng rng)
        {
            if (s == null || !s.Enabled || rng == null) return parent;
            double rate = s.MutationRate, sigma = s.MutationSigma;
            if (!(rate > 0.0) || !(sigma > 0.0)) return parent;

            var genes = new float[LifeformGenome.LocusCount];
            parent.CopyTo(genes);
            for (int i = 0; i < LifeformGenome.LocusCount; i++)
            {
                // The rate draw is skipped at rate >= 1 so the common "every locus drifts" setting costs one draw
                // per locus fewer - and so the JS port, which does the same, stays in step.
                if (rate < 1.0 && rng.NextUnit() >= rate) continue;
                double g = genes[i] + sigma * Normal(rng);
                genes[i] = LifeformGenome.Clamp((float)g);
            }
            return new LifeformGenome(genes[0], genes[1], genes[2], genes[3]);
        }

        /// <summary>
        /// A founder's genome (a creature the SEEDER spawns): each gene a normal draw of
        /// <see cref="EvolutionSettings.FounderSpread"/>, clamped. Spread 0 (or the switch off) is the authored species
        /// exactly, with no draw made.
        /// </summary>
        public static LifeformGenome Founder(EvolutionSettings s, IGenomeRng rng)
        {
            if (s == null || !s.Enabled || rng == null) return LifeformGenome.Founder;
            double spread = s.FounderSpread;
            if (!(spread > 0.0)) return LifeformGenome.Founder;

            var genes = new float[LifeformGenome.LocusCount];
            for (int i = 0; i < LifeformGenome.LocusCount; i++)
                genes[i] = LifeformGenome.Clamp((float)(spread * Normal(rng)));
            return new LifeformGenome(genes[0], genes[1], genes[2], genes[3]);
        }
    }
}
