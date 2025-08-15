using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherHapticsToShipEffectWrapper", menuName = "ScriptableObjects/Impact Effects/OtherHapticsToShipEffectWrapperSO")]
    public class OtherHapticsToShipEffectWrapperSO : ImpactEffectSO<R_ImpactorBase, R_ShipImpactor>
    {
        [SerializeField]
        ShipHapticsByOtherEffectSO shipHapticsByOtherEffect;
        
        protected override void ExecuteTyped(R_ImpactorBase impactor, R_ShipImpactor shipImpactee)
        {
            shipHapticsByOtherEffect.Execute(shipImpactee, impactor);
        }
    }
}