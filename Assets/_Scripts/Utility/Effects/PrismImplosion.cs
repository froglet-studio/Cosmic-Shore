using System.Collections;
using CosmicShore.Utility.ClassExtensions;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles prism implosion VFX. Managed by PrismImplosionPoolManager.
    /// Uses MaterialPropertyBlock so prefab materials remain untouched.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class PrismImplosion : MonoBehaviour
    {
        [SerializeField] private Renderer prismRenderer;

        [SerializeField] private float implosionDuration = 2f;

        private MaterialPropertyBlock mpb;
        private Transform convergenceTransform;
        private float implosionProgress;
        private bool isImploding;

        private Coroutine running;

        // Shader property IDs (cache for performance)
        private static readonly int ImplosionProgressID = Shader.PropertyToID("_State");
        private static readonly int ConvergencePointID = Shader.PropertyToID("_Location");

        /// <summary>
        /// Callback for pooling system when implosion finishes.
        /// </summary>
        public System.Action<PrismImplosion> OnFinished;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!prismRenderer)
                prismRenderer = GetComponent<Renderer>();
        }
#endif

        private void Awake()
        {
            if (prismRenderer == null)
                prismRenderer = GetComponent<Renderer>();

            mpb = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            if (prismRenderer != null && mpb != null)
            {
                mpb.Clear();
                prismRenderer.SetPropertyBlock(mpb);
            }
        }

        public void StartImplosion(Transform worldConvergenceTransform)
        {
            if (!prismRenderer || mpb == null)
            {
                Debug.LogError("[PrismImplosion] Missing required components, cannot start implosion.");
                return;
            }

            convergenceTransform = worldConvergenceTransform;
            isImploding = true;
            implosionProgress = 0f;

            prismRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ImplosionProgressID, 0f);
            mpb.SetVector(ConvergencePointID,convergenceTransform.position);
            prismRenderer.SetPropertyBlock(mpb);

            if (running != null)
                StopCoroutine(running);

            running = StartCoroutine(HandleImplosionAnimation());
            
            DebugExtensions.LogColored("Prism implosion started", Color.green);
        }

        private IEnumerator HandleImplosionAnimation()
        {
            float elapsedTime = 0f;

            while (elapsedTime < implosionDuration && isImploding)
            {
                elapsedTime += Time.deltaTime;
                implosionProgress = Mathf.Clamp01(elapsedTime / implosionDuration);

                if (!prismRenderer || mpb == null)
                    yield break;

                prismRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat(ImplosionProgressID, implosionProgress);
                mpb.SetVector(ConvergencePointID, convergenceTransform.position);
                prismRenderer.SetPropertyBlock(mpb);

                yield return null;
            }

            CompleteImplosion();
        }

        private void CompleteImplosion()
        {
            isImploding = false;
            implosionProgress = 1f;

            if (prismRenderer && mpb != null)
            {
                prismRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat(ImplosionProgressID, 1f);
                prismRenderer.SetPropertyBlock(mpb);
            }

            if (running != null)
                StopCoroutine(running);

            running = StartCoroutine(DelayedFinish());
        }

        private IEnumerator DelayedFinish()
        {
            yield return new WaitForSeconds(0.5f);
            running = null;
            OnFinished?.Invoke(this); // notify pool manager
            DebugExtensions.LogColored("Prism implosion ended", Color.red);
        }

        public float GetImplosionProgress() => implosionProgress;

        public void StopImplosion()
        {
            isImploding = false;
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
        }
    }
}
