using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherStunShipEffectWrapper", menuName = "ScriptableObjects/Impact Effects/OtherStunShipEffectWrapperSO")]
    public class OtherStunShipEffectWrapperSO : ImpactEffectSO<R_ImpactorBase, R_ShipImpactor>
    {
        [SerializeField] ShipStunEffectByOtherSO shipStunEffectByOther;
        
        protected override void ExecuteTyped(R_ImpactorBase impactor, R_ShipImpactor impactee)
        {
            shipStunEffectByOther.Execute(impactee, impactor);
        }
    }
}