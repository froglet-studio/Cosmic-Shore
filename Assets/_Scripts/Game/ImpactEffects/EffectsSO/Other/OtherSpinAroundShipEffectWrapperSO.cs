using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherSpinAroundShipEffectWrapper", menuName = "ScriptableObjects/Impact Effects/OtherSpinAroundShipEffectWrapperSO")]
    public class OtherSpinAroundShipEffectWrapperSO : ImpactEffectSO<ImpactorBase, ShipImpactor>
    {
        [FormerlySerializedAs("shipSpinAroundByOtherEffect")] [SerializeField]
        ShipSpinByOtherEffectSO shipSpinByOtherEffect;
        
        protected override void ExecuteTyped(ImpactorBase impactor, ShipImpactor impactee)
        {
            shipSpinByOtherEffect.Execute(impactee, impactor);
        }
    }
}