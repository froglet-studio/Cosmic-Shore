using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "TrailSpawnerCooldownEffect", menuName = "ScriptableObjects/Impact Effects/TrailSpawnerCooldownEffectSO")]
    public class ShipTrailSpawnerCooldownEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField]
        float _coolDownDuration = 10f;

        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase crystalImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            shipStatus.TrailSpawner.PauseTrailSpawner();
            shipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(_coolDownDuration);
        }
    }
}
