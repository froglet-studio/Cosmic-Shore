namespace CosmicShore.Game
{
    public abstract class VesselMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, MineImpactor impactee);
    }
}