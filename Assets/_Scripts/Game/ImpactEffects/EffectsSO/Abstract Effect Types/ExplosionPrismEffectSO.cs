namespace CosmicShore.Game
{
    public abstract class ExplosionPrismEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ExplosionImpactor impactor, PrismImpactor prismImpactee);
    }
}