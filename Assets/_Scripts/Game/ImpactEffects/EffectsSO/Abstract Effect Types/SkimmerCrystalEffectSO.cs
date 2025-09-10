namespace CosmicShore.Game
{
    public abstract class SkimmerCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(SkimmerImpactor impactor, CrystalImpactor  impactee);
    }   
}