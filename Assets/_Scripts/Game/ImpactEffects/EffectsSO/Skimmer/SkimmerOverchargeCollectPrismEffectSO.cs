using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerOverchargeCollectPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer/SkimmerOverchargeCollectPrismEffectSO")]
    public class SkimmerOverchargeCollectPrismEffectSO : ImpactEffectSO<SkimmerImpactor, PrismImpactor>
    {
        [Header("Overcharge Settings")]
        [SerializeField] private int   maxBlockHits     = 30;
        [SerializeField] private float cooldownDuration = 5f;
        [SerializeField] private bool  verbose;
        [SerializeField] private Material overchargedMaterial;

        public int MaxBlockHits => maxBlockHits;
        
        public event Action<SkimmerImpactor,int,int> OnCountChanged;   
        public event Action<SkimmerImpactor,float>   OnCooldownStarted; 
        public event Action<SkimmerImpactor>         OnOvercharge;     

        private static readonly Dictionary<SkimmerImpactor, HashSet<PrismImpactor>> hitsBySkimmer = new();
        private static readonly Dictionary<SkimmerImpactor, float> cooldownTimers = new();

        protected override void ExecuteTyped(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            if (prismImpactee.Prism.Team == Teams.Jade) return;

            // cooldown gate
            if (cooldownTimers.TryGetValue(impactor, out var cooldownEnd) && Time.time < cooldownEnd)
            {
                if (verbose) Debug.Log("[SkimmerOvercharge] Still on cooldown.", impactor);
                return;
            }

            if (!hitsBySkimmer.TryGetValue(impactor, out var hitSet))
            {
                hitSet = new HashSet<PrismImpactor>();
                hitsBySkimmer[impactor] = hitSet;
            }

            // skip duplicates
            if (!hitSet.Add(prismImpactee)) return;

            // visual: mark prism overcharged
            var rend = prismImpactee ? prismImpactee.GetComponent<Renderer>() : null;
            if (rend && overchargedMaterial) rend.material = overchargedMaterial;

            var count = hitSet.Count;
            OnCountChanged?.Invoke(impactor, count, maxBlockHits);   // <— notify HUD

            if (count < maxBlockHits) return;

            // threshold reached
            TriggerOvercharge(impactor, hitSet);

            hitSet.Clear();
            cooldownTimers[impactor] = Time.time + cooldownDuration;
            OnCooldownStarted?.Invoke(impactor, cooldownDuration);
        }

        private void TriggerOvercharge(SkimmerImpactor impactor, HashSet<PrismImpactor> hitSet)
        {
            foreach (var prism in hitSet) prism.gameObject.SetActive(false);
            OnOvercharge?.Invoke(impactor);
            if (verbose) Debug.Log($"[SkimmerOvercharge] Overcharge triggered! ({hitSet.Count})", impactor);
        }
    }
}

