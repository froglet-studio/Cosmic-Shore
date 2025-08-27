using CosmicShore.Game;

public abstract class ElementalCrystalShipEffectSO : ImpactEffectSO
{
    public abstract void Execute(ElementalCrystalImpactor impactor, ShipImpactor impactee);
}