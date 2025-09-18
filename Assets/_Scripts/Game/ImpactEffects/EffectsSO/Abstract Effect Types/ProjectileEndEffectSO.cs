namespace CosmicShore.Game
{
    public abstract class ProjectileEndEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, ImpactorBase impactee);
    }
}