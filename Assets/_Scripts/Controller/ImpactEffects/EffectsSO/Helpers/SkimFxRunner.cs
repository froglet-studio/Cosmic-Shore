using System.Threading;
using CosmicShore.Gameplay;
using Cysharp.Threading.Tasks;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
{
    // ------------------------------------------------------------
    // Small internal helper: spawns & updates skim FX, then cleans up.
    // Lifetime is scaled by vessel speed: progress += speed * deltaTime
    // so total duration ~= particleDurationAtSpeedOne / speed.
    // ------------------------------------------------------------
    internal static class SkimFxRunner
    {
        // Prefab names already reported, so a dense trail of unauthored prisms logs once, not once
        // per contact. Names (not instances) - the pool recycles the objects.
        static readonly System.Collections.Generic.HashSet<string> _warnedMissingFx = new();

        static void WarnMissingSkimFxOnce(Prism prism)
        {
            string key = prism.name;
            if (!_warnedMissingFx.Add(key)) return;
            Debug.LogWarning(
                $"[SkimFxRunner] Prism prefab '{key}' has no ParticleEffect assigned - skimming it " +
                "produces no beam. Assign one on the prism prefab to give this mass skim feedback.",
                prism);
        }

        public static async UniTaskVoid RunAsync(
            IVesselStatus vesselStatus,
            Prism prism,
            float particleDurationAtSpeedOne)
        {
            if (vesselStatus == null || !prism)
                return;

            var shipTransform = vesselStatus.ShipTransform;
            if (!shipTransform)
                return;

            // Not every prism prefab authors a skim beam - MenuTrailBlock Variant, FloraBlock and
            // ShieldedHealthBlock all leave ParticleEffect empty. Instantiate(null) THROWS, and this
            // runs once per prism entering the skimmer, so skimming that mass turned into an
            // exception per contact with no visual either way. Name the prefab once and draw
            // nothing rather than failing silently in a swallowed UniTaskVoid.
            if (!prism.ParticleEffect)
            {
                WarnMissingSkimFxOnce(prism);
                return;
            }

            // Ends when the prism is destroyed. (A linked source used to be made per contact here, only
            // ever cancelled right before a `break` - the prism's own token does the same, unallocated.)
            var token = prism.GetCancellationTokenOnDestroy();

            var prefab = prism.ParticleEffect;
            var particle = SkimFxPool.Get(prefab, prism.transform);
            try
            {
                float progress = 0f;

                while (!token.IsCancellationRequested)
                {
                    // Explicit null-check: the ship or the prism can go between frames.
                    if (shipTransform == null || prism == null)
                        break;

                    float speed = Mathf.Max(0f, vesselStatus.Speed);

                    if (speed <= 0.0001f)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    Vector3 distance = prism.transform.position - shipTransform.position;
                    particle.transform.localScale = new Vector3(1f, 1f, distance.magnitude);
                    if (SafeLookRotation.TryGet(distance, prism.transform.up, out var rotation, prism, logError: false))
                        particle.transform.SetPositionAndRotation(shipTransform.position, rotation);
                    else
                        particle.transform.position = shipTransform.position;

                    progress += speed * Time.deltaTime;
                    if (progress >= particleDurationAtSpeedOne)
                        break;

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                // A beam on a live prism goes back to the pool; one whose prism is gone dies with it, as
                // every beam used to.
                if (particle)
                {
                    if (prism) SkimFxPool.Release(prefab, particle);
                    else Object.Destroy(particle);
                }
            }
        }
    }

    // ------------------------------------------------------------
    // Skim beams, recycled per prefab. Every prism a skimmer touches spawns one for a fraction of
    // a second - a Squirrel racing touches ~2 a frame - and a synchronous Instantiate + Destroy per
    // contact measured ~0.33 ms a frame with 1.3 ms spikes (prof, Skim Race I2, 2026-10-06).
    // A reused beam is placed exactly as Instantiate(prefab, prism, instantiateInWorldSpace: true)
    // places a new one, and its particles are cleared before it is switched back on, so its
    // play-on-awake emission starts from nothing like a fresh copy's.
    // ------------------------------------------------------------
    internal static class SkimFxPool
    {
        const int MaxPooledPerPrefab = 32;
        static readonly System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.Stack<GameObject>> s_free = new();
        static Transform s_root;

        // Enter Play Mode without a domain reload keeps statics; the pooled objects did not survive.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_free.Clear();
            s_root = null;
        }

        public static GameObject Get(GameObject prefab, Transform parent)
        {
            if (s_free.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0)
                {
                    var go = stack.Pop();
                    if (!go) continue; // destroyed out from under the pool
                    // Instantiate(prefab, parent, true) puts the copy at the prefab root's pose IN WORLD
                    // space and keeps its scale: the same pose, set unparented, then parented keeping world.
                    var t = go.transform;
                    var src = prefab.transform;
                    t.SetParent(null, false);
                    t.SetPositionAndRotation(src.localPosition, src.localRotation);
                    t.localScale = src.localScale;
                    t.SetParent(parent, true);
                    if (go.TryGetComponent(out ParticleSystem ps)) ps.Clear(true);
                    go.SetActive(true);
                    return go;
                }
            }
            return Object.Instantiate(prefab, parent, true);
        }

        public static void Release(GameObject prefab, GameObject instance)
        {
            if (!s_free.TryGetValue(prefab, out var stack))
            {
                stack = new System.Collections.Generic.Stack<GameObject>(8);
                s_free[prefab] = stack;
            }
            if (stack.Count >= MaxPooledPerPrefab)
            {
                Object.Destroy(instance);
                return;
            }
            instance.SetActive(false);
            instance.transform.SetParent(Root, false);
            stack.Push(instance);
        }

        static Transform Root
        {
            get
            {
                if (s_root) return s_root;
                var go = new GameObject("[Skim FX Pool]");
                Object.DontDestroyOnLoad(go);
                s_root = go.transform;
                return s_root;
            }
        }
    }

}