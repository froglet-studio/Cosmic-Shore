using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipPrismSpawnerCooldownByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipPrismSpawnerCooldownByOtherEffectSO")]
    public class ShipPrismSpawnerCooldownByOtherEffectSO : ImpactEffectSO
    {
        [SerializeField]
        float _coolDownDuration = 10f;

        public void Execute(VesselImpactor impactor, ImpactorBase impactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            shipStatus.TrailSpawner.PauseTrailSpawner();
            shipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(_coolDownDuration);
        }
    }
}
