using CosmicShore.Game;

namespace CosmicShore
{
    public abstract class VesselCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee);
    }
}