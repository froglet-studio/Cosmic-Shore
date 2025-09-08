namespace CosmicShore.Game
{
    public abstract class ShipCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ShipImpactor shipImpactor, CrystalImpactor crystalImpactee);
    }
}