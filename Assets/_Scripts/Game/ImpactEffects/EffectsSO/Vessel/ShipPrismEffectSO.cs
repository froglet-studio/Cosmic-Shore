namespace CosmicShore.Game
{
    public abstract class ShipPrismEffectSO : AnyPrismEffectSO
    {
        public abstract void Execute(ShipImpactor impactor, PrismImpactor prismImpactee);
    }
}