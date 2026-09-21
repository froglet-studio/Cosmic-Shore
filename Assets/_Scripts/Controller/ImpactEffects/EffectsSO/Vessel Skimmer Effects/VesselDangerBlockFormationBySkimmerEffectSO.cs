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
            if (!dangerHemispherePrefab) return;

            var victimVessel  = vesselImpactor.Vessel;
            var attackerSkimmer = skimmerImpactee.Skimmer;
            if (attackerSkimmer == null || victimVessel == null)
                return;

            var attackerStatus = attackerSkimmer.VesselStatus;
            var victimStatus   = victimVessel.VesselStatus;
            if (attackerStatus == null || victimStatus == null)
                return;

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

            // The formation is aimed AT the cell's crystal, and both halves of reaching it
            // could be absent. The authored cellData is a serialized handle on a SHARED SO
            // asset, so it names one cell and is null (or stale) in every scene that is not
            // that one; and a live cell legitimately has no crystal right now, in which case
            // GetCrystalTransform warns and returns NULL. The old code guarded neither `.Cell`
            // nor the returned transform, so a Rhino skimming a rival in any crystal-less cell
            // threw once per CONTACT - a per-skim NullReferenceException on the fleet's most
            // contact-dense path, which is what the impactor's isolation guard was catching.
            var cell = Cell.ResolveHostCell(cellData ? cellData.Cell : null, victimPos);
            if (!cell) return;
            var crystalTransform = cell.GetCrystalTransform();
            if (!crystalTransform) return;
            var targetPos = crystalTransform.position;

            var toTarget = targetPos - victimPos;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                CSDebug.LogWarning("[VesselDangerBlockFormationBySkimmerEffectSO] Target too close to victim. Aborting AOE.");
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(toTarget, Vector3.up); // LookRotation normalizes its forward internally

            var aoeGo = Instantiate(dangerHemispherePrefab, victimPos, rotation);
            var container = vesselImpactor.DIContainer;
            if (container != null)
                GameObjectInjector.InjectRecursive(aoeGo, container);
            var aoe   = aoeGo.GetComponent<AOEDangerHemisphereBlocks>();
            if (!aoe)
            {
                CSDebug.LogError(
                    $"[VesselDangerBlockFormationBySkimmerEffectSO] '{dangerHemispherePrefab.name}' carries no " +
                    $"{nameof(AOEDangerHemisphereBlocks)} - the formation cannot be built.");
                Destroy(aoeGo);
                return;
            }

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
