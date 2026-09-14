using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rolls a genome from a seed — a distribution, not a lucky draw. Every random decision the
    /// generator makes is made HERE and written into the genome; nothing downstream draws.
    /// </summary>
    public static class GenomeRoller
    {
        static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        public static CharacterGenome RollChimera(int seed, CladeCatalog catalog, CharacterGenerationConfigSO config)
        {
            var rng = new CharacterRandom(seed);
            var animals = catalog.Animals();
            if (animals.Count < 2) throw new System.InvalidOperationException("GenomeRoller: need at least two non-human clades.");
            int ia = rng.Range(0, animals.Count);
            int ib = rng.Range(0, animals.Count - 1);
            if (ib >= ia) ib++;
            var g = RollCommon(ref rng, seed, config);
            g.CladeA = animals[ia].Key;
            g.CladeB = animals[ib].Key;
            g.Weights = CharacterWeights.Roll(ref rng);
            return g;
        }

        public static CharacterGenome RollHuman(int seed, CharacterGenerationConfigSO config)
        {
            var rng = new CharacterRandom(seed);
            var g = RollCommon(ref rng, seed, config);
            g.CladeA = string.Empty;
            g.CladeB = string.Empty;
            g.Weights = new CharacterWeights { Human = 1f, CladeA = 0f, CladeB = 0f };
            return g;
        }

        /// <summary>Re-roll only the human base + palette, keeping clades and weights.</summary>
        public static CharacterGenome RerollIndividual(CharacterGenome g, int seed, CharacterGenerationConfigSO config)
        {
            var rng = new CharacterRandom(seed);
            var fresh = RollCommon(ref rng, seed, config);
            fresh.CladeA = g.CladeA; fresh.CladeB = g.CladeB; fresh.Weights = g.Weights;
            return fresh;
        }

        static CharacterGenome RollCommon(ref CharacterRandom rng, int seed, CharacterGenerationConfigSO config)
        {
            var g = CharacterGenome.Empty;
            g.Seed = seed;
            float sigma = config != null ? config.HumanVariationSigma : 0.34f;
            float clamp = config != null ? config.HumanVariationClamp : 0.85f;
            var shape = HeadShape.Neutral;
            for (int i = 0; i < HeadShape.AxisCount; i++) shape[(HeadAxis)i] = rng.Gaussian(sigma, clamp);
            g.BaseShape = shape;
            g.Age = rng.Range(0.1f, 0.85f);
            g.Fleshiness = rng.Range(0.15f, 0.85f);
            g.HairVolume = rng.Range(0.2f, 1f);
            g.SkinTone = GeometryKit.SafePow(rng.NextFloat(), 0.9f);
            g.SkinWarmth = rng.NextFloat();
            g.HairShade = GeometryKit.SafePow(rng.NextFloat(), 1.6f);
            g.HairWarmth = rng.NextFloat();
            g.IrisKey = rng.NextFloat();
            g.MarkingKey = rng.NextFloat();
            g.Domain = PlayableDomains[rng.Range(0, PlayableDomains.Length)];
            return g;
        }

        /// <summary>The contact sheet's roster: N chimeras then M humans from one seed.</summary>
        public static List<CharacterGenome> RollSheet(int seed, int chimeras, int humans, CladeCatalog catalog, CharacterGenerationConfigSO config)
        {
            var list = new List<CharacterGenome>(chimeras + humans);
            var rng = new CharacterRandom(seed * 7919 + 13);
            for (int i = 0; i < chimeras; i++) list.Add(RollChimera((int)(rng.NextUInt() & 0x7FFFFFFF), catalog, config));
            for (int i = 0; i < humans; i++) list.Add(RollHuman((int)(rng.NextUInt() & 0x7FFFFFFF), config));
            return list;
        }
    }
}
