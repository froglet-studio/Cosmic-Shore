using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipSpinAroundByOtherEffect", menuName = "ScriptableObjects/Impact Effects/ShipSpinAroundByOtherEffectSO")]
    public class ShipSpinAroundByOtherEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField]
        float spinSpeed;
        
        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase impactee)
        {
            Vector3 impactVector = (impactee.Transform.position - impactor.Transform.position).normalized;
            
            var shipStatus = impactor.Ship.ShipStatus;
            shipStatus.ShipTransformer.SpinShip(impactVector * spinSpeed);
        }
    }
}
