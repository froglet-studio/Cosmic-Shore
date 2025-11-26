using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Spawns an AOE conic explosion where the OmniCrystal is hit.
    /// Position is taken from the crystal's world-space bounds center (collider → renderer → transform).
    /// Orientation faces from the crystal toward the impacting ship.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipAOEConicExplosionByOmniCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/ShipAOEConicExplosionByOmniCrystalEffectSO")]
    public class ShipAOEConicExplosionByOmniCrystalEffectSO : ImpactEffectSO<ShipImpactor, OmniCrystalImpactor>
    {
        [SerializeField] private GameObject _aoeConicExplosion;

        protected override void ExecuteTyped(ShipImpactor shipImpactor, OmniCrystalImpactor impactee)
        {
            var crystal = impactee.Crystal;
            var targetTransform = crystal.transform;

            var spawnPos = targetTransform.position;

            if (crystal && crystal.TryGetComponent<SphereCollider>(out var sphere))
            {
                spawnPos = sphere.bounds.center;
            }
            else if (crystal && crystal.TryGetComponent<Collider>(out var anyCrystalCol))
            {
                spawnPos = anyCrystalCol.bounds.center;
            }
            else if (impactee.TryGetComponent<Collider>(out var impacteeCol))
            {
                spawnPos = impacteeCol.bounds.center;
            }
            else
            {
                var r = targetTransform.GetComponentInChildren<Renderer>();
                if (r != null) spawnPos = r.bounds.center;
            }

            var toShip = shipImpactor.transform.position - spawnPos;
            var forward = toShip.sqrMagnitude > 0.0001f ? toShip.normalized : targetTransform.forward;
            var spawnRot = Quaternion.LookRotation(forward, Vector3.up);

            var aoe = Instantiate(_aoeConicExplosion, spawnPos, spawnRot);
            aoe.transform.SetParent(null, true);
        }
    }
}