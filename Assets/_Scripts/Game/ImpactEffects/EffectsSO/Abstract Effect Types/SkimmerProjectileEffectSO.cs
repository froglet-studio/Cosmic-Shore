namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class SkimmerProjectileEffectSO : ImpactEffectSO
    {
        public abstract void Execute(SkimmerImpactor impactor, ProjectileImpactor projectileImpactee);
    }
}