using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using CosmicShore.Core;
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
        // Teardown guard. Death cascades that run from OnDisable/OnDestroy while a scene is
        // closing (a Spindle evaporating, a prism exploding) reach EnsureInstance after this
        // manager is already gone; auto-creating one then leaks a [PrismTimerManager] into the
        // closing scene ("Some objects were not cleaned up when closing the scene"). So no
        // auto-create from play-mode exit / quit, nor between this manager being destroyed with
        // its scene and that scene finishing its unload. Same shape as PrismEffectsManager's.
        private static bool _isQuitting;
        private static bool _sceneClosing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _isQuitting = false;
            _sceneClosing = false;
            // Application.quitting also fires on editor play-mode exit, and needs no live
            // instance (unlike OnApplicationQuit).
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        static void HandleQuitting() => _isQuitting = true;
        static void HandleSceneUnloaded(Scene _) => _sceneClosing = false;

        static bool IsTearingDown =>
            _isQuitting || _sceneClosing || ApplicationLifecycleManager.IsQuitting;

        /// <summary>
        /// Ensures a PrismTimerManager instance exists. If none was placed in the scene,
        /// creates one automatically so timed shield operations don't silently fail.
        /// Returns null during teardown (play-mode exit / quit / the manager's scene
        /// unloading) instead of spawning a GameObject into a closing scene - callers
        /// must null-check (<c>EnsureInstance()?.ScheduleAction(...)</c>).
        /// </summary>
        public static PrismTimerManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            if (IsTearingDown) return null;

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

        // Indexed by owner so CancelScheduledActions is O(owner's entries), not a full
        // list scan with RemoveAt (which was O(N²) on mass pool returns).
        private readonly Dictionary<UnityEngine.Object, List<ScheduledAction>> scheduledByOwner = new(64);
        // An owner's list is retired when its last action fires or is cancelled, and every trail
        // prism's settle schedules one, so the lists are recycled rather than allocated per schedule.
        private readonly Stack<List<ScheduledAction>> ownerListPool = new(64);
        private readonly List<UnityEngine.Object> ownerScratch = new(32);
        private readonly List<Action> dueActions = new(16);
        int scheduledActionCount;

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
            if (!scheduledByOwner.TryGetValue(owner, out var list))
            {
                list = ownerListPool.Count > 0 ? ownerListPool.Pop() : new List<ScheduledAction>(4);
                scheduledByOwner[owner] = list;
            }
            list.Add(new ScheduledAction
            {
                Owner = owner,
                EndTime = Time.time + delay,
                Callback = callback
            });
            scheduledActionCount++;
        }

        /// <summary>Cancel every scheduled action for this owner (pool return /
        /// destruction / animation re-stamp superseding the old settle).</summary>
        public void CancelScheduledActions(UnityEngine.Object owner)
        {
            if (owner == null) return;
            if (!scheduledByOwner.TryGetValue(owner, out var list)) return;
            scheduledActionCount -= list.Count;
            RetireOwner(owner, list);
        }

        void RetireOwner(UnityEngine.Object owner, List<ScheduledAction> list)
        {
            list.Clear();
            scheduledByOwner.Remove(owner);
            ownerListPool.Push(list);
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
            if (activeTimers.Count == 0 && scheduledActionCount == 0) return;

            float currentTime = Time.time;

            // Generalized settle swaps first (the law's touchpoint 3).
            if (scheduledActionCount > 0)
            {
                dueActions.Clear();
                ownerScratch.Clear();
                ownerScratch.AddRange(scheduledByOwner.Keys);
                for (int o = 0; o < ownerScratch.Count; o++)
                {
                    var owner = ownerScratch[o];
                    if (!scheduledByOwner.TryGetValue(owner, out var list)) continue;
                    if (owner == null)
                    {
                        scheduledActionCount -= list.Count;
                        RetireOwner(owner, list);
                        continue;
                    }
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var entry = list[i];
                        if (currentTime >= entry.EndTime)
                        {
                            list.RemoveAt(i);
                            scheduledActionCount--;
                            dueActions.Add(entry.Callback);
                        }
                    }
                    if (list.Count == 0)
                        RetireOwner(owner, list);
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
            // Destroyed because its scene is unloading: hold off auto-create until the unload
            // completes, so the rest of the scene's teardown can't spawn a replacement.
            if (Instance == this && !gameObject.scene.isLoaded) _sceneClosing = true;

            activeTimers.Clear();
            completionTargets.Clear();
            scheduledByOwner.Clear();
            ownerListPool.Clear();
            ownerScratch.Clear();
            scheduledActionCount = 0;
            dueActions.Clear();
        }
    }
}
