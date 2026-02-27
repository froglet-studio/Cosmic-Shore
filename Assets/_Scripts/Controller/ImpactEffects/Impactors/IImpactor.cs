using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
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