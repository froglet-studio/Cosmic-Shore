
namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class VesselCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor vesselImpactor, CrystalImpactData data);
    }
}
