namespace CosmicShore.Game
{
    public abstract class ShipMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ShipImpactor impactor, MineImpactor impactee);
    }
}