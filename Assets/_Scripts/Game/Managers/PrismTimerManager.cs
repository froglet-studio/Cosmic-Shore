using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Utilities;

namespace CosmicShore.Core
{
    /// <summary>
    /// Centralized timer manager that replaces per-object coroutines for timed
    /// prism state changes (shield activation/deactivation with duration).
    ///
    /// Instead of each PrismStateManager allocating a coroutine on the heap,
    /// all timers are stored in a flat list and checked each frame. At 500 prisms
    /// with active shields, this eliminates ~500 coroutine scheduler entries and
    /// their associated heap allocations.
    ///
    /// This is also the stepping stone toward full ECS migration, where
    /// ShieldTimer (IEnableableComponent) replaces this manager entirely.
    /// </summary>
    public class PrismTimerManager : Singleton<PrismTimerManager>
    {
        /// <summary>
        /// When true, the manager is shutting down (scene unload). Individual
        /// CancelTimers calls become no-ops since the entire list is already cleared.
        /// This prevents O(N²) teardown when thousands of prisms each try to cancel.
        /// </summary>
        private bool _disposing;

        /// <summary>
        /// Ensures a PrismTimerManager instance exists. If none was placed in the scene,
        /// creates one automatically so timed shield operations don't silently fail.
        /// </summary>
        public static PrismTimerManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("[PrismTimerManager]");
            go.AddComponent<PrismTimerManager>();
            Debug.LogWarning("[PrismTimerManager] No instance found in scene — auto-created. " +
                             "Consider adding one to the scene to avoid this overhead.");
            return Instance;
        }

        internal enum TimerAction : byte
        {
            DeactivateShield = 0,
        }

        internal struct TimerEntry
        {
            public PrismStateManager Target;
            public float EndTime;
            public TimerAction Action;
        }

        private readonly List<TimerEntry> activeTimers = new(64);
        private readonly List<PrismStateManager> completionTargets = new(16);

        /// <summary>
        /// Schedule a shield deactivation for the given PrismStateManager after a delay.
        /// Cancels any existing timer for the same target first.
        /// </summary>
        public void ScheduleShieldDeactivation(PrismStateManager target, float delay)
        {
            if (_disposing || target == null) return;

            // Cancel any existing timer for this target to avoid duplicates
            CancelTimers(target);

            activeTimers.Add(new TimerEntry
            {
                Target = target,
                EndTime = Time.time + delay,
                Action = TimerAction.DeactivateShield
            });
        }

        /// <summary>
        /// Cancel all pending timers for the given PrismStateManager.
        /// Call this when the prism is destroyed or returned to pool.
        /// Uses swap-remove to avoid O(N) element shifting per removal.
        /// </summary>
        public void CancelTimers(PrismStateManager target)
        {
            if (_disposing || activeTimers.Count == 0) return;

            for (int i = activeTimers.Count - 1; i >= 0; i--)
            {
                if (activeTimers[i].Target == target)
                {
                    // Swap with last element for O(1) removal instead of O(N) shift
                    int last = activeTimers.Count - 1;
                    if (i != last)
                        activeTimers[i] = activeTimers[last];
                    activeTimers.RemoveAt(last);
                }
            }
        }

        private void Update()
        {
            if (activeTimers.Count == 0) return;

            float currentTime = Time.time;
            completionTargets.Clear();

            // Process expired timers (swap-remove to avoid element shifting)
            for (int i = activeTimers.Count - 1; i >= 0; i--)
            {
                var entry = activeTimers[i];

                // Null check: target may have been destroyed
                if (entry.Target == null)
                {
                    int last = activeTimers.Count - 1;
                    if (i != last) activeTimers[i] = activeTimers[last];
                    activeTimers.RemoveAt(last);
                    continue;
                }

                if (currentTime >= entry.EndTime)
                {
                    int last = activeTimers.Count - 1;
                    if (i != last) activeTimers[i] = activeTimers[last];
                    activeTimers.RemoveAt(last);
                    completionTargets.Add(entry.Target);
                }
            }

            // Execute completions after iteration to avoid re-entrancy issues
            for (int i = 0; i < completionTargets.Count; i++)
            {
                var target = completionTargets[i];
                if (target != null)
                {
                    target.ExecuteTimerDeactivation();
                }
            }
        }

        private void OnDisable()
        {
            Debug.Log($"[ScenePerf] PrismTimerManager.OnDisable — {activeTimers.Count} timers t={Time.realtimeSinceStartup:F3}");
            _disposing = true;
            activeTimers.Clear();
            completionTargets.Clear();
        }

        protected override void OnDestroy()
        {
            Debug.Log($"[ScenePerf] PrismTimerManager.OnDestroy t={Time.realtimeSinceStartup:F3}");
            _disposing = true;
            activeTimers.Clear();
            completionTargets.Clear();
            base.OnDestroy();
        }
    }
}
