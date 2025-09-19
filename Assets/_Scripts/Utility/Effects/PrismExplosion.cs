using System.Collections;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles visual + positional explosion effect for prism destruction.
    /// </summary>
    public class PrismExplosion : MonoBehaviour
    {
        [SerializeField] private float minSpeed = 30f;
        [SerializeField] private float maxSpeed = 250f;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Coroutine _running;
        
        public System.Action<PrismExplosion> OnFinished; // callback for pool manager

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            if (_renderer != null && _mpb != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        public void TriggerExplosion(Vector3 velocity)
        {
            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * minSpeed;

            if (_running != null)
                StopCoroutine(_running);
            
            if (!_renderer.sharedMaterial.HasProperty("_ExplosionAmount"))
                Debug.LogError("Shader missing property: _ExplosionAmount");

            if (!_renderer.sharedMaterial.HasProperty("_Opacity"))
                Debug.LogError("Shader missing property: _Opacity");

            _running = StartCoroutine(ExplosionCoroutine(velocity));
        }

        private IEnumerator ExplosionCoroutine(Vector3 velocity)
        {
            if (_renderer == null || _mpb == null)
                yield break;

            // Clamp velocity and calculate speed
            float speed;
            velocity = GeometryUtils.ClampMagnitude(velocity, minSpeed, maxSpeed, out speed);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetVector("_Velocity", velocity);
            _renderer.SetPropertyBlock(_mpb);

            Vector3 initialPosition = transform.position;
            const float maxDuration = 7f;
            float duration = 0f;

            while (duration <= maxDuration && this != null && _renderer != null)
            {
                duration += Time.deltaTime;

                Vector3 newPosition = initialPosition + duration * velocity;
                if (!float.IsNaN(newPosition.x) && !float.IsNaN(newPosition.y) && !float.IsNaN(newPosition.z))
                    transform.position = newPosition;

                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_ExplosionAmount", speed * duration);
                _mpb.SetFloat("_Opacity", 1f - (duration / maxDuration));
                _renderer.SetPropertyBlock(_mpb);

                yield return null;
            }

            // Reset
            if (_renderer != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }

            _running = null;
            OnFinished?.Invoke(this); // notify pool manager
        }
    }
}
