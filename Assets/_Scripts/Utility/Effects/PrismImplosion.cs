using System.Collections;
using UnityEngine;
using CosmicShore.Utility.ClassExtensions;

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles prism implosion/grow VFX. Managed by PrismImplosionPoolManager.
    /// Uses MaterialPropertyBlock so prefab materials remain untouched.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class PrismImplosion : MonoBehaviour
    {
        [SerializeField] private Renderer prismRenderer;
        [SerializeField] private float implosionDuration = 2f;
        [SerializeField] private float growDelay = 0.25f; // small pause before expanding

        private MaterialPropertyBlock mpb;
        private Coroutine running;
        private float implosionProgress;

        // Shader property IDs
        private static readonly int ImplosionProgressID = Shader.PropertyToID("_State");
        private static readonly int ConvergencePointID = Shader.PropertyToID("_Location");

        /// <summary> Callback for pooling system when effect finishes. </summary>
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
            if (!prismRenderer)
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

        // ---------------- API ----------------

        /// <summary> Start implosion (shader: 0 → 1). </summary>
        public void StartImplosion(Transform convergenceTransform)
        {
            StartEffect(convergenceTransform.position, 0f, 1f, "Prism implosion started", "Prism implosion ended");
        }

        /// <summary> Start grow (shader: 1 → 0). </summary>
        public void StartGrow(Transform ownerTransform)
        {
            if (!prismRenderer || mpb == null)
            {
                Debug.LogError("[PrismImplosion] Missing required components, cannot start grow.");
                return;
            }

            if (running != null)
                StopCoroutine(running);

            running = StartCoroutine(GrowEffectCoroutine(ownerTransform));

            // DebugExtensions.LogColored("Prism grow started", Color.green);
        }

        // ---------------- Internals ----------------

        private void StartEffect(Vector3 targetPos, float startValue, float endValue, string startMsg, string endMsg)
        {
            if (!prismRenderer || mpb == null)
            {
                Debug.LogError("[PrismImplosion] Missing required components, cannot start effect.");
                return;
            }

            // Reset shader properties
            prismRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ImplosionProgressID, startValue);
            mpb.SetVector(ConvergencePointID, targetPos);
            prismRenderer.SetPropertyBlock(mpb);

            if (running != null)
                StopCoroutine(running);

            running = StartCoroutine(EffectCoroutine(targetPos, startValue, endValue, endMsg));

            // DebugExtensions.LogColored(startMsg, Color.green);
        }

        private IEnumerator EffectCoroutine(Vector3 targetPos, float startValue, float endValue, string endMsg)
        {
            float elapsedTime = 0f;

            while (elapsedTime < implosionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / implosionDuration);

                implosionProgress = Mathf.Lerp(startValue, endValue, t);

                prismRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat(ImplosionProgressID, implosionProgress);
                mpb.SetVector(ConvergencePointID, targetPos);
                prismRenderer.SetPropertyBlock(mpb);

                yield return null;
            }

            // Force final state
            implosionProgress = endValue;
            prismRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ImplosionProgressID, endValue);
            mpb.SetVector(ConvergencePointID, targetPos);
            prismRenderer.SetPropertyBlock(mpb);

            if (running != null)
                StopCoroutine(running);

            running = null;
            OnFinished?.Invoke(this);
        }

        private IEnumerator GrowEffectCoroutine(Transform ownerTransform)
        {
            Vector3 startPosition = ownerTransform.position;
            // Initialize at collapsed state
            prismRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ImplosionProgressID, 1f);
            mpb.SetVector(ConvergencePointID, startPosition);
            prismRenderer.SetPropertyBlock(mpb);

            float elapsedTime = 0f;
            while (elapsedTime < implosionDuration)
            {
                elapsedTime += Time.deltaTime;
                implosionProgress = 1 - Mathf.Clamp01(elapsedTime / implosionDuration);

                prismRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat(ImplosionProgressID, implosionProgress);
                // mpb.SetVector(ConvergencePointID, ownerTransform.position);
                prismRenderer.SetPropertyBlock(mpb);

                yield return null;
            }

            // Force final state (fully grown, placed at start position)
            /*implosionProgress = 0f;
            prismRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ImplosionProgressID, 0f);
            mpb.SetVector(ConvergencePointID, startPosition);
            prismRenderer.SetPropertyBlock(mpb);*/

            if (prismRenderer && mpb != null)
            {
                mpb.Clear();
                prismRenderer.SetPropertyBlock(mpb);
            }
            
            running = null;
            OnFinished?.Invoke(this);
        }

        public float GetImplosionProgress() => implosionProgress;

        public void StopEffect()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
        }
    }
}
