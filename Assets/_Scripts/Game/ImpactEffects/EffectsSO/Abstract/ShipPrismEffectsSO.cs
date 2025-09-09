namespace CosmicShore.Game
{
    public abstract class ShipPrismEffectsSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, PrismImpactor impactee);
    }
}