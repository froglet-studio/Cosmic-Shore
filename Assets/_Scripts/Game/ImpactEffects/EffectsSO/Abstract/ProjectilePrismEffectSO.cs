namespace CosmicShore.Game
{
    public abstract class ProjectilePrismEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee);
    }
}