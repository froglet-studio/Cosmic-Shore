using System;
using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Physics-driven ball for Astro League.
    /// Ships collide with it to push it toward goals, like Rocket League.
    /// Attach to a sphere with a Rigidbody and SphereCollider.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class AstroLeagueBall : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] float maxSpeed = 120f;
        [SerializeField] float hitForceMultiplier = 2f;
        [SerializeField] float drag = 0.3f;
        [SerializeField] float bounciness = 0.8f;

        [Header("Reset")]
        [SerializeField] float resetDelay = 1.5f;

        Rigidbody rb;
        Vector3 spawnPosition;
        bool isResetting;

        public event Action<Domains> OnGoalScored;

        public Vector3 Velocity => rb.linearVelocity;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = drag;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            spawnPosition = transform.position;
        }

        void FixedUpdate()
        {
            if (rb.linearVelocity.magnitude > maxSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (isResetting) return;

            var otherRb = collision.rigidbody;
            if (otherRb == null) return;

            // Apply hit force based on the colliding object's velocity
            Vector3 hitDirection = (transform.position - collision.contacts[0].point).normalized;
            float impactSpeed = otherRb.linearVelocity.magnitude;
            Vector3 force = hitDirection * impactSpeed * hitForceMultiplier;

            rb.AddForce(force, ForceMode.Impulse);
        }

        /// <summary>
        /// Called by AstroLeagueGoal when the ball enters a goal zone.
        /// </summary>
        public void NotifyGoalScored(Domains scoringTeam)
        {
            if (isResetting) return;
            isResetting = true;

            OnGoalScored?.Invoke(scoringTeam);

            // Freeze the ball briefly, then reset to center
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Invoke(nameof(ResetToCenter), resetDelay);
        }

        void ResetToCenter()
        {
            transform.position = spawnPosition;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            isResetting = false;
        }

        public void SetSpawnPosition(Vector3 position)
        {
            spawnPosition = position;
        }

        public void FullReset()
        {
            CancelInvoke();
            isResetting = false;
            ResetToCenter();
        }
    }
}
