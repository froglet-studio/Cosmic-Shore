namespace CosmicShore.Game
{
    public abstract class VesselCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee);

        public virtual void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
        }
    }
}