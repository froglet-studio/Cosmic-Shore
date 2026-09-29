using System;
using System.Collections.Generic;
using CosmicShore.ECS;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Unbounded, prefab-keyed pool for environment and flora HealthPrism mass
    /// (Docs/PRISM_ANIMATION.md §5 C13b).
    ///
    /// Why this is not InteractivePrismPoolManager / GenericPoolManager:
    /// those vessel-trail pools cap inactive capacity and Destroy overflow on
    /// Release, and Interactive wires the prism's pool-return delegate on Get
    /// so Cell.RetireWorldIntoSuctionRoot would vacuum every issued prism —
    /// including the Wanderway conveyor stock, which is conserved mass that
    /// must survive a cell swap. Membership here is an issued dict +
    /// TryRelease; that return delegate is never touched.
    ///
    /// Never Release a live environment prism during gameplay. Live mass stays
    /// until an active sink (fauna, vessel ability, or cell-swap drain of the
    /// authored environment gathered under the retiring root). Consumed/exploded
    /// prisms Destroy and ForgetDestroyed drops them; they do not return.
    ///
    /// PrepareForLay snaps Domains.Blue materials even on first mint so
    /// ChangeTeam(final domain) is a real Blue → domain clock lerp. A raw
    /// Instantiate cloned the prefab already wearing the final domain material,
    /// so HandleTeamChange read Jade as both start and end.
    ///
    /// Named, not folded (raw Instantiate remains): Boid.cs body prisms,
    /// SpawnableBase non-prism leafPrefab, SpawnableCord healthBlock.
    /// </summary>
    public static class EnvironmentPrismPool
    {
        static Transform s_host;
        static Dictionary<int, Stack<Prism>> s_stacks = new(8);
        static Dictionary<Prism, int> s_issued = new(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_host = null;
            s_stacks = new Dictionary<int, Stack<Prism>>(8);
            s_issued = new Dictionary<Prism, int>(256);
        }

        static void EnsureHost()
        {
            if (s_host) return;
            var go = new GameObject("EnvironmentPrismPool");
            // HideInHierarchy, NOT HideAndDontSave — that flag fights DontDestroyOnLoad
            // (VesselSpeedTunnel.InstallDriver).
            go.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_host = go.transform;
        }

        public static T Get<T>(T prefab) where T : Prism
        {
            return Get(prefab, null);
        }

        public static T Get<T>(T prefab, Transform parent) where T : Prism
        {
            if (!prefab)
                throw new ArgumentNullException(nameof(prefab));
            var instance = Acquire(prefab);
            PrepareForLay(prefab, instance);
            Place(instance, parent, false, default, default);
            return instance;
        }

        public static T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            where T : Prism
        {
            if (!prefab)
                throw new ArgumentNullException(nameof(prefab));
            var instance = Acquire(prefab);
            PrepareForLay(prefab, instance);
            Place(instance, parent, true, position, rotation);
            return instance;
        }

        /// <summary>
        /// Clone <paramref name="count"/> prisms as children of <paramref name="parent"/>.
        /// Drains the inactive stack first (Prepare while inactive), then
        /// InstantiateAsync only the shortfall, then sync-mints any remainder.
        /// A short async result never orphans a partial batch by minting a
        /// second full <paramref name="count"/>.
        /// </summary>
        public static async UniTask<(Prism[] clones, bool batchedFailed)> GetBatchAsync(
            Prism prefab, int count, Transform parent, bool useBatchedInstantiate, float stallSeconds)
        {
            if (!prefab) throw new ArgumentNullException(nameof(prefab));
            if (count <= 0) return (Array.Empty<Prism>(), false);

            var clones = new Prism[count];
            int filled = 0;
            int prefabId = prefab.GetInstanceID();

            while (filled < count && TryPullInactive(prefab, out var reused))
            {
                PrepareForLay(prefab, reused);
                Place(reused, parent, false, default, default);
                clones[filled++] = reused;
            }

            bool batchedFailed = false;
            int shortfall = count - filled;
            if (shortfall > 0 && useBatchedInstantiate)
            {
                try
                {
                    var op = UnityEngine.Object.InstantiateAsync(prefab, shortfall, parent);
                    float waitStart = Time.unscaledTime;
                    while (!op.isDone)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                        if (!parent) return (null, batchedFailed);
                        if (Time.unscaledTime - waitStart > stallSeconds)
                        {
                            CSDebug.LogWarning(
                                $"[EnvironmentPrismPool] Async clone batch ({shortfall} prisms) not " +
                                $"integrated after {stallSeconds:F0}s — forcing WaitForCompletion. " +
                                "The engine's async-instantiate budget is being starved by other loading.");
                            op.WaitForCompletion();
                            break;
                        }
                    }

                    var result = op.Result;
                    if (result != null)
                    {
                        int n = Mathf.Min(result.Length, shortfall);
                        for (int i = 0; i < n; i++)
                        {
                            var clone = result[i];
                            if (!clone) continue;
                            s_issued[clone] = prefabId;
                            PrepareForLay(prefab, clone);
                            clones[filled++] = clone;
                        }
                    }
                }
                catch (Exception ex)
                {
                    batchedFailed = true;
                    CSDebug.LogWarning(
                        $"[EnvironmentPrismPool] Batched InstantiateAsync failed " +
                        $"({ex.GetType().Name}: {ex.Message}) — falling back to per-item cloning.");
                }
            }

            if (!parent && filled < count) return (null, batchedFailed);

            while (filled < count)
            {
                var minted = AcquireMint(prefab);
                PrepareForLay(prefab, minted);
                Place(minted, parent, false, default, default);
                clones[filled++] = minted;
            }

            return (clones, batchedFailed);
        }

        /// <summary>
        /// Return an issued prism to the inactive stack. Never Destroys.
        /// Already-parked issued prisms return true without double-push.
        /// Live mass not in <see cref="s_issued"/> returns false (caller Destroys).
        /// </summary>
        public static bool TryRelease(Prism prism)
        {
            if (!prism) return false;
            if (!s_issued.TryGetValue(prism, out int prefabId)) return false;

            EnsureHost();
            if (!prism.gameObject.activeSelf && prism.transform.parent == s_host)
                return true;

            prism.ClearSuctionClockStamp();
            if (PrismRenderService.IsHandleUsable(in prism.RenderHandle))
                PrismRenderService.ClearPrismStamps(in prism.RenderHandle);

            // worldPositionStays: false so a suctioned localScale is not baked in
            // (same as vessel pooled returns).
            prism.transform.SetParent(s_host, worldPositionStays: false);
            prism.gameObject.SetActive(false);

            if (!s_stacks.TryGetValue(prefabId, out var stack))
            {
                stack = new Stack<Prism>(16);
                s_stacks[prefabId] = stack;
            }
            stack.Push(prism);
            return true;
        }

        public static void ForgetDestroyed(Prism prism)
        {
            if (!prism) return;
            s_issued.Remove(prism);
        }

        static T Acquire<T>(T prefab) where T : Prism
        {
            if (TryPullInactive(prefab, out var reused))
                return reused;
            return AcquireMint(prefab);
        }

        static T AcquireMint<T>(T prefab) where T : Prism
        {
            EnsureHost();
            var minted = UnityEngine.Object.Instantiate(prefab, s_host);
            minted.gameObject.SetActive(false);
            s_issued[minted] = prefab.GetInstanceID();
            return minted;
        }

        static bool TryPullInactive<T>(T prefab, out T instance) where T : Prism
        {
            EnsureHost();
            instance = null;
            int id = prefab.GetInstanceID();
            if (!s_stacks.TryGetValue(id, out var stack)) return false;
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p)
                {
                    instance = (T)p;
                    return true;
                }
            }
            return false;
        }

        static void Place(Prism instance, Transform parent, bool hasWorldPose, Vector3 position, Quaternion rotation)
        {
            instance.transform.SetParent(parent, worldPositionStays: false);
            instance.gameObject.SetActive(true);
            if (hasWorldPose)
                instance.transform.SetPositionAndRotation(position, rotation);
        }

        static void PrepareForLay(Prism prefab, Prism instance)
        {
            var src = prefab.prismProperties;
            var dst = instance.prismProperties;
            if (src != null && dst != null)
            {
                dst.IsDangerous = src.IsDangerous;
                dst.IsShielded = src.IsShielded;
                dst.IsSuperShielded = false;
                dst.IsTransparent = src.IsTransparent;
            }

            var prefabScale = prefab.GetComponent<PrismScaleAnimator>();
            var instanceScale = instance.GetComponent<PrismScaleAnimator>();
            if (prefabScale != null && instanceScale != null)
            {
                instanceScale.RestoreAuthoredScaleWindow();
                var restore = prefabScale.AuthoredTargetScale;
                if (restore == Vector3.zero)
                    restore = prefab.transform.localScale;
                instanceScale.SetTargetScale(restore);
            }

            var team = instance.GetComponent<PrismTeamManager>();
            if (team == null)
            {
                CSDebug.LogError($"[EnvironmentPrismPool] PrepareForLay: PrismTeamManager missing on {instance.name}");
            }
            else
            {
                team.ResetToNeutralForReuse();
            }

            if (instance is HealthPrism hp)
            {
                hp.LifeForm = null;
                hp.OwnerFauna = null;
            }
        }
    }
}
