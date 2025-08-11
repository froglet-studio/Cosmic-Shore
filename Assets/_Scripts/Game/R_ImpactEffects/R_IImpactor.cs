using CosmicShore.Core;

namespace CosmicShore.Game
{
    public interface R_IImpactor
    {
    }

    public interface R_IShipImpactor
    {
        IShip Ship { get; }
    }

    public interface R_IPrismImpactor
    {
        TrailBlock TrailBlock { get; }
    }

    public interface R_IImpactCollider
    {
        public R_IImpactor Impactor { get; }
    }
}