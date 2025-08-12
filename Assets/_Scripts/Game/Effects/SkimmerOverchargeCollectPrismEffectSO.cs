using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerOverchargeCollectPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/SkimmerOverchargeCollectPrismEffectSO")]
    public class SkimmerOverchargeCollectPrismEffectSO 
        : ImpactEffectSO<R_SkimmerImpactor, R_PrismImpactor>
    {
        [Header("Overcharge Settings")]
        [SerializeField] private int maxBlockHits = 30; // Threshold before trigger
        [SerializeField] private float cooldownDuration = 5f; // Seconds after explosion before counting again
        [SerializeField] private bool verbose = false;

        // Tracking hits and cooldowns per skimmer
        private static readonly Dictionary<R_SkimmerImpactor, HashSet<R_PrismImpactor>> hitsBySkimmer
            = new Dictionary<R_SkimmerImpactor, HashSet<R_PrismImpactor>>();
        private static readonly Dictionary<R_SkimmerImpactor, float> cooldownTimers
            = new Dictionary<R_SkimmerImpactor, float>();

        protected override void ExecuteTyped(R_SkimmerImpactor impactor, R_PrismImpactor prismImpactee)
        {
            if (prismImpactee.TrailBlock.Team == Teams.Jade)
            {
                if (verbose) Debug.Log("[SkimmerOvercharge] Ignored Jade team prism.", prismImpactee as Component);
                return;
            }
            
            if (cooldownTimers.TryGetValue(impactor, out var cooldownEnd) && Time.time < cooldownEnd)
            {
                if (verbose) Debug.Log("[SkimmerOvercharge] Still on cooldown.", impactor);
                return;
            }

            // Ensure hit set exists
            if (!hitsBySkimmer.TryGetValue(impactor, out var hitSet))
            {
                hitSet = new HashSet<R_PrismImpactor>();
                hitsBySkimmer[impactor] = hitSet;
            }

            // Avoid counting the same block twice
            if (!hitSet.Add(prismImpactee))
                return;

            if (verbose)
                Debug.Log($"[SkimmerOvercharge] Hit {hitSet.Count}/{maxBlockHits} | Prism: {prismImpactee.name}", prismImpactee as Component);

            // Threshold reached
            if (hitSet.Count < maxBlockHits) return;
            TriggerOvercharge(impactor, hitSet);
            hitSet.Clear();
            cooldownTimers[impactor] = Time.time + cooldownDuration; // Start cooldown
        }

        private void TriggerOvercharge(R_SkimmerImpactor impactor, HashSet<R_PrismImpactor> hitSet)
        {
            foreach (var prism in hitSet)
            {
                if (prism is Component prismComp)
                {
                    prismComp.gameObject.SetActive(false); // For now disable
                    if (verbose) Debug.Log($"[SkimmerOvercharge] Disabled {prismComp.name}", prismComp);
                }
            }

            if (verbose)
                Debug.Log($"[SkimmerOvercharge] Overcharge triggered! ({hitSet.Count} prisms) on {impactor.name}", impactor);
        }
    }
}
