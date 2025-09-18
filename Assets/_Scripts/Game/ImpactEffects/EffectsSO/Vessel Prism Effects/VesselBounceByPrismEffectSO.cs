using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselBounceByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselBounceByPrismEffectSO")]
    public class VesselBounceByPrismEffectSO : VesselPrismEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            IVesselStatus vesselStatus = vesselImpactor.Vessel.VesselStatus;
            TrailBlockProperties trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            Transform shipTransform = vesselStatus.ShipTransform;

            var cross = Vector3.Cross(shipTransform.forward, trailBlockProperties.trailBlock.transform.forward);
            var normal = Quaternion.AngleAxis(90, cross) * trailBlockProperties.trailBlock.transform.forward;
            var reflectForward = Vector3.Reflect(shipTransform.forward, normal);
            var reflectUp = Vector3.Reflect(shipTransform.up, normal);
            vesselStatus.VesselTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
            vesselStatus.VesselTransformer.ModifyVelocity((shipTransform.position - trailBlockProperties.trailBlock.transform.position).normalized * 5,
                Time.deltaTime * 15);
        }
    }
}
