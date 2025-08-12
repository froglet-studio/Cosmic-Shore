using CosmicShore.Core;

namespace CosmicShore.Game
{
    public interface R_IImpactor
    {
    }

    public interface R_IImpactCollider
    {
        public R_IImpactor Impactor { get; }
    }
}