using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
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
        // (Retired: appendOverchargedMaterial — multi-material appends are GameObject-
        // renderer-only and invisible on the instanced path; the overcharged tag now
        // rides the one color-transition pipeline. See Docs/PRISM_ANIMATION.md §3.8.)

        public int MaxBlockHits => maxBlockHits;

        public event Action<SkimmerImpactor,int,int> OnCountChanged;
        public event Action<SkimmerImpactor>         OnReadyToOvercharge;
        public event Action<SkimmerImpactor,float>   OnCooldownStarted;
        public event Action<SkimmerImpactor>         OnOvercharge;

        private static readonly Dictionary<SkimmerImpactor, HashSet<PrismImpactor>> hitsBySkimmer = new();
        private static readonly Dictionary<SkimmerImpactor, float> cooldownTimers   = new();
        private static readonly HashSet<SkimmerImpactor> readySet                  = new();
        private static readonly List<SkimmerImpactor> _pruneScratch = new();

        // The state above is static (keyed by SkimmerImpactor) and lives for the SO asset's
        // process lifetime, so destroyed impactors (scene unload / vessel despawn) would linger
        // forever and carry overcharge state across scenes. Drop Unity-destroyed keys whenever a
        // new skimmer first appears — bounds the leak without needing a turn-end event wired in
        // the asset.
        private static void PruneDestroyed()
        {
            _pruneScratch.Clear();
            foreach (var key in hitsBySkimmer.Keys) if (!key) _pruneScratch.Add(key);
            foreach (var key in cooldownTimers.Keys) if (!key) _pruneScratch.Add(key);
            foreach (var key in readySet)            if (!key) _pruneScratch.Add(key);

            for (int i = 0; i < _pruneScratch.Count; i++)
            {
                var key = _pruneScratch[i];
                hitsBySkimmer.Remove(key);
                cooldownTimers.Remove(key);
                readySet.Remove(key);
            }
            _pruneScratch.Clear();
        }

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
                // The armour blows off along the skim, not symmetrically
                // (Docs/PRISM_ANIMATION.md §4.8.1). Magnitude is clamped by the shield
                // component's own cap, so the raw flight velocity is safe to hand over.
                var status = impactor.Skimmer.VesselStatus;
                prismImpactee.Prism.DeactivateShields(status.Course * status.Speed);
                return;
            }

            if (cooldownTimers.TryGetValue(impactor, out var cooldownEnd) && Time.time < cooldownEnd)
                return;

            if (readySet.Contains(impactor)) return;

            // Collect unique hits
            if (!hitsBySkimmer.TryGetValue(impactor, out var hitSet))
            {
                PruneDestroyed();
                hitSet = new HashSet<PrismImpactor>();
                hitsBySkimmer[impactor] = hitSet;
            }

            if (!hitSet.Add(prismImpactee)) return;

            // Overcharged tag rides the ONE color-transition pipeline (clock-material
            // or legacy — Docs/PRISM_ANIMATION.md): bind + lerp toward the overcharged
            // material's authored colors with a proper SyncRenderMaterial, visible on
            // BOTH render paths. The former MaterialBlendUtility blend cloned material
            // instances and wrote a disabled MeshRenderer on the instanced path.
            if (overchargedMaterial && prismImpactee && prismImpactee.Prism &&
                prismImpactee.Prism.TryGetComponent(out MaterialPropertyAnimator overchargeAnimator))
            {
                overchargeAnimator.UpdateMaterial(overchargedMaterial, overchargedMaterial, materialBlendDuration);
            }

            // Element → parameter (Mass → harvest capacity). Anchored at base at resting level;
            // a high Mass element lets the ray collect more opposing prisms before it detonates.
            int effectiveMax = EffectiveMaxFor(impactor);

            var rawCount = hitSet.Count;
            var clamped  = Mathf.Min(rawCount, effectiveMax);
            OnCountChanged?.Invoke(impactor, clamped, effectiveMax);

            if (rawCount >= effectiveMax)
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
            // Reset against the SAME Mass-scaled capacity used while filling, so the HUD "/ max"
            // denominator doesn't jump between the scaled cap and the base cap.
            OnCountChanged?.Invoke(impactor, 0, EffectiveMaxFor(impactor)); // reset UI counter
        }

        // Mass → harvest capacity, read per-vessel from the skimmer's own ResourceSystem.
        // Anchored at maxBlockHits at the resting level (Mass == 0).
        private int EffectiveMaxFor(SkimmerImpactor impactor)
        {
            var status = impactor ? impactor.Skimmer?.VesselStatus : null;
            return Mathf.Max(1, Mathf.RoundToInt(
                maxBlockHits * (status?.ElementalAbilityHandler.Multiplier(Element.Mass) ?? 1f)));
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
            // Element → parameter (Charge → detonation blast strength). Anchored at 1x at resting
            // level; a charged-up reaper ray flings the harvested mass harder.
            float chargeMul = status?.ElementalAbilityHandler.Multiplier(Element.Charge) ?? 1f;
            var damage = dir * (explosionSpeed * chargeMul);
            if (Physics.Raycast(prism.transform.position, dir, out var hitInfo, dir.magnitude, LayerMask.GetMask("TrailBlocks")))
            {
                Prism hitPrism;
                if (hitInfo.collider.gameObject.TryGetComponent<Prism>(out hitPrism))
                {
                    RecursiveRaycastDestruction(hitPrism, status);
                }
            }
            prism.Damage(damage, Domains.Blue, status.PlayerName, devastate: true);
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
                .OrderBy(p => (shipPos - p.transform.position).sqrMagnitude); // sort key: distance order == squared-distance order

            foreach (var i in orderedPrisms)
            {
                RecursiveRaycastDestruction(i.Prism, status);

                // Async delay before hitting the next prism
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
            }

            OnOvercharge?.Invoke(impactor);
        }
    }
}


