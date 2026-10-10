using System.Linq;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselDecoyByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselDecoyByCrystalEffectSO")]
    public class VesselDecoyByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField] private GameObject minePrefab;

        // Per-crystal debounce. A static table keyed by the crystal object: ObjectCooldowns prunes
        // destroyed crystals on first sight of a new one, so a session's dead crystals do not pile up.
        private static readonly ObjectCooldowns<Crystal> _cooldowns = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _cooldowns.Clear();

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            
        }
        
        /*public override void Execute(VesselImpactor vesselImpactor, CrystalImpactor impactee)
        {
            var crystal = impactee.Crystal;
            if (!crystal) return;

            if (!_cooldowns.TryBegin(crystal, Time.time, debounceSeconds)) return;
            
            var models = crystal.CrystalModels;
            if (models != null)
                foreach (var m in models.Where(m => m?.model)) m?.model.SetActive(false);
            
            Vector3 spawnPosition = crystal.transform.localPosition;
            
            if (crystal.TryGetComponent<SphereCollider>(out var sphere))
                spawnPosition = sphere.bounds.center;
            else if (crystal.TryGetComponent<Collider>(out var anyCol))
                spawnPosition = anyCol.bounds.center;
            else
            {
                var r = crystal.GetComponentInChildren<Renderer>();
                if (r) spawnPosition = r.bounds.center;
            }
            var spawnRotation = crystal.transform.rotation;
            
            if (minePrefab != null)
            {
                var mine = Instantiate(minePrefab, spawnPosition, spawnRotation);
                mine.transform.SetParent(null, true);
            }

            crystal.Respawn();
        }*/
    }
}
