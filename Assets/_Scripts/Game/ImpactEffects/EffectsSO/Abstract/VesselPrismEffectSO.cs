namespace CosmicShore.Game
{
    public abstract class VesselPrismEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, PrismImpactor prismImpactee);
    }
}