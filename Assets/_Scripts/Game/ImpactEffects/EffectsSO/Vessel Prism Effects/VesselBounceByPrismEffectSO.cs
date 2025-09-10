using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipBounceByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/VesselBounceByPrismEffectSO")]
    public class VesselBounceByPrismEffectSO : VesselPrismEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            IShipStatus shipStatus = vesselImpactor.Ship.ShipStatus;
            TrailBlockProperties trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
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
