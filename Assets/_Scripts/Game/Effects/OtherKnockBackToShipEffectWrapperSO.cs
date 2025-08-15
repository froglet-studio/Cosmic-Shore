using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherKnockBackToShipEffectWrapper", menuName = "ScriptableObjects/Impact Effects/OtherKnockBackToShipEffectWrapperSO")]
    public class OtherKnockBackToShipEffectWrapperSO : ImpactEffectSO<R_ImpactorBase, R_ShipImpactor>
    {
        [SerializeField]
        ShipKnockbackEffectByOtherSO shipKnockbackEffectByOther;
        
        protected override void ExecuteTyped(R_ImpactorBase impactor, R_ShipImpactor impactee)
        {
            shipKnockbackEffectByOther.Execute(impactee, impactor);
        }
    }
}