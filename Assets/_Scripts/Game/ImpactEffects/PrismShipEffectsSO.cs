namespace CosmicShore.Game
{
    public abstract class PrismShipEffectsSO : AnyPrismEffectSO
    {
        public abstract void Execute(PrismImpactor impactor, ShipImpactor shipImpactee);
    }
}