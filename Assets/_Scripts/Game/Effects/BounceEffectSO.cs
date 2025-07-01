using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "BounceImpactEffect", menuName = "ScriptableObjects/Impact Effects/BounceImpactEffectSO")]
    public class BounceEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            Transform shipTransform = context.ShipStatus.ShipTransform;

            var cross = Vector3.Cross(shipTransform.forward, context.TrailBlockProperties.trailBlock.transform.forward);
            var normal = Quaternion.AngleAxis(90, cross) * context.TrailBlockProperties.trailBlock.transform.forward;
            var reflectForward = Vector3.Reflect(shipTransform.forward, normal);
            var reflectUp = Vector3.Reflect(shipTransform.up, normal);
            context.ShipStatus.ShipTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
            context.ShipStatus.ShipTransformer.ModifyVelocity((shipTransform.position - context.TrailBlockProperties.trailBlock.transform.position).normalized * 5,
                Time.deltaTime * 15);
        }
    }
}
