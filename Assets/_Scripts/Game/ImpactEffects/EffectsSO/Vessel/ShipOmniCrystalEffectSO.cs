using CosmicShore.Game;

namespace CosmicShore
{
    public abstract class ShipOmniCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ShipImpactor shipImpactor, OmniCrystalImpactor impactee);
    }
}