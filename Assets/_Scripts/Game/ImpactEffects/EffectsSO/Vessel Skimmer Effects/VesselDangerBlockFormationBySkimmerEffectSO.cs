using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselDangerBlockFormationBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselDangerBlockFormationBySkimmerEffectSO")]
    public sealed class VesselDangerBlockFormationBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("AOE Prefab")]
        [SerializeField] private GameObject dangerHemispherePrefab;

        public override void Execute(VesselImpactor vesselImpactor, SkimmerImpactor skimmerImpactee)
        {
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

            if (!dangerHemispherePrefab)
            {
                Debug.LogError("[VesselDangerBlockFormationBySkimmerEffectSO] dangerHemispherePrefab is not assigned.");
                return;
            }

            var victimPos   = victimTransform.position;
            var cellManager = CellControlManager.Instance;
            var cell = cellManager.GetNearestCell(victimPos);
            if (cell == null)
            {
                Debug.LogWarning("[VesselDangerBlockFormationBySkimmerEffectSO] GetNearestCell returned null.");
                return;
            }

            var targetPos = cell.GetExplosionTarget(attackerStatus.Domain);

            var toTarget = targetPos - victimPos;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                Debug.LogWarning("[VesselDangerBlockFormationBySkimmerEffectSO] Target too close to victim. Aborting AOE.");
                return;
            }

            var dirToTarget = toTarget.normalized;
            Quaternion rotation = Quaternion.LookRotation(dirToTarget, Vector3.up);

            var aoeGo = Object.Instantiate(dangerHemispherePrefab, victimPos, rotation);
            var aoe   = aoeGo.GetComponent<AOEDangerHemisphereBlocks>();
            if (!aoe)
            {
                Debug.LogError("[VesselDangerBlockFormationBySkimmerEffectSO] dangerHemispherePrefab has no AOEDangerHemisphereBlocks.");
                return;
            }

            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain           = victimStatus.Domain, // mostly for factory logic; prisms themselves are "danger"
                AnnonymousExplosion = false,
                Vessel              = victimVessel,
                OverrideMaterial    = null,        // AOEDangerHemisphereBlocks sets its own danger material
                MaxScale            = aoe.MaxScale,
                SpawnPosition       = victimPos,
                SpawnRotation       = rotation
            };

            aoe.Initialize(init);
            aoe.Detonate();
        }
    }
}
