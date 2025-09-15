using System.Collections;
using UnityEngine;

namespace CosmicShore
{
    public class BlockImplosion : MonoBehaviour
    {
        [SerializeField] private Material implosionMaterial;
        [SerializeField] private Renderer blockRenderer;
        [SerializeField] private AnimationCurve implosionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float implosionDuration = 2f;

        private Vector3 convergencePoint;
        private float implosionProgress = 0f;
        private bool isImploding = false;
        private float blockVolume = 1f;

        // Shader property IDs (cache for performance)
        private static readonly int ImplosionProgress = Shader.PropertyToID("_ImplosionProgress");
        private static readonly int ConvergencePoint = Shader.PropertyToID("_ConvergencePoint");

        void Start()
        {
            if (blockRenderer == null)
                blockRenderer = GetComponent<Renderer>();
        }

        public void StartImplosion(Vector3 worldConvergencePoint, float volume = 1f)
        {
            convergencePoint = worldConvergencePoint;
            blockVolume = volume;
            isImploding = true;
            implosionProgress = 0f;

            // Switch to implosion material
            if (implosionMaterial != null && blockRenderer != null)
            {
                blockRenderer.material = implosionMaterial;

                // Set initial shader properties
                blockRenderer.material.SetFloat(ImplosionProgress, 0f);
                blockRenderer.material.SetVector(ConvergencePoint, convergencePoint);
            }

            StartCoroutine(HandleImplosionAnimation());
        }

        private IEnumerator HandleImplosionAnimation()
        {
            float elapsedTime = 0f;
            float adjustedDuration = implosionDuration / Mathf.Sqrt(blockVolume); // Larger blocks take longer

            while (elapsedTime < adjustedDuration && isImploding)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / adjustedDuration;

                // Apply animation curve for non-linear implosion
                implosionProgress = implosionCurve.Evaluate(normalizedTime);

                // Update shader
                if (blockRenderer != null && blockRenderer.material != null)
                {
                    blockRenderer.material.SetFloat(ImplosionProgress, implosionProgress);
                    blockRenderer.material.SetVector(ConvergencePoint, convergencePoint);
                }

                yield return null;
            }

            // Complete implosion
            CompleteImplosion();
        }

        private void CompleteImplosion()
        {
            isImploding = false;
            implosionProgress = 1f;

            // Final shader update
            if (blockRenderer != null && blockRenderer.material != null)
            {
                blockRenderer.material.SetFloat(ImplosionProgress, 1f);
            }

            // Optionally destroy or return to pool after a brief delay
            StartCoroutine(DelayedCleanup());
        }

        private IEnumerator DelayedCleanup()
        {
            yield return new WaitForSeconds(0.5f); // Brief pause to show complete implosion

            // Return to pool or destroy
            gameObject.SetActive(false);
            // Or: Destroy(gameObject);
            // Or: ReturnToPool();
        }

        // Public method to get current progress (useful for other systems)
        public float GetImplosionProgress() => implosionProgress;

        // Method to interrupt implosion if needed
        public void StopImplosion()
        {
            isImploding = false;
            StopAllCoroutines();
        }
    }

}
