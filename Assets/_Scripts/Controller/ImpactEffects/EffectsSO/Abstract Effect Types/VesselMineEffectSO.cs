
namespace CosmicShore.Gameplay
{
    public abstract class VesselMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, MineImpactor impactee);
    }
}
