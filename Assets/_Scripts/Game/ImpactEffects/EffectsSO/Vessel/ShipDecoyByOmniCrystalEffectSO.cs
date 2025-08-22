using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipDecoyByOmniCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipDecoyByOmniCrystalEffectSO")]
    public class ShipDecoyByOmniCrystalEffectSO : ImpactEffectSO<ShipImpactor, OmniCrystalImpactor>
    {
        [SerializeField] private float debounceSeconds = 0.15f;
        [SerializeField] private bool verbose = false;
        [SerializeField] private GameObject fakeCrystalPrefab;

        private static readonly Dictionary<Crystal, float> _nextAllowedAt = new();

        protected override void ExecuteTyped(ShipImpactor shipImpactor, OmniCrystalImpactor impactee)
        {
            var crystal = impactee.Crystal;
            if (!crystal) return;

            crystal.IsDecoyCrystal = true;
            if (_nextAllowedAt.TryGetValue(crystal, out var t) && Time.time < t) return;
            _nextAllowedAt[crystal] = Time.time + debounceSeconds;
            
            var models = crystal.CrystalModels;
            if (models != null)
                foreach (var m in models.Where(m => m?.model)) m.model.SetActive(false);

            // Robust world spawn position (collider / renderer center)
            var spawnPosition = crystal.transform.localPosition;
            
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

            if (fakeCrystalPrefab)
            {
                var fake = Instantiate(fakeCrystalPrefab, spawnPosition, spawnRotation);
                fake.transform.SetParent(null, true);

                if (fake.TryGetComponent<FakeCrystal>(out var fc))
                    fc.OwnTeam = crystal.OwnTeam;

                if (verbose) Debug.Log($"[Decoy] Spawned FakeCrystal @ {spawnPosition}");
            }
            else if (verbose)
            {
                Debug.LogWarning("[Decoy] No FakeCrystalPrefab set on Crystal.");
            }
            crystal.CrystalRespawn();
        }
    }
}
