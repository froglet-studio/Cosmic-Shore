using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipSpinByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipSpinByOtherEffectSO")]
    public class ShipSpinByOtherEffectSO : ImpactEffectSO
    {
        [SerializeField]
        float spinSpeed;
        
        public virtual void Execute(VesselImpactor impactor, ImpactorBase impactee)
        {
            Vector3 impactVector = (impactee.Transform.position - impactor.Transform.position).normalized;
            
            var shipStatus = impactor.Ship.ShipStatus;
            shipStatus.ShipTransformer.SpinShip(impactVector * spinSpeed);
            
            // TODO: Implement GentleSpin from here only
            /*shipStatus.ShipTransformer.GentleSpinShip(.5f * transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right,
                transform.up, 1);*/
        }
    }
}

