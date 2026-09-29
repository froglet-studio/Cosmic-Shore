using System;
using System.Collections;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Drifts a spent-crystal husk along the collector's course while fading it
    /// out, then returns it to its pool (or destroys it when spawned unpooled).
    /// Shader state is animated per-renderer via MaterialPropertyBlock over the
    /// SHARED material — callers pass the crystal's exploding material as-is and
    /// must never clone it (this replaces the legacy new-Material-per-impact
    /// pattern that allocated and destroyed a material every pickup).
    /// </summary>
    public class Impact : MonoBehaviour
    {
        static readonly int PlayerID = Shader.PropertyToID("_player");
        static readonly int RedID = Shader.PropertyToID("_red");
        static readonly int VelocityID = Shader.PropertyToID("_velocity");
        static readonly int OpacityID = Shader.PropertyToID("_Opacity");

        public float positionScale;
        public float maxDistance = 3f;

        /// <summary>Raised when the drift completes; the owning pool subscribes on checkout.</summary>
        public event Action<Impact> OnReturnToPool;

        Renderer _renderer;
        MaterialPropertyBlock _mpb;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        public void HandleImpact(Vector3 velocity, Material material, string ID)
        {
            _renderer.sharedMaterial = material;

            // Mirror the legacy per-clone flag writes exactly: unset properties
            // fall back to the shared material's authored values.
            _mpb.Clear();
            if (ID == "Player")
                _mpb.SetFloat(PlayerID, 1);
            else if (ID == "red")
            {
                _mpb.SetFloat(PlayerID, 0);
                _mpb.SetFloat(RedID, 1);
            }
            else
            {
                _mpb.SetFloat(PlayerID, 0);
                _mpb.SetFloat(RedID, 0);
            }
            _renderer.SetPropertyBlock(_mpb);

            StartCoroutine(ImpactCoroutine(velocity));
        }

        IEnumerator ImpactCoroutine(Vector3 velocity)
        {
            var velocityScale = .07f / positionScale;
            Vector3 distance = Vector3.zero;

            velocity = velocity.sqrMagnitude < 2f ? Vector3.one * 2 : velocity;
            float maxDistanceSqr = maxDistance * maxDistance;
            while (distance.sqrMagnitude <= maxDistanceSqr)
            {
                yield return null;
                distance += velocityScale * Time.deltaTime * velocity;
                _mpb.SetVector(VelocityID, distance);
                _mpb.SetFloat(OpacityID, Mathf.Clamp(1 - (distance.magnitude / maxDistance), 0, 1));
                _renderer.SetPropertyBlock(_mpb);
                transform.position += positionScale * distance;
            }

            // Pooled instances hand themselves back (the pool deactivates the
            // GameObject, which also stops this coroutine); unpooled fallbacks
            // keep the legacy self-destroy.
            if (OnReturnToPool != null)
                OnReturnToPool.Invoke(this);
            else
                Destroy(gameObject);
        }
    }
}
