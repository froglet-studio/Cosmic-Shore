namespace CosmicShore.Game
{
    public abstract class OmniCrystalShipEffectSO : ImpactEffectSO
    {
        public abstract void Execute(OmniCrystalImpactor crystalImpactor, ShipImpactor shipImpactee);
    }
}