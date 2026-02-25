using CosmicShore.Utility;
using CosmicShore.Models.Enums;

namespace CosmicShore.Game
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