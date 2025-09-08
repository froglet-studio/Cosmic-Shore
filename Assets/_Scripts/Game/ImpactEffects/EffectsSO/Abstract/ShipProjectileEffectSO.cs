namespace CosmicShore.Game
{
    public abstract class ShipProjectileEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ShipImpactor impactor, ProjectileImpactor impactee);
    }
}