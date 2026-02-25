using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselSkimmerEffects
{
    [CreateAssetMenu(fileName = "VesselPrismSpawnerCooldownBySkimmerEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselPrismSpawnerCooldownBySkimmerEffectSO")]
    public class VesselPrismSpawnerCooldownBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [SerializeField]
        float _coolDownDuration = 10f;

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            var shipStatus = impactor.Vessel.VesselStatus;
            shipStatus.VesselPrismController.StopSpawn();
            
            ExecuteAfterDelay(shipStatus.VesselPrismController.StartSpawn).Forget();
        }

        async UniTaskVoid ExecuteAfterDelay(Action action)
        {
            await UniTask.WaitForSeconds(_coolDownDuration);
            action();
        }
    }
}
