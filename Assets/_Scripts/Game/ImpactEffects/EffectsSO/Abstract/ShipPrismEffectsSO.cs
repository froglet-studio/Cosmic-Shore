namespace CosmicShore.Game
{
    public abstract class ShipPrismEffectsSO : ImpactEffectSO
    {
        public abstract void Execute(ShipImpactor impactor, PrismImpactor impactee);
    }
}