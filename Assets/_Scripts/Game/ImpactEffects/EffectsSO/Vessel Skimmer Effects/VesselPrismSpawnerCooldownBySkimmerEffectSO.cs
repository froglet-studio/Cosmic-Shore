using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselPrismSpawnerCooldownBySkimmerEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselPrismSpawnerCooldownBySkimmerEffectSO")]
    public class VesselPrismSpawnerCooldownBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [SerializeField]
        float _coolDownDuration = 10f;

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            var shipStatus = impactor.Vessel.VesselStatus;
            shipStatus.VesselPrismController.PauseTrailSpawner();
            shipStatus.VesselPrismController.RestartTrailSpawnerAfterDelay(_coolDownDuration);
        }
    }
}
