namespace CosmicShore.Game
{
    public abstract class MineShipEffectSO : AnyPrismEffectSO
    {
        public abstract void Execute(MineImpactor impactor, ShipImpactor shipImpactee);
    }
}