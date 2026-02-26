
namespace CosmicShore.Game.ImpactEffects
{
    public abstract class ProjectileEndEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, ImpactorBase impactee);
    }
}
