using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerOverchargeCollectPrismEffect", menuName = "ScriptableObjects/Impact Effects/SkimmerOverchargeCollectPrismEffectSO")] 
    public class SkimmerOverchargeCollectPrismEffectSO:ImpactEffectSO<SkimmerImpactor, PrismImpactor>
    {
        [Header("Overcharge Settings")]
        [SerializeField] private int maxBlockHits = 30;
        [SerializeField] private float cooldownDuration = 5f;
        [SerializeField] private bool verbose = false;  
        
        private static readonly Dictionary <SkimmerImpactor,HashSet<PrismImpactor>> hitsBySkimmer = new();
        private static readonly Dictionary <SkimmerImpactor,float> cooldownTimers = new();
        
        [SerializeField] private Material overchargedMaterial;
        
        protected override void ExecuteTyped(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            if (prismImpactee.Prism.Team == Teams.Jade)
            {
                if (verbose) Debug.Log("[SkimmerOvercharge] Ignored Jade team prism.", prismImpactee);
                return;
            }
            if (cooldownTimers.TryGetValue(impactor, out
                    var cooldownEnd) && Time.time < cooldownEnd)
            {
                if (verbose) Debug.Log("[SkimmerOvercharge] Still on cooldown.", impactor);
                return;
            } 
            
            // Ensure hit set exists 
            if (!hitsBySkimmer.TryGetValue(impactor, out var hitSet))
            {
                hitSet = new HashSet<PrismImpactor>();
                hitsBySkimmer[impactor] = hitSet;
            } 
            
            // Avoid counting the same block twice 
            if (!hitSet.Add(prismImpactee)) return;
            
            var rend = prismImpactee.GetComponent<Renderer>();
            if (rend != null && overchargedMaterial != null)
            {
                rend.material = overchargedMaterial; // Change material
                if (verbose)
                    Debug.Log($"[SkimmerOvercharge] Material changed for {prismImpactee.name}", prismImpactee);
            }
            
            if (verbose) Debug.Log($"[SkimmerOvercharge] Hit {hitSet.Count}/{maxBlockHits} | Prism: {prismImpactee.name}", prismImpactee);
            
            if (hitSet.Count < maxBlockHits) return;
            
            TriggerOvercharge(impactor, hitSet);
            hitSet.Clear();
            cooldownTimers[impactor] = Time.time + cooldownDuration;
        }
        private void TriggerOvercharge(SkimmerImpactor impactor, HashSet<PrismImpactor> hitSet)
        {
            foreach (var prism in hitSet)
            {
                prism.gameObject.SetActive(false);
            }

            if (verbose)
                Debug.Log($"[SkimmerOvercharge] Overcharge triggered! ({hitSet.Count} prisms) on {impactor.name}", impactor);
        }
    }
}