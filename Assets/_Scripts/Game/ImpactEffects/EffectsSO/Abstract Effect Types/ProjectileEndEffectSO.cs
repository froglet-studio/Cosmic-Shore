
namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class ProjectileEndEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, ImpactorBase impactee);
    }
}
