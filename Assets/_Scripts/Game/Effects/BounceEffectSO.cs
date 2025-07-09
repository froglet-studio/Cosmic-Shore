using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "BounceImpactEffect", menuName = "ScriptableObjects/Impact Effects/BounceImpactEffectSO")]
    public class BounceEffectSO : ImpactEffectSO, ITrailBlockImpactEffect
    {
        public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            Transform shipTransform = data.ThisShipStatus.ShipTransform;

            var cross = Vector3.Cross(shipTransform.forward, trailBlockProperties.trailBlock.transform.forward);
            var normal = Quaternion.AngleAxis(90, cross) * trailBlockProperties.trailBlock.transform.forward;
            var reflectForward = Vector3.Reflect(shipTransform.forward, normal);
            var reflectUp = Vector3.Reflect(shipTransform.up, normal);
            data.ThisShipStatus.ShipTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
            data.ThisShipStatus.ShipTransformer.ModifyVelocity((shipTransform.position - trailBlockProperties.trailBlock.transform.position).normalized * 5,
                Time.deltaTime * 15);
        }
    }
}
