using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>A trait the resolver decided to PLACE, with everything the assembler needs.</summary>
    public sealed class ResolvedFeature
    {
        public string TraitId;
        public string SourceKey;
        public FeatureKind Kind;
        public string Site;
        public bool Signature;
        public float Weight;
        public FeatureParams Params;
        public float SitePitchDeg, SiteYawDeg, SiteRollDeg;
    }

    /// <summary>One source's covering on this head, with how far it reaches toward the face.</summary>
    public sealed class CoveringLayer
    {
        public string SourceKey;
        public CoveringRecipe Recipe;
        public float Weight;
        public float ReachDeg;
        public bool IsHuman;
    }

    /// <summary>
    /// The resolved description of ONE character: the final head shape, the placed features,
    /// the covering layers and the eye/keratin recipes. Everything downstream — assembler,
    /// painter, bust builder — reads this and never the genome or the clade assets directly.
    /// </summary>
    public sealed class CharacterBlueprint
    {
        public CharacterGenome Genome;
        public HeadShape Shape;
        public CharacterWeights EffectiveWeights;
        public CladeSO Human, CladeA, CladeB;
        public readonly List<ResolvedFeature> Features = new();
        public readonly List<CoveringLayer> Coverings = new();
        public readonly List<string> ExpressedTraitIds = new();
        public EyeParams Eye;
        public Color KeratinA, KeratinB;
        public bool HasBeak, HasMandibles, HasCompoundEyes, HasHumanNose, HasHumanMouth, HasBlowhole;

        public bool HasFeature(FeatureKind kind)
        {
            for (int i = 0; i < Features.Count; i++) if (Features[i].Kind == kind) return true;
            return false;
        }
    }
}
