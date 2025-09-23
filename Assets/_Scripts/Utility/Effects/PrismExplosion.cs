using System.Collections;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles visual + positional explosion effect for prism destruction.
    /// Uses MaterialPropertyBlock to keep prefab-assigned materials intact.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class PrismExplosion : MonoBehaviour
    {
        [SerializeField] 
        private float minSpeed = 30f;

        [SerializeField] 
        private float maxSpeed = 250f;

        [SerializeField] 
        private MeshRenderer _renderer;

        private MaterialPropertyBlock _mpb;
        private Coroutine _running;

        // Callback so the pool manager can reclaim this instance
        public System.Action<PrismExplosion> OnFinished;

        // Cache shader property IDs for performance
        private static readonly int VelocityID = Shader.PropertyToID("_Velocity");
        private static readonly int ExplosionAmountID = Shader.PropertyToID("_ExplosionAmount");
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!_renderer)
                _renderer = GetComponent<MeshRenderer>();
        }
#endif

        private void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponent<MeshRenderer>();

            // Always create a MPB instance
            _mpb = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            if (_renderer && _mpb != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        public void TriggerExplosion(Vector3 velocity)
        {
            if (!_renderer || _mpb == null)
            {
                Debug.LogError("[PrismExplosion] Missing required components, cannot trigger explosion.");
                return;
            }

            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * minSpeed;

            if (_running != null)
                StopCoroutine(_running);

            _running = StartCoroutine(ExplosionCoroutine(velocity));
        }

        private IEnumerator ExplosionCoroutine(Vector3 velocity)
        {
            // Clamp velocity and calculate speed
            float speed;
            velocity = GeometryUtils.ClampMagnitude(velocity, minSpeed, maxSpeed, out speed);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(VelocityID, velocity);
            _renderer.SetPropertyBlock(_mpb);

            Vector3 initialPosition = transform.position;
            const float maxDuration = 7f;
            float duration = 0f;

            while (duration <= maxDuration)
            {
                duration += Time.deltaTime;

                // Update position
                Vector3 newPosition = initialPosition + duration * velocity;
                if (!float.IsNaN(newPosition.x) && !float.IsNaN(newPosition.y) && !float.IsNaN(newPosition.z))
                    transform.position = newPosition;

                // Update shader overrides
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(ExplosionAmountID, speed * duration);
                _mpb.SetFloat(OpacityID, 1f - (duration / maxDuration));
                _renderer.SetPropertyBlock(_mpb);

                yield return null;
            }

            // Reset overrides
            if (_renderer && _mpb != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }

            _running = null;
            OnFinished?.Invoke(this); // Notify pool manager
        }
    }
}
