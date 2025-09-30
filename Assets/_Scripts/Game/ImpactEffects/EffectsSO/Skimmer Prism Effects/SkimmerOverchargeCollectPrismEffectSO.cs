using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerOverchargeCollectPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerOverchargeCollectPrismEffectSO")]
    public class SkimmerOverchargeCollectPrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Overcharge Settings")]
        [SerializeField] private int   maxBlockHits     = 30;
        [SerializeField] private float cooldownDuration = 5f;
        [SerializeField] private bool  verbose;
        [SerializeField] private Material overchargedMaterial;
        [SerializeField] private float overchargeInertia = 70f;
        [SerializeField] private float minBlastSpeed     = 25f; 

        public int MaxBlockHits => maxBlockHits;
        
        public event Action<SkimmerImpactor,int,int> OnCountChanged;   
        public event Action<SkimmerImpactor,float>   OnCooldownStarted; 
        public event Action<SkimmerImpactor>         OnOvercharge;     

        private static readonly Dictionary<SkimmerImpactor, HashSet<PrismImpactor>> hitsBySkimmer = new();
        private static readonly Dictionary<SkimmerImpactor, float> cooldownTimers = new();

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            if (prismImpactee.Prism.Domain == impactor.Skimmer.Domain)
            {
                if (prismImpactee.Prism.CurrentState == BlockState.Normal)
                {
                    prismImpactee.Prism.ActivateShield();
                }
                return;
            }

            if (prismImpactee.Prism.CurrentState == BlockState.Shielded)
            {
                prismImpactee.Prism.DeactivateShields();
                return;
            }

            if (cooldownTimers.TryGetValue(impactor, out var cooldownEnd) && Time.time < cooldownEnd)
                return;

            if (!hitsBySkimmer.TryGetValue(impactor, out var hitSet))
            {
                hitSet = new HashSet<PrismImpactor>();
                hitsBySkimmer[impactor] = hitSet;
            }

            if (!hitSet.Add(prismImpactee)) return;

            var rend = prismImpactee ? prismImpactee.GetComponent<Renderer>() : null;
            if (rend && overchargedMaterial) rend.material = overchargedMaterial;

            var rawCount = hitSet.Count;
            var clamped  = Mathf.Min(rawCount, maxBlockHits);  // <- clamp for UI
            OnCountChanged?.Invoke(impactor, clamped, maxBlockHits);

            if (rawCount < maxBlockHits) return;

            TriggerOvercharge(impactor, hitSet);
            hitSet.Clear();

            cooldownTimers[impactor] = Time.time + cooldownDuration;
            OnCooldownStarted?.Invoke(impactor, cooldownDuration);

            OnCountChanged?.Invoke(impactor, 0, maxBlockHits);
        }

        private void TriggerOvercharge(SkimmerImpactor impactor, HashSet<PrismImpactor> hitSet)
        {
            var status = impactor?.Skimmer.VesselStatus;
            if (status == null) return;

            var shipPos = status.ShipTransform ? status.ShipTransform.position : impactor.transform.position;
            var speed   = Mathf.Max(minBlastSpeed, status.Speed);

            foreach (var prism in hitSet)
            {
                if (!prism || !prism.Prism) continue;

                var dir = (prism.transform.position - shipPos).normalized;
                var damage = dir * speed * overchargeInertia;

                prism.Prism.Damage(damage, Domains.None, status.PlayerName, devastate: true);
            }

            OnOvercharge?.Invoke(impactor);
            if (verbose) Debug.Log($"[SkimmerOvercharge] Overcharge triggered! ({hitSet.Count})", impactor);
        }
    }
}

