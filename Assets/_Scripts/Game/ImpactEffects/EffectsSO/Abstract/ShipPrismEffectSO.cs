namespace CosmicShore.Game
{
    public abstract class ShipPrismEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ShipImpactor impactor, PrismImpactor prismImpactee);
    }
}