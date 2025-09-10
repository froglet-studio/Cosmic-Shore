namespace CosmicShore.Game
{
    public abstract class SkimmerProjectileEffectSO : ImpactEffectSO
    {
        public abstract void Execute(SkimmerImpactor impactor, ProjectileImpactor projectileImpactee);
    }
}