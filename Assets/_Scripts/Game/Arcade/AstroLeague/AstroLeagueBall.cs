using System;
using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Physics-driven ball for Astro League.
    /// Ships collide with it to push it toward goals.
    /// Self-illuminates with a point light and leaves a speed trail.
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

        [Header("Visuals")]
        [SerializeField] Color ballColor = new(1f, 0.85f, 0.3f, 1f);
        [SerializeField] float lightRange = 40f;
        [SerializeField] float lightIntensity = 2f;
        [SerializeField] float trailTime = 0.4f;
        [SerializeField] float trailWidth = 2f;

        Rigidbody rb;
        Vector3 spawnPosition;
        bool isResetting;
        Light ballLight;
        TrailRenderer trail;

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

            SetupVisuals();
        }

        void SetupVisuals()
        {
            // Emissive ball material
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = ballColor;
                mat.SetColor("_EmissionColor", ballColor * 2f);
                mat.EnableKeyword("_EMISSION");
                renderer.material = mat;
            }

            // Point light so the ball illuminates surroundings
            ballLight = gameObject.AddComponent<Light>();
            ballLight.type = LightType.Point;
            ballLight.color = ballColor;
            ballLight.range = lightRange;
            ballLight.intensity = lightIntensity;
            ballLight.shadows = LightShadows.None;

            // Speed trail
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = trailWidth;
            trail.endWidth = 0.1f;
            trail.material = new Material(Shader.Find("Sprites/Default")) { color = ballColor };
            trail.startColor = ballColor;
            trail.endColor = new Color(ballColor.r, ballColor.g, ballColor.b, 0f);
            trail.minVertexDistance = 0.5f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.generateLightingData = false;
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

            if (trail != null) trail.Clear();
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
