using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Core.Visuals;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private float explosionSpeed = 70f;
        [SerializeField] private float minBlastSpeed     = 25f;
        [SerializeField, Min(0f)] private float materialBlendDuration = 0.6f;
        [SerializeField] private bool appendOverchargedMaterial = true;

        public int MaxBlockHits => maxBlockHits;

        public event Action<SkimmerImpactor,int,int> OnCountChanged;
        public event Action<SkimmerImpactor>         OnReadyToOvercharge;
        public event Action<SkimmerImpactor,float>   OnCooldownStarted;
        public event Action<SkimmerImpactor>         OnOvercharge;

        private static readonly Dictionary<SkimmerImpactor, HashSet<PrismImpactor>> hitsBySkimmer = new();
        private static readonly Dictionary<SkimmerImpactor, float> cooldownTimers   = new();
        private static readonly HashSet<SkimmerImpactor> readySet                  = new();

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

            if (readySet.Contains(impactor)) return;

            // Collect unique hits
            if (!hitsBySkimmer.TryGetValue(impactor, out var hitSet))
            {
                hitSet = new HashSet<PrismImpactor>();
                hitsBySkimmer[impactor] = hitSet;
            }

            if (!hitSet.Add(prismImpactee)) return;

            var rend = prismImpactee ? prismImpactee.GetComponent<Renderer>() : null;
            if (rend && overchargedMaterial)
            {
                MaterialBlendUtility.BeginBlend(
                    rend, overchargedMaterial, materialBlendDuration, appendOverchargedMaterial);
            }

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
            if (!impactor) return;

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

            BlowUpPrismsOverTime(impactor, hitSet, status).Forget();
        }

        void RecursiveRaycastDestruction(Prism prism, IVesselStatus status)
        {
            var shipPos = status.ShipTransform.position;
            var dir = shipPos - prism.transform.position;
            var damage = dir * explosionSpeed; //TODO: use mult
            if (Physics.Raycast(prism.transform.position, dir, out var hitInfo, dir.magnitude, LayerMask.GetMask("TrailBlocks")))
            {
                Prism hitPrism;
                if (hitInfo.collider.gameObject.TryGetComponent<Prism>(out hitPrism))
                {
                    RecursiveRaycastDestruction(hitPrism, status);
                }
            }
            prism.Damage(damage, Domains.None, status.PlayerName, devastate: true);
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
                RecursiveRaycastDestruction(prism.Prism, status);

                // Async delay before hitting the next prism
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
            }

            OnOvercharge?.Invoke(impactor);
        }
    }
}


