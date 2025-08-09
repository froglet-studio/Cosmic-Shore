using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "RedirectImpactEffect", menuName = "ScriptableObjects/Impact Effects/RedirectImpactEffectSO")]
    public class RedirectEffectSO : ImpactEffectSO
    {
        /*public void Execute(ImpactEffectData data)
        {
            var transform = data.ThisShipStatus.ShipTransform;

            data.ThisShipStatus.ShipTransformer.GentleSpinShip(.5f * transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right,
                                transform.up, 1);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
