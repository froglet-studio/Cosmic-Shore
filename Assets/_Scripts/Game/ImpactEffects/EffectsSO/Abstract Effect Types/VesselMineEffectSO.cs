
namespace CosmicShore.Game.ImpactEffects
{
    public abstract class VesselMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, MineImpactor impactee);
    }
}
