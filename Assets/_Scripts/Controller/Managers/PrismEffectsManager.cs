using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Death explosions/implosions ride batched PrismDebris (D4). This class
    /// remains for two conforming jobs: Grow's moving-convergence-target refresh
    /// (pooled PrismImplosion.StartGrow — Sparrow ReverseSuction; the doc's §1
    /// exception, location only, one float3/frame) and the dev-build zombie-VFX
    /// audit. The explosion EnabledInstances walk is empty in gameplay (pool
    /// never Get()d); the implosion walk covers Grow pool zombies.
    /// </summary>
    public class PrismEffectsManager : Singleton<PrismEffectsManager>
    {
        private static bool _isQuitting;

        // OnApplicationQuit fires on editor play-mode EXIT — with domain reload disabled a
        // stale true makes EnsureInstance() return null for every later session, silently
        // killing implosion convergence. Same shape as ApplicationLifecycleManager.ResetStatics.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _isQuitting = false;

        private void OnApplicationQuit() => _isQuitting = true;

        /// <summary>
        /// Ensures a PrismEffectsManager instance exists. If none was placed in the scene,
        /// creates one automatically so the convergence refresh / zombie audit don't silently fail.
        /// </summary>
        public static PrismEffectsManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            if (_isQuitting) return null;

            var go = new GameObject("[PrismEffectsManager]");
            go.AddComponent<PrismEffectsManager>();
            Debug.LogWarning("[PrismEffectsManager] No instance found in scene - auto-created. " +
                             "Consider adding one to the scene to avoid this overhead.");
            return Instance;
        }

        // ------------------------------------------------------------------
        // Clock-mode implosions: progress rides the GPU clock. ONLY the moving
        // convergence target refreshes here — the documented exception
        // (PRISM_ANIMATION.md §1): live gameplay data, one float3 write per
        // frame per implosion, and nothing else. Entries drop themselves when
        // the target dies (the suction freezes at the last stamped point) or
        // the effect completes.
        // ------------------------------------------------------------------

        private readonly List<PrismImplosion> clockConvergenceTracking = new(32);

        public void RegisterClockConvergence(PrismImplosion implosion)
        {
            if (implosion == null || clockConvergenceTracking.Contains(implosion)) return;
            clockConvergenceTracking.Add(implosion);
        }

        public void UnregisterClockConvergence(PrismImplosion implosion)
        {
            clockConvergenceTracking.Remove(implosion);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Diagnostics (Editor / Dev builds only)")]
        [Tooltip("Seconds between the leaked-VFX safety audit. The audit walks the effects' enabled-instance registries (O(live effects), no scene scan). Set <= 0 to disable.")]
        [SerializeField] private float zombieAuditIntervalSeconds = 5f;
        private float _nextZombieAuditTime;
#endif

        private void Update()
        {
            // Moving-target refresh for clock-stamped implosions (§1 exception —
            // location only; the animation itself never touches the CPU).
            for (int i = clockConvergenceTracking.Count - 1; i >= 0; i--)
            {
                var imp = clockConvergenceTracking[i];
                if (imp == null || !imp.IsActive || !imp.RefreshConvergenceForClock())
                    clockConvergenceTracking.RemoveAt(i);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Safety audit: detect pooled VFX with enabled renderers that aren't
            // actively managed. Explosion walk is empty in gameplay (D4 never
            // Get()s that pool). Implosion walk covers Grow (Sparrow ReverseSuction)
            // pool zombies. Backwards, because SetActive(false) below removes the
            // entry from the registry mid-walk.
            if (zombieAuditIntervalSeconds > 0f && Time.unscaledTime >= _nextZombieAuditTime)
            {
                _nextZombieAuditTime = Time.unscaledTime + zombieAuditIntervalSeconds;
                var allExplosions = PrismExplosion.EnabledInstances;
                for (int i = allExplosions.Count - 1; i >= 0; i--)
                {
                    var exp = allExplosions[i];
                    if (!exp) continue;
                    if (exp.Renderer != null && exp.Renderer.enabled && !exp.IsActive)
                        exp.Renderer.enabled = false;
                    // Entity-path zombies: companion entity left visible without
                    // an active animation driving it.
                    if (!exp.IsActive && exp.UsesEntityRenderPath)
                        CosmicShore.ECS.PrismRenderService.SetVisible(in exp.RenderHandle, false);
                }

                var allImplosions = PrismImplosion.EnabledInstances;
                int activeGameObjects = 0;
                int zombies = 0;
                int healthy = 0;
                for (int i = allImplosions.Count - 1; i >= 0; i--)
                {
                    var imp = allImplosions[i];
                    if (!imp) continue;
                    bool goActive = imp.gameObject.activeSelf;
                    if (!goActive) continue;

                    activeGameObjects++;
                    if (!imp.IsActive)
                    {
                        zombies++;
                        // Active GameObject + IsActive=false = orphaned by the pool
                        // callback chain. Force-deactivate immediately rather than
                        // waiting 4s for the per-instance watchdog.
                        imp.gameObject.SetActive(false);
                    }
                    else
                    {
                        healthy++;
                    }
                }

                // Periodic stats: lets us distinguish "many legitimate implosions
                // playing at once" (expected when the squirrel circles the crystal
                // and fauna swarm-eat its trail) from "leak accumulating zombies".
                // Only log when count is interesting (>0 zombies or >32 healthy)
                // so the console isn't spammy on quiet scenes.
                if (zombies > 0 || activeGameObjects > 32)
                {
                    CSDebug.Log($"[PrismEffectsManager] Active implosions: total={activeGameObjects} healthy={healthy} zombies={zombies}");
                }
            }
#endif
        }

        private void OnDisable()
        {
            clockConvergenceTracking.Clear();
        }

        private void OnDestroy()
        {
            clockConvergenceTracking.Clear();
        }
    }
}
