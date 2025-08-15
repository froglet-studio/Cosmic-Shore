using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherSpinAroundShipEffectWrapper", menuName = "ScriptableObjects/Impact Effects/OtherSpinAroundShipEffectWrapperSO")]
    public class OtherSpinAroundShipEffectWrapperSO : ImpactEffectSO<R_ImpactorBase, R_ShipImpactor>
    {
        [SerializeField]
        ShipSpinAroundByOtherEffectSO shipSpinAroundByOtherEffect;
        
        protected override void ExecuteTyped(R_ImpactorBase impactor, R_ShipImpactor impactee)
        {
            shipSpinAroundByOtherEffect.Execute(impactee, impactor);
        }
    }
}