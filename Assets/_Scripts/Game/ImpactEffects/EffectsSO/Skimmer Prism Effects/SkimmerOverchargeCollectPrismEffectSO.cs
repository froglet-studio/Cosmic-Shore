using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
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
        public event Action<SkimmerImpactor>         OnReadyToOvercharge; // NEW
        public event Action<SkimmerImpactor,float>   OnCooldownStarted;
        public event Action<SkimmerImpactor>         OnOvercharge;

        private static readonly Dictionary<SkimmerImpactor, HashSet<PrismImpactor>> hitsBySkimmer = new();
        private static readonly Dictionary<SkimmerImpactor, float> cooldownTimers   = new();
        private static readonly HashSet<SkimmerImpactor> readySet                  = new(); // NEW: prevent double-ready

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

            // If already full/ready, ignore more hits until controller confirms
            if (readySet.Contains(impactor)) return;

            // Collect unique hits
            if (!hitsBySkimmer.TryGetValue(impactor, out var hitSet))
            {
                hitSet = new HashSet<PrismImpactor>();
                hitsBySkimmer[impactor] = hitSet;
            }

            if (!hitSet.Add(prismImpactee)) return;

            var rend = prismImpactee ? prismImpactee.GetComponent<Renderer>() : null;
            if (rend && overchargedMaterial) rend.material = overchargedMaterial;

            var rawCount = hitSet.Count;
            var clamped  = Mathf.Min(rawCount, maxBlockHits);
            OnCountChanged?.Invoke(impactor, clamped, maxBlockHits);

            if (rawCount >= maxBlockHits)
            {
                readySet.Add(impactor);
                OnReadyToOvercharge?.Invoke(impactor);
            }
        }

        public void ConfirmOvercharge(SkimmerImpactor impactor)
        {
            if (impactor == null) return;

            if (!hitsBySkimmer.TryGetValue(impactor, out var hitSet) || hitSet.Count == 0)
            {
                // Nothing to do; still start cooldown/UI reset to be safe
                StartCooldownAndReset(impactor);
                return;
            }

            TriggerOvercharge(impactor, hitSet);
            hitSet.Clear();
            StartCooldownAndReset(impactor);
        }

        private void StartCooldownAndReset(SkimmerImpactor impactor)
        {
            readySet.Remove(impactor);
            cooldownTimers[impactor] = Time.time + cooldownDuration;
            OnCooldownStarted?.Invoke(impactor, cooldownDuration);
            OnCountChanged?.Invoke(impactor, 0, maxBlockHits); // reset UI counter
        }
        
        private void TriggerOvercharge(SkimmerImpactor impactor, HashSet<PrismImpactor> hitSet)
        {
            var status = impactor?.Skimmer?.VesselStatus;
            if (status == null) return;

            // Fire and forget async task
            BlowUpPrismsOverTime(impactor, hitSet, status).Forget();
        }

        private async UniTaskVoid BlowUpPrismsOverTime(
            SkimmerImpactor impactor, 
            HashSet<PrismImpactor> hitSet, 
            IVesselStatus status)
        {
            var shipPos = status.ShipTransform ? status.ShipTransform.position : impactor.transform.position;
            var speed   = Mathf.Max(minBlastSpeed, status.Speed);

            var orderedPrisms = hitSet
                .Where(p => p && p.Prism)
                .OrderBy(p => Vector3.Distance(shipPos, p.transform.position));

            foreach (var prism in orderedPrisms)
            {
                var dir    = (prism.transform.position - shipPos).normalized;
                var damage = dir * (overchargeInertia * speed);

                prism.Prism.Damage(damage, Domains.None, status.PlayerName, devastate: true);

                // Async delay before hitting the next prism
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
            }

            OnOvercharge?.Invoke(impactor);
            if (verbose) Debug.Log($"[SkimmerOvercharge] Overcharge triggered sequentially! ({hitSet.Count})", impactor);
        }

    }
}

