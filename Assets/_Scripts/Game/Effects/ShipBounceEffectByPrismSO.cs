using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipBounceEffectByPrism", menuName = "ScriptableObjects/Impact Effects/ShipBounceEffectByPrismSO")]
    public class ShipBounceEffectByPrismSO : ImpactEffectSO<R_ShipImpactor, R_PrismImpactor>
    {
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_PrismImpactor prismImpactee)
        {
            IShipStatus shipStatus = shipImpactor.Ship.ShipStatus;
            TrailBlockProperties trailBlockProperties = prismImpactee.TrailBlock.TrailBlockProperties;
            Transform shipTransform = shipStatus.ShipTransform;

            var cross = Vector3.Cross(shipTransform.forward, trailBlockProperties.trailBlock.transform.forward);
            var normal = Quaternion.AngleAxis(90, cross) * trailBlockProperties.trailBlock.transform.forward;
            var reflectForward = Vector3.Reflect(shipTransform.forward, normal);
            var reflectUp = Vector3.Reflect(shipTransform.up, normal);
            shipStatus.ShipTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
            shipStatus.ShipTransformer.ModifyVelocity((shipTransform.position - trailBlockProperties.trailBlock.transform.position).normalized * 5,
                Time.deltaTime * 15);
        }
    }
}
