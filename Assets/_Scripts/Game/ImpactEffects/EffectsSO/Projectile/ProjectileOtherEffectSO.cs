namespace CosmicShore.Game
{
    public abstract class ProjectileOtherEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, ImpactorBase impactee);
    }
}