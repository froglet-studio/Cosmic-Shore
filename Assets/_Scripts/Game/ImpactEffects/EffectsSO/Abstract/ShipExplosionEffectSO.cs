namespace CosmicShore.Game
{
    public abstract class ShipExplosionEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, ExplosionImpactor impactee);
    }
}