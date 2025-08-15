using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherPrismSpawnCooldownToShipEffectWrapper", menuName = "ScriptableObjects/Impact Effects/OtherPrismSpawnCooldownToShipEffectWrapperSO")]
    public class OtherPrismSpawnCooldownToShipEffectWrapperSO : ImpactEffectSO<R_ImpactorBase, R_ShipImpactor>
    {
        [SerializeField]
        ShipTrailSpawnerCooldownByOtherEffectSO shipTrailSpawnerCooldownByOtherEffect;
        
        protected override void ExecuteTyped(R_ImpactorBase impactor, R_ShipImpactor impactee)
        {
            shipTrailSpawnerCooldownByOtherEffect.Execute(impactee, impactor);
        }
    }
}