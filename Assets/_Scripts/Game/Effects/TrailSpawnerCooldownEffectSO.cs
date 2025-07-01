using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "TrailSpawnerCooldownImpactEffect", menuName = "ScriptableObjects/Impact Effects/TrailSpawnerCooldownImpactEffectSO")]
    public class TrailSpawnerCooldownEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        float _coolDownDuration = 10f;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.TrailSpawner.PauseTrailSpawner();
            context.ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(_coolDownDuration);
        }
    }
}
