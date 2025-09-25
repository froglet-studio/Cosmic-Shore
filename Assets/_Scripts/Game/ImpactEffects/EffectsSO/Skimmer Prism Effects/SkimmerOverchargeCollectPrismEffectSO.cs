using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerOverchargeCollectPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerOverchargeCollectPrismEffectSO")]
    public class SkimmerOverchargeCollectPrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Overcharge Settings")]
        [SerializeField] private int    maxBlockHits       = 30;
        [SerializeField] private float  cooldownDuration   = 5f;
        [SerializeField] private bool   verbose;
        [SerializeField] private Material overchargedMaterial;
        [SerializeField] private float  overchargeInertia  = 70f;
        [SerializeField] private float  minBlastSpeed      = 25f;

        [Header("Sequencing")]
        [Tooltip("Delay between each prism Damage() call during overcharge.")]
        [SerializeField] private float  hitIntervalSeconds = 0.08f;

        public int MaxBlockHits => maxBlockHits;

        public event Action<SkimmerImpactor,int,int> OnCountChanged;
        public event Action<SkimmerImpactor,float>   OnCooldownStarted;
        public event Action<SkimmerImpactor>         OnOvercharge;


        // For uniqueness:
        private static readonly Dictionary<SkimmerImpactor, HashSet<PrismImpactor>> hitSetBySkimmer = new();
        // For FIFO order:
        private static readonly Dictionary<SkimmerImpactor, Queue<PrismImpactor>> hitQueueBySkimmer = new();
        // Cooldowns:
        private static readonly Dictionary<SkimmerImpactor, float> cooldownTimers = new();

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            if (prismImpactee.Prism.Team == Teams.Jade)
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

            // Respect cooldown
            if (cooldownTimers.TryGetValue(impactor, out var cooldownEnd) && Time.time < cooldownEnd)
                return;

            if (!hitSetBySkimmer.TryGetValue(impactor, out var set))
            {
                set = new HashSet<PrismImpactor>();
                hitSetBySkimmer[impactor] = set;
            }
            if (!hitQueueBySkimmer.TryGetValue(impactor, out var queue))
            {
                queue = new Queue<PrismImpactor>(maxBlockHits + 4);
                hitQueueBySkimmer[impactor] = queue;
            }

            if (!set.Add(prismImpactee))
                return;

            queue.Enqueue(prismImpactee);

            var rend = prismImpactee ? prismImpactee.GetComponent<Renderer>() : null;
            if (rend && overchargedMaterial) rend.material = overchargedMaterial;

            var rawCount = set.Count;
            var clamped  = Mathf.Min(rawCount, maxBlockHits); // UI clamp
            OnCountChanged?.Invoke(impactor, clamped, maxBlockHits);

            // Not yet at threshold
            if (rawCount < maxBlockHits) return;

            // Reached threshold -> trigger overcharge
            TriggerOvercharge(impactor, set, queue);

            // Reset counters immediately (as before)
            set.Clear();
            queue.Clear();

            // Start cooldown immediately (same timing as original)
            cooldownTimers[impactor] = Time.time + cooldownDuration;
            OnCooldownStarted?.Invoke(impactor, cooldownDuration);

            // Reset UI counter
            OnCountChanged?.Invoke(impactor, 0, maxBlockHits);
        }

        private void TriggerOvercharge(SkimmerImpactor impactor, HashSet<PrismImpactor> _, Queue<PrismImpactor> orderedHits)
        {
            var status = impactor?.Skimmer?.VesselStatus;
            if (status == null || impactor == null) return;

            var shipPos = status.ShipTransform ? status.ShipTransform.position : impactor.transform.position;
            var speed   = Mathf.Max(minBlastSpeed, status.Speed);

            // Start FIFO damage sequence on the impactor (MonoBehaviour)
            impactor.StartCoroutine(OverchargeSequence(impactor, orderedHits.ToArray(), shipPos, speed));
        }

        private IEnumerator OverchargeSequence(SkimmerImpactor impactor, PrismImpactor[] ordered, Vector3 shipPos, float speedAtTrigger)
        {

            OnOvercharge?.Invoke(impactor);
            if (verbose) Debug.Log($"[SkimmerOvercharge] Overcharge sequence started. Count={ordered.Length}", impactor);

            for (var i = 0; i < ordered.Length; i++)
            {
                var prism = ordered[i];

                if (prism && prism.Prism)
                {

                    var dir    = (prism.transform.position - shipPos).normalized;
                    var damage = dir * (speedAtTrigger * overchargeInertia);

                    prism.Prism.Damage(damage, Teams.None, impactor.Skimmer?.VesselStatus?.PlayerName, devastate: true);

                    if (verbose) Debug.Log($"[SkimmerOvercharge] Hit {i+1}/{ordered.Length}: {prism.name}", impactor);
                }

                // Delay before next hit (skip delay after last one)
                if (i < ordered.Length - 1 && hitIntervalSeconds > 0f)
                    yield return new WaitForSeconds(hitIntervalSeconds);
            }

            if (verbose) Debug.Log("[SkimmerOvercharge] Overcharge sequence complete.", impactor);
        }
    }
}
