using System.Collections;
using UnityEngine;
using CosmicShore.Core; // for PooledObject

namespace CosmicShore.Game
{
    public class BlockImpact : MonoBehaviour
    {
        [SerializeField] private float minSpeed = 30f;
        [SerializeField] private float maxSpeed = 250f;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private PooledObject _pooled;
        private Coroutine _running;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _pooled = GetComponent<PooledObject>(); // attached by PoolManagerBase on instantiate
            _mpb = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            // Stop any in-flight coroutine if object is disabled mid-flight
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            // Optional: clear per-instance overrides when disabled
            if (_renderer != null && _mpb != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        public void HandleImpact(Vector3 velocity)
        {
            // Validate velocity
            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * minSpeed;

            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(ImpactCoroutine(velocity));
        }

        private IEnumerator ImpactCoroutine(Vector3 velocity)
        {
            if (_renderer == null || _mpb == null)
                yield break;

            // Clamp magnitude and extract speed
            float speed;
            velocity = GeometryUtils.ClampMagnitude(velocity, minSpeed, maxSpeed, out speed);

            // Initial property setup
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetVector("_velocity", velocity);
            _renderer.SetPropertyBlock(_mpb);

            Vector3 initialPosition = transform.position;
            const float maxDuration = 7f;
            float duration = 0f;

            while (duration <= maxDuration && this != null && _renderer != null)
            {
                duration += Time.deltaTime;

                // New position with NaN guard
                Vector3 newPosition = initialPosition + duration * velocity;
                if (!float.IsNaN(newPosition.x) && !float.IsNaN(newPosition.y) && !float.IsNaN(newPosition.z))
                    transform.position = newPosition;

                // Update shader properties via MPB
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_ExplosionAmount", speed * duration);
                _mpb.SetFloat("_opacity", 1f - (duration / maxDuration));
                _renderer.SetPropertyBlock(_mpb);

                yield return null;
            }

            // Clear overrides when finished (optional)
            if (_renderer != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }

            // Return to pool via metadata (no tag needed)
            if (_pooled != null && _pooled.Manager != null)
                _pooled.Manager.ReturnToPool(gameObject);

            _running = null;
        }
    }
}
