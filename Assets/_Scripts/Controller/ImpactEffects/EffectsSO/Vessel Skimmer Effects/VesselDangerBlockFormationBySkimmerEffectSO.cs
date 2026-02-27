using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using Reflex.Injectors;
using System;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "VesselDangerBlockFormationBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselDangerBlockFormationBySkimmerEffectSO")]
    public sealed class VesselDangerBlockFormationBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        /// <summary>Static event: fired when danger blocks are spawned. Param = attacker player name.</summary>
        public static event System.Action<string> OnDangerBlockSpawned;

        [Header("AOE Prefab")]
        [SerializeField] private GameObject dangerHemispherePrefab;
        
        [SerializeField]
        CellRuntimeDataSO cellData;
        
        public override void Execute(VesselImpactor vesselImpactor, SkimmerImpactor skimmerImpactee)
        {
            if (!cellData)
            {
                CSDebug.LogError("No Cell data found!");
                return;
            }
            
            var victimVessel  = vesselImpactor.Vessel;
            var attackerSkimmer = skimmerImpactee.Skimmer;
            if (attackerSkimmer == null || victimVessel == null)
                return;

            var attackerStatus = attackerSkimmer.VesselStatus;
            var victimStatus   = victimVessel.VesselStatus;
    
            if (attackerStatus.VesselType != VesselClassType.Rhino)
                return;

            if (attackerStatus.Vessel == victimVessel)
            {
                return;
            }

            var victimTransform = victimStatus.Vessel?.Transform;
            if (!victimTransform)
                return;

            var victimPos   = victimTransform.position;
            var targetPos = cellData.Cell.GetCrystalTransform().position;

            var toTarget = targetPos - victimPos;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                CSDebug.LogWarning("[VesselDangerBlockFormationBySkimmerEffectSO] Target too close to victim. Aborting AOE.");
                return;
            }

            var dirToTarget = toTarget.normalized;
            Quaternion rotation = Quaternion.LookRotation(dirToTarget, Vector3.up);

            var aoeGo = Instantiate(dangerHemispherePrefab, victimPos, rotation);
            var container = vesselImpactor.DIContainer;
            if (container != null)
                GameObjectInjector.InjectRecursive(aoeGo, container);
            var aoe   = aoeGo.GetComponent<AOEDangerHemisphereBlocks>();

            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain           = attackerStatus.Domain,
                AnnonymousExplosion = false,
                Vessel              = victimVessel,
                OverrideMaterial    = null,       
                MaxScale            = aoe.MaxScale,
                SpawnPosition       = victimPos,
                SpawnRotation       = rotation
            };

            aoe.Initialize(init);
            aoe.Detonate();

            OnDangerBlockSpawned?.Invoke(attackerStatus.PlayerName);
        }
    }
}
