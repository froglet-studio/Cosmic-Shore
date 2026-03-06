using System.Collections.Generic;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Replaces per-prism CreateBlockCoroutine with a centralized queue that
    /// activates a bounded number of prisms per frame. This eliminates the
    /// thundering-herd problem where 50K WaitForSeconds(0.6) coroutines all
    /// resume on the same frame, causing a multi-second stall.
    ///
    /// Each prism queues itself via <see cref="Enqueue"/> with a target activation
    /// time. Each Update, the queue processes up to <see cref="maxActivationsPerFrame"/>
    /// prisms whose delay has elapsed, spreading the cost across frames.
    /// </summary>
    public class PrismActivationQueue : Singleton<PrismActivationQueue>
    {
        [Header("Throughput")]
        [Tooltip("Max prisms to activate per frame. Higher = faster but more frame cost.")]
        [SerializeField] private int maxActivationsPerFrame = 200;

        private struct PendingActivation
        {
            public Prism Prism;
            public Vector3 AuthoredTargetScale;
            public float ActivateAtTime;
        }

        private readonly List<PendingActivation> _queue = new(256);

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
        /// Remove all pending activations for a specific prism (e.g. when returned to pool).
        /// Uses swap-remove for O(1) per removal.
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

                // Remove from queue (swap-remove)
                int last = _queue.Count - 1;
                if (i != last) _queue[i] = _queue[last];
                _queue.RemoveAt(last);

                // Skip destroyed/disabled prisms
                if (entry.Prism == null || !entry.Prism.gameObject.activeInHierarchy)
                    continue;

                entry.Prism.ExecuteDeferredActivation(entry.AuthoredTargetScale);
                activated++;
            }
        }

        /// <summary>
        /// Ensures a PrismActivationQueue instance exists.
        /// </summary>
        public static PrismActivationQueue EnsureInstance()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("[PrismActivationQueue]");
            go.AddComponent<PrismActivationQueue>();
            return Instance;
        }

        private void OnDisable()
        {
            _queue.Clear();
        }

        protected override void OnDestroy()
        {
            _queue.Clear();
            base.OnDestroy();
        }
    }
}
