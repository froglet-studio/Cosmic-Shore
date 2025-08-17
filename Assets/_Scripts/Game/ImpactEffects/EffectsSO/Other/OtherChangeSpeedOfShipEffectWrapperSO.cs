using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherChangeSpeedOfShipEffectWrapper", menuName = "ScriptableObjects/Impact Effects/Other/OtherChangeSpeedOfShipEffectWrapperSO")]
    public class OtherChangeSpeedOfShipEffectWrapperSO : ImpactEffectSO<ImpactorBase, ShipImpactor>
    {
        [SerializeField] ShipChangeSpeedByOtherEffectSO shipChangeSpeedByOtherEffect;
        
        protected override void ExecuteTyped(ImpactorBase impactor, ShipImpactor impactee)
        {
            shipChangeSpeedByOtherEffect.Execute(impactee, impactor);
        }
    }
}