using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles prism implosion VFX. Managed by PrismImplosionPoolManager.
    /// </summary>
    public class PrismImplosion : MonoBehaviour
    {
        [SerializeField] private Material implosionMaterial;
        [FormerlySerializedAs("blockRenderer")] 
        [SerializeField] private Renderer prismRenderer;
        [SerializeField] private AnimationCurve implosionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float implosionDuration = 2f;

        private Vector3 convergencePoint;
        private float implosionProgress;
        private bool isImploding;
        private float blockVolume = 1f;

        // Shader property IDs
        private static readonly int ImplosionProgressID = Shader.PropertyToID("_ImplosionProgress");
        private static readonly int ConvergencePointID = Shader.PropertyToID("_ConvergencePoint");

        private Coroutine running;

        /// <summary>
        /// Callback for pooling system when implosion finishes.
        /// </summary>
        public System.Action<PrismImplosion> OnFinished;

        private void Awake()
        {
            if (prismRenderer == null)
                prismRenderer = GetComponent<Renderer>();
        }

        private void OnDisable()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
        }

        public void StartImplosion(Vector3 worldConvergencePoint, float volume = 1f)
        {
            convergencePoint = worldConvergencePoint;
            blockVolume = Mathf.Max(0.01f, volume);
            isImploding = true;
            implosionProgress = 0f;

            if (implosionMaterial != null && prismRenderer != null)
            {
                prismRenderer.material = implosionMaterial;
                prismRenderer.material.SetFloat(ImplosionProgressID, 0f);
                prismRenderer.material.SetVector(ConvergencePointID, convergencePoint);
            }

            if (running != null)
                StopCoroutine(running);

            running = StartCoroutine(HandleImplosionAnimation());
        }

        private IEnumerator HandleImplosionAnimation()
        {
            float elapsedTime = 0f;
            float adjustedDuration = implosionDuration / Mathf.Sqrt(blockVolume);

            while (elapsedTime < adjustedDuration && isImploding)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / adjustedDuration;

                implosionProgress = implosionCurve.Evaluate(normalizedTime);

                if (prismRenderer != null && prismRenderer.material != null)
                {
                    prismRenderer.material.SetFloat(ImplosionProgressID, implosionProgress);
                    prismRenderer.material.SetVector(ConvergencePointID, convergencePoint);
                }

                yield return null;
            }

            CompleteImplosion();
        }

        private void CompleteImplosion()
        {
            isImploding = false;
            implosionProgress = 1f;

            if (prismRenderer != null && prismRenderer.material != null)
            {
                prismRenderer.material.SetFloat(ImplosionProgressID, 1f);
            }

            running = StartCoroutine(DelayedFinish());
        }

        private IEnumerator DelayedFinish()
        {
            yield return new WaitForSeconds(0.5f);
            running = null;
            OnFinished?.Invoke(this); // notify pool manager
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
