using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherPrismSpawnCooldownToShipEffectWrapper", menuName = "ScriptableObjects/Impact Effects/OtherPrismSpawnCooldownToShipEffectWrapperSO")]
    public class OtherPrismSpawnCooldownToShipEffectWrapperSO : ImpactEffectSO<ImpactorBase, ShipImpactor>
    {
        [FormerlySerializedAs("shipTrailSpawnerCooldownByOtherEffect")] [SerializeField]
        ShipPrismSpawnerCooldownByOtherEffectSO shipPrismSpawnerCooldownByOtherEffect;
        
        protected override void ExecuteTyped(ImpactorBase impactor, ShipImpactor impactee)
        {
            shipPrismSpawnerCooldownByOtherEffect.Execute(impactee, impactor);
        }
    }
}