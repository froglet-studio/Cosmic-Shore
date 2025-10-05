using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Utility.ClassExtensions; // if you still need it elsewhere

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles prism implosion/grow VFX. Managed by PrismImplosionPoolManager.
    /// Uses MaterialPropertyBlock so prefab materials remain untouched.
    /// UniTask-based animations with cancellation.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class PrismImplosion : MonoBehaviour
    {
        [SerializeField] private Renderer prismRenderer;
        [SerializeField] private float implosionDuration = 2f;
        [SerializeField] private float growDelay = 0.25f; // small pause before expanding

        private MaterialPropertyBlock mpb;

        // UniTask cancellation
        private CancellationTokenSource cts;

        private float implosionProgress;
        
        /// <summary> Callback for pooling system when effect finishes. </summary>
        public Action<PrismImplosion> OnReturnToPool;

        // Shader property IDs
        private static readonly int ImplosionProgressID = Shader.PropertyToID("_State");
        private static readonly int ConvergencePointID = Shader.PropertyToID("_Location");

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!prismRenderer)
                prismRenderer = GetComponent<Renderer>();
        }
#endif

        private void Awake()
        {
            if (!prismRenderer)
                prismRenderer = GetComponent<Renderer>();

            mpb = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            // cancel any running tasks
            CancelRunning();
            // clear overrides
            if (prismRenderer != null && mpb != null)
            {
                mpb.Clear();
                prismRenderer.SetPropertyBlock(mpb);
            }
        }

        // ---------------- API ----------------

        /// <summary> Start implosion (shader: 0 → 1). </summary>
        public void StartImplosion(Transform convergenceTransform)
        {
            if (!prismRenderer || mpb == null)
            {
                Debug.LogError("[PrismImplosion] Missing required components, cannot start implosion.");
                return;
            }

            CancelRunning();
            cts = new CancellationTokenSource();

            var targetPos = convergenceTransform.position;

            // Reset shader properties
            prismRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ImplosionProgressID, 0f);
            mpb.SetVector(ConvergencePointID, targetPos);
            prismRenderer.SetPropertyBlock(mpb);

            ImplosionAsync(targetPos, cts.Token).Forget();
        }

        /// <summary> Start grow (shader: 1 → 0). </summary>
        public void StartGrow(Transform ownerTransform)
        {
            if (!prismRenderer || mpb == null)
            {
                Debug.LogError("[PrismImplosion] Missing required components, cannot start grow.");
                return;
            }

            CancelRunning();
            cts = new CancellationTokenSource();

            GrowAsync(ownerTransform, cts.Token).Forget();
        }

        /// <summary>
        /// Immediately stop any animation, clear overrides, reparent under pool root, and return to pool.
        /// </summary>
        public void ReturnToPool()
        {
            CancelRunning();

            if (prismRenderer && mpb != null)
            {
                mpb.Clear();
                prismRenderer.SetPropertyBlock(mpb);
            }

            OnReturnToPool?.Invoke(this);
        }

        public float GetImplosionProgress() => implosionProgress;

        /// <summary>Externally stop (cancels async op, but does not auto-return).</summary>
        public void StopEffect() => CancelRunning();

        // ---------------- Internals ----------------

        private void CancelRunning()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }

        private async UniTaskVoid ImplosionAsync(Vector3 targetPos, CancellationToken ct)
        {
            float elapsed = 0f;

            try
            {
                while (elapsed < implosionDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / implosionDuration);

                    implosionProgress = Mathf.Lerp(0f, 1f, t);

                    prismRenderer.GetPropertyBlock(mpb);
                    mpb.SetFloat(ImplosionProgressID, implosionProgress);
                    mpb.SetVector(ConvergencePointID, targetPos);
                    prismRenderer.SetPropertyBlock(mpb);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // cleanup happens below; do not release to pool on cancel
            }

            // Force final state if not canceled
            if (!ct.IsCancellationRequested)
            {
                implosionProgress = 1f;

                prismRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat(ImplosionProgressID, 1f);
                mpb.SetVector(ConvergencePointID, targetPos);
                prismRenderer.SetPropertyBlock(mpb);

                // finished normally -> return to pool
                OnReturnToPool?.Invoke(this);
            }
        }

        private async UniTaskVoid GrowAsync(Transform ownerTransform, CancellationToken ct)
        {
            // initialize at collapsed state
            Vector3 startPosition = ownerTransform.position;
            prismRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ImplosionProgressID, 1f);
            mpb.SetVector(ConvergencePointID, startPosition);
            prismRenderer.SetPropertyBlock(mpb);

            try
            {
                // optional delay before expanding
                if (growDelay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(growDelay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                float elapsed = 0f;
                while (elapsed < implosionDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;
                    implosionProgress = 1f - Mathf.Clamp01(elapsed / implosionDuration);

                    prismRenderer.GetPropertyBlock(mpb);
                    mpb.SetFloat(ImplosionProgressID, implosionProgress);
                    // If you need to keep following the owner during grow, uncomment:
                    // mpb.SetVector(ConvergencePointID, ownerTransform.position);
                    prismRenderer.SetPropertyBlock(mpb);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // cleanup happens below; do not release to pool on cancel
            }

            // finished (not canceled): clear overrides and return to pool
            if (!ct.IsCancellationRequested)
            {
                if (prismRenderer && mpb != null)
                {
                    mpb.Clear();
                    prismRenderer.SetPropertyBlock(mpb);
                }

                OnReturnToPool?.Invoke(this);
            }
        }
    }
}
