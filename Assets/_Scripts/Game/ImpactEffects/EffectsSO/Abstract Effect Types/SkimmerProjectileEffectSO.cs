namespace CosmicShore.Game.ImpactEffects
{
    public abstract class SkimmerProjectileEffectSO : ImpactEffectSO
    {
        public abstract void Execute(SkimmerImpactor impactor, ProjectileImpactor projectileImpactee);
    }
}