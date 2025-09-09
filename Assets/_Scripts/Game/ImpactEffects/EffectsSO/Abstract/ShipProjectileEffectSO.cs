namespace CosmicShore.Game
{
    public abstract class ShipProjectileEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, ProjectileImpactor impactee);
    }
}