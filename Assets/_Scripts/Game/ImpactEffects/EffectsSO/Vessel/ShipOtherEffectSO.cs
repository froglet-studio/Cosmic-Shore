using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class ShipOtherEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ShipImpactor impactor, ImpactorBase impactee);
    }
}