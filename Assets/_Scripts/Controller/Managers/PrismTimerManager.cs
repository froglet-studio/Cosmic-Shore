using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Utility;
using System;

namespace CosmicShore.Gameplay
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
        /// Ensures a PrismTimerManager instance exists. If none was placed in the scene,
        /// creates one automatically so timed shield operations don't silently fail.
        /// </summary>
        public static PrismTimerManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("[PrismTimerManager]");
            go.AddComponent<PrismTimerManager>();
            Debug.LogWarning("[PrismTimerManager] No instance found in scene - auto-created. " +
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

        // ------------------------------------------------------------------
        // Generalized scheduled actions — the clock-material law's touchpoint 3
        // (Docs/PRISM_ANIMATION.md §1: the end-state swap runs at an
        // analytically-known time through a flat timer list, never discovered
        // by per-frame progress polling). One delegate per animation EVENT
        // (bounded churn), not per frame.
        // ------------------------------------------------------------------

        private struct ScheduledAction
        {
            public UnityEngine.Object Owner; // cancellation key + destroyed-check
            public float EndTime;
            public Action Callback;
        }

        private readonly List<ScheduledAction> scheduledActions = new(64);
        private readonly List<Action> dueActions = new(16);

        /// <summary>
        /// Schedule <paramref name="callback"/> to run once at Time.time + delay —
        /// the settle swap of a clock-material animation (swap to the end-state
        /// material/mesh, clear stamps, fire completion side effects). The owner
        /// keys cancellation and suppresses the callback if destroyed first.
        /// Does NOT deduplicate per owner (an owner may legitimately await several
        /// swaps); pair every schedule with <see cref="CancelScheduledActions"/> in
        /// the owner's OnDisable/pool-return path.
        /// </summary>
        public void ScheduleAction(UnityEngine.Object owner, float delay, Action callback)
        {
            if (owner == null || callback == null) return;
            scheduledActions.Add(new ScheduledAction
            {
                Owner = owner,
                EndTime = Time.time + delay,
                Callback = callback
            });
        }

        /// <summary>Cancel every scheduled action for this owner (pool return /
        /// destruction / animation re-stamp superseding the old settle).</summary>
        public void CancelScheduledActions(UnityEngine.Object owner)
        {
            for (int i = scheduledActions.Count - 1; i >= 0; i--)
            {
                if (scheduledActions[i].Owner == owner)
                    scheduledActions.RemoveAt(i);
            }
        }

        /// <summary>
        /// Schedule a shield deactivation for the given PrismStateManager after a delay.
        /// Cancels any existing timer for the same target first.
        /// </summary>
        public void ScheduleShieldDeactivation(PrismStateManager target, float delay)
        {
            if (target == null) return;

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
        /// </summary>
        public void CancelTimers(PrismStateManager target)
        {
            for (int i = activeTimers.Count - 1; i >= 0; i--)
            {
                if (activeTimers[i].Target == target)
                {
                    activeTimers.RemoveAt(i);
                }
            }
        }

        private void Update()
        {
            if (activeTimers.Count == 0 && scheduledActions.Count == 0) return;

            float currentTime = Time.time;

            // Generalized settle swaps first (the law's touchpoint 3).
            if (scheduledActions.Count > 0)
            {
                dueActions.Clear();
                for (int i = scheduledActions.Count - 1; i >= 0; i--)
                {
                    var entry = scheduledActions[i];
                    if (entry.Owner == null)
                    {
                        scheduledActions.RemoveAt(i); // owner destroyed — swap moot
                        continue;
                    }
                    if (currentTime >= entry.EndTime)
                    {
                        scheduledActions.RemoveAt(i);
                        dueActions.Add(entry.Callback);
                    }
                }
                // Run after iteration — callbacks may schedule/cancel actions.
                for (int i = 0; i < dueActions.Count; i++)
                {
                    try { dueActions[i]?.Invoke(); }
                    catch (Exception e) { Debug.LogError($"[PrismTimerManager] Scheduled action threw: {e}"); }
                }
            }

            if (activeTimers.Count == 0) return;
            completionTargets.Clear();

            // Process expired timers
            for (int i = activeTimers.Count - 1; i >= 0; i--)
            {
                var entry = activeTimers[i];

                // Null check: target may have been destroyed
                if (entry.Target == null)
                {
                    activeTimers.RemoveAt(i);
                    continue;
                }

                if (currentTime >= entry.EndTime)
                {
                    activeTimers.RemoveAt(i);
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

        private void OnDestroy()
        {
            activeTimers.Clear();
            completionTargets.Clear();
            scheduledActions.Clear();
            dueActions.Clear();
        }
    }
}
