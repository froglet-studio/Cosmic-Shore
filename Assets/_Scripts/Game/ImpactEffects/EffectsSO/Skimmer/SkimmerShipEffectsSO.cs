namespace CosmicShore.Game
{
    public abstract class SkimmerShipEffectsSO : ImpactEffectSO
    {
        public abstract void Execute(SkimmerImpactor impactor, VesselImpactor vesselImpactee);
    }
}