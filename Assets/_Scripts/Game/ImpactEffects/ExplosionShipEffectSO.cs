namespace CosmicShore.Game
{
    public abstract class ExplosionShipEffectSO : AnyPrismEffectSO
    {
        public abstract void Execute(ExplosionImpactor impactor, ShipImpactor shipImpactee);
    }
}