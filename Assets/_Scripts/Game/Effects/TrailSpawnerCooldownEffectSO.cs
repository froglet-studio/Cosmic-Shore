using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "TrailSpawnerCooldownImpactEffect", menuName = "ScriptableObjects/Impact Effects/TrailSpawnerCooldownImpactEffectSO")]
    public class TrailSpawnerCooldownEffectSO : ImpactEffectSO, IBaseImpactEffect
    {
        [SerializeField]
        float _coolDownDuration = 10f;

        public void Execute(ImpactEffectData context)
        {
            context.ThisShipStatus.TrailSpawner.PauseTrailSpawner();
            context.ThisShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(_coolDownDuration);
        }
    }
}
