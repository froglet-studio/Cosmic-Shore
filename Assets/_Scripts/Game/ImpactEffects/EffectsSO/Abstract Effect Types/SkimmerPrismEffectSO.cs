namespace CosmicShore.Game
{
    public abstract class SkimmerPrismEffectSO : ImpactEffectSO
    {
        public abstract void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee);
    }
}