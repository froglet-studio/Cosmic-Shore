
namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class VesselSkimmerEffectsSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, SkimmerImpactor impactee);
    }
}
