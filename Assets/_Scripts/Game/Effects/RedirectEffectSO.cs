using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "RedirectImpactEffect", menuName = "ScriptableObjects/Impact Effects/RedirectImpactEffectSO")]
    public class RedirectEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            var transform = context.ShipStatus.ShipTransform;

            context.ShipStatus.ShipTransformer.GentleSpinShip(.5f * transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right,
                                transform.up, 1);
        }
    }
}
