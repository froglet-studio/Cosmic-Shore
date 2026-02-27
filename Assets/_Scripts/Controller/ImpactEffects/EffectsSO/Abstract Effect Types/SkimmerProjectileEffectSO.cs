namespace CosmicShore.Gameplay
{
    public abstract class SkimmerProjectileEffectSO : ImpactEffectSO
    {
        public abstract void Execute(SkimmerImpactor impactor, ProjectileImpactor projectileImpactee);
    }
}