using CosmicShore.Utility;

namespace CosmicShore.Game
{
    public interface IImpactor : ITransform
    {
    }

    public interface IImpactCollider
    {
        public IImpactor Impactor { get; }
    }
}