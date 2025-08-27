using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipDecoyByOmniCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipDecoyByOmniCrystalEffectSO")]
    public class ShipDecoyByOmniCrystalEffectSO : ShipCrystalEffectSO
    {
        [SerializeField] private float debounceSeconds = 0.15f;
        [SerializeField] private GameObject minePrefab;

        private static readonly Dictionary<Crystal, float> NextAllowedAt = new();

        public override void Execute(ShipImpactor shipImpactor, CrystalImpactor impactee)
        {
            var crystal = impactee.Crystal;
            if (!crystal) return;

            if (NextAllowedAt.TryGetValue(crystal, out var t) && Time.time < t) return;
            NextAllowedAt[crystal] = Time.time + debounceSeconds;
            
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

            crystal.CrystalRespawn();
        }
    }
}
