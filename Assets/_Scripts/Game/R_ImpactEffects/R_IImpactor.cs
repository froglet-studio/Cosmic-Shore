using CosmicShore.Core;
using CosmicShore.Utility;

namespace CosmicShore.Game
{
    public interface R_IImpactor : ITransform
    {
    }

    public interface R_IImpactCollider
    {
        public R_IImpactor Impactor { get; }
    }
}