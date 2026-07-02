using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Replaces per-prism CreateBlockCoroutine with a centralized queue that
    /// activates a bounded number of prisms per frame.
    ///
    /// Every spawned prism used to start its own coroutine ending in a single
    /// WaitForSeconds(waitTime) — so a mass spawn (cell flora seeding, segment
    /// tracks, AOE block bursts) scheduled thousands of timers that all resumed
    /// on the SAME frame ~0.6s later (profiled on the source branch: 49,856
    /// coroutines resuming in one frame — a 1.9s stall and 10.1MB of GC).
    ///
    /// Each prism queues itself via <see cref="Enqueue"/> with a target
    /// activation time; each Update processes up to
    /// <see cref="maxActivationsPerFrame"/> due prisms, spreading the cost.
    /// Follows the centralized-timer pattern of <see cref="PrismTimerManager"/>.
    /// Extracted from claude/add-prism-activation-queue-CEoJM and adapted to the
    /// current lifecycle (destroyed-guard + PrismSpatialIndex registration live
    /// inside Prism.ExecuteDeferredActivation).
    /// </summary>
    public class PrismActivationQueue : Singleton<PrismActivationQueue>
    {
        [Header("Throughput")]
        [Tooltip("Max prisms to activate per frame. Higher = faster ramp-in on mass spawns, more frame cost.")]
        [SerializeField] private int maxActivationsPerFrame = 200;

        private struct PendingActivation
        {
            public Prism Prism;
            public Vector3 AuthoredTargetScale;
            public float ActivateAtTime;
        }

        private readonly List<PendingActivation> _queue = new(256);

        /// <summary>
        /// Ensures a PrismActivationQueue instance exists. If none was placed in the
        /// scene, creates one automatically so prism activation never silently stalls.
        /// </summary>
        public static PrismActivationQueue EnsureInstance()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("[PrismActivationQueue]");
            go.AddComponent<PrismActivationQueue>();
            return Instance;
        }

        /// <summary>
        /// Queue a prism for deferred activation. Replaces StartCoroutine(CreateBlockCoroutine).
        /// </summary>
        public void Enqueue(Prism prism, Vector3 authoredTargetScale, float delay)
        {
            if (prism == null) return;

            _queue.Add(new PendingActivation
            {
                Prism = prism,
                AuthoredTargetScale = authoredTargetScale,
                ActivateAtTime = Time.time + delay
            });
        }

        /// <summary>
        /// Remove all pending activations for a specific prism (pool return /
        /// re-initialize). Swap-remove — entry order is irrelevant to correctness.
        /// </summary>
        public void Cancel(Prism prism)
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i].Prism == prism)
                {
                    int last = _queue.Count - 1;
                    if (i != last) _queue[i] = _queue[last];
                    _queue.RemoveAt(last);
                }
            }
        }

        private void Update()
        {
            if (_queue.Count == 0) return;

            float now = Time.time;
            int activated = 0;

            for (int i = _queue.Count - 1; i >= 0 && activated < maxActivationsPerFrame; i--)
            {
                var entry = _queue[i];

                if (entry.ActivateAtTime > now)
                    continue;

                // Swap-remove before executing so re-entrant Enqueue/Cancel from the
                // activation path can't corrupt the iteration window.
                int last = _queue.Count - 1;
                if (i != last) _queue[i] = _queue[last];
                _queue.RemoveAt(last);

                // Skip destroyed/pooled-out prisms; Prism.ExecuteDeferredActivation
                // additionally guards its own destroyed flag.
                if (entry.Prism == null || !entry.Prism.gameObject.activeInHierarchy)
                    continue;

                entry.Prism.ExecuteDeferredActivation(entry.AuthoredTargetScale);
                activated++;
            }
        }

        private void OnDisable() => _queue.Clear();
    }
}
