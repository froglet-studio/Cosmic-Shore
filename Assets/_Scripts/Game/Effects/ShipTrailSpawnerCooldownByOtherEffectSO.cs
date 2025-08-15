using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipTrailSpawnerCooldownByOtherEffect", menuName = "ScriptableObjects/Impact Effects/ShipTrailSpawnerCooldownByOtherEffectSO")]
    public class ShipTrailSpawnerCooldownByOtherEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField]
        float _coolDownDuration = 10f;

        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase impactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            shipStatus.TrailSpawner.PauseTrailSpawner();
            shipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(_coolDownDuration);
        }
    }
}
