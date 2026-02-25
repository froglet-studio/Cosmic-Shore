using CosmicShore.Utility.Recording;
using CosmicShore.Models.Enums;

namespace CosmicShore.Game.ImpactEffects.Impactors
{
    public interface IImpactor : ITransform
    {
        public Domains OwnDomain { get; }
    }

    public interface IImpactCollider
    {
        public IImpactor Impactor { get; }
    }
}