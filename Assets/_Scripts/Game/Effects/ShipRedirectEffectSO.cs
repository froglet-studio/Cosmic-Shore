using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipRedirectEffect", menuName = "ScriptableObjects/Impact Effects/ShipRedirectEffectSO")]
    public class ShipRedirectEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_ImpactorBase impactee)
        {
            var shipStatus = shipImpactor.Ship.ShipStatus;
            var transform = shipImpactor.Ship.ShipStatus.ShipTransform;

            shipStatus.ShipTransformer.GentleSpinShip(.5f * transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right,
                transform.up, 1);
        }
    }
}
