
namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class VesselMineEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, MineImpactor impactee);
    }
}
