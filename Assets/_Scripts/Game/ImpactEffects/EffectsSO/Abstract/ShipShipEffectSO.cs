namespace CosmicShore.Game
{
    public abstract class ShipShipEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, VesselImpactor impactee);
    }
}