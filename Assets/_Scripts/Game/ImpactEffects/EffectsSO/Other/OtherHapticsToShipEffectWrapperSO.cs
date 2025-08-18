using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherHapticsToShipEffectWrapper", menuName = "ScriptableObjects/Impact Effects/Other/OtherHapticsToShipEffectWrapperSO")]
    public class OtherHapticsToShipEffectWrapperSO : ImpactEffectSO<ImpactorBase, ShipImpactor>
    {
        [SerializeField]
        ShipHapticsByOtherEffectSO shipHapticsByOtherEffect;
        
        protected override void ExecuteTyped(ImpactorBase impactor, ShipImpactor shipImpactee)
        {
            shipHapticsByOtherEffect.Execute(shipImpactee, impactor);
        }
    }
}