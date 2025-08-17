using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipPrismSpawnerCooldownByOtherEffect", menuName = "ScriptableObjects/Impact Effects/ShipPrismSpawnerCooldownByOtherEffectSO")]
    public class ShipPrismSpawnerCooldownByOtherEffectSO : ImpactEffectSO<ShipImpactor, ImpactorBase>
    {
        [SerializeField]
        float _coolDownDuration = 10f;

        protected override void ExecuteTyped(ShipImpactor impactor, ImpactorBase impactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            shipStatus.TrailSpawner.PauseTrailSpawner();
            shipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(_coolDownDuration);
        }
    }
}
