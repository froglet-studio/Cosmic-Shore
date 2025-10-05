using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles visual + positional explosion effect for prism destruction.
    /// Uses MaterialPropertyBlock to keep prefab-assigned materials intact.
    /// UniTask-based animation with cancellation on disable.
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

        // Pool callback (set by PoolManager)
        public Action<PrismExplosion> OnReturnToPool;

        // UniTask cancellation
        private CancellationTokenSource _cts;

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

            _mpb = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            // Cancel any running animation and clean up overrides
            CancelRunningTask();

            if (_renderer != null && _mpb != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// Fire the explosion animation. Safe-guards NaNs and uses UniTask.
        /// </summary>
        public void TriggerExplosion(Vector3 velocity)
        {
            if (_renderer == null || _mpb == null)
            {
                Debug.LogError("[PrismExplosion] Missing required components, cannot trigger explosion.");
                return;
            }

            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * minSpeed;

            // Restart CTS and run new async animation
            CancelRunningTask();
            _cts = new CancellationTokenSource();
            ExplosionAsync(velocity, _cts.Token).Forget();
        }

        /// <summary>
        /// Public method to immediately return this instance to the pool.
        /// Also reparents under the PoolManager's transform for hierarchy cleanliness.
        /// </summary>
        public void ReturnToPool()
        {
            // Stop animation & clear overrides
            CancelRunningTask();

            if (_renderer != null && _mpb != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }
            
            OnReturnToPool?.Invoke(this);
        }

        private void CancelRunningTask()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        private async UniTaskVoid ExplosionAsync(Vector3 velocity, CancellationToken ct)
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

            try
            {
                while (duration <= maxDuration)
                {
                    ct.ThrowIfCancellationRequested();

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

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Swallow; cleanup happens below
            }

            // Reset overrides
            if (_renderer != null && _mpb != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }

            // Notify pool manager that we’re done (if not canceled by manual ReturnToPool)
            if (!ct.IsCancellationRequested)
            {
                OnReturnToPool?.Invoke(this);
            }
        }
    }
}
