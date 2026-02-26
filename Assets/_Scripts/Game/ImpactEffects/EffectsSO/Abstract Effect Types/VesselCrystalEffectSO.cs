
namespace CosmicShore.Game.ImpactEffects
{
    public abstract class VesselCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor vesselImpactor, CrystalImpactData data);
    }
}
