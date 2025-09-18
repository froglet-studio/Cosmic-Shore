namespace CosmicShore.Game
{
    public abstract class VesselSkimmerEffectsSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, SkimmerImpactor impactee);
    }
}