namespace CosmicShore.Game
{
    public abstract class ShipShipEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ShipImpactor impactor, ShipImpactor impactee);
    }
}