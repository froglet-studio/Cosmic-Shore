namespace CosmicShore.Game
{
    public abstract class VesselProjectileEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, ProjectileImpactor impactee);
    }
}